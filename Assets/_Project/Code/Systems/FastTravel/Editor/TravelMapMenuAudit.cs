using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.Player;
using Project.StartMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.FastTravel.Editor
{
    /// <summary>Real Gaming regression: map safety must release before menu captures body state.</summary>
    public static class TravelMapMenuAudit
    {
        private const string Flag = "TravelMapMenuAudit.Running";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<string> Report = new List<string>();
        private static int phase, checks, roundTrips, playerId, worldHandle, loadedScenes, sceneLoads;
        private static double started;
        private static float phaseAt;
        private static PlayerController player;
        private static PlayerInputDriver input;
        private static WorldTravelMap map;
        private static GameFlowController flow;
        private static StartMenuController menu;
        private static RigidbodyConstraints constraints;
        private static bool bodyUsesGravity;

        public static void Run()
        {
            string path = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            if (!path.EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase) ||
                EditorApplication.isPlaying)
                throw new InvalidOperationException("TravelMapMenuAudit only runs from the stopped isolated FlowRetirementProbe project.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("TravelMapMenuAudit requires -noaudio; never run audible gameplay probes.");
            EditorSceneManager.OpenScene(GameFlowController.GameplayScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = checks = roundTrips = sceneLoads = 0;
            Report.Clear();
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static bool Ready(GameFlowSceneId id) => flow != null && flow.IsInitialized &&
            !flow.IsTransitioning && flow.ActiveSceneId == id;

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120d)
                    throw new TimeoutException("Travel map/menu round trip timed out at phase " + phase);
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                AudioListener.volume = 0f; // Also silent if an audio service reconfigures listeners.
                flow = GameFlowController.Instance;
                if (phase == 0)
                {
                    if (!Ready(GameFlowSceneId.MainMenu)) return;
                    player = Object.FindFirstObjectByType<PlayerController>();
                    menu = Object.FindFirstObjectByType<StartMenuController>();
                    Check(player != null && menu != null, "formal Gaming installed the standard player and fused menu");
                    map = player.GetComponent<WorldTravelMap>();
                    input = player.GetComponent<PlayerInputDriver>();
                    Check(map != null && input != null, "standard player bootstrapped the real travel map and input driver");
                    playerId = player.GetInstanceID();
                    worldHandle = player.gameObject.scene.handle;
                    loadedScenes = SceneManager.sceneCount;
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    CheckPreview("initial menu");
                    // Unrelated author keys may schedule a color-unlock cutscene next to spawn.
                    // Disable only these runtime fixtures; do not write scene/assets or grant progress.
                    foreach (ColorKeyPickup key in Object.FindObjectsByType<ColorKeyPickup>(FindObjectsSortMode.None))
                        if (key.gameObject.scene.handle == worldHandle) key.gameObject.SetActive(false);
                    Check(flow.EnterGameplayImmediatelyForDevelopment(), "explicit fixture entry releases initial menu preview");
                    constraints = player.Motor.constraints;
                    bodyUsesGravity = player.Motor.useGravity;
                    CheckGameplay("initial gameplay");
                    phaseAt = Time.unscaledTime;
                    phase = 1;
                    return;
                }
                if (phase == 1 && Time.unscaledTime >= phaseAt + .25f)
                {
                    CheckGameplay("settled gameplay before map " + (roundTrips + 1));
                    OpenMapAndReturn();
                    phase = 2;
                    return;
                }
                if (phase == 2 && Ready(GameFlowSceneId.MainMenu))
                {
                    roundTrips++;
                    CheckPreview("returned menu " + roundTrips);
                    CheckMapReleased("completed menu transition");
                    map.Close();
                    map.Close();
                    CheckPreview("idempotent map cleanup preserves menu-owned freeze");
                    Check(!map.TryOpen(), "map cannot reopen behind the menu");
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "normal animated gameplay re-entry accepted");
                    phase = 3;
                    return;
                }
                if (phase == 3 && Ready(GameFlowSceneId.Gaming))
                {
                    CheckGameplay("gameplay after round trip " + roundTrips);
                    CheckMapReleased("gameplay after round trip " + roundTrips);
                    Check(player.Motor.constraints == constraints && player.Motor.useGravity == bodyUsesGravity,
                        "map/menu handoff preserves authored body gravity and constraints");
                    phaseAt = Time.unscaledTime;
                    phase = roundTrips < 2 ? 1 : 4;
                    return;
                }
                if (phase == 4 && Time.unscaledTime >= phaseAt + .3f)
                {
                    CheckGameplay("several gameplay frames after both round trips");
                    Check(sceneLoads == 0 && SceneManager.sceneCount == loadedScenes,
                        "both round trips use the same world without any scene loading");
                    // Reopening catches stale safety-handle epochs as well as a visually hidden stale panel.
                    VerifyMapAvailable();
                    Check(map.TryOpen(), "map still opens after two menu handoffs");
                    map.Close();
                    CheckMapReleased("final explicit map close");
                    CheckGameplay("final map close restores a dynamic controllable player");
                    Finish(true, "Two real Gaming map/menu/gameplay round trips; no author scene or progress writes.");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void VerifyMapAvailable()
        {
            Check(map.IsUnlocked && map.CanUseHere,
                "map is available at the actual Gaming spawn without a living Boss");
        }

        private static void OpenMapAndReturn()
        {
            VerifyMapAvailable();
            Check(map.TryOpen() && map.IsOpen, "actual travel map opens in formal Gaming");
            Check(player.Motor.isKinematic && player.IsControlLocked && player.IsInvulnerable &&
                player.ControlLockDepth == 1 && Read<int>(player, "travelSafetyDepth") == 1,
                "open map owns exactly one player safety/control lease");
            Check(Read<object>(map, "playerLock") != null && Read<object>(map, "presentationLock") != null &&
                GameplayPresentationGate.IsSuppressed, "open map owns both concrete leases, not just hidden UI");

            bool signalled = false, synchronousClose = false, releasedBeforeMenuCapture = false;
            Action<GameFlowSceneId> observer = target =>
            {
                if (target != GameFlowSceneId.MainMenu) return;
                signalled = true;
                // Registered after TryOpen/BindFlow: inspect in the same event dispatch,
                // before ReturnToMenu starts and before the next Update can hide the defect.
                synchronousClose = !map.IsOpen && !map.IsTravelling && MapLeasesReleased();
                releasedBeforeMenuCapture = !player.Motor.isKinematic && !player.IsControlLocked &&
                    player.ControlLockDepth == 0 && Read<int>(player, "travelSafetyDepth") == 0 &&
                    !GameplayPresentationGate.IsSuppressed;
            };
            bool accepted;
            flow.TransitionStarted += observer;
            try { accepted = flow.RequestTransition(GameFlowSceneId.MainMenu); }
            finally { flow.TransitionStarted -= observer; }
            Check(accepted && signalled, "return request synchronously emits TransitionStarted");
            Check(synchronousClose && releasedBeforeMenuCapture,
                "TransitionStarted closes map and restores dynamic body before menu captures its state");
            CheckMapReleased("immediately when RequestTransition returns, before another Update");
            Check(player.Motor.isKinematic && player.IsControlLocked && GameplayPresentationGate.IsSuppressed &&
                flow.WorldMenu.IsPreviewing, "menu immediately takes its own preview freeze after map safety release");
        }

        private static bool MapLeasesReleased() =>
            Read<object>(map, "playerLock") == null && Read<object>(map, "presentationLock") == null &&
            !Read<CameraControlHandle>(map, "cameraHandle").IsValid && !Read<bool>(map, "ownsCursor");

        private static void CheckMapReleased(string context)
        {
            Check(!map.IsOpen && !map.IsTravelling && MapLeasesReleased() &&
                Read<int>(player, "travelSafetyDepth") == 0,
                context + ": map has no residual UI, travel, cursor, camera or player leases");
            GameObject panel = Read<GameObject>(map, "panel");
            Check(panel == null || !panel.activeSelf, context + ": map panel is actually inactive");
        }

        private static void CheckPreview(string context)
        {
            CheckSameWorld(context);
            Check(flow.WorldMenu.IsPreviewing && menu.IsPresented && GameplayPresentationGate.IsSuppressed,
                context + ": fused menu owns world presentation");
            Check(player.Motor.isKinematic && player.IsControlLocked && player.ControlLockDepth == 1 && !input.enabled,
                context + ": only menu owns the player freeze and control lock");
        }

        private static void CheckGameplay(string context)
        {
            CheckSameWorld(context);
            Check(Ready(GameFlowSceneId.Gaming) && !flow.WorldMenu.IsPreviewing && !menu.IsPresented,
                context + ": flow and presentation agree on gameplay");
            Check(!player.Motor.isKinematic && !player.IsControlLocked && player.ControlLockDepth == 0 &&
                input.enabled && !GameplayPresentationGate.IsSuppressed && !player.IsDead && !player.IsRespawning,
                context + ": player is dynamic, alive and unlocked with live input");
            Check(Time.timeScale == 1f, context + ": global timescale is not left paused");
        }

        private static void CheckSameWorld(string context)
        {
            Check(player != null && player.GetInstanceID() == playerId && player.gameObject.scene.handle == worldHandle &&
                player.gameObject.scene.path == GameFlowController.GameplayScenePath &&
                flow.WorldMenu.ActiveWorld.handle == worldHandle && SceneManager.GetActiveScene().handle == worldHandle,
                context + ": same real Gaming player/world instance, not a replacement fixture");
            Check(string.IsNullOrEmpty(flow.LastTransitionFailure), context + ": no fallback transition recovery");
        }

        private static T Read<T>(object target, string name) => (T)Field(target, name).GetValue(target);
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, PrivateInstance) ??
            throw new MissingFieldException(target.GetType().FullName, name);
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneLoads++;
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException("Check " + checks + ": " + message);
            Report.Add("PASS " + checks + ": " + message);
        }

        private static void Finish(bool passed, string details)
        {
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SessionState.EraseBool(Flag);
            string result = (passed ? "PASS " : "FAIL ") + checks + "\n" + string.Join("\n", Report) + "\n" + details;
            Directory.CreateDirectory("Library");
            File.WriteAllText("Library/TravelMapMenuAudit.txt", result);
            if (passed) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
