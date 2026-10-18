using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit disposable Play audit. Never saves the scene or test poses.</summary>
public static class R3GameplayVerification
{
    private const string Flag="ILorR.R3.PlayAudit", Visual="ILorR.R3.PlayVisual";
    private static int stage,checks,cubeIndex,growthIndex;
    private static float since,released;
    private static double deadline;
    private static PlayerController player;
    private static CrankPlatformFeature crank;
    private static MovingPlatformFeature lift;
    private static DoorController door;
    private static PlatformMount[] mounts;
    private static ContactCube[] roots,cubes;
    private static Vector3 liftStart,dropPosition,ordinaryStart;
    private static Vector3[] anchors;
    private static RigidbodyConstraints constraints;
    private static CarryableFeature carrying;
    private static CameraControlHandle cameraLease;
    private static AuditCamera cameraSource;
    private static EditorWindow gameView;
    private static object gameViewGroup;
    private static PropertyInfo selectedSize;
    private static int originalSize,temporarySize;
    private static GameObject ordinaryFixture;
    private static readonly Vector3 Spawn=new Vector3(2.5f,-21.5f,0);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').EndsWith("/2026TapTapGameJam-R3/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new Exception("Silent isolated R3 worktree only");
        R3LevelBuilder.FinalAudit();
        EditorSceneManager.OpenScene(R3LevelBuilder.ScenePath);
        SessionState.SetBool(Flag,true); Resume(); EditorApplication.EnterPlaymode();
    }
    public static void RenderAudit() { SessionState.SetBool(Visual,true); Run(); }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag,false)) return;
        stage=checks=cubeIndex=growthIndex=0; deadline=EditorApplication.timeSinceStartup+240;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static void Check(bool value,string message)
    { checks++; if(!value) throw new Exception("R3 Play stage "+stage+": "+message); }
    private static void Go(int value) { stage=value; since=Time.time; File.WriteAllText("Library/R3PlayProgress.txt","Stage "+stage+"; checks "+checks+"; game time "+Time.time); }
    private static T Find<T>(string id) where T:Component => Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None)
        .Single(p=>p.PlanningBoxId=="r3_"+id).GetComponent<T>();
    private static void Move(Vector3 p)
    { player.Motor.linearVelocity=Vector3.zero; player.Motor.position=p; player.transform.position=p; Physics.SyncTransforms(); }
    private static void MountsFollow()
    {
        foreach(var mount in mounts.Where(m=>m.IsMounted))
        {
            var body=mount.GetComponent<Rigidbody>();
            Check(Vector3.Distance(mount.transform.position,mount.TargetPosition)<.0625f,"mount follows within one artwork pixel "+mount.name+" delta="+(mount.transform.position-mount.TargetPosition).ToString("F6")+" target="+mount.TargetPosition.ToString("F6")+" body="+(body!=null?body.position.ToString("F6")+" interp="+body.interpolation+" constraints="+body.constraints:"none")+" progress="+crank.Progress);
        }
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("R3 audit timeout stage "+stage);
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.time<1f) return;
            float elapsed=Time.time-since;
            if(stage==0)
            {
                player=Object.FindFirstObjectByType<PlayerController>();
                var color=ColorRuntimeService.Instance;
                Check(new[]{"red","green","blue"}.All(color.IsUnlocked),"three scene colors enabled");
                Check(color.IsAbilityUnlocked("blue")&&!color.IsAbilityUnlocked("red")&&!color.IsAbilityUnlocked("green"),"blue-only player ability");
                Check(!player.IsDead&&Vector3.Distance(player.transform.position,Spawn)<.12f,"Z96 idle spawn safe, actual "+player.transform.position);
                player.GetComponent<PlayerInputDriver>().enabled=false; constraints=player.Motor.constraints;
                lift=Find<MovingPlatformFeature>("lift"); crank=Find<CrankPlatformFeature>("crank"); liftStart=lift.transform.position;
                Check(Vector3.Distance(liftStart,new Vector3(63,-22.5f,0))<.001f,"platform starts row97, above original lava");
                mounts=Object.FindObjectsByType<PlatformMount>(FindObjectsSortMode.None);
                Check(mounts.Length==3&&mounts.All(m=>m.IsMounted&&m.Platform==lift),"three independent authored mounts");
                anchors=mounts.Select(m=>m.transform.position-liftStart).ToArray(); MountsFollow();
                roots=R3LevelBuilder.ReadSource().growingCubes.Select(s=>Find<ContactCube>(s.id)).ToArray();
                Check(roots.Length==4&&roots.All(c=>c.Length==1&&c.GetComponent<ContactCubeMotion>().IsAuthoredFixed&&!c.GetComponent<CarryableFeature>().enabled),"four fixed noncarryable green roots");
                cubes=new[]{Find<ContactCube>("cube_red"),Find<ContactCube>("cube_blue")};
                door=Find<DoorController>("exit");
                Check(door.State==DoorState.Locked&&door.Buttons.Count==2&&door.Buttons.All(b=>b.Persistent&&!b.IsPressed),"two unpressed latched buttons lock exit");
                Check(Object.FindObjectsByType<LavaHazardFeature>(FindObjectsSortMode.None).Length==5,"all five original lava regions intact at startup, no automatic cooling");
                if(SessionState.GetBool(Visual,false)) { ConfigureGameView(); SetCamera(new Vector3(40.5f,-13.5f,-80),28); Go(1); }
                else StartLift();
            }
            else if(stage==1)
            {
                if(elapsed<1f)return; Capture("Overview"); Go(3);
            }
            else if(stage==3)
            {if(elapsed<.15f)return;SetCamera(new Vector3(63,-18,-80),9);Go(2);}
            else if(stage==2)
            { if(elapsed<1f)return; Capture("LiftBottom"); Go(4); }
            else if(stage==4)
            {if(elapsed<.15f)return;StartLift();}
            else if(stage==10)
            {
                if(elapsed<.3f)return;
                Check(!player.IsDead&&player.IsGrounded&&lift.HasStandingPlayer,"real player stands on CH96 above original lava; pos="+player.transform.position);
                Check(crank.TryInteract(player.gameObject),"real nearby crank interaction"); Go(11);
            }
            else if(stage==11)
            {
                crank.HoldInteract(player.gameObject); MountsFollow();
                Check(!player.IsDead,"player survives authored lift route");
                float feet=player.GetComponent<Collider>().bounds.min.y+player.Motor.position.y-player.transform.position.y;
                Check(Mathf.Abs(feet-lift.GetComponent<Collider>().bounds.max.y)<.16f,"real player carried on actual thin platform surface, without pose forcing; t="+elapsed+" physics="+player.Motor.position+" lift="+lift.transform.position+" feet="+feet+" surface="+lift.GetComponent<Collider>().bounds.max.y+" grounded="+player.IsGrounded+" rider="+lift.HasStandingPlayer);
                Check(Vector3.Distance(player.transform.position,crank.transform.position)<1.26f,"moving crank stays within interaction reach");
                if(elapsed<3.4f)return;
                Check(crank.Progress>.999f&&Mathf.Abs(lift.transform.position.y-liftStart.y-9)<.025f,"full9-cell rise reaches row88");
                if(SessionState.GetBool(Visual,false)) Capture("LiftTop");
                crank.EndInteract(player.gameObject); released=crank.Progress; Go(12);
            }
            else if(stage==12)
            {
                MountsFollow(); if(elapsed<1.5f)return;
                Check(!player.IsDead&&crank.Progress<released-.1f&&lift.transform.position.y<liftStart.y+8,"release descends with real rider");
                Move(Spawn); player.Motor.constraints=RigidbodyConstraints.FreezeAll;
                RoomLifecycleManager.ResetCurrentRooms(); Go(20);
            }
            else if(stage==20)
            {
                if(elapsed<.4f)return;
                Check(crank.Progress<.001f&&Vector3.Distance(lift.transform.position,liftStart)<.01f,"room reset returns platform to row97");
                Check(mounts.All(m=>m.IsMounted&&!m.IsDetached),"reset reattaches all three"); MountsFollow();
                crank.SetSignal(true,player); Go(21);
            }
            else if(stage==21)
            {
                if(elapsed<.12f)return;
                var mount=cubes[cubeIndex].GetComponent<PlatformMount>();
                carrying=cubes[cubeIndex].GetComponent<CarryableFeature>();
                Check(!carrying.BeginCarry(null)&&!mount.IsDetached,"rejected pickup leaves mount intact");
                Move(cubes[cubeIndex].transform.position+Vector3.right);
                Check(carrying.BeginCarry(player),"real carry API succeeds for "+cubes[cubeIndex].Color);
                Check(mount.IsDetached&&!mount.IsMounted,"successful pickup detaches "+cubes[cubeIndex].Color);
                Move(Spawn); carrying.UpdateCarryAnchor(player.CarryAnchorWorldPosition); Go(22);
            }
            else if(stage==22)
            {
                carrying.UpdateCarryAnchor(player.CarryAnchorWorldPosition); if(elapsed<.35f)return;
                bool found=false;
                for(int x=1;x<=5;x++)
                {
                    var candidate=Spawn+Vector3.right*x;
                    if(!carrying.CanPlaceCarryAt(player,candidate))continue;
                    Check(carrying.BeginCarryDrop(player,candidate),"real drop starts on clear authored floor"); found=true; break;
                }
                Check(found,"clear drop destination exists near spawn"); Go(23);
            }
            else if(stage==23)
            {
                if(elapsed<.8f)return;
                Check(!carrying.IsCarryActive&&!carrying.IsCarryDropAnimating,"drop completed");
                dropPosition=cubes[cubeIndex].transform.position; crank.SetSignal(false,player); Go(24);
            }
            else if(stage==24)
            {
                if(elapsed<.6f)return;
                Check(cubes[cubeIndex].GetComponent<PlatformMount>().IsDetached&&Vector3.Distance(cubes[cubeIndex].transform.position,dropPosition)<.08f,"dropped cube no longer follows platform");
                Check(crank.GetComponent<PlatformMount>().IsMounted,"crank remains mounted");
                RoomLifecycleManager.ResetCurrentRooms(); cubeIndex++;
                if(cubeIndex<2)Go(20);else Go(30);
            }
            else if(stage==30)
            {
                if(elapsed<.4f)return; MountsFollow();
                Check(mounts.All(m=>m.IsMounted),"both carry/drop cases reset to mounted author poses");
                Move(door.Buttons[0].transform.position+Vector3.up*.015f); Go(31);
            }
            else if(stage==31)
            {
                if(elapsed<.3f)return;
                Check(door.Buttons[0].IsPressed&&!door.Buttons[1].IsPressed&&door.State==DoorState.Locked,"first real foot press latches but cannot open exit");
                Move(Spawn); Go(32);
            }
            else if(stage==32)
            {
                if(elapsed<.25f)return;
                Check(door.Buttons[0].IsPressed,"button remains pressed after leaving");
                Move(door.Buttons[1].transform.position+Vector3.up*.015f); Go(33);
            }
            else if(stage==33)
            {
                if(elapsed<1.2f)return;
                Check(door.Buttons.All(b=>b.IsPressed)&&door.State==DoorState.Unlocked,"second real foot press opens AND door");
                Move(Spawn); RoomLifecycleManager.ResetCurrentRooms(); Go(40);
            }
            else if(stage==40)
            {
                if(elapsed<.3f)return;
                Check(door.State==DoorState.Unlocked&&door.Buttons.All(b=>b.IsPressed),"reset preserves latched door progress");
                var root=roots[growthIndex]; var spec=R3LevelBuilder.ReadSource().growingCubes[growthIndex];
                var a=R3LevelBuilder.ExcelRect(spec.column,spec.row); var b=R3LevelBuilder.ExcelRect(spec.targetColumn,spec.targetRow);
                var dir=new Vector3(Math.Sign(b.x-a.x),-Math.Sign(b.y-a.y),0);
                Move(root.transform.position+(dir.y==0?-dir:dir)*2);
                Check(player.GetComponent<PlayerColorWheel>().TrySelectColor("blue"),"blue selected");
                var target=root.GetComponent<BlueContactCubeGrowthTarget>();
                var shape=root.GetComponentInChildren<BoxCollider>();
                Check(target.ApplyColorAt("blue",player.gameObject,shape.ClosestPoint(root.transform.position)),"real blue hit receiver requests "+spec.id);
                Move(Spawn); Go(41);
            }
            else if(stage==41)
            {
                var root=roots[growthIndex]; if(elapsed<root.MaximumLength+1f)return;
                var spec=R3LevelBuilder.ReadSource().growingCubes[growthIndex];
                var target=R3LevelBuilder.ExcelRect(spec.targetColumn,spec.targetRow);
                var bounds=root.GetComponentInChildren<BoxCollider>().bounds;
                Check(root.Length==root.MaximumLength,"growth reaches specified limit "+spec.id+" length="+root.Length+"/"+root.MaximumLength+" state="+root.State+" dir="+root.GrowthDirection);
                Check(bounds.Contains(new Vector3(target.x+.5f,-target.y-.5f,0)),"grown footprint includes target "+spec.targetColumn+spec.targetRow);
                Check(root.GetComponent<ContactCubeMotion>().IsAuthoredFixed,"growth retains fixed author constraints");
                RoomLifecycleManager.ResetCurrentRooms(); growthIndex++;
                if(growthIndex<roots.Length)Go(40);else Go(50);
            }
            else if(stage==50)
            {
                if(elapsed<.4f)return;
                Check(roots.All(c=>c.Length==1),"room reset restores four one-cell roots");
                ordinaryStart=Spawn+Vector3.right*4+Vector3.up*4;
                ordinaryFixture=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab"),ordinaryStart,Quaternion.identity);
                Check(ordinaryFixture.GetComponent<PlatformMount>()==null,"ordinary prefab has no optional mount"); Go(51);
            }
            else if(stage==51)
            {
                if(elapsed<1.2f)return;
                var motion=ordinaryFixture.GetComponent<ContactCubeMotion>();
                Check(!motion.IsAuthoredFixed&&!ordinaryFixture.GetComponent<Rigidbody>().isKinematic&&ordinaryFixture.GetComponent<Rigidbody>().interpolation==RigidbodyInterpolation.Interpolate&&ordinaryFixture.transform.position.y<ordinaryStart.y-.5f,"unmounted ordinary cube still falls normally and preserves interpolation; actual="+ordinaryFixture.transform.position);
                Object.DestroyImmediate(ordinaryFixture);ordinaryFixture=null;
                Check(ColorRuntimeService.Instance.IsAbilityUnlocked("blue")&&!ColorRuntimeService.Instance.IsAbilityUnlocked("red")&&!ColorRuntimeService.Instance.IsAbilityUnlocked("green"),"intended abilities survive resets");
                for(int i=0;i<mounts.Length;i++)Check(Vector3.Distance(mounts[i].transform.position-lift.transform.position,anchors[i])<.02f,"final author offset restored");
                Finish(true,"PASS "+checks+" actual Play assertions: safe Z96 spawn/colors; real rider/crank 9-cell rise/release above lava; per-frame mount following; red and blue successful carry/drop detachment, rejected pickup stays attached, room-reset reattachment; all five original lava regions intact at startup; two real foot-trigger latched AND buttons/reset persistence; four blue-hit green growth endpoints and resets; ordinary unmounted cube gravity regression. Test teleports and debug SetSignal used only for carry/growth fixtures. No full route completion or package build claimed.");
            }
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    private static void StartLift() { Move(new Vector3(62.5f,-21.5f,0)); Go(10); }
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/R3GameView-"+name+".png"));
    private static void SetCamera(Vector3 position,float size)
    {
        cameraLease.Dispose(); cameraSource=new AuditCamera{position=position,size=size};
        cameraLease=Object.FindFirstObjectByType<CameraControlManager>().RequestControl(cameraSource,CameraControlPriorities.Debug,transition:CameraTransition.Immediate);
    }
    private sealed class AuditCamera:ICameraControlSource,ICameraLensOverrideSource
    {
        public Vector3 position; public float size;
        public string CameraControlName=>"R3 visual audit";
        public bool TryGetCameraState(in CameraControlContext context,out CameraState state)
        {state=new CameraState{position=position,rotation=Quaternion.identity,projection=CameraProjectionMode.Orthographic,orthographicSize=size,fieldOfView=60};return true;}
        public bool TryGetLensOverride(out float orthographicSize,out float fieldOfView){orthographicSize=size;fieldOfView=60;return true;}
    }
    private static void ConfigureGameView()
    {
        var editor=typeof(EditorWindow).Assembly;var type=editor.GetType("UnityEditor.GameView",true);gameView=EditorWindow.GetWindow(type);
        selectedSize=type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);originalSize=(int)selectedSize.GetValue(gameView);
        var sizesType=editor.GetType("UnityEditor.GameViewSizes",true);var sizes=sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        gameViewGroup=sizesType.GetProperty("currentGroup",BindingFlags.Instance|BindingFlags.Public).GetValue(sizes);
        temporarySize=(int)gameViewGroup.GetType().GetMethod("GetTotalCount").Invoke(gameViewGroup,null);
        var size=Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize",true),Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType",true),"FixedResolution"),1856,1152,"R3 audit");
        gameViewGroup.GetType().GetMethod("AddCustomSize").Invoke(gameViewGroup,new[]{size});selectedSize.SetValue(gameView,temporarySize);gameView.Show();gameView.Repaint();
    }
    private static void Finish(bool success,string report)
    {
        EditorApplication.update-=Tick;SessionState.EraseBool(Flag);SessionState.EraseBool(Visual);cameraLease.Dispose();
        if(gameViewGroup!=null){selectedSize.SetValue(gameView,originalSize);gameViewGroup.GetType().GetMethod("RemoveCustomSize").Invoke(gameViewGroup,new object[]{temporarySize});gameViewGroup=null;}
        if(crank!=null&&player!=null)crank.EndInteract(player.gameObject);
        if(player!=null)player.Motor.constraints=constraints;
        File.WriteAllText("Library/R3GameplayVerification.txt",report);if(success)Debug.Log(report);else Debug.LogError(report);EditorApplication.Exit(success?0:1);
    }
}
