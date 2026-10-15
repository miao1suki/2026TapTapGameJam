using System;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.WaterBubbles
{
    /// <summary>
    /// Lightweight visual-only bubble stream for directional water. It does not apply forces
    /// or participate in water collision/gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DirectionalWaterBubbleStream : MonoBehaviour
    {
        public enum FlowDirection
        {
            Up = 0,
            Left = 1,
            Right = 2
        }

        private const float MinLengthInCells = 1f;
        private const float MaxLengthInCells = 10f;
        private const float MinDensityPerCell = 0.2f;
        private const float MaxDensityPerCell = 1.5f;
        private const float MaxWidthInCells = 4f;
        private const float MinSpeedInCellsPerSecond = 0.1f;
        private const float MaxSpeedInCellsPerSecond = 5f;

        [SerializeField, InspectorName("流动方向"), Tooltip("泡泡沿该方向移动；此组件只负责视觉表现。")]
        private FlowDirection flowDirection = FlowDirection.Up;

        [SerializeField, Range(MinLengthInCells, MaxLengthInCells), InspectorName("特效长度（格）"), Tooltip("泡泡流的长度，1 到 10 格线性调整。")]
        private float lengthInCells = 6f;

        [SerializeField, Min(0.01f), InspectorName("每格世界单位"), Tooltip("当前关卡中一个格对应的世界单位长度。")]
        private float worldUnitsPerCell = 1f;

        [SerializeField, Range(MinDensityPerCell, MaxDensityPerCell), InspectorName("泡泡密度（个/格）"), Tooltip("每格目标泡泡数量；最终数量受预制体内泡泡池容量限制。")]
        private float bubbleDensityPerCell = 1.25f;

        [SerializeField, FormerlySerializedAs("crossAxisSpreadInCells"), Range(0f, MaxWidthInCells), InspectorName("泡泡流宽度（格）"), Tooltip("泡泡在主流线两侧随机分布的总宽度；0 时收拢在主流线上。")]
        private float streamWidthInCells = 0.32f;

        [SerializeField, Range(MinSpeedInCellsPerSecond, MaxSpeedInCellsPerSecond), InspectorName("流动速度（格/秒）"), Tooltip("泡泡沿流向循环移动的基准速度。")]
        private float speedInCellsPerSecond = 1.1f;

        [SerializeField, Min(0.01f), InspectorName("起点尺寸倍率")]
        private float minimumScale = 0.72f;

        [SerializeField, Min(0.01f), InspectorName("尾端尺寸倍率")]
        private float maximumScale = 1f;

        [SerializeField, Range(0f, 0.4f), InspectorName("尺寸随机幅度")]
        private float scaleVariation = 0.12f;

        [SerializeField, Range(0f, 1f), InspectorName("基础透明度")]
        private float baseOpacity = 0.9f;

        [SerializeField, InspectorName("排序顺序"), Tooltip("相对默认排序层的绘制顺序。")]
        private int sortingOrder;

        [SerializeField, InspectorName("随机种子"), Tooltip("相同种子会生成相同的初始泡泡分布。")]
        private int randomSeed = 26015;

        [SerializeField, InspectorName("泡泡图片（由小到大）")]
        private Sprite[] bubbleSprites = Array.Empty<Sprite>();

        [SerializeField, HideInInspector]
        private SpriteRenderer[] bubbleRenderers = Array.Empty<SpriteRenderer>();

        private ParticleVariation[] variations;

        public FlowDirection Direction
        {
            get => flowDirection;
            set
            {
                flowDirection = value;
                RefreshPreviewIfEditing();
            }
        }

        /// <summary>Current flow length in grid cells, clamped to the supported 1–10 range.</summary>
        public float LengthInCells
        {
            get => lengthInCells;
            set
            {
                lengthInCells = Mathf.Clamp(value, MinLengthInCells, MaxLengthInCells);
                RefreshPreviewIfEditing();
            }
        }

        public float WorldUnitsPerCell
        {
            get => worldUnitsPerCell;
            set => worldUnitsPerCell = Mathf.Max(0.01f, value);
        }

        /// <summary>Target bubbles per grid cell, limited by the prefab's renderer pool.</summary>
        public float DensityPerCell
        {
            get => bubbleDensityPerCell;
            set
            {
                bubbleDensityPerCell = Mathf.Clamp(value, MinDensityPerCell, MaxDensityPerCell);
                RefreshPreviewIfEditing();
            }
        }

        /// <summary>Total width of the randomized stream in grid cells.</summary>
        public float StreamWidthInCells
        {
            get => streamWidthInCells;
            set
            {
                streamWidthInCells = Mathf.Clamp(value, 0f, MaxWidthInCells);
                RefreshPreviewIfEditing();
            }
        }

        /// <summary>Base movement speed in grid cells per second.</summary>
        public float SpeedInCellsPerSecond
        {
            get => speedInCellsPerSecond;
            set => speedInCellsPerSecond = Mathf.Clamp(value, MinSpeedInCellsPerSecond, MaxSpeedInCellsPerSecond);
        }

        public void RefreshBubblePool()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            var childRenderers = new System.Collections.Generic.List<SpriteRenderer>(renderers.Length);
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.gameObject != gameObject)
                    childRenderers.Add(renderer);
            }

            bubbleRenderers = childRenderers.ToArray();
            variations = CreateVariations(bubbleRenderers.Length);
            if (!Application.isPlaying)
                RefreshEditorPreview();
        }

        private void Awake()
        {
            lengthInCells = Mathf.Clamp(lengthInCells, MinLengthInCells, MaxLengthInCells);
            worldUnitsPerCell = Mathf.Max(0.01f, worldUnitsPerCell);
            bubbleDensityPerCell = Mathf.Clamp(bubbleDensityPerCell, MinDensityPerCell, MaxDensityPerCell);
            streamWidthInCells = Mathf.Clamp(streamWidthInCells, 0f, MaxWidthInCells);
            speedInCellsPerSecond = Mathf.Clamp(speedInCellsPerSecond, MinSpeedInCellsPerSecond, MaxSpeedInCellsPerSecond);
            RefreshBubblePool();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && (variations == null || variations.Length != bubbleRenderers.Length))
                variations = CreateVariations(bubbleRenderers.Length);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            lengthInCells = Mathf.Clamp(lengthInCells, MinLengthInCells, MaxLengthInCells);
            worldUnitsPerCell = Mathf.Max(0.01f, worldUnitsPerCell);
            bubbleDensityPerCell = Mathf.Clamp(bubbleDensityPerCell, MinDensityPerCell, MaxDensityPerCell);
            streamWidthInCells = Mathf.Clamp(streamWidthInCells, 0f, MaxWidthInCells);
            speedInCellsPerSecond = Mathf.Clamp(speedInCellsPerSecond, MinSpeedInCellsPerSecond, MaxSpeedInCellsPerSecond);
            minimumScale = Mathf.Max(0.01f, minimumScale);
            maximumScale = Mathf.Max(minimumScale, maximumScale);

            if (bubbleRenderers == null || bubbleRenderers.Length == 0)
            {
                var renderers = GetComponentsInChildren<SpriteRenderer>(true);
                var childRenderers = new System.Collections.Generic.List<SpriteRenderer>(renderers.Length);
                foreach (var renderer in renderers)
                {
                    if (renderer != null && renderer.gameObject != gameObject)
                        childRenderers.Add(renderer);
                }

                bubbleRenderers = childRenderers.ToArray();
            }

            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= RefreshEditorPreviewDelayed;
                EditorApplication.delayCall += RefreshEditorPreviewDelayed;
            }
        }

        private void RefreshEditorPreviewDelayed()
        {
            if (this == null || Application.isPlaying)
                return;

            RefreshEditorPreview();
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying || bubbleRenderers == null || bubbleRenderers.Length == 0 || bubbleSprites == null || bubbleSprites.Length == 0)
                return;

            if (variations == null || variations.Length != bubbleRenderers.Length)
                variations = CreateVariations(bubbleRenderers.Length);

            var activeCount = GetActiveParticleCount();
            var direction = DirectionVector;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var distance = lengthInCells * worldUnitsPerCell;

            for (var i = 0; i < bubbleRenderers.Length; i++)
            {
                var renderer = bubbleRenderers[i];
                if (renderer == null)
                    continue;

                var isActive = i < activeCount;
                renderer.enabled = isActive;
                if (!isActive)
                    continue;

                var variation = variations[i];
                var travelRate = speedInCellsPerSecond * variation.speedMultiplier / Mathf.Max(lengthInCells, MinLengthInCells);
                var progress = Mathf.Repeat(variation.phase + Time.time * travelRate, 1f);
                ApplyBubble(renderer, variation, progress, direction, perpendicular, distance);
            }
        }

        private void RefreshPreviewIfEditing()
        {
            if (!Application.isPlaying)
                RefreshEditorPreview();
        }

        private void RefreshEditorPreview()
        {
            if (bubbleRenderers == null || bubbleRenderers.Length == 0 || bubbleSprites == null || bubbleSprites.Length == 0)
                return;

            variations = CreateVariations(bubbleRenderers.Length);
            var activeCount = GetActiveParticleCount();
            var direction = DirectionVector;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var distance = lengthInCells * worldUnitsPerCell;

            for (var i = 0; i < bubbleRenderers.Length; i++)
            {
                var renderer = bubbleRenderers[i];
                if (renderer == null)
                    continue;

                var isActive = i < activeCount;
                renderer.enabled = isActive;
                if (!isActive)
                    continue;

                // Keep the editor preview ordered from small at the source to large at the tail.
                var variation = variations[i];
                var progress = activeCount <= 1
                    ? 0.5f
                    : Mathf.Clamp01((i + 0.5f + (variation.phase - 0.5f) * 0.28f) / activeCount);
                ApplyBubble(renderer, variation, progress, direction, perpendicular, distance);
            }
        }

        private void ApplyBubble(SpriteRenderer renderer, ParticleVariation variation, float progress, Vector2 direction, Vector2 perpendicular, float distance)
        {
            var crossOffset = variation.lateralOffset * streamWidthInCells * 0.5f * worldUnitsPerCell;
            var localPosition = direction * (progress * distance) + perpendicular * crossOffset;
            renderer.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

            var spriteIndex = Mathf.Clamp(
                Mathf.RoundToInt(progress * (bubbleSprites.Length - 1) + variation.spriteBias),
                0,
                bubbleSprites.Length - 1);
            renderer.sprite = bubbleSprites[spriteIndex];
            renderer.sortingOrder = sortingOrder;

            var scaleAlongFlow = Mathf.Lerp(minimumScale, maximumScale, progress);
            var scale = scaleAlongFlow * variation.scaleMultiplier;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);

            var color = renderer.color;
            color.a = baseOpacity * variation.opacityMultiplier;
            renderer.color = color;
        }

        private ParticleVariation[] CreateVariations(int count)
        {
            var result = new ParticleVariation[Mathf.Max(0, count)];
            var random = new System.Random(randomSeed);
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new ParticleVariation
                {
                    phase = (float)random.NextDouble(),
                    lateralOffset = (float)random.NextDouble() * 2f - 1f,
                    speedMultiplier = Mathf.Lerp(0.78f, 1.22f, (float)random.NextDouble()),
                    scaleMultiplier = Mathf.Lerp(1f - scaleVariation, 1f + scaleVariation, (float)random.NextDouble()),
                    opacityMultiplier = Mathf.Lerp(0.82f, 1f, (float)random.NextDouble()),
                    spriteBias = Mathf.Lerp(-0.34f, 0.34f, (float)random.NextDouble())
                };
            }

            return result;
        }

        private int GetActiveParticleCount()
        {
            if (bubbleRenderers == null || bubbleRenderers.Length == 0)
                return 0;

            var requestedCount = Mathf.RoundToInt(lengthInCells * bubbleDensityPerCell + 1f);
            return Mathf.Clamp(requestedCount, 2, bubbleRenderers.Length);
        }

        private Vector2 DirectionVector
        {
            get
            {
                switch (flowDirection)
                {
                    case FlowDirection.Left:
                        return Vector2.left;
                    case FlowDirection.Right:
                        return Vector2.right;
                    default:
                        return Vector2.up;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            var direction = DirectionVector;
            var start = transform.position;
            var end = start + new Vector3(direction.x, direction.y, 0f) * (lengthInCells * worldUnitsPerCell);
            var perpendicular = new Vector3(-direction.y, direction.x, 0f);
            var arrowSize = Mathf.Clamp(worldUnitsPerCell * 0.16f, 0.04f, 0.2f);

            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.8f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawLine(end, end - new Vector3(direction.x, direction.y, 0f) * arrowSize + perpendicular * arrowSize * 0.55f);
            Gizmos.DrawLine(end, end - new Vector3(direction.x, direction.y, 0f) * arrowSize - perpendicular * arrowSize * 0.55f);
        }

        private struct ParticleVariation
        {
            public float phase;
            public float lateralOffset;
            public float speedMultiplier;
            public float scaleMultiplier;
            public float opacityMultiplier;
            public float spriteBias;
        }
    }
}
