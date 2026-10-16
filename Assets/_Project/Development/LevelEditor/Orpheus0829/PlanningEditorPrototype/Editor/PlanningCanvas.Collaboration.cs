using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Project.LevelEditor.Editor;
using UnityEditor;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private bool editWaterLayer;
        private PlanningDocument importPreview;
        private Vector2Int importAnchor;
        private bool importValid;
        private RectInt importBounds;
        private readonly List<RectInt> importSpans = new List<RectInt>();
        public event Action<RectInt> ExportAreaSelected;

        internal void CapturePlayerStart()
        {
            var player = LevelEditorState.Player;
            if (document == null || player == null || EditorApplication.isPlayingOrWillChangePlaymode ||
                player.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;
            Vector2 plan = new Vector2(player.transform.position.x, -player.transform.position.y) / Mathf.Max(.05f, LevelEditorState.CellSize);
            var owner = PlanningWorldUtility.OwnerAt(document, Vector2Int.FloorToInt(plan));
            document.playerStartRoomId = owner?.id;
            document.hasUnassignedPlayerStart = owner == null;
            document.playerStartWorld = player.transform.position / Mathf.Max(.05f, LevelEditorState.CellSize);
            if (owner != null) document.playerStartLocal = plan - (Vector2)PlanningWorldUtility.Bounds(owner).position;
        }

        internal bool MoveWorldSelection(Vector2Int delta)
        {
            var player = LevelEditorState.Player;
            bool movePlayer = false;
            if (player != null && !EditorApplication.isPlayingOrWillChangePlaymode && player.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            {
                var plan = new Vector2(player.transform.position.x, -player.transform.position.y) / Mathf.Max(.05f, LevelEditorState.CellSize);
                var owner = PlanningWorldUtility.OwnerAt(document, Vector2Int.FloorToInt(plan));
                movePlayer = owner != null && worldSelection.Contains(owner.id);
            }
            if (!PlanningWorldUtility.TryMove(document, worldSelection, regionSelection, delta)) return false;
            if (movePlayer)
            {
                LevelEditorPlayerService.SetPlayerPosition(player.transform.position + new Vector3(delta.x, -delta.y, 0) * LevelEditorState.CellSize, false);
                CapturePlayerStart();
                UpdatePlayerOverlay();
            }
            return true;
        }

        public void SetWaterEditing(bool value) { editWaterLayer = value; MarkDirtyRepaint(); }
        public void SetImportPreview(PlanningDocument source, Vector2Int anchor, bool valid)
        {
            if (importPreview != source)
            {
                importSpans.Clear();
                if (source != null)
                {
                    importBounds = PlanningFragmentUtility.Bounds(source);
                    foreach (var row in source.rooms.SelectMany(r => r.cells).GroupBy(c => c.y))
                    {
                        var columns = row.Select(c => c.x).Distinct().OrderBy(x => x).ToArray();
                        int start = columns[0], end = start + 1;
                        foreach (int x in columns.Skip(1))
                        {
                            if (x == end) { end++; continue; }
                            importSpans.Add(new RectInt(start, row.Key, end - start, 1)); start = x; end = x + 1;
                        }
                        importSpans.Add(new RectInt(start, row.Key, end - start, 1));
                    }
                }
            }
            importPreview = source; importAnchor = anchor; importValid = valid; MarkDirtyRepaint();
        }

        public void FocusImportPreview()
        {
            if (importPreview == null) return;
            CenterOn(importAnchor.x + importBounds.width * .5f, importAnchor.y + importBounds.height * .5f);
        }

        public void BeginAreaExport()
        {
            SetMapTool(PlanningMapTool.ExportArea);
            StatusChanged?.Invoke("拖拽框选要导出的区域。");
        }

        private IEnumerable<(PlanningRoom room, List<PlanningBox> boxes, PlanningBox box)> HitOrder()
        {
            return AllBoxes().Where(item => !editWaterLayer || PlanningWaterLayerUtility.IsWater(item.box, palette))
                .Reverse().OrderBy(item => PlanningWaterLayerUtility.IsWater(item.box, palette) ? 1 : 0);
        }

        private void DrawTransferPreview(Painter2D painter)
        {
            if (importPreview == null) return;
            Vector2Int shift = importAnchor - importBounds.position;
            var color = importValid ? new Color(.25f, 1f, .6f, .8f) : new Color(1f, .3f, .25f, .8f);
            foreach (var span in importSpans)
            {
                var rect = CellRect(span.x + shift.x, span.y + shift.y, span.width, span.height);
                if (!rect.Overlaps(contentRect)) continue;
                FillRect(painter, rect, new Color(color.r, color.g, color.b, .12f), 0);
                StrokeRect(painter, rect, color, 1f);
            }
            StrokeRect(painter, CellRect(importAnchor.x, importAnchor.y, importBounds.width, importBounds.height), color, 3f);
        }
    }
}
