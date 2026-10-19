using TMPro;
using UnityEngine;

namespace Project.FastTravel
{
    [CreateAssetMenu(menuName = "2026TapTap/传送地图设置")]
    public sealed class TravelMapSettings : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Range(0f, 1f)] private float requiredCollectibleRatio = .3f;
        [SerializeField, Min(.1f)] private float zoomOutSeconds = .3f;
        [SerializeField, Min(.1f)] private float travelSeconds = .65f;
        [SerializeField, Min(.1f)] private float zoomInSeconds = .4f;
        public TMP_FontAsset Font => font;
        public float RequiredRatio => Mathf.Clamp01(requiredCollectibleRatio);
        public float ZoomOutSeconds => Mathf.Max(.1f, zoomOutSeconds);
        public float TravelSeconds => Mathf.Max(.1f, travelSeconds);
        public float ZoomInSeconds => Mathf.Max(.1f, zoomInSeconds);
    }
}
