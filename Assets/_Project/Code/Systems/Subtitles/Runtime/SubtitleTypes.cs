using System;
using TMPro;
using UnityEngine;

namespace Project.Subtitles
{
    public enum SubtitleLayer
    {
        [InspectorName("中间大字")] Center = 0,
        [InspectorName("下方小字")] Bottom = 1
    }

    public enum SubtitleAnchor
    {
        [InspectorName("屏幕中央")] Center = 0,
        [InspectorName("屏幕下方")] Bottom = 1,
        [InspectorName("屏幕上方")] Top = 2,
        [InspectorName("自定义位置")] Custom = 3,
        [InspectorName("左上角")] TopLeft = 4,
        [InspectorName("右上角")] TopRight = 5,
        [InspectorName("左侧中央")] MiddleLeft = 6,
        [InspectorName("右侧中央")] MiddleRight = 7,
        [InspectorName("左下角")] BottomLeft = 8,
        [InspectorName("右下角")] BottomRight = 9
    }

    public enum SubtitleAnimation
    {
        [InspectorName("无")] None = 0,
        [InspectorName("淡入淡出")] Fade = 1,
        [InspectorName("向上浮现")] FadeSlideUp = 2,
        [InspectorName("向下浮现")] FadeSlideDown = 3,
        [InspectorName("缩放浮现")] Scale = 4,
        [InspectorName("从左滑入")] SlideLeft = 5,
        [InspectorName("从右滑入")] SlideRight = 6,
        [InspectorName("左侧淡入")] FadeSlideLeft = 7,
        [InspectorName("右侧淡入")] FadeSlideRight = 8,
        [InspectorName("弹性弹出")] PopIn = 9,
        [InspectorName("回弹入场")] BounceIn = 10,
        [InspectorName("旋转入场")] RotateIn = 11,
        [InspectorName("打字机")] Typewriter = 12,
        [InspectorName("故障抖动")] Glitch = 13,
        [InspectorName("漂浮")] Float = 14,
        [InspectorName("脉冲")] Pulse = 15
    }

    [Serializable]
    public sealed class SubtitleStyle
    {
        public TMP_FontAsset font;
        [Min(1f)] public float fontSize = 48f;
        public Color color = Color.white;
        public SubtitleAnchor anchor = SubtitleAnchor.Center;
        [Range(0f, 1f)] public float normalizedX = .5f;
        [Range(0f, 1f)] public float normalizedY = .5f;
        public Vector2 offset;
        [Min(0f)] public float holdDuration = 2f;
        public SubtitleAnimation inAnimation =
            SubtitleAnimation.FadeSlideUp;
        [Min(0f)] public float inDuration = .25f;
        public SubtitleAnimation outAnimation =
            SubtitleAnimation.Fade;
        [Min(0f)] public float outDuration = .2f;
        public AnimationCurve inCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve outCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Min(0f)] public float motionDistance = 36f;
        [Range(.1f, 1f)] public float scaleFrom = .82f;
        [Range(-180f, 180f)] public float startRotation = -12f;
        [Range(0f, 1f)] public float overshoot = .18f;
        [Min(0f)] public float floatAmplitude = 12f;
        [Min(0f)] public float floatSpeed = 2f;
        [Range(0f, 1f)] public float glitchStrength = .5f;
        [Min(1f)] public float charactersPerSecond = 24f;
    }

    [Serializable]
    public sealed class SubtitleCue
    {
        public SubtitleLayer layer = SubtitleLayer.Center;
        [TextArea(1, 4)] public string text = "新字幕";
        public bool useManagerDefaults = true;
        public SubtitleStyle style = new SubtitleStyle();
    }

    public interface ISubtitleService
    {
        void Show(SubtitleCue cue);
        void ShowLarge(string text, SubtitleStyle style = null);
        void ShowBottom(string text, SubtitleStyle style = null);
        void Hide(SubtitleLayer layer, bool immediate = false);
        void HideAll(bool immediate = false);
    }

    public static class SubtitleServiceRegistry
    {
        private static ISubtitleService current;

        public static ISubtitleService Current
        {
            get
            {
                if (current == null)
                {
                    current = SubtitleManager.Instance;
                }

                return current;
            }
        }

        public static void Register(ISubtitleService service)
        {
            current = service;
        }

        public static void Unregister(ISubtitleService service)
        {
            if (ReferenceEquals(current, service))
            {
                current = null;
            }
        }
    }
}
