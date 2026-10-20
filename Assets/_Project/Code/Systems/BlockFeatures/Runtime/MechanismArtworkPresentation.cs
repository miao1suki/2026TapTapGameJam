using Project.ColorBlocks;
using Project.ContactCubes;
using Project.Mechanisms;
using Project.ObjectArtwork;
using Project.WorldItems;
using Project.Items;
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
        private NeutralSpike spike;
        private Transform spikeDirection;
        private MechanismBaseFeature mechanismBase;
        private PlantObstacleFeature obstacle;
        private LavaHazardFeature lava;
        private OneWayPlatform oneWay;
        private DoorHint hint;
        private ColorObject colorObject;
        private ColorKeyPickup colorKey;
        private ContactCubeMotion contactCubeMotion;
        private BoxCollider footprint;
        private MeshRenderer[] placeholders;
        private SpriteRenderer[] indicators;
        private Renderer[] hintRenderers;
        private MonoBehaviour gridOwner;
        private Project.LevelEditor.LevelEditorMergedShape shape;
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
            owner.GetComponent<OneWayPlatform>() != null || owner.GetComponent<NeutralLadder>() != null || owner.GetComponent<LavaHazardFeature>() != null ||
            owner.GetComponent<NeutralSpike>() != null || owner.GetComponent<MechanismBaseFeature>() != null ||
            owner.GetComponent<CollectiblePickup>() != null ||
            owner.GetComponent<PlantObstacleFeature>() != null || owner.GetComponent<MechanismArtworkPresentation>() != null;

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
        // Edit-time Scene artwork is a read-only Editor overlay. Do not add
        // serialized children or change authored Renderer flags merely by opening
        // a scene. Explicit Ensure/Refresh remain available to asset generators.
        private void OnEnable() { if (!Application.isPlaying) return; initialized = false; elapsed = 0; Refresh(0f); }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (visual != null) visual.enabled = false;
            if (indicators != null) foreach (var item in indicators) if (item != null) item.enabled = false;
        }
        private void LateUpdate() { if (Application.isPlaying) Refresh(Time.deltaTime); }
        private void Initialize()
        {
            if (initialized) return;
            artwork = artwork != null ? artwork : ObjectArtworkLibrary.Load();
            if (artwork == null) return;
            door = GetComponent<DoorController>(); button = GetComponent<DoorButton>();
            crank = GetComponent<CrankPlatformFeature>(); platform = GetComponent<MovingPlatformFeature>();
            redButton = GetComponent<ButtonLockFeature>(); colorObject = GetComponent<ColorObject>();
            ladder = GetComponent<NeutralLadder>();
            spike = GetComponent<NeutralSpike>();
            mechanismBase = GetComponent<MechanismBaseFeature>();
            obstacle = GetComponent<PlantObstacleFeature>();
            if (spike != null)
            {
                var volume = GetComponentInChildren<MeshCollider>(true);
                if (volume != null) spikeDirection = volume.transform;
            }
            lava = GetComponent<LavaHazardFeature>();
            oneWay = GetComponent<OneWayPlatform>();
            var cube = GetComponent<ContactCube>(); var key = GetComponent<ColorKeyPickup>();
            contactCubeMotion = cube != null ? GetComponent<ContactCubeMotion>() : null;
            colorKey = key;
            kind = door != null ? MechanismArtworkKind.Door : button != null ? MechanismArtworkKind.DoorButton
                : crank != null ? MechanismArtworkKind.Crank
                : platform != null ? platform.IsCrankControlled ? MechanismArtworkKind.CrankPlatform : MechanismArtworkKind.MovingPlatform
                : cube != null ? cube.Color == CubeColor.Red ? MechanismArtworkKind.RedCube : cube.Color == CubeColor.Green ? MechanismArtworkKind.GreenCube : MechanismArtworkKind.BlueCube
                : key != null ? key.ColorTypeId == "red" ? MechanismArtworkKind.RedKey : key.ColorTypeId == "green" ? MechanismArtworkKind.GreenKey : MechanismArtworkKind.BlueKey
                : redButton != null ? MechanismArtworkKind.RedButton
                : mechanismBase != null ? MechanismArtworkKind.MechanismBase
                : obstacle != null ? MechanismArtworkKind.PlantObstacle
                : ladder != null ? ladder.SideFacing ? MechanismArtworkKind.LadderSide : MechanismArtworkKind.LadderFront
                : spike != null ? spike.ArtworkKind
                : GetComponent<CollectiblePickup>() != null ? MechanismArtworkKind.Collectible
                : lava != null ? MechanismArtworkKind.Lava
                : oneWay != null ? oneWay.ArtworkKind : kind;
            var entry = artwork.Find(kind);
            if (entry == null || entry.frames == null || entry.frames.Length == 0) return;
            footprint = GetComponent<BoxCollider>();
            shape = GetComponent<Project.LevelEditor.LevelEditorMergedShape>();
            if (cube != null) foreach (var box in GetComponentsInChildren<BoxCollider>(true)) if (!box.isTrigger) { footprint = box; break; }
            if (visual == null)
            {
                var child = transform.Find("Mechanism Visual");
                if (child == null) { child = new GameObject("Mechanism Visual").transform; child.SetParent(transform, false); }
                visual = child.GetComponent<SpriteRenderer>();
                if (visual == null) visual = child.gameObject.AddComponent<SpriteRenderer>();
            }
            placeholders = System.Array.FindAll(GetComponentsInChildren<MeshRenderer>(true),
                model => model.GetComponentInParent<Project.FlowMagma.FlowMagmaVisual>() == null);
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
            if (ladder != null) ladder.PreserveLegacyFootprint();
            if (runtimeMaterial != Application.isPlaying) BindMaterial();
            if (button != null) kind = ObjectArtworkLibrary.DoorButtonKind(button.BindingIndex);
            if (oneWay != null) kind = oneWay.ArtworkKind;
            if (spike != null) kind = spike.ArtworkKind;
            var entry = artwork.Find(kind);
            if (entry == null || entry.frames == null || entry.frames.Length == 0) entry = artwork.Find(MechanismArtworkKind.DoorButton);
            if (entry == null || entry.frames == null || entry.frames.Length == 0) return;
            bool real = !Application.isPlaying || colorObject == null || ColorAppearanceManager.Instance.ShowsUnlockedAppearance(colorObject.BaseColorTypeId);
            foreach (var model in placeholders)
                if (model != null && (!Application.isPlaying || colorObject == null || real || model != colorObject.TargetRenderer)) model.enabled = false;
            visual.enabled = isActiveAndEnabled && (!Application.isPlaying ||
                ((colorKey == null || !colorKey.IsCollectionVisualComplete) &&
                 (colorObject == null || colorObject.IsActive) && (obstacle == null || obstacle.ShowsArtwork)));
            visual.gameObject.layer = oneWay != null ? oneWay.ArtworkLayer
                : colorObject != null && colorObject.TargetRenderer != null ? colorObject.TargetRenderer.gameObject.layer : gameObject.layer;
            int frame = 0;
            if (mechanismBase != null) frame = mechanismBase.IsSignalActive ? 1 : 0;
            else if (crank != null) frame = ObjectArtworkLibrary.ProgressFrame(crank.Progress, entry.frames.Length);
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
            if (platform != null || oneWay != null)
            {
                DisplayBounds(out _, out var platformSize);
                Vector3 authorScale = transform.lossyScale;
                float length = Mathf.Max(Mathf.Abs(platformSize.x * authorScale.x), Mathf.Abs(platformSize.y * authorScale.y)) / cell;
                sprite = PlatformArtworkLayout.CroppedFrame(sprite, length);
            }
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
                else if (oneWay != null && Application.isPlaying)
                {
                    // Decoration-style HSV classification. Never use the color
                    // reveal material: locked colors must not hide the platform.
                    mask ??= new MaterialPropertyBlock(); mask.Clear();
                    mask.SetFloat("_ColorMaskGroup", -1f); mask.SetFloat("_ColorMaskReal", 1f);
                    mask.SetFloat("_ColorMaskUseVertexAlpha", 1f); mask.SetFloat("_ColorMaskOpacity", 1f);
                    mask.SetTexture("_ColorMaskAlphaTexture", sprite.texture); visual.SetPropertyBlock(mask);
                }
            }
            if (lava != null && lava.HasFlowArtwork) visual.enabled = false;
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
        private void DisplayBounds(out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero; size = Vector3.one;
            if (door != null && door.TryGetPlanningFootprint(out var doorBounds))
            {
                center = doorBounds.center; size = doorBounds.size;
                return;
            }
            if (footprint != null)
            {
                var toRoot = transform.worldToLocalMatrix * footprint.transform.localToWorldMatrix;
                center = toRoot.MultiplyPoint3x4(footprint.center);
                // Red/blue physical boxes have a horizontal clearance skin so
                // they can fall through a one-cell opening. Tiling from that
                // .96-cell collision width crops the last artwork column and
                // makes the right outline thinner. Artwork uses the original
                // logical face, exactly as color contacts already do; do not
                // expand the real collider or stretch the source pixels.
                Vector3 artworkSize = contactCubeMotion != null
                    ? contactCubeMotion.LogicalContactShapeSize : footprint.size;
                Vector3 x = toRoot.MultiplyVector(Vector3.right * artworkSize.x);
                Vector3 y = toRoot.MultiplyVector(Vector3.up * artworkSize.y);
                Vector3 z = toRoot.MultiplyVector(Vector3.forward * artworkSize.z);
                size = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            }
            if (shape == null) shape = GetComponent<Project.LevelEditor.LevelEditorMergedShape>();
            if (shape != null) { center = shape.LocalBounds.center; size = shape.LocalBounds.size; }
        }
        private void Layout(Sprite sprite)
        {
            if (sprite == null) return;
            if (gridOwner != null) { grid ??= gridOwner as IGridCellSizeProvider; if (grid != null) cell = Mathf.Max(.01f, grid.CellWorldSize); }
            DisplayBounds(out var center, out var size);
            Vector3 scale = transform.lossyScale;
            if (button != null || redButton != null)
            {
                // All frames are complete 16x16 cells, with a bottom pivot.
                // Rotate about the cell centre, not about the pressure trigger
                // or its thin model bounds. Swapping parent axes keeps pixels
                // square even with a non-uniform authored root scale.
                ButtonFacing facing = button != null ? button.Facing : redButton.Facing;
                bool sideways = facing == ButtonFacing.Left || facing == ButtonFacing.Right;
                Quaternion rotation = ButtonPressureGeometry.Rotation(facing);
                visual.drawMode = SpriteDrawMode.Simple;
                visual.transform.localRotation = rotation;
                visual.transform.localScale = GridArtworkMetrics.NaturalSpriteScale(sprite,
                    sideways ? new Vector3(scale.y, scale.x, scale.z) : scale, cell);
                Vector3 edge = rotation * Vector3.down * (cell * .5f);
                visual.transform.localPosition = new Vector3(edge.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    edge.y / Mathf.Max(.001f, Mathf.Abs(scale.y)), center.z - size.z * .5f - .015f);
                return;
            }
            if (spike != null)
            {
                Quaternion rotation = Quaternion.Euler(0, 0, spike.ArtworkRotationDegrees);
                bool sideways = Mathf.Abs((rotation * Vector3.right).y) > .9f;
                visual.drawMode = SpriteDrawMode.Tiled;
                visual.tileMode = SpriteTileMode.Continuous;
                visual.transform.localRotation = rotation;
                visual.transform.localScale = ObjectArtworkLibrary.PlatformTileScale(scale, cell, sideways);
                visual.size = new Vector2(Mathf.Abs(sideways ? size.y * scale.y : size.x * scale.x) / cell,
                    sprite.rect.height / sprite.pixelsPerUnit);
                Vector3 baseEdge = rotation * (Vector3.down * .5f);
                visual.transform.localPosition = center + new Vector3(baseEdge.x * size.x, baseEdge.y * size.y, -size.z * .5f - .015f);
                spike.RenderArtwork(visual, artwork.FirstFrame(ObjectArtworkLibrary.SpikeKind(spike.Size == NeutralSpike.SpikeSize.Large, false)),
                    artwork.FirstFrame(ObjectArtworkLibrary.SpikeKind(spike.Size == NeutralSpike.SpikeSize.Large, true)), cell, artwork.neutralMeshMaterial);
                return;
            }
            bool tile = ladder != null || lava != null || obstacle != null || kind == MechanismArtworkKind.Obsidian || kind == MechanismArtworkKind.RedCube || kind == MechanismArtworkKind.GreenCube || kind == MechanismArtworkKind.BlueCube;
            bool sliced = platform != null || oneWay != null;
            Vector2 platformDimensions = new Vector2(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y));
            bool verticalPlatform = sliced && (platform != null ? platform.IsArtworkVertical(platformDimensions)
                : ObjectArtworkLibrary.PlatformVisualIsVertical(platformDimensions));
            visual.flipX = ladder != null && ladder.SideFacing && ladder.WallSide == LadderWallSide.Left;
            // A narrow ladder trigger is not its art footprint. Its complete 16-pixel
            // tile still occupies one authored cell, including existing long ladders.
            if (ladder != null) size.x = 1f;
            visual.transform.localRotation = door != null && door.IsHorizontal ? Quaternion.Euler(0, 0, 90) : Quaternion.identity;
            if (tile || sliced)
            {
                visual.drawMode = SpriteDrawMode.Tiled;
                visual.tileMode = SpriteTileMode.Continuous;
                visual.transform.localScale = ObjectArtworkLibrary.PlatformTileScale(scale, cell, verticalPlatform);
                float depth = center.z - size.z * .5f - .015f * cell / Mathf.Max(.001f, Mathf.Abs(scale.z));
                if (verticalPlatform)
                {
                    visual.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    visual.size = new Vector2(Mathf.Abs(size.y * scale.y) / cell, sprite.rect.height / sprite.pixelsPerUnit);
                    float visibleThickness = PlatformArtworkLayout.VisibleThicknessCells(kind, sprite, artwork);
                    visual.transform.localPosition = new Vector3(center.x - size.x * .5f + visibleThickness * cell / Mathf.Max(.001f, Mathf.Abs(scale.x)), center.y, depth);
                }
                else
                {
                    visual.size = new Vector2(Mathf.Abs(size.x * scale.x) / cell, tile ? Mathf.Abs(size.y * scale.y) / cell : sprite.rect.height / sprite.pixelsPerUnit);
                    float visibleThickness = sliced ? PlatformArtworkLayout.VisibleThicknessCells(kind, sprite, artwork) : visual.size.y;
                    float bottom = tile ? center.y - size.y * .5f : center.y + size.y * .5f - visibleThickness * cell / Mathf.Max(.001f, Mathf.Abs(scale.y));
                    visual.transform.localPosition = new Vector3(center.x, bottom, depth);
                }
            }
            else
            {
                visual.drawMode = SpriteDrawMode.Simple;
                bool horizontal = door != null && door.IsHorizontal;
                Vector3 parentAxes = horizontal ? new Vector3(scale.y, scale.x, scale.z) : scale;
                visual.transform.localScale = GridArtworkMetrics.NaturalSpriteScale(sprite, parentAxes, cell);
                if (door != null && artwork.doorClosed != null)
                {
                    var localScale = visual.transform.localScale;
                    localScale.x *= Mathf.Cos(openProgress * Mathf.PI * .5f);
                    visual.transform.localScale = localScale;
                }
                // Bottom-pivot sprites fit the complete footprint, including the door's two cells.
                float bottom = colorKey != null || kind == MechanismArtworkKind.Collectible
                    ? center.y - sprite.rect.height / GridArtworkMetrics.PixelsPerCell * cell * .5f / Mathf.Max(.001f, Mathf.Abs(scale.y))
                    : button != null ? -.5f : center.y - size.y * .5f;
                visual.transform.localPosition = horizontal ? new Vector3(center.x + size.x * .5f, center.y, center.z - size.z * .5f - .015f)
                    : new Vector3(center.x, bottom, center.z - size.z * .5f - .015f);
            }
            if (shape != null && (tile || sliced)) shape.ApplySprite(visual, sliced,
                sliced ? PlatformArtworkLayout.VisibleThicknessCells(kind, sprite, artwork) : 0f);
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
