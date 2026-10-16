using System;
using System.Reflection;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.StartMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    public static class ClimbMobileVerification
    {
        private const string Key = "ClimbMobileVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int frames, checks;
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
        { if (!pass) throw new Exception(label); checks++; Debug.Log("CLIMB MOBILE PASS " + label); }
        private static void Call(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 10) return;
            EditorApplication.update -= Tick;
            var oldSimulation = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                var actor = new GameObject("Player"); var player = actor.AddComponent<PlayerController>();
                player.Motor.interpolation = RigidbodyInterpolation.None;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ContactCubes/Prefabs/Green_ContactCube.prefab");
                if (prefab == null)
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab Green"))
                    {
                        var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                        if (candidate != null && candidate.GetComponent<ContactCube>() != null) { prefab = candidate; break; }
                    }
                Check(prefab != null, "green contact cube prefab available");
                var cubeObject = Object.Instantiate(prefab, new Vector3(100, 100, 0), Quaternion.identity);
                var cube = cubeObject.GetComponent<ContactCube>(); var motion = cubeObject.GetComponent<ContactCubeMotion>();
                var serialized = new SerializedObject(cube); serialized.FindProperty("initialLength").intValue = 4;
                serialized.ApplyModifiedPropertiesWithoutUndo(); cube.ResetForRoom();
                cubeObject.GetComponent<Rigidbody>().isKinematic = true;
                typeof(ContactCubeMotion).GetField("<IsSettled>k__BackingField", Private).SetValue(motion, true);
                var horizontal = cubeObject.GetComponent<HorizontalCubeVine>();
                var solid = cubeObject.GetComponentInChildren<BoxCollider>(); Physics.SyncTransforms();
                Bounds bounds = solid.bounds;
                Move(player, new Vector3(bounds.max.x - .1f, bounds.max.y + .52f, 0));
                player.SetMoveInput(Vector2.right);
                bool protectedOnce = false;
                for (int i = 0; i < 210; i++)
                {
                    Call(horizontal, "FixedUpdate"); Call(player, "FixedUpdate");
                    protectedOnce |= player.IsClimbCornerProtected;
                    Physics.Simulate(.02f); Physics.SyncTransforms();
                }
                Check(protectedOnce, "top edge starts protection through real horizontal feature");
                Check(player.Motor.position.y < bounds.min.y - .35f && player.Motor.position.y > bounds.min.y - .8f,
                    "actual physics wraps top to underside without falling: " + player.Motor.position);
                Check(player.IsHorizontalClimbing, "underside remains attached after corner completes");
                player.RequestJump(); Check(!player.IsHorizontalClimbing && player.Motor.linearVelocity.y < 0, "jump releases and drops, not jumps");
                Call(horizontal, "FixedUpdate"); Check(!player.IsHorizontalClimbing, "release cooldown prevents immediate recapture");

                player.enabled = false; player.enabled = true;
                Move(player, new Vector3(bounds.max.x + .52f, bounds.center.y, 0));
                player.EnterClimb(horizontal); player.SetMoveInput(Vector2.up);
                protectedOnce = false;
                for (int i = 0; i < 140; i++)
                {
                    Call(horizontal, "FixedUpdate"); Call(player, "FixedUpdate");
                    protectedOnce |= player.IsClimbCornerProtected;
                    Physics.Simulate(.02f); Physics.SyncTransforms();
                }
                Check(protectedOnce && player.Motor.position.y > bounds.max.y + .35f, "vertical side joins horizontal top without a gap");
                player.enabled = false; player.enabled = true;
                Move(player, new Vector3(bounds.min.x + .1f, bounds.min.y - .52f, 0));
                player.SetHorizontalVineSurface(horizontal, player.Motor.position.y, 2f);
                player.SetMoveInput(Vector2.left);
                for (int i = 0; i < 210; i++)
                {
                    Call(horizontal, "FixedUpdate");
                    Call(player, "FixedUpdate"); Physics.Simulate(.02f); Physics.SyncTransforms();
                }
                Check(player.Motor.position.y > bounds.max.y + .35f && player.Motor.position.y < bounds.max.y + .8f,
                    "left outer edge wraps underside back to top: " + player.Motor.position + " bounds=" + bounds + " protected=" + player.IsClimbCornerProtected);
                Object.DestroyImmediate(cubeObject);
                ColorChecks(player);
                Object.DestroyImmediate(actor);
                LayoutChecks();
                Debug.Log("CLIMB MOBILE VERIFICATION PASSED " + checks);
                SessionState.SetBool(Key, false); Physics.simulationMode = oldSimulation; EditorApplication.Exit(0);
            }
            catch (Exception e)
            { Debug.LogException(e); Physics.simulationMode = oldSimulation; SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
        private static void Move(PlayerController player, Vector3 position)
        { player.Motor.position = position; player.transform.position = position; player.Motor.linearVelocity = Vector3.zero; Physics.SyncTransforms(); }
        private static void ColorChecks(PlayerController player)
        {
            Move(player, new Vector3(150, 150, 0));
            var cameraObject = new GameObject("Camera", typeof(Camera)); cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = player.transform.position + Vector3.back * 10;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Universal_Block.prefab");
            var target = Object.Instantiate(prefab, player.transform.position + Vector3.right * 1.5f, Quaternion.identity);
            var wheel = player.GetComponent<PlayerColorWheel>(); Physics.SyncTransforms();
            int successes = 0; wheel.ColorApplicationSucceeded += () => successes++;
            foreach (string color in new[] { "red", "blue" })
            {
                Check(wheel.TrySelectColor(color), "unlocked " + color + " selectable");
                wheel.Tick(camera.WorldToScreenPoint(target.transform.position));
                wheel.TryUseSelectedAbility(); wheel.TryUseSelectedAbility();
                Check(wheel.SelectedColorId == color && successes >= 2, "repeat " + color + " applications retain selection");
                successes = 0;
            }
            wheel.TrySelectColor("green");
            typeof(PlayerColorWheel).GetMethod("HandleGreenHookResult", Private).Invoke(wheel, new object[] { Project.GreenHook.GreenHookLaunchResult.Success });
            Check(wheel.SelectedColorId == "green", "successful green hook retains selection for next cast");
            wheel.DeselectAbility(); Check(!wheel.HasSelectedColor, "explicit center cancellation still clears color");
            Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
        }
        private static void LayoutChecks()
        {
            string saved = PlayerPrefs.GetString("ShapeOfColor.TouchLayout.v1", "");
            bool existed = PlayerPrefs.HasKey("ShapeOfColor.TouchLayout.v1");
            try
            {
                MobileControlLayout.Reset();
                var canvas = new GameObject("Layout test", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                var hud = canvas.AddComponent<MobileGameplayControls>(); hud.LayoutEditing = true;
                Canvas.ForceUpdateCanvases();
                Check(hud.GetComponentsInChildren<VirtualInputButton>(true).Length == 8, "two independent jump controls plus actions");
                var leftJump = (RectTransform)hud.Controls.Find("左跳跃");
                leftJump.anchorMin = leftJump.anchorMax = new Vector2(.4f, .4f);
                leftJump.anchoredPosition = Vector2.zero; leftJump.sizeDelta = Vector2.one * 150;
                MobileControlLayout.Save(hud.Controls);
                leftJump.sizeDelta = Vector2.one * 100; leftJump.anchoredPosition = new Vector2(100, 100);
                hud.ApplySavedLayout();
                Check(leftJump.sizeDelta.x == 150 && leftJump.anchorMin == new Vector2(.4f, .4f), "normalized position and per-button size restored");
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("97e10da648522f547bbec66357199a2a"));
                MobileControlLayout.Open(font);
                var editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
                Check(editor != null, "settings layout overlay creates successfully");
                foreach (var button in editor.GetComponentsInChildren<VirtualInputButton>(true)) Check(!button.enabled, "layout preview cannot fire gameplay actions");
                foreach (var label in editor.GetComponentsInChildren<TextMeshProUGUI>(true)) Check(label.font == font, "layout uses project font");
                Object.DestroyImmediate(editor.gameObject); Object.DestroyImmediate(canvas);
            }
            finally
            {
                MobileControlLayout.Reset();
                if (existed) PlayerPrefs.SetString("ShapeOfColor.TouchLayout.v1", saved);
                PlayerPrefs.Save();
            }
        }
    }
}
