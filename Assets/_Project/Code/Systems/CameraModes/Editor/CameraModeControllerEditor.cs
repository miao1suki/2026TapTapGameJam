using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.CameraModes.Editor
{
    [CustomEditor(typeof(CameraModeController))]
    [CanEditMultipleObjects]
    public sealed class CameraModeControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty cameraManager;
        private SerializedProperty controlPriority;
        private SerializedProperty initialMode;
        private SerializedProperty side2D;
        private SerializedProperty perspective3D;
        private SerializedProperty transition;
        private SerializedProperty onTransitionStarted;
        private SerializedProperty onModeChanged;

        private void OnEnable()
        {
            cameraManager = serializedObject.FindProperty("cameraManager");
            controlPriority = serializedObject.FindProperty("controlPriority");
            initialMode = serializedObject.FindProperty("initialMode");
            side2D = serializedObject.FindProperty("side2D");
            perspective3D = serializedObject.FindProperty("perspective3D");
            transition = serializedObject.FindProperty("transition");
            onTransitionStarted = serializedObject.FindProperty("onTransitionStarted");
            onModeChanged = serializedObject.FindProperty("onModeChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "2D 使用平视正交相机；3D 使用可环绕的透视相机，可由鼠标输入调用 RotatePerspective 与 SetPerspectiveAngles。" +
                "CameraControlManager 是唯一 Camera 写入者；CameraModeController 是唯一 2D/3D 与 2D Yaw 权威。" +
                "CameraFollowController、Timeline 和编辑器通过 RequestMode 提交申请；" +
                "CameraControlManager 会在最终写入前强制应用权威投影。",
                MessageType.Info);

            DrawSection("统一控制", cameraManager, controlPriority);
            DrawSection("启动模式", initialMode);
            DrawSection("2D 平台视角", side2D);
            DrawSection("3D 俯视视角", perspective3D);
            DrawSection("模式切换", transition);
            DrawSection("事件", onTransitionStarted, onModeChanged);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawRuntimeStatus();
            DrawModeButtons();

            EditorGUILayout.HelpBox(
                "常用操作已放在 Scene View 的“2D / 3D 相机”Overlay 中。",
                MessageType.None);

            if (GUILayout.Button("打开独立相机工具窗口"))
            {
                CameraModeSetupWindow.OpenWindow();
            }
        }

        private static void DrawSection(string title, params SerializedProperty[] properties)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (SerializedProperty property in properties)
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        private void DrawRuntimeStatus()
        {
            if (targets.Length != 1)
            {
                return;
            }

            CameraModeController controller = (CameraModeController)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("当前模式", controller.CurrentMode);
                EditorGUILayout.EnumPopup("目标模式", controller.TargetMode);
                EditorGUILayout.Toggle("正在切换", controller.IsTransitioning);
                EditorGUILayout.Slider("切换进度", controller.NormalizedTransitionTime, 0f, 1f);
                EditorGUILayout.Toggle("持有控制权", controller.HasControl);
                EditorGUILayout.TextField(
                    "当前控制者",
                    controller.Manager != null ? controller.Manager.ActiveControlName : "无 Manager");
            }
        }

        private void DrawModeButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                Application.isPlaying ? "运行时控制" : "编辑模式预览",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Application.isPlaying ? "切换到 2D" : "预览 2D"))
                {
                    ApplyMode(CameraViewMode.Side2D);
                }

                if (GUILayout.Button(Application.isPlaying ? "切换到 3D" : "预览 3D"))
                {
                    ApplyMode(CameraViewMode.Perspective3D);
                }
            }
        }

        private void ApplyMode(CameraViewMode mode)
        {
            foreach (Object selectedTarget in targets)
            {
                CameraModeController controller = (CameraModeController)selectedTarget;
                Camera camera = controller.ControlledCamera;
                Undo.RecordObject(controller, "Change Camera View Mode");
                if (camera != null)
                {
                    Undo.RecordObject(camera, "Change Camera View Mode");
                    Undo.RecordObject(camera.transform, "Change Camera View Mode");
                }

                if (Application.isPlaying)
                {
                    CameraModeEditorRequests.RequestMode(
                        controller,
                        mode,
                        false);
                }
                else
                {
                    CameraModeEditorRequests.RequestMode(
                        controller,
                        mode,
                        true);
                    EditorUtility.SetDirty(controller);
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

            SceneView.RepaintAll();
        }
    }
}
