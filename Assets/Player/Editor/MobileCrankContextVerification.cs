using System;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.InputAbstraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Opt-in isolated regression; never enters Play Mode in the main project.</summary>
    public static class MobileCrankContextVerification
    {
        private const string SessionKey = "MobileCrankContextVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int frames, checks;

        public static void Run()
        {
            string path = Application.dataPath.Replace('\\', '/');
            if (!path.EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run only in Library/FlowRetirementProbe; main Editor is not modified.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            frames = checks = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Check(bool ok, string label)
        {
            if (!ok) throw new Exception("MOBILE CRANK FAIL " + label);
            checks++;
            Debug.Log("MOBILE CRANK PASS " + label);
        }

        private static void Invoke(object target, string method, params object[] arguments)
            => target.GetType().GetMethod(method, Private).Invoke(target, arguments);

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, Private).SetValue(target, value);

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 12) return;
            EditorApplication.update -= Tick;
            InputService input = null;
            object oldMode = null;
            var source = new TouchSource();
            SimulationMode oldSimulation = Physics.simulationMode;
            bool passed = false;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                input = InputService.EnsureInstance();
                oldMode = typeof(InputService).GetField("platformMode", Private).GetValue(input);
                Set(input, "platformMode", InputPlatformMode.Mobile);
                input.SetExternalSource(source);
                var service = ColorRuntimeService.Instance;
                service.SetAllUnlockedForCurrentSession(true);
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
                var actor = Object.Instantiate(playerPrefab, new Vector3(100.5f, 100.5f, 0), Quaternion.identity);
                var player = actor.GetComponent<PlayerController>();
                var driver = actor.GetComponent<PlayerInputDriver>();
                driver.enabled = false;
                var sensor = actor.GetComponent<PlayerInteractionSensor>();
                player.SetMoveInput(Vector2.right);
                var crankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Crank.prefab");
                var crank = Object.Instantiate(crankPrefab, actor.transform.position + Vector3.right, Quaternion.identity)
                    .GetComponent<CrankPlatformFeature>();
                Physics.SyncTransforms();

                void Scan()
                {
                    Set(driver, "nextMobileContextScan", -1f);
                    Invoke(driver, "UpdateInteraction", player);
                }
                Check(!source.Pressed && !source.Triggered, "touch starts with no physical or virtual interaction press");
                Scan();
                Check(ReferenceEquals(sensor.CurrentTarget, crank) && player.HasContextInteraction,
                    "idle mobile scan exposes crank before contextual button can be pressed");
                Check(crank.Progress == 0f, "discovery alone never turns crank");
                Check((float)typeof(PlayerInputDriver).GetField("nextMobileContextScan", Private).GetValue(driver) > Time.unscaledTime,
                    "idle discovery schedules a future bounded scan");

                source.Pressed = source.Triggered = true;
                Invoke(driver, "UpdateInteraction", player);
                source.Triggered = false;
                Invoke(crank, "OnTick", .2f);
                float progress = crank.Progress;
                Check(progress > 0f, "touch press starts hold-driven crank progress without keyboard F");
                Invoke(driver, "UpdateInteraction", player);
                Invoke(crank, "OnTick", .2f);
                Check(crank.Progress > progress, "continued touch hold keeps increasing progress");
                source.Pressed = false;
                Invoke(driver, "UpdateInteraction", player);
                progress = crank.Progress;
                Invoke(crank, "OnTick", .01f);
                Check(Mathf.Approximately(crank.Progress, progress), "release ends drive without an extra turn");

                player.SetMoveInput(Vector2.left);
                Scan();
                Check(!player.HasContextInteraction, "idle scan rejects crank behind player");
                player.SetMoveInput(Vector2.right);
                Scan();
                Check(player.HasContextInteraction, "turning toward crank restores contextual interaction");
                Vector3 nearby = crank.transform.position;
                crank.transform.position = actor.transform.position + Vector3.right * 3f;
                Physics.SyncTransforms();
                Set(driver, "nextMobileContextScan", Time.unscaledTime + 10f);
                Invoke(driver, "UpdateInteraction", player);
                Check(ReferenceEquals(sensor.CurrentTarget, crank), "idle calls inside the scan interval do not rescan");
                source.Pressed = source.Triggered = true;
                Invoke(driver, "UpdateInteraction", player);
                source.Pressed = source.Triggered = false;
                Check(!player.HasContextInteraction, "real press refreshes immediately despite idle scan throttle");
                Scan();
                Check(!player.HasContextInteraction, "sensor radius does not bypass crank own interaction distance");
                crank.transform.position = nearby;
                Physics.SyncTransforms(); Scan();
                Check(player.HasContextInteraction, "returning into one-cell reach restores candidate");

                service.SetAllUnlockedForCurrentSession(false);
                Scan();
                Check(!player.HasContextInteraction && !crank.CanInteract(actor), "locked red color cannot expose a usable crank");
                service.SetAllUnlockedForCurrentSession(true);
                Scan();
                Check(player.HasContextInteraction, "scene color unlock restores crank without ability unlock requirement");

                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab");
                var brake = Object.Instantiate(basePrefab, actor.transform.position + Vector3.up * 8, Quaternion.identity)
                    .GetComponent<MechanismBaseFeature>();
                crank.BindBase(brake);
                Set(brake, "powered", true);
                Check(brake.IsSignalActive, "brake fixture has an active powered base");
                source.Pressed = source.Triggered = true;
                Scan(); source.Triggered = false;
                progress = crank.Progress;
                Invoke(crank, "OnTick", .2f);
                Check(player.HasContextInteraction && Mathf.Approximately(progress, crank.Progress),
                    "powered base preserves interaction affordance but brakes progress");
                Set(brake, "powered", false);
                Invoke(driver, "UpdateInteraction", player);
                Invoke(crank, "OnTick", .2f);
                Check(crank.Progress > progress, "unpowered base allows the same held touch to drive again");
                passed = true;
                Debug.Log("MOBILE CRANK CONTEXT VERIFICATION PASSED " + checks);
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (input != null)
                {
                    input.ClearExternalSource(source);
                    if (oldMode != null) Set(input, "platformMode", oldMode);
                }
                Physics.simulationMode = oldSimulation;
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        private sealed class TouchSource : IInputSource
        {
            public bool Pressed, Triggered;
            public InputDeviceMode ActiveDeviceMode => InputDeviceMode.Touch;
            public bool HasKeyboard => false;
            public bool HasMouse => false;
            public bool HasGamepad => false;
            public bool HasTouch => true;
            public Vector2 PointerPosition => Vector2.zero;
            public Vector2 PointerDelta => Vector2.zero;
            public bool IsActionPressed(InputActionId action) => action == InputActionId.Interact && Pressed;
            public bool WasActionPressedThisFrame(InputActionId action) => action == InputActionId.Interact && Triggered;
            public bool WasActionTriggeredThisFrame(InputActionId action) => action == InputActionId.Interact && Triggered;
            public bool WasActionReleasedThisFrame(InputActionId action) => false;
            public InputActionTrigger GetActionTrigger(InputActionId action) => InputActionTrigger.Press;
            public float ReadAxis(InputActionId action) => 0f;
            public Vector2 ReadVector2(InputActionId action) => Vector2.zero;
        }
    }
}
