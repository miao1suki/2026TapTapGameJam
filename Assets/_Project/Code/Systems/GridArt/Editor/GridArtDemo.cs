using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.GridArt.Editor
{
    public static class GridArtDemo
    {
        public const string Root = "Assets/_Project/Development/GridArt/TileArtTrial";
        public const string MapPath = Root + "/示例美术图.asset";
        public const string ScenePath = Root + "/网格美术测试.unity";
        private const string Source = "Assets/_Project/Resources/26TAPTAP/场景背景/tail.png";
        public static void Create(GridArtWindow window)
        {
            BuildAssets(); window.SetMap(AssetDatabase.LoadAssetAtPath<GridArtMap>(MapPath));
        }
        [MenuItem("Tools/2026TapTap/关卡美术/打开美术测试场景")]
        public static void OpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                EditorUtility.DisplayDialog("美术测试场景", "测试场景尚未导入。请等待资源导入完成。", "确定"); return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath); Create(EditorWindow.GetWindow<GridArtWindow>("网格美术"));
        }
        public static void BuildAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GridArtMap>(MapPath) != null) return;
            GridArtBuilder.EnsureFolder(Root);
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Source);
            if (source == null) throw new System.InvalidOperationException("缺少 tail.png 素材。");
            var palette = ScriptableObject.CreateInstance<GridArtPalette>();
            AssetDatabase.CreateAsset(palette, Root + "/素材与规则示例.asset");
            // Small schematic sheet demonstrates complete corner rules, not final art.
            var texture = new Texture2D(64 * 13, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[texture.width * texture.height];
            for (int tile = 0; tile < 13; tile++)
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    bool edge = false;
                    if (tile < 9)
                    {
                        int column = tile % 3, row = tile / 3;
                        edge = column == 0 && x < 4 || column == 2 && x >= 60 || row == 0 && y >= 60 || row == 2 && y < 4;
                    }
                    else
                    {
                        int q = tile - 9;
                        edge = (q % 2 == 0 ? x < 4 : x >= 60) && (q < 2 ? y < 4 : y >= 60);
                    }
                    pixels[(tile * 64 + x) + y * texture.width] = edge ? new Color32(150,174,193,255) : new Color32(43,52,62,255);
                }
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(Root + "/规则示意.png", texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Root + "/规则示意.png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/规则示意.png");
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var schematic = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/规则示意.png");
            var rules = new GridArtBrush { label = "自动边角示意", autoTerrain = true };
            for (int i = 0; i < 13; i++) rules.pieces[i] = AddSprite(palette, schematic, new Rect(i * 64,0,64,64), "规则_" + i);
            palette.brushes.Add(rules);
            var frame = new GridArtBrush { label = "美术边框区域", autoTerrain = true };
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
            {
                int i = row * 3 + col;
                frame.pieces[i] = i == 4 ? AddSprite(palette,source,new Rect(640,351,1,1),"黑色填充") : AddSprite(palette, source,
                    new Rect(640 + col * 128, 704 - (320 + row * 128) - 64, 64,64), "边框_" + i);
            }
            palette.brushes.Add(frame);
            for (int row = 0; row < 5; row++) for (int col = 0; col < 4; col++)
                palette.brushes.Add(new GridArtBrush { label = "图案 " + (row * 4 + col + 1), sprite = AddSprite(palette, source,
                    new Rect(64 + col * 128, 704 - (64 + row * 128) - 64,64,64), "图案_" + row + "_" + col) });
            palette.brushes.Add(new GridArtBrush { label = "大图案 · 3×3", size = new Vector2Int(3,3), sprite = palette.brushes[14].sprite });
            EditorUtility.SetDirty(palette);
            var map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette = palette;
            var back = new GridArtLayer { label = "远景墙体", depth = 5, parallax = new Vector2(.25f,.25f), tint = new Color(.55f,.6f,.7f) };
            for (int y = -3; y < 7; y++) for (int x = -10; x < 11; x++)
                if (!(x >= -2 && x <= 2 && y >= 1 && y <= 3)) back.Set(new Vector2Int(x,y),0);
            var middle = new GridArtLayer { label = "中景纹样", depth = 3, parallax = new Vector2(.65f,.65f), tint = new Color(.55f,.7f,.8f) };
            for (int x = -8; x <= 8; x += 2) middle.Set(new Vector2Int(x,4),2 + (x + 8) % 20);
            var front = new GridArtLayer { label = "场景装饰", depth = -.51f };
            for (int x = -8; x <= 8; x++) for (int y = -2; y < 0; y++) front.Set(new Vector2Int(x,y),1);
            map.layers.Add(back); map.layers.Add(middle); map.layers.Add(front);
            AssetDatabase.CreateAsset(map, MapPath); AssetDatabase.SaveAssets();
        }
        private static Sprite AddSprite(GridArtPalette palette, Texture2D texture, Rect rect, string label)
        {
            var sprite = Sprite.Create(texture, rect, new Vector2(.5f,.5f),64,0,SpriteMeshType.FullRect);
            sprite.name = label; AssetDatabase.AddObjectToAsset(sprite,palette); return sprite;
        }
        // Used only by the isolated verification project; does not open/edit a user's scene.
        public static void BuildTestScene()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace(Path.DirectorySeparatorChar, '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new System.InvalidOperationException("测试场景生成仅允许在隔离验证项目执行。");
            BuildAssets(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera = new GameObject("美术预览摄像机").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0,1,-20); camera.orthographic = true; camera.orthographicSize = 7;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f,.13f,.17f);
            var output = GridArtBuilder.Apply(AssetDatabase.LoadAssetAtPath<GridArtMap>(MapPath),scene); output.targetCamera = camera;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        }
    }
}
