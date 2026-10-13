using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TaSource.GivingColor
{
    /// <summary>Observes completed player dye actions. Never changes gameplay color state.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class GivingColorEffectController : MonoBehaviour
    {
        private static readonly int RedSpeedId = Shader.PropertyToID("_RedSpeed");
        private static readonly int BlueSpeedId = Shader.PropertyToID("_BlueSpeed");
        private static readonly int GreenSpeedId = Shader.PropertyToID("_GreenSpeed");
        private static readonly int PixelationId = Shader.PropertyToID("_Pixelation");
        private static readonly int RedRimWidthId = Shader.PropertyToID("_RedRimWidth");
        private static readonly int RedNoiseAmountId = Shader.PropertyToID("_RedNoiseAmount");
        private static readonly int BlueNoiseAmountId = Shader.PropertyToID("_BlueNoiseAmount");
        private static readonly int GreenNoiseAmountId = Shader.PropertyToID("_GreenNoiseAmount");

        [Header("只读接入：原有染色成功事件")]
        [SerializeField] private PlayerColorWheel colorWheel;
        [SerializeField] private PlayerInteractionSensor interactionSensor;
        [SerializeField] private Shader overlayShader;
        private Material speedSettings;

        [Header("扩散时长")]
        [SerializeField, Min(.05f)] private float redDuration = 1.35f;
        [SerializeField, Min(.05f)] private float blueDuration = 1.65f;
        [SerializeField, Min(.05f)] private float greenDuration = 1.9f;

        [Header("覆盖层效果")]
        [SerializeField, Range(0f, 1f)] private float dyeStrength = 1f;
        [SerializeField, Range(.005f, .15f)] private float edgeWidth = .035f;
        [SerializeField, Range(1f, 64f)] private float noiseScale = 22f;
        [SerializeField, Range(0f, 2f)] private float fireGlow = 1f;
        [SerializeField, Range(0f, 2f)] private float waterDiffusion = 1f;
        [SerializeField, Range(3, 12)] private int greenBranchCount = 8;
        [SerializeField, Range(.005f, .08f)] private float greenBranchWidth = .025f;
        [SerializeField, Range(0f, .25f)] private float alphaThreshold = .015f;

        private IColorApplicationTarget pendingTarget;
        private string pendingColorId;
        private Vector2 pendingPointer;
        private string lastEffectTargetName = string.Empty;
        private bool subscribed;

        public string LastSelectedColorId => pendingColorId;
        public string LastEffectTargetName => lastEffectTargetName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootstrap()
        {
            SceneManager.sceneLoaded -= EnsureRuntimeBridge;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= EnsureRuntimeBridge;
            SceneManager.sceneLoaded += EnsureRuntimeBridge;
            EnsureRuntimeBridge(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void EnsureRuntimeBridge(Scene scene, LoadSceneMode mode)
        {
            if (FindFirstObjectByType<GivingColorEffectController>() != null ||
                FindFirstObjectByType<PlayerColorWheel>() == null)
                return;

            new GameObject("GivingColor Visual Listener")
            {
                hideFlags = HideFlags.DontSave
            }.AddComponent<GivingColorEffectController>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            speedSettings = Resources.Load<Material>("GivingColorDye");
            if (overlayShader == null)
                overlayShader = speedSettings != null ? speedSettings.shader : null;
            if (overlayShader == null)
                overlayShader = Shader.Find("2026TapTap/Giving Color Dye Overlay");
            if (overlayShader == null || !overlayShader.isSupported)
                Debug.LogError("[GivingColor] 覆盖 Shader 不可用，已停用特效，避免紫色错误材质。", this);
            BindColorEvents();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindColorEvents();
            pendingTarget = null;
            pendingColorId = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnbindColorEvents();
            colorWheel = null;
            interactionSensor = null;
            pendingTarget = null;
            pendingColorId = null;
            BindColorEvents();
        }

        private void Update()
        {
            // Runs before PlayerColorWheel.Update. Its success event has no
            // arguments and it clears the selection before publishing it.
            if (colorWheel == null || !colorWheel.HasSelectedColor ||
                interactionSensor == null || Camera.main == null ||
                !interactionSensor.TryGetScreenComponent(
                    Camera.main, GameInput.PointerPosition,
                    out IColorApplicationTarget target))
            {
                pendingTarget = null;
                return;
            }

            pendingTarget = target;
            pendingColorId = colorWheel.SelectedColorId;
            pendingPointer = GameInput.PointerPosition;
        }

        private void BindColorEvents()
        {
            if (colorWheel == null)
                colorWheel = FindFirstObjectByType<PlayerColorWheel>();
            if (colorWheel == null || subscribed)
                return;
            if (interactionSensor == null)
                interactionSensor = colorWheel.GetComponent<PlayerInteractionSensor>();
            colorWheel.ColorApplicationSucceeded += OnColorApplicationSucceeded;
            subscribed = true;
        }

        private void UnbindColorEvents()
        {
            if (subscribed && colorWheel != null)
                colorWheel.ColorApplicationSucceeded -= OnColorApplicationSucceeded;
            subscribed = false;
        }

        private void OnColorApplicationSucceeded()
        {
            IColorApplicationTarget receiver = pendingTarget;
            string colorId = pendingColorId;
            Vector2 pointer = pendingPointer;
            pendingTarget = null;
            pendingColorId = null;

            if (receiver == null || string.IsNullOrEmpty(colorId) ||
                (pointer - GameInput.PointerPosition).sqrMagnitude > 4f ||
                overlayShader == null || !overlayShader.isSupported)
                return;

            ColorTypeDefinition definition =
                ColorRuntimeService.Existing?.Catalog?.Find(colorId);
            if (definition == null || !(receiver is Component component))
                return;

            // Steam, destruction and plant growth do not dye a new surface.
            if (component is WaterVisualFeature ||
                component is PlantObstacleFeature ||
                component is BouncePlantFeature ||
                component is ClimbableVineFeature)
                return;

            // A blue-water/lava reaction creates a gameplay platform. Its
            // blue material is the reaction result, not a player dye target.
            if (component is LavaHazardFeature)
                return;

            if (component is UniversalColorBlock universal)
            {
                PlayOn(universal.gameObject, colorId, definition.swatch);
                return;
            }

            // An optional target may opt in without modifying the gameplay
            // IColorApplicationTarget implementation.
            if (component is GivingColorTestReceiver ||
                component.GetComponent<GivingColorEffectTarget>() != null)
                PlayOn(component.gameObject, colorId, definition.swatch);
        }

        /// <summary>Visual-only entry for future successful reactions.</summary>
        public void PlayOn(GameObject target, string colorId, Color color)
        {
            int mode = ResolveMode(colorId);
            if (target == null || overlayShader == null ||
                !overlayShader.isSupported || mode == 0)
                return;

            GivingColorEffectTarget effect =
                target.GetComponent<GivingColorEffectTarget>();
            if (effect == null)
                effect = target.AddComponent<GivingColorEffectTarget>();
            lastEffectTargetName = target.name;
            effect.Play(overlayShader, color, mode, ResolveDuration(mode),
                dyeStrength, edgeWidth, noiseScale, fireGlow, waterDiffusion,
                greenBranchCount, greenBranchWidth, alphaThreshold,
                pixelation: ReadSetting(PixelationId, .7f),
                redRimWidth: ReadSetting(RedRimWidthId, .035f),
                redNoiseAmount: ReadSetting(RedNoiseAmountId, .65f),
                blueNoiseAmount: ReadSetting(BlueNoiseAmountId, .55f),
                greenNoiseAmount: ReadSetting(GreenNoiseAmountId, .7f));
        }

        private static int ResolveMode(string colorId)
        {
            if (string.Equals(colorId, "red", System.StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(colorId, "blue", System.StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(colorId, "green", System.StringComparison.OrdinalIgnoreCase)) return 3;
            return 0;
        }

        private float ResolveDuration(int mode)
        {
            float baseDuration = mode == 1 ? redDuration :
                mode == 2 ? blueDuration : greenDuration;
            int speedId = mode == 1 ? RedSpeedId :
                mode == 2 ? BlueSpeedId : GreenSpeedId;
            float speed = ReadSetting(speedId, 1f);
            return baseDuration / Mathf.Clamp(speed, .25f, 4f);
        }

        private float ReadSetting(int propertyId, float fallback)
        {
            return speedSettings != null && speedSettings.shader == overlayShader &&
                speedSettings.HasProperty(propertyId)
                ? speedSettings.GetFloat(propertyId)
                : fallback;
        }
    }
}
