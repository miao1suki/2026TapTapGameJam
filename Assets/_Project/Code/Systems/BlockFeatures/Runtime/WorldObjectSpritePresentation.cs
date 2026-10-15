using Project.ColorBlocks;
using Project.ObjectArtwork;
using UnityEngine;

namespace Project.BlockFeatures
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WorldObjectSpritePresentation : MonoBehaviour
    {
        [SerializeField] private ObjectArtworkLibrary artwork;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private float depthOffsetCells = .015f;
        private RespawnAnchorFeature anchor;
        private BouncePlantFeature plant;
        private BoxCollider footprint;
        private Renderer placeholder;
        private ColorObject colorObject;
        private RespawnArtworkState state;
        private bool hasState, bouncing, initialized, runtimeBound;
        private float elapsed;
        private MaterialPropertyBlock mask;
        private IGridCellSizeProvider gridProvider;
        private MonoBehaviour gridProviderOwner;
        private bool searchedGrid;
        private float gridRefreshAt;
        public SpriteRenderer Visual => visual;
        public RespawnArtworkState CurrentRespawnState => state;
        public bool IsBouncePlaying => bouncing;

        public static WorldObjectSpritePresentation Ensure(GameObject owner)
        {
            if (owner == null || ObjectArtworkLibrary.Load() == null) return null;
            var result = owner.GetComponent<WorldObjectSpritePresentation>();
            if (result == null) result = owner.AddComponent<WorldObjectSpritePresentation>();
            result.searchedGrid = false;
            result.Refresh(0f);
            return result;
        }
        private void OnEnable()
        {
            Initialize();
            mask = null;
            BindMaterial();
            if (plant != null) { plant.BounceTriggered += PlayBounce; plant.AppearanceReset += ResetPlayback; }
            ResetPlayback();
            Refresh(0f);
        }
        private void Initialize()
        {
            if (initialized) return;
            anchor = GetComponent<RespawnAnchorFeature>();
            plant = GetComponent<BouncePlantFeature>();
            colorObject = GetComponent<ColorObject>();
            footprint = GetComponent<BoxCollider>();
            if (artwork == null) artwork = ObjectArtworkLibrary.Load();
            if (artwork == null || anchor == null && plant == null) return;
            if (visual == null)
            {
                Transform child = transform.Find("Object Visual");
                if (child == null) { child = new GameObject("Object Visual").transform; child.SetParent(transform, false); }
                visual = child.GetComponent<SpriteRenderer>();
                if (visual == null) visual = child.gameObject.AddComponent<SpriteRenderer>();
            }
            placeholder = GetComponent<MeshRenderer>();
            visual.color = Color.white;
            visual.sortingOrder = -1;
            visual.sharedMaterial = artwork.neutralSpriteMaterial;
            if (placeholder != null && (!Application.isPlaying || anchor != null)) placeholder.enabled = false;
            initialized = true;
            BindMaterial();
        }
        private void BindMaterial()
        {
            if (!initialized) return;
            runtimeBound = Application.isPlaying;
            searchedGrid = false;
            if (plant != null && Application.isPlaying)
            {
                visual.sharedMaterial = ColorAppearanceManager.Instance.GetSharedMaterial(artwork.colorSpriteMaterial, "green", true);
            }
            else visual.sharedMaterial = plant != null ? artwork.colorSpriteMaterial : artwork.neutralSpriteMaterial;
        }
        private void OnDisable()
        {
            if (plant != null) { plant.BounceTriggered -= PlayBounce; plant.AppearanceReset -= ResetPlayback; }
            ResetPlayback();
            if (visual != null) visual.enabled = false;
        }
        private void LateUpdate() => Refresh(Application.isPlaying ? Time.deltaTime : 0f);
        public void PlayBounce()
        {
            if (!initialized || plant == null) return;
            elapsed = 0f;
            bouncing = true;
        }
        public void ResetPlayback() { elapsed = 0f; bouncing = false; hasState = false; }
        public void Refresh(float delta)
        {
            Initialize();
            if (!initialized || visual == null) return;
            if (runtimeBound != Application.isPlaying) { BindMaterial(); mask = null; ResetPlayback(); }
            if (placeholder != null && (!Application.isPlaying || anchor != null)) placeholder.enabled = false;
            visual.gameObject.layer = gameObject.layer;
            float cell = CellSize();
            Vector3 authorScale = transform.lossyScale;
            Vector3 size = footprint != null ? footprint.size : Vector3.one;
            Vector3 center = footprint != null ? footprint.center : Vector3.zero;
            Sprite frame;
            if (anchor != null)
            {
                var requested = ObjectArtworkLibrary.ResolveRespawn(anchor.IsActivated, anchor.IsCurrent);
                if (!hasState || requested != state) { state = requested; elapsed = 0f; hasState = true; }
                elapsed += Mathf.Max(0f, delta);
                int index = ObjectArtworkLibrary.Frame(elapsed, artwork.respawnFrameSeconds, artwork.respawnCurrent.Length, true);
                frame = state == RespawnArtworkState.Current && index >= 0 ? artwork.respawnCurrent[index]
                    : state == RespawnArtworkState.Locked ? artwork.respawnLocked : artwork.respawnUsed;
                visual.drawMode = SpriteDrawMode.Simple;
                SetLayout(ObjectArtworkLibrary.TileScale(authorScale, cell),
                    center + new Vector3(0f, -.5f * cell / Mathf.Max(.001f, Mathf.Abs(authorScale.y)), -.51f));
                visual.enabled = isActiveAndEnabled && frame != null;
            }
            else
            {
                elapsed += Mathf.Max(0f, delta);
                if (bouncing && elapsed >= artwork.plantBounce.Length * artwork.plantFrameSeconds) bouncing = false;
                int index = bouncing ? ObjectArtworkLibrary.Frame(elapsed, artwork.plantFrameSeconds, artwork.plantBounce.Length, false)
                    : Mathf.Clamp(artwork.plantIdleFrame, 0, Mathf.Max(0, artwork.plantBounce.Length - 1));
                frame = artwork.plantBounce.Length > 0 ? artwork.plantBounce[index] : null;
                // Width/height use the authored collision footprint; never touch its physics or merge identity.
                visual.drawMode = SpriteDrawMode.Tiled;
                visual.tileMode = SpriteTileMode.Continuous;
                Vector2 tiles = new Vector2(Mathf.Abs(authorScale.x * size.x) / cell, Mathf.Abs(authorScale.y * size.y) / cell);
                if (visual.size != tiles) visual.size = tiles;
                float bottom = center.y - size.y * .5f;
                SetLayout(ObjectArtworkLibrary.TileScale(authorScale, cell),
                    new Vector3(center.x, bottom, center.z - size.z * .5f - depthOffsetCells * cell / Mathf.Max(.001f, Mathf.Abs(authorScale.z))));
                visual.enabled = isActiveAndEnabled && frame != null && (!Application.isPlaying || colorObject == null || colorObject.IsActive);
            }
            if (visual.sprite != frame || plant != null && Application.isPlaying && mask == null)
            {
                visual.sprite = frame;
                if (plant != null && Application.isPlaying)
                {
                    if (mask == null) mask = new MaterialPropertyBlock();
                    mask.Clear();
                    mask.SetFloat("_ColorMaskGroup", 1f);
                    mask.SetFloat("_ColorMaskReal", 1f);
                    mask.SetFloat("_ColorMaskUseVertexAlpha", 1f);
                    mask.SetTexture("_ColorMaskAlphaTexture", frame != null ? frame.texture : Texture2D.whiteTexture);
                    visual.SetPropertyBlock(mask);
                }
            }
        }
        private void SetLayout(Vector3 scale, Vector3 position)
        {
            if (visual.transform.localScale != scale) visual.transform.localScale = scale;
            if (visual.transform.localPosition != position) visual.transform.localPosition = position;
        }
        private void OnTransformParentChanged() { searchedGrid = false; gridProviderOwner = null; }
        private float CellSize()
        {
            // Unity restores the MonoBehaviour reference on domain reload, but not its
            // interface cache. Rebind the interface before using the surviving owner.
            if (gridProviderOwner != null)
            {
                gridProvider ??= gridProviderOwner as IGridCellSizeProvider;
                if (gridProvider != null) return Mathf.Max(.01f, gridProvider.CellWorldSize);
                gridProviderOwner = null;
                searchedGrid = false;
            }
            if (!searchedGrid || !Application.isPlaying && Time.realtimeSinceStartup >= gridRefreshAt)
            {
                searchedGrid = true;
                gridRefreshAt = Time.realtimeSinceStartup + .5f;
                foreach (var component in GetComponentsInParent<MonoBehaviour>(true))
                    if (component is IGridCellSizeProvider provider)
                    { gridProvider = provider; gridProviderOwner = component; return Mathf.Max(.01f, provider.CellWorldSize); }
            }
            return GridCellSizeUtility.FallbackCellWorldSize;
        }
    }
}
