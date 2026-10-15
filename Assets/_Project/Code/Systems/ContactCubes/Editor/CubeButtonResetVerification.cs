using System;
using System.Collections.Generic;
using System.IO;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.ContactCubes.Editor
{
    public static class CubeButtonResetVerification
    {
        private static int phase, checks, colorIndex;
        private static float deadline;
        private static double started;
        private static ContactCube[] cubes;
        private static PlayerController player;
        private static RoomLifecycleManager manager;
        private static DoorController door;
        private static DoorButton latch, held;
        private static void Check(bool value,string label) { checks++; if (!value) throw new Exception(label); }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("隔离验证入口");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool("CubeButtonResetVerification",true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("CubeButtonResetVerification",false)) return;
            phase = checks = colorIndex = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static GameObject Spawn(string path,Vector3 position) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),position,Quaternion.identity);
        private static void Move(ContactCube cube,Vector3 position) { cube.transform.position = cube.GetComponent<Rigidbody>().position = position; Physics.SyncTransforms(); }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused || (phase != 0 && Time.time < deadline)) return;
                if (phase == 0)
                {
                    Project.ColorBlocks.ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(20,-.5f,0); floor.transform.localScale = new Vector3(44,1,4);
                    manager = new GameObject("区域管理").AddComponent<RoomLifecycleManager>();
                    var a = new RoomRegion { id = "a" }; var b = new RoomRegion { id = "b" };
                    for (int x = 0; x < 40; x++) for (int y = 0; y < 10; y++) (x < 20 ? a : b).cells.Add(new Vector2Int(x,-y-1));
                    manager.Configure(new List<RoomRegion> { a,b },1,"");
                    // Keep the full random respawn search inside region A; a
                    // region-exit reset must not race the later pressure fixture.
                    player = Spawn("Assets/_Project/Content/Player/RuntimePlayer.prefab",new Vector3(2,2,0)).GetComponent<PlayerController>();
                    var respawnSettings = new SerializedObject(player);
                    respawnSettings.FindProperty("respawnRandomRadiusBlocks").intValue = 1;
                    respawnSettings.ApplyModifiedPropertiesWithoutUndo();
                    player.GetComponent<PlayerInputDriver>().enabled = false; player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                    cubes = new ContactCube[3];
                    for (int i = 0; i < 3; i++) cubes[i] = Spawn($"Assets/_Project/Content/ContactCubes/{(CubeColor)i}_ContactCube.prefab",new Vector3(3+i*2,3,0)).GetComponent<ContactCube>();
                    door = Spawn("Assets/_Project/Content/Mechanisms/Door.prefab",new Vector3(15,.5f,0)).GetComponent<DoorController>();
                    // This fixture checks pressure/reset edges; delayed presentation has its own regression.
                    var doorSettings = new SerializedObject(door); doorSettings.FindProperty("unlockDelay").floatValue = 0f; doorSettings.ApplyModifiedPropertiesWithoutUndo();
                    latch = Spawn("Assets/_Project/Content/Mechanisms/DoorButton.prefab",new Vector3(10,.5f,0)).GetComponent<DoorButton>();
                    held = Spawn("Assets/_Project/Content/Mechanisms/DoorButton.prefab",new Vector3(12,.5f,0)).GetComponent<DoorButton>();
                    var settings = new SerializedObject(held); settings.FindProperty("mode").enumValueIndex = (int)DoorButtonMode.Held; settings.ApplyModifiedPropertiesWithoutUndo();
                    door.SetButtons(new[] { latch,held });
                    phase = 1; deadline = Time.time + 1.1f; return;
                }
                if (phase == 1)
                {
                    Check(manager.CurrentRegionId == "a","initial player region");
                    for (int i = 0; i < 3; i++)
                    {
                        Check(cubes[i].GetComponent<Rigidbody>().position.y < 1,"gravity changes authored position");
                        cubes[i].GetComponent<ContactCubeMotion>().enabled = false; cubes[i].GetComponent<Rigidbody>().isKinematic = true; cubes[i].GetComponent<Rigidbody>().useGravity = false;
                    }
                    Move(cubes[2],new Vector3(4,.5f,0)); phase = 2; deadline = Time.time + 1.2f; return;
                }
                if (phase == 2)
                {
                    Check(cubes[1].Length > 1,"green reaction changed initial state");
                    player.RequestRespawn();
                    for (int i = 0; i < 3; i++)
                    {
                        Check(Vector3.Distance(cubes[i].transform.position,new Vector3(3+i*2,3,0)) < .01f,"R restores placed position " + i);
                        Check(cubes[i].Length == 1 && cubes[i].State == CubeReactionState.Idle,"R restores cube state " + i);
                        Check(cubes[i].GetComponent<Rigidbody>().linearVelocity == Vector3.zero,"R clears cube velocity " + i);
                        cubes[i].GetComponent<ContactCubeMotion>().enabled = false; cubes[i].GetComponent<Rigidbody>().isKinematic = true; cubes[i].GetComponent<Rigidbody>().useGravity = false;
                    }
                    Check(latch.Mode == DoorButtonMode.Latched && latch.Persistent,"legacy mode remains latched");
                    Check(!held.GetComponent<RoomResetObject>().Persistent,"held button registered for room reset");
                    Move(cubes[colorIndex],new Vector3(10,.5f,0)); phase = 3; deadline = Time.time + .12f; return;
                }
                if (phase == 3)
                {
                    Check(latch.IsPressed,"cube color presses ground button " + colorIndex);
                    Move(cubes[colorIndex],new Vector3(3+colorIndex*2,3,0)); phase = 4; deadline = Time.time + .12f; return;
                }
                if (phase == 4)
                {
                    Check(latch.IsPressed,"latched mode survives weight removal " + colorIndex);
                    if (++colorIndex < 3)
                    {
                        latch.ResetButton(); Move(cubes[colorIndex],new Vector3(10,.5f,0)); phase = 3; deadline = Time.time + .12f; return;
                    }
                    Move(cubes[0],new Vector3(12,.5f,0)); phase = 5; deadline = Time.time + .12f; return;
                }
                if (phase == 5)
                {
                    Check(held.IsPressed && door.State == DoorState.Unlocked,"held pressure opens fully signaled door");
                    Move(cubes[1],new Vector3(12,.5f,0)); Move(cubes[0],new Vector3(3,3,0)); phase = 6; deadline = Time.time + .12f; return;
                }
                if (phase == 6)
                {
                    Check(held.IsPressed && door.State == DoorState.Unlocked,"another weight keeps held signal");
                    Move(cubes[1],new Vector3(5,3,0)); phase = 7; deadline = Time.time + .12f; return;
                }
                if (phase == 7)
                {
                    Check(!held.IsPressed && door.State == DoorState.Locked,"release clears signal and closes door");
                    Check(latch.IsPressed,"closing door does not clear latched progress");
                    Move(cubes[0],new Vector3(12,.5f,0)); phase = 8; deadline = Time.time + .12f; return;
                }
                if (phase == 8)
                {
                    Check(held.IsPressed,"held repress"); player.RequestRespawn();
                    Check(!held.IsPressed && door.State == DoorState.Locked,"R resets held signal and door");
                    Check(latch.IsPressed,"R preserves latched progress");
                    // Room reset restored dynamics; freeze only for deterministic pressure placement.
                    cubes[0].GetComponent<Rigidbody>().isKinematic = true; cubes[0].GetComponent<Rigidbody>().useGravity = false;
                    Move(cubes[0],new Vector3(12,.5f,0)); phase = 9; deadline = Time.time + .12f; return;
                }
                if (phase == 9)
                {
                    // Editor update can observe the deadline before the next
                    // physics pressure query. Wait for that query, with a limit.
                    if (!held.IsPressed && Time.time < deadline+.5f) return;
                    Check(held.IsPressed,"held repress before region exit; cube="+cubes[0].transform.position+", feet="+cubes[0].GetComponentInChildren<BoxCollider>().bounds.min.y+", player="+player.transform.position+", region="+manager.CurrentRegionId);
                    player.transform.position = player.Motor.position = new Vector3(25,2,0); manager.UpdatePlayerRegion(player);
                    Check(!held.IsPressed && door.State == DoorState.Locked,"region exit resets held button");
                    Check(latch.IsPressed,"region exit preserves latched progress");
                    Check(Vector3.Distance(cubes[0].transform.position,new Vector3(3,3,0)) < .01f,"region exit restores cube authored position");
                    var motion = cubes[0].GetComponent<ContactCubeMotion>(); motion.enabled = true; motion.SuspendMotion(true);
                    Move(cubes[0],new Vector3(25,2,0)); phase = 10; deadline = Time.time + .12f; return;
                }
                if (phase == 10)
                {
                    Check(cubes[0].GetComponent<RoomResetObject>().RegionId == "a","moving cube does not change region ownership");
                    Check(cubes[0].transform.position.x < 10 && !cubes[0].GetComponent<ContactCubeMotion>().IsSuspended,"cross-region item returned and suspended carry released");
                    Check(!manager.CanPlaceInAuthoredRegion(cubes[0].GetComponent<RoomResetObject>(),new Vector3(25,2,0)),"drop destination in another room rejected");
                    cubes[0].GetComponent<Rigidbody>().isKinematic = true; Move(cubes[0],new Vector3(-5,2,0));
                    phase = 11; deadline = Time.time + .12f; return;
                }
                if (phase == 11)
                {
                    Check(cubes[0].transform.position.x > 0,"item outside every region returned");
                    var capsule = player.GetComponent<CapsuleCollider>();
                    player.transform.position = player.Motor.position = new Vector3(12,player.Motor.position.y + .05f - capsule.bounds.min.y,0);
                    Physics.SyncTransforms(); phase = 12; deadline = Time.time + .12f; return;
                }
                if (phase == 12)
                {
                    Check(held.IsPressed && door.State == DoorState.Unlocked,"player feet still activate pressure button");
                    var capsule = player.GetComponent<CapsuleCollider>();
                    player.transform.position = player.Motor.position = new Vector3(12,player.Motor.position.y + .25f - capsule.bounds.max.y,0);
                    Physics.SyncTransforms(); phase = 13; deadline = Time.time + .12f; return;
                }
                Check(!held.IsPressed,"player head is not button pressure");
                Finish(0,"PASS " + checks + " assertions");
            }
            catch (Exception exception) { Finish(1,exception.ToString()); }
        }
        private static void Finish(int code,string message)
        {
            SessionState.SetBool("CubeButtonResetVerification",false); EditorApplication.update -= Tick;
            File.WriteAllText("Library/CubeButtonResetVerification.txt",message); Debug.Log(message); EditorApplication.Exit(code);
        }
    }
}
