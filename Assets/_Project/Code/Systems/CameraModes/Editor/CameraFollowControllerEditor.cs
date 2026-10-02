using UnityEditor;
using UnityEngine;

namespace Project.CameraModes.Editor
{
    [CustomEditor(typeof(CameraFollowController))]
    public sealed class CameraFollowControllerEditor :
        UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            CameraFollowController controller =
                (CameraFollowController)target;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("2D / 3D 模式申请", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            CameraViewMode requested =
                (CameraViewMode)EditorGUILayout.EnumPopup(
                    "申请模式",
                    controller.RequestedMode);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(
                    controller,
                    "申请相机模式");
                if (requested == CameraViewMode.Side2D)
                {
                    controller.SetRequestedMode(
                        requested,
                        controller.RequestedSide2DYawDegrees,
                        false);
                }
                else
                {
                    controller.SetRequestedMode(
                        requested,
                        false);
                }
                CameraModeEditorRequests.Release(
                    controller.ModeController,
                    false);
                if (!Application.isPlaying)
                {
                    CameraModeEditPreviewDriver.TrackTransition(
                        controller.ModeController);
                }
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
                Repaint();
            }

            EditorGUILayout.LabelField(
                "2D 转向累加",
                EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("向左90"))
                {
                    Undo.RecordObject(
                        controller,
                        "向左申请相机转向");
                    controller.TurnRequestedLeft90(
                        false);
                    CameraModeEditorRequests.Release(
                        controller.ModeController,
                        false);
                    if (!Application.isPlaying)
                    {
                        CameraModeEditPreviewDriver.TrackTransition(
                            controller.ModeController);
                    }
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                    Repaint();
                }
                if (GUILayout.Button("向右90"))
                {
                    Undo.RecordObject(
                        controller,
                        "向右申请相机转向");
                    controller.TurnRequestedRight90(
                        false);
                    CameraModeEditorRequests.Release(
                        controller.ModeController,
                        false);
                    if (!Application.isPlaying)
                    {
                        CameraModeEditPreviewDriver.TrackTransition(
                            controller.ModeController);
                    }
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                    Repaint();
                }
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup(
                    "当前模式",
                    controller.FollowMode);
                EditorGUILayout.EnumPopup(
                    "权威目标",
                    controller.ModeController != null
                        ? controller.ModeController.TargetMode
                        : controller.RequestedMode);
                if (controller.RequestedMode == CameraViewMode.Side2D)
                {
                    EditorGUILayout.FloatField(
                        "申请 Yaw",
                        controller.RequestedSide2DYawDegrees);
                }
            }
        }
    }
}
