using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.LevelEditor.Editor
{
    [InitializeOnLoad]
    internal static class LevelEditorScenePainter
    {
        private static Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);

        static LevelEditorScenePainter()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !LevelEditorState.EditMode)
            {
                lastCell = new Vector2Int(int.MinValue, int.MinValue);
                return;
            }

            LevelEditorViewLock.Tick(sceneView);
            DrawGrid(sceneView);
            if (SurfaceTileEditorBridge.IsPainting)
            {
                return;
            }

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                LevelEditorState.Tool = LevelEditorTool.Select;
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            if (LevelEditorState.Tool == LevelEditorTool.Player)
            {
                lastCell = new Vector2Int(int.MinValue, int.MinValue);
                LevelEditorPlayerService.EnforceEditPlane();
                return;
            }

            if (evt.type == EventType.Layout && !evt.alt)
            {
                HandleUtility.AddDefaultControl(
                    GUIUtility.GetControlID(FocusType.Passive));
            }

            if (!TryGetCell(evt.mousePosition, out Vector2Int cell))
            {
                lastCell = new Vector2Int(int.MinValue, int.MinValue);
                return;
            }

            DrawHover(cell);
            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                lastCell = new Vector2Int(int.MinValue, int.MinValue);
            }

            if (evt.alt || evt.button != 0 ||
                (evt.type != EventType.MouseDown &&
                 evt.type != EventType.MouseDrag) ||
                cell == lastCell)
            {
                return;
            }

            switch (LevelEditorState.Tool)
            {
                case LevelEditorTool.Select:
                    SelectAt(cell);
                    break;
                case LevelEditorTool.Paint:
                    PaintAt(cell);
                    break;
                case LevelEditorTool.Erase:
                    LevelEditorBlockFactory.EraseAt(cell);
                    break;
            }

            lastCell = cell;
            evt.Use();
            SceneView.RepaintAll();
        }

        private static void SelectAt(Vector2Int cell)
        {
            LevelEditorPlacedBlock block =
                LevelEditorBlockFactory.FindAt(cell);
            Selection.activeGameObject =
                block != null ? block.gameObject : null;
        }

        private static void PaintAt(Vector2Int cell)
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            int index = LevelEditorState.SelectedEntryIndex;
            if (palette == null || index < 0 ||
                index >= palette.Entries.Count)
            {
                return;
            }

            LevelEditorBlockFactory.Place(palette.Entries[index], cell);
        }

        private static void DrawGrid(SceneView sceneView)
        {
            if (sceneView == null || sceneView.camera == null)
            {
                return;
            }

            float size = Mathf.Max(.05f, LevelEditorState.CellSize);
            Vector3 center = sceneView.pivot;
            float halfHeight = sceneView.camera.orthographicSize * 1.15f;
            float halfWidth = halfHeight * sceneView.camera.aspect * 1.15f;
            float left = Mathf.Floor((center.x - halfWidth) / size) * size;
            float right = center.x + halfWidth;
            float bottom = Mathf.Floor((center.y - halfHeight) / size) * size;
            float top = center.y + halfHeight;

            CompareFunction previousZTest = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = CompareFunction.Always;
            Handles.color = new Color(0.28f, 0.62f, 0.85f, 0.28f);

            int lineCount = 0;
            for (float x = left; x <= right && lineCount < 260; x += size)
            {
                Handles.DrawLine(
                    new Vector3(x, bottom, 0f),
                    new Vector3(x, top, 0f));
                lineCount++;
            }

            for (float y = bottom; y <= top && lineCount < 520; y += size)
            {
                Handles.DrawLine(
                    new Vector3(left, y, 0f),
                    new Vector3(right, y, 0f));
                lineCount++;
            }

            Handles.color = new Color(0.35f, 0.84f, 1f, 0.72f);
            Handles.DrawLine(
                new Vector3(0f, bottom, 0f),
                new Vector3(0f, top, 0f));
            Handles.DrawLine(
                new Vector3(left, 0f, 0f),
                new Vector3(right, 0f, 0f));
            Handles.zTest = previousZTest;
            Handles.color = previousColor;
        }

        private static void DrawHover(Vector2Int cell)
        {
            float size = Mathf.Max(.05f, LevelEditorState.CellSize);
            Vector3 min = new Vector3(cell.x * size, cell.y * size, 0f);
            Vector3 max = min + new Vector3(size, size, 0f);
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(max.x, min.y, 0f),
                new Vector3(max.x, max.y, 0f),
                new Vector3(min.x, max.y, 0f),
            };

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Color color = LevelEditorState.Tool == LevelEditorTool.Erase
                ? new Color(1f, 0.28f, 0.22f, 0.22f)
                : new Color(0.22f, 0.78f, 1f, 0.22f);
            Handles.DrawSolidRectangleWithOutline(
                corners,
                color,
                color * 1.5f);
            Handles.zTest = previousZTest;
        }

        private static bool TryGetCell(
            Vector2 mousePosition,
            out Vector2Int cell)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance))
            {
                cell = default;
                return false;
            }

            cell = LevelEditorBlockFactory.WorldToCell(
                ray.GetPoint(distance));
            return true;
        }
    }
}
