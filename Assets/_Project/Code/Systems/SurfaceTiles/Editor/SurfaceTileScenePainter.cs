using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.SurfaceTiles.Editor
{
    [InitializeOnLoad]
    internal static class SurfaceTileScenePainter
    {
        private static SurfaceTileBlock lastDragBlock;
        private static SurfaceTileFace lastDragFace;
        private static Vector2Int lastDragCell = new Vector2Int(-1, -1);

        static SurfaceTileScenePainter()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!SurfaceTileEditorState.Painting)
            {
                ClearHover();
                return;
            }

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                SurfaceTileEditorState.Painting = false;
                ClearHover();
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            if (evt.type == EventType.Layout && !evt.alt)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            bool hasCell = TryFindCell(
                evt.mousePosition,
                out SurfaceTileBlock block,
                out SurfaceTileFace face,
                out Vector2Int cell);
            SurfaceTileEditorState.HoverBlock = hasCell ? block : null;
            if (hasCell)
            {
                SurfaceTileEditorState.HoverFace = face;
                SurfaceTileEditorState.HoverCell = cell;
                SurfaceTilePaintMode hoverMode = evt.control
                    ? SurfaceTilePaintMode.Pick
                    : evt.shift
                        ? SurfaceTilePaintMode.Erase
                        : SurfaceTileEditorState.Mode;
                DrawGrid(
                    block,
                    face,
                    cell,
                    hoverMode == SurfaceTilePaintMode.Paint);
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                ResetDrag();
            }

            if (!hasCell || evt.alt || evt.button != 0 ||
                (evt.type != EventType.MouseDown && evt.type != EventType.MouseDrag))
            {
                return;
            }

            if (block == lastDragBlock && face == lastDragFace && cell == lastDragCell)
            {
                evt.Use();
                return;
            }

            SurfaceTilePaintMode mode = evt.control
                ? SurfaceTilePaintMode.Pick
                : evt.shift
                    ? SurfaceTilePaintMode.Erase
                    : SurfaceTileEditorState.Mode;
            Apply(block, face, cell, mode);
            lastDragBlock = block;
            lastDragFace = face;
            lastDragCell = cell;
            evt.Use();
        }

        private static bool TryFindCell(
            Vector2 mouse,
            out SurfaceTileBlock block,
            out SurfaceTileFace face,
            out Vector2Int cell)
        {
            block = null;
            face = default;
            cell = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                float.PositiveInfinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) =>
                left.distance.CompareTo(right.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                SurfaceTileBlock candidate =
                    hits[index].collider.GetComponentInParent<SurfaceTileBlock>();
                if (candidate == null || candidate.SurfaceCollider == null)
                {
                    continue;
                }

                Vector3 localNormal = candidate.transform
                    .InverseTransformDirection(hits[index].normal).normalized;
                SurfaceTileFace candidateFace =
                    SurfaceTileGeometry.FaceFromLocalNormal(localNormal);
                Vector3 localPoint = candidate.transform
                    .InverseTransformPoint(hits[index].point);
                Vector2Int grid = candidate.GetGridSize(candidateFace);
                if (SurfaceTileEditorState.UseForcedFace &&
                    candidateFace != SurfaceTileEditorState.ForcedFace)
                {
                    continue;
                }

                if (!SurfaceTileGeometry.TryGetCell(
                        candidate.SurfaceCollider,
                        candidateFace,
                        localPoint,
                        grid,
                        out Vector2Int candidateCell))
                {
                    continue;
                }

                block = candidate;
                face = candidateFace;
                cell = candidateCell;
                return true;
            }

            return false;
        }

        private static void Apply(
            SurfaceTileBlock block,
            SurfaceTileFace face,
            Vector2Int cell,
            SurfaceTilePaintMode mode)
        {
            if (mode == SurfaceTilePaintMode.Pick)
            {
                if (block.TryGetPlacement(face, cell, out SurfaceTilePlacement placement))
                {
                    SurfaceTileEditorState.SelectedTileId = placement.TileId;
                    SurfaceTileEditorState.QuarterTurns = placement.QuarterTurns;
                    SurfaceTileEditorState.FlipX = placement.FlipX;
                    SurfaceTileEditorState.FlipY = placement.FlipY;
                    SurfaceTileEditorState.Anchor = placement.Anchor;
                    SurfaceTileEditorState.OffsetCells = placement.OffsetCells;
                }

                return;
            }

            Undo.RecordObject(block, mode == SurfaceTilePaintMode.Erase
                ? "擦除方块贴画"
                : "绘制方块贴画");
            if (mode == SurfaceTilePaintMode.Erase)
            {
                block.RemoveTile(face, cell);
            }
            else if (block.Palette != null &&
                     block.Palette.TryGet(
                         SurfaceTileEditorState.SelectedTileId,
                         out _))
            {
                block.AddTile(
                    face,
                    cell,
                    SurfaceTileEditorState.SelectedTileId,
                    SurfaceTileEditorState.QuarterTurns,
                    SurfaceTileEditorState.FlipX,
                    SurfaceTileEditorState.FlipY,
                    SurfaceTileEditorState.Anchor,
                    SurfaceTileEditorState.Stack,
                    SurfaceTileEditorState.OffsetCells);
            }

            SurfaceTileMeshBuilder.RefreshPreview(block);
            EditorUtility.SetDirty(block);
            EditorSceneManager.MarkSceneDirty(block.gameObject.scene);
            SceneView.RepaintAll();
        }

        private static void DrawGrid(
            SurfaceTileBlock block,
            SurfaceTileFace face,
            Vector2Int hoverCell,
            bool previewSelectedTile)
        {
            SurfaceTileFaceBasis basis = SurfaceTileGeometry.GetLocalBasis(
                block.SurfaceCollider,
                face);
            Vector2Int grid = block.GetGridSize(face);
            Rect hoverRect = new Rect(hoverCell, Vector2.one);
            Rect fitRect = hoverRect;
            if (previewSelectedTile && block.Palette != null &&
                block.Palette.TryGet(
                    SurfaceTileEditorState.SelectedTileId,
                    out SurfaceTilePalette.Entry entry))
            {
                hoverRect = SurfaceTileGeometry.GetPlacementRect(
                    entry,
                    hoverCell,
                    SurfaceTileEditorState.QuarterTurns,
                    SurfaceTileEditorState.Anchor,
                    SurfaceTileEditorState.OffsetCells);
                fitRect = SurfaceTileGeometry.GetPlacementRect(
                    entry,
                    hoverCell,
                    SurfaceTileEditorState.QuarterTurns,
                    SurfaceTileEditorState.Anchor);
            }

            bool valid = SurfaceTileGeometry.PlacementFitsGrid(fitRect, grid) &&
                         Mathf.Abs(SurfaceTileEditorState.OffsetCells.x) <=
                         SurfaceTileBlock.MaximumOffsetCells &&
                         Mathf.Abs(SurfaceTileEditorState.OffsetCells.y) <=
                         SurfaceTileBlock.MaximumOffsetCells;
            Matrix4x4 previous = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix = block.transform.localToWorldMatrix;
            Handles.color = new Color(0.3f, 0.85f, 1f, 0.78f);
            const float bias = 0.004f;
            for (int x = 0; x <= grid.x; x++)
            {
                float u = (float)x / grid.x;
                Handles.DrawLine(
                    basis.Point(u, 0f, bias),
                    basis.Point(u, 1f, bias));
            }

            for (int y = 0; y <= grid.y; y++)
            {
                float v = (float)y / grid.y;
                Handles.DrawLine(
                    basis.Point(0f, v, bias),
                    basis.Point(1f, v, bias));
            }

            float u0 = hoverRect.xMin / grid.x;
            float u1 = hoverRect.xMax / grid.x;
            float v0 = hoverRect.yMin / grid.y;
            float v1 = hoverRect.yMax / grid.y;
            Vector3[] corners =
            {
                basis.Point(u0, v0, bias * 1.5f),
                basis.Point(u1, v0, bias * 1.5f),
                basis.Point(u1, v1, bias * 1.5f),
                basis.Point(u0, v1, bias * 1.5f)
            };
            Handles.DrawSolidRectangleWithOutline(
                corners,
                valid
                    ? new Color(0.1f, 0.75f, 1f, 0.22f)
                    : new Color(1f, 0.15f, 0.1f, 0.22f),
                valid
                    ? new Color(0.2f, 0.95f, 1f, 1f)
                    : new Color(1f, 0.2f, 0.1f, 1f));
            Handles.matrix = previous;
            Handles.color = previousColor;
        }

        private static void ResetDrag()
        {
            lastDragBlock = null;
            lastDragCell = new Vector2Int(-1, -1);
        }

        private static void ClearHover()
        {
            SurfaceTileEditorState.HoverBlock = null;
            ResetDrag();
        }
    }
}
