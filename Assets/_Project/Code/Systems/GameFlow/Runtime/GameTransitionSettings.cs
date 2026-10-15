using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Project.GameFlow
{
    public enum GameTransitionRevealCenter
    {
        ViewportPoint = 0,
        Transform = 1,
        TaggedTransform = 2
    }

    [CreateAssetMenu(
        fileName = "GameTransitionSettings",
        menuName = "2026TapTap/Game Flow/Transition Settings")]
    public sealed class GameTransitionSettings : ScriptableObject
    {
        [Header("字体与文字")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Shader revealShader;
        [SerializeField] private string loadingText = "加载中……";
        [SerializeField, Min(1f)] private float loadingFontSize = 120f;
        [SerializeField] private Color loadingTextColor = Color.white;
        [SerializeField] private Color loadingOutlineColor = Color.black;
        [SerializeField, Min(0f)] private float loadingOutlineWidth = .22f;
        [SerializeField, Min(.01f)] private float loadingTextFadeDuration = .2f;

        [Header("App 进入黑屏")]
        [SerializeField] private GameTransitionRevealCenter appRevealCenter =
            GameTransitionRevealCenter.ViewportPoint;
        [SerializeField] private Vector2 appRevealViewportCenter =
            new Vector2(.5f, .5f);
        [SerializeField, Min(.01f)] private float appRevealSpeed = .72f;
        [SerializeField, Min(0f)] private float appRevealSoftness = 0f;

        [Header("MainMenu -> Gaming")]
        [SerializeField, Min(0f)] private float menuContentSlideDistance = 1600f;
        [SerializeField, Min(.01f)] private float menuContentSlideDuration = .45f;
        [SerializeField] private Ease menuContentSlideEase = Ease.InCubic;
        [SerializeField, Min(.01f)] private float gameplayRevealSpeed = .78f;
        [SerializeField, Min(0f)] private float gameplayRevealSoftness = 0f;
        [SerializeField, Min(0f)] private float gameplayRevealHoldSeconds = .5f;

        [Header("Gaming -> MainMenu")]
        [SerializeField] private Color backgroundRevealColor = Color.white;
        [SerializeField, Min(.01f)] private float menuReturnCoverSpeed = .72f;
        [SerializeField, Min(0f)] private float menuReturnCoverSoftness = 0f;
        [SerializeField, Min(0f)] private float menuReturnCoverHoldSeconds = .5f;
        [SerializeField, Min(.01f)] private float menuBackgroundSlideDuration = .55f;
        [SerializeField] private Ease menuBackgroundSlideEase = Ease.OutCubic;
        [SerializeField, Min(.01f)] private float menuContentReturnDuration = .45f;
        [SerializeField] private Ease menuContentReturnEase = Ease.OutCubic;

        [Header("加载")]
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 2f;
        [SerializeField, Min(0f)] private float preRevealBlackSeconds = .5f;

        [Header("退出游戏")]
        [SerializeField, Min(.01f)] private float exitCoverSpeed = .72f;
        [SerializeField, Min(0f)] private float exitHoldSeconds = 1f;

        public TMP_FontAsset Font => font;
        public Shader RevealShader => revealShader;
        public string LoadingText => loadingText;
        public float LoadingFontSize => loadingFontSize;
        public Color LoadingTextColor => loadingTextColor;
        public Color LoadingOutlineColor => loadingOutlineColor;
        public float LoadingOutlineWidth => loadingOutlineWidth;
        public float LoadingTextFadeDuration => loadingTextFadeDuration;
        public GameTransitionRevealCenter AppRevealCenter => appRevealCenter;
        public Vector2 AppRevealViewportCenter => appRevealViewportCenter;
        public float AppRevealSpeed => appRevealSpeed;
        public float AppRevealSoftness => appRevealSoftness;
        public float MenuContentSlideDistance => menuContentSlideDistance;
        public float MenuContentSlideDuration => menuContentSlideDuration;
        public Ease MenuContentSlideEase => menuContentSlideEase;
        public float GameplayRevealSpeed => gameplayRevealSpeed;
        public float GameplayRevealSoftness => gameplayRevealSoftness;
        public float GameplayRevealHoldSeconds => gameplayRevealHoldSeconds;
        public Color BackgroundRevealColor => backgroundRevealColor;
        public float MenuReturnCoverSpeed => menuReturnCoverSpeed;
        public float MenuReturnCoverSoftness => menuReturnCoverSoftness;
        public float MenuReturnCoverHoldSeconds =>
            menuReturnCoverHoldSeconds;
        public float MenuBackgroundSlideDuration =>
            menuBackgroundSlideDuration;
        public Ease MenuBackgroundSlideEase => menuBackgroundSlideEase;
        public float MenuContentReturnDuration =>
            menuContentReturnDuration;
        public Ease MenuContentReturnEase => menuContentReturnEase;
        public float MinimumDisplaySeconds => minimumDisplaySeconds;
        public float PreRevealBlackSeconds => preRevealBlackSeconds;
        public float ExitCoverSpeed => exitCoverSpeed;
        public float ExitHoldSeconds => exitHoldSeconds;

        private void OnValidate()
        {
            loadingFontSize = Mathf.Max(1f, loadingFontSize);
            loadingOutlineWidth = Mathf.Max(0f, loadingOutlineWidth);
            appRevealSpeed = Mathf.Max(.01f, appRevealSpeed);
            appRevealSoftness = Mathf.Max(0f, appRevealSoftness);
            exitCoverSpeed = Mathf.Max(.01f, exitCoverSpeed);
            menuContentSlideDistance =
                Mathf.Max(0f, menuContentSlideDistance);
            menuContentSlideDuration =
                Mathf.Max(.01f, menuContentSlideDuration);
            gameplayRevealSpeed = Mathf.Max(.01f, gameplayRevealSpeed);
            gameplayRevealSoftness =
                Mathf.Max(0f, gameplayRevealSoftness);
            menuReturnCoverSpeed = Mathf.Max(.01f, menuReturnCoverSpeed);
            menuReturnCoverSoftness =
                Mathf.Max(0f, menuReturnCoverSoftness);
            menuReturnCoverHoldSeconds =
                Mathf.Max(0f, menuReturnCoverHoldSeconds);
        }
    }
}
