using Project.BlockFeatures;
using Project.ObjectArtwork;
using UnityEngine;

namespace Project.Subtitles
{
    /// <summary>Owns only the sign artwork; subtitle triggering and trigger bounds stay independent.</summary>
    internal sealed class SubtitleSignVisual
    {
        private readonly Transform owner;
        private readonly Renderer[] placeholders;
        private readonly bool[] previousForcedHidden;
        private SpriteRenderer renderer;
        private ObjectArtworkLibrary artwork;
        public SpriteRenderer Renderer => renderer;

        public SubtitleSignVisual(Transform owner)
        {
            this.owner = owner;
            placeholders = owner.GetComponentsInChildren<Renderer>(true);
            previousForcedHidden = new bool[placeholders.Length];
            for (int i = 0; i < placeholders.Length; i++)
                previousForcedHidden[i] = placeholders[i].forceRenderingOff;
        }

        public void Refresh(bool show, Sprite selected)
        {
            artwork = artwork != null ? artwork : ObjectArtworkLibrary.Load();
            Sprite sprite = selected;
            if (sprite == null && artwork != null && artwork.signboards != null && artwork.signboards.Length > 0)
                sprite = artwork.signboards[0];
            bool visible = show && sprite != null;
            if (visible && renderer == null)
            {
                var visual = new GameObject("Subtitle Sign Visual") { hideFlags = HideFlags.HideAndDontSave };
                visual.transform.SetParent(owner, false);
                visual.layer = owner.gameObject.layer;
                renderer = visual.AddComponent<SpriteRenderer>();
            }
            for (int i = 0; i < placeholders.Length; i++)
                if (placeholders[i] != null)
                    placeholders[i].forceRenderingOff = visible || previousForcedHidden[i];
            if (renderer == null) return;
            renderer.enabled = visible;
            if (!visible) return;
            renderer.sprite = sprite;
            if (artwork != null && artwork.neutralSpriteMaterial != null)
                renderer.sharedMaterial = artwork.neutralSpriteMaterial;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.transform.localRotation = Quaternion.identity;
            // A sign is one authored image, not a texture stretched to its trigger volume.
            renderer.transform.localScale = GridArtworkMetrics.NaturalSpriteScale(
                sprite, owner.lossyScale, GridCellSizeUtility.Resolve(owner));
            renderer.transform.localPosition = new Vector3(0f, 0f, -.51f);
        }

        public void Dispose()
        {
            for (int i = 0; i < placeholders.Length; i++)
                if (placeholders[i] != null)
                    placeholders[i].forceRenderingOff = previousForcedHidden[i];
            if (renderer == null) return;
            renderer.enabled = false;
            if (Application.isPlaying) Object.Destroy(renderer.gameObject);
            else Object.DestroyImmediate(renderer.gameObject);
            renderer = null;
        }
    }
}
