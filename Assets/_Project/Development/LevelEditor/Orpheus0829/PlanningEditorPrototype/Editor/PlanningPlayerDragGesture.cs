using UnityEngine;

namespace Project.LevelEditor.Editor
{
    // The gesture owns only screen displacement; room origins never enter its math.
    internal sealed class PlanningPlayerDragGesture
    {
        private const float DragThresholdSquared = 9f;
        private Vector2 pressPosition;
        private Vector2 startPlan;
        private float pixelsPerCell;
        private bool passedThreshold;

        internal bool IsActive { get; private set; }
        internal int PointerId { get; private set; } = -1;

        internal void Begin(int pointerId, Vector2 position, Vector2 playerPlan, float gridSize)
        {
            IsActive = true;
            PointerId = pointerId;
            pressPosition = position;
            startPlan = playerPlan;
            pixelsPerCell = Mathf.Max(.001f, gridSize);
            passedThreshold = false;
        }

        internal bool TryMove(int pointerId, bool hasCapture, bool leftPressed,
            Vector2 position, out Vector2 plan)
        {
            plan = startPlan;
            if (!IsActive || PointerId != pointerId || !hasCapture || !leftPressed)
                return false;

            Vector2 delta = position - pressPosition;
            passedThreshold |= delta.sqrMagnitude >= DragThresholdSquared;
            if (!passedThreshold) return false;
            plan = startPlan + delta / pixelsPerCell;
            return true;
        }

        internal void End()
        {
            IsActive = false;
            PointerId = -1;
            passedThreshold = false;
        }
    }
}
