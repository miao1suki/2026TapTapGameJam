using System;
using System.Linq;
using Project.SurfaceTiles;
using UnityEditor;
using UnityEngine;

namespace Project.GridArt.Editor
{
    public static class GridArtLibraryService
    {
        public const string SharedPath = "Assets/_Project/Content/Art/SceneArtLibrary.asset";
        public static GridArtPalette Shared()
        {
            var library = AssetDatabase.LoadAssetAtPath<GridArtPalette>(SharedPath);
            if (library != null) return library;
            GridArtBuilder.EnsureFolder("Assets/_Project/Content/Art");
            library = ScriptableObject.CreateInstance<GridArtPalette>();
            AssetDatabase.CreateAsset(library, SharedPath);
            AddProjectTiles(library);
            return library;
        }

        public static string SpriteKey(Sprite sprite)
        {
            return sprite != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id)
                ? guid + ":" + id : null;
        }

        // Append-only: old maps store brush indices. Never reorder or delete occupied slots.
        public static bool Add(GridArtPalette library, Sprite sprite, Vector2 size, Rect crop, string category)
        {
            if (library == null || sprite == null || sprite.packed) return false;
            string key = SpriteKey(sprite);
            if (string.IsNullOrEmpty(key) || library.brushes.Any(b => b.sourceKey == key || b.sprite == sprite)) return false;
            library.brushes.Add(new GridArtBrush { sourceKey = key, label = sprite.name, sprite = sprite,
                size = new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(size.x)), Mathf.Max(1, Mathf.CeilToInt(size.y))),
                visualSize = size, contentRect = crop, category = category });
            EditorUtility.SetDirty(library);
            return true;
        }

        public static int Synchronize(GridArtPalette library)
        {
            if (Application.isPlaying || library == null) return 0;
            Undo.RecordObject(library, "汇入切片素材");
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SurfaceTilePalette", new[] { "Assets" }).OrderBy(g => g))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/Plugins/", StringComparison.Ordinal) || path.Contains("/Development/") || path.Contains("/__SceneArtTest_")) continue;
                var source = AssetDatabase.LoadAssetAtPath<SurfaceTilePalette>(path);
                count += ImportPalette(library, source);
            }
            AssetDatabase.SaveAssetIfDirty(library);
            return count;
        }

        private static int ImportPalette(GridArtPalette library, SurfaceTilePalette source)
        {
            int count=0;
            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            foreach (var tile in source.Tiles)
            {
                if (tile?.Sprite == null || tile.Sprite.packed) continue;
                string key=guid+":tile:"+tile.Id;
                var existing=library.brushes.FirstOrDefault(b=>b.sourceKey==key);
                if (existing != null)
                {
                    if (existing.sprite != tile.Sprite || existing.contentRect != tile.ContentRect)
                    { existing.sprite=tile.Sprite; existing.contentRect=tile.ContentRect; EditorUtility.SetDirty(library); }
                    continue;
                }
                // The slicer recreates Sprite subassets but preserves tile.Id. Maps keep this slot.
                var size=tile.SizeInCells;
                library.brushes.Add(new GridArtBrush { sourceKey=key, label=tile.Sprite.name, sprite=tile.Sprite,
                    size=new Vector2Int(Mathf.Max(1,Mathf.CeilToInt(size.x)),Mathf.Max(1,Mathf.CeilToInt(size.y))),
                    visualSize=size, contentRect=tile.ContentRect, category=source.name });
                EditorUtility.SetDirty(library); count++;
            }
            return count;
        }

        private static void AddProjectTiles(GridArtPalette library)
        {
            const string path = "Assets/_Project/Resources/26TAPTAP/场景背景/tail.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != 1408 || texture.height != 704) return;
            Sprite Crop(Rect rect, string name)
            {
                var sprite = Sprite.Create(texture, rect, Vector2.one * .5f, 64, 0, SpriteMeshType.FullRect);
                sprite.name = name; AssetDatabase.AddObjectToAsset(sprite, library); return sprite;
            }
            for (int row = 0; row < 5; row++) for (int column = 0; column < 4; column++)
            {
                string label = "墙面纹样 " + (row * 4 + column + 1).ToString("00");
                var sprite = Crop(new Rect(64 + column * 128, 704 - (64 + row * 128) - 64,64,64), label);
                Add(library, sprite, Vector2.one, new Rect(0,0,1,1), "场景纹样");
            }
            var frame = new GridArtBrush { label = "白线边框", category = "边框", autoTerrain = true,
                sourceKey = AssetDatabase.AssetPathToGUID(path) + "#frame" };
            for (int row=0;row<3;row++) for (int column=0;column<3;column++)
            {
                int i=row*3+column;
                frame.pieces[i] = Crop(i==4 ? new Rect(640,351,1,1) : new Rect(640+column*128,704-(320+row*128)-64,64,64),"边框_"+i);
            }
            library.brushes.Add(frame); EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
        }

        public static void Connect(GridArtMap map, GridArtPalette destination)
        {
            if (map == null || destination == null || map.palette == destination || Application.isPlaying) return;
            var source = map.palette;
            if (source != null && map.layers.Any(l => l.cells.Any(c => source.Get(c.brush) == null)))
                throw new InvalidOperationException("美术图存在失效的素材编号，请修复原库后迁移。");
            Undo.RecordObjects(new UnityEngine.Object[] { map, destination }, "接入共享素材库");
            var remap = new System.Collections.Generic.Dictionary<int, int>();
            if (source != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                for (int i = 0; i < source.brushes.Count; i++)
                {
                    var brush = source.brushes[i];
                    string key = guid + "#brush:" + i;
                    int index = destination.brushes.FindIndex(b => b.sourceKey == key);
                    if (index < 0)
                    {
                        var copy = new GridArtBrush { sourceKey = key, label = brush.label, category = brush.category,
                            autoTerrain = brush.autoTerrain, sprite = brush.sprite, size = brush.size,
                            visualSize = brush.visualSize, contentRect = brush.contentRect, normalMap = brush.normalMap,
                            pieces = brush.pieces != null ? (Sprite[])brush.pieces.Clone() : new Sprite[13] };
                        index = destination.brushes.Count; destination.brushes.Add(copy);
                    }
                    remap[i] = index;
                }
                foreach (var layer in map.layers)
                {
                    foreach (var cell in layer.cells) cell.brush = remap[cell.brush];
                    if (layer.followLevel) layer.sourceBrush = remap[layer.sourceBrush];
                }
            }
            map.palette = destination; map.revision++;
            EditorUtility.SetDirty(map); EditorUtility.SetDirty(destination);
            AssetDatabase.SaveAssetIfDirty(destination); AssetDatabase.SaveAssetIfDirty(map);
        }

        public static int Import(GridArtPalette library, UnityEngine.Object source, int pixelsPerCell)
        {
            if (Application.isPlaying || library == null || source == null) return 0;
            Undo.RecordObject(library, "添加美术素材");
            if (source is SurfaceTilePalette tiles)
            { int added=ImportPalette(library,tiles); AssetDatabase.SaveAssetIfDirty(library); return added; }
            var sprites = source is Sprite sprite ? new[] { sprite } :
                AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(source)).OfType<Sprite>().ToArray();
            int count = 0;
            foreach (var item in sprites)
                if (Add(library, item, item.rect.size / Mathf.Max(1, pixelsPerCell), new Rect(0, 0, 1, 1), "自定义")) count++;
            AssetDatabase.SaveAssetIfDirty(library);
            return count;
        }
    }
}
