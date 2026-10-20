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
        private VisualElement importPreviewLayer;
        private bool draggingImport;
        private Vector2Int importDragAnchor;
        public event Action<Vector2Int> ImportPreviewMoved;
        public event Action ImportPreviewCancelled;
        public event Action<RectInt> ExportAreaSelected;

        public new void MarkDirtyRepaint()
        {
            // Invalidate both surfaces outside generateVisualContent. UI Toolkit forbids
            // changing another element's render data while the tree is being rendered.
            if (importPreviewLayer != null)
            {
                importPreviewLayer.style.display = mode == PlanningCanvasMode.World && importPreview != null
                    ? DisplayStyle.Flex : DisplayStyle.None;
                importPreviewLayer.MarkDirtyRepaint();
            }
            base.MarkDirtyRepaint();
        }

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

        public void SetWaterEditing(bool value)
        {
            if (editWaterLayer == value) return;
            CancelPointerInteraction();
            editWaterLayer = value;
            mergeSelection.Clear(); selectedBoxId = null; movingBox = null;
            bindingConsumerId = null;
            objectPreviewLayer.style.opacity = value ? .2f : 1f;
            UpdateDetailPreviewImages(); UpdatePlayerOverlay(); MarkDirtyRepaint();
        }
        private bool CanEditPlacement(PlanningBox box)
        {
            if (!editWaterLayer || PlanningWaterLayerUtility.IsWater(box, palette)) return true;
            StatusChanged?.Invoke("仅操作水层：请选择水体，或关闭水层模式放置其他物品。");
            return false;
        }

        private bool CanPlaceWaterLayer(PlanningRoom owner, PlanningBox box, Vector2Int position, PlanningBox ignored = null)
        {
            if (PlanningWaterLayerUtility.CanPlace(document, owner, box, position, palette, ignored)) return true;
            StatusChanged?.Invoke("水与岩浆不能放在同一格；其他物体可以与水叠放。");
            return false;
        }
        public void SetImportPreview(PlanningDocument source, Vector2Int anchor, bool valid)
        {
            bool changedSource = importPreview != source;
            importPreview = source;
            importAnchor = anchor;
            importValid = valid;
            if (importPreviewLayer == null)
            {
                importPreviewLayer = new VisualElement { name = "PlanningImportPreviewLayer", pickingMode = PickingMode.Ignore };
                importPreviewLayer.StretchToParentSize();
                importPreviewLayer.generateVisualContent += context => DrawTransferPreview(context.painter2D);
                Add(importPreviewLayer);
            }
            importPreviewLayer.BringToFront();
            importPreviewLayer.MarkDirtyRepaint();
            if (!changedSource) { MarkDirtyRepaint(); return; }
            importSpans.Clear();

            if (source == null ||
                !PlanningFragmentUtility.TryBounds(
                    source,
                    out importBounds))
            {
                importBounds = default;
                MarkDirtyRepaint();
                return;
            }

            foreach (var row in source.rooms
                         .SelectMany(r => r.cells)
                         .GroupBy(c => c.y))
            {
                var columns = row.Select(c => c.x)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToArray();
                int start = columns[0];
                int end = start + 1;
                foreach (int x in columns.Skip(1))
                {
                    if (x == end)
                    {
                        end++;
                        continue;
                    }

                    importSpans.Add(new RectInt(
                        start,
                        row.Key,
                        end - start,
                        1));
                    start = x;
                    end = x + 1;
                }

                importSpans.Add(new RectInt(
                    start,
                    row.Key,
                    end - start,
                    1));
            }

            MarkDirtyRepaint();
        }

        public void FocusImportPreview(bool includeDestination = false)
        {
            if (importPreview == null ||
                importBounds.width <= 0 ||
                importBounds.height <= 0)
            {
                return;
            }

            var focus = new RectInt(importAnchor, importBounds.size);
            if (includeDestination && PlanningFragmentUtility.TryBounds(document, out var destination))
            {
                int x = Mathf.Min(focus.x, destination.x), y = Mathf.Min(focus.y, destination.y);
                focus = new RectInt(x, y, Mathf.Max(focus.xMax, destination.xMax) - x, Mathf.Max(focus.yMax, destination.yMax) - y);
            }
            // Fit the whole staged fragment, leaving room for the import controls on the right.
            zoom = Mathf.Clamp(Mathf.Min(Mathf.Max(100f, contentRect.width - 420f) / (focus.width + 6),
                Mathf.Max(100f, contentRect.height - 80f) / (focus.height + 6)) / BaseGridSize, MinimumZoom, MaximumZoom);
            CenterOn(focus.center.x, focus.center.y);
            pan.x -= Mathf.Min(210f, contentRect.width * .2f);
            MarkDirtyRepaint();
        }

        private bool TryBeginImportDrag(Vector2 position)
        {
            if (importPreview == null || mode != PlanningCanvasMode.World) return false;
            // Own all left clicks during staging, so the destination map cannot be accidentally edited.
            draggingImport = CellRect(importAnchor.x, importAnchor.y, importBounds.width, importBounds.height).Contains(position);
            importDragAnchor = importAnchor;
            return true;
        }

        private void ContinueImportDrag(Vector2 position)
        {
            if (!draggingImport) return;
            var next = importDragAnchor + CellAt(position) - CellAt(dragStart);
            if (next != importAnchor) ImportPreviewMoved?.Invoke(next);
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
            if (importPreview == null ||
                importBounds.width <= 0 ||
                importBounds.height <= 0)
            {
                return;
            }
            Vector2Int shift = importAnchor - importBounds.position;
            var color = importValid ? new Color(.25f, 1f, .6f, .8f) : new Color(1f, .3f, .25f, .8f);
            foreach (var span in importSpans)
            {
                var rect = CellRect(span.x + shift.x, span.y + shift.y, span.width, span.height);
                if (!rect.Overlaps(contentRect)) continue;
                FillRect(painter, rect, new Color(color.r, color.g, color.b, .12f), 0);
                StrokeRect(painter, rect, color, 1f);
            }
            // Show contents in addition to room silhouettes. Source data stays read-only.
            foreach (var item in PlanningWaterLayerUtility.WorldBoxes(importPreview))
            {
                var offset = item.origin + shift;
                foreach (var part in PlanningShapeUtility.Rectangles(item.box))
                {
                    var rect = CellRect(part.x + offset.x, part.y + offset.y, part.width, part.height);
                    if (!rect.Overlaps(contentRect)) continue;
                    var tint = ColorForBox(item.box); tint.a = .65f;
                    FillRect(painter, rect, tint, 0);
                }
            }
            StrokeRect(painter, CellRect(importAnchor.x, importAnchor.y, importBounds.width, importBounds.height), color, 3f);
        }
    }
}
