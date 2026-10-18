using System;
using System.Linq;
using System.Reflection;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.InputAbstraction;
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
    /// <summary>Fault injection and real layout-modal regression. Isolated project only.</summary>
    public static class WorldMenuFailureVerification
    {
        private const string Flag = "WorldMenuFailureVerification";
        private const string MenuPath = "Assets/_Project/Scenes/Start/MainMenu.unity";
        private const string GamePath = "Assets/_Project/Scenes/Playing/Gaming.unity";
        private static int phase, checks;
        private static double started;
        private static float phaseAt;
        private static bool removeIncomingCamera;
        private static GameSceneCatalog healthyCatalog, faultCatalog;
        private static EditorWindow gameView;
        private static object gameViewGroup;
        private static PropertyInfo selectedSize;
        private static int originalSize, auditSize;
        private static bool viewPrepared;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("WorldMenuFailureVerification requires the isolated verification project.");
            EditorSceneManager.OpenScene(MenuPath);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = checks = 0;
            viewPrepared = false;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("WORLD MENU FAILURE AUDIT FAIL: " + description);
            checks++;
            Debug.Log("WORLD MENU FAILURE AUDIT PASS: " + description);
        }

        private static bool Ready(GameFlowSceneId id) => GameFlowController.Instance != null &&
            GameFlowController.Instance.IsInitialized && !GameFlowController.Instance.IsTransitioning &&
            GameFlowController.Instance.ActiveSceneId == id;

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 160) { Finish(false, "timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                GameFlowController flow = GameFlowController.Instance;
                if (phase == 0 && Ready(GameFlowSceneId.MainMenu))
                {
                    if (!viewPrepared)
                    {
                        PrepareGameView();
                        viewPrepared = true;
                        phaseAt = Time.unscaledTime;
                        return;
                    }
                    if (Time.unscaledTime < phaseAt + .4f) return;
                    Check(Screen.width == 1280 && Screen.height == 720, "fault fixture has a visible rendered 1280x720 GameView for the real GPU handoff");
                    healthyCatalog = flow.Catalog;
                    SceneManager.sceneLoaded += PrepareIncomingWorld;
                    DisableUnrelatedKeyFixture(SceneManager.GetSceneByPath(GamePath));
                    Check(GameplayPresentationGate.IsSuppressed, "layout fixture starts in the real menu preview gate");
                    MobileControlLayout.Open(StartMenuTypography.SdfFont);
                    phaseAt = Time.unscaledTime;
                    phase = 1;
                }
                else if (phase == 1 && Time.unscaledTime > phaseAt + .25f)
                {
                    VerifyLayoutPreview();
                    Object.Destroy(Object.FindFirstObjectByType<MobileControlLayoutEditor>().gameObject);
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "healthy initial start accepted");
                    phase = 2;
                }
                else if (phase == 2 && Ready(GameFlowSceneId.Gaming))
                {
                    Check(flow.ReturnToMainMenu(), "healthy return before second-start failure accepted");
                    phase = 3;
                }
                else if (phase == 3 && Ready(GameFlowSceneId.MainMenu))
                {
                    UseFaultPath(string.Empty);
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "null-operation loader fixture accepted after a real previous session");
                    phase = 4;
                }
                else if (phase == 4 && Ready(GameFlowSceneId.MainMenu) && flow.LastTransitionFailure != null)
                {
                    VerifyRecovery("empty Gaming path after screenshot and old-world unload");
                    Check(flow.WorldMenu.FrameHandoffCount == 1, "failed second start actually exercised the captured-frame unload branch");
                    UseFaultPath("Assets/__MissingWorldAudit/NoSuchWorld.unity");
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "throwing loader fixture accepted");
                    phase = 5;
                }
                else if (phase == 5 && Ready(GameFlowSceneId.MainMenu) && flow.LastTransitionFailure != null)
                {
                    VerifyRecovery("missing scene loader exception");
                    RestoreHealthyCatalog();
                    removeIncomingCamera = true;
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "missing CameraControlManager fixture accepted");
                    phase = 6;
                }
                else if (phase == 6 && Ready(GameFlowSceneId.MainMenu) && flow.LastTransitionFailure != null)
                {
                    VerifyRecovery("loaded world without CameraControlManager");
                    Check(flow.LastTransitionFailure.Contains("CameraControlManager"), "missing camera failure reached the guarded component boundary");
                    removeIncomingCamera = false;
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "healthy retry accepted after three recoveries");
                    phase = 7;
                }
                else if (phase == 7 && Ready(GameFlowSceneId.Gaming))
                {
                    Check(flow.LastTransitionFailure == null, "successful retry clears the prior error report");
                    Check(!GameplayPresentationGate.IsSuppressed && !flow.WorldMenu.IsPreviewing, "retry releases its own preview and shared gate");
                    var player = Object.FindFirstObjectByType<PlayerController>();
                    Check(player != null && !player.Motor.isKinematic && player.ControlLockDepth == 0, "retry owns a movable unfrozen standard player");
                    VerifyDevices();
                    Check(flow.ReturnToMainMenu(), "normal reverse transition still works after recovery");
                    phase = 8;
                }
                else if (phase == 8 && Ready(GameFlowSceneId.MainMenu))
                {
                    Check(GameplayPresentationGate.IsSuppressed, "healthy return acquires normal menu preview again");
                    VerifyDevices();
                    Finish(true, checks + " checks; layout modal and 3 failure/retry branches");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void VerifyLayoutPreview()
        {
            var editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            Check(editor != null, "settings layout modal exists while gameplay is gated");
            var preview = editor.GetComponentInChildren<MobileGameplayControls>(true);
            Check(preview != null && preview.LayoutEditing && preview.Controls.gameObject.activeInHierarchy,
                "layout-mode touch controls remain visible despite gameplay suppression");
            Check(preview.Controls.childCount >= 15 && preview.Controls.Cast<Transform>().All(x => x.gameObject.activeInHierarchy),
                "actual editable touch buttons are visible, not only the toolbar");
            Check(preview.GetComponentsInChildren<VirtualInputButton>(true).All(x => !x.enabled) &&
                preview.GetComponentsInChildren<VirtualDirectionButton>(true).All(x => !x.enabled),
                "visible layout buttons cannot send gameplay input");
            var button = preview.Controls.Find("右").GetComponent<RectTransform>();
            editor.Select(button);
            float before = button.sizeDelta.x;
            editor.transform.Find("工具栏/放大").GetComponent<Button>().onClick.Invoke();
            Check(button.sizeDelta.x > before, "layout toolbar can resize a selected actual button without saving preferences");
        }

        private static void PrepareGameView()
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            Type viewType = editor.GetType("UnityEditor.GameView", true);
            gameView = EditorWindow.GetWindow(viewType);
            selectedSize = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            originalSize = (int)selectedSize.GetValue(gameView);
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            gameViewGroup = sizesType.GetProperty("currentGroup", BindingFlags.Instance | BindingFlags.Public).GetValue(sizes);
            Type groupType = gameViewGroup.GetType();
            auditSize = (int)groupType.GetMethod("GetTotalCount").Invoke(gameViewGroup, null);
            Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType", true);
            object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize", true),
                Enum.Parse(sizeKind, "FixedResolution"), 1280, 720, "World menu failure audit");
            groupType.GetMethod("AddCustomSize").Invoke(gameViewGroup, new[] { size });
            selectedSize.SetValue(gameView, auditSize);
            gameView.Show();
            gameView.Repaint();
        }

        private static void RestoreGameView()
        {
            if (gameViewGroup == null) return;
            if (gameView != null) selectedSize.SetValue(gameView, originalSize);
            gameViewGroup.GetType().GetMethod("RemoveCustomSize").Invoke(gameViewGroup, new object[] { auditSize });
            gameViewGroup = null;
        }

        private static void VerifyRecovery(string fixture)
        {
            var flow = GameFlowController.Instance;
            var menu = Object.FindFirstObjectByType<StartMenuController>();
            Check(!flow.IsTransitioning && flow.ActiveSceneId == GameFlowSceneId.MainMenu, fixture + ": stable retryable flow");
            Check(!GameplayPresentationGate.IsSuppressed && !flow.WorldMenu.IsPreviewing, fixture + ": no leaked gameplay gate or physical preview lease");
            Check(!flow.WorldMenu.ActiveWorld.IsValid(), fixture + ": no stale active-world reference");
            Check(menu != null && menu.IsPresented && menu.MenuContentRoot.GetComponent<CanvasGroup>().interactable &&
                menu.MenuContentRoot.GetComponent<CanvasGroup>().alpha == 1f, fixture + ": visible interactive menu restored");
            Check(!flow.GetComponentsInChildren<RawImage>(true).Any(x => x.name == "Previous World"), fixture + ": frozen-frame overlay removed");
            Check(!SceneManager.GetSceneByPath(GamePath).isLoaded, fixture + ": failed world fully unloaded");
            Check(Time.timeScale == 1f && !AudioListener.pause, fixture + ": global state restored");
            menu.ShowSettings();
            Check(menu.GetComponentInChildren<SettingsPanelController>(true).gameObject.activeInHierarchy, fixture + ": menu business remains callable");
            menu.ShowMain();
            Check(menu.GetComponent<StartMenuFlowBridge>() != null && menu.GetComponent<StartMenuFlowBridge>().isActiveAndEnabled,
                fixture + ": existing start/exit bridge remains installed");
            VerifyDevices();
        }

        private static void VerifyDevices()
        {
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled && x.cameraType == CameraType.Game) == 1,
                "recovery/handoff has exactly one enabled game camera");
            Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1,
                "recovery/handoff has exactly one enabled AudioListener");
            Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1,
                "recovery/handoff has exactly one enabled EventSystem");
        }

        private static void UseFaultPath(string path)
        {
            RestoreHealthyCatalog();
            faultCatalog = Object.Instantiate(healthyCatalog);
            faultCatalog.Configure(healthyCatalog.PersistentScenePaths, healthyCatalog.FlowScenes.Select(x =>
                new GameFlowSceneEntry(x.Id, x.Id == GameFlowSceneId.Gaming ? path : x.ScenePath, x.IsGameplay)));
            GameFlowController.Instance.Configure(faultCatalog, GameFlowSceneId.MainMenu, false);
        }

        private static void RestoreHealthyCatalog()
        {
            GameFlowController.Instance.Configure(healthyCatalog, GameFlowSceneId.MainMenu, false);
            if (faultCatalog != null) Object.Destroy(faultCatalog);
            faultCatalog = null;
        }

        private static void PrepareIncomingWorld(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != GamePath) return;
            DisableUnrelatedKeyFixture(scene);
            if (!removeIncomingCamera) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (CameraControlManager manager in root.GetComponentsInChildren<CameraControlManager>(true))
                    if (manager != null) Object.DestroyImmediate(manager.gameObject);
        }

        private static void DisableUnrelatedKeyFixture(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ColorKeyPickup key in root.GetComponentsInChildren<ColorKeyPickup>(true)) key.gameObject.SetActive(false);
        }

        private static void Finish(bool okay, string report)
        {
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= PrepareIncomingWorld;
            RestoreGameView();
            if (okay) Debug.Log("WORLD MENU FAILURE AUDIT COMPLETE: " + report);
            else Debug.LogError("WORLD MENU FAILURE AUDIT FAILED: " + report);
            EditorApplication.Exit(okay ? 0 : 1);
        }
    }
}
