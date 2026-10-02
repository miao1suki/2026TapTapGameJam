using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.CameraModes.Editor
{
    [InitializeOnLoad]
    internal static class CameraModeEditPreviewDriver
    {
        private static readonly HashSet<CameraModeController> ActiveControllers =
            new HashSet<CameraModeController>();

        private static double lastUpdateTime;

        static CameraModeEditPreviewDriver()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= SettleAll;
            AssemblyReloadEvents.beforeAssemblyReload += SettleAll;
            AssemblyReloadEvents.beforeAssemblyReload -=
                ReleaseEditorRequests;
            AssemblyReloadEvents.beforeAssemblyReload +=
                ReleaseEditorRequests;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static void StartTransition(
            CameraModeController controller,
            CameraViewMode mode)
        {
            if (controller == null)
            {
                return;
            }

            CameraModeEditorRequests.RequestMode(
                controller,
                mode,
                false);
            TrackTransition(controller);
        }

        internal static void TrackTransition(
            CameraModeController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!controller.IsTransitioning)
            {
                MarkDirty(controller);
                return;
            }

            ActiveControllers.Add(controller);
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Min(0.1f, (float)(currentTime - lastUpdateTime));
            lastUpdateTime = currentTime;
            CameraModeController[] controllers =
                new CameraModeController[ActiveControllers.Count];
            ActiveControllers.CopyTo(controllers);

            foreach (CameraModeController controller in controllers)
            {
                if (controller == null)
                {
                    ActiveControllers.Remove(controller);
                    continue;
                }

                controller.Tick(deltaTime);
                MarkDirty(controller);
                if (!controller.IsTransitioning)
                {
                    ActiveControllers.Remove(controller);
                }
            }

            SceneView.RepaintAll();
            if (ActiveControllers.Count == 0)
            {
                EditorApplication.update -= Update;
            }
        }

        private static void SettleAll()
        {
            foreach (CameraModeController controller in ActiveControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.SettleTransitionForDisable();
                MarkDirty(controller);
            }

            ActiveControllers.Clear();
            EditorApplication.update -= Update;
        }

        private static void ReleaseEditorRequests()
        {
            CameraModeEditorRequests.ReleaseAll();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SettleAll();
                CameraModeEditorRequests.ReleaseAll();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                CameraModeEditorRequests.ReleaseAll();
            }
        }

        private static void MarkDirty(CameraModeController controller)
        {
            EditorUtility.SetDirty(controller);
            Camera camera = controller.ControlledCamera;
            if (camera != null)
            {
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(camera.transform);
            }

            if (controller.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }
    }
}
