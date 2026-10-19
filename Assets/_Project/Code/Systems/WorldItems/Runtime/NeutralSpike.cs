using Project.ColorBlocks;
using Project.LevelEditor;
using Project.Player;
using Project.ObjectArtwork;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.WorldItems
{
    [DisallowMultipleComponent]
    public sealed class NeutralSpike : MonoBehaviour, IRoomColorResettable
    {
        public enum SpikeSize { [InspectorName("小尖刺")] Small, [InspectorName("大尖刺")] Large }
        public enum SpikeFacing { [InspectorName("沿用原方向")] Authored, [InspectorName("上")] Up,
            [InspectorName("下")] Down, [InspectorName("左")] Left, [InspectorName("右")] Right }
        [SerializeField] private SpikeSize size;
        [SerializeField] private bool initiallyBloody;
        [SerializeField] private SpikeFacing facing;
        private MeshCollider sourceVolume;
        private Mesh tiledContactMesh;
        private Mesh sizedContactMesh;
        // Session presentation only. Never serialize these cells into planning/save data.
        private readonly HashSet<Vector2Int> bloodyCells = new HashSet<Vector2Int>();
        private Mesh artworkMesh;
        private MeshRenderer artworkRenderer;
        private int artworkRevision, renderedRevision = -1;
        private Sprite renderedSprite;
        private Vector3 renderedScale;
        private Vector2Int renderedDimensions;
        private float renderedCellWorldSize;
        private MaterialPropertyBlock artworkProperties;
        public SpikeSize Size => size;
        public bool InitiallyBloody => initiallyBloody;
        public int BloodyCellCount => bloodyCells.Count;
        public bool IsCellBloody(Vector2Int cell) => initiallyBloody || bloodyCells.Contains(cell);
        public MechanismArtworkKind ArtworkKind => ObjectArtworkLibrary.SpikeKind(size == SpikeSize.Large, initiallyBloody);
        public float ArtworkRotationDegrees => facing == SpikeFacing.Up ? 0f : facing == SpikeFacing.Down ? 180f
            : facing == SpikeFacing.Left ? 90f : facing == SpikeFacing.Right ? -90f
            : SourceVolume != null ? SourceVolume.transform.localEulerAngles.z : 0f;
        private MeshCollider SourceVolume => sourceVolume != null ? sourceVolume : sourceVolume = GetComponentInChildren<MeshCollider>(true);

        public void Configure(SpikeSize valueSize, bool bloody, SpikeFacing valueFacing)
        { size = valueSize; initiallyBloody = bloody; facing = valueFacing; artworkRevision++; }

        private void Awake() => RefreshContacts();
        private void OnEnable() => RefreshContacts();
        private void Start() => RefreshContacts();
        public void ResetForRoom() => RefreshContacts();

        private void RefreshContacts()
        {
            ConfigureSourceContact();
            ConfigureMergedContacts();
            BindChildContacts();
        }

        private void ConfigureSourceContact()
        {
            var volume = SourceVolume;
            if (volume == null || volume.sharedMesh == null || sizedContactMesh != null) return;
            float angle = ArtworkRotationDegrees;
            volume.transform.localRotation = Quaternion.Euler(0, 0, angle);
            var original = volume.sharedMesh;
            sizedContactMesh = Instantiate(original);
            sizedContactMesh.name = "Spike Instance Contact"; sizedContactMesh.hideFlags = HideFlags.DontSave;
            var vertices = original.vertices; Bounds bounds = original.bounds;
            float height = size == SpikeSize.Large ? .5f : .25f;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i].y = -.5f + (vertices[i].y - bounds.min.y) / Mathf.Max(.0001f, bounds.size.y) * height;
            sizedContactMesh.vertices = vertices; sizedContactMesh.RecalculateBounds();
            volume.sharedMesh = sizedContactMesh;
        }

        private void ConfigureMergedContacts()
        {
            if (tiledContactMesh != null)
            {
                // RoomResetObject restores authored collider states before this hook.
                if (sourceVolume != null) sourceVolume.enabled = false;
                return;
            }
            var placed = GetComponent<LevelEditorPlacedBlock>();
            var shape = GetComponent<LevelEditorMergedShape>();
            Vector2Int size = shape != null ? shape.Dimensions : placed != null ? placed.SizeCells : Vector2Int.one;
            if (size.x < 1 || size.y < 1 || size == Vector2Int.one) return;
            sourceVolume = SourceVolume;
            if (sourceVolume == null || !sourceVolume.enabled || sourceVolume.sharedMesh == null) return;

            // Scaling a whole row into one giant triangle leaves its outer cells
            // without a trigger. Repeat the original convex prism per occupied cell.
            // Bake rotation before compensating nonuniform root scale (left/right).
            var vertices = sourceVolume.sharedMesh.vertices;
            Matrix4x4 toRoot = transform.worldToLocalMatrix * sourceVolume.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = toRoot.MultiplyPoint3x4(vertices[i]);
                vertices[i] = new Vector3(point.x / size.x, point.y / size.y, point.z);
            }
            tiledContactMesh = new Mesh { name = "Spike Cell Contact", hideFlags = HideFlags.DontSave };
            tiledContactMesh.vertices = vertices;
            tiledContactMesh.triangles = sourceVolume.sharedMesh.triangles;
            tiledContactMesh.RecalculateBounds();
            if (shape != null)
            {
                foreach (var rect in shape.Rectangles)
                    foreach (var cell in rect.allPositionsWithin) AddCellContact(cell, size);
            }
            else
                foreach (var cell in new RectInt(Vector2Int.zero, size).allPositionsWithin) AddCellContact(cell, size);
            sourceVolume.enabled = false;
        }

        private void AddCellContact(Vector2Int cell, Vector2Int size)
        {
            var child = new GameObject("__SpikeContact_" + cell.x + "_" + cell.y) { hideFlags = HideFlags.DontSave };
            child.layer = sourceVolume.gameObject.layer;
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3((cell.x + .5f) / size.x - .5f, .5f - (cell.y + .5f) / size.y, 0f);
            var volume = child.AddComponent<MeshCollider>();
            volume.sharedMesh = tiledContactMesh; volume.convex = true; volume.isTrigger = true;
            child.AddComponent<SpikeContactRelay>().Bind(this, cell);
        }

        private void OnDestroy()
        {
            if (tiledContactMesh != null) Destroy(tiledContactMesh);
            if (sizedContactMesh != null) Destroy(sizedContactMesh);
            if (artworkMesh != null) Destroy(artworkMesh);
        }

        private void BindChildContacts()
        {
            // Static child colliders do not send physics messages to an arbitrary
            // parent MonoBehaviour. Keep their exact shapes and forward locally;
            // no extra Rigidbody or per-frame overlap scan is needed.
            foreach (var shape in GetComponentsInChildren<Collider>(true))
            {
                if (shape.gameObject == gameObject || shape.GetComponentInParent<NeutralSpike>() != this) continue;
                var relay = shape.GetComponent<SpikeContactRelay>();
                if (relay == null) relay = shape.gameObject.AddComponent<SpikeContactRelay>();
                relay.Bind(this);
            }
        }

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);
        private void OnCollisionEnter(Collision collision) => Hit(collision.collider);
        private void OnCollisionStay(Collision collision) => Hit(collision.collider);

        internal void Hit(Collider other) => Hit(other, Vector2Int.zero);
        internal void Hit(Collider other, Vector2Int cell)
        {
            if (!isActiveAndEnabled || other == null) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && !player.IsDead && !player.IsInvulnerable)
            {
                player.EnterDeath(PlayerDeathCause.Spike, this);
                if (!initiallyBloody && bloodyCells.Add(cell))
                    artworkRevision++;
            }
        }

        /// <summary>One batched mesh, with independent UV per lethal cell; no renderer per tooth.</summary>
        public void RenderArtwork(SpriteRenderer source, Sprite normal, Sprite bloody, float cellWorldSize, Material meshMaterial)
        {
            if (source == null || normal == null) return;
            bool visible = source.enabled;
            source.enabled = false;
            if (artworkRenderer == null)
            {
                var child = new GameObject("__SpikeArtwork") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(transform, false);
                child.AddComponent<MeshFilter>(); artworkRenderer = child.AddComponent<MeshRenderer>();
                artworkRenderer.shadowCastingMode = ShadowCastingMode.Off; artworkRenderer.receiveShadows = false;
            }
            artworkRenderer.enabled = visible && isActiveAndEnabled;
            artworkRenderer.sharedMaterial = meshMaterial != null ? meshMaterial : source.sharedMaterial;
            artworkRenderer.gameObject.layer = source.gameObject.layer;
            artworkRenderer.sortingLayerID = source.sortingLayerID; artworkRenderer.sortingOrder = source.sortingOrder;
            var shape = GetComponent<LevelEditorMergedShape>();
            var placed = GetComponent<LevelEditorPlacedBlock>();
            Vector2Int dimensions = shape != null ? shape.Dimensions : placed != null ? placed.SizeCells : Vector2Int.one;
            dimensions = Vector2Int.Max(Vector2Int.one, dimensions);
            Vector3 scale = transform.lossyScale;
            if (renderedRevision == artworkRevision && renderedSprite == normal && renderedScale == scale && renderedDimensions == dimensions && renderedCellWorldSize == cellWorldSize) return;
            renderedRevision = artworkRevision; renderedSprite = normal; renderedScale = scale; renderedDimensions = dimensions; renderedCellWorldSize = cellWorldSize;
            artworkProperties ??= new MaterialPropertyBlock();
            artworkProperties.SetTexture("_BaseMap", normal.texture); artworkProperties.SetTexture("_MainTex", normal.texture);
            artworkProperties.SetColor("_BaseColor", Color.white); artworkProperties.SetColor("_Color", Color.white);
            artworkRenderer.SetPropertyBlock(artworkProperties);
            if (artworkMesh != null) Destroy(artworkMesh);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var rotation = Quaternion.Euler(0, 0, ArtworkRotationDegrees);
            var cells = new HashSet<Vector2Int>();
            if (shape != null) foreach (var rect in shape.Rectangles) foreach (var cell in rect.allPositionsWithin) cells.Add(cell);
            else foreach (var cell in new RectInt(Vector2Int.zero, dimensions).allPositionsWithin) cells.Add(cell);
            foreach (var cell in cells)
            {
                Sprite sprite = IsCellBloody(cell) && bloody != null ? bloody : normal;
                Rect rect = sprite.textureRect;
                Vector2 low = new Vector2(rect.xMin / sprite.texture.width, rect.yMin / sprite.texture.height);
                Vector2 high = new Vector2(rect.xMax / sprite.texture.width, rect.yMax / sprite.texture.height);
                Vector3 center = shape != null ? shape.RectBounds(new RectInt(cell, Vector2Int.one)).center
                    : new Vector3((cell.x + .5f) / dimensions.x - .5f, .5f - (cell.y + .5f) / dimensions.y, 0f);
                Vector2 natural = GridArtworkMetrics.SpriteCells(sprite);
                float width = natural.x, height = natural.y;
                int start = vertices.Count;
                foreach (var point in new[] { new Vector3(-width * .5f, -.5f, 0), new Vector3(width * .5f, -.5f, 0),
                    new Vector3(width * .5f, height - .5f, 0), new Vector3(-width * .5f, height - .5f, 0) })
                {
                    Vector3 p = rotation * point * cellWorldSize;
                    vertices.Add(center + new Vector3(p.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                        p.y / Mathf.Max(.001f, Mathf.Abs(scale.y)), source.transform.localPosition.z - .001f));
                }
                uv.Add(low); uv.Add(new Vector2(high.x, low.y)); uv.Add(high); uv.Add(new Vector2(low.x, high.y));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
            artworkMesh = new Mesh { name = "Spike Cell Artwork", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            artworkMesh.SetVertices(vertices); artworkMesh.SetUVs(0, uv); artworkMesh.SetTriangles(triangles, 0); artworkMesh.RecalculateBounds();
            artworkRenderer.GetComponent<MeshFilter>().sharedMesh = artworkMesh;
        }

        private void OnDisable() { if (artworkRenderer != null) artworkRenderer.enabled = false; }
    }
}
