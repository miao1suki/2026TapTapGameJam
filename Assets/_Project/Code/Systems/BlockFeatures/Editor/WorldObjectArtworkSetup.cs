using System;
using System.IO;
using System.Linq;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    [InitializeOnLoad]
    public static class WorldObjectArtworkSetup
    {
        private const string Root = "Assets/_Project/Resources/ObjectArtwork";
        private const string Generated = "Assets/_Project/Art/Generated/ObjectArtwork";
        private const string Source = "Assets/_Project/Resources/26TAPTAP/物体道具/";
        static WorldObjectArtworkSetup()
        {
            EditorApplication.delayCall += RefreshOpenScenes;
            EditorSceneManager.sceneOpened += (_, _) => RefreshOpenScenes();
        }
        public static void RefreshOpenScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || ObjectArtworkLibrary.Load() == null) return;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                MechanismArtworkPresentation.RefreshScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
            foreach (var anchor in UnityEngine.Object.FindObjectsByType<RespawnAnchorFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (anchor.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(anchor)) WorldObjectSpritePresentation.Ensure(anchor.gameObject);
            foreach (var plant in UnityEngine.Object.FindObjectsByType<BouncePlantFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (plant.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(plant)) WorldObjectSpritePresentation.Ensure(plant.gameObject);
        }

        public static void BuildMechanisms()
        {
            Folder(Generated);
            var library = AssetDatabase.LoadAssetAtPath<ObjectArtworkLibrary>(Root + "/Objects.asset");
            if (library == null) throw new InvalidOperationException("缺少物体美术配置。");
            var entries = new System.Collections.Generic.List<MechanismArtworkEntry>();
            void Add(MechanismArtworkKind kind, string file, int width, int height, int count, int start = 0, int stride = 0, int top = 0, int cropHeight = 0, Vector4 border = default)
            {
                using (var image = new ImageScope(Read(Source + file)))
                {
                    var source = image.Texture;
                    int h = cropHeight > 0 ? cropHeight : height;
                    var output = new Texture2D(width * count, h, TextureFormat.RGBA32, false);
                    for (int i = 0; i < count; i++)
                    {
                        var pixels = source.GetPixels(start + i * (stride > 0 ? stride : width), source.height - top - h, width, h);
                        if (kind == MechanismArtworkKind.Door) ClearBlackBackground(pixels);
                        output.SetPixels(i * width, 0, width, h, pixels);
                    }
                    string path = Generated + "/" + kind + ".png";
                    File.WriteAllBytes(path, output.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(output);
                    AssetDatabase.ImportAsset(path);
                    entries.Add(new MechanismArtworkEntry { kind = kind, frames = SliceMechanism(path, width, h, border), frameSeconds = kind == MechanismArtworkKind.Crank ? .04f : .08f });
                }
            }
            Add(MechanismArtworkKind.Door, "门80ms每帧.png", 16, 32, 4, 64, 32);
            Add(MechanismArtworkKind.DoorButton, "按钮80ms每帧.png", 16, 16, 2);
            Add(MechanismArtworkKind.GreenDoorButton, "蓝绿按钮80ms每帧.png", 16, 16, 2);
            Add(MechanismArtworkKind.BlueDoorButton, "蓝绿按钮80ms每帧.png", 16, 16, 2, 32);
            SliceMechanism(Source + "蓝绿按钮80ms每帧.png", 16, 16, Vector4.zero);
            Add(MechanismArtworkKind.RedButton, "按钮80ms每帧.png", 16, 16, 2);
            Add(MechanismArtworkKind.Crank, "曲柄40ms每帧.png", 16, 16, 28);
            Add(MechanismArtworkKind.CrankPlatform, "移动平台80ms每帧.png", 48, 16, 6, top: 11, cropHeight: 5, border: new Vector4(8,0,8,0));
            Add(MechanismArtworkKind.MovingPlatform, "自动平台80ms每帧.png", 48, 16, 8, top: 11, cropHeight: 5, border: new Vector4(8,0,8,0));
            Add(MechanismArtworkKind.RedCube, "0-2~4.png", 16, 16, 1, 80, top: 64);
            Add(MechanismArtworkKind.GreenCube, "0-2~4.png", 16, 16, 1, 48, top: 64);
            Add(MechanismArtworkKind.BlueCube, "0-2~4.png", 16, 16, 1, 112, top: 64);
            Add(MechanismArtworkKind.RedKey, "0-2~4.png", 16, 16, 1, 384, top: 64);
            Add(MechanismArtworkKind.GreenKey, "0-2~4.png", 16, 16, 1, 352, top: 64);
            Add(MechanismArtworkKind.BlueKey, "0-2~4.png", 16, 16, 1, 416, top: 64);
            Add(MechanismArtworkKind.Lava, "0-5.png", 16, 16, 1, 104, top: 105);
            using (var image = new ImageScope(Read("Assets/_Project/Resources/26TAPTAP/场景背景/tail.png")))
                foreach (var kind in new[] { MechanismArtworkKind.LadderFront, MechanismArtworkKind.LadderSide })
                {
                    // Fourth-row H-shaped ladder. Side variant keeps the same rung art on its narrower authored volume.
                    var output = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                    for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                        output.SetPixel(x, y, image.Texture.GetPixel(448 + x * 4, image.Texture.height - 512 + y * 4));
                    var pixels = output.GetPixels();
                    ClearBlackBackground(pixels);
                    output.SetPixels(pixels);
                    string path = Generated + "/" + kind + ".png";
                    File.WriteAllBytes(path, output.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(output); AssetDatabase.ImportAsset(path);
                    entries.Add(new MechanismArtworkEntry { kind = kind, frames = SliceMechanism(path, 16, 16, Vector4.zero) });
                }
            using (var image = new ImageScope(Read(Source + "0-2~4.png")))
            {
                var output = new Texture2D(48, 8, TextureFormat.RGBA32, false);
                for (int i = 0; i < 3; i++) output.SetPixels(i * 16, 0, 16, 8, image.Texture.GetPixels(256 + i * 32, 88, 16, 8));
                string path = Generated + "/OneWayPlatform.png";
                File.WriteAllBytes(path, output.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(output); AssetDatabase.ImportAsset(path);
                entries.Add(new MechanismArtworkEntry { kind = MechanismArtworkKind.OneWayPlatform, frames = SliceMechanism(path, 48, 8, new Vector4(8,0,8,0)) });
            }
            using (var image = new ImageScope(Read(Source + "门80ms每帧.png")))
            {
                var output = new Texture2D(48, 32, TextureFormat.RGBA32, false);
                output.SetPixels32(new Color32[48 * 32]);
                for (int i = 0; i < 3; i++)
                {
                    // Binding order is red, green, blue, independent of the symbols' vertical locations.
                    for (int y = 0; y < 32; y++) for (int x = 0; x < 16; x++)
                    {
                        Color pixel = image.Texture.GetPixel(x, y);
                        bool belongs = i == 0 ? pixel.r > pixel.g && pixel.r > pixel.b
                            : i == 1 ? pixel.g > pixel.r && pixel.g > pixel.b : pixel.b > pixel.r && pixel.b > pixel.g;
                        if (belongs) output.SetPixel(i * 16 + x, y, pixel);
                    }
                }
                string path = Generated + "/Door Indicators.png";
                File.WriteAllBytes(path, output.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(output); AssetDatabase.ImportAsset(path);
                library.doorIndicators = SliceMechanism(path, 16, 32, Vector4.zero);
                var white = new Texture2D(48, 32, TextureFormat.RGBA32, false);
                var closed = new Texture2D(16, 32, TextureFormat.RGBA32, false);
                var closedPixels = image.Texture.GetPixels(64, 0, 16, 32);
                ClearBlackBackground(closedPixels);
                white.SetPixels32(new Color32[48 * 32]);
                for (int y = 0; y < 32; y++) for (int x = 0; x < 16; x++)
                {
                    Color pixel = image.Texture.GetPixel(x, y);
                    int channel = pixel.r > pixel.g && pixel.r > pixel.b ? 0
                        : pixel.g > pixel.r && pixel.g > pixel.b ? 1
                        : pixel.b > pixel.r && pixel.b > pixel.g ? 2 : -1;
                    if (pixel.a <= 0 || channel < 0) continue;
                    white.SetPixel(channel * 16 + x, y, new Color(1f, 1f, 1f, pixel.a));
                    closedPixels[y * 16 + x] = Color.clear;
                }
                closed.SetPixels(closedPixels);
                string whitePath = Generated + "/Door White Indicators.png", closedPath = Generated + "/Door Closed.png";
                File.WriteAllBytes(whitePath, white.EncodeToPNG()); File.WriteAllBytes(closedPath, closed.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(white); UnityEngine.Object.DestroyImmediate(closed);
                AssetDatabase.ImportAsset(whitePath); AssetDatabase.ImportAsset(closedPath);
                library.doorWhiteIndicators = SliceMechanism(whitePath, 16, 32, Vector4.zero);
                library.doorClosed = SliceMechanism(closedPath, 16, 32, Vector4.zero)[0];
            }
            library.mechanisms = entries.ToArray(); EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            foreach (string folder in new[] { "Assets/_Project/Content/Mechanisms", "Assets/_Project/Content/ContactCubes", "Assets/_Project/Content/ColorBlocks/Prefabs" })
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!MechanismArtworkPresentation.Supports(asset)) continue;
                    var owner = PrefabUtility.LoadPrefabContents(path);
                    try { MechanismArtworkPresentation.Ensure(owner); PrefabUtility.SaveAsPrefabAsset(owner, path); }
                    finally { PrefabUtility.UnloadPrefabContents(owner); }
                }
            RefreshOpenScenes();
        }
        private sealed class ImageScope : IDisposable
        {
            public readonly Texture2D Texture;
            public ImageScope(Texture2D texture) => Texture = texture;
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Texture);
        }
        public static void ClearBlackBackground(Color[] pixels)
        {
            // These monochrome source sheets encode empty space as opaque black,
            // including enclosed ladder gaps and the interior of the door frame.
            for (int index = 0; index < pixels.Length; index++)
                if (pixels[index].r <= .025f && pixels[index].g <= .025f && pixels[index].b <= .025f)
                    pixels[index] = Color.clear;
        }
        private static Sprite[] SliceMechanism(string path, int width, int height, Vector4 border)
        {
            Slice(path, width, height, "Frame");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spritePixelsPerUnit = 16;
            #pragma warning disable CS0618
            var entries = importer.spritesheet;
            for (int i = 0; i < entries.Length; i++) { var entry = entries[i]; entry.border = border; entries[i] = entry; }
            importer.spritesheet = entries;
            #pragma warning restore CS0618
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        }
        public static void Build()
        {
            Folder(Root); Folder(Generated);
            Texture2D states = Read(Source + "Image_765781691944282.png");
            var output = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            output.SetPixels(0, 0, 32, 32, states.GetPixels(2, 0, 32, 32));
            output.SetPixels(32, 0, 32, 32, states.GetPixels(68, 0, 32, 32));
            string statePath = Generated + "/Respawn States.png";
            File.WriteAllBytes(statePath, output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(states); UnityEngine.Object.DestroyImmediate(output);
            File.Copy(Source + "Image_765776490195273.png", Generated + "/Respawn Current.png", true);
            File.Copy(Source + "弹簧80ms每帧.png", Generated + "/Bounce Plant.png", true);
            AssetDatabase.Refresh();
            var stills = Slice(statePath, 32, 32, "Respawn");
            var current = Slice(Generated + "/Respawn Current.png", 32, 32, "Current");
            var bounce = Slice(Generated + "/Bounce Plant.png", 16, 16, "Bounce");
            var library = AssetDatabase.LoadAssetAtPath<ObjectArtworkLibrary>(Root + "/Objects.asset");
            if (library == null) { library = ScriptableObject.CreateInstance<ObjectArtworkLibrary>(); AssetDatabase.CreateAsset(library, Root + "/Objects.asset"); }
            library.respawnLocked = stills[0]; library.respawnUsed = stills[1]; library.respawnCurrent = current;
            library.plantBounce = bounce; library.plantIdleFrame = 5;
            library.respawnFrameSeconds = .08f; library.plantFrameSeconds = .08f;
            Texture2D spring = Read(Source + "弹簧80ms每帧.png");
            int idleTop = 0;
            for (int y = 0; y < 16; y++) for (int x = 80; x < 96; x++)
                if (spring.GetPixel(x, y).a > .001f) idleTop = Mathf.Max(idleTop, y + 1);
            library.plantIdleTop = idleTop / 16f;
            UnityEngine.Object.DestroyImmediate(spring);
            library.neutralSpriteMaterial = Material(Root + "/Neutral Sprite.mat");
            library.colorSpriteMaterial = Material(Root + "/Plant Sprite.mat");
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            BindPrefab("Assets/_Project/Content/ColorBlocks/Prefabs/Respawn_Block.prefab");
            BindPrefab("Assets/_Project/Content/ColorBlocks/Prefabs/Green_JumpPlant.prefab");
            RefreshOpenScenes();
        }

        public static void BuildVineSequence()
        {
            Folder(Root); Folder(Generated);
            var library = AssetDatabase.LoadAssetAtPath<ObjectArtworkLibrary>(Root + "/Objects.asset");
            if (library == null) throw new InvalidOperationException("缺少物体美术配置。");
            const string source = "Assets/ta_source/Particle/Grow/序列帧";
            var paths = Directory.GetFiles(source, "Grow_*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (paths.Length != 192) throw new InvalidOperationException("藤蔓素材应包含192帧。");
            var textures = new Texture2D[paths.Length];
            var bounds = new RectInt[paths.Length];
            int left = int.MaxValue, right = 0, maxHeight = 0;
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    textures[i] = Read(paths[i]);
                    bounds[i] = AlphaBounds(textures[i]);
                    left = Mathf.Min(left, bounds[i].xMin); right = Mathf.Max(right, bounds[i].xMax);
                    maxHeight = Mathf.Max(maxHeight, bounds[i].height);
                }
                int width = right - left;
                const int padding = 2, perPage = 16, columns = 4, rows = 4;
                int strideX = width + padding * 2, strideY = maxHeight + padding * 2;
                var frames = new System.Collections.Generic.List<Sprite>();
                for (int page = 0; page * perPage < paths.Length; page++)
                {
                    var atlas = new Texture2D(strideX * columns, strideY * rows, TextureFormat.RGBA32, false);
                    atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
                    var entries = new System.Collections.Generic.List<SpriteMetaData>();
                    for (int item = 0; item < perPage && page * perPage + item < paths.Length; item++)
                    {
                        int index = page * perPage + item;
                        int x = item % columns * strideX + padding, y = item / columns * strideY + padding;
                        int height = bounds[index].height;
                        atlas.SetPixels(x, y, width, height, textures[index].GetPixels(left, bounds[index].yMin, width, height));
                        entries.Add(new SpriteMetaData { name = "Vine_" + index.ToString("D3"),
                            rect = new Rect(x, y, width, height), alignment = (int)SpriteAlignment.BottomCenter,
                            pivot = new Vector2(.5f, 0f) });
                    }
                    string path = Generated + "/Vine Sequence " + page.ToString("D2") + ".png";
                    File.WriteAllBytes(path, atlas.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(atlas);
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.spritePixelsPerUnit = width;
                    importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true; importer.maxTextureSize = 2048;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
                    importer.SetTextureSettings(settings);
                    #pragma warning disable CS0618
                    importer.spritesheet = entries.ToArray();
                    #pragma warning restore CS0618
                    importer.SaveAndReimport();
                    frames.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name));
                }
                library.vineGrowth = frames.ToArray();
                library.vineInitialFrame = 24; library.vineGrownFrame = 160; library.vineIdleFrameSeconds = 1f / 12f;
                EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
                if (library.vineGrowth.Length != paths.Length) throw new InvalidOperationException("藤蔓图集帧数错误。");
            }
            finally { foreach (var texture in textures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static RectInt AlphaBounds(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            int xMin = texture.width, yMin = texture.height, xMax = -1, yMax = -1;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a > 0)
                { xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x); yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y); }
            if (xMax < xMin) throw new InvalidOperationException("藤蔓包含空白帧。");
            return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }
        private static void BindPrefab(string path)
        {
            var owner = PrefabUtility.LoadPrefabContents(path);
            try { WorldObjectSpritePresentation.Ensure(owner); PrefabUtility.SaveAsPrefabAsset(owner, path); }
            finally { PrefabUtility.UnloadPrefabContents(owner); }
        }
        private static Texture2D Read(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("无法读取物体图片：" + path);
            return texture;
        }
        private static Material Material(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Project/Objects/Pixel Sprite Reveal");
            if (shader == null) throw new InvalidOperationException("物体精灵 Shader 未导入。");
            material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static Sprite[] Slice(string path, int width, int height, string prefix)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int textureWidth, out int textureHeight);
            if (textureHeight != height || textureWidth % width != 0) throw new InvalidOperationException("素材帧尺寸错误：" + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = height;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(textureWidth, textureHeight)));
            importer.alphaIsTransparency = true;
            foreach (var target in new[] { "Standalone", "Android", "iPhone" })
            {
                var platform = importer.GetPlatformTextureSettings(target);
                platform.overridden = false;
                importer.SetPlatformTextureSettings(platform);
            }
            var data = new SpriteMetaData[textureWidth / width];
            for (int index = 0; index < data.Length; index++) data[index] = new SpriteMetaData {
                name = prefix + "_" + index.ToString("D2"), rect = new Rect(index * width, 0, width, height),
                alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0f), border = Vector4.zero
            };
            #pragma warning disable CS0618
            importer.spritesheet = data;
            #pragma warning restore CS0618
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (sprites.Length != data.Length) throw new InvalidOperationException("切片帧数量错误：" + path);
            return sprites;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    public sealed class WorldObjectArtworkImports : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (imported.Any(path => path == "Assets/_Project/Resources/ObjectArtwork/Objects.asset"))
                EditorApplication.delayCall += WorldObjectArtworkSetup.RefreshOpenScenes;
        }
    }
}
