using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.SurfaceTiles
{
    [CreateAssetMenu(
        fileName = "SurfaceTilePalette",
        menuName = "2026TapTap/Surface Tiles/Tile Palette")]
    public sealed class SurfaceTilePalette : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField, HideInInspector] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private Sprite sprite;
            [SerializeField] private Vector2 sizeInCells = Vector2.one;
            [SerializeField] private Rect contentRect = new Rect(0f, 0f, 1f, 1f);

            public string Id => id;
            public string DisplayName => displayName;
            public Sprite Sprite => sprite;
            public Vector2 SizeInCells => new Vector2(
                sizeInCells.x > 0f ? sizeInCells.x : 1f,
                sizeInCells.y > 0f ? sizeInCells.y : 1f);
            public Rect ContentRect => contentRect.width > 0f && contentRect.height > 0f
                ? contentRect
                : new Rect(0f, 0f, 1f, 1f);
            public Vector2Int FootprintCells => new Vector2Int(
                Mathf.Max(1, Mathf.CeilToInt(SizeInCells.x - 0.0001f)),
                Mathf.Max(1, Mathf.CeilToInt(SizeInCells.y - 0.0001f)));

            public Entry(Sprite value)
            {
                id = Guid.NewGuid().ToString("N");
                displayName = value != null ? value.name : "Tile";
                sprite = value;
                sizeInCells = Vector2.one;
                contentRect = new Rect(0f, 0f, 1f, 1f);
            }

            internal void EnsureValid()
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = Guid.NewGuid().ToString("N");
                }

                if (string.IsNullOrWhiteSpace(displayName) && sprite != null)
                {
                    displayName = sprite.name;
                }

                if (sizeInCells.x <= 0f || sizeInCells.y <= 0f)
                {
                    sizeInCells = Vector2.one;
                }

                if (contentRect.width <= 0f || contentRect.height <= 0f)
                {
                    contentRect = new Rect(0f, 0f, 1f, 1f);
                }
            }

            internal void UpdateSprite(Sprite value)
            {
                sprite = value;
                if (value != null)
                {
                    displayName = value.name;
                }

                EnsureValid();
            }

            internal void ConfigureLayout(Vector2 valueSizeInCells, Rect valueContentRect)
            {
                sizeInCells = new Vector2(
                    Mathf.Max(0.01f, valueSizeInCells.x),
                    Mathf.Max(0.01f, valueSizeInCells.y));
                float x = Mathf.Clamp(valueContentRect.x, 0f, 0.9999f);
                float y = Mathf.Clamp(valueContentRect.y, 0f, 0.9999f);
                contentRect = new Rect(
                    x,
                    y,
                    Mathf.Clamp(valueContentRect.width, 0.0001f, 1f - x),
                    Mathf.Clamp(valueContentRect.height, 0.0001f, 1f - y));
            }
        }

        [SerializeField] private List<Entry> tiles = new List<Entry>();
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Vector2Int bakePixelsPerCell =
            new Vector2Int(128, 128);

        public IReadOnlyList<Entry> Tiles => tiles;
        public Material PreviewMaterial => previewMaterial;
        public Vector2Int BakePixelsPerCell => new Vector2Int(
            bakePixelsPerCell.x >= 8 ? bakePixelsPerCell.x : 128,
            bakePixelsPerCell.y >= 8 ? bakePixelsPerCell.y : 128);

        public bool TryGet(string id, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int index = 0; index < tiles.Count; index++)
            {
                Entry candidate = tiles[index];
                if (candidate != null && candidate.Id == id)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public Texture2D GetSourceTexture()
        {
            for (int index = 0; index < tiles.Count; index++)
            {
                Sprite sprite = tiles[index]?.Sprite;
                if (sprite != null)
                {
                    return sprite.texture;
                }
            }

            return null;
        }

        public bool UsesSingleTexture()
        {
            Texture2D texture = null;
            for (int index = 0; index < tiles.Count; index++)
            {
                Sprite sprite = tiles[index]?.Sprite;
                if (sprite == null)
                {
                    continue;
                }

                texture ??= sprite.texture;
                if (sprite.texture != texture)
                {
                    return false;
                }
            }

            return true;
        }

        public void ReplaceTiles(IEnumerable<Sprite> sprites)
        {
            tiles.Clear();
            if (sprites == null)
            {
                return;
            }

            foreach (Sprite sprite in sprites)
            {
                if (sprite != null)
                {
                    tiles.Add(new Entry(sprite));
                }
            }
        }

        public void ReplaceTilesPreservingIds(IEnumerable<Sprite> sprites)
        {
            Dictionary<string, Entry> existing = new Dictionary<string, Entry>(
                StringComparer.Ordinal);
            for (int index = 0; index < tiles.Count; index++)
            {
                Entry entry = tiles[index];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName))
                {
                    existing[entry.DisplayName] = entry;
                }
            }

            List<Entry> replacement = new List<Entry>();
            if (sprites != null)
            {
                foreach (Sprite sprite in sprites)
                {
                    if (sprite == null)
                    {
                        continue;
                    }

                    if (existing.TryGetValue(sprite.name, out Entry entry))
                    {
                        entry.UpdateSprite(sprite);
                        replacement.Add(entry);
                    }
                    else
                    {
                        replacement.Add(new Entry(sprite));
                    }
                }
            }

            tiles = replacement;
        }

        public void SetPreviewMaterial(Material material)
        {
            previewMaterial = material;
        }

        public bool ConfigureTileLayout(
            string displayName,
            Vector2 sizeInCells,
            Rect contentRect)
        {
            for (int index = 0; index < tiles.Count; index++)
            {
                Entry entry = tiles[index];
                if (entry != null && entry.DisplayName == displayName)
                {
                    entry.ConfigureLayout(sizeInCells, contentRect);
                    return true;
                }
            }

            return false;
        }

        public void SetBakePixelsPerCell(Vector2Int value)
        {
            bakePixelsPerCell = new Vector2Int(
                Mathf.Max(8, value.x),
                Mathf.Max(8, value.y));
        }

        private void OnValidate()
        {
            bakePixelsPerCell = BakePixelsPerCell;
            for (int index = 0; index < tiles.Count; index++)
            {
                tiles[index]?.EnsureValid();
            }
        }
    }
}
