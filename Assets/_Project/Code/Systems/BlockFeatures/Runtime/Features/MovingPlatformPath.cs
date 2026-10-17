using UnityEngine;

namespace Project.BlockFeatures
{
    public enum MovingPlatformPathMode
    {
        [InspectorName("原路线（兼容已有配置）")] Legacy = 0,
        [InspectorName("从此向右")] Right = 1,
        [InspectorName("从此向左")] Left = 2,
        [InspectorName("从此向上")] Up = 3,
        [InspectorName("从此向下")] Down = 4,
        [InspectorName("以中心左右往返")] HorizontalRange = 5,
        [InspectorName("以中心上下往返")] VerticalRange = 6
    }

    /// <summary>Grid-space route shared by gameplay, Scene gizmos and planning previews.</summary>
    public readonly struct MovingPlatformPath
    {
        public readonly Vector2 Axis;
        public readonly float Minimum, Maximum, Center;
        public readonly bool StartsNegative, IsRange;
        public float Span => Maximum - Minimum;
        public float FirstEnd => StartsNegative ? Minimum : Maximum;
        public Vector2 PreviewFrom => IsRange ? Axis * Minimum : Vector2.zero;
        public Vector2 PreviewTo => Axis * (IsRange ? Maximum : FirstEnd);

        private MovingPlatformPath(Vector2 axis, float minimum, float maximum,
            float center, bool negative, bool range)
        {
            Axis = axis; Minimum = minimum; Maximum = maximum; Center = center;
            StartsNegative = negative && minimum < 0f || maximum <= 0f;
            IsRange = range;
        }

        public static MovingPlatformPath Resolve(MovingPlatformPathMode mode,
            MovingPlatformRoute legacyAxis, int legacyDistance, bool legacyReverse,
            float left, float right, float down, float up, float centerOffset, bool startNegative)
        {
            bool horizontal = mode == MovingPlatformPathMode.HorizontalRange ||
                mode == MovingPlatformPathMode.Left || mode == MovingPlatformPathMode.Right ||
                mode == MovingPlatformPathMode.Legacy && legacyAxis == MovingPlatformRoute.Horizontal;
            Vector2 axis = horizontal ? Vector2.right : Vector2.up;
            if (mode == MovingPlatformPathMode.Legacy)
            {
                float distance = Mathf.Max(1, legacyDistance);
                return new MovingPlatformPath(axis, legacyReverse ? -distance : 0f,
                    legacyReverse ? 0f : distance, 0f, legacyReverse, false);
            }
            float negative = NonNegative(horizontal ? left : down);
            float positive = NonNegative(horizontal ? right : up);
            bool range = mode == MovingPlatformPathMode.HorizontalRange || mode == MovingPlatformPathMode.VerticalRange;
            if (!range)
            {
                bool reverse = mode == MovingPlatformPathMode.Left || mode == MovingPlatformPathMode.Down;
                return new MovingPlatformPath(axis, reverse ? -negative : 0f,
                    reverse ? 0f : positive, 0f, reverse, false);
            }
            // Keep the placed platform on its route, including asymmetric ranges.
            float center = Mathf.Clamp(Finite(centerOffset), -positive, negative);
            return new MovingPlatformPath(axis, center - negative, center + positive,
                center, startNegative, true);
        }

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private static float NonNegative(float value) => Mathf.Max(0f, Finite(value));
    }
}
