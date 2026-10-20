using UnityEngine;

namespace Project.ObjectArtwork
{
    /// <summary>Presentation only. A future Boss/ending component owns fountain gameplay.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class FountainArtwork : MonoBehaviour
    {
        [SerializeField] Sprite[] frames = new Sprite[0];
        [SerializeField, Min(.1f)] float framesPerSecond = 10;
        [SerializeField, Min(.01f)] float cellWorldSize = 1;
        [SerializeField] bool playing = true;
        SpriteRenderer spriteRenderer;
        double elapsed;
        int lastFrame = -1;
        public int FrameCount => frames == null ? 0 : frames.Length;
        public int CurrentFrame => Mathf.Max(0,lastFrame);
        public void SetPlaying(bool value) => playing = value;
        void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            elapsed = 0; lastFrame = -1; ApplyFrame(0);
        }
        void Update()
        {
            if (!Application.isPlaying || !playing || FrameCount == 0) return;
            elapsed += Time.deltaTime;
            ApplyFrame((int)(elapsed * framesPerSecond % FrameCount));
        }
        void OnValidate() { lastFrame = -1; if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>(); ApplyFrame(0); }
        void ApplyFrame(int index)
        {
            if (!spriteRenderer || FrameCount == 0 || index == lastFrame) return;
            var sprite = frames[index]; if (!sprite) return;
            spriteRenderer.sprite = sprite;
            // Sprite PPU is imported at 16. Parent scaling is compensated without changing the source frame.
            transform.localScale = GridArtworkMetrics.NaturalSpriteScale(sprite,
                transform.parent ? transform.parent.lossyScale : Vector3.one,cellWorldSize);
            lastFrame = index;
        }
#if UNITY_EDITOR
        public void EditorConfigure(Sprite[] value, Material material)
        { frames = value; spriteRenderer = GetComponent<SpriteRenderer>(); spriteRenderer.sharedMaterial = material; lastFrame = -1; ApplyFrame(0); }
#endif
    }
}
