using System;
using System.Collections;
using Project.StartMenu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GameFlow
{
    /// <summary>Gaming owns both presentations. Menu/game changes never load or unload a world.</summary>
    [DefaultExecutionOrder(-20000)]
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        public const string GameplayScenePath = "Assets/_Project/Scenes/Playing/Gaming.unity";
        public const string CompatibilityMenuScenePath = "Assets/_Project/Scenes/Start/MainMenu.unity";
        public const string MenuResourcePath = "UI/WorldMainMenu";

        [SerializeField] private GameSceneCatalog catalog;
        [SerializeField] private GameFlowSceneId initialScene = GameFlowSceneId.MainMenu;
        [SerializeField] private bool initializeOnStart = true;
        private static GameFlowController instance;
        private GameFlowSceneId? activeSceneId;
        private WorldMenuPresentation worldMenu;
        private StartMenuController menu;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSceneFlowBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureSceneFlow(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureSceneFlow(scene);

        private static void EnsureSceneFlow(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                (scene.path != GameplayScenePath && scene.path != CompatibilityMenuScenePath)) return;
            if (instance == null)
            {
                GameObject root = new GameObject("__GameFlow");
                var flow = root.AddComponent<GameFlowController>();
                flow.Configure(GameSceneCatalog.LoadDefault(), GameFlowSceneId.MainMenu);
            }
            if (scene.path == GameplayScenePath) instance.StageSingleWorld(scene);
        }

        public void Configure(GameSceneCatalog valueCatalog, GameFlowSceneId valueInitialScene,
            bool valueInitializeOnStart = true)
        {
            catalog = valueCatalog;
            initialScene = valueInitialScene;
            initializeOnStart = valueInitializeOnStart;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            worldMenu = GetComponent<WorldMenuPresentation>();
            if (worldMenu == null) worldMenu = gameObject.AddComponent<WorldMenuPresentation>();
            worldMenu.BeginPreview();
            StartMenuController.SetExternalBackgroundScrollPaused(true);
            GameSceneLoader.Instance.UseWorldMenuPresentation();
        }

        private IEnumerator Start()
        {
            if (!initializeOnStart || instance != this) yield break;
            catalog ??= GameSceneCatalog.LoadDefault();
            IsTransitioning = true;
            Scene world = SceneManager.GetSceneByPath(GameplayScenePath);
            if (!world.IsValid() || !world.isLoaded)
            {
                // Editor-only old entry compatibility. Shipping builds start directly in Gaming.
                yield return SceneManager.LoadSceneAsync(GameplayScenePath, LoadSceneMode.Single);
                world = SceneManager.GetSceneByPath(GameplayScenePath);
            }
            StageSingleWorld(world);
            GameAudioService audio = GameAudioService.Instance;
            if (audio == null) audio = gameObject.AddComponent<GameAudioService>();
            yield return audio.Initialize();
            IsInitialized = true;
            IsTransitioning = false;
            InitializationCompleted?.Invoke();
            activeSceneId = GameFlowSceneId.MainMenu;
            menu.SetPresented(true);
            ActiveSceneChanged?.Invoke(GameFlowSceneId.MainMenu);
            if (initialScene == GameFlowSceneId.Gaming) yield return TransitionRoutine(GameFlowSceneId.Gaming);
        }

        private void StageSingleWorld(Scene world)
        {
            if (!world.IsValid() || !world.isLoaded) throw new InvalidOperationException("Gaming 尚未载入。");
            if (menu != null && worldMenu.ActiveWorld.handle == world.handle) return;
            LevelSceneContext context = FindInScene<LevelSceneContext>(world);
            if (context == null) throw new InvalidOperationException("Gaming 缺少 LevelSceneContext。");
            context.Activate(); // Only once, before the first rendered menu frame; never during either transition.
            menu = FindInScene<StartMenuController>(world);
            if (menu == null)
            {
                GameObject prefab = Resources.Load<GameObject>(MenuResourcePath);
                if (prefab == null) throw new InvalidOperationException("缺少共享菜单 Resources/" + MenuResourcePath);
                GameObject view = Instantiate(prefab);
                view.name = "World Main Menu";
                SceneManager.MoveGameObjectToScene(view, world);
                menu = view.GetComponentInChildren<StartMenuController>(true);
            }
            if (menu == null) throw new InvalidOperationException("共享菜单缺少 StartMenuController。");
            if (menu.GetComponent<StartMenuFlowBridge>() == null) menu.gameObject.AddComponent<StartMenuFlowBridge>();
            activeSceneId = GameFlowSceneId.MainMenu;
            worldMenu.BeginPreview();
            worldMenu.UseWorld(world, menu);
        }

        public bool RequestTransition(GameFlowSceneId target)
        {
            if (!IsInitialized || IsTransitioning ||
                (target != GameFlowSceneId.MainMenu && target != GameFlowSceneId.Gaming)) return false;
            if (activeSceneId == target) return false;
            StartCoroutine(TransitionRoutine(target));
            return true;
        }

        // Menu Start resumes the very same world. Explicit room reset remains the gameplay R action.
        public bool ReloadActiveScene() => false;
        public bool ReturnToMainMenu() => RequestTransition(GameFlowSceneId.MainMenu);

#if UNITY_EDITOR
        /// <summary>Tests that explicitly launch formal Gaming can opt in after initialization.</summary>
        public bool EnterGameplayImmediatelyForDevelopment()
        {
            if (!IsInitialized || IsTransitioning || menu == null) return false;
            if (activeSceneId == GameFlowSceneId.Gaming) return true;
            TransitionStarted?.Invoke(GameFlowSceneId.Gaming);
            worldMenu.EnterGameplayImmediatelyForDevelopment(menu);
            activeSceneId = GameFlowSceneId.Gaming;
            ActiveSceneChanged?.Invoke(GameFlowSceneId.Gaming);
            return true;
        }
#endif

        private IEnumerator TransitionRoutine(GameFlowSceneId target)
        {
            IsTransitioning = true;
            LastTransitionFailure = null;
            TransitionStarted?.Invoke(target);
            IEnumerator sequence = target == GameFlowSceneId.Gaming
                ? worldMenu.EnterGameplay(menu) : worldMenu.ReturnToMenu(menu);
            Exception failure = null;
            while (true)
            {
                bool next;
                object current = null;
                try { next = sequence.MoveNext(); if (next) current = sequence.Current; }
                catch (Exception exception) { failure = exception; break; }
                if (!next) break;
                yield return current;
            }
            (sequence as IDisposable)?.Dispose();
            if (failure != null)
            {
                LastTransitionFailure = failure.GetType().Name + ": " + failure.Message;
                Debug.LogError("菜单运镜失败，已恢复同场景菜单。" + LastTransitionFailure, this);
                worldMenu.RecoverMenu(menu);
                target = GameFlowSceneId.MainMenu;
            }
            activeSceneId = target;
            IsTransitioning = false;
            ActiveSceneChanged?.Invoke(target);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
