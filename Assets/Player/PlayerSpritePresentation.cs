using Project.BlockFeatures;
using Project.ObjectArtwork;
using UnityEngine;

namespace Project.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerController)), DefaultExecutionOrder(100)]
    public sealed class PlayerSpritePresentation : MonoBehaviour
    {
        [SerializeField, InspectorName("动画配置")] private PlayerSpriteAnimationSet animationSet;
        [SerializeField, InspectorName("精灵显示")] private SpriteRenderer visual;
        [SerializeField, InspectorName("精灵材质")] private Material spriteMaterial;
        [SerializeField, InspectorName("隐藏原占位模型")] private bool hidePlaceholder = true;
        [SerializeField, InspectorName("面向相机")] private bool faceCamera = true;
        [SerializeField, InspectorName("外观垂直偏移（格）")] private float verticalOffsetCells = -0.08f;
        private PlayerController player;
        private CapsuleCollider capsule;
        private Renderer placeholder;
        private bool placeholderWasEnabled;
        private bool hasPresented;
        private Camera viewingCamera;
        private float cellWorldSize = 1f;
        private readonly PlayerSpriteAnimationClock clock = new PlayerSpriteAnimationClock();
        public PlayerSpriteState CurrentVisualState => clock.State;
        public SpriteRenderer Visual => visual;
        public float DeathAnimationDuration
        {
            get
            {
                var clip = animationSet != null ? animationSet.GetClip(PlayerSpriteState.Death) : null;
                return clip?.frames != null ? clip.frames.Length * Mathf.Max(.01f, clip.secondsPerFrame) : 0f;
            }
        }
        public bool IsDeathAnimationComplete => clock.State == PlayerSpriteState.Death &&
            clock.Elapsed >= DeathAnimationDuration;

        public static void Ensure(PlayerController controller)
        {
            if (controller.GetComponent<PlayerSpritePresentation>() == null &&
                Resources.Load<PlayerSpriteAnimationSet>("PlayerVisuals/PlayerSprites") != null)
                controller.gameObject.AddComponent<PlayerSpritePresentation>();
        }
        private void Awake() => Initialize();
        private void OnEnable()
        {
            Initialize();
            clock.Reset(PlayerSpriteState.Idle);
            hasPresented = false;
            if (placeholder != null && hidePlaceholder) placeholder.enabled = false;
            Refresh(0f);
        }
        private void Initialize()
        {
            player = GetComponent<PlayerController>();
            capsule = GetComponent<CapsuleCollider>();
            cellWorldSize = GridCellSizeUtility.Resolve(player);
            if (animationSet == null) animationSet = Resources.Load<PlayerSpriteAnimationSet>("PlayerVisuals/PlayerSprites");
            if (spriteMaterial == null) spriteMaterial = Resources.Load<Material>("PlayerVisuals/PlayerSprite");
            if (visual == null && animationSet != null)
            {
                var child = new GameObject("Player Visual");
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);
                visual = child.AddComponent<SpriteRenderer>();
                visual.sortingOrder = 10;
            }
            if (visual == null || animationSet == null) return;
            if (spriteMaterial != null) visual.sharedMaterial = spriteMaterial;
            if (placeholder == null)
            {
                placeholder = GetComponent<MeshRenderer>();
                if (placeholder == null)
                {
                    Transform legacy = transform.Find("Visual");
                    if (legacy != null) placeholder = legacy.GetComponent<MeshRenderer>();
                }
                if (placeholder != null) placeholderWasEnabled = placeholder.enabled;
            }
        }
        private void LateUpdate() => Refresh(Time.deltaTime);
        private void Refresh(float delta)
        {
            if (visual == null || animationSet == null || player == null) return;
            PlayerStateId state = player.CurrentStateId;
            bool suspended = state == PlayerStateId.Locked || state == PlayerStateId.Action;
            PlayerSpriteState requested = PlayerSpriteAnimationClock.Resolve(state, player.IsGrounded,
                player.HorizontalVelocity, player.VerticalVelocity, player.MoveInput);
            // Static climbing holds its current pose instead of climbing in place.
            bool stationaryClimb = requested == PlayerSpriteState.Climb &&
                Mathf.Abs(player.HorizontalVelocity) < .05f && Mathf.Abs(player.VerticalVelocity) < .05f;
            bool wasDeadVisual = clock.State == PlayerSpriteState.Death;
            clock.Advance(requested, suspended, stationaryClimb ? 0f : delta);
            var clip = animationSet.GetClip(clock.State);
            int frame = clock.Frame(clip);
            // Do not overwrite a Timeline Sprite track while it owns player control.
            if (!suspended || !hasPresented || wasDeadVisual)
            {
                visual.enabled = frame >= 0;
                visual.sprite = frame >= 0 ? clip.frames[frame] : null;
                hasPresented = true;
            }
            if (!suspended && state != PlayerStateId.Death)
                visual.flipX = player.FacingDirection < 0;
            Vector3 parentScale = transform.lossyScale;
            Sprite sprite = visual.sprite;
            int sourcePixelScale = GridArtworkMetrics.SourcePixelScale(sprite);
            visual.transform.localScale = GridArtworkMetrics.NaturalSpriteScale(sprite, parentScale,
                cellWorldSize, sourcePixelScale);
            if (capsule != null && sprite != null)
            {
                float bodyHeight = Mathf.Max(capsule.height, capsule.radius * 2f) * Mathf.Abs(parentScale.y);
                float pivotHeight = GridArtworkMetrics.PixelsToWorld(sprite.pivot, cellWorldSize, sourcePixelScale).y;
                // Anchor the authored frame bottom to the existing feet offset; visual size never resizes physics.
                visual.transform.position = transform.TransformPoint(capsule.center) + Vector3.up *
                    (-bodyHeight * .5f + pivotHeight + verticalOffsetCells * cellWorldSize);
            }
            if (faceCamera)
            {
                if (viewingCamera == null || !viewingCamera.isActiveAndEnabled) viewingCamera = Camera.main;
                if (viewingCamera != null) visual.transform.rotation = viewingCamera.transform.rotation;
            }
        }
        private void OnDisable()
        {
            if (visual != null) visual.enabled = false;
            if (placeholder != null && hidePlaceholder) placeholder.enabled = placeholderWasEnabled;
        }
    }
}
