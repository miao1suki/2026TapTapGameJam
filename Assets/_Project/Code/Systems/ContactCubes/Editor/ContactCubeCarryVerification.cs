using System;
using System.IO;
using System.Linq;
using Project.BlockFeatures;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.ContactCubes.Editor
{
    public static class ContactCubeCarryVerification
    {
        private sealed class Host : IPlayerCarryHost
        {
            public Vector3 CarryAnchorWorldPosition => new Vector3(0, 5, 0);
            public Vector3 CarryCenterWorldPosition => Vector3.zero;
        }
        private static readonly Host host = new Host();
        private static int phase, color, checks;
        private static double started;
        private static float deadline;
        private static ContactCube cube;
        private static CarryableFeature carry;
        private static ContactCubeMotion motion;
        private static PlayerController player;
        private static Vector3 start, destination;
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool("CubeCarryVerification", true);
            Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("CubeCarryVerification", false)) return;
            phase = color = checks = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Check(bool value, string label)
        {
            checks++; if (!value) throw new Exception(label);
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("timeout");
                if (!EditorApplication.isPlaying || Time.time < deadline) return;
                if (phase == 0)
                {
                    Project.ColorBlocks.ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    start = new Vector3(10 + color * 10, 2, 0); destination = start + Vector3.right * 3;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Content/ContactCubes/{(CubeColor)color}_ContactCube.prefab");
                    cube = UnityEngine.Object.Instantiate(prefab, start, Quaternion.identity).GetComponent<ContactCube>();
                    if (color == (int)CubeColor.Green)
                    {
                        var settings = new SerializedObject(cube);
                        settings.FindProperty("initialLength").intValue = 3;
                        settings.ApplyModifiedPropertiesWithoutUndo(); cube.ResetForRoom();
                    }
                    carry = cube.GetComponent<CarryableFeature>(); motion = cube.GetComponent<ContactCubeMotion>();
                    Check(carry != null && PlayerCarryTargetRegistry.All.Contains(carry), "prefab registered");
                    Check(carry.CanCarry(host) && carry.BeginCarry(host), "begin carry");
                    Check(motion.IsSuspended && cube.GetComponent<Rigidbody>().isKinematic, "physics handed off");
                    Check(!cube.GetComponentInChildren<BoxCollider>().enabled, "held collision disabled");
                    Check(!carry.CanCarry(new Host()), "cannot steal held target");
                    phase = 1; deadline = Time.time + .55f; return;
                }
                if (phase == 1)
                {
                    Check((cube.transform.position-host.CarryAnchorWorldPosition).sqrMagnitude < .001f, "head animation arrived");
                    Check(cube.State == CubeReactionState.Idle && cube.Length == (color == 1 ? 3 : 1), "held length retained");
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = destination + (color == 1 ? Vector3.right * 2 : Vector3.zero);
                    Physics.SyncTransforms();
                    Check(!carry.BeginCarryDrop(host,destination), "blocked placement rejected");
                    UnityEngine.Object.DestroyImmediate(wall);
                    Physics.SyncTransforms();
                    Check(carry.BeginCarryDrop(host,destination), "drop accepted");
                    Check(motion.IsSuspended, "drop tween owns pose");
                    phase = 2; deadline = Time.time + .23f; return;
                }
                if (phase == 2)
                {
                    Check(!carry.IsCarryActive && !motion.IsSuspended && !cube.GetComponent<Rigidbody>().isKinematic, "drop releases physics");
                    Check(cube.GetComponentInChildren<BoxCollider>().enabled, "drop restores collision");
                    Check(carry.BeginCarry(host), "can carry again");
                    Check(carry.BeginCarryReset(host), "carry reset starts");
                    phase = 3; deadline = Time.time + .31f; return;
                }
                if (phase == 3)
                {
                    Check(!carry.IsCarryResetAnimating && !motion.IsSuspended, "reset releases physics");
                    Check(carry.BeginCarry(host), "carry before room reset");
                    var member = cube.GetComponent<Project.Rooms.RoomResetObject>(); member.RestorePose(); member.RestoreState();
                    Check(!carry.IsCarryActive && !motion.IsSuspended && cube.Length == (color == 1 ? 3 : 1), "room reset cancels motion and state");
                    Check((cube.transform.position-start).sqrMagnitude < .001f, "room reset uses authored position");
                    UnityEngine.Object.DestroyImmediate(cube.gameObject);
                    color++; phase = 0; deadline = Time.time;
                    if (color < 3) return;
                    var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
                    player = UnityEngine.Object.Instantiate(playerPrefab,new Vector3(100.5f,.5f,0),Quaternion.identity).GetComponent<PlayerController>();
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                    var redPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab");
                    cube = UnityEngine.Object.Instantiate(redPrefab,new Vector3(101.5f,.5f,0),Quaternion.identity).GetComponent<ContactCube>();
                    Physics.SyncTransforms();
                    Check(player.UpdateCarryInput(true,false,false,false) && player.IsCarryPreparing,"existing interact command selects cube");
                    phase = 4; deadline = Time.time + .15f; return;
                }
                if (phase == 4)
                {
                    player.UpdateCarryInput(false,false,false,false);
                    Check(player.IsCarrying && cube.GetComponent<ContactCubeMotion>().IsSuspended,"player preparation enters carry");
                    phase = 5; deadline = Time.time + .5f; return;
                }
                if (phase == 5)
                {
                    player.UpdateCarryInput(false,false,true,false);
                    Check(player.IsCarryDropAnimating,"existing right drop command accepted");
                    phase = 6; deadline = Time.time + .3f; return;
                }
                if (phase == 6)
                {
                    player.UpdateCarryInput(false,false,false,false);
                    Check(!player.IsCarryBusy && !cube.GetComponent<ContactCubeMotion>().IsSuspended,"player and target released after drop");
                    Finish(true, $"PASS {checks}");
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false,error.ToString()); }
        }
        private static void Finish(bool success, string result)
        {
            File.WriteAllText(Path.Combine(Application.dataPath,"../Library/ContactCubeCarryVerification.txt"),result);
            SessionState.SetBool("CubeCarryVerification",false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
