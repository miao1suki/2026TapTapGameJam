using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.LevelEditor
{
    /// <summary>One gameplay owner with exact, compressed grid geometry, including holes.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LevelEditorMergedShape : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Vector2Int dimensions = Vector2Int.one;
        [SerializeField, HideInInspector] private List<RectInt> rectangles = new List<RectInt>();
        [SerializeField, HideInInspector] private Bounds sourceBounds = new Bounds(Vector3.zero, Vector3.one);
        [SerializeField, HideInInspector] private List<BoxCollider> volumes = new List<BoxCollider>();
        [SerializeField, HideInInspector] private MeshFilter meshFilter;
        private Mesh generatedMesh;
        private readonly List<SpriteRenderer> spriteParts = new List<SpriteRenderer>();
        private MaterialPropertyBlock spriteProperties;
        private HashSet<Vector2Int> occupiedCells;
        public IReadOnlyList<RectInt> Rectangles => rectangles;
        public Vector2Int Dimensions => dimensions;
        public IReadOnlyList<BoxCollider> Volumes => volumes;
        public Bounds LocalBounds => sourceBounds;
        public Bounds WorldBounds
        {
            get
            {
                Bounds result = new Bounds(transform.TransformPoint(sourceBounds.min), Vector3.zero);
                for (int i = 1; i < 8; i++) result.Encapsulate(transform.TransformPoint(new Vector3(
                    (i & 1) == 0 ? sourceBounds.min.x : sourceBounds.max.x,
                    (i & 2) == 0 ? sourceBounds.min.y : sourceBounds.max.y,
                    (i & 4) == 0 ? sourceBounds.min.z : sourceBounds.max.z)));
                return result;
            }
        }

        public void Configure(Vector2Int size, IReadOnlyList<RectInt> coverage)
        {
            dimensions = size;
            rectangles = new List<RectInt>(coverage);
            CacheCells();
            BoxCollider original = GetComponent<BoxCollider>();
            if (volumes.Count == 0 && original != null)
            {
                sourceBounds = new Bounds(original.center, original.size);
                volumes.Add(original);
            }
            if (original != null)
            {
                while (volumes.Count < rectangles.Count)
                {
                    var box = gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = original.isTrigger; box.sharedMaterial = original.sharedMaterial;
                    box.enabled = original.enabled; volumes.Add(box);
                }
                for (int i = 0; i < volumes.Count; i++)
                {
                    if (volumes[i] == null) continue;
                    volumes[i].enabled = i < rectangles.Count && original.enabled;
                    if (i >= rectangles.Count) continue;
                    Bounds local = RectBounds(rectangles[i]);
                    volumes[i].center = local.center; volumes[i].size = local.size;
                }
            }
            meshFilter = GetComponent<MeshFilter>();
            RebuildMesh();
        }

        public Bounds RectBounds(RectInt rect)
        {
            Vector3 min = sourceBounds.min, size = sourceBounds.size;
            return new Bounds(new Vector3(min.x + (rect.x + rect.width * .5f) / dimensions.x * size.x,
                sourceBounds.max.y - (rect.y + rect.height * .5f) / dimensions.y * size.y, sourceBounds.center.z),
                new Vector3(rect.width / (float)dimensions.x * size.x, rect.height / (float)dimensions.y * size.y, size.z));
        }
        public bool ContainsCell(Vector2Int topDownCell)
        {
            if (occupiedCells == null) CacheCells();
            return occupiedCells.Contains(topDownCell);
        }
        public bool ContainsWorldPoint(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            if (p.z < sourceBounds.min.z - .001f || p.z > sourceBounds.max.z + .001f) return false;
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt((p.x - sourceBounds.min.x) / sourceBounds.size.x * dimensions.x),
                Mathf.FloorToInt((sourceBounds.max.y - p.y) / sourceBounds.size.y * dimensions.y));
            return ContainsCell(cell);
        }

        // Sprite artwork remains a single logical object; only the visible rectangles are tiled.
        public void ApplySprite(SpriteRenderer source, bool topStrip)
        {
            while (spriteParts.Count < rectangles.Count)
            {
                string name = "__MergedShapeVisual_" + spriteParts.Count;
                var child = transform.Find(name);
                if (child == null)
                {
                    child = new GameObject(name) { hideFlags = HideFlags.DontSave }.transform;
                    child.SetParent(transform, false);
                }
                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
                spriteParts.Add(renderer);
            }
            spriteProperties ??= new MaterialPropertyBlock();
            source.GetPropertyBlock(spriteProperties);
            bool verticalStrip = topStrip && Mathf.Abs((source.transform.localRotation * Vector3.right).y) > .9f;
            for (int i = 0; i < spriteParts.Count; i++)
            {
                var part = spriteParts[i];
                part.enabled = source.enabled && i < rectangles.Count;
                if (i >= rectangles.Count) continue;
                part.sprite = source.sprite; part.sharedMaterial = source.sharedMaterial; part.color = source.color;
                part.flipX = source.flipX; part.flipY = source.flipY;
                part.gameObject.layer = source.gameObject.layer; part.sortingLayerID = source.sortingLayerID;
                part.sortingOrder = source.sortingOrder; part.SetPropertyBlock(spriteProperties);
                part.drawMode = SpriteDrawMode.Tiled; part.tileMode = SpriteTileMode.Continuous;
                part.transform.localScale = source.transform.localScale;
                part.transform.localRotation = source.transform.localRotation;
                Bounds b = RectBounds(rectangles[i]);
                Vector3 scale = source.transform.localScale;
                float visibleThickness = topStrip && source.sprite != null && source.sprite.rect.height == 16f ? 5f / 16f : source.size.y;
                if (verticalStrip)
                {
                    part.size = new Vector2(b.size.y / Mathf.Max(.0001f, Mathf.Abs(scale.x)), source.size.y);
                    part.transform.localPosition = new Vector3(b.min.x + visibleThickness * Mathf.Abs(scale.y), b.center.y, source.transform.localPosition.z);
                    continue;
                }
                float height = topStrip ? source.size.y : b.size.y / Mathf.Max(.0001f, Mathf.Abs(scale.y));
                part.size = new Vector2(b.size.x / Mathf.Max(.0001f, Mathf.Abs(scale.x)), height);
                part.transform.localPosition = new Vector3(b.center.x, topStrip ? b.max.y - visibleThickness * Mathf.Abs(scale.y) : b.min.y,
                    source.transform.localPosition.z);
            }
            source.enabled = false;
        }

        private void CacheCells()
        {
            occupiedCells = new HashSet<Vector2Int>();
            foreach (var rect in rectangles) foreach (Vector2Int cell in rect.allPositionsWithin) occupiedCells.Add(cell);
        }
        private void OnEnable() { CacheCells(); if (rectangles.Count > 0) RebuildMesh(); }
        private void OnDisable() { foreach (var part in spriteParts) if (part != null) part.enabled = false; }
        private void OnDestroy() => ReleaseMesh();
        private void ReleaseMesh()
        {
            if (generatedMesh == null) return;
            if (Application.isPlaying) Destroy(generatedMesh); else DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }
        private void RebuildMesh()
        {
            if (meshFilter == null || rectangles.Count == 0) return;
            ReleaseMesh();
            generatedMesh = CreateMesh(sourceBounds, dimensions, rectangles);
            meshFilter.sharedMesh = generatedMesh;
        }

        public static Mesh CreateMesh(Bounds bounds, Vector2Int size, IReadOnlyList<RectInt> coverage)
        {
            Bounds RectBounds(RectInt rect) => new Bounds(new Vector3(
                bounds.min.x + (rect.x + rect.width * .5f) / size.x * bounds.size.x,
                bounds.max.y - (rect.y + rect.height * .5f) / size.y * bounds.size.y, bounds.center.z),
                new Vector3(rect.width / (float)size.x * bounds.size.x, rect.height / (float)size.y * bounds.size.y, bounds.size.z));
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var cells = new HashSet<Vector2Int>();
            foreach (var rect in coverage) foreach (Vector2Int cell in rect.allPositionsWithin) cells.Add(cell);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            }
            foreach (var rect in coverage)
            {
                Bounds b = RectBounds(rect); Vector3 l = b.min, h = b.max;
                Face(new Vector3(l.x,l.y,l.z),new Vector3(l.x,h.y,l.z),new Vector3(h.x,h.y,l.z),new Vector3(h.x,l.y,l.z));
                Face(new Vector3(l.x,l.y,h.z),new Vector3(h.x,l.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(l.x,h.y,h.z));
            }
            foreach (Vector2Int cell in cells)
            {
                Bounds b = RectBounds(new RectInt(cell, Vector2Int.one)); Vector3 l = b.min, h = b.max;
                if (!cells.Contains(cell + Vector2Int.left)) Face(new Vector3(l.x,l.y,h.z),new Vector3(l.x,h.y,h.z),new Vector3(l.x,h.y,l.z),new Vector3(l.x,l.y,l.z));
                if (!cells.Contains(cell + Vector2Int.right)) Face(new Vector3(h.x,l.y,l.z),new Vector3(h.x,h.y,l.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,l.y,h.z));
                if (!cells.Contains(cell + Vector2Int.down)) Face(new Vector3(l.x,h.y,l.z),new Vector3(l.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,l.z));
                if (!cells.Contains(cell + Vector2Int.up)) Face(new Vector3(l.x,l.y,h.z),new Vector3(l.x,l.y,l.z),new Vector3(h.x,l.y,l.z),new Vector3(h.x,l.y,h.z));
            }
            var mesh = new Mesh { name = "Merged Grid Shape", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
