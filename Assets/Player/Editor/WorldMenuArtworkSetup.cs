using System;
using System.IO;
using Project.StartMenu;
using UnityEditor;
using UnityEngine;

namespace Project.Player.Editor
{
    /// <summary>Explicit source import and shared artwork wiring. Does not open/save any scene.</summary>
    public static class WorldMenuArtworkSetup
    {
        private const string Root = "Assets/_Project/Resources/26TAPTAP/UI/";
        private const string ArtworkPath = "Assets/_Project/Resources/UI/GameUiArtwork.asset";

        [MenuItem("Tools/2026TapTap/UI/接入场景融合开始菜单素材")]
        public static void Build()
        {
            string[] names = { "Start", "Staff", "Setting", "Exit" };
            var normal = new Sprite[4];
            var selected = new Sprite[4];
            var leftPadding = new float[4];
            for (int i = 0; i < names.Length; i++)
            {
                normal[i] = Import(Root + "ButtonIcon/Icon_" + names[i] + "01.png");
                selected[i] = Import(Root + "ButtonIcon/Icon_" + names[i] + "02.png");
                leftPadding[i] = Mathf.Min(InkLeft(Root + "ButtonIcon/Icon_" + names[i] + "01.png"),
                    InkLeft(Root + "ButtonIcon/Icon_" + names[i] + "02.png"));
            }
            var frames = new Sprite[27];
            for (int i = 0; i < frames.Length; i++) frames[i] = Import(Root + "LOGO/LOGO" + (i + 1).ToString("00") + ".png");
            GameUiArtwork art = AssetDatabase.LoadAssetAtPath<GameUiArtwork>(ArtworkPath);
            if (art == null) throw new InvalidOperationException("Missing shared GameUiArtwork: " + ArtworkPath);
            art.worldMenuButtons = normal;
            art.worldMenuSelectedButtons = selected;
            art.worldMenuButtonLeftPadding = leftPadding;
            art.worldMenuLogoFrames = frames;
            art.worldMenuLogoFrameSeconds = .08f;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssetIfDirty(art);
            Debug.Log("WORLD MENU ARTWORK: imported 35 Point/FullRect sprites and wired the shared artwork. No scenes or planning saves changed.");
        }

        private static int InkLeft(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read menu pixels: " + path);
                Color32[] pixels = texture.GetPixels32();
                int left = texture.width;
                for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < left; x++)
                        if (pixels[y * texture.width + x].a > 0) left = x;
                if (left == texture.width) throw new InvalidOperationException("Empty menu button: " + path);
                return left;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static Sprite Import(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing menu artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.spritePixelsPerUnit = 16f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = 512;
            importer.isReadable = false;
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = platform != "DefaultTexturePlatform";
                settings.maxTextureSize = 512;
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
