using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Project.Player.Editor
{
    /// <summary>Explicit artwork import only. Does not open or save scenes/prefabs or touch user layouts.</summary>
    public static class MobileArtworkSetup
    {
        private const string Source = "Assets/_Project/Resources/26TAPTAP/UI/B31A4497C97A51CF13A50549B8241494.png";
        private const string IconPath = "Assets/_Project/Resources/正式ICON.png";
        private const string Output = "Assets/_Project/Resources/MobileControls";
        private static readonly string[] Names =
        { "Pause", "Respawn", "Left", "Right", "Up", "Jump", "DropRight", "Interact", "Color", "Down", "DropLeft", "Walk" };
        // Top-left sheet: its movement/jump keys are 30x30, versus the wide right-hand variant.
        // Positions use top-left PNG coordinates; bottom-left sheet is its pressed state (+185 y).
        private static readonly RectInt[] Rects =
        {
            new RectInt(224,30,34,28), new RectInt(272,30,34,28),
            new RectInt(16,147,30,30), new RectInt(112,147,30,30),
            new RectInt(64,99,30,30), new RectInt(112,99,30,30),
            new RectInt(176,108,34,28), new RectInt(224,108,34,28),
            new RectInt(272,108,34,28), new RectInt(64,147,30,30),
            new RectInt(176,149,34,28), new RectInt(224,149,34,28)
        };

        [MenuItem("Tools/2026TapTap/UI/接入方形手机按键与双端图标")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(Source))) throw new InvalidOperationException("Cannot decode mobile artwork.");
                if (source.width != 645 || source.height != 382) throw new InvalidOperationException("Mobile source layout changed; review crop coordinates first. Actual=" + source.width + "x" + source.height);
                for (int i = 0; i < Names.Length; i++)
                {
                    Write(source, Names[i], Rects[i], false);
                    Write(source, Names[i], Rects[i], true);
                }
                // No independent run caption exists in the supplied artwork; pressed walking key
                // communicates the existing latched sprint state without inventing a new drawing.
                Write(source, "Run", Rects[11], false);
                Write(source, "Run", Rects[11], true);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(Output, "*.png")) Import(path.Replace('\\', '/'), true);
            ApplyOfficialIcons();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("MOBILE ARTWORK PASS: 26 pixel sprites, square movement/jump variant, supplied Windows/Android icon. No scene, prefab, layout or network settings changed.");
        }

        [MenuItem("Tools/2026TapTap/UI/应用正式双端图标")]
        public static void ApplyOfficialIcons()
        {
            Import(IconPath, false);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) throw new InvalidOperationException("Missing supplied application icon.");
            SetLegacyIcons(BuildTargetGroup.Unknown, icon);
            SetLegacyIcons(BuildTargetGroup.Standalone, icon);
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in slots)
                    for (int layer = 0; layer < slot.maxLayerCount; layer++) slot.SetTexture(icon, layer);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }
            AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            foreach (string name in Names)
                foreach (string state in new[] { "Normal", "Pressed" })
                {
                    string path = Output + "/" + name + "_" + state + ".png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (sprite == null || importer == null || importer.mipmapEnabled || importer.filterMode != FilterMode.Point)
                        throw new InvalidOperationException("Invalid mobile artwork: " + path);
                    if ((name == "Up" || name == "Down" || name == "Left" || name == "Right" || name == "Jump") && sprite.rect.width != sprite.rect.height)
                        throw new InvalidOperationException("Wrong wide mobile variant: " + path);
                }
        }

        private static void Write(Texture2D source, string name, RectInt rect, bool pressed)
        {
            int y = rect.y + (pressed ? 185 : 0);
            var texture = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(source.GetPixels(rect.x, source.height - y - rect.height, rect.width, rect.height));
                texture.Apply();
                string path = Output + "/" + name + (pressed ? "_Pressed.png" : "_Normal.png");
                byte[] bytes = texture.EncodeToPNG();
                if (!File.Exists(path) || !Equal(File.ReadAllBytes(path), bytes)) File.WriteAllBytes(path, bytes);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static void Import(string path, bool sprite)
        {
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing artwork: " + path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = sprite; importer.isReadable = false;
            importer.maxTextureSize = sprite ? 32 : 512;
            if (sprite) importer.maxTextureSize = 64;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.name = platform; platformSettings.overridden = platform != "DefaultTexturePlatform";
                platformSettings.maxTextureSize = importer.maxTextureSize;
                platformSettings.format = TextureImporterFormat.RGBA32;
                platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(platformSettings);
            }
            importer.SaveAndReimport();
        }
        private static void SetLegacyIcons(BuildTargetGroup group, Texture2D icon)
        {
            int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(group);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIconsForTargetGroup(group, icons);
        }
    }
}
