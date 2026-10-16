using System;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Player.Editor
{
    internal static class FacingInteractionVerification
    {
        private const string Key = "FacingInteractionVerification";
        private static int frames, checks;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            EditorSceneManager.OpenScene("Assets/Scenes/Pick.unity");
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++; Debug.Log("PASS " + message);
        }
        private static CarryableFeature Box(Vector3 position, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = position; box.transform.localScale = size;
            return box.AddComponent<CarryableFeature>();
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 15) return;
            EditorApplication.update -= Tick;
            try
            {
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                player.GetComponent<PlayerInputDriver>().enabled = false;
                player.Motor.position = new Vector3(-199.5f, 200.5f, 0);
                player.transform.position = player.Motor.position;
                player.Motor.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                var crankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Crank.prefab");
                var leftCrank = UnityEngine.Object.Instantiate(crankPrefab, player.transform.position + Vector3.left, Quaternion.identity);
                var rightCrank = UnityEngine.Object.Instantiate(crankPrefab, player.transform.position + Vector3.right, Quaternion.identity);
                Physics.SyncTransforms();
                var sensor = player.GetComponent<PlayerInteractionSensor>();
                player.SetMoveInput(Vector2.left); sensor.RefreshTarget();
                Check(ReferenceEquals(sensor.CurrentTarget, leftCrank.GetComponent<CrankPlatformFeature>()), "keyboard interaction faces left crank");
                player.SetMoveInput(Vector2.right); sensor.RefreshTarget();
                Check(ReferenceEquals(sensor.CurrentTarget, rightCrank.GetComponent<CrankPlatformFeature>()), "turn selects right crank immediately");
                var selected = sensor.CurrentTarget;
                for (int i = 0; i < 12; i++) sensor.RefreshTarget();
                Check(ReferenceEquals(sensor.CurrentTarget, selected), "repeated scans keep same target");
                leftCrank.SetActive(false); rightCrank.SetActive(false);
                player.SetMoveInput(Vector2.left); player.SetMoveInput(Vector2.zero);
                Check(player.FacingDirection == -1, "facing persists after release");
                Check(float.IsPositiveInfinity(PlayerInteractionSensor.FacingScore(player, player.transform.position + Vector3.right)), "behind target excluded");
                var left = Box(player.transform.position + Vector3.left, Vector3.one);
                var right = Box(player.transform.position + Vector3.right, Vector3.one);
                Physics.SyncTransforms();
                player.UpdateCarryInput(false, false, false, false);
                Check(player.CarryQueueCount == 1, "only facing-side carry candidate");
                player.SetMoveInput(Vector2.right);
                player.UpdateCarryInput(false, false, false, false);
                Check(player.CarryQueueCount == 1, "turn immediately selects opposite side");
                Check(right.BeginCarry(player), "carry starts");
                typeof(PlayerController).GetField("selectedCarryTarget", Private).SetValue(player, right);
                left.gameObject.SetActive(false);
                right.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                Physics.SyncTransforms();
                Check(player.TryGetCarryPlacementPreview(out Bounds destination, out bool valid) && valid, "empty drop preview valid");
                Check((destination.size - Vector3.one).sqrMagnitude < .001f, "preview uses real solid size despite disabled colliders");
                var wall = Box(destination.center, Vector3.one);
                int activeCount = Project.LevelEditor.LevelEditorPlacedBlock.ActiveInstances.Count;
                var marker = wall.gameObject.AddComponent<Project.LevelEditor.LevelEditorPlacedBlock>();
                marker.Configure(new Vector2Int(Mathf.FloorToInt(destination.center.x), Mathf.FloorToInt(destination.center.y)), "Verification", Color.white, false);
                Check(Project.LevelEditor.LevelEditorPlacedBlock.ActiveInstances.Count == activeCount + 1, "runtime grid metadata registered without frame scans");
                Physics.SyncTransforms();
                bool shown = player.TryGetCarryPlacementPreview(out Bounds blockedDestination, out valid);
                Check(shown && !valid, "occupied drop preview invalid");
                typeof(PlayerController).GetMethod("LateUpdate", Private).Invoke(player, null);
                var preview = player.transform.Find("Carry placement preview");
                Check(preview != null && preview.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0 &&
                    preview.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor").r > .9f, "invalid world preview has red dashed mesh");
                Check(!right.CanPlaceCarryAt(player, destination.center), "execution shares invalid placement check");
                wall.gameObject.SetActive(false);
                Check(Project.LevelEditor.LevelEditorPlacedBlock.ActiveInstances.Count == activeCount, "disabled grid metadata unregisters");
                right.transform.localScale = new Vector3(3, 1, 1);
                Check(right.GetCarryPlacementBounds(destination.center).size.x > 2.99f, "merged target full width");
                Check(!right.CanPlaceCarryAt(player, destination.center), "merged volume cannot overlap player");
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                var wheel = player.GetComponent<PlayerColorWheel>();
                var view = new TestView(); wheel.BindExternalView(view);
                int deselected = 0; wheel.AbilityDeselectedByPlayer += () => deselected++;
                Check(wheel.TrySelectColor("red"), "select unlocked red");
                typeof(PlayerColorWheel).GetMethod("OpenWheel", Private).Invoke(wheel, null);
                wheel.CommitWheelSelection();
                Check(!wheel.HasSelectedColor && deselected == 1, "center clears active ability exactly once");
                wheel.TrySelectColor("blue"); wheel.ClearSelectedColor();
                Check(deselected == 1, "automatic clear does not emit manual cancellation");
                view.Selected = "green";
                typeof(PlayerColorWheel).GetMethod("OpenWheel", Private).Invoke(wheel, null);
                wheel.CommitWheelSelection();
                Check(wheel.SelectedColorId == "green", "sector selection unchanged");
                view.Selected = "missing-locked-color";
                typeof(PlayerColorWheel).GetMethod("OpenWheel", Private).Invoke(wheel, null);
                wheel.CommitWheelSelection();
                Check(wheel.SelectedColorId == "green" && deselected == 1, "invalid sector does not cancel existing ability");
                Debug.Log("FACING INTERACTION VERIFICATION PASSED " + checks);
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.Exit(1);
            }
        }
        private sealed class TestView : IPlayerColorWheelView
        {
            public string Selected = string.Empty;
            public string HighlightedColorId => Selected;
            public void Show() { }
            public void Hide() { }
            public void UpdatePointer(Vector2 position) { }
        }
    }
}
