using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    internal static class MechanismPreview
    {
        private static BlockFeature picking;
        internal static void BeginPick(BlockFeature target)
        {
            picking = target;
            SceneView.duringSceneGui -= Pick;
            SceneView.duringSceneGui += Pick;
            SceneView.RepaintAll();
        }
        private static void Pick(SceneView view)
        {
            if (picking == null || EditorApplication.isPlaying) { Stop(); return; }
            var e = Event.current;
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape || e.type == EventType.MouseDown && e.button == 1) { e.Use(); Stop(); return; }
            if (e.type != EventType.MouseDown || e.button != 0) return;
            var go = HandleUtility.PickGameObject(e.mousePosition, false);
            if (picking is MovingPlatformFeature lift && lift.IsCrankControlled)
            {
                var controller = go != null ? go.GetComponentInParent<CrankPlatformFeature>() : null;
                if (controller == null) { view.ShowNotification(new GUIContent("请选择曲柄，Esc 取消")); e.Use(); return; }
                Undo.RecordObject(lift,"绑定曲柄"); lift.BindCrank(controller); Save(lift); e.Use(); Stop(); return;
            }
            var source = go != null ? go.GetComponentInParent<MechanismBaseFeature>() : null;
            if (source == null) { view.ShowNotification(new GUIContent("请选择基座，Esc 取消")); e.Use(); return; }
            Undo.RecordObject(picking, "绑定基座");
            if (picking is MovingPlatformFeature platform) platform.BindBase(source);
            if (picking is CrankPlatformFeature crank) crank.BindBase(source);
            Save(picking);
            e.Use(); Stop();
        }
        private static void Stop() { picking = null; SceneView.duringSceneGui -= Pick; SceneView.RepaintAll(); }
        private static void Save(BlockFeature feature)
        {
            EditorUtility.SetDirty(feature); PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
            EditorSceneManager.MarkSceneDirty(feature.gameObject.scene);
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(MovingPlatformFeature platform, GizmoType type)
        {
            Handles.color = new Color(.3f,.9f,1f,.9f);
            Handles.DrawDottedLine(platform.PreviewStart, platform.PreviewEnd, 5f);
            float cell = GridCellSizeUtility.Resolve(platform);
            Handles.DrawWireCube(platform.PreviewEnd, Vector3.one * cell);
            if (platform.Path.IsRange)
            {
                Handles.DrawWireCube(platform.PreviewStart, Vector3.one * cell);
                Handles.DrawWireDisc(platform.PreviewCenter, Vector3.forward, cell * .12f);
            }
            Vector3 direction = platform.PreviewFirstTarget - platform.PreviewOrigin;
            if (direction.sqrMagnitude > .0001f)
                Handles.ArrowHandleCap(0, platform.PreviewOrigin,
                    Quaternion.LookRotation(direction), Mathf.Min(cell * .6f, direction.magnitude), EventType.Repaint);
            if (platform.CrankController != null)
            {
                Handles.color = new Color(1f,.78f,.3f,.9f);
                Handles.DrawDottedLine(platform.transform.position,platform.CrankController.transform.position,5f);
            }
            if (platform.LinkedControls == null) return;
            foreach (var control in platform.LinkedControls) if (control != null)
            {
                bool isButton =
                    control is Project.Mechanisms.DoorButton ||
                    control is ButtonLockFeature;
                Handles.color = isButton
                    ? new Color(1f,.35f,.25f,.9f)
                    : new Color(1f,.45f,.8f,.9f);
                Handles.DrawDottedLine(platform.transform.position, control.transform.position, 5f);
            }
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(CrankPlatformFeature crank, GizmoType type)
        {
            if (crank.BrakeBase == null) return;
            Handles.color = new Color(1f,.45f,.8f,.9f); Handles.DrawDottedLine(crank.transform.position,crank.BrakeBase.transform.position,5f);
        }
    }
}
