using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.ContactCubes.Editor
{
    public static class ContactCubeVerification
    {
        private static int phase, checks, pushes;
        private static float deadline;
        private static double started;
        private static ContactCube green, blue, red, pushed, physical;
        private static Project.Player.PlayerController player;
        private static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("隔离验证入口");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool("CubeVerification", true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("CubeVerification", false)) return;
            phase = checks = pushes = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static ContactCube Spawn(CubeColor color, Vector3 position, bool freeze = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Content/ContactCubes/{color}_ContactCube.prefab");
            var obj = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            if (freeze) { obj.GetComponent<ContactCubeMotion>().enabled = false; obj.GetComponent<Rigidbody>().isKinematic = true; obj.GetComponent<Rigidbody>().useGravity = false; }
            return obj.GetComponent<ContactCube>();
        }
        private static void Move(ContactCube cube, Vector3 position) { cube.transform.position = position; cube.GetComponent<Rigidbody>().position = position; Physics.SyncTransforms(); }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (phase != 0 && Time.time < deadline) return;
                if (phase == 0)
                {
                    Check(ContactCube.Evaluate(false,false) == CubeReactionState.Idle, "idle");
                    Check(ContactCube.Evaluate(true,false) == CubeReactionState.Shrinking, "shrink");
                    Check(ContactCube.Evaluate(false,true) == CubeReactionState.Growing, "grow");
                    Check(ContactCube.Evaluate(true,true) == CubeReactionState.Locked, "lock wins");
                    green = Spawn(CubeColor.Green, Vector3.zero); blue = Spawn(CubeColor.Blue, Vector3.left); red = Spawn(CubeColor.Red, Vector3.one * 100);
                    Check(!((object)green is Project.ColorBlocks.IColorApplicationTarget), "not a player color target");
                    phase = 1; deadline = Time.time + 1.2f; return;
                }
                if (phase == 1)
                {
                    Check(green.Length == 2 && green.State == CubeReactionState.Growing, "real contact grows one cell");
                    Check(Vector3.Dot(green.GrowthDirection, Vector3.right) > .99f, "away from blue");
                    Check(Mathf.Approximately(green.GetComponentInChildren<BoxCollider>().size.x, 2), "single collider resized");
                    Move(red, Vector3.up); phase = 2; deadline = Time.time + 1.2f; return;
                }
                if (phase == 2)
                {
                    Check(green.State == CubeReactionState.Locked && green.Length == 2, "simultaneous red blue locks length");
                    Move(blue, Vector3.one * 100); phase = 3; deadline = Time.time + 1.2f; return;
                }
                if (phase == 3)
                {
                    Check(green.State == CubeReactionState.Shrinking && green.Length == 1, "red contact shrinks to one");
                    Move(red, Vector3.one * 80); phase = 4; deadline = Time.time + .2f; return;
                }
                if (phase == 4)
                {
                    Check(green.State == CubeReactionState.Idle && green.Length == 1, "exit clears state");
                    Move(blue, Vector3.left); phase = 5; deadline = Time.time + 6.4f; return;
                }
                if (phase == 5)
                {
                    Check(green.Length == green.MaximumLength, "max length clamp");
                    green.ResetForRoom(); Check(green.Length == 1 && green.State == CubeReactionState.Idle, "room reset");
                    Move(green, Vector3.one * 90); Move(blue, Vector3.right * 10); Move(red, Vector3.right * 11);
                    pushed = Spawn(CubeColor.Green, Vector3.right * 8); pushed.PushRequested += (source,v,dt) => { if (v.x < 0 && dt > 0) pushes++; };
                    var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/RuntimePlayer.prefab");
                    player = UnityEngine.Object.Instantiate(playerPrefab, new Vector3(7,0,0), Quaternion.identity).GetComponent<Project.Player.PlayerController>();
                    player.GetComponent<Project.Player.PlayerInputDriver>().enabled = false;
                    player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                    phase = 6; deadline = Time.time + .3f; return;
                }
                if (phase == 6)
                {
                    var jet = blue.GetComponentInChildren<CubeWaterJet>();
                    Check(jet != null && Vector3.Dot(jet.Direction, Vector3.left) > .99f, "red blue jet direction");
                    Check(pushes > 0, "cube motion command emitted");
                    Check(pushed.transform.position == Vector3.right * 8, "reaction never writes object movement");
                    Check(player.IsSwimming, "player water motion adapter entered");
                    Move(red, Vector3.one * 80); phase = 7; deadline = Time.time + .3f; return;
                }
                if (phase == 7)
                {
                Check(blue.GetComponentsInChildren<CubeWaterJet>().Length == 0, "jet removed on contact exit");
                Check(!player.IsSwimming, "player water motion adapter cleared");
                blue.ResetForRoom(); Check(blue.GetComponentsInChildren<CubeWaterJet>().Length == 0, "reset clears jets");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(30,-.5f,0); floor.transform.localScale = new Vector3(16,1,4);
                physical = Spawn(CubeColor.Green, new Vector3(30,3,0), false);
                player.Motor.position = new Vector3(30,2,0); player.transform.position = player.Motor.position;
                Physics.SyncTransforms(); phase = 8; deadline = Time.time + .15f; return;
                }
                if (phase == 8)
                {
                    Check(!physical.GetComponent<ContactCubeMotion>().IsSettled, "falling cube not settled");
                    Check(Physics.GetIgnoreCollision(physical.GetComponentInChildren<BoxCollider>(),player.GetComponent<CapsuleCollider>()), "falling cube ignores player");
                    player.Motor.position = new Vector3(40,2,0); player.transform.position = player.Motor.position;
                    phase = 9; deadline = Time.time + 1.3f; return;
                }
                if (phase == 9)
                {
                    var motion = physical.GetComponent<ContactCubeMotion>();
                    Check(motion.IsSettled, "gravity cube lands and settles");
                    Check(!Physics.GetIgnoreCollision(physical.GetComponentInChildren<BoxCollider>(),player.GetComponent<CapsuleCollider>()), "settled restores player collision");
                    Check((physical.GetComponent<Rigidbody>().constraints & RigidbodyConstraints.FreezePositionX) != 0, "settled cube resists player push");
                    var config = new SerializedObject(physical); config.FindProperty("initialLength").intValue = 3; config.ApplyModifiedPropertiesWithoutUndo(); physical.ResetForRoom();
                    phase = 10; deadline = Time.time + .2f; return;
                }
                if (phase == 10)
                {
                    Bounds b = physical.GetComponentInChildren<BoxCollider>().bounds;
                    var c = player.GetComponent<CapsuleCollider>();
                    Vector3 pos = new Vector3(31, player.Motor.position.y + b.max.y + .015f - c.bounds.min.y,0);
                    player.Motor.position = pos; player.transform.position = pos; Physics.SyncTransforms();
                    player.Motor.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                    player.SetMoveInput(new Vector2(.5f,0));
                    phase = 11; deadline = Time.time + .15f; return;
                }
                if (phase == 11)
                {
                    Check(player.IsHorizontalClimbing, "grown green top activates traversal state");
                    Check(player.Motor.position.x > 31.08f, "player traverses horizontal green top");
                    player.RequestJump(); Check(player.IsHorizontalClimbing, "traversal remains active on jump request");
                    Check(player.Motor.linearVelocity.y < .1f, "horizontal traversal prevents upward jump velocity");
                    player.SetMoveInput(Vector2.zero); player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                    Bounds b = physical.GetComponentInChildren<BoxCollider>().bounds; var c = player.GetComponent<CapsuleCollider>();
                    var pos = new Vector3(31, player.Motor.position.y + b.min.y - .015f - c.bounds.max.y,0);
                    player.Motor.position = pos; player.transform.position = pos; Physics.SyncTransforms();
                    phase = 12; deadline = Time.time + .15f; return;
                }
                if (phase == 12)
                {
                    Check(player.IsHorizontalClimbing, "grown green underside activates traversal state");
                    player.Motor.position = new Vector3(40,2,0); player.transform.position = player.Motor.position;
                    physical.RequestPush(blue,new Vector3(4,0,0),Time.fixedDeltaTime);
                    phase = 13; deadline = Time.time + .1f; return;
                }
                if (phase == 13)
                {
                    Check(!player.IsHorizontalClimbing, "exit clears traversal state");
                    Check(physical.GetComponent<ContactCubeMotion>().GravityProtected && !physical.GetComponent<Rigidbody>().useGravity, "horizontal jet temporarily disables gravity");
                    Check(physical.GetComponent<Rigidbody>().linearVelocity.x > 3, "cube consumes jet velocity");
                    phase = 14; deadline = Time.time + .35f; return;
                }
                if (phase == 14)
                {
                    Check(!physical.GetComponent<ContactCubeMotion>().GravityProtected && physical.GetComponent<Rigidbody>().useGravity, "sustained horizontal jet does not extend grace indefinitely");
                    physical.RequestPush(blue,Vector3.zero,Time.fixedDeltaTime);
                    phase = 15; deadline = Time.time + .08f; return;
                }
                if (phase == 15)
                {
                    Move(blue,new Vector3(35,.5f,0)); Move(red,new Vector3(36,.5f,0));
                    phase = 16; deadline = Time.time + .12f; return;
                }
                if (phase == 16)
                {
                    Check(physical.GetComponent<Rigidbody>().linearVelocity.x < -3f, "real red blue water jet pushes dynamic cube");
                    Check(physical.GetComponent<ContactCubeMotion>().GravityProtected, "real jet starts gravity grace");
                    blue.enabled = false;
                    phase = 17; deadline = Time.time + .35f; return;
                }
                Check(physical.GetComponent<ContactCubeMotion>().IsSettled, "disabled jet clears velocity source and cube settles");
                physical.GetComponent<ContactCubeMotion>().SuspendMotion(true);
                Check(physical.GetComponent<Rigidbody>().isKinematic, "carry handoff suspends physics");
                var room = physical.GetComponent<Project.Rooms.RoomResetObject>(); room.RestorePose(); room.RestoreState();
                Check(!physical.GetComponent<ContactCubeMotion>().IsSuspended && !physical.GetComponent<Rigidbody>().isKinematic, "room reset releases suspended movement");
                Check(Vector3.Distance(physical.transform.position,new Vector3(30,3,0)) < .01f, "room reset restores authored root position");
                Check(physical.Length == 3 && physical.State == CubeReactionState.Idle, "room reset restores configured length and idle reaction");
                Finish(0, "PASS " + checks + " assertions");
            }
            catch (Exception exception) { Finish(1, exception.ToString()); }
        }
        private static void Finish(int code, string message)
        {
            SessionState.SetBool("CubeVerification", false); EditorApplication.update -= Tick;
            File.WriteAllText("Library/ContactCubeVerification.txt", message); Debug.Log(message); EditorApplication.Exit(code);
        }
    }
}
