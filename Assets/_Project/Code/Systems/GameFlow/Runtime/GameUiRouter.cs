using System.Collections;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameUiRouter :
        MonoBehaviour,
        IGameSystemService,
        IAchievementSignalProvider
    {
        private static readonly string[] AchievementSignals =
        {
            AchievementSignalIds.GameStarted,
            AchievementSignalIds.GamePaused,
            AchievementSignalIds.GameResumed
        };

        [Header("Screens")]
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject gameplayHud;
        [SerializeField] private GameObject pauseScreen;
        [SerializeField] private GameObject endingScreen;
        [SerializeField] private GameObject loadingScreen;

        [Header("Main menu")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button mainMenuQuitButton;

        [Header("Gameplay")]
        [SerializeField] private Text gameplaySceneLabel;

        [Header("Pause")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button reloadLevelButton;
        [SerializeField] private Button returnToMenuButton;

        [Header("Ending")]
        [SerializeField] private Button endingReturnToMenuButton;
        [SerializeField] private Button endingQuitButton;

        private GameFlowController flow;
        private bool buttonsBound;
        private bool isPaused;
        private Coroutine screenAnimation;

        public int InitializationOrder => 100;
        public bool IsInitialized { get; private set; }
        public bool IsPaused => isPaused;
        public GameObject PauseScreen => pauseScreen;

        public IReadOnlyList<string> GetAchievementSignalIds()
        {
            return AchievementSignals;
        }

        public void Configure(
            GameObject valueMainMenuScreen,
            GameObject valueGameplayHud,
            GameObject valuePauseScreen,
            GameObject valueEndingScreen,
            GameObject valueLoadingScreen,
            Button valueStartGameButton,
            Button valueMainMenuQuitButton,
            Text valueGameplaySceneLabel,
            Button valueResumeButton,
            Button valueReloadLevelButton,
            Button valueReturnToMenuButton,
            Button valueEndingReturnToMenuButton,
            Button valueEndingQuitButton)
        {
            mainMenuScreen = valueMainMenuScreen;
            gameplayHud = valueGameplayHud;
            pauseScreen = valuePauseScreen;
            endingScreen = valueEndingScreen;
            loadingScreen = valueLoadingScreen;
            startGameButton = valueStartGameButton;
            mainMenuQuitButton = valueMainMenuQuitButton;
            gameplaySceneLabel = valueGameplaySceneLabel;
            resumeButton = valueResumeButton;
            reloadLevelButton = valueReloadLevelButton;
            returnToMenuButton = valueReturnToMenuButton;
            endingReturnToMenuButton = valueEndingReturnToMenuButton;
            endingQuitButton = valueEndingQuitButton;
        }

        public IEnumerator Initialize()
        {
            BindButtons();
            BindFlow();
            SetPaused(false, false);
            SetOnly(loadingScreen, false);
            IsInitialized = true;
            yield break;
        }

        public void StartGame()
        {
            PlayClick();
            SetPaused(false, false);
            Flow()?.RequestTransition(GameFlowSceneId.Level01);
            GameplaySignalHub.Emit(
                AchievementSignalIds.GameStarted,
                gameObject);
        }

        public void OpenMainMenu()
        {
            PlayClick();
            SetPaused(false, false);
            Flow()?.ReturnToMainMenu();
        }

        public void ReloadLevel()
        {
            PlayClick();
            SetPaused(false, false);
            Flow()?.ReloadActiveScene();
        }

        public void ResumeGame()
        {
            PlayClick();
            SetPaused(false);
        }

        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void SetPaused(bool value, bool playSound = true)
        {
            if (value && !IsGameplayScene())
            {
                value = false;
            }

            if (isPaused == value)
            {
                SetActive(pauseScreen, value);
                return;
            }

            isPaused = value;
            GameplaySignalHub.Emit(
                value
                    ? AchievementSignalIds.GamePaused
                    : AchievementSignalIds.GameResumed,
                gameObject);
            Time.timeScale = value ? 0f : 1f;
            AudioListener.pause = value;
            SetActive(pauseScreen, value);
            if (value)
            {
                AnimateScreen(pauseScreen);
            }

            Cursor.visible = value;
            Cursor.lockState = CursorLockMode.None;
            if (playSound)
            {
                GameAudioService.Instance?.PlayPause();
            }
        }

        public void QuitGame()
        {
            PlayClick();
            SetPaused(false, false);
            Application.Quit();
        }

        private void Awake()
        {
            BindButtons();
        }

        private void Update()
        {
            if (IsInitialized &&
                IsGameplayScene() &&
                GameInput.WasTriggeredThisFrame(InputActionId.Pause))
            {
                TogglePause();
            }
        }

        private void OnEnable()
        {
            BindFlow();
        }

        private void OnDisable()
        {
            UnbindFlow();
        }

        private void OnDestroy()
        {
            SetPaused(false, false);
            UnbindFlow();
            UnbindButtons();
        }

        private GameFlowController Flow()
        {
            if (flow == null)
            {
                BindFlow();
            }

            return flow;
        }

        private bool IsGameplayScene()
        {
            if (flow == null || !flow.ActiveSceneId.HasValue)
            {
                return false;
            }

            GameFlowSceneId id = flow.ActiveSceneId.Value;
            return id == GameFlowSceneId.Level01 ||
                   id == GameFlowSceneId.Level02 ||
                   id == GameFlowSceneId.Level03;
        }

        private void BindFlow()
        {
            GameFlowController candidate = GameFlowController.Instance;
            if (candidate == null || candidate == flow)
            {
                return;
            }

            UnbindFlow();
            flow = candidate;
            flow.TransitionStarted += OnTransitionStarted;
            flow.ActiveSceneChanged += OnActiveSceneChanged;
            if (flow.ActiveSceneId.HasValue)
            {
                OnActiveSceneChanged(flow.ActiveSceneId.Value);
            }
        }

        private void UnbindFlow()
        {
            if (flow == null)
            {
                return;
            }

            flow.TransitionStarted -= OnTransitionStarted;
            flow.ActiveSceneChanged -= OnActiveSceneChanged;
            flow = null;
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            startGameButton?.onClick.AddListener(StartGame);
            mainMenuQuitButton?.onClick.AddListener(QuitGame);
            resumeButton?.onClick.AddListener(ResumeGame);
            reloadLevelButton?.onClick.AddListener(ReloadLevel);
            returnToMenuButton?.onClick.AddListener(OpenMainMenu);
            endingReturnToMenuButton?.onClick.AddListener(OpenMainMenu);
            endingQuitButton?.onClick.AddListener(QuitGame);
            buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!buttonsBound)
            {
                return;
            }

            startGameButton?.onClick.RemoveListener(StartGame);
            mainMenuQuitButton?.onClick.RemoveListener(QuitGame);
            resumeButton?.onClick.RemoveListener(ResumeGame);
            reloadLevelButton?.onClick.RemoveListener(ReloadLevel);
            returnToMenuButton?.onClick.RemoveListener(OpenMainMenu);
            endingReturnToMenuButton?.onClick.RemoveListener(OpenMainMenu);
            endingQuitButton?.onClick.RemoveListener(QuitGame);
            buttonsBound = false;
        }

        private void OnTransitionStarted(GameFlowSceneId _)
        {
            SetPaused(false, false);
            SetOnly(loadingScreen, false);
        }

        private void OnActiveSceneChanged(GameFlowSceneId sceneId)
        {
            SetPaused(false, false);
            bool menuLike = sceneId == GameFlowSceneId.MainMenu ||
                            sceneId == GameFlowSceneId.Ending;
            Cursor.visible = menuLike;
            Cursor.lockState = CursorLockMode.None;
            switch (sceneId)
            {
                case GameFlowSceneId.MainMenu:
                    SetOnly(mainMenuScreen);
                    break;
                case GameFlowSceneId.Ending:
                    SetOnly(endingScreen);
                    break;
                default:
                    if (gameplaySceneLabel != null)
                    {
                        gameplaySceneLabel.text = SceneTitle(sceneId);
                    }
                    SetOnly(gameplayHud);
                    break;
            }
        }

        private void SetOnly(GameObject target, bool animate = true)
        {
            SetActive(mainMenuScreen, target == mainMenuScreen);
            SetActive(gameplayHud, target == gameplayHud);
            SetActive(endingScreen, target == endingScreen);
            SetActive(loadingScreen, target == loadingScreen);
            SetActive(pauseScreen, false);
            if (animate)
            {
                AnimateScreen(target);
            }
        }

        private void AnimateScreen(GameObject target)
        {
            if (target == null || !target.activeInHierarchy)
            {
                return;
            }

            if (screenAnimation != null)
            {
                StopCoroutine(screenAnimation);
            }
            screenAnimation = StartCoroutine(FadeIn(target));
        }

        private static IEnumerator FadeIn(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.AddComponent<CanvasGroup>();
            }

            group.alpha = 0f;
            const float duration = 0.18f;
            float elapsed = 0f;
            while (elapsed < duration && target.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static string SceneTitle(GameFlowSceneId id)
        {
            switch (id)
            {
                case GameFlowSceneId.Level01:
                    return "关卡 1";
                case GameFlowSceneId.Level02:
                    return "关卡 2";
                case GameFlowSceneId.Level03:
                    return "关卡 3";
                default:
                    return id.ToString();
            }
        }

        private static void PlayClick()
        {
            GameAudioService.Instance?.PlayUiClick();
        }
    }
}
