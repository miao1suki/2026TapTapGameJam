using Project.ColorBlocks;
using Project.ContactCubes;
using Project.Mechanisms;
using Project.ObjectArtwork;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.BlockFeatures
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class MechanismArtworkPresentation : MonoBehaviour
    {
        [SerializeField] private ObjectArtworkLibrary artwork;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private MechanismArtworkKind kind;
        private DoorController door;
        private DoorButton button;
        private CrankPlatformFeature crank;
        private MovingPlatformFeature platform;
        private ButtonLockFeature redButton;
        private NeutralLadder ladder;
        private LavaHazardFeature lava;
        private DoorHint hint;
        private ColorObject colorObject;
        private ColorKeyPickup colorKey;
        private BoxCollider footprint;
        private MeshRenderer[] placeholders;
        private SpriteRenderer[] indicators;
        private Renderer[] hintRenderers;
        private MonoBehaviour gridOwner;
        private IGridCellSizeProvider grid;
        private float cell = 1f;
        private MaterialPropertyBlock mask;
        private bool initialized, runtimeMaterial, buttonPressed;
        private float elapsed, openProgress;
        private Vector3 previousPosition;
        public SpriteRenderer Visual => visual;
        public MechanismArtworkKind Kind => kind;

        public static bool Supports(GameObject owner) => owner.GetComponent<DoorController>() != null ||
            owner.GetComponent<DoorButton>() != null || owner.GetComponent<CrankPlatformFeature>() != null ||
            owner.GetComponent<MovingPlatformFeature>() != null || owner.GetComponent<ContactCube>() != null ||
            owner.GetComponent<ColorKeyPickup>() != null || owner.GetComponent<ButtonLockFeature>() != null ||
            owner.GetComponent<OneWayPlatform>() != null || owner.GetComponent<NeutralLadder>() != null || owner.GetComponent<LavaHazardFeature>() != null;

        public static MechanismArtworkPresentation Ensure(GameObject owner)
        {
            if (owner == null || !Supports(owner)) return null;
            var component = owner.GetComponent<MechanismArtworkPresentation>();
            if (component == null) component = owner.AddComponent<MechanismArtworkPresentation>();
            component.Refresh(0f);
            return component;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++) RefreshScene(SceneManager.GetSceneAt(i));
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => RefreshScene(scene);
        public static void RefreshScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null && Supports(component.gameObject)) Ensure(component.gameObject);
        }
        private void OnEnable() { initialized = false; elapsed = 0; Refresh(0f); }
        private void OnDisable()
        {
            if (visual != null) visual.enabled = false;
            if (indicators != null) foreach (var item in indicators) if (item != null) item.enabled = false;
        }
        private void LateUpdate() => Refresh(Application.isPlaying ? Time.deltaTime : 0f);
        private void Initialize()
        {
            if (initialized) return;
            artwork = artwork != null ? artwork : ObjectArtworkLibrary.Load();
            if (artwork == null) return;
            door = GetComponent<DoorController>(); button = GetComponent<DoorButton>();
            crank = GetComponent<CrankPlatformFeature>(); platform = GetComponent<MovingPlatformFeature>();
            redButton = GetComponent<ButtonLockFeature>(); colorObject = GetComponent<ColorObject>();
            ladder = GetComponent<NeutralLadder>();
            lava = GetComponent<LavaHazardFeature>();
            var cube = GetComponent<ContactCube>(); var key = GetComponent<ColorKeyPickup>();
            colorKey = key;
            kind = door != null ? MechanismArtworkKind.Door : button != null ? MechanismArtworkKind.DoorButton
                : crank != null ? MechanismArtworkKind.Crank
                : platform != null ? platform.IsCrankControlled ? MechanismArtworkKind.CrankPlatform : MechanismArtworkKind.MovingPlatform
                : cube != null ? cube.Color == CubeColor.Red ? MechanismArtworkKind.RedCube : cube.Color == CubeColor.Green ? MechanismArtworkKind.GreenCube : MechanismArtworkKind.BlueCube
                : key != null ? key.ColorTypeId == "red" ? MechanismArtworkKind.RedKey : key.ColorTypeId == "green" ? MechanismArtworkKind.GreenKey : MechanismArtworkKind.BlueKey
                : redButton != null ? MechanismArtworkKind.RedButton
                : ladder != null ? ladder.SideFacing ? MechanismArtworkKind.LadderSide : MechanismArtworkKind.LadderFront
                : lava != null ? MechanismArtworkKind.Lava : MechanismArtworkKind.OneWayPlatform;
            var entry = artwork.Find(kind);
            if (entry == null || entry.frames == null || entry.frames.Length == 0) return;
            footprint = GetComponent<BoxCollider>();
            if (cube != null) foreach (var box in GetComponentsInChildren<BoxCollider>(true)) if (!box.isTrigger) { footprint = box; break; }
            if (visual == null)
            {
                var child = transform.Find("Mechanism Visual");
                if (child == null) { child = new GameObject("Mechanism Visual").transform; child.SetParent(transform, false); }
                visual = child.GetComponent<SpriteRenderer>();
                if (visual == null) visual = child.gameObject.AddComponent<SpriteRenderer>();
            }
            placeholders = GetComponentsInChildren<MeshRenderer>(true);
            if (door != null)
            {
                hint = GetComponentInChildren<DoorHint>();
                indicators = new SpriteRenderer[3];
                for (int i = 0; i < indicators.Length; i++)
                {
                    var child = visual.transform.Find("Button " + (i + 1));
                    if (child == null) { child = new GameObject("Button " + (i + 1)).transform; child.SetParent(visual.transform, false); }
                    indicators[i] = child.GetComponent<SpriteRenderer>();
                    if (indicators[i] == null) indicators[i] = child.gameObject.AddComponent<SpriteRenderer>();
                    indicators[i].sharedMaterial = artwork.neutralSpriteMaterial;
                    indicators[i].sortingOrder = 1;
                    child.localPosition = new Vector3(0, 0, -.001f);
                }
            }
            initialized = true; previousPosition = transform.position;
            CacheGrid();
            runtimeMaterial = !Application.isPlaying;
            BindMaterial();
        }
        private void BindMaterial()
        {
            runtimeMaterial = Application.isPlaying;
            bool colored = colorObject != null;
            visual.sharedMaterial = colored && Application.isPlaying
                ? ColorAppearanceManager.Instance.GetSharedMaterial(artwork.colorSpriteMaterial, colorObject.BaseColorTypeId, true)
                : colored ? artwork.colorSpriteMaterial : artwork.neutralSpriteMaterial;
            mask = null;
        }
        public void Refresh(float delta)
        {
            Initialize();
            if (!initialized || visual == null) return;
            if (runtimeMaterial != Application.isPlaying) BindMaterial();
            if (button != null) kind = ObjectArtworkLibrary.DoorButtonKind(button.BindingIndex);
            var entry = artwork.Find(kind);
            if (entry == null || entry.frames == null || entry.frames.Length == 0) entry = artwork.Find(MechanismArtworkKind.DoorButton);
            if (entry == null || entry.frames == null || entry.frames.Length == 0) return;
            bool real = !Application.isPlaying || colorObject == null || ColorAppearanceManager.Instance.ShowsUnlockedAppearance(colorObject.BaseColorTypeId);
            foreach (var model in placeholders)
                if (model != null && (!Application.isPlaying || colorObject == null || real || model != colorObject.TargetRenderer)) model.enabled = false;
            visual.enabled = isActiveAndEnabled && (!Application.isPlaying ||
                ((colorKey == null || !colorKey.IsCollectionVisualComplete) &&
                 (colorObject == null || colorObject.IsActive)));
            visual.gameObject.layer = colorObject != null && colorObject.TargetRenderer != null ? colorObject.TargetRenderer.gameObject.layer : gameObject.layer;
            int frame = 0;
            if (crank != null) frame = ObjectArtworkLibrary.ProgressFrame(crank.Progress, entry.frames.Length);
            else if (door != null)
            {
                openProgress = Mathf.MoveTowards(openProgress, door.State == DoorState.Unlocked ? 1f : 0f, Mathf.Max(0, delta) / Mathf.Max(.01f, door.OpenDuration));
                frame = ObjectArtworkLibrary.ProgressFrame(openProgress, entry.frames.Length);
            }
            else if (button != null || redButton != null)
            {
                bool pressed = button != null ? button.IsPressed : redButton.IsSignalActive;
                if (pressed != buttonPressed) { elapsed = 0; buttonPressed = pressed; }
                if (pressed) elapsed += Mathf.Max(0, delta);
                frame = pressed ? ObjectArtworkLibrary.Frame(elapsed, entry.frameSeconds, entry.frames.Length, true) : 0;
            }
            else if (platform != null)
            {
                if ((transform.position - previousPosition).sqrMagnitude > .0000001f) elapsed += Mathf.Max(0, delta);
                frame = ObjectArtworkLibrary.Frame(elapsed, entry.frameSeconds, entry.frames.Length, true);
            }
            previousPosition = transform.position;
            var sprite = door != null && artwork.doorClosed != null ? artwork.doorClosed : entry.frames[Mathf.Clamp(frame, 0, entry.frames.Length - 1)];
            if (visual.sprite != sprite || mask == null)
            {
                visual.sprite = sprite;
                if (colorObject != null && Application.isPlaying)
                {
                    mask ??= new MaterialPropertyBlock(); mask.Clear();
                    mask.SetFloat("_ColorMaskGroup", ColorAppearanceManager.GetGroupIndex(colorObject.BaseColorTypeId));
                    mask.SetFloat("_ColorMaskReal", 1); mask.SetFloat("_ColorMaskUseVertexAlpha", 1);
                    mask.SetTexture("_ColorMaskAlphaTexture", sprite.texture); visual.SetPropertyBlock(mask);
                }
            }
            Layout(sprite);
            if (door != null)
            {
                for (int i = 0; i < indicators.Length; i++)
                {
                    bool pressed = Application.isPlaying && i < door.Buttons.Count && door.Buttons[i] != null && door.Buttons[i].IsPressed;
                    var frames = pressed ? artwork.doorIndicators : artwork.doorWhiteIndicators;
                    indicators[i].sprite = i < frames.Length ? frames[i] : null;
                    indicators[i].transform.localPosition = ObjectArtworkLibrary.DoorIndicatorOffset(i, door.Buttons.Count);
                    indicators[i].gameObject.layer = gameObject.layer;
                    indicators[i].enabled = visual.enabled && openProgress < .999f && i < door.Buttons.Count && door.Buttons[i] != null;
                }
                // Door artwork now owns the prompt; preserve the hint's binding/state component.
                if (hint == null) hint = GetComponentInChildren<DoorHint>();
                if (hint != null && hintRenderers == null) hintRenderers = hint.GetComponentsInChildren<Renderer>(true);
                if (hintRenderers != null) foreach (var renderer in hintRenderers) if (renderer != null) renderer.enabled = false;
            }
        }
        private void Layout(Sprite sprite)
        {
            if (sprite == null) return;
            Vector3 center = Vector3.zero, size = Vector3.one;
            if (footprint != null)
            {
                var toRoot = transform.worldToLocalMatrix * footprint.transform.localToWorldMatrix;
                center = toRoot.MultiplyPoint3x4(footprint.center);
                Vector3 x = toRoot.MultiplyVector(Vector3.right * footprint.size.x);
                Vector3 y = toRoot.MultiplyVector(Vector3.up * footprint.size.y);
                Vector3 z = toRoot.MultiplyVector(Vector3.forward * footprint.size.z);
                size = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            }
            if (gridOwner != null) { grid ??= gridOwner as IGridCellSizeProvider; if (grid != null) cell = Mathf.Max(.01f, grid.CellWorldSize); }
            Vector3 scale = transform.lossyScale;
            bool tile = ladder != null || lava != null || kind == MechanismArtworkKind.RedCube || kind == MechanismArtworkKind.GreenCube || kind == MechanismArtworkKind.BlueCube;
            bool sliced = platform != null || kind == MechanismArtworkKind.OneWayPlatform;
            visual.transform.localRotation = door != null && door.IsHorizontal ? Quaternion.Euler(0, 0, 90) : Quaternion.identity;
            if (tile || sliced)
            {
                visual.drawMode = SpriteDrawMode.Tiled;
                visual.tileMode = SpriteTileMode.Continuous;
                visual.transform.localScale = ObjectArtworkLibrary.TileScale(scale, cell);
                // Platform art is a five-pixel-high strip at the collision top, not an inflated solid block.
                visual.size = new Vector2(Mathf.Abs(size.x * scale.x) / cell, tile ? Mathf.Abs(size.y * scale.y) / cell : sprite.rect.height / sprite.pixelsPerUnit);
                float bottom = tile ? center.y - size.y * .5f : center.y + size.y * .5f - visual.size.y * cell / Mathf.Max(.001f, Mathf.Abs(scale.y));
                visual.transform.localPosition = new Vector3(center.x, bottom, center.z - size.z * .5f - .015f * cell / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            }
            else
            {
                visual.drawMode = SpriteDrawMode.Simple;
                bool horizontal = door != null && door.IsHorizontal;
                float width = horizontal ? size.y : size.x, height = horizontal ? size.x : size.y;
                if (button != null || redButton != null) { width = 1f; height = 1f; }
                visual.transform.localScale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
                if (door != null && artwork.doorClosed != null)
                {
                    var localScale = visual.transform.localScale;
                    localScale.x *= Mathf.Cos(openProgress * Mathf.PI * .5f);
                    visual.transform.localScale = localScale;
                }
                // Bottom-pivot sprites fit the complete footprint, including the door's two cells.
                visual.transform.localPosition = horizontal ? new Vector3(center.x + size.x * .5f, center.y, center.z - size.z * .5f - .015f)
                    : new Vector3(center.x, (button != null ? -.5f : center.y - size.y * .5f), center.z - size.z * .5f - .015f);
            }
        }
        private void OnTransformParentChanged() => CacheGrid();
        private void CacheGrid()
        {
            gridOwner = null; grid = null; cell = GridCellSizeUtility.FallbackCellWorldSize;
            foreach (var component in GetComponentsInParent<MonoBehaviour>(true))
                if (component is IGridCellSizeProvider provider)
                { grid = provider; gridOwner = component; cell = Mathf.Max(.01f, provider.CellWorldSize); break; }
        }
    }
}
