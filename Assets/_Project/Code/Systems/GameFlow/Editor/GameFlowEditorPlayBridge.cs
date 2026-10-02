using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.GameFlow.Editor
{
    [InitializeOnLoad]
    public static class GameFlowEditorPlayBridge
    {
        private const string MenuPath =
            "Tools/2026TapTap/游戏流程/Play时自动从Bootstrap启动";
        private const string PreferenceKey =
            "2026TapTap.GameFlow.AutoBootstrapOnPlay";
        private const string RequestedSceneKey =
            "2026TapTap.GameFlow.RequestedScene";
        private const string RequestedSceneValidKey =
            "2026TapTap.GameFlow.RequestedSceneValid";

        static GameFlowEditorPlayBridge()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ApplyStartScenePreference;
        }

        [MenuItem(MenuPath, priority = 30)]
        private static void ToggleAutoBootstrap()
        {
            UseFormalFlow = !UseFormalFlow;
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggleAutoBootstrap()
        {
            Menu.SetChecked(MenuPath, UseFormalFlow);
            return true;
        }

        public static void ApplyStartScenePreference()
        {
            if (Application.isBatchMode)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            SceneAsset bootstrap = UseFormalFlow
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    GameFlowSceneScaffolder.BootstrapPath)
                : null;
            EditorSceneManager.playModeStartScene = bootstrap;
            Menu.SetChecked(MenuPath, UseFormalFlow);
        }

        public static bool UseFormalFlow
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set
            {
                EditorPrefs.SetBool(PreferenceKey, value);
                ApplyStartScenePreference();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CaptureRequestedFlowScene();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (UseFormalFlow)
                {
                    GameFlowSceneId requested =
                        (GameFlowSceneId)SessionState.GetInt(
                            RequestedSceneKey,
                            (int)GameFlowSceneId.MainMenu);
                    GameFlowLaunchOverride.Set(requested);
                }
                else
                {
                    StartDirectSceneDebugSupport();
                }
            }
        }

        private static void CaptureRequestedFlowScene()
        {
            GameFlowSceneId requested = GameFlowSceneId.MainMenu;
            GameSceneCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(
                    GameFlowSceneScaffolder.CatalogPath);
            string activePath = EditorSceneManager.GetActiveScene().path;
            if (catalog != null &&
                catalog.TryGetIdForPath(activePath, out GameFlowSceneId sceneId))
            {
                requested = sceneId;
                SessionState.SetBool(RequestedSceneValidKey, true);
            }
            else
            {
                SessionState.SetBool(RequestedSceneValidKey, false);
            }

            SessionState.SetInt(RequestedSceneKey, (int)requested);
        }

        private static void StartDirectSceneDebugSupport()
        {
            if (!SessionState.GetBool(RequestedSceneValidKey, false) ||
                GameFlowController.Instance != null)
            {
                return;
            }

            GameSceneCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(
                    GameFlowSceneScaffolder.CatalogPath);
            if (catalog == null)
            {
                return;
            }

            GameFlowSceneId requested =
                (GameFlowSceneId)SessionState.GetInt(
                    RequestedSceneKey,
                    (int)GameFlowSceneId.MainMenu);
            GameObject host = new GameObject("__EditorDirectSceneFlow");
            Object.DontDestroyOnLoad(host);
            GameFlowController controller = host.AddComponent<GameFlowController>();
            controller.Configure(catalog, requested, true);
        }
    }
}
