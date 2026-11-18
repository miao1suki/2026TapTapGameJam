using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.GridArt;
using Project.GridArt.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    /// <summary>Explicit candidate lab only; never runs from normal authoring UI.</summary>
    public static class SceneWorkbenchWindingLab
    {
        private const string Parent = "Assets/_Project/Development/LevelEditor";
        private static string labName = "WindingMotherLabR2";
        private static string Folder => Parent + "/" + labName;
        private static string Output => "Library/" + labName;

        public static void CreateFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int labIndex = Array.IndexOf(args, "-windingLabName");
                if (labIndex >= 0)
                {
                    if (labIndex + 1 >= args.Length || !System.Text.RegularExpressions.Regex.IsMatch(args[labIndex + 1], @"\AWindingMotherLabR[1-9][0-9]{0,2}[a-z]?\z"))
                        throw new ArgumentException("候选名须为 WindingMotherLabR 加轮次，不能包含路径");
                    labName = args[labIndex + 1];
                }
                if (Directory.Exists(Folder)) throw new InvalidOperationException("候选样板已存在，拒绝覆盖：" + Folder);
                int index = Array.IndexOf(args, "-windingArtInput");
                if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("缺少 -windingArtInput");
                string input = Path.GetFullPath(args[index + 1]);
                bool compactOnly = Array.IndexOf(args, "-compactOnly") >= 0;
                string[] textures = compactOnly ? new[] { "MossWindingCompactRaw.png" } : new[] { "MossWindingCompactRaw.png", "MossWindingWideRaw.png" };
                foreach (string name in textures)
                    if (!File.Exists(Path.Combine(input, name))) throw new FileNotFoundException(name);
                AssetDatabase.CreateFolder(Parent, labName);
                foreach (string name in textures)
                {
                    string path = Folder + "/" + name;
                    File.Copy(Path.Combine(input, name), path);
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 16;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                string palettePath = Folder + "/CandidatePalette.asset";
                Copy(SceneWorkbenchShowcase.Folder + "/ShowcasePalette.asset", palettePath);
                var palette = AssetDatabase.LoadAssetAtPath<GridArtPalette>(palettePath);
                var brush = palette.brushes[0];
                var compact = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/MossWindingCompactRaw.png");
                var wide = compactOnly ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/MossWindingWideRaw.png");
                if (compactOnly) brush.widePieces = Array.Empty<Sprite>();
                brush.label = "候选 · 弯曲母版同源提取（未定稿）";
                for (int i = 0; i < 13; i++)
                {
                    Sprite Piece(Texture2D texture, int pixels, string label)
                    {
                        var sprite = Sprite.Create(texture, new Rect(i * pixels, 0, pixels, pixels), Vector2.zero, 16, 0, SpriteMeshType.FullRect);
                        sprite.name = label + i; AssetDatabase.AddObjectToAsset(sprite, palette); return sprite;
                    }
                    brush.pieces[i] = Piece(compact, 16, "WindingCompact_");
                    if (!compactOnly) brush.widePieces[i] = Piece(wide, 32, "WindingWide_");
                }
                EditorUtility.SetDirty(palette); AssetDatabase.SaveAssetIfDirty(palette);
                Directory.CreateDirectory(Output);
                Clone(SceneWorkbenchShowcase.Folder + "/MossConnections.unity", "Connections", 1536, 1248);
                Clone(SceneWorkbenchShowcase.ScenePath, "Aqueduct", 1536, 864);
                Create47();
                File.WriteAllText(Output + "/result.txt", "Candidate scenes saved and GPU captures exported. Artwork is not approved.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void Copy(string source, string target)
        {
            if (!AssetDatabase.CopyAsset(source, target)) throw new IOException("复制失败：" + target);
        }

        private static void Clone(string source, string name, int width, int height)
        {
            string target = Folder + "/" + name + ".unity";
            Copy(source, target);
            var scene = EditorSceneManager.OpenScene(target);
            var art = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GridArtScene>(true)).Single();
            var sourceOwnership = art.GetComponent<GridArtRegionOwnershipLink>().Ownership;
            Copy(AssetDatabase.GetAssetPath(art.map), Folder + "/" + name + "Map.asset");
            Copy(AssetDatabase.GetAssetPath(sourceOwnership), Folder + "/" + name + "Ownership.asset");
            art.map = AssetDatabase.LoadAssetAtPath<GridArtMap>(Folder + "/" + name + "Map.asset");
            art.map.ownerSceneGuid = string.Empty;
            // Opening a Scene can unload an asset held only by this C# local.
            art.map.palette = AssetDatabase.LoadAssetAtPath<GridArtPalette>(Folder + "/CandidatePalette.asset");
            art.GetComponent<GridArtRegionOwnershipLink>().SetOwnership(AssetDatabase.LoadAssetAtPath<GridArtRegionOwnership>(Folder + "/" + name + "Ownership.asset"));
            EditorUtility.SetDirty(art.map);
            GridArtBuilder.Apply(art.map, scene, false, true);
            EditorSceneManager.SaveScene(scene);
            Capture(scene, Output + "/" + name + ".png", width, height);
        }

        private static void Create47()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var map = ScriptableObject.CreateInstance<GridArtMap>();
            map.palette = AssetDatabase.LoadAssetAtPath<GridArtPalette>(Folder + "/CandidatePalette.asset");
            var offsets = new[] { new Vector2Int(-1,-1),new Vector2Int(0,-1),new Vector2Int(1,-1),new Vector2Int(-1,0),
                new Vector2Int(1,0),new Vector2Int(-1,1),new Vector2Int(0,1),new Vector2Int(1,1) };
            var seen = new HashSet<int>();
            for (int bits = 0; bits < 256; bits++)
            {
                bool Has(int x, int y) => x == 0 && y == 0 || Array.FindIndex(offsets,p => p.x == x && p.y == y) is int i && i >= 0 && (bits & (1 << i)) != 0;
                int normalized = 0;
                for (int i = 0; i < offsets.Length; i++)
                {
                    var p = offsets[i];
                    if (Has(p.x,p.y) && (p.x == 0 || p.y == 0 || Has(p.x,0) && Has(0,p.y))) normalized |= 1 << i;
                }
                if (!seen.Add(normalized)) continue;
                int n = seen.Count - 1;
                var origin = new Vector2Int(n % 10 * 6 - 30, n / 10 * 6 - 12);
                var layer = new GridArtLayer { label = "邻接 " + n };
                layer.Set(origin,0);
                for (int i = 0; i < offsets.Length; i++) if ((normalized & (1 << i)) != 0) layer.Set(origin + offsets[i],0);
                map.layers.Add(layer);
            }
            if (seen.Count != 47) throw new InvalidOperationException("合法邻接数量错误：" + seen.Count);
            AssetDatabase.CreateAsset(map, Folder + "/All47Map.asset");
            var camera = new GameObject("候选邻接检查相机").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 18; camera.transform.position = new Vector3(-2.5f,.5f,-20);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f,.16f,.21f);
            EditorSceneManager.SaveScene(scene, Folder + "/All47.unity");
            GridArtBuilder.Apply(map, scene, false, true).SetCamera(camera);
            EditorSceneManager.SaveScene(scene);
            Capture(scene, Output + "/All47.png", 1536, 864);
        }

        private static void Capture(Scene scene, string output, int width, int height)
        {
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).First();
            var rt = new RenderTexture(width,height,24); rt.Create();
            var png = new Texture2D(width,height,TextureFormat.RGBA32,false);
            var active = RenderTexture.active; var target = camera.targetTexture;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                png.ReadPixels(new Rect(0,0,width,height),0,0); png.Apply(); File.WriteAllBytes(output,png.EncodeToPNG());
            }
            finally { camera.targetTexture = target; RenderTexture.active = active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(png); }
        }
    }
}
