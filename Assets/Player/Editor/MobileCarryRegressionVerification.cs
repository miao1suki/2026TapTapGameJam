using System;
using System.Reflection;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.InputRebinding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Run only in the isolated verification project, never in a designer's scene.</summary>
    public static class MobileCarryRegressionVerification
    {
        private const string Key = "MobileCarryRegressionVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int frames, checks, phase;
        private static float began;
        private static PlayerController player;
        private static CarryableFeature carried;
        private static Vector3 destination;
        private static GameObject obstacle;
        private static SimulationMode previousSimulation;

        public static void Run()
        {
            if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets"))
                throw new Exception("Isolated verification only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            frames = checks = phase = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new Exception(label);
            checks++; Debug.Log("MOBILE CARRY PASS " + label);
        }
        private static void Move(Vector3 position)
        {
            player.Motor.position = position; player.transform.position = position;
            player.Motor.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 10) return;
            try
            {
                if (phase == 0)
                {
                    previousSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
                    new GameObject("Verification Audio", typeof(AudioListener));
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
                    var actor = Object.Instantiate(prefab, new Vector3(10.5f, -28.5f, 0), Quaternion.identity);
                    player = actor.GetComponent<PlayerController>();
                    actor.GetComponent<PlayerInputDriver>().enabled = false;
                    player.Motor.interpolation = RigidbodyInterpolation.None;
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.position = new Vector3(10, -29.5f, 0); floor.transform.localScale = new Vector3(30, 1, 2);
                    Physics.SyncTransforms();
                    for (int i = 0; i < 80; i++)
                    {
                        typeof(PlayerController).GetMethod("FixedUpdate", Private).Invoke(player, null);
                        Physics.Simulate(.02f); Physics.SyncTransforms();
                    }
                    var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    item.transform.position = new Vector3(11.5f, -28.5f, 0);
                    carried = item.AddComponent<CarryableFeature>();
                    Check(carried.BeginCarry(player), "standard player starts carrying");
                    typeof(PlayerController).GetField("selectedCarryTarget", Private).SetValue(player, carried);
                    carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition); Physics.SyncTransforms();
                    WallPlacementChecks();
                    player.SetMoveInput(Vector2.right);
                    Vector3 stable = Vector3.zero;
                    foreach (float x in new[] { 10.70f, 10.95f, 11.00f, 11.04f, 10.98f, 11.02f })
                    {
                        Move(new Vector3(x, -28.5f, 0)); carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                        Check(player.TryGetCarryPlacementPreview(out Bounds bounds, out bool valid) && valid,
                            "walking overlap remains placeable x=" + x);
                        if (stable == Vector3.zero) stable = bounds.center;
                        Check(bounds.center == stable, "cell-boundary jitter keeps one preview cell");
                    }
                    Bounds proposed = ((IPlayerCarryPlacementTarget)carried).GetCarryPlacementBounds(stable);
                    Vector3 before = player.Motor.position;
                    Check(player.ResolveCarryOverlap(proposed, item.transform, false) && player.Motor.position == before,
                        "preview clearance never moves the player");
                    player.SetMoveInput(Vector2.zero);
                    Check(player.ResolveCarryOverlap(proposed, item.transform, true), "small self-overlap can be separated");
                    Check(player.Motor.position == before, "separation request never teleports the player");
                    float largestStep = 0f;
                    for (int i = 0; i < 9; i++)
                    {
                        float oldX = player.Motor.position.x;
                        typeof(PlayerController).GetMethod("FixedUpdate", Private).Invoke(player, null);
                        Physics.Simulate(.02f); Physics.SyncTransforms();
                        largestStep = Mathf.Max(largestStep, Mathf.Abs(player.Motor.position.x - oldX));
                    }
                    Check(largestStep > .01f && largestStep < .13f, "separation is spread over physical steps");
                    Check(player.GetComponent<CapsuleCollider>().bounds.max.x <= proposed.min.x + .004f,
                        "motor separates the player outside placed bounds");
                    Move(before);
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.position = before + Vector3.left * .85f; Physics.SyncTransforms();
                    Check(!player.ResolveCarryOverlap(proposed, item.transform, false), "escape blocked by wall is rejected");
                    Object.DestroyImmediate(obstacle);
                    Bounds huge = new Bounds(before + Vector3.right * .1f, Vector3.one * 3);
                    Check(!player.ResolveCarryOverlap(huge, item.transform, false), "deep overlap with merged object rejected");
                    ColorChecks(); LayoutChecks(); PlatformHudChecks(); RebindingChecks(); CameraChecks();
                    Move(new Vector3(10.8f, -28.5f, 0)); carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                    destination = new Vector3(11.5f, -28.5f, 0);
                    player.UpdateCarryInput(false, false, true, false);
                    Check(carried.IsCarryDropAnimating, "drop animation begins through player command");
                    Physics.simulationMode = SimulationMode.FixedUpdate;
                    began = Time.time; phase = 1;
                }
                else if (phase == 1 && Time.time - began > .4f)
                {
                    Check(!carried.IsCarryActive && !carried.IsCarryDropAnimating, "clear drop completes");
                    Check(carried.GetComponent<Collider>().enabled, "solid collider restored only after safe separation");
                    Check(player.GetComponent<CapsuleCollider>().bounds.max.x <= destination.x - .5f + .004f,
                        "actual animation completion leaves player outside cube");
                    Move(new Vector3(10.8f, -28.5f, 0));
                    Check(carried.BeginCarry(player), "second pickup begins");
                    typeof(PlayerController).GetField("selectedCarryTarget", Private).SetValue(player, carried);
                    carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                    player.UpdateCarryInput(false, false, true, false);
                    Check(carried.IsCarryDropAnimating, "second drop begins through player command");
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.transform.position = destination;
                    Physics.SyncTransforms(); began = Time.time; phase = 2;
                }
                else if (phase == 2 && Time.time - began > .4f)
                {
                    Check(carried.IsCarryActive && !carried.IsCarryDropAnimating,
                        $"obstacle entering during animation keeps object safely held (active={carried.IsCarryActive}, animating={carried.IsCarryDropAnimating})");
                    Check(!carried.GetComponent<Collider>().enabled, "failed drop never enables overlapping solid collider");
                    Object.DestroyImmediate(obstacle);
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.position = new Vector3(12.5f, -28.5f, 0);
                    Move(new Vector3(11.48f, -28.5f, 0));
                    carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                    player.SetMoveInput(Vector2.left); player.SetMoveInput(Vector2.zero);
                    Check(player.TryGetCarryPlacementPreview(out Bounds preview, out bool valid) && valid &&
                        Mathf.Abs(preview.center.x - 10.5f) < .01f, "tap left beside right wall previews the left cell");
                    player.UpdateCarryInput(false, true, false, false);
                    Check(carried.IsCarryDropAnimating && Mathf.Abs(player.Motor.position.x - 11.48f) < .001f,
                        "wall-side left drop starts without snapping player");
                    began = Time.time; phase = 3;
                }
                else if (phase == 3 && Time.time - began > .5f)
                {
                    Check(!carried.IsCarryActive && carried.GetComponent<Collider>().enabled,
                        "wall-side left drop completes with solid restored");
                    Check(Mathf.Abs(player.Motor.position.x - 11.5f) < .015f,
                        "one-cell gap fits player between placed block and opposite wall");
                    Check(!Physics.ComputePenetration(player.GetComponent<CapsuleCollider>(), player.Motor.position,
                        player.transform.rotation, obstacle.GetComponent<Collider>(), obstacle.transform.position,
                        Quaternion.identity, out _, out float depth) || depth < .004f,
                        "smooth separation never penetrates opposite wall");
                    Object.DestroyImmediate(obstacle);
                    Move(new Vector3(20.52f, -28.5f, 0));
                    Check(carried.BeginCarry(player), "mirror case pickup");
                    typeof(PlayerController).GetField("selectedCarryTarget", Private).SetValue(player, carried);
                    carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.position = new Vector3(19.5f, -28.5f, 0);
                    player.SetMoveInput(Vector2.right); player.SetMoveInput(Vector2.zero);
                    Physics.SyncTransforms();
                    Check(player.TryGetCarryPlacementPreview(out _, out bool valid) && valid,
                        "mirror case permits right placement beside left wall");
                    player.UpdateCarryInput(false, false, true, false);
                    Check(carried.IsCarryDropAnimating, "mirror case drop command begins");
                    began = Time.time; phase = 4;
                }
                else if (phase == 4 && Time.time - began > .5f)
                {
                    Check(!carried.IsCarryActive && carried.GetComponent<Collider>().enabled,
                        "mirror case right drop completes");
                    Check(Mathf.Abs(player.Motor.position.x - 20.5f) < .015f,
                        "mirror case motor separation settles in one cell");
                    Object.DestroyImmediate(obstacle);
                    Check(carried.BeginCarry(player), "cancel case pickup");
                    typeof(PlayerController).GetField("selectedCarryTarget", Private).SetValue(player, carried);
                    Move(new Vector3(20.8f, -28.5f, 0));
                    carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                    player.UpdateCarryInput(false, false, true, false);
                    Check(carried.IsCarryDropAnimating && player.IsCarrySeparationPending(carried.transform),
                        "cancel case has a pending motor separation");
                    carried.CancelCarry();
                    Check(!player.IsCarrySeparationPending(carried.transform) && carried.GetComponent<Collider>().enabled,
                        "cancel restores solid and cancels pending velocity");
                    Finish(true);
                }
                if (frames > 20000) throw new Exception("Verification timeout");
            }
            catch (Exception e) { Debug.LogException(e); Finish(false); }
        }
        private static void WallPlacementChecks()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(12.5f, -28.5f, 0);
            player.SetMoveInput(Vector2.left);
            foreach (float x in new[] { 11.48f, 11.49f, 11.50f, 11.51f })
            {
                Move(new Vector3(x, -28.5f, 0));
                carried.UpdateCarryAnchor(player.CarryAnchorWorldPosition);
                Physics.SyncTransforms();
                bool shown = player.TryGetCarryPlacementPreview(out Bounds bounds, out bool valid);
                Debug.Log($"WALL BASELINE x={x} shown={shown} valid={valid} cell={bounds.center}");
                Check(shown && valid, "opposite wall permits left adjacent cell x=" + x);
            }
            Object.DestroyImmediate(wall);
            Move(new Vector3(10.5f, -28.5f, 0));
            typeof(PlayerController).GetField("carryDropCellValid", Private).SetValue(player, false);
        }

        private static void ColorChecks()
        {
            var colors = ColorRuntimeService.Instance; colors.ResetProgress();
            var wheel = player.GetComponent<PlayerColorWheel>(); int denied = 0;
            wheel.AbilitySelectionLocked += () => denied++;
            Check(!wheel.TrySelectColor("blue") && denied == 1, "locked selection emits feedback request");
            colors.CollectColorKey("blue");
            Check(colors.IsUnlocked("blue") && !wheel.TrySelectColor("blue") && denied == 2,
                "first key reveals scene but still denies ability");
            colors.CollectColorKey("blue");
            Check(wheel.TrySelectColor("blue") && wheel.SelectedColorId == "blue", "second key permits ability");
        }
        private static void LayoutChecks()
        {
            string original = PlayerPrefs.GetString("ShapeOfColor.TouchLayout.v1", "");
            bool existed = PlayerPrefs.HasKey("ShapeOfColor.TouchLayout.v1");
            try
            {
                MobileControlLayout.Reset();
                var obj = new GameObject("Controls Test", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                obj.SetActive(false);
                obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var hud = obj.AddComponent<MobileGameplayControls>(); hud.LayoutEditing = true;
                hud.Configure(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("97e10da648522f547bbec66357199a2a")), null);
                obj.SetActive(true);
                Canvas.ForceUpdateCanvases();
                Check(hud.Controls.Find("右手左") != null && hud.Controls.Find("右手右") != null,
                    "temporary right-hand directions are separate controls");
                var run = hud.Controls.Find("跑").GetComponent<VirtualInputButton>();
                var e = new PointerEventData(null) { pointerId = 19 }; run.OnPointerDown(e); run.OnPointerUp(e);
                Check(run.ToggleOn, "mobile run uses a tap toggle");
                var jump = (RectTransform)hud.Controls.Find("左跳跃"); jump.sizeDelta = Vector2.one * 160;
                jump.anchorMin = jump.anchorMax = new Vector2(.38f, .42f); jump.anchoredPosition = Vector2.zero;
                MobileControlLayout.Save(hud.Controls); jump.sizeDelta = Vector2.one * 80;
                hud.ApplySavedLayout(); Check(jump.sizeDelta.x == 160 && jump.anchorMin == new Vector2(.38f, .42f),
                    "custom layout size and normalized position persist");
                Check(hud.Controls.Find("跳跃").GetComponent<Image>().raycastPadding.x == -14,
                    "mobile controls include forgiving initial touch margins");
                var context = new ControlContext { ShowAirborneDownControl = true };
                var oldContext = MobileGameplayContext.Current;
                MobileGameplayContext.Current = context;
                try
                {
                    hud.LayoutEditing = false;
                    typeof(MobileGameplayControls).GetField("previewOnDesktop", Private).SetValue(hud, true);
                    typeof(MobileGameplayControls).GetMethod("Update", Private).Invoke(hud, null);
                    Check(hud.Controls.Find("下").gameObject.activeSelf && hud.Controls.Find("右手左").gameObject.activeSelf,
                        "airborne context exposes down and right-hand directions");
                    var down = hud.Controls.Find("下").GetComponent<VirtualDirectionButton>();
                    down.OnPointerDown(e); context.ShowAirborneDownControl = false;
                    typeof(MobileGameplayControls).GetMethod("Update", Private).Invoke(hud, null);
                    Check(down.gameObject.activeSelf && down.IsPressed, "landing cannot revoke a held down press");
                    down.OnPointerUp(e);
                    typeof(MobileGameplayControls).GetMethod("Update", Private).Invoke(hud, null);
                    Check(!down.gameObject.activeSelf && !hud.Controls.Find("右手左").gameObject.activeSelf,
                        "contextual directions disappear after landing grace and release");
                }
                finally { MobileGameplayContext.Current = oldContext; }
                Object.DestroyImmediate(obj);
            }
            finally
            {
                MobileControlLayout.Reset();
                if (existed) PlayerPrefs.SetString("ShapeOfColor.TouchLayout.v1", original);
                PlayerPrefs.Save();
            }
        }
        private sealed class ControlContext : IMobileGameplayContext
        {
            public bool IsDead => false;
            public bool IsCarrying => false;
            public bool IsSwimming => false;
            public bool IsClimbing => false;
            public bool IsHorizontalClimbing => false;
            public bool IsGreenHookActive => false;
            public bool HasContextInteraction => false;
            public int CarryQueueCount => 0;
            public bool ShowAirborneDownControl { get; set; }
        }
        private static void RebindingChecks()
        {
            var asset = InputActionAssetFactory.CreateDefaultGameplayAsset();
            var service = new InputBindingService(asset, new MemoryInputBindingStorage());
            try
            {
                InputAction jump = asset.FindAction("Gameplay/Jump", true);
                int keyboard = -1, pad = -1;
                for (int i = 0; i < jump.bindings.Count; i++)
                {
                    if (jump.bindings[i].path.StartsWith("<Keyboard>")) keyboard = i;
                    if (jump.bindings[i].path.StartsWith("<Gamepad>")) pad = i;
                }
                Check(keyboard >= 0 && pad >= 0, "default jump has keyboard and controller bindings");
                jump.ApplyBindingOverride(keyboard, "<Keyboard>/q"); jump.ApplyBindingOverride(pad, "<Gamepad>/buttonNorth");
                service.ResetDevice(InputBindingDevice.Gamepad);
                bool q = false, south = false;
                foreach (InputBinding b in jump.bindings)
                { q |= b.effectivePath == "<Keyboard>/q"; south |= b.effectivePath == "<Gamepad>/buttonSouth"; }
                Check(q && south, "mobile controller reset preserves desktop custom binding");
                int controllerIndex = -1;
                for (int i = 0; i < jump.bindings.Count; i++)
                    if (jump.bindings[i].effectivePath.StartsWith("<Gamepad>")) controllerIndex = i;
                var touch = InputSystem.AddDevice<Touchscreen>();
                var padDevice = InputSystem.AddDevice<Gamepad>();
                bool finished = false;
                var operation = service.StartRebind(InputActionId.Jump, controllerIndex, () => finished = true);
                try
                {
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = 71,
                        phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(20, 20) });
                    InputSystem.Update();
                    Check(!finished && operation.candidates.Count == 0, "touching settings cannot replace a controller binding");
                    operation.OnPotentialMatch(result => result.Complete());
                    InputSystem.QueueStateEvent(padDevice, new GamepadState().WithButton(GamepadButton.West));
                    InputSystem.Update();
                    Check(finished && jump.bindings[controllerIndex].effectivePath.Contains("buttonWest"),
                        "controller remapping accepts controller input");
                }
                finally
                {
                    if (!finished) { operation.Cancel(); operation.Dispose(); }
                    InputSystem.RemoveDevice(touch); InputSystem.RemoveDevice(padDevice);
                }
            }
            finally { service.Dispose(); Object.DestroyImmediate(asset); }
        }
        private static void CameraChecks()
        {
            var obj = new GameObject("Follow Test", typeof(CameraControlManager), typeof(CameraFollowController));
            var follow = obj.GetComponent<CameraFollowController>();
            follow.SetFallbackMode(CameraViewMode.Side2D); follow.SetTarget(player.transform, true);
            Check(Mathf.Abs(follow.FocusPoint.y - player.transform.position.y) < .001f,
                "2D focus moved down one unit from former +1 offset");
            Object.DestroyImmediate(obj);
        }
        private static void PlatformHudChecks()
        {
            var input = InputService.EnsureInstance();
            var field = typeof(InputService).GetField("platformMode", Private);
            object previous = field.GetValue(input);
            GameObject instance = null;
            try
            {
                field.SetValue(input, InputPlatformMode.Mobile);
                instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab"));
                var router = instance.GetComponentInChildren<GameUiRouter>(true);
                var hud = (GameObject)typeof(GameUiRouter).GetField("gameplayHud", Private).GetValue(router);
                foreach (string name in new[] { "Help", "PauseHint", "PauseButton", "LevelLabel" })
                    Check(hud.transform.Find(name) != null && !hud.transform.Find(name).gameObject.activeSelf,
                        "mobile hides desktop HUD " + name);
                field.SetValue(input, InputPlatformMode.Desktop);
                typeof(GameUiRouter).GetMethod("RefreshPlatformHud", Private).Invoke(router, null);
                foreach (string name in new[] { "Help", "PauseHint", "LevelLabel", "PauseButton" })
                    Check(!hud.transform.Find(name).gameObject.activeSelf, "desktop hides retired HUD " + name);
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                field.SetValue(input, previous);
            }
        }
        private static void Finish(bool ok)
        {
            Physics.simulationMode = previousSimulation; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            if (ok) Debug.Log("MOBILE CARRY VERIFICATION PASSED " + checks);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
