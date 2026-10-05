using System.Collections.Generic;
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
        private static bool strokeActive;
        private static Vector2Int strokeStart;
        private static Vector2Int strokeEnd;
        private static bool selectionStroke;
        private static bool selectionAdditive;
        private static bool selectionDragged;

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
                selectionStroke = false;
                strokeActive = false;
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
                strokeActive = false;
                selectionStroke = false;
                LevelEditorState.Tool = LevelEditorTool.Select;
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            if (LevelEditorState.Tool == LevelEditorTool.Player)
            {
                strokeActive = false;
                selectionStroke = false;
                lastCell = new Vector2Int(int.MinValue, int.MinValue);
                LevelEditorPlayerService.EnforceEditPlane();
                return;
            }

            if (evt.type == EventType.Layout && !evt.alt)
            {
                HandleUtility.AddDefaultControl(
                    GUIUtility.GetControlID(FocusType.Passive));
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (selectionStroke)
                {
                    if (selectionDragged) SelectArea(strokeStart, strokeEnd, selectionAdditive);
                    else SelectAt(strokeStart, selectionAdditive);
                    selectionStroke = false;
                }
                else if (strokeActive)
                {
                    CompleteStroke();
                    strokeActive = false;
                }

                lastCell = new Vector2Int(int.MinValue, int.MinValue);
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            if (!TryGetCell(evt.mousePosition, out Vector2Int cell))
            {
                lastCell = new Vector2Int(int.MinValue, int.MinValue);
                return;
            }

            DrawHover(cell);
            if (selectionStroke && selectionDragged) DrawSelectionArea(strokeStart, strokeEnd);
            if (evt.alt || evt.button != 0)
            {
                return;
            }

            if (evt.type == EventType.MouseDown &&
                LevelEditorState.Tool == LevelEditorTool.Select)
            {
                selectionStroke = true;
                selectionAdditive = evt.control || evt.command;
                selectionDragged = false;
                strokeStart = strokeEnd = cell;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown &&
                IsStrokeTool(LevelEditorState.Tool))
            {
                strokeActive = true;
                strokeStart = cell;
                strokeEnd = cell;
                lastCell = cell;
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            if (evt.type == EventType.MouseDrag && selectionStroke)
            {
                selectionDragged |= strokeEnd != cell;
                strokeEnd = cell;
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            if (evt.type == EventType.MouseDrag && strokeActive)
            {
                strokeEnd = cell;
                lastCell = cell;
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            if (evt.type != EventType.MouseDown ||
                cell == lastCell)
            {
                return;
            }

            lastCell = cell;
            evt.Use();
            SceneView.RepaintAll();
        }

        private static bool IsStrokeTool(LevelEditorTool tool)
        {
            return tool == LevelEditorTool.Paint ||
                   tool == LevelEditorTool.Erase;
        }

        private static void CompleteStroke()
        {
            IEnumerable<Vector2Int> cells =
                BuildAreaCells(strokeStart, strokeEnd);
            foreach (Vector2Int cell in cells)
            {
                if (LevelEditorState.Tool == LevelEditorTool.Erase)
                {
                    LevelEditorBlockFactory.EraseAt(cell);
                }
                else
                {
                    PaintAt(cell);
                }
            }
        }

        private static IEnumerable<Vector2Int> BuildAreaCells(
            Vector2Int start,
            Vector2Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }

        private static IEnumerable<Vector2Int> BuildStrokeCells(
            Vector2Int start,
            Vector2Int end)
        {
            int x = start.x;
            int y = start.y;
            int deltaX = Mathf.Abs(end.x - start.x);
            int deltaY = -Mathf.Abs(end.y - start.y);
            int stepX = start.x < end.x ? 1 : -1;
            int stepY = start.y < end.y ? 1 : -1;
            int error = deltaX + deltaY;

            while (true)
            {
                yield return new Vector2Int(x, y);
                if (x == end.x && y == end.y)
                {
                    yield break;
                }

                int doubled = error * 2;
                if (doubled >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }

                if (doubled <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
            }
        }

        private static void SelectAt(Vector2Int cell, bool additive)
        {
            LevelEditorPlacedBlock block =
                LevelEditorBlockFactory.FindAt(cell);
            if (!additive)
            {
                Selection.activeGameObject = block != null ? block.gameObject : null;
                return;
            }
            var selected = new HashSet<Object>(Selection.objects);
            if (block != null)
            {
                if (!selected.Add(block.gameObject)) selected.Remove(block.gameObject);
            }
            Selection.objects = new List<Object>(selected).ToArray();
        }

        private static void SelectArea(Vector2Int start, Vector2Int end, bool additive)
        {
            var selected = additive
                ? new HashSet<Object>(Selection.objects)
                : new HashSet<Object>();
            foreach (Vector2Int cell in BuildAreaCells(start, end))
            {
                LevelEditorPlacedBlock block = LevelEditorBlockFactory.FindAt(cell);
                if (block != null) selected.Add(block.gameObject);
            }
            Selection.objects = new List<Object>(selected).ToArray();
        }

        private static void DrawSelectionArea(Vector2Int start, Vector2Int end)
        {
            float size = Mathf.Max(.05f, LevelEditorState.CellSize);
            float left = Mathf.Min(start.x, end.x) * size;
            float right = (Mathf.Max(start.x, end.x) + 1) * size;
            float bottom = Mathf.Min(start.y, end.y) * size;
            float top = (Mathf.Max(start.y, end.y) + 1) * size;
            var corners = new[] {
                new Vector3(left, bottom, 0f), new Vector3(right, bottom, 0f),
                new Vector3(right, top, 0f), new Vector3(left, top, 0f) };
            Handles.DrawSolidRectangleWithOutline(corners,
                new Color(.18f, .62f, 1f, .15f), new Color(.28f, .75f, 1f, .9f));
        }

        private static void PaintAt(Vector2Int cell)
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            int propIndex = LevelEditorState.SelectedPropIndex;
            if (palette != null &&
                propIndex >= 0 &&
                propIndex < palette.PropEntries.Count)
            {
                LevelEditorBlockFactory.PlaceProp(
                    palette.PropEntries[propIndex],
                    cell);
                return;
            }

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
