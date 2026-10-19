using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.Items;
using Project.StartMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Actual Gaming single-scene entry and two round trips; no author scene/save writes.</summary>
    public static class WorldMenuFusionVerification
    {
        private const string Flag = "WorldMenuFusionVerification";
        private const string MenuPath = GameFlowController.GameplayScenePath;
        private const string GamePath = "Assets/_Project/Scenes/Playing/Gaming.unity";
        private static int phase, checks, firstWorld;
        private static double start;
        private static float phaseAt;
        private static PlayerController player;
        private static StartMenuController menu;
        private static IDisposable externalLock;
        private static ColorRuntimeService colors;
        private static Sprite firstLogo;
        private static int maxWorlds;
        private static int listenerSamples;
        private static bool initialSizeSet;
        private static object gameViewGroup;
        private static EditorWindow gameView;
        private static PropertyInfo selectedSize;
        private static int originalSize;
        private static readonly List<int> temporarySizes = new List<int>();
        private static readonly HashSet<int> cameraStepSamples = new HashSet<int>();
        private static int initialLoadedScenes, loadingEvents;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("WorldMenuFusionVerification must run in the isolated verification project.");
            EditorSceneManager.OpenScene(MenuPath);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = checks = maxWorlds = listenerSamples = loadingEvents = 0;
            cameraStepSamples.Clear();
            initialSizeSet = false;
            start = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Check(bool okay, string message)
        {
            if (!okay) throw new Exception("WORLD MENU FAIL: " + message);
            checks++;
            Debug.Log("WORLD MENU PASS: " + message);
        }

        private static bool Ready(GameFlowSceneId id) => GameFlowController.Instance != null &&
            GameFlowController.Instance.IsInitialized && !GameFlowController.Instance.IsTransitioning &&
            GameFlowController.Instance.ActiveSceneId == id;

        private static void CheckDevices()
        {
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled && x.cameraType == CameraType.Game) == 1,
                "exactly one enabled game camera");
            Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1,
                "exactly one enabled AudioListener");
            Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1,
                "exactly one enabled EventSystem");
            Check(CountWorlds() == 1, "exactly one loaded Gaming world");
            Check(SceneManager.GetActiveScene().path == GamePath, "real Gaming is Unity's active scene behind the menu");
            Check(Time.timeScale == 1f && !AudioListener.pause, "global simulation and audio are not left paused");
        }

        private static int CountWorlds()
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == GamePath && SceneManager.GetSceneAt(i).isLoaded) count++;
            return count;
        }

        private static void CheckPreview()
        {
            Check(GameplayPresentationGate.IsSuppressed, "real world gameplay interaction is gated in the menu");
            Check(player.IsControlLocked && player.Motor.isKinematic, "standard player is leased and physically frozen for presentation");
            Check(!player.GetComponentsInChildren<Collider>().Any(x => x.enabled), "player cannot hit boss/contact triggers in preview");
            Check(!player.GetComponent<PlayerInputDriver>().enabled, "ability and locomotion input are not driven during preview");
            Check(menu.IsPresented && menu.Background == null, "menu overlays the real world; old blue background is removed from the asset");
            var router = Object.FindFirstObjectByType<GameUiRouter>();
            if (router != null)
            {
                router.SetPaused(true, false);
                Check(!router.IsPaused && Time.timeScale == 1f, "preview ignores gameplay pause requests without freezing its world animations");
            }
            foreach (DropItemBase key in Object.FindObjectsByType<DropItemBase>(FindObjectsSortMode.None))
            {
                Check(!key.CanAttract && !key.CanCollect, "preview suppresses pickup attraction and collection: " + key.name);
                Check(!key.CollectionRequested, "preview did not collect author item: " + key.name);
            }
            CheckDevices();
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - start > 150) { Finish(false, "timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                maxWorlds = Mathf.Max(maxWorlds, CountWorlds());
                // Every rendered PlayMode interval, including unload/load gaps, retains one
                // listener. Final-state-only checks missed the original loading warning storm.
                int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled);
                if (GameFlowController.Instance != null)
                {
                    if (listeners != 1) throw new Exception("WORLD MENU FAIL: transition frame has " + listeners + " enabled AudioListeners");
                    int cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                        .Count(x => x.isActiveAndEnabled && x.cameraType == CameraType.Game && x.cullingMask != 0);
                    if (cameras != 1) throw new Exception("WORLD MENU FAIL: startup/transition frame has " + cameras + " live world cameras");
                    listenerSamples++;
                }
                var flow = GameFlowController.Instance;
                if (flow != null && flow.IsTransitioning && flow.WorldMenu.ActiveWorld.IsValid())
                {
                    var output = Object.FindFirstObjectByType<Project.CameraModes.CameraControlManager>()?.OutputCamera;
                    if (output != null) cameraStepSamples.Add(Mathf.RoundToInt(output.orthographicSize * 10000f));
                }
                if (phase == 0 && Ready(GameFlowSceneId.MainMenu))
                {
                    if (!initialSizeSet)
                    {
                        SetGameViewResolution(1280, 720);
                        initialSizeSet = true;
                        phaseAt = Time.unscaledTime;
                        return;
                    }
                    if (Time.unscaledTime < phaseAt + .4f) return;
                    CheckResolution(1280, 720);
                    player = Object.FindFirstObjectByType<PlayerController>();
                    menu = Object.FindFirstObjectByType<StartMenuController>();
                    Check(player != null && menu != null, "main entry automatically loaded the real standard player and menu");
                    colors = ColorRuntimeService.Instance;
                    Check(!colors.IsAbilityUnlocked("red") && !colors.IsAbilityUnlocked("green") && !colors.IsAbilityUnlocked("blue"),
                        "menu preload does not grant any color ability");
                    CheckPreview();
                    firstWorld = player.gameObject.scene.handle;
                    initialLoadedScenes = SceneManager.sceneCount;
                    SceneManager.sceneLoaded += TrackUnexpectedLoad;
                    Check(!SceneManager.GetSceneByPath(GameFlowController.CompatibilityMenuScenePath).isLoaded,
                        "shipping entry does not load MainMenu at all");
                    firstLogo = menu.GetComponentsInChildren<Image>(true).First(x => x.name == "Animated Logo").sprite;
                    Capture("menu-16x9");
                    phaseAt = Time.unscaledTime;
                    phase = 1;
                }
                else if (phase == 1 && Time.unscaledTime > phaseAt + .25f)
                {
                    Sprite logo = menu.GetComponentsInChildren<Image>(true).First(x => x.name == "Animated Logo").sprite;
                    Check(logo != firstLogo, "menu logo sequence advances while gameplay is gated (world is not time-scale paused)");
                    menu.ShowSettings();
                    Check(menu.GetComponentInChildren<SettingsPanelController>(true).gameObject.activeInHierarchy, "settings reuses its existing live controller");
                    Capture("settings");
                    phaseAt = Time.unscaledTime;
                    phase = 10;
                }
                else if (phase == 10 && Time.unscaledTime > phaseAt + .2f)
                {
                    menu.GetComponentInChildren<SettingsPanelController>(true).ShowCredits();
                    Capture("credits");
                    phaseAt = Time.unscaledTime;
                    phase = 11;
                }
                else if (phase == 11 && Time.unscaledTime > phaseAt + .2f)
                {
                    menu.ShowMain();
                    // Keys close to an author spawn may legitimately play a cutscene after gameplay
                    // is released. Disable only that unrelated fixture after proving preview safety.
                    foreach (var key in Object.FindObjectsByType<ColorKeyPickup>(FindObjectsSortMode.None)) key.gameObject.SetActive(false);
                    externalLock = player.AcquireControlLock("menu independent owner regression");
                    cameraStepSamples.Clear();
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "start request accepted through the existing flow");
                    phase = 2;
                }
                else if (phase == 2 && Ready(GameFlowSceneId.Gaming))
                {
                    Check(player.gameObject.scene.handle == firstWorld, "first start does not reload the preview world");
                    Check(cameraStepSamples.Count >= 4, "start camera has multiple actual intermediate lens values, not a delayed hard cut");
                    Check(!GameplayPresentationGate.IsSuppressed && !player.Motor.isKinematic, "enter completes by releasing physical preview, not changing progress");
                    Check(player.ControlLockDepth == 1, "preview release preserves the independent external control lease");
                    externalLock.Dispose(); externalLock = null;
                    Check(player.ControlLockDepth == 0, "the external owner alone can release its own lease");
                    Check(!menu.IsPresented && player.GetComponent<PlayerInputDriver>().enabled, "only gameplay input is active after UI leaves");
                    CheckCollectibleFiltering();
                    CheckDevices();
                    Check(GameAudioService.Instance.MusicSource.clip.name.Contains("atmosphere_emin"), "gameplay retains atmosphere music routing");
                    Capture("gameplay");
                    Object.FindFirstObjectByType<GameUiRouter>().SetPaused(true, false);
                    Check(Time.timeScale == 0f, "pause fixture is active before returning");
                    Object.FindFirstObjectByType<GameUiRouter>().OpenMainMenu();
                    cameraStepSamples.Clear();
                    phase = 3;
                }
                else if (phase == 3 && Ready(GameFlowSceneId.MainMenu))
                {
                    Check(player.gameObject.scene.handle == firstWorld, "return reverses in the same real world, without unloading it");
                    Check(cameraStepSamples.Count >= 4, "return camera also crosses multiple actual intermediate lens values");
                    CheckPreview();
                    Check(GameAudioService.Instance.MusicSource.clip.name == "bmg_op", "return routes the current main menu music");
                    Capture("returned-menu");
                    SetGameViewResolution(1600, 720);
                    phaseAt = Time.unscaledTime;
                    phase = 30;
                }
                else if (phase == 30 && Time.unscaledTime > phaseAt + .3f)
                {
                    CheckResolution(1600, 720);
                    CheckButtonBounds();
                    Capture("menu-20x9");
                    phaseAt = Time.unscaledTime;
                    phase = 31;
                }
                else if (phase == 31 && Time.unscaledTime > phaseAt + .2f)
                {
                    SetGameViewResolution(1024, 768);
                    phaseAt = Time.unscaledTime;
                    phase = 32;
                }
                else if (phase == 32 && Time.unscaledTime > phaseAt + .3f)
                {
                    CheckResolution(1024, 768);
                    CheckButtonBounds();
                    Capture("menu-4x3");
                    phaseAt = Time.unscaledTime;
                    phase = 33;
                }
                else if (phase == 33 && Time.unscaledTime > phaseAt + .2f)
                {
                    SetGameViewResolution(1280, 720);
                    colors.Unlock("green");
                    SceneManager.sceneLoaded += CheckReloadedScene;
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "second start request accepted");
                    phase = 4;
                }
                else if (phase == 4 && Ready(GameFlowSceneId.Gaming))
                {
                    player = Object.FindFirstObjectByType<PlayerController>();
                    Check(player.gameObject.scene.handle == firstWorld, "second start resumes the same Gaming instance without any scene replacement");
                    Check(ColorRuntimeService.Instance == colors && colors.IsUnlocked("green") && !colors.IsAbilityUnlocked("green"),
                        "new level keeps existing independent session color/ability semantics");
                    Check(flow.WorldMenu.ActiveWorld.handle == player.gameObject.scene.handle, "flow refers only to the new player scene");
                    var follow = Object.FindFirstObjectByType<Project.CameraModes.CameraFollowController>();
                    Check(follow.Target == player.transform, "new camera follow target is the new scene's standard player");
                    Check(player.ControlLockDepth == 0 && !player.Motor.isKinematic, "second entry does not retain a stale preview lock or frozen body");
                    CheckCollectibleFiltering();
                    Check(maxWorlds == 1, "reload never runs duplicate room and color registries from two Gaming scenes");
                    Check(flow.WorldMenu.FrameHandoffCount == 0 && loadingEvents == 0 && SceneManager.sceneCount == initialLoadedScenes,
                        "both menu round trips have zero scene loads/unloads or still-frame handoffs");
                    CheckDevices();
                    player.ConfigureRespawn(PlayerRespawnMode.LoadingTransition, .1f);
                    player.EnterDeath();
                    Check(player.IsDead, "death fixture starts before returning to preview");
                    Check(flow.ReturnToMainMenu(), "second return request accepted");
                    phase = 5;
                }
                else if (phase == 5 && Ready(GameFlowSceneId.MainMenu))
                {
                    CheckPreview();
                    menu.ShowMain();
                    Check(maxWorlds == 1, "all round trips kept one world");
                    phaseAt = Time.unscaledTime;
                    phase = 6;
                }
                else if (phase == 6 && Time.unscaledTime > phaseAt + 2f)
                {
                    Check(player.IsDead && !player.IsRespawning, "dead player stays in preview without automatic slow-respawn or room reset");
                    player.RequestRespawn();
                    Check(player.IsDead && !player.IsRespawning, "manual respawn request is also suppressed in the menu");
                    CheckDevices();
                    Finish(true, checks + " checks, " + listenerSamples + " one-listener transition samples, real initial entry plus 2 round trips");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void CheckCollectibleFiltering()
        {
            foreach (var item in Object.FindObjectsByType<CollectiblePickup>(FindObjectsSortMode.None))
                foreach (var shape in item.SolidColliders)
                    if (shape != null && shape.enabled && !shape.isTrigger)
                        foreach (var playerShape in player.GetComponentsInChildren<Collider>())
                            if (playerShape.enabled && !playerShape.isTrigger && playerShape.attachedRigidbody == player.Motor)
                                Check(Physics.GetIgnoreCollision(shape, playerShape),
                                    "gameplay handoff retains collectible pass-through: " + item.name);
        }

        private static void CheckButtonBounds()
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            foreach (Button button in menu.StartButtonTransform.parent.GetComponentsInChildren<Button>())
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Rect safe = Screen.safeArea;
                Check(corners.All(x => x.x >= safe.xMin - 1f && x.x <= safe.xMax + 1f && x.y >= safe.yMin - 1f && x.y <= safe.yMax + 1f),
                    "menu button stays inside safe area at " + Screen.width + "x" + Screen.height + ": " + button.name);
            }
        }

        private static void CheckResolution(int width, int height) =>
            Check(Screen.width == width && Screen.height == height,
                $"actual GameView resolution is {width}x{height}, not a screenshot label ({Screen.width}x{Screen.height})");

        private static void SetGameViewResolution(int width, int height)
        {
            // Screen.SetResolution does not resize the Editor GameView. Temporary fixed sizes
            // exercise the actual CanvasScaler/safe-area chain, and are removed before exit.
            Assembly editor = typeof(EditorWindow).Assembly;
            Type viewType = editor.GetType("UnityEditor.GameView", true);
            if (gameView == null)
            {
                gameView = EditorWindow.GetWindow(viewType);
                selectedSize = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                originalSize = (int)selectedSize.GetValue(gameView);
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
                object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
                gameViewGroup = sizesType.GetProperty("currentGroup", BindingFlags.Instance | BindingFlags.Public).GetValue(sizes);
            }
            Type groupType = gameViewGroup.GetType();
            int index = (int)groupType.GetMethod("GetTotalCount").Invoke(gameViewGroup, null);
            Type sizeType = editor.GetType("UnityEditor.GameViewSize", true);
            Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType", true);
            object size = Activator.CreateInstance(sizeType, Enum.Parse(sizeKind, "FixedResolution"), width, height, "World menu audit");
            groupType.GetMethod("AddCustomSize").Invoke(gameViewGroup, new[] { size });
            temporarySizes.Add(index);
            selectedSize.SetValue(gameView, index);
            gameView.Show();
            gameView.Repaint();
        }

        private static void RestoreGameView()
        {
            if (gameViewGroup == null) return;
            if (gameView != null) selectedSize.SetValue(gameView, originalSize);
            MethodInfo remove = gameViewGroup.GetType().GetMethod("RemoveCustomSize");
            for (int i = temporarySizes.Count - 1; i >= 0; i--)
                remove.Invoke(gameViewGroup, new object[] { temporarySizes[i] });
            temporarySizes.Clear();
            gameViewGroup = null;
        }

        private static void CheckReloadedScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != GamePath) return;
            Check(GameplayPresentationGate.IsSuppressed, "incoming level starts with the shared interaction gate already held");
            Check(CountWorlds() == 1, "old room registry owners were unloaded before the next level loaded");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var key in root.GetComponentsInChildren<ColorKeyPickup>(true))
                {
                    Check(!key.CollectionRequested && !key.CanAttract, "incoming keys remain uncollected and unattracted before control handoff");
                    key.gameObject.SetActive(false);
                }
        }

        private static void TrackUnexpectedLoad(Scene scene, LoadSceneMode mode) => loadingEvents++;

        private static void Capture(string name)
        {
            Debug.Log($"WORLD MENU FRAME: {name} {Screen.width}x{Screen.height}");
            string folder = Path.Combine(Application.dataPath, "../Library/WorldMenuFusionVerification");
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        }

        private static void Finish(bool okay, string message)
        {
            externalLock?.Dispose(); externalLock = null;
            SceneManager.sceneLoaded -= CheckReloadedScene;
            SceneManager.sceneLoaded -= TrackUnexpectedLoad;
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
            RestoreGameView();
            if (okay) Debug.Log("WORLD MENU COMPLETE: " + message); else Debug.LogError("WORLD MENU FAILED: " + message);
            EditorApplication.Exit(okay ? 0 : 1);
        }
    }
}
