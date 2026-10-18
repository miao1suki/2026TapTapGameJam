using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ObjectArtwork;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ContactCubes
{
    /// <summary>Visual-only TA water/bubble batches. Never creates a water volume or moves an actor.</summary>
    [DisallowMultipleComponent]
    public sealed class CubeWaterJetArtwork : MonoBehaviour
    {
        public const int MaximumBubbles = 96;
        private readonly List<Sprite> sprites = new List<Sprite>(6);
        private readonly List<Batch> bubbles = new List<Batch>(6);
        private Batch water;
        private float length, width, cell, density, speed;
        private int bubbleCount;
        private Sprite waterSprite;
        private float started;
        public int VisibleBubbleCount => bubbleCount;
        public float BubbleSpeedCells => speed;
        public int RendererCount => water != null ? bubbles.Count + 1 : 0;

        public void Configure(float distance, float diameter, float cellWorldSize, float rowsPerCell, float speedCells)
        {
            bool changed = water == null || length != distance || width != diameter || cell != cellWorldSize ||
                density != rowsPerCell || speed != speedCells;
            length = Mathf.Max(.01f,distance); width = Mathf.Max(.01f,diameter);
            cell = Mathf.Max(.01f,cellWorldSize); density = Mathf.Clamp(rowsPerCell,.2f,4f); speed = Mathf.Clamp(speedCells,.1f,12f);
            if (water == null && !Build()) return;
            bubbleCount = Mathf.Clamp(Mathf.CeilToInt(length / cell * density) * 2,2,MaximumBubbles);
            if (changed) Refresh(); // Pre-fill the entire jet on the contact frame, not after a particle lifetime.
        }

        private bool Build()
        {
            var settings = WaterPresentationSettings.Load();
            if (settings == null || !settings.IsValid) return false;
            waterSprite = settings.tiles[1];
            water = new Batch(transform,"TA jet water",waterSprite,settings.tileMaterials[1],-100);
            if (settings.bubblePrefab != null)
                foreach (var renderer in settings.bubblePrefab.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.sprite != null && !sprites.Contains(renderer.sprite)) sprites.Add(renderer.sprite);
            sprites.Sort((a,b) => a.rect.width != b.rect.width ? a.rect.width.CompareTo(b.rect.width) :
                string.CompareOrdinal(a.name,b.name));
            // Mesh-compatible formal water material, with per-batch TA bubble texture.
            // All instances still share the manager's one blue reveal material.
            foreach (Sprite sprite in sprites)
                bubbles.Add(new Batch(transform,"TA jet bubbles " + sprite.name,sprite,settings.tileMaterials[1],0));
            started = Time.time;
            return true;
        }

        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (water == null) return;
            Vector3 origin = transform.position;
            Vector3 direction = transform.right.normalized;
            Vector3 across = new Vector3(-direction.y,direction.x,0f).normalized;
            water.Clear();
            Rect source = waterSprite.textureRect;
            float sourceBottom = source.yMin + 1f; // Formal TA water is 16x17; omit its transparent padding row.
            for (float x = 0f; x < length - .00001f; x += cell)
                for (float y = -width * .5f; y < width * .5f - .00001f; y += cell)
                {
                    float segmentLength = Mathf.Min(cell,length - x), segmentWidth = Mathf.Min(cell,width * .5f - y);
                    float uvBottom = sourceBottom + (cell - segmentWidth) / cell * 8f;
                    var uv = new Rect(source.xMin / waterSprite.texture.width,uvBottom / waterSprite.texture.height,
                        source.width / waterSprite.texture.width * segmentLength / cell,
                        16f / waterSprite.texture.height * segmentWidth / cell);
                    Vector3 a = origin + direction * x + across * y;
                    a.z = origin.z + cell * .52f; // Behind the player and bubbles, matching formal water ordering.
                    water.Quad(a,a + direction * segmentLength,a + direction * segmentLength + across * segmentWidth,
                        a + across * segmentWidth,uv,new Color32(255,255,255,155));
                }
            water.Commit();
            foreach (Batch batch in bubbles) batch.Clear();
            if (sprites.Count > 0)
                for (int i = 0; i < bubbleCount; i++)
                {
                    // Stable, pre-filled spatial phases. No Random calls, Instantiate, material
                    // copies or per-particle objects in the render loop.
                    float seed = Mathf.Repeat(i * .61803398875f,1f);
                    float progress = Mathf.Repeat((i + .5f) / bubbleCount +
                        (Time.time - started) * speed * cell / length * Mathf.Lerp(.88f,1.12f,seed),1f);
                    int frame = Mathf.Min(sprites.Count - 1,Mathf.FloorToInt(progress * sprites.Count));
                    Sprite sprite = sprites[frame];
                    Vector2 size = GridArtworkMetrics.SpriteCells(sprite,GridArtworkMetrics.SourcePixelScale(sprite)) * cell;
                    float lateral = ((i & 1) == 0 ? -.22f : .22f) * width + (seed - .5f) * width * .12f;
                    Vector3 center = origin + direction * (progress * length) + across * lateral;
                    // Keep sprites upright and preserve every original effective pixel. Odd/even
                    // frame dimensions place their edges (not arbitrary scaled centers) on the grid.
                    float pixel = GridArtworkMetrics.PixelWorldSize(cell);
                    float left = Mathf.Round((center.x - size.x * .5f) / pixel) * pixel;
                    float bottom = Mathf.Round((center.y - size.y * .5f) / pixel) * pixel;
                    float z = origin.z - cell * .05f;
                    Rect rect = sprite.textureRect;
                    var uv = new Rect(rect.x / sprite.texture.width,rect.y / sprite.texture.height,
                        rect.width / sprite.texture.width,rect.height / sprite.texture.height);
                    float fade = Mathf.Min(1f,Mathf.Min(progress * 16f,(1f-progress) * 12f));
                    bubbles[frame].Quad(new Vector3(left,bottom,z),new Vector3(left+size.x,bottom,z),
                        new Vector3(left+size.x,bottom+size.y,z),new Vector3(left,bottom+size.y,z),uv,
                        new Color32(255,255,255,(byte)Mathf.RoundToInt(235f*fade)));
                }
            foreach (Batch batch in bubbles) batch.Commit();
        }

        private void OnDestroy()
        {
            water?.Dispose();
            foreach (Batch batch in bubbles) batch.Dispose();
        }

        private sealed class Batch
        {
            private readonly Transform owner;
            private readonly Mesh mesh;
            private readonly MeshRenderer renderer;
            private readonly List<Vector3> vertices = new List<Vector3>(MaximumBubbles * 4);
            private readonly List<Vector2> uvs = new List<Vector2>(MaximumBubbles * 4);
            private readonly List<Color32> colors = new List<Color32>(MaximumBubbles * 4);
            private readonly List<int> triangles = new List<int>(MaximumBubbles * 6);
            public Batch(Transform parent,string name,Sprite sprite,Material source,int order)
            {
                var part = new GameObject(name); part.transform.SetParent(parent,false); owner = part.transform;
                mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave }; mesh.MarkDynamic();
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ColorAppearanceManager.Instance.GetSharedMaterial(source,"blue",true);
                renderer.sortingOrder = order; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                var definition = ColorRuntimeService.Instance.Catalog?.Find("blue");
                if (definition != null) part.layer = definition.unityLayer;
                var properties = new MaterialPropertyBlock();
                properties.SetTexture("_MainTex",sprite.texture);
                properties.SetTexture("_ColorMaskAlphaTexture",sprite.texture);
                properties.SetFloat("_ColorMaskGroup",ColorAppearanceManager.GetGroupIndex("blue"));
                properties.SetFloat("_ColorMaskReal",1f); properties.SetFloat("_ColorMaskUseVertexAlpha",1f);
                renderer.SetPropertyBlock(properties);
            }
            public void Clear() { vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear(); }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Rect uv,Color32 color)
            {
                int start = vertices.Count;
                vertices.Add(owner.InverseTransformPoint(a)); vertices.Add(owner.InverseTransformPoint(b));
                vertices.Add(owner.InverseTransformPoint(c)); vertices.Add(owner.InverseTransformPoint(d));
                uvs.Add(new Vector2(uv.xMin,uv.yMin)); uvs.Add(new Vector2(uv.xMax,uv.yMin));
                uvs.Add(new Vector2(uv.xMax,uv.yMax)); uvs.Add(new Vector2(uv.xMin,uv.yMax));
                for (int i=0;i<4;i++) colors.Add(color);
                triangles.Add(start); triangles.Add(start+2); triangles.Add(start+1);
                triangles.Add(start); triangles.Add(start+3); triangles.Add(start+2);
            }
            public void Commit()
            {
                mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.SetColors(colors);
                mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); renderer.enabled = vertices.Count > 0;
            }
            public void Dispose() { if (mesh != null) Object.Destroy(mesh); }
        }
    }
}
