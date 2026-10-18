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
        private WorldMenuPresentation worldMenu;
        private bool gameplayHasStarted;

        public static GameFlowController Instance => instance;
        public GameSceneCatalog Catalog => catalog;
        public bool IsInitialized { get; private set; }
        public bool IsTransitioning { get; private set; }
        public GameFlowSceneId? ActiveSceneId => activeSceneId;
        public WorldMenuPresentation WorldMenu => worldMenu;
        public string LastTransitionFailure { get; private set; }

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
            worldMenu = GetComponent<WorldMenuPresentation>();
            if (worldMenu == null) worldMenu = gameObject.AddComponent<WorldMenuPresentation>();
            if (initialScene == GameFlowSceneId.MainMenu) worldMenu.BeginPreview();
            StartupCameraGuard.CreateIfNeeded(transform);
            StartMenuController.SetExternalBackgroundScrollPaused(true);
            GameSceneLoader.Instance.UseWorldMenuPresentation();
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

            yield return TransitionRoutine(initialScene);
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
            if (instance != null && instance.worldMenu != null && instance.worldMenu.IsPreviewing &&
                instance.catalog != null && instance.catalog.TryGet(GameFlowSceneId.Gaming, out var gameplay) &&
                scene.path == gameplay.ScenePath)
                instance.worldMenu.StageWorld(scene);
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

            if ((target == GameFlowSceneId.MainMenu || target == GameFlowSceneId.Gaming) &&
                catalog.TryGet(GameFlowSceneId.MainMenu, out _) && catalog.TryGet(GameFlowSceneId.Gaming, out _))
            {
                yield return WorldMenuTransitionRoutine(target);
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

        private IEnumerator WorldMenuTransitionRoutine(GameFlowSceneId target)
        {
            // Unity executes nested IEnumerator yields outside the caller's MoveNext. Drive
            // that stack here so loader/component failures have one recoverable boundary.
            var stack = new Stack<IEnumerator>();
            stack.Push(WorldMenuTransitionSteps(target));
            Exception failure = null;
            while (stack.Count > 0)
            {
                object yielded = null;
                bool next = false;
                try
                {
                    next = stack.Peek().MoveNext();
                    if (next) yielded = stack.Peek().Current;
                }
                catch (Exception exception) { failure = exception; }
                if (failure != null) break;
                if (!next)
                {
                    (stack.Pop() as IDisposable)?.Dispose();
                    continue;
                }
                if (yielded is IEnumerator nested) stack.Push(nested);
                else yield return yielded;
            }
            if (failure == null) yield break;
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            yield return RecoverWorldMenuTransition(failure);
        }

        private IEnumerator WorldMenuTransitionSteps(GameFlowSceneId target)
        {
            IsTransitioning = true;
            LastTransitionFailure = null;
            TransitionStarted?.Invoke(target);
            GameSceneLoader loader = GameSceneLoader.Instance;
            loader.UseWorldMenuPresentation();
            catalog.TryGet(GameFlowSceneId.MainMenu, out GameFlowSceneEntry menuEntry);
            catalog.TryGet(GameFlowSceneId.Gaming, out GameFlowSceneEntry gameEntry);
            Scene menuScene = SceneManager.GetSceneByPath(menuEntry.ScenePath);
            if (!menuScene.IsValid() || !menuScene.isLoaded)
            {
                // Returning from a directly launched level may not yet have a menu scene.
                SuspendOutgoingEventSystems(menuEntry.ScenePath);
                yield return loader.LoadSceneAsync(menuEntry.ScenePath, LoadSceneMode.Additive, false);
                menuScene = SceneManager.GetSceneByPath(menuEntry.ScenePath);
            }
            StartMenuController menu = FindInScene<StartMenuController>(menuScene);
            if (menu == null)
                throw new InvalidOperationException("融合菜单缺少 StartMenuController。");
            worldMenu.DisableMenuDevices(menuScene);
            menu.SetBackgroundVisible(false);
            menu.SetBackgroundScrollEnabled(false);
            menu.SetPresented(true, false);
            Scene world = worldMenu.ActiveWorld;
            bool returning = target == GameFlowSceneId.MainMenu && activeSceneId == GameFlowSceneId.Gaming &&
                world.IsValid() && world.isLoaded;
            if (returning)
            {
                yield return worldMenu.ReturnToMenu(menu);
            }
            else
            {
                worldMenu.BeginPreview();
                bool reload = target == GameFlowSceneId.Gaming && gameplayHasStarted;
                Scene previousWorld = world;
                if (reload && previousWorld.IsValid() && previousWorld.isLoaded)
                {
                    worldMenu.FreezeActiveWorld();
                    yield return worldMenu.HoldCurrentFrame();
                    // The still is rendered below the live menu. Unload the old registry owners
                    // before loading matching room IDs again; never run duplicate Gaming worlds.
                    worldMenu.SuspendWorldDevices(previousWorld);
                    yield return SceneManager.UnloadSceneAsync(previousWorld);
                    StartupCameraGuard.CreateIfNeeded(transform);
                    world = default;
                }
                if (!world.IsValid() || !world.isLoaded)
                {
                    if (!reload) world = SceneManager.GetSceneByPath(gameEntry.ScenePath);
                    if (!world.IsValid() || !world.isLoaded)
                    {
                        yield return loader.LoadSceneAsync(gameEntry.ScenePath, LoadSceneMode.Additive, false);
                        for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
                        {
                            Scene candidate = SceneManager.GetSceneAt(index);
                            if (candidate.path == gameEntry.ScenePath && candidate.isLoaded &&
                                (!reload || candidate.handle != previousWorld.handle)) { world = candidate; break; }
                        }
                    }
                }
                if (!world.IsValid() || !world.isLoaded)
                    throw new InvalidOperationException("融合菜单无法加载 Gaming：" + gameEntry.ScenePath);
                worldMenu.StageWorld(world);
                SceneManager.SetActiveScene(world);
                ActivateFlowScene(world);
                // Follow and mode get one normal LateUpdate before the menu requests a close-up.
                yield return null;
                worldMenu.UseWorld(world, menu);
                if (reload) yield return worldMenu.ReleaseFrameHold();
                if (target == GameFlowSceneId.Gaming)
                {
                    yield return worldMenu.EnterGameplay(menu);
                    gameplayHasStarted = true;
                }
                else menu.SetPresented(true);
            }
            suspendedEventSystems.Clear(); // Menu devices stay suspended for this shared-world lifetime.
            activeSceneId = target;
            IsTransitioning = false;
            ActiveSceneChanged?.Invoke(target);
        }

        private IEnumerator RecoverWorldMenuTransition(Exception failure)
        {
            IsTransitioning = true;
            LastTransitionFailure = failure.GetType().Name + ": " + failure.Message;
            Debug.LogError("融合菜单切换失败，已回到可重试的主菜单。" + LastTransitionFailure, this);
            // Keep interaction suppression and the handoff listener until failed world
            // participants are gone. Restoring player colliders first could collect items.
            worldMenu.BeginPreview();
            var worlds = new List<Scene>();
            worldMenu.CollectStagedWorlds(worlds);
            if (catalog != null && catalog.TryGet(GameFlowSceneId.Gaming, out var gameEntry))
            {
                Scene incoming = SceneManager.GetSceneByPath(gameEntry.ScenePath);
                if (incoming.IsValid() && incoming.isLoaded && !worlds.Contains(incoming)) worlds.Add(incoming);
            }
            foreach (Scene scene in worlds)
            {
                if (!scene.IsValid() || !scene.isLoaded) continue;
                // A malformed catalog may point Gaming at the menu itself. Never unload
                // the only recovery UI merely because a bad entry called it a world.
                if (FindInScene<StartMenuController>(scene) != null) continue;
                worldMenu.SuspendWorldDevices(scene);
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
            StartMenuController menu = ProjectDiscovery.FindFirst<StartMenuController>(true);
            if (menu != null) SceneManager.SetActiveScene(menu.gameObject.scene);
            suspendedEventSystems.Clear();
            worldMenu.RecoverMenu(menu);
            gameplayHasStarted = false;
            activeSceneId = GameFlowSceneId.MainMenu;
            IsTransitioning = false;
            ActiveSceneChanged?.Invoke(GameFlowSceneId.MainMenu);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
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
