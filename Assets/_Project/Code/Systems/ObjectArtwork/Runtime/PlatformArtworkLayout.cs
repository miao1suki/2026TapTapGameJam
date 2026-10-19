using System.Collections.Generic;
using UnityEngine;

namespace Project.ObjectArtwork
{
    /// <summary>Native pixel clipping for legacy short platforms; never changes their logical footprint.</summary>
    public static class PlatformArtworkLayout
    {
        private static readonly Dictionary<(int, int), Sprite> croppedFrames = new();

        public static Rect SliceUV(Sprite sprite, float lengthCells)
        {
            if (sprite == null) return default;
            Rect rect = CropRect(sprite, lengthCells);
            return new Rect(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
                rect.width / sprite.texture.width, rect.height / sprite.texture.height);
        }

        private static Rect CropRect(Sprite sprite, float lengthCells)
        {
            Rect rect = sprite.textureRect;
            float width = Mathf.Min(rect.width, Mathf.Max(1, Mathf.Round(lengthCells * GridArtworkMetrics.PixelsPerCell)));
            rect.x += (rect.width - width) * .5f;
            rect.width = width;
            return rect;
        }

        public static Sprite CroppedFrame(Sprite source, float lengthCells)
        {
            if (source == null) return null;
            Rect rect = CropRect(source, lengthCells);
            if (rect.width >= source.textureRect.width) return source;
            var key = (source.GetInstanceID(), Mathf.RoundToInt(rect.width));
            if (croppedFrames.TryGetValue(key, out var cached) && cached != null) return cached;
            Vector2 pivot = new Vector2(.5f, source.pivot.y / source.rect.height);
            var sprite = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = source.name + " native clip " + rect.width;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            croppedFrames[key] = sprite;
            return sprite;
        }

        // This is the support edge measured from the complete frame's bottom,
        // not the opaque strip height. New animations keep two transparent rows
        // above and below their 12-pixel strip; aligning by strip height leaves a gap.
        public static float VisibleThicknessCells(MechanismArtworkKind kind, Sprite sprite, ObjectArtworkLibrary library = null)
        {
            float top = library != null ? library.Find(kind)?.supportTopPixels ?? 0 : 0;
            if (top > 0) return top / GridArtworkMetrics.PixelsPerCell;
            return ObjectArtworkLibrary.IsOneWayPlatform(kind) && sprite != null
                ? sprite.rect.height / GridArtworkMetrics.PixelsPerCell : 5f / 16f;
        }

        public static void ClearCache()
        {
            foreach (var sprite in croppedFrames.Values)
                if (sprite != null) { if (Application.isPlaying) Object.Destroy(sprite); else Object.DestroyImmediate(sprite); }
            croppedFrames.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => ClearCache();
    }
}
