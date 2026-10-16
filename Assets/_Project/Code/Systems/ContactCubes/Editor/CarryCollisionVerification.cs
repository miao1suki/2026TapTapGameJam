using System;
using System.Linq;
using Project.BlockFeatures;
using Project.Player;
using Project.WordNote;
using Project.InputAbstraction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.ContactCubes.Editor
{
    /// <summary>Isolated editor/real-physics regression; never rewrites an author's scene.</summary>
    public static class CarryCollisionVerification
    {
        private sealed class Host : IPlayerCarryHost
        {
            public Vector3 CarryAnchorWorldPosition => new Vector3(20, 5, 0);
            public Vector3 CarryCenterWorldPosition => new Vector3(20, 4, 0);
        }
        private static int phase, checks;
        private static double started;
        private static float deadline;
        private static PlayerController player;
        private static CarryableFeature carried;
        private static void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new Exception("Carry regression: " + label);
            Debug.Log("CARRY PASS: " + label);
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated project only");
            foreach (string path in new[] { "Assets/Scenes/Pick.unity", "Assets/Scenes/Test.unity", "Assets/_Project/Scenes/Playing/Gaming.unity" })
            {
                EditorSceneManager.OpenScene(path);
                var notes = UnityEngine.Object.FindObjectsByType<WordNoteUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(notes.Length == 1 && notes[0].DefaultFont.name == "Cubic_11 SDF", path + " one configured WordNote");
                Debug.Log("CARRY SCENE: " + path + " carryables=" + UnityEngine.Object.FindObjectsByType<CarryableFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool("CarryCollisionVerification", true);
            Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("CarryCollisionVerification", false)) return;
            phase = checks = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static GameObject Box(Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = position; go.transform.localScale = scale; return go;
        }
        private static void GeometryChecks()
        {
            var common = new GameObject("SharedMapRoot");
            var target = Box(new Vector3(15, 4, 0), new Vector3(3, 1, 1));
            target.transform.SetParent(common.transform);
            var feature = target.AddComponent<CarryableFeature>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("97e10da648522f547bbec66357199a2a"));
            var config = new SerializedObject(feature); config.FindProperty("indicatorFont").objectReferenceValue = font; config.ApplyModifiedPropertiesWithoutUndo();
            feature.SetCarryIndicator(true);
            var label = target.GetComponentInChildren<TextMeshPro>();
            label.ForceMeshUpdate();
            Check(label.font == font && label.fontSize == 2.7f, "label uses project font and size");
            Check((label.transform.lossyScale - Vector3.one).sqrMagnitude < .001f, "merged label not stretched");
            Check((label.transform.position - target.GetComponent<BoxCollider>().bounds.center).sqrMagnitude < .001f, "label at actual shape center");
            Check(label.color == Color.black && label.fontMaterial.GetColor(ShaderUtilities.ID_OutlineColor) == Color.white, "black text white outline");
            Check(label.text == (InputPromptUtility.UseActionLabels ? "搬运" : InputPromptUtility.FormatKey(InputActionId.Interact) + "\n搬运") && label.sortingOrder == short.MaxValue, "device-specific centered topmost label");
            feature.SetCarryIndicator(false);
            var host = new Host();
            Check(feature.BeginCarry(host), "start merged carry");
            var wall = Box(new Vector3(23, 5, 0), Vector3.one); wall.transform.SetParent(common.transform);
            Physics.SyncTransforms();
            var solid = (IPlayerCarryCollisionTarget)feature;
            Vector3 limited = solid.ConstrainCarryDisplacement(host.CarryAnchorWorldPosition, Vector3.right * 10, .01f);
            Check(limited.x > .95f && limited.x < 1.01f, "full three-cell width and high-speed sweep");
            Check(solid.ConstrainCarryDisplacement(host.CarryAnchorWorldPosition, Vector3.left, .01f).x < -.99f, "reverse away from wall allowed");
            target.transform.rotation = Quaternion.Euler(0, 0, 90); Physics.SyncTransforms();
            limited = solid.ConstrainCarryDisplacement(host.CarryAnchorWorldPosition, Vector3.right * 10, .01f);
            Check(limited.x > 1.95f && limited.x < 2.01f, "rotated authored OBB uses correct width");
            target.transform.rotation = Quaternion.identity;
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
            Check(solid.ConstrainCarryDisplacement(host.CarryAnchorWorldPosition, Vector3.right * 10, .01f).x > 9.99f, "triggers do not block");
            wall.GetComponent<Collider>().isTrigger = false;
            wall.transform.position = new Vector3(20, 7, 0); wall.transform.localScale = new Vector3(6, 1, 1); Physics.SyncTransforms();
            limited = solid.ConstrainCarryDisplacement(host.CarryAnchorWorldPosition, Vector3.up * 8, .01f);
            Check(limited.y > .95f && limited.y < 1.01f, "head ceiling limits upward travel");
            Check(solid.ConstrainCarryDisplacement(host.CarryAnchorWorldPosition, Vector3.right * 2, .01f).x > 1.99f, "ceiling does not block parallel slide");
            Check(!target.GetComponent<BoxCollider>().enabled, "no physical carry push collider");
            feature.CancelCarry(); Check(target.GetComponent<BoxCollider>().enabled, "cancel restores shape");
            UnityEngine.Object.DestroyImmediate(common);
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Carry timeout");
                if (!EditorApplication.isPlaying || Time.time < deadline) return;
                if (phase == 0)
                {
                    new GameObject("Test Audio Listener", typeof(AudioListener));
                    GeometryChecks();
                    Box(new Vector3(0, -.5f, 0), new Vector3(30, 1, 2));
                    Box(new Vector3(3, 1.5f, 0), Vector3.one); // Above body; only carried box can touch it.
                    player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"), new Vector3(.5f, .5f, 0), Quaternion.identity).GetComponent<PlayerController>();
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    carried = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Universal_Block.prefab"), new Vector3(1.5f, .5f, 0), Quaternion.identity).GetComponent<CarryableFeature>();
                    Physics.SyncTransforms(); phase = 1; deadline = Time.time + .2f; return;
                }
                if (phase == 1)
                {
                    Check(player.UpdateCarryInput(true, false, false, false), "F enters existing carry workflow");
                    phase = 2; deadline = Time.time + .7f; return;
                }
                if (phase == 2)
                {
                    Check(player.IsCarrying && carried.IsCarryActive, "pickup complete");
                    player.SetMoveInput(Vector2.right); player.SetSprintInput(true);
                    phase = 3; deadline = Time.time + 1.1f; return;
                }
                if (phase == 3)
                {
                    Check(player.Motor.position.x > 1.8f && player.Motor.position.x <= 2.01f, "real physics sprint stopped by overhead box");
                    Check(Mathf.Abs(player.Motor.linearVelocity.x) < .05f, "no continued velocity into obstacle");
                    player.SetMoveInput(Vector2.left); player.SetSprintInput(false);
                    phase = 4; deadline = Time.time + .25f; return;
                }
                if (phase == 4)
                {
                    Check(player.Motor.position.x < 1.8f, "player can walk away after collision");
                    player.SetMoveInput(Vector2.zero); player.RequestRespawn();
                    phase = 5; deadline = Time.time + .6f; return;
                }
                if (phase == 5)
                {
                    Check(!player.IsCarrying && carried.GetComponent<Collider>().enabled, "reset restores carry and collisions");
                    Debug.Log("CARRY VERIFICATION PASSED " + checks);
                    SessionState.SetBool("CarryCollisionVerification", false); EditorApplication.Exit(0);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e); SessionState.SetBool("CarryCollisionVerification", false); EditorApplication.Exit(1);
            }
        }
    }
}
