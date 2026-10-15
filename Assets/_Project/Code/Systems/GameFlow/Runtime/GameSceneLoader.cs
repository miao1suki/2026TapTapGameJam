using System;
using System.Collections;
using DG.Tweening;
using Project.StartMenu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GameFlow
{
    public interface IGameLoadingScreen
    {
        void ShowLoading();
        void SetLoadingProgress(float progress);
        void HideLoading();
    }

    [DisallowMultipleComponent]
    public sealed class GameSceneLoader : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour loadingScreenBehaviour;
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = .2f;
        [SerializeField] private GameTransitionSettings transitionSettings;

        private static GameSceneLoader instance;
        private IGameLoadingScreen loadingScreen;
        private GameTransitionOverlay transitionOverlay;
        private AsyncOperation activeOperation;
        private float shownAt;
        private bool appEntryPlayed;
        private bool applicationQuitRequested;
        private float loadingTextVisibleSince;

        public static GameSceneLoader Instance => EnsureInstance();
        public static GameSceneLoader Existing => instance;
        public IGameLoadingScreen LoadingScreen => loadingScreen;
        public GameTransitionSettings TransitionSettings =>
            ResolveTransitionSettings();
        public GameTransitionOverlay TransitionOverlay =>
            EnsureTransitionOverlay();
        public bool AppEntryPlayed => appEntryPlayed;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static GameSceneLoader EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            GameSceneLoader existing =
                ProjectDiscovery.FindFirst<GameSceneLoader>();
            if (existing != null)
            {
                return existing;
            }

            GameObject root = new GameObject("Game Scene Loader");
            instance = root.AddComponent<GameSceneLoader>();
            return instance;
        }

        public void RegisterLoadingScreen(IGameLoadingScreen screen)
        {
            loadingScreen = screen;
            ResolveLoadingScreen();
        }

        public void ClearLoadingScreen(IGameLoadingScreen screen)
        {
            if (ReferenceEquals(loadingScreen, screen))
            {
                loadingScreen = null;
            }
        }

        public void RequestApplicationQuit()
        {
            if (applicationQuitRequested)
            {
                return;
            }

            applicationQuitRequested = true;
            StartCoroutine(ApplicationQuitRoutine());
        }

        public IEnumerator PlayAppEntryReveal(
            StartMenuController menu)
        {
            if (appEntryPlayed)
            {
                yield break;
            }

            GameTransitionOverlay overlay = EnsureTransitionOverlay();
            GameTransitionSettings settings = ResolveTransitionSettings();
            StartMenuController.SetExternalBackgroundScrollPaused(true);
            overlay.ShowBlackImmediate();

            float holdSeconds = settings != null
                ? settings.PreRevealBlackSeconds
                : .08f;
            if (holdSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(holdSeconds);
            }
            if (menu != null)
            {
                menu.SetBackgroundScrollEnabled(true);
            }
            StartMenuController.SetExternalBackgroundScrollPaused(false);

            Vector2 viewportCenter = settings != null
                ? settings.AppRevealViewportCenter
                : new Vector2(.5f, .5f);
            Transform center = null;
            if (settings != null &&
                settings.AppRevealCenter ==
                GameTransitionRevealCenter.Transform &&
                menu != null)
            {
                center = menu.StartButtonTransform;
            }

            yield return overlay.RevealFromAsync(
                center,
                viewportCenter,
                settings != null ? settings.AppRevealSpeed : .72f,
                settings != null ? settings.AppRevealSoftness : .08f);
            overlay.SetBlackVisible(false);
            if (menu != null)
            {
                menu.SetBackgroundScrollEnabled(true);
            }

            appEntryPlayed = true;
        }

        public IEnumerator PrepareGameplayTransition(
            StartMenuController menu)
        {
            GameTransitionOverlay overlay = EnsureTransitionOverlay();
            GameTransitionSettings settings = ResolveTransitionSettings();
            if (menu != null)
            {
                menu.SetBackgroundScrollEnabled(false);
                yield return menu.SlideMenuContentOutAsync(
                    settings != null
                        ? settings.MenuContentSlideDistance
                        : 1600f,
                    settings != null
                        ? settings.MenuContentSlideDuration
                        : .45f,
                    settings != null
                        ? settings.MenuContentSlideEase
                        : Ease.InCubic);
            }
            StartMenuController.SetExternalBackgroundScrollPaused(true);

            overlay.ShowBlackImmediate();
            overlay.SetBackground(menu != null ? menu.Background : null);
            overlay.SetBackgroundVisible(true);
            if (menu != null)
            {
                menu.SetBackgroundVisible(false);
            }
            yield return overlay.FadeLoadingTextAsync(true);
            loadingTextVisibleSince = Time.unscaledTime;
        }

        public IEnumerator FinishGameplayTransition(
            Transform player)
        {
            GameTransitionOverlay overlay = EnsureTransitionOverlay();
            GameTransitionSettings settings = ResolveTransitionSettings();
            float duration = settings != null
                ? settings.MenuContentSlideDuration
                : .45f;
            float minimum = settings != null
                ? settings.MinimumDisplaySeconds
                : 2f;
            float remaining = minimum -
                (Time.unscaledTime - loadingTextVisibleSince);
            if (remaining > 0f)
            {
                yield return new WaitForSecondsRealtime(remaining);
            }
            SetPlayerInputLocked(player, true);
            yield return overlay.FadeLoadingTextAsync(false);
            yield return overlay.SlideBackgroundOutAsync(
                duration,
                settings != null
                    ? settings.MenuContentSlideEase
                    : Ease.InCubic);
            float blackHold = settings != null
                ? Mathf.Max(
                    settings.PreRevealBlackSeconds,
                    settings.GameplayRevealHoldSeconds)
                : .5f;
            if (blackHold > 0f)
            {
                yield return new WaitForSecondsRealtime(blackHold);
            }

            yield return overlay.RevealFromAsync(
                player,
                settings != null
                    ? settings.AppRevealViewportCenter
                    : new Vector2(.5f, .5f),
                settings != null
                    ? settings.GameplayRevealSpeed
                    : .78f,
                settings != null
                    ? settings.GameplayRevealSoftness
                    : .08f);
            SetPlayerInputLocked(player, false);
            overlay.SetBlackVisible(false);
            overlay.SetBackgroundVisible(false);
        }

        public IEnumerator PrepareMenuReturnTransition(
            Transform centerTransform)
        {
            GameTransitionOverlay overlay = EnsureTransitionOverlay();
            GameTransitionSettings settings = ResolveTransitionSettings();
            StartMenuController.SetExternalBackgroundScrollPaused(true);
            overlay.SetBackgroundVisible(false);
            yield return overlay.CoverFromCenterAsync(
                centerTransform,
                settings != null
                    ? settings.AppRevealViewportCenter
                    : new Vector2(.5f, .5f),
                settings != null
                    ? settings.MenuReturnCoverSpeed
                    : .72f,
                settings != null
                    ? settings.MenuReturnCoverSoftness
                    : 0f);
            float coverHoldSeconds = settings != null
                ? settings.MenuReturnCoverHoldSeconds
                : .5f;
            if (coverHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(coverHoldSeconds);
            }

            overlay.SetBackgroundVisible(true);
            overlay.SetInputBlocking(true);
            yield return overlay.SlideBackgroundInAsync(
                settings != null
                    ? settings.MenuBackgroundSlideDuration
                    : .55f,
                settings != null
                    ? settings.MenuBackgroundSlideEase
                    : Ease.OutCubic);
            overlay.SetBlackVisible(false);
            yield return overlay.FadeLoadingTextAsync(true);
            loadingTextVisibleSince = Time.unscaledTime;
        }

        public IEnumerator FinishMenuReturnTransition(
            StartMenuController menu)
        {
            GameTransitionOverlay overlay = EnsureTransitionOverlay();
            GameTransitionSettings settings = ResolveTransitionSettings();
            float minimum = settings != null
                ? settings.MinimumDisplaySeconds
                : 2f;
            float remaining = minimum -
                (Time.unscaledTime - loadingTextVisibleSince);
            if (remaining > 0f)
            {
                yield return new WaitForSecondsRealtime(remaining);
            }
            yield return overlay.FadeLoadingTextAsync(false);
            if (menu != null)
            {
                overlay.ApplyBackgroundStateTo(menu.Background);
            }
            overlay.SetBackgroundVisible(false);
            if (menu != null)
            {
                menu.SetBackgroundVisible(true);
                yield return menu.SlideMenuContentInAsync(
                    settings != null
                        ? settings.MenuContentSlideDistance
                        : 1600f,
                    settings != null
                        ? settings.MenuContentReturnDuration
                        : .45f,
                    settings != null
                        ? settings.MenuContentReturnEase
                        : Ease.OutCubic);
                menu.SetBackgroundScrollEnabled(true);
            }
            StartMenuController.SetExternalBackgroundScrollPaused(false);
            overlay.SetInputBlocking(false);

            overlay.SetBlackVisible(false);
        }

        public AsyncOperation LoadSceneAsync(
            string scenePath,
            LoadSceneMode mode,
            bool showLoading = true)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return null;
            }

            AsyncOperation operation =
                SceneManager.LoadSceneAsync(scenePath, mode);
            if (operation != null && showLoading)
            {
                activeOperation = operation;
                shownAt = Time.unscaledTime;
                ResolveLoadingScreen();
                loadingScreen?.ShowLoading();
                loadingScreen?.SetLoadingProgress(0f);
            }

            return operation;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ResolveLoadingScreen();
            ResolveTransitionSettings();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            if (activeOperation == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(
                activeOperation.progress / .9f);
            loadingScreen?.SetLoadingProgress(progress);
            if (!activeOperation.isDone ||
                Time.unscaledTime - shownAt < minimumDisplaySeconds)
            {
                return;
            }

            loadingScreen?.SetLoadingProgress(1f);
            loadingScreen?.HideLoading();
            activeOperation = null;
        }

        private IEnumerator ApplicationQuitRoutine()
        {
            GameTransitionOverlay overlay = EnsureTransitionOverlay();
            GameTransitionSettings settings = ResolveTransitionSettings();
            StartMenuController.SetExternalBackgroundScrollPaused(true);

            yield return overlay.CoverFromCenterAsync(
                settings != null
                    ? settings.AppRevealViewportCenter
                    : new Vector2(.5f, .5f),
                settings != null ? settings.ExitCoverSpeed : .72f,
                settings != null ? settings.AppRevealSoftness : 0f);

            float holdSeconds = settings != null
                ? settings.ExitHoldSeconds
                : 1f;
            if (holdSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(holdSeconds);
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ResolveLoadingScreen()
        {
            if (loadingScreen == null &&
                loadingScreenBehaviour is IGameLoadingScreen screen)
            {
                loadingScreen = screen;
            }
        }

        private GameTransitionSettings ResolveTransitionSettings()
        {
            if (transitionSettings == null)
            {
                transitionSettings = Resources.Load<GameTransitionSettings>(
                    "GameTransitionSettings");
            }

            return transitionSettings;
        }

        private GameTransitionOverlay EnsureTransitionOverlay()
        {
            if (transitionOverlay == null)
            {
                transitionOverlay =
                    ProjectDiscovery.FindFirst<GameTransitionOverlay>(true);
                if (transitionOverlay == null)
                {
                    transitionOverlay = GameTransitionOverlay.Ensure(
                        transform,
                        ResolveTransitionSettings());
                }
                else
                {
                    transitionOverlay.Initialize(
                        ResolveTransitionSettings());
                }
            }

            return transitionOverlay;
        }

        private static void SetPlayerInputLocked(
            Transform player,
            bool locked)
        {
            if (player == null)
            {
                return;
            }

            player.SendMessage(
                "SetControlLocked",
                locked,
                SendMessageOptions.DontRequireReceiver);
        }
    }
}
