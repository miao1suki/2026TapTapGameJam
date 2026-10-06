using System;
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

        private static GameSceneLoader instance;
        private IGameLoadingScreen loadingScreen;
        private AsyncOperation activeOperation;
        private float shownAt;

        public static GameSceneLoader Instance => EnsureInstance();
        public static GameSceneLoader Existing => instance;
        public IGameLoadingScreen LoadingScreen => loadingScreen;

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

        private void ResolveLoadingScreen()
        {
            if (loadingScreen == null &&
                loadingScreenBehaviour is IGameLoadingScreen screen)
            {
                loadingScreen = screen;
            }
        }
    }
}
