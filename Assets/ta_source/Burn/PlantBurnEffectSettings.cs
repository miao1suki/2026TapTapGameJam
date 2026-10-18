using UnityEngine;

namespace Project.BurnEffects
{
    [CreateAssetMenu(
        menuName = "2026TapTap/植物燃烧效果配置",
        fileName = "PlantBurnEffectSettings")]
    public sealed class PlantBurnEffectSettings : ScriptableObject
    {
        [SerializeField, Min(.1f), InspectorName("燃烧时长（秒）")]
        private float durationSeconds = 2.25f;

        [SerializeField, Range(.25f, 4f), InspectorName("燃烧速度（倍）")]
        private float speedMultiplier = 1f;

        [SerializeField, Range(.005f, .18f), InspectorName("火焰边宽度")]
        private float flameEdgeWidth = .065f;

        [SerializeField, Range(0f, 2f), InspectorName("橙色焰边强度")]
        private float flameEdgeIntensity = 1.25f;

        [SerializeField, Range(0f, 1f), InspectorName("像素化程度")]
        private float pixelation = .24f;

        [SerializeField, Range(0f, 1f), InspectorName("火焰边缘扰动")]
        private float edgeNoise = .5f;

        [SerializeField, Min(.05f), InspectorName("灰烬消散时长（秒）")]
        private float ashFadeSeconds = .7f;

        [SerializeField, ColorUsage(false, true), InspectorName("火焰内芯")]
        private Color flameCoreColor = new Color(1f, .94f, .46f, 1f);

        [SerializeField, ColorUsage(false, true), InspectorName("火焰外焰")]
        private Color flameOuterColor = new Color(1f, .19f, .025f, 1f);

        [SerializeField, ColorUsage(false, true), InspectorName("灰烬颜色")]
        private Color ashColor = new Color(.19f, .17f, .16f, 1f);

        public float DurationSeconds => Mathf.Max(.1f, durationSeconds);
        public float SpeedMultiplier => Mathf.Max(.25f, speedMultiplier);
        public float FlameEdgeWidth => Mathf.Clamp(flameEdgeWidth, .005f, .18f);
        public float FlameEdgeIntensity => Mathf.Clamp(flameEdgeIntensity, 0f, 2f);
        public float Pixelation => Mathf.Clamp01(pixelation);
        public float EdgeNoise => Mathf.Clamp01(edgeNoise);
        public float AshFadeSeconds => Mathf.Max(.05f, ashFadeSeconds);
        public Color FlameCoreColor => flameCoreColor;
        public Color FlameOuterColor => flameOuterColor;
        public Color AshColor => ashColor;

        public static PlantBurnEffectSettings LoadDefault() =>
            Resources.Load<PlantBurnEffectSettings>("Burn/PlantBurnEffectSettings");
    }
}
