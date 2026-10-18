using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.CameraModes;
using Project.ContactCubes;
using Project.Items;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit silent Play audit; disposable contact fixtures are never saved.</summary>
public static class R2GameplayVerification
{
    private const string Flag = "ILorR.R2.GameplayAudit";
    private const string VisualFlag = "ILorR.R2.VisualAudit";
    private static int stage, checks;
    private static float stageTime, releasedProgress;
    private static double deadline;
    private static PlayerController player;
    private static CrankPlatformFeature crank;
    private static MovingPlatformFeature lift;
    private static DoorController door;
    private static ContactCube upper, entrance, middleRed, entranceRed;
    private static CollectiblePickup innerPickup;
    private static PlantObstacleFeature[] plants;
    private static GameObject blueFixture, redFixture;
    private static IDisposable controlLock;
    private static RigidbodyConstraints oldConstraints;
    private static Vector3 liftStart, upperStart, climbStart;
    private static CameraControlHandle visualHandle;
    private static EditorWindow gameView;
    private static object gameViewGroup;
    private static PropertyInfo selectedSize;
    private static int originalSize, temporarySize = -1;

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').EndsWith("/2026TapTapGameJam-R2/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent isolated R2 worktree only");
        EditorSceneManager.OpenScene(R2LevelBuilder.ScenePath);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    public static void RenderAudit() { SessionState.SetBool(VisualFlag,true); Run(); }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        stage = checks = 0; deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new Exception("R2 Play stage " + stage + ": " + message);
    }
    private static void Next() { stage++; stageTime = Time.time; }
    private static T Find<T>(string id) where T : Component => Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None)
        .Single(p => p.PlanningBoxId == "r2_" + id).GetComponent<T>();
    private static void MoveTo(Vector3 point)
    {
        player.Motor.linearVelocity = Vector3.zero;
        player.Motor.position = point; player.transform.position = point; Physics.SyncTransforms();
    }
    private static void StepOn(int index) => MoveTo(door.Buttons[index].transform.position + Vector3.up * .015f);
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("R2 Play timed out at stage " + stage);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < 1f) return;
            float elapsed = Time.time - stageTime;
            if (stage == 100)
            {
                if (elapsed < 1f) return;
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/R2GameView.png")); stage=101; stageTime=Time.time; return;
            }
            if (stage == 101)
            {
                if (elapsed < 1f) return;
                Check(File.Exists("Library/R2GameView.png"),"real Game View frame saved");
                visualHandle.Dispose(); RestoreGameView();
                EditorApplication.update -= Tick; SessionState.EraseBool(Flag); SessionState.EraseBool(VisualFlag);
                Debug.Log("R2 visual capture complete, actual 1856x1152 Game View. No Scene saved."); EditorApplication.Exit(0); return;
            }
            if (stage == 0)
            {
                var colors = ColorRuntimeService.Instance;
                Check(new[]{"red","green","blue"}.All(colors.IsUnlocked), "all scene colors unlocked");
                Check(colors.IsAbilityUnlocked("blue") && !colors.IsAbilityUnlocked("red") && !colors.IsAbilityUnlocked("green"), "blue-only player ability");
                player = Object.FindFirstObjectByType<PlayerController>();
                Check(!player.IsDead && Vector3.Distance(player.transform.position,new Vector3(57.5f,-19.5f,0)) < .2f, "safe BK115 spawn");
                Check(Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None).All(c => c.Color != CubeColor.Blue), "delivered scene contains no blue cube");
                upper = Find<ContactCube>("cube_green_upper"); entrance = Find<ContactCube>("cube_green_entrance"); middleRed = Find<ContactCube>("cube_red_middle"); entranceRed = Find<ContactCube>("cube_red_entrance");
                Check(upper.Length == 1 && upper.MaximumLength == 11 && upper.GetComponent<ContactCubeMotion>().IsAuthoredFixed, "fixed AK114 root starts one, max eleven");
                Check(!upper.GetComponent<CarryableFeature>().enabled && upper.GetComponent<Rigidbody>().constraints == RigidbodyConstraints.FreezeAll, "AK114 immovable and not carryable");
                Check(entrance.Length == 1 && entrance.MaximumLength == 9 && !entrance.GetComponent<ContactCubeMotion>().IsAuthoredFixed, "ordinary BG115 starts one, max nine");
                Check(Vector3.Distance(entrance.transform.position,new Vector3(53.5f,-19.5f,0)) < .025f, "ordinary BG115 rests on its authored ledge");
                upperStart = upper.transform.position;
                door = Object.FindFirstObjectByType<DoorController>();
                Check(door.State == DoorState.Locked && door.Buttons.Count == 3 && door.Buttons.All(b => b.Persistent && !b.IsPressed), "three unpressed latched buttons lock door");
                plants = Object.FindObjectsByType<PlantObstacleFeature>(FindObjectsSortMode.None);
                Check(plants.Length == 8 && plants.All(p => p.IsIntact), "eight intact authored plant cells");
                innerPickup = Find<CollectiblePickup>("collectible_inner");
                Check(!innerPickup.IsCollected && Vector3.Distance(innerPickup.transform.position,new Vector3(36.5f,-22.5f,0)) < .05f, "AP118 collectible preserved in hole");
                if (SessionState.GetBool(VisualFlag,false))
                {
                    ConfigureGameView();
                    visualHandle=Object.FindFirstObjectByType<CameraControlManager>().RequestControl(new OverviewSource(),CameraControlPriorities.Debug,transition:CameraTransition.Immediate);
                    stage=100; stageTime=Time.time; return;
                }
                player.GetComponent<PlayerInputDriver>().enabled = false;
                controlLock = player.AcquireControlLock(typeof(R2GameplayVerification));
                oldConstraints = player.Motor.constraints; player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                crank = Find<CrankPlatformFeature>("crank"); lift = Find<MovingPlatformFeature>("lift"); liftStart = lift.transform.position;
                Check(Mathf.Abs(liftStart.y + 22.5f) < .001f, "platform starts on row118");
                MoveTo(crank.transform.position + Vector3.right);
                Check(crank.TryInteract(player.gameObject), "nearby player operates crank"); Next();
            }
            else if (stage == 1)
            {
                crank.HoldInteract(player.gameObject);
                if (elapsed < 3.4f) return;
                Check(crank.Progress > .999f && Mathf.Abs(lift.transform.position.y - liftStart.y - 10) < .05f, "turning reaches row108 exactly");
                crank.EndInteract(player.gameObject); releasedProgress = crank.Progress; Next();
            }
            else if (stage == 2)
            {
                if (elapsed < 1.5f) return;
                Check(crank.Progress < releasedProgress - .02f && lift.transform.position.y < liftStart.y + 9.8f, "release makes platform descend");
                StepOn(0); Next();
            }
            else if (stage == 3)
            {
                if (elapsed < .25f) return;
                Check(door.Buttons[0].IsPressed && !door.Buttons[1].IsPressed && door.State == DoorState.Locked, "real foot contact latches first button, not exit");
                MoveTo(door.Buttons[0].transform.position + Vector3.right * 2 + Vector3.up * .15f); Next();
            }
            else if (stage == 4)
            {
                if (elapsed < .3f) return;
                Check(door.Buttons[0].IsPressed, "button remains pressed after player leaves"); StepOn(1); Next();
            }
            else if (stage == 5)
            {
                if (elapsed < .25f) return;
                Check(door.Buttons.Take(2).All(b => b.IsPressed) && !door.Buttons[2].IsPressed && door.State == DoorState.Locked,
                    "two buttons still cannot open exit; " + string.Join(";",door.Buttons.Select(b => b.name+":"+b.IsPressed+" active="+b.isActiveAndEnabled+" pos="+b.transform.position+" collider="+b.GetComponent<BoxCollider>().enabled+" bounds="+b.GetComponent<BoxCollider>().bounds)) + "; player="+player.transform.position+" feet="+player.GetComponent<CapsuleCollider>().bounds.min.y+" state="+door.State);
                // A 13-cell test teleport is not a playable fall. Respawn through
                // the official API first, preserving the two latched buttons.
                player.RequestRespawn(); StepOn(2); Next();
            }
            else if (stage == 6)
            {
                if (elapsed < 1.2f) return;
                Check(door.State == DoorState.Unlocked && door.Buttons.All(b => b.IsPressed), "third real foot contact opens AND exit");
                Check(plants[0].ApplyColor("red",middleRed.gameObject), "existing red-object application burns plant group"); Next();
            }
            else if (stage == 7)
            {
                if (elapsed < 1.1f) return;
                Check(plants.All(p => !p.IsIntact && p.GetComponentsInChildren<Collider>().All(c => !c.enabled)), "all eight orthogonally connected plant cells burn away");
                MoveTo(innerPickup.transform.position); Next();
            }
            else if (stage == 8)
            {
                if (elapsed < 1.5f) return;
                Check(innerPickup.IsCollected, "real player trigger collects AP118; dead="+player.IsDead+" requested="+innerPickup.CollectionRequested+" player="+player.transform.position+" item="+innerPickup.transform.position);
                MoveTo(new Vector3(57.5f,-19.5f,0)); RoomLifecycleManager.ResetCurrentRooms(); Next();
            }
            else if (stage == 9)
            {
                if (elapsed < .6f) return;
                Check(door.State == DoorState.Unlocked && door.Buttons.All(b => b.IsPressed), "room reset preserves latched door progress");
                Check(plants.All(p => p.IsIntact), "room reset restores eight plants");
                Check(innerPickup.IsCollected, "collected reward does not return on reset");
                Check(crank.Progress < .001f && Vector3.Distance(lift.transform.position,liftStart) < .01f, "room reset restores crank and platform");
                Check(upper.Length == 1 && Vector3.Distance(upper.transform.position,upperStart) < .001f, "fixed root never drifted");
                // Temporary blue contacts prove default reaction semantics only.
                // They are never written into the delivered Scene or planning.
                middleRed.GetComponent<ContactCubeMotion>().SetCarrySuspended(true);
                entranceRed.GetComponent<ContactCubeMotion>().SetCarrySuspended(true);
                blueFixture = new GameObject("Disposable blue contact audit"); blueFixture.SetActive(false);
                MakeCube("Blue",upperStart + Vector3.left,blueFixture);
                MakeCube("Blue",entrance.transform.position + Vector3.right,blueFixture);
                blueFixture.SetActive(true); Physics.SyncTransforms(); Next();
            }
            else if (stage == 10)
            {
                if (elapsed < 11.5f) return;
                Check(upper.Length == 11 && upper.GrowthDirection == Vector3.right, "temporary blue contact grows AK114 right to AU114");
                Check(entrance.Length == 9 && entrance.GrowthDirection == Vector3.left, "temporary blue contact grows BG115 left to AY115; length="+entrance.Length+" dir="+entrance.GrowthDirection.ToString("F6")+" state="+entrance.State+" pos="+entrance.transform.position.ToString("F6"));
                Check(Vector3.Distance(upper.transform.position,upperStart) < .001f && upper.transform.lossyScale == Vector3.one, "growth holds fixed anchor without root scaling");
                var bounds = upper.GetComponentInChildren<BoxCollider>().bounds;
                Check(Mathf.Abs(bounds.min.x-31) < .01f && Mathf.Abs(bounds.max.x-42) < .01f, "eleven-cell physical footprint is exact");
                Object.DestroyImmediate(blueFixture); blueFixture = null;
                controlLock.Dispose(); controlLock = null; player.Motor.constraints = oldConstraints;
                MoveTo(new Vector3(34.5f,-19.515f,0)); Next();
            }
            else if (stage == 11)
            {
                if (elapsed < .35f) return;
                Check(player.IsHorizontalClimbing, "player attaches to real underside of fixed grown green cube");
                climbStart = player.transform.position; Next();
            }
            else if (stage == 12)
            {
                player.SetMoveInput(Vector2.right);
                if (elapsed < .5f) return;
                Check(player.transform.position.x > climbStart.x + .4f && Mathf.Abs(player.transform.position.y-climbStart.y) < .15f, "horizontal traverse keeps underside height");
                player.SetMoveInput(Vector2.zero); player.RequestJump(); Next();
            }
            else if (stage == 13)
            {
                if (elapsed < .15f) return;
                Check(!player.IsHorizontalClimbing, "jump releases fixed green underside");
                controlLock = player.AcquireControlLock(typeof(R2GameplayVerification)); player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                MoveTo(new Vector3(57.5f,-19.5f,0));
                redFixture = new GameObject("Disposable red contact audit"); redFixture.SetActive(false);
                MakeCube("Red",upperStart+Vector3.left,redFixture); redFixture.SetActive(true); Physics.SyncTransforms(); Next();
            }
            else if (stage == 14)
            {
                if (elapsed < 11.5f) return;
                Check(upper.Length == 1 && Vector3.Distance(upper.transform.position,upperStart) < .001f, "red contact shrinks fixed root to one without movement");
                Check(entrance.Length == 9 && !entrance.GetComponent<ContactCubeMotion>().IsAuthoredFixed, "ordinary green retains length without contact and stays nonfixed");
                Object.DestroyImmediate(redFixture); redFixture = null;
                middleRed.GetComponent<ContactCubeMotion>().SetCarrySuspended(false);
                entranceRed.GetComponent<ContactCubeMotion>().SetCarrySuspended(false);
                RoomLifecycleManager.ResetCurrentRooms(); Next();
            }
            else if (stage == 15)
            {
                if (elapsed < .5f) return;
                Check(upper.Length == 1 && entrance.Length == 1, "reset restores initial length for both greens");
                Check(upper.GetComponent<ContactCubeMotion>().IsAuthoredFixed && !entrance.GetComponent<ContactCubeMotion>().IsAuthoredFixed, "reset preserves authored fixed versus ordinary physics");
                var colors = ColorRuntimeService.Instance;
                Check(colors.IsAbilityUnlocked("blue") && !colors.IsAbilityUnlocked("red") && !colors.IsAbilityUnlocked("green"), "reset preserves intended abilities");
                Check(Object.FindObjectsByType<ContactCube>(FindObjectsSortMode.None).All(c => c.Color != CubeColor.Blue), "no test blue cubes remain");
                Finish(true,$"PASS {checks} assertions in actual Play: safe spawn/colors, real three-button foot triggers and AND gate, crank full rise/release, red plant burn and exact eight-cell restore, real collectible trigger/persistence, temporary-blue growth to both specified endpoints, fixed-anchor physical footprint, real underside attach/traverse/jump, red shrink, room resets. No full entrance-to-exit route or player-blue growth claimed. Temporary contacts never saved. Visual audit is separate: Library/R2GameView.png.");
            }
        }
        catch (Exception e) { Finish(false,e.ToString()); }
    }
    private static void MakeCube(string color,Vector3 position,GameObject parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ContactCubes/"+color+"_ContactCube.prefab");
        Check(prefab != null,"temporary contact prefab exists");
        var instance = Object.Instantiate(prefab,position,Quaternion.identity,parent.transform);
        instance.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        instance.GetComponent<CarryableFeature>().enabled = false;
    }
    private static void ConfigureGameView()
    {
        var editor = typeof(EditorWindow).Assembly; var viewType = editor.GetType("UnityEditor.GameView",true);
        gameView = EditorWindow.GetWindow(viewType);
        selectedSize = viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        originalSize = (int)selectedSize.GetValue(gameView);
        var sizesType = editor.GetType("UnityEditor.GameViewSizes",true);
        var sizes = sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        gameViewGroup = sizesType.GetProperty("currentGroup",BindingFlags.Instance|BindingFlags.Public).GetValue(sizes);
        temporarySize = (int)gameViewGroup.GetType().GetMethod("GetTotalCount").Invoke(gameViewGroup,null);
        var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize",true),
            Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType",true),"FixedResolution"),1856,1152,"R2 visual audit");
        gameViewGroup.GetType().GetMethod("AddCustomSize").Invoke(gameViewGroup,new[]{size});
        selectedSize.SetValue(gameView,temporarySize); gameView.Show(); gameView.Repaint();
    }
    private static void RestoreGameView()
    {
        if (gameViewGroup == null) return;
        selectedSize.SetValue(gameView,originalSize);
        gameViewGroup.GetType().GetMethod("RemoveCustomSize").Invoke(gameViewGroup,new object[]{temporarySize});
        gameViewGroup = null;
    }
    private sealed class OverviewSource : ICameraControlSource, ICameraLensOverrideSource
    {
        public string CameraControlName => "R2 audit overview";
        public bool TryGetCameraState(in CameraControlContext context,out CameraState state)
        {
            state = new CameraState { position = new Vector3(29,-17,-80), rotation = Quaternion.identity,
                projection = CameraProjectionMode.Orthographic, orthographicSize = 18, fieldOfView = 60 }; return true;
        }
        public bool TryGetLensOverride(out float orthographicSize,out float fieldOfView) { orthographicSize=18;fieldOfView=60;return true; }
    }
    private static void Finish(bool success,string report)
    {
        EditorApplication.update -= Tick; SessionState.EraseBool(Flag); SessionState.EraseBool(VisualFlag);
        visualHandle.Dispose(); RestoreGameView();
        if (crank != null && player != null) crank.EndInteract(player.gameObject);
        controlLock?.Dispose(); controlLock = null;
        if (player != null) player.Motor.constraints = oldConstraints;
        File.WriteAllText("Library/R2GameplayVerification.txt",report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
