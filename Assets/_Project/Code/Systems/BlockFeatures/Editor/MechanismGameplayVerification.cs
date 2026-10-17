using System;
using System.IO;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.BlockFeatures.Editor
{
    public static class MechanismGameplayVerification
    {
        private static int phase, checks;
        private static float deadline, startY, peak, recordedProgress;
        private static double started;
        private static ContactCube red, green, blue;
        private static MechanismBaseFeature source;
        private static MovingPlatformFeature platform;
        private static CrankPlatformFeature crank;
        private static MovingPlatformFeature secondCrankPlatform;
        private static PlayerController player;
        private static BouncePlantFeature plant;
        private static Vector3 stopped;
        private static void Check(bool value,string label) { checks++; if (!value) throw new Exception(label); }
        private static GameObject Spawn(string path,Vector3 position) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),position,Quaternion.identity);
        private static ContactCube Cube(CubeColor color,Vector3 position)
        {
            var value = Spawn($"Assets/_Project/Content/ContactCubes/{color}_ContactCube.prefab",position).GetComponent<ContactCube>();
            value.GetComponent<ContactCubeMotion>().enabled = false; value.GetComponent<Rigidbody>().isKinematic = true;
            return value;
        }
        private static void Move(ContactCube cube,Vector3 value) { cube.transform.position = cube.GetComponent<Rigidbody>().position = value; Physics.SyncTransforms(); }
        private static PlayerController Player(Vector3 pos)
        {
            var value = Spawn("Assets/_Project/Content/Player/Player.prefab",pos).GetComponent<PlayerController>();
            value.GetComponent<PlayerInputDriver>().enabled = false;
            return value;
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("隔离验证入口");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool("MechanismVerification",true); Resume(); EditorApplication.EnterPlaymode();
        }
        public static void VerifyAuthoredPlayers()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("隔离验证入口");
            checks = 0;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Test.unity");
                var avatar = GameObject.Find("TestPlayer");
                Check(avatar != null, "Test avatar exists");
                Bounds body = avatar.GetComponent<CapsuleCollider>().bounds;
                Check(Mathf.Abs(body.size.x-1)<.001f && Mathf.Abs(body.size.y-1)<.001f, "Test authored collider is one cell");
                Bounds visual = avatar.GetComponentInChildren<Renderer>().bounds;
                Check(Mathf.Abs(visual.size.x-1)<.001f && Mathf.Abs(visual.size.y-1)<.001f, "Test authored model is one cell");
                Check(new SerializedObject(avatar.GetComponent<PlayerController>()).FindProperty("jumpRiseCells").floatValue == 4, "Test jump is four-cell rise");
                Check(Object.FindObjectsByType<EnergyBlockFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, "Test has no obsolete energy feature");
                EditorSceneManager.OpenScene("Assets/Scenes/Pick.unity");
                avatar = GameObject.Find("TestPlayer");
                Check(avatar != null && avatar.GetComponent<MeshFilter>().sharedMesh != null && Mathf.Abs(avatar.GetComponent<MeshFilter>().sharedMesh.bounds.size.y-1)<.001f, "Pick prefab uses one-cell model");
                File.WriteAllText("Library/MechanismAuthoredPlayers.txt", "PASS " + checks);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/MechanismAuthoredPlayers.txt", error.ToString()); Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool("MechanismVerification",false)) return;
            phase = checks = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Next(int step,float seconds) { phase = step; deadline = Time.time + seconds; }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup-started > 120) throw new Exception("timeout phase " + phase);
                if (!EditorApplication.isPlaying) return;
                if (player != null && (phase == 701 || phase == 8 || phase == 11 || phase == 14 || phase == 18)) peak = Mathf.Max(peak,player.Motor.position.y);
                if (Time.time < deadline) return;
                if (phase == 0)
                {
                    red = Cube(CubeColor.Red,Vector3.right); green = Cube(CubeColor.Green,Vector3.right*50); blue = Cube(CubeColor.Blue,Vector3.right*60);
                    source = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab",Vector3.zero).GetComponent<MechanismBaseFeature>();
                    platform = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab",Vector3.right*10).GetComponent<MovingPlatformFeature>();
                    platform.gameObject.AddComponent<RoomResetObject>(); platform.BindBase(source);
                    Check(!red.IsUnlocked && !green.IsUnlocked && !blue.IsUnlocked,"all cubes locked at start");
                    Check(!red.IsEnergyAvailable && !red.GetComponent<CarryableFeature>().CanCarryNow,"locked cube cannot power/carry");
                    Check(!source.IsSignalActive,"locked base inactive");
                    ColorRuntimeService.Instance.Unlock("red");
                    Check(red.IsUnlocked && !blue.IsUnlocked && !green.IsUnlocked,"key only unlocks its color");
                    Check(red.IsEnergyAvailable,"red implements energy source");
                    Check(!((object)red is IColorApplicationTarget),"cube is not click recoloring target");
                    ColorRuntimeService.Instance.Unlock("blue"); ColorRuntimeService.Instance.Unlock("green");
                    Check(green.IsUnlocked && blue.IsUnlocked,"other keys unlock remaining cubes");
                    var legacy = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Battery.prefab",new Vector3(200,0,0));
                    Check(legacy.GetComponent<ContactCube>() != null && legacy.GetComponent<EnergyBlockFeature>() == null && legacy.GetComponent<ContactCubeMotion>() != null,"legacy energy prefab upgraded in place");
                    Next(1,.35f); return;
                }
                if (phase == 1)
                {
                    Check(source.IsSignalActive,"any red powers base"); Check(platform.transform.position.x > 10.01f,"signal drives platform");
                    Physics.SyncTransforms();
                    Collider surface = platform.GetComponent<Collider>();
                    player = Player(new Vector3(surface.bounds.center.x,surface.bounds.max.y+.5f,0));
                    Next(101,.4f); return;
                }
                if (phase == 101)
                {
                    Check(Mathf.Abs(player.Motor.position.x-platform.transform.position.x)<.15f,"signal platform carries standing player delta=" + (player.Motor.position.x-platform.transform.position.x) + " y=" + player.Motor.position.y);
                    Object.Destroy(player.gameObject);
                    Move(red,Vector3.right*30); Next(2,.12f); return;
                }
                if (phase == 2) { Check(!source.IsSignalActive,"remove red removes energy"); stopped = platform.transform.position; Next(3,.3f); return; }
                if (phase == 3)
                {
                    Check(Vector3.Distance(stopped,platform.transform.position)<.001f,"unpowered platform stops");
                    platform.GetComponent<RoomResetObject>().RestorePose(); platform.GetComponent<RoomResetObject>().RestoreState();
                    Check(Vector3.Distance(platform.transform.position,Vector3.right*10)<.001f,"platform resets authored pose");
                    var alternative = Cube(CubeColor.Red,Vector3.right); Check(alternative.IsEnergyAvailable,"second arbitrary red supports base");
                    crank = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Crank.prefab",new Vector3(40,0,0)).GetComponent<CrankPlatformFeature>();
                    var independent = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_CrankLift.prefab",new Vector3(40,1.25f,0)).GetComponent<MovingPlatformFeature>();
                    independent.BindCrank(crank);
                    secondCrankPlatform = Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Red_CrankLift.prefab",new Vector3(43,1.25f,0)).GetComponent<MovingPlatformFeature>();
                    secondCrankPlatform.BindCrank(crank);
                    player = Player(new Vector3(40.8f,0,0)); player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                    Check(crank.LiftPlatform != null,"crank has actual lift model");
                    Check(crank.TryInteract(player.gameObject),"existing interaction operates crank");
                    Next(4,.4f); return;
                }
                if (phase == 4)
                {
                    Check(crank.Progress>.05f && crank.LiftPlatform.localPosition.y>1.25f,"held crank raises platform");
                    Check(crank.Platforms.Count == 2 && crank.LiftPlatform.parent != crank.transform && crank.transform.childCount == 0,"independent crank controls two sibling platforms");
                    Check(Mathf.Abs(crank.LiftPlatform.position.y-secondCrankPlatform.transform.position.y)<.001f,"both platforms follow same progress");
                    crank.EndInteract(player.gameObject); recordedProgress = crank.Progress;
                    Next(5,.8f); return;
                }
                if (phase == 5)
                {
                    Check(crank.Progress<recordedProgress,"idle crank descends");
                    crank.SetSignal(true,crank);
                    Physics.SyncTransforms();
                    Collider lift = crank.LiftPlatform.GetComponent<Collider>();
                    player.transform.position = player.Motor.position = new Vector3(lift.bounds.center.x,lift.bounds.max.y+.5f,0);
                    player.Motor.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
                    player.Motor.linearVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    recordedProgress = crank.Progress; Next(105,.8f); return;
                }
                if (phase == 105)
                {
                    Check(recordedProgress-crank.Progress>.1f,"standing player accelerates descent " + (recordedProgress-crank.Progress) + " rider="+crank.HasStandingPlayer+" feet="+player.GetComponent<Collider>().bounds.min.y+" top="+crank.LiftPlatform.GetComponent<Collider>().bounds.max.y);
                    Check(Mathf.Abs(player.GetComponent<Collider>().bounds.min.y-crank.LiftPlatform.GetComponent<Collider>().bounds.max.y)<.15f,"descending crank keeps rider on top");
                    crank.BindBase(source); recordedProgress = crank.Progress; Next(6,.3f); return;
                }
                if (phase == 6)
                {
                    Check(source.IsSignalActive && Mathf.Approximately(crank.Progress,recordedProgress),"powered base brakes crank");
                    crank.ResetForRoom(); Check(crank.Progress==0f && Mathf.Abs(crank.LiftPlatform.localPosition.y-1.25f)<.001f,"crank reset");
                    Check(Mathf.Abs(secondCrankPlatform.transform.position.y-1.25f)<.001f,"reset restores both independent platforms");
                    Object.Destroy(player.gameObject);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(100,-.5f,0); floor.transform.localScale=new Vector3(20,1,4);
                    Spawn("Assets/_Project/Content/Mechanisms/OneWayPlatform.prefab",new Vector3(100,2,0));
                    var hidden = Spawn("Assets/_Project/Content/Mechanisms/HiddenBlock.prefab",Vector3.right*90);
                    Check(hidden.GetComponent<Renderer>()!=null && hidden.GetComponent<Collider>()==null,"hidden block visual only");
                    player=Player(new Vector3(100,.6f,0)); Next(7,.4f); return;
                }
                if (phase == 7)
                {
                    Check(player.IsGrounded,"player grounded below one-way");
                    Check(Mathf.Abs(player.GetComponent<CapsuleCollider>().bounds.size.y-1)<.001f,"player one cell tall");
                    Check(Mathf.Abs(player.GetComponent<CapsuleCollider>().bounds.size.x-1)<.001f,"player one cell wide");
                    startY=peak=player.Motor.position.y; player.RequestJump(); player.SetJumpHeld(false); Next(701,1f); return;
                }
                if (phase == 701)
                {
                    Check(Mathf.Abs(peak-startY-1)<.16f,"tap jump actual apex 1 cell: " + (peak-startY));
                    startY=peak=player.Motor.position.y; player.SetJumpHeld(true); player.RequestJump(); Next(8,2.5f); return;
                }
                if (phase == 8)
                {
                    Check(Mathf.Abs(peak-startY-4)<.14f,"held jump actual apex 4 cells: " + (peak-startY));
                    Check(player.IsGrounded && Mathf.Abs(player.Motor.position.y-2.6f)<.12f,"jump through and land atop one-way y=" + player.Motor.position.y + " vy=" + player.VerticalVelocity + " grounded=" + player.IsGrounded);
                    Object.Destroy(player.gameObject);
                    plant=Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Green_JumpPlant.prefab",new Vector3(120,0,0)).GetComponent<BouncePlantFeature>();
                    Check(plant.GetRiseCells(5,false)==3 && plant.GetRiseCells(4,true)==3 && plant.GetRiseCells(4.1f,true)==8,"plant 3/8 threshold rules");
                    player=Player(new Vector3(120,3,0)); player.SetJumpHeld(false); Next(9,0); return;
                }
                if (phase == 9)
                {
                    if (player.VerticalVelocity>3 && player.Motor.position.y<1.5f) { startY=peak=player.Motor.position.y; Next(11,0); }
                    return;
                }
                if (phase == 11)
                {
                    if (player.VerticalVelocity>0) return;
                    Check(Mathf.Abs(peak-startY-3)<.28f,"actual plant normal 3 cells: " + (peak-startY));
                    Object.Destroy(player.gameObject); player=Player(new Vector3(120,7,0)); player.SetJumpHeld(true); Next(12,0); return;
                }
                if (phase == 12)
                {
                    if (player.VerticalVelocity>3 && player.Motor.position.y<1.5f) { startY=peak=player.Motor.position.y; Next(14,0); }
                    return;
                }
                if (phase == 14)
                {
                    if (player.VerticalVelocity>0) return;
                    Check(Mathf.Abs(peak-startY-8)<.4f,"actual plant held high 8 cells: " + (peak-startY));
                    Object.Destroy(player.gameObject);
                    var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(140,-.5f,0); floor.transform.localScale=new Vector3(10,1,4);
                    player=Player(new Vector3(140.5f,.5f,0)); Cube(CubeColor.Red,new Vector3(141.5f,.5f,0)); Next(15,.3f); return;
                }
                if (phase == 15)
                {
                    Check(player.UpdateCarryInput(true,false,false,false),"carry request"); Next(16,.2f); return;
                }
                if (phase == 16)
                {
                    player.UpdateCarryInput(false,false,false,false); Check(player.IsCarrying,"carrying started"); Next(17,.5f); return;
                }
                if (phase == 17)
                {
                    Check(!player.CanCarryPlantJump,"carrying ordinary jump locked");
                    Spawn("Assets/_Project/Content/ColorBlocks/Prefabs/Green_JumpPlant.prefab",new Vector3(140,.5f,0));
                    player.transform.position = player.Motor.position = new Vector3(140,1.5f,0);
                    player.Motor.linearVelocity = Vector3.zero;
                    Physics.SyncTransforms(); Next(171,.2f); return;
                }
                if (phase == 171)
                {
                    Check(player.CanCarryPlantJump,"plant grants carrying jump");
                    startY=peak=player.Motor.position.y; player.RequestJump(); Next(18,.95f); return;
                }
                if (phase == 18)
                {
                    Check(Mathf.Abs(peak-startY-4)<.14f,"actual carrying jump 4 cells: " + (peak-startY));
                    Check(!player.CanCarryPlantJump,"carry jump consumed");
                    Next(181,.6f); return;
                }
                if (phase == 181)
                {
                    Check(player.CanCarryPlantJump,"returning to plant grants next carrying jump");
                    Check(ColorRuntimeService.Instance.IsUnlocked("red"),"resets preserve keys");
                    Finish(true,"PASS " + checks); return;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false,error.ToString()); }
        }
        private static void Finish(bool success,string message)
        {
            File.WriteAllText(Path.Combine(Application.dataPath,"../Library/MechanismGameplayVerification.txt"),message);
            SessionState.SetBool("MechanismVerification",false); EditorApplication.update-=Tick; EditorApplication.Exit(success?0:1);
        }
    }
}
