using System;
using System.Linq;
using System.Reflection;
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
    /// <summary>Single-world camera-owner failure recovery and real touch-layout modal.</summary>
    public static class WorldMenuFailureVerification
    {
        private const string Flag = "WorldMenuFailureVerification";
        private static int phase, checks, firstWorld, loadEvents;
        private static double started;
        private static float phaseAt;
        private static bool viewPrepared;
        private static EditorWindow gameView;
        private static object gameViewGroup;
        private static PropertyInfo selectedSize;
        private static int originalSize, auditSize;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("WorldMenuFailureVerification requires the isolated verification project.");
            EditorSceneManager.OpenScene(GameFlowController.GameplayScenePath);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = checks = loadEvents = 0;
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
            if (EditorApplication.timeSinceStartup - started > 100) { Finish(false, "timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                GameFlowController flow = GameFlowController.Instance;
                if (phase == 0 && Ready(GameFlowSceneId.MainMenu))
                {
                    if (!viewPrepared) { PrepareGameView(); viewPrepared = true; phaseAt = Time.unscaledTime; return; }
                    if (Time.unscaledTime < phaseAt + .4f) return;
                    firstWorld = flow.WorldMenu.ActiveWorld.handle;
                    SceneManager.sceneLoaded += TrackLoad;
                    foreach (var key in Object.FindObjectsByType<Project.ColorBlocks.ColorKeyPickup>(FindObjectsSortMode.None))
                        key.gameObject.SetActive(false);
                    Check(GameplayPresentationGate.IsSuppressed, "layout starts in Gaming's real menu gate");
                    MobileControlLayout.Open(StartMenuTypography.SdfFont);
                    phaseAt = Time.unscaledTime;
                    phase = 1;
                }
                else if (phase == 1 && Time.unscaledTime > phaseAt + .25f)
                {
                    VerifyLayoutPreview();
                    Object.Destroy(Object.FindFirstObjectByType<MobileControlLayoutEditor>().gameObject);
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "healthy start accepted");
                    phase = 2;
                }
                else if (phase == 2 && Ready(GameFlowSceneId.Gaming))
                {
                    VerifyDevices();
                    Check(flow.ReturnToMainMenu(), "healthy return accepted");
                    phase = 3;
                }
                else if (phase == 3 && Ready(GameFlowSceneId.MainMenu))
                {
                    // Lose only the transient cached owner, not the actual scene's Camera.
                    // This injects a recoverable mid-transition failure without deleting the world.
                    typeof(WorldMenuPresentation).GetField("cameraManager", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(flow.WorldMenu, null);
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "camera-owner failure fixture accepted");
                    phase = 4;
                }
                else if (phase == 4 && Ready(GameFlowSceneId.MainMenu) && flow.LastTransitionFailure != null)
                {
                    var menu = Object.FindFirstObjectByType<StartMenuController>();
                    Check(flow.WorldMenu.ActiveWorld.handle == firstWorld && loadEvents == 0, "recovery retains world identity and never loads a scene");
                    Check(GameplayPresentationGate.IsSuppressed && flow.WorldMenu.IsPreviewing, "recovery owns a clean menu preview gate");
                    Check(menu.IsPresented && menu.MenuContentRoot.GetComponent<CanvasGroup>().interactable &&
                        menu.MenuContentRoot.GetComponent<CanvasGroup>().alpha == 1f, "recovery shows clickable same-scene menu");
                    Check(flow.WorldMenu.FrameHandoffCount == 0, "recovery has no screenshot/black-screen handoff");
                    VerifyDevices();
                    menu.ShowSettings();
                    Check(menu.GetComponentInChildren<SettingsPanelController>(true).gameObject.activeInHierarchy, "settings is still callable");
                    menu.ShowMain();
                    Check(flow.RequestTransition(GameFlowSceneId.Gaming), "healthy retry accepted");
                    phase = 5;
                }
                else if (phase == 5 && Ready(GameFlowSceneId.Gaming))
                {
                    Check(flow.LastTransitionFailure == null, "retry clears prior failure report");
                    Check(!GameplayPresentationGate.IsSuppressed && !flow.WorldMenu.IsPreviewing, "retry releases only its preview gate");
                    var player = Object.FindFirstObjectByType<PlayerController>();
                    Check(player != null && !player.Motor.isKinematic && player.ControlLockDepth == 0, "retry restores movable player");
                    VerifyDevices();
                    Check(flow.ReturnToMainMenu(), "normal reverse still works");
                    phase = 6;
                }
                else if (phase == 6 && Ready(GameFlowSceneId.MainMenu))
                {
                    Check(flow.WorldMenu.ActiveWorld.handle == firstWorld && loadEvents == 0, "all retry and return paths preserve one scene");
                    VerifyDevices();
                    Finish(true, checks + " checks; touch-layout modal, camera failure and same-world retry");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void TrackLoad(Scene scene, LoadSceneMode mode) => loadEvents++;

        private static void VerifyLayoutPreview()
        {
            var editor = Object.FindFirstObjectByType<MobileControlLayoutEditor>();
            Check(editor != null, "layout modal exists while gameplay is gated");
            var preview = editor.GetComponentInChildren<MobileGameplayControls>(true);
            Check(preview != null && preview.LayoutEditing && preview.Controls.gameObject.activeInHierarchy,
                "layout touch controls remain visible despite gameplay suppression");
            Check(preview.Controls.childCount >= 15 && preview.Controls.Cast<Transform>().All(x => x.gameObject.activeInHierarchy),
                "actual editable buttons are visible, not only the toolbar");
            Check(preview.GetComponentsInChildren<VirtualInputButton>(true).All(x => !x.enabled) &&
                preview.GetComponentsInChildren<VirtualDirectionButton>(true).All(x => !x.enabled), "layout does not send gameplay input");
            var button = preview.Controls.Find("右").GetComponent<RectTransform>();
            editor.Select(button);
            float before = button.sizeDelta.x;
            editor.transform.Find("工具栏/放大").GetComponent<Button>().onClick.Invoke();
            Check(button.sizeDelta.x > before, "layout toolbar resizes selected button without saving preferences");
        }

        private static void VerifyDevices()
        {
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled && x.cameraType == CameraType.Game) == 1, "one enabled world camera");
            Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1, "one enabled listener");
            Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1, "one enabled event system");
            Check(Time.timeScale == 1f && !AudioListener.pause, "simulation and audio are restored");
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
                Enum.Parse(sizeKind, "FixedResolution"), 1280, 720, "Single world menu audit");
            groupType.GetMethod("AddCustomSize").Invoke(gameViewGroup, new[] { size });
            selectedSize.SetValue(gameView, auditSize);
            gameView.Show();
            gameView.Repaint();
        }

        private static void Finish(bool okay, string report)
        {
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= TrackLoad;
            if (gameViewGroup != null)
            {
                if (gameView != null) selectedSize.SetValue(gameView, originalSize);
                gameViewGroup.GetType().GetMethod("RemoveCustomSize").Invoke(gameViewGroup, new object[] { auditSize });
            }
            if (okay) Debug.Log("WORLD MENU FAILURE AUDIT COMPLETE: " + report);
            else Debug.LogError("WORLD MENU FAILURE AUDIT FAILED: " + report);
            EditorApplication.Exit(okay ? 0 : 1);
        }
    }
}
