using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    /// <summary>Explicit selections supplied by the artist; one source pixel stays one art pixel.</summary>
    public static class GridArtAuthorBorderLibrary
    {
        public const string SourcePath = "Assets/_Project/Resources/26TAPTAP/场景背景/297124ef87a31676fc7d123431cbcbaa.png";
        public const string SupplementPath = "Assets/_Project/Resources/26TAPTAP/场景背景/2f891a031596a583f63dfa7be93f1a4c.png";
        // Source coordinates use the image's top-left origin. No rotation, resampling or generated elbows.
        public static readonly Vector2Int[] WhiteOrigins = {
            new(96,142), new(64,174), new(32,142),
            new(128,110), new(64,110), new(0,110),
            new(96,78), new(64,46), new(32,78),
            new(96,46), new(32,46), new(128,142), new(0,142)
        };
        private static readonly string[] Names = {
            "外角·左上", "直边·上", "外角·右上", "直边·左", "中心", "直边·右",
            "外角·左下", "直边·下", "外角·右下", "内角·左下", "内角·右下", "内角·左上", "内角·右上"
        };

        public static bool Ensure(GridArtPalette palette, GridArtBrush automatic)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (texture == null) return false;
            if (texture.width != 256 || texture.height != 190)
                throw new InvalidOperationException("新内外角原图尺寸变化，请重新核对切片，不能自动缩放。");
            string path = AssetDatabase.GetAssetPath(palette);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("请先保存素材库。");
            string guid = AssetDatabase.AssetPathToGUID(SourcePath);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            bool changed = false;
            Sprite Slice(string name, Vector2Int origin, Texture2D source = null)
            {
                source = source == null ? texture : source;
                Rect rect = new(origin.x, source.height - origin.y - 16, 16, 16);
                var sprite = assets.OfType<Sprite>().FirstOrDefault(s => s.name == name && s.texture == source && s.rect == rect);
                if (sprite != null) return sprite;
                sprite = Sprite.Create(source, rect, Vector2.one * .5f, 16, 0, SpriteMeshType.FullRect);
                sprite.name = name; AssetDatabase.AddObjectToAsset(sprite, palette); changed = true;
                return sprite;
            }
            void Add(string key, string label, string category, Sprite sprite, string color = "")
            {
                var brush = palette.brushes.FirstOrDefault(b => b.sourceKey == key);
                if (brush != null)
                {
                    if (brush.sprite != sprite) { brush.sprite = sprite; changed = true; }
                    return;
                }
                palette.brushes.Add(new GridArtBrush { sourceKey = key, label = label, category = category,
                    sprite = sprite, size = Vector2Int.one, visualSize = Vector2.one, sourcePixelScale = 1, colorId = color });
                changed = true;
            }
            if (automatic.pieces == null || automatic.pieces.Length != 13)
            { Array.Resize(ref automatic.pieces, 13); changed = true; }
            for (int i = 0; i < 13; i++)
            {
                var sprite = Slice("ArtistWhiteBorder_" + i, WhiteOrigins[i]);
                if (automatic.pieces[i] != sprite) { automatic.pieces[i] = sprite; changed = true; }
                if (i != 4) Add(guid + "#white:" + i, "白线边框·" + Names[i], "边框·美术内外角", sprite);
            }
            if (automatic.sourcePixelScale != 1) { automatic.sourcePixelScale = 1; changed = true; }
            string[] directions = { "左上", "上", "右上", "左", "中", "右", "左下", "下", "右下" };
            for (int i = 0; i < 9; i++)
                if (i != 4) Add(guid + "#ornament:" + i, "纹饰边框·" + directions[i] + "（新图）", "边框·美术纹饰",
                    Slice("ArtistOrnamentBorder_" + i, new Vector2Int(176 + i % 3 * 32, 46 + i / 3 * 32)));
            string[] colors = { "red", "blue", "green" };
            string[] colorNames = { "红", "蓝", "绿" };
            for (int i = 0; i < 3; i++)
                Add(guid + "#grass:" + i, colorNames[i] + "色草地", "地面草饰·三色",
                    Slice("ArtistGrass_" + i, new Vector2Int(176 + i * 32, 174)), colors[i]);
            var supplement = AssetDatabase.LoadAssetAtPath<Texture2D>(SupplementPath);
            if (supplement != null)
            {
                if (supplement.width != 256 || supplement.height != 190)
                    throw new InvalidOperationException("补充图尺寸变化，请核对新增切片。");
                string supplementGuid = AssetDatabase.AssetPathToGUID(SupplementPath);
                Add(supplementGuid + "#center:white", "白线纹样·中心", "边框·美术纹饰",
                    Slice("ArtistSupplementWhiteCenter", new Vector2Int(64, 78), supplement));
                Add(supplementGuid + "#center:ornament", "纹饰边框·中心", "边框·美术纹饰",
                    Slice("ArtistSupplementOrnamentCenter", new Vector2Int(208, 78), supplement));
            }
            if (changed) EditorUtility.SetDirty(palette);
            return true;
        }
    }
}
