using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Rooms.Editor
{
    [CustomEditor(typeof(RoomLifecycleManager))]
    public sealed class RoomLifecycleManagerEditor : UnityEditor.Editor
    {
        private sealed class Boundary
        {
            internal int revision;
            internal readonly System.Collections.Generic.List<Vector3[]> lines = new System.Collections.Generic.List<Vector3[]>();
            internal readonly System.Collections.Generic.List<Color> colors = new System.Collections.Generic.List<Color>();
        }
        private static readonly System.Collections.Generic.Dictionary<RoomLifecycleManager, Boundary> Boundaries =
            new System.Collections.Generic.Dictionary<RoomLifecycleManager, Boundary>();
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("房间与重生"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var settings = ProjectInspectorUtility.CreateFoldout("运行设置", true);
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "player", "玩家"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "autoRespawnOnDeath", "死亡后自动重生"));
            root.Add(settings);
            root.Add(ProjectInspectorUtility.CreateReadOnlyRow("当前区域", () => ((RoomLifecycleManager)target).CurrentRegionId ?? "区域外", owner: target));
            root.Add(ProjectInspectorUtility.CreateReadOnlyRow("区域数量", () => ((RoomLifecycleManager)target).Regions.Count.ToString(), owner: target));
            var reset = new Button(() => { var manager = (RoomLifecycleManager)target; if (manager != null) manager.ResetRegion(manager.CurrentRegionId); }) { text = "重置当前区域" };
            reset.SetEnabled(Application.isPlaying); root.Add(reset);
            root.Add(ProjectInspectorUtility.CreateHelp("区域边界来自关卡编辑器世界图。离开区域或重生会恢复物体的布置状态，保留门、按钮、钥匙及颜色解锁进度。"));
            ProjectInspectorUtility.Bind(root, serializedObject); return root;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawRegions(RoomLifecycleManager manager, GizmoType unused)
        {
            if (Application.isPlaying || Event.current != null && Event.current.type != EventType.Repaint) return;
            if (!Boundaries.TryGetValue(manager, out var boundary) || boundary.revision != manager.Revision)
            {
                boundary = new Boundary { revision = manager.Revision };
                foreach (var region in manager.Regions)
                {
                    var occupied = new System.Collections.Generic.HashSet<Vector2Int>(region.cells);
                    var lines = new System.Collections.Generic.List<Vector3>();
                    boundary.colors.Add(Color.HSVToRGB((uint)Animator.StringToHash(region.id) % 1000 / 1000f, .6f, .9f));
                    foreach (var cell in region.cells)
                    {
                        Vector3 p = new Vector3(cell.x, -cell.y - 1, 0) * manager.CellSize;
                        Vector3 x = Vector3.right * manager.CellSize, y = Vector3.up * manager.CellSize;
                        if (!occupied.Contains(cell + Vector2Int.left)) { lines.Add(p); lines.Add(p + y); }
                        if (!occupied.Contains(cell + Vector2Int.right)) { lines.Add(p + x); lines.Add(p + x + y); }
                        if (!occupied.Contains(cell + Vector2Int.up)) { lines.Add(p); lines.Add(p + x); }
                        if (!occupied.Contains(cell + Vector2Int.down)) { lines.Add(p + y); lines.Add(p + x + y); }
                    }
                    boundary.lines.Add(lines.ToArray());
                }
                Boundaries[manager] = boundary;
            }
            Matrix4x4 previous = Handles.matrix;
            Handles.matrix = manager.transform.localToWorldMatrix;
            for (int i = 0; i < boundary.lines.Count; i++) { Handles.color = boundary.colors[i]; Handles.DrawLines(boundary.lines[i]); }
            Handles.matrix = previous;
        }
    }

    [CustomEditor(typeof(RoomResetObject))]
    public sealed class RoomResetObjectEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("区域物体"));
            root.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "regionId", "所属区域 ID", "由关卡编辑器自动设置；手动放置时留空可在启动时按位置识别。"));
            root.Add(ProjectInspectorUtility.CreateReadOnlyRow("重置策略", () => ((RoomResetObject)target).Persistent ? "保留状态" : "恢复布置状态", owner: target));
            ProjectInspectorUtility.Bind(root, serializedObject); return root;
        }
    }

    public static class RespawnAnchorGizmos
    {
        private static readonly Vector3[] DashedRing = BuildRing();
        private static Vector3[] BuildRing()
        {
            var points = new Vector3[64];
            for (int i = 0; i < 32; i++)
            {
                float a = (i * 2 + 1) * Mathf.PI * 2 / 64;
                float b = (i * 2 + 2) * Mathf.PI * 2 / 64;
                points[i * 2] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                points[i * 2 + 1] = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0);
            }
            return points;
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(RespawnAnchorFeature anchor, GizmoType unused)
        {
            if (Application.isPlaying || Event.current != null && Event.current.type != EventType.Repaint) return;
            float radius = anchor.ActivationRadiusBlocks * GridCellSizeUtility.Resolve(anchor);
            if (!ScenePreviewCulling.Visible(new Bounds(anchor.transform.position, Vector3.one * radius * 2))) return;
            Handles.color = new Color(.2f, 1f, .75f, .85f);
            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(anchor.transform.position, Quaternion.identity, Vector3.one * radius);
            Handles.DrawLines(DashedRing);
            Handles.matrix = previousMatrix;
            Handles.Label(anchor.transform.position + Vector3.up * radius, "重生点");
        }
    }
}
