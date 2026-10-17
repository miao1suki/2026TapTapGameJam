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
        private bool edgePanInside;
        private bool completingPointer;

        private void InitializeEdgePan()
        {
            edgePanSchedule = schedule.Execute(() => TickEdgePan(EditorApplication.timeSinceStartup)).Every(16);
            edgePanSchedule.Pause();
        }

        private void BeginEdgePan(Vector2 pointer)
        {
            edgePanPointer = pointer;
            edgePanInside = true;
            edgePanLastTime = EditorApplication.timeSinceStartup;
            edgePanSchedule.Resume();
        }

        private void UpdateEdgePanPointer(Vector2 pointer)
        {
            edgePanPointer = pointer;
            edgePanInside = contentRect.Contains(pointer);
            if (edgePanInside) edgePanSchedule.Resume();
            else edgePanSchedule.Pause();
        }

        private void PauseEdgePan()
        {
            edgePanInside = false;
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
            if (viewport.width < 24f || viewport.height < 24f || !viewport.Contains(pointer)) return Vector2.zero;
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
            if (!isDragging || isPanning || !edgePanInside || !SupportsEdgePan()) return;
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
                if (mode == PlanningCanvasMode.World) CompleteWorldDrag();
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

        private void OnCanvasCaptureOut(PointerCaptureOutEvent evt)
        {
            // Player and child manipulators also bubble capture changes through this canvas.
            if (evt.target != this || evt.pointerId != dragPointerId && evt.pointerId != panPointerId) return;
            EndPointerAction();
            MarkDirtyRepaint();
        }
    }
}
