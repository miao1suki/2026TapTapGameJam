using System;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    public static class CarryGroundVerification
    {
        private const string Key = "CarryGroundVerification";
        private static int frames, checks;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            frames = checks = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Check(bool pass, string label)
        { if (!pass) throw new Exception(label); checks++; Debug.Log("GROUND CARRY PASS " + label); }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 10) return;
            EditorApplication.update -= Tick;
            SimulationMode previous = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
                var actor = Object.Instantiate(prefab, new Vector3(10.5f, -28.5f, 0), Quaternion.identity);
                var player = actor.GetComponent<PlayerController>();
                player.GetComponent<PlayerInputDriver>().enabled = false;
                player.Motor.interpolation = RigidbodyInterpolation.None;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = new Vector3(10, -29.5f, 0); floor.transform.localScale = new Vector3(30, 1, 2);
                Physics.SyncTransforms();
                for (int i = 0; i < 80; i++)
                {
                    typeof(PlayerController).GetMethod("FixedUpdate", Private).Invoke(player, null);
                    Physics.Simulate(.02f); Physics.SyncTransforms();
                }
                Check(Mathf.Abs(player.GetComponent<CapsuleCollider>().bounds.min.y + 29f) < .05f, "real prefab settles on negative-coordinate floor");
                Check(!ColorRuntimeService.Instance.IsUnlocked("red") && !ColorRuntimeService.Instance.IsUnlocked("green") && !ColorRuntimeService.Instance.IsUnlocked("blue"), "new session starts with all three colors locked");
                var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                item.transform.position = new Vector3(11.5f, -28.5f, 0);
                var carry = item.AddComponent<CarryableFeature>(); Physics.SyncTransforms();
                player.SetMoveInput(Vector2.right);
                player.UpdateCarryInput(false, false, false, false);
                Check(player.CarryQueueCount == 1, "same-height adjacent object selectable while grounded without jumping");
                Check((player.CarryCenterWorldPosition - player.GetComponent<CapsuleCollider>().bounds.center).sqrMagnitude < .0001f, "carry origin is actual body center, not offset prefab root");
                Check(carry.BeginCarry(player), "grounded carry begins");
                typeof(PlayerController).GetField("selectedCarryTarget", Private).SetValue(player, carry);
                carry.UpdateCarryAnchor(player.CarryAnchorWorldPosition); Physics.SyncTransforms();
                Check(player.TryGetCarryPlacementPreview(out Bounds bounds, out bool valid) && valid, "side drop is valid beside floor rather than inside it");
                Check(Mathf.Abs(bounds.min.y + 29f) < .03f, "drop preview bottom aligns with floor top");
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.position = bounds.center;
                Physics.SyncTransforms();
                Check(player.TryGetCarryPlacementPreview(out _, out valid) && !valid, "blocked destination remains rejected");
                Object.DestroyImmediate(blocker);
                player.Motor.position += Vector3.up;
                player.transform.position = player.Motor.position;
                carry.UpdateCarryAnchor(player.CarryAnchorWorldPosition); Physics.SyncTransforms();
                Check(player.TryGetCarryPlacementPreview(out Bounds raised, out valid) && valid &&
                    Mathf.Abs(raised.min.y - bounds.min.y - 1f) < .03f, "drop follows raised body cell, not locked to ground");
                Object.DestroyImmediate(item); Object.DestroyImmediate(actor); Object.DestroyImmediate(floor);
                Debug.Log("GROUND CARRY VERIFICATION PASSED " + checks);
                Physics.simulationMode = previous; SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            catch (Exception e)
            { Debug.LogException(e); Physics.simulationMode = previous; SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
