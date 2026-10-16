using System;
using System.Collections;
using System.Collections.Generic;
using Project.StartMenu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

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
        private readonly List<EventSystem> suspendedEventSystems = new List<EventSystem>();

        public static GameFlowController Instance => instance;
        public GameSceneCatalog Catalog => catalog;
        public bool IsInitialized { get; private set; }
        public bool IsTransitioning { get; private set; }
        public GameFlowSceneId? ActiveSceneId => activeSceneId;

        public event Action InitializationCompleted;
        public event Action<GameFlowSceneId> TransitionStarted;
        public event Action<GameFlowSceneId> ActiveSceneChanged;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSceneFlowBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureSceneFlow(SceneManager.GetActiveScene());
        }

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
            DontDestroyOnLoad(gameObject);
            StartupCameraGuard.CreateIfNeeded(transform);
            StartMenuController.SetExternalBackgroundScrollPaused(true);
            if (!GameSceneLoader.Instance.AppEntryPlayed)
            {
                GameSceneLoader.Instance.TransitionOverlay
                    .ShowBlackImmediate();
            }
        }

        private IEnumerator Start()
        {
            if (!initializeOnStart || instance != this)
            {
                yield break;
            }

            if (catalog == null)
            {
                catalog = GameSceneCatalog.LoadDefault();
            }

            if (catalog == null)
            {
                Debug.LogError("GameFlowController 缺少 GameSceneCatalog。", this);
                yield break;
            }

            IsTransitioning = true;
            yield return LoadPersistentScenes();
            // Flow-scoped services survive menu/gameplay round trips without legacy system scenes.
            GameAudioService audio = GameAudioService.Instance;
            if (audio == null)
            {
                audio = gameObject.AddComponent<GameAudioService>();
            }
            yield return audio.Initialize();
            IsInitialized = true;
            InitializationCompleted?.Invoke();
            IsTransitioning = false;

            GameSceneLoader loader = GameSceneLoader.Instance;
            if (!loader.AppEntryPlayed)
            {
                loader.TransitionOverlay.ShowBlackImmediate();
            }

            yield return TransitionRoutine(initialScene);
            if (!loader.AppEntryPlayed)
            {
                yield return loader.PlayAppEntryReveal(
                    ProjectDiscovery.FindFirst<StartMenuController>(true));
            }
        }

        private void OnDestroy()
        {
            RestoreSceneEventSystems();
            if (instance == this)
            {
                instance = null;
            }
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            EnsureSceneFlow(scene);
        }

        private static void EnsureSceneFlow(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            StartMenuController menu = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                menu = roots[index]
                    .GetComponentInChildren<StartMenuController>(true);
                if (menu != null)
                {
                    break;
                }
            }

            if (menu == null)
            {
                return;
            }

            if (menu.GetComponent<StartMenuFlowBridge>() == null)
            {
                menu.gameObject.AddComponent<StartMenuFlowBridge>();
            }

            if (instance != null)
            {
                return;
            }

            GameSceneCatalog sceneCatalog =
                GameSceneCatalog.LoadDefault();
            if (sceneCatalog == null)
            {
                return;
            }

            GameObject flowRoot = new GameObject("__GameFlow");
            GameFlowController controller =
                flowRoot.AddComponent<GameFlowController>();
            controller.Configure(
                sceneCatalog,
                GameFlowSceneId.MainMenu,
                true);
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
            StartMenuController menu =
                ProjectDiscovery.FindFirst<StartMenuController>(true);
            bool isInitialTransition = !activeSceneId.HasValue;
            if (!isInitialTransition && target == GameFlowSceneId.Gaming)
            {
                yield return GameSceneLoader.Instance
                    .PrepareGameplayTransition(menu);
            }
            else if (!isInitialTransition &&
                     target == GameFlowSceneId.MainMenu)
            {
                Transform player = FindPlayerInScene(
                    SceneManager.GetActiveScene());
                yield return GameSceneLoader.Instance
                    .PrepareMenuReturnTransition(player);
            }

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
                // Additive activation enables the incoming scene's EventSystem before
                // sceneLoaded. Hand off the outgoing scene before that happens.
                SuspendOutgoingEventSystems(targetPath);
                AsyncOperation load =
                    GameSceneLoader.Instance.LoadSceneAsync(
                        targetPath,
                        LoadSceneMode.Additive,
                        false);
                if (load == null)
                {
                    RestoreSceneEventSystems();
                    Debug.LogError($"无法加载流程场景：{targetPath}", this);
                    IsTransitioning = false;
                    yield break;
                }

                yield return load;
                targetScene = SceneManager.GetSceneByPath(targetPath);
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                RestoreSceneEventSystems();
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
            RestoreSceneEventSystems();

            if (!isInitialTransition &&
                target == GameFlowSceneId.Gaming)
            {
                Transform player = FindPlayerInScene(targetScene);
                yield return GameSceneLoader.Instance
                    .FinishGameplayTransition(player);
            }
            else if (!isInitialTransition &&
                     target == GameFlowSceneId.MainMenu)
            {
                yield return GameSceneLoader.Instance
                    .FinishMenuReturnTransition(
                        ProjectDiscovery.FindFirst<StartMenuController>(
                            true));
            }

            activeSceneId = target;
            IsTransitioning = false;
            ActiveSceneChanged?.Invoke(target);
        }

        private void SuspendOutgoingEventSystems(string targetPath)
        {
            suspendedEventSystems.Clear();
            foreach (EventSystem system in ProjectDiscovery.FindAll<EventSystem>())
            {
                if (system == null || !system.isActiveAndEnabled || system.gameObject.scene.path == targetPath) continue;
                // Only suspend scenes owned by this flow, not unrelated persistent UI.
                foreach (GameFlowSceneEntry entry in catalog.FlowScenes)
                {
                    if (entry == null || entry.ScenePath != system.gameObject.scene.path) continue;
                    suspendedEventSystems.Add(system);
                    system.enabled = false;
                    break;
                }
            }
        }

        private void RestoreSceneEventSystems()
        {
            foreach (EventSystem system in suspendedEventSystems)
                if (system != null) system.enabled = true;
            suspendedEventSystems.Clear();
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

        private static Transform FindPlayerInScene(Scene scene)
        {
            GameObject[] players =
                GameObject.FindGameObjectsWithTag("Player");
            for (int index = 0; index < players.Length; index++)
            {
                if (players[index] != null &&
                    players[index].scene == scene)
                {
                    return players[index].transform;
                }
            }

            return null;
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
