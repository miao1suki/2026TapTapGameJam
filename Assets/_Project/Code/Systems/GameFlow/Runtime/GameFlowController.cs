using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GameFlow
{
    [DefaultExecutionOrder(-20000)]
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private GameSceneCatalog catalog;
        [SerializeField] private GameFlowSceneId initialScene = GameFlowSceneId.MainMenu;
        [SerializeField] private bool initializeOnStart = true;

        private static GameFlowController instance;
        private GameFlowSceneId? activeSceneId;

        public static GameFlowController Instance => instance;
        public GameSceneCatalog Catalog => catalog;
        public bool IsInitialized { get; private set; }
        public bool IsTransitioning { get; private set; }
        public GameFlowSceneId? ActiveSceneId => activeSceneId;

        public event Action InitializationCompleted;
        public event Action<GameFlowSceneId> TransitionStarted;
        public event Action<GameFlowSceneId> ActiveSceneChanged;

        public void Configure(
            GameSceneCatalog valueCatalog,
            GameFlowSceneId valueInitialScene,
            bool valueInitializeOnStart = true)
        {
            catalog = valueCatalog;
            initialScene = valueInitialScene;
            initializeOnStart = valueInitializeOnStart;
        }

        public bool RequestTransition(GameFlowSceneId target)
        {
            if (!IsInitialized || IsTransitioning || catalog == null ||
                !catalog.TryGet(target, out _))
            {
                return false;
            }

            StartCoroutine(TransitionRoutine(target));
            return true;
        }

        public bool ReloadActiveScene()
        {
            return activeSceneId.HasValue && RequestTransition(activeSceneId.Value);
        }

        public bool ReturnToMainMenu()
        {
            return RequestTransition(GameFlowSceneId.MainMenu);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            StartupCameraGuard.CreateIfNeeded(transform);
        }

        private IEnumerator Start()
        {
            if (!initializeOnStart || instance != this)
            {
                yield break;
            }

            if (catalog == null)
            {
                Debug.LogError("GameFlowController 缺少 GameSceneCatalog。", this);
                yield break;
            }

            IsTransitioning = true;
            yield return LoadPersistentScenes();
            IsInitialized = true;
            InitializationCompleted?.Invoke();
            IsTransitioning = false;

            GameFlowSceneId firstScene = GameFlowLaunchOverride.TryConsume(
                out GameFlowSceneId requested)
                ? requested
                : initialScene;
            yield return TransitionRoutine(firstScene);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private IEnumerator LoadPersistentScenes()
        {
            for (int index = 0; index < catalog.PersistentScenePaths.Count; index++)
            {
                string path = catalog.PersistentScenePaths[index];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    AsyncOperation load =
                        GameSceneLoader.Instance.LoadSceneAsync(
                            path,
                            LoadSceneMode.Additive,
                            false);
                    if (load == null)
                    {
                        Debug.LogError($"无法加载持久系统场景：{path}", this);
                        continue;
                    }

                    yield return load;
                    scene = SceneManager.GetSceneByPath(path);
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    Debug.LogError($"持久系统场景加载后无效：{path}", this);
                    continue;
                }

                GameSystemSceneRoot systemRoot = FindSystemRoot(scene);
                if (systemRoot == null)
                {
                    Debug.LogError($"持久场景缺少 GameSystemSceneRoot：{path}", this);
                    continue;
                }

                yield return systemRoot.InitializeServices();
            }
        }

        private IEnumerator TransitionRoutine(GameFlowSceneId target)
        {
            if (!catalog.TryGet(target, out GameFlowSceneEntry entry))
            {
                Debug.LogError($"Scene Catalog 中没有流程场景：{target}", this);
                yield break;
            }

            IsTransitioning = true;
            TransitionStarted?.Invoke(target);
            string targetPath = entry.ScenePath;
            Scene targetScene = SceneManager.GetSceneByPath(targetPath);
            bool reloadingCurrent = activeSceneId.HasValue &&
                                    activeSceneId.Value == target &&
                                    targetScene.IsValid() &&
                                    targetScene.isLoaded;
            if (reloadingCurrent)
            {
                AsyncOperation unloadCurrent = SceneManager.UnloadSceneAsync(targetScene);
                if (unloadCurrent != null)
                {
                    yield return unloadCurrent;
                }

                targetScene = default;
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                AsyncOperation load =
                    GameSceneLoader.Instance.LoadSceneAsync(
                        targetPath,
                        LoadSceneMode.Additive);
                if (load == null)
                {
                    Debug.LogError($"无法加载流程场景：{targetPath}", this);
                    IsTransitioning = false;
                    yield break;
                }

                yield return load;
                targetScene = SceneManager.GetSceneByPath(targetPath);
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                Debug.LogError($"流程场景加载后无效：{targetPath}", this);
                IsTransitioning = false;
                yield break;
            }

            SceneManager.SetActiveScene(targetScene);
            for (int index = 0; index < catalog.FlowScenes.Count; index++)
            {
                GameFlowSceneEntry other = catalog.FlowScenes[index];
                if (other == null || other.Id == target)
                {
                    continue;
                }

                Scene otherScene = SceneManager.GetSceneByPath(other.ScenePath);
                if (!otherScene.IsValid() || !otherScene.isLoaded)
                {
                    continue;
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(otherScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            ActivateFlowScene(targetScene);

            activeSceneId = target;
            IsTransitioning = false;
            ActiveSceneChanged?.Invoke(target);
        }

        private static void ActivateFlowScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                LevelSceneContext context =
                    roots[index].GetComponentInChildren<LevelSceneContext>(true);
                if (context != null)
                {
                    context.Activate();
                    return;
                }
            }
        }

        private static GameSystemSceneRoot FindSystemRoot(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameSystemSceneRoot root =
                    roots[index].GetComponentInChildren<GameSystemSceneRoot>(true);
                if (root != null)
                {
                    return root;
                }
            }

            return null;
        }
    }
}
