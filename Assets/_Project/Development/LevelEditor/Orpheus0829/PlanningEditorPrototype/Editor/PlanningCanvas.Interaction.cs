using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningCanvas
    {
        private IVisualElementScheduledItem edgePanSchedule;
        private Vector2 edgePanPointer;
        private double edgePanLastTime;
        private bool edgePanTracking;
        private bool completingPointer;
        private bool assemblyAltHeld;
        private bool assemblyShiftHeld;
        private bool HasTemporaryAssemblyTool => mode == PlanningCanvasMode.Assembly &&
            (assemblyAltHeld || assemblyShiftHeld);
        private bool IsTemporaryWhiteBrush => mode == PlanningCanvasMode.Assembly &&
            assemblyShiftHeld && !assemblyAltHeld;

        private void UpdateTemporaryAssemblyTools(bool alt, bool shift)
        {
            if (mode != PlanningCanvasMode.Assembly) return;
            if (assemblyAltHeld == alt && assemblyShiftHeld == shift) return;
            // A marquee belongs to the tool that started it. Never reinterpret
            // an unfinished selection/erase/paint rectangle after a modifier changes.
            if (assemblyAltHeld != alt || !alt && assemblyShiftHeld != shift)
                EndPointerAction();
            assemblyAltHeld = alt;
            assemblyShiftHeld = shift;
            UpdatePlayerOverlay();
            StatusChanged?.Invoke(alt ? "临时擦除：左键拖框，松开 Alt 恢复原工具。" :
                shift ? "临时白方块：左键拖框，松开 Shift 恢复原工具。" : "已恢复原工具。");
            UpdateTriggerPlacementPreview();
            MarkDirtyRepaint();
        }

        internal void ResetTemporaryAssemblyTools()
        {
            if (!assemblyAltHeld && !assemblyShiftHeld) return;
            EndPointerAction();
            assemblyAltHeld = assemblyShiftHeld = false;
            UpdatePlayerOverlay();
            UpdateTriggerPlacementPreview();
            MarkDirtyRepaint();
        }

        private bool HandleTemporaryToolKey(KeyCode key, bool pressed, bool alt, bool shift)
        {
            if (mode != PlanningCanvasMode.Assembly) return false;
            bool altKey = key == KeyCode.LeftAlt || key == KeyCode.RightAlt;
            bool shiftKey = key == KeyCode.LeftShift || key == KeyCode.RightShift;
            if (!altKey && !shiftKey) return false;
            UpdateTemporaryAssemblyTools(altKey ? pressed : alt, shiftKey ? pressed : shift);
            return true;
        }

        private void OnTemporaryToolKeyUp(KeyUpEvent evt)
        {
            if (HandleTemporaryToolKey(evt.keyCode, false, evt.altKey, evt.shiftKey))
                evt.StopPropagation();
        }

        private void InitializeEdgePan()
        {
            edgePanSchedule = schedule.Execute(() => TickEdgePan(EditorApplication.timeSinceStartup)).Every(16);
            edgePanSchedule.Pause();
        }

        private void BeginEdgePan(Vector2 pointer)
        {
            edgePanPointer = pointer;
            edgePanTracking = true;
            edgePanLastTime = EditorApplication.timeSinceStartup;
            edgePanSchedule.Resume();
        }

        private void UpdateEdgePanPointer(Vector2 pointer)
        {
            edgePanPointer = pointer;
            // Pointer capture owns the drag even over a sidebar. Crossing the
            // canvas boundary must not stop a fast drag before the next tick.
            edgePanTracking = true;
            edgePanSchedule.Resume();
        }

        private void PauseEdgePan()
        {
            edgePanTracking = false;
            edgePanSchedule?.Pause();
        }

        private void StopEdgePan()
        {
            PauseEdgePan();
            edgePanLastTime = 0;
        }

        // View-space speed is independent of editor refresh rate and world zoom.
        internal static Vector2 EdgePanVelocity(Rect viewport, Vector2 pointer)
        {
            if (viewport.width < 24f || viewport.height < 24f) return Vector2.zero;
            // Overshoot continues at the nearest edge's bounded maximum speed.
            pointer.x = Mathf.Clamp(pointer.x, viewport.xMin, viewport.xMax);
            pointer.y = Mathf.Clamp(pointer.y, viewport.yMin, viewport.yMax);
            float Axis(float position, float low, float high)
            {
                float band = Mathf.Min(56f, (high - low) * .2f);
                float before = Mathf.Clamp01(1f - (position - low) / band);
                float after = Mathf.Clamp01(1f - (high - position) / band);
                return (before * before - after * after) * 680f;
            }
            return new Vector2(Axis(pointer.x, viewport.xMin, viewport.xMax), Axis(pointer.y, viewport.yMin, viewport.yMax));
        }

        private bool SupportsEdgePan()
        {
            if (draggingImport) return true;
            if (bossMarkerDragBox != null)
                return true;
            if (mode == PlanningCanvasMode.World)
                return mapTool == PlanningMapTool.Paint || mapTool == PlanningMapTool.Connector ||
                    mapTool == PlanningMapTool.Erase || mapTool == PlanningMapTool.Select ||
                    mapTool == PlanningMapTool.Rectangle || mapTool == PlanningMapTool.Region || mapTool == PlanningMapTool.ExportArea;
            var tool = GetActiveDetailTool();
            return tool == PlanningDetailTool.Box || tool == PlanningDetailTool.Erase || tool == PlanningDetailTool.Merge ||
                tool == PlanningDetailTool.Select && movingBox != null;
        }

        internal void TickEdgePan(double now)
        {
            if (!isDragging || isPanning || !edgePanTracking || !SupportsEdgePan()) return;
            if (panel == null || !this.HasPointerCapture(dragPointerId)) { EndPointerAction(); return; }
            float seconds = Mathf.Clamp((float)(now - edgePanLastTime), 0f, .05f);
            edgePanLastTime = now;
            Vector2 delta = EdgePanVelocity(contentRect, edgePanPointer) * seconds;
            if (delta.sqrMagnitude < .0001f) return;
            pan += delta;
            // The initial world cell stays anchored while the viewport moves.
            dragStart += delta;
            movingBoxStart += delta;
            dragCurrent = edgePanPointer;
            ContinuePointerDrag(edgePanPointer);
            UpdateHover(edgePanPointer);
            UpdateDetailPreviewImages();
            UpdatePlayerOverlay();
            MarkDirtyRepaint();
        }

        private void ContinuePointerDrag(Vector2 position)
        {
            if (importPreview != null && mode == PlanningCanvasMode.World) { ContinueImportDrag(position); return; }
            if (bossMarkerDragBox != null)
            {
                ContinueBossMarkerDrag(position);
                return;
            }
            if (mode == PlanningCanvasMode.World) HandleWorldPointerMove(position);
            else if (mode == PlanningCanvasMode.Assembly) HandleAssemblyPointerMove(position);
            else HandleDetailPointerMove(position);
        }

        private void CompletePointerDrag(Vector2 position)
        {
            if (!isDragging || completingPointer) return;
            completingPointer = true;
            PauseEdgePan();
            try
            {
                dragCurrent = position;
                ContinuePointerDrag(position);
                if (importPreview != null && mode == PlanningCanvasMode.World) return;
                if (bossMarkerDragBox != null) CompleteBossMarkerDrag();
                else if (mode == PlanningCanvasMode.World) CompleteWorldDrag();
                else if (mode == PlanningCanvasMode.Assembly) CompleteAssemblyDrag();
                else CompleteDetailDrag();
                // Its completion above already recorded the transaction.
                movingBox = null;
            }
            finally
            {
                EndPointerAction();
                completingPointer = false;
            }
        }

        private bool TryBeginBossMarkerDrag(Vector2 position)
        {
            if (mode == PlanningCanvasMode.World)
            {
                return false;
            }

            PlanningDetailTool tool = GetActiveDetailTool();
            if (tool != PlanningDetailTool.Select &&
                tool != PlanningDetailTool.Parameters)
            {
                return false;
            }

            PlanningRoom bestRoom = null;
            PlanningBox bestBox = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var item in AllBoxes())
            {
                if (mode == PlanningCanvasMode.Detail &&
                    item.room?.id != selectedRoomId)
                {
                    continue;
                }

                if (!TryGetBossMarkerLayout(
                        item.room,
                        item.box,
                        out _,
                        out Vector2 marker,
                        out _))
                {
                    continue;
                }

                RectInt rect = DisplayRect(
                    item.room,
                    item.box);
                Rect hit = CellRect(
                    rect.x,
                    rect.y,
                    1,
                    1);
                hit = new Rect(
                    hit.x - 3f,
                    hit.y - 3f,
                    hit.width + 6f,
                    hit.height + 6f);
                if (!hit.Contains(position))
                {
                    continue;
                }

                float distance =
                    Vector2.Distance(position, marker);
                if (distance > bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestRoom = item.room;
                bestBox = item.box;
            }

            if (bestBox == null)
            {
                return false;
            }

            bossMarkerDragRoom = bestRoom;
            bossMarkerDragBox = bestBox;
            bossMarkerDragMoved = false;
            selectedRoomId = bestRoom?.id;
            selectedBoxId = bestBox.id;
            SelectionChanged?.Invoke();
            StatusChanged?.Invoke(
                "左右拖动 Boss 复活标记，Y 坐标保持与重生点一致。");
            return true;
        }

        private void ContinueBossMarkerDrag(Vector2 position)
        {
            if (bossMarkerDragBox == null)
            {
                return;
            }

            PlanningBox owner =
                bossMarkerDragRoom?.boxes.Find(
                    value => value.id ==
                             bossMarkerDragBox
                                 .bossRespawnOwnerId);
            if (owner == null)
            {
                return;
            }

            RectInt rect = DisplayRect(
                bossMarkerDragRoom,
                owner);
            Rect footprint = CellRect(
                rect.x,
                rect.y,
                rect.width,
                rect.height);
            int localX = Mathf.RoundToInt(
                (position.x - footprint.center.x) /
                GridSize);
            bossMarkerDragBox.x = owner.x + localX;
            bossMarkerDragBox.y = owner.y;
            bossMarkerDragBox.width =
                bossMarkerDragBox.height = 1;
            SetBossMarkerLocalX(
                owner,
                localX);
            bossMarkerDragMoved = true;
            MarkDirtyRepaint();
        }

        private void CompleteBossMarkerDrag()
        {
            if (bossMarkerDragBox != null &&
                bossMarkerDragMoved)
            {
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
            }

            bossMarkerDragRoom = null;
            bossMarkerDragBox = null;
            bossMarkerDragMoved = false;
        }

        private void OnCanvasCaptureOut(PointerCaptureOutEvent evt)
        {
            // Player and child manipulators also bubble capture changes through this canvas.
            if (evt.target != this || evt.pointerId != dragPointerId && evt.pointerId != panPointerId) return;
            EndPointerAction();
            MarkDirtyRepaint();
        }
    }
}
