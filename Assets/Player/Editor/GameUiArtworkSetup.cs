using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Project.GameFlow;
using Project.StartMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Player.Editor
{
    public static class GameUiArtworkSetup
    {
        private const string Source = "Assets/_Project/Resources/26TAPTAP/UI/";
        private const string Output = "Assets/_Project/Art/Generated/GameUI/";
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            var asset = AssetDatabase.LoadAssetAtPath<GameUiArtwork>("Assets/_Project/Resources/UI/GameUiArtwork.asset");
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GameUiArtwork>();
                AssetDatabase.CreateAsset(asset, "Assets/_Project/Resources/UI/GameUiArtwork.asset");
            }
            var background = Read("Image_765789177504539.png");
            asset.menuBackground = Crop(background, "Menu Background", 0, 0, 320, 180).texture;
            var backgroundImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset.menuBackground));
            backgroundImporter.wrapModeU = TextureWrapMode.Repeat;
            backgroundImporter.SaveAndReimport();
            asset.panelBackground = Crop(background, "Panel Background", 0, 0, 320, 180);
            UnityEngine.Object.DestroyImmediate(background);
            var menu = Read("Image_765791514096674.png");
            asset.startButton = CropPadded(menu, "Start Button", 8, 23, 108, 48);
            asset.settingsButton = CropPadded(menu, "Settings Button", 6, 127, 68, 30);
            asset.quitButton = CropPadded(menu, "Quit Button", 74, 127, 58, 30);
            asset.creditsButton = CropPadded(menu, "Credits Button", 7, 79, 108, 41);
            UnityEngine.Object.DestroyImmediate(menu);
            BuildPauseSprites(asset);
            var wheelSheet = Read("Image_765784931964749.png");
            var wheel = wheelSheet.GetPixels(313, wheelSheet.height - 39 - 64, 64, 64);
            asset.abilitySectors = new Sprite[3]; asset.abilityHighlights = new Sprite[3];
            asset.abilityPivots = new Vector2[3];
            int[] pixelOwners = ClassifyWheel(wheel,64,64);
            for (int i = 0; i < 3; i++)
            {
                var normal = new Color[wheel.Length]; var white = new Color[wheel.Length];
                Vector2 center = Vector2.zero; float weight = 0;
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int p = y * 64 + x;
                    if (wheel[p].a <= 0) continue;
                    if (pixelOwners[p] != i) continue;
                    normal[p] = wheel[p]; white[p] = new Color(1, 1, 1, wheel[p].a);
                    center += new Vector2(x + .5f, y + .5f) * wheel[p].a;
                    weight += wheel[p].a;
                }
                asset.abilityPivots[i] = center / Mathf.Max(1, weight) / 64;
                asset.abilitySectors[i] = Save("Ability " + i, normal, 64, 64);
                asset.abilityHighlights[i] = Save("Ability White " + i, white, 64, 64);
            }
            UnityEngine.Object.DestroyImmediate(wheelSheet);
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            ApplySavedViews();
        }

        public static void BuildPauseArtwork()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameUiArtwork>("Assets/_Project/Resources/UI/GameUiArtwork.asset");
            if (asset == null) throw new InvalidOperationException("Missing shared UI artwork.");
            BuildPauseSprites(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            ApplySavedPauseView();
        }

        private static void BuildPauseSprites(GameUiArtwork asset)
        {
            var icons = Read("1-4菜单.png");
            try
            {
                asset.pauseIcons = new Sprite[6];
                int[] positions = {176, 112, 144, 16, 48, 80};
                for (int i = 0; i < positions.Length; i++)
                {
                    var sprite = Crop(icons, "Pause Icon " + i, positions[i], 22, 32, 38);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    asset.pauseIcons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(sprite));
                }
                asset.pauseSectorPivot = new Vector2(-7f / 32f, 23f / 38f);
                asset.pauseSectorAngle = 60f;
                asset.pauseSectorPixelScale = 9f;
            }
            finally { UnityEngine.Object.DestroyImmediate(icons); }
        }
        private static Texture2D Read(string file)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(Source + file))) throw new Exception("Cannot read " + file);
            return texture;
        }
        private static int[] ClassifyWheel(Color[] pixels, int width, int height)
        {
            var owners = new int[pixels.Length];
            for(int i=0;i<owners.Length;i++) owners[i]=-1;
            var visited = new bool[pixels.Length];
            var queue = new Queue<int>();
            var component = new List<int>();
            for(int start=0;start<pixels.Length;start++)
            {
                if(visited[start] || pixels[start].a <= 0) continue;
                queue.Enqueue(start); visited[start]=true; component.Clear();
                Vector3 votes=Vector3.zero;
                while(queue.Count>0)
                {
                    int p=queue.Dequeue(); component.Add(p);
                    votes += new Vector3(pixels[p].r,pixels[p].g,pixels[p].b)*pixels[p].a;
                    int x=p%width, y=p/width;
                    Visit(x-1,y); Visit(x+1,y); Visit(x,y-1); Visit(x,y+1);
                }
                int owner = votes.y>votes.x && votes.y>votes.z ? 1 : votes.z>votes.x ? 2 : 0;
                foreach(int p in component) owners[p]=owner;
            }
            return owners;
            void Visit(int x,int y)
            {
                if(x<0 || x>=width || y<0 || y>=height) return;
                int p=y*width+x;
                if(visited[p] || pixels[p].a<=0) return;
                visited[p]=true;queue.Enqueue(p);
            }
        }
        private static Sprite Crop(Texture2D texture, string name, int x, int top, int width, int height) =>
            Save(name, texture.GetPixels(x, texture.height - top - height, width, height), width, height);
        private static Sprite CropPadded(Texture2D texture, string name, int x, int top, int width, int height)
        {
            const int padding = 2;
            var source = texture.GetPixels(x, texture.height - top - height, width, height);
            var pixels = new Color[(width + padding * 2) * (height + padding * 2)];
            for (int row = 0; row < height; row++)
                Array.Copy(source, row * width, pixels, (row + padding) * (width + padding * 2) + padding, width);
            return Save(name, pixels, width + padding * 2, height + padding * 2);
        }
        private static Sprite Save(string name, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels); texture.Apply();
            string path = Output + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
            importer.alphaIsTransparency = true; importer.isReadable = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void ApplySavedViews()
        {
            foreach (string path in new[] { "Assets/_Project/Scenes/Start/MainMenu.unity",
                "Assets/_Project/Development/StartMenu/Orpheus0829/Scenes/StartMenuPrototype.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (StartMenuController controller in root.GetComponentsInChildren<StartMenuController>(true))
                        controller.ApplyArtwork();
                EditorSceneManager.SaveScene(scene);
            }
            ApplySavedPauseView();
        }

        private static void ApplySavedPauseView()
        {
            string prefab = "Assets/_Project/Content/UI/SharedGameUI.prefab";
            var ui = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                foreach (var controller in ui.GetComponentsInChildren<EscMenuController>(true))
                    typeof(EscMenuController).GetMethod("EnsureSectorGraphics", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                PrefabUtility.SaveAsPrefabAsset(ui, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(ui); }
            AssetDatabase.SaveAssets();
        }
    }
}
