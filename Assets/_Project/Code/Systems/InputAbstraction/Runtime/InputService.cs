using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class InputService : MonoBehaviour
    {
        private static InputService instance;

        [SerializeField]
        private InputActionAsset actionAsset;

        [SerializeField]
        private InputPlatformMode platformMode = InputPlatformMode.Automatic;

        [SerializeField]
        [Tooltip("独立场景使用时保持跨场景；由常驻系统场景托管时应关闭。")]
        private bool persistAcrossSceneLoads = true;

        private UnityInputSource unitySource;
        private IInputSource builtInSource;
        private IInputSource externalSource;

        public static InputService Instance => instance;
        public IInputSource ActiveSource => externalSource ?? builtInSource;
        public InputActionAsset ConfiguredActionAsset => actionAsset;
        public InputActionAsset RuntimeActionAsset =>
            unitySource != null
                ? unitySource.Asset
                : actionAsset;
        public InputPlatformMode ConfiguredPlatformMode => platformMode;
        public InputPlatformMode ResolvedPlatformMode => InputPlatformResolver.Resolve(platformMode);

        public void ConfigurePersistence(bool value)
        {
            persistAcrossSceneLoads = value;
        }

        public static InputService EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject gameObject = new GameObject("InputService");
            InputService created = gameObject.AddComponent<InputService>();
            if (instance == null)
            {
                // EditMode does not invoke MonoBehaviour.Awake for this component.
                instance = created;
                created.CreateUnitySource();
            }

            return instance;
        }

        public static void EnsureDefaultMoveBindings(
            InputActionAsset asset)
        {
            InputActionCatalog.EnsureDefaultMoveBindings(asset);
        }

        public void SetExternalSource(IInputSource source)
        {
            externalSource = source;
        }

        public void ClearExternalSource(IInputSource source = null)
        {
            if (source == null || ReferenceEquals(source, externalSource))
            {
                externalSource = null;
            }
        }

        public bool EnsureRuntimeInputEnabled()
        {
            if (externalSource != null)
            {
                return true;
            }

            if (unitySource == null)
            {
                CreateUnitySource();
            }

            return unitySource != null && unitySource.EnsureEnabled();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying && persistAcrossSceneLoads)
            {
                DontDestroyOnLoad(gameObject);
            }

            CreateUnitySource();
        }

        private void OnEnable()
        {
            if (instance == this && unitySource == null)
            {
                CreateUnitySource();
            }
        }

        private void OnDisable()
        {
            if (instance == this)
            {
                unitySource?.Dispose();
                unitySource = null;
                builtInSource = null;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                externalSource = null;
                instance = null;
            }
        }

        private void CreateUnitySource()
        {
            if (unitySource == null)
            {
                InputActionAsset sourceAsset =
                    actionAsset != null
                        ? Object.Instantiate(actionAsset)
                        : InputActionAssetFactory
                            .CreateDefaultGameplayAsset();
                InputBindingOverrideStore.Apply(sourceAsset);
                InputActionInteractionPolicy.Normalize(
                    sourceAsset);
                InputActionCatalog.EnsureDefaultMoveBindings(
                    sourceAsset);
                unitySource = new UnityInputSource(
                    sourceAsset,
                    false);
                builtInSource = ResolvedPlatformMode == InputPlatformMode.Mobile
                    ? (IInputSource)new CompositeInputSource(
                        unitySource,
                        new VirtualInputSource())
                    : unitySource;
            }
        }
    }
}
