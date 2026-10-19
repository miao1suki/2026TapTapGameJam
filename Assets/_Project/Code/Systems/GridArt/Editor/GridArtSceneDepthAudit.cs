using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.GridArt.Editor
{
    /// <summary>Actual URP GPU check; intentionally restricted to the disposable validation project.</summary>
    public static class GridArtSceneDepthAudit
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe.");
            string folder = "Assets/__GridArtDepthAudit_" + Guid.NewGuid().ToString("N");
            string generated = null;
            RenderTexture target = null;
            Texture2D image = null;
            int checks = 0;
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                EditorSceneManager.SaveScene(scene, folder + "/Depth.unity");
                generated = "Assets/_Project/Art/Generated/GridArt/" + AssetDatabase.AssetPathToGUID(scene.path);
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                texture.SetPixels(Enumerable.Repeat(Color.white, 256).ToArray()); texture.Apply();
                AssetDatabase.CreateAsset(texture, folder + "/White.asset");
                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16);
                AssetDatabase.AddObjectToAsset(sprite, texture);
                var palette = ScriptableObject.CreateInstance<GridArtPalette>();
                palette.brushes.Add(new GridArtBrush { sprite = sprite, visualSize = Vector2.one });
                AssetDatabase.CreateAsset(palette, folder + "/Palette.asset");
                var map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette = palette;
                var far = new GridArtLayer { label = "远景背景", depth = 5, tint = Color.red };
                var near = new GridArtLayer { label = "近景背景", depth = 2, tint = Color.blue };
                var surface = new GridArtLayer { label = "墙地表面", depth = -.51f, sortingOrder = -100, tint = Color.yellow };
                for (int x = 0; x < 5; x++) far.Set(new Vector2Int(x, 0), 0);
                near.Set(new Vector2Int(2, 0), 0); surface.Set(new Vector2Int(3, 0), 0);
                map.layers.Add(far); map.layers.Add(near); map.layers.Add(surface);
                AssetDatabase.CreateAsset(map, folder + "/Map.asset");
                var output = GridArtBuilder.Apply(map, scene, false);
                var prop = new GameObject("Transparent gameplay artwork");
                prop.transform.position = new Vector3(1.5f, .5f, -.55f);
                var propRenderer = prop.AddComponent<SpriteRenderer>(); propRenderer.sprite = sprite;
                var propMaterial = new Material(Shader.Find("2026TapTap/Grid Art/Unlit"));
                propMaterial.SetTexture("_BaseMap", texture); propMaterial.SetColor("_BaseColor", Color.green);
                propRenderer.sharedMaterial = propMaterial;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(.5f, .5f, 0);
                var wallMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                wallMaterial.SetColor("_BaseColor", Color.cyan); wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                var camera = new GameObject("Depth validation camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(2.5f, .5f, -10); camera.orthographic = true;
                camera.orthographicSize = .5f; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(640, 128, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                image = new Texture2D(640, 128, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 640, 128), 0, 0); image.Apply(); RenderTexture.active = previous;
                File.WriteAllBytes("Library/GridArtSceneDepth.png", image.EncodeToPNG());
                void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
                void Pixel(int cell, Color expected, string message)
                {
                    Color actual = image.GetPixel(cell * 128 + 64, 64);
                    Check(Mathf.Abs(actual.r - expected.r) < .08f && Mathf.Abs(actual.g - expected.g) < .08f && Mathf.Abs(actual.b - expected.b) < .08f,
                        message + ": actual " + actual);
                }
                Pixel(0, Color.cyan, "far art must remain behind opaque gameplay wall");
                Pixel(1, Color.green, "far art must remain behind transparent gameplay prop");
                Pixel(2, Color.blue, "near background must remain in front of far background");
                Pixel(3, Color.yellow, "wall surface must remain in front of far background");
                Pixel(4, Color.red, "uncovered far background must remain visible");
                Check(output.GetComponentsInChildren<Collider>(true).Length == 0, "decorative art must not add gameplay collisions");
                Check(far.depth == 5 && near.depth == 2 && surface.depth == -.51f, "author depth stays unchanged");
                Check(map.layers.Count == 3 && map.layers.Sum(l => l.cells.Count) == 7, "author placement stays unchanged");
                Object.DestroyImmediate(propMaterial); Object.DestroyImmediate(wallMaterial);
                Debug.Log("GridArtSceneDepthAudit PASS " + checks);
                File.WriteAllText("Library/GridArtSceneDepthAudit.txt", "PASS " + checks + "\nActual URP camera render: wall / transparent prop / near background / wall surface / far background.\nNo production assets or authored layer values modified.");
                Finish(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText("Library/GridArtSceneDepthAudit.txt", "FAIL " + checks + "\n" + error);
                Finish(1);
            }
            void Finish(int code)
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (image != null) Object.DestroyImmediate(image);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                if (!string.IsNullOrEmpty(generated)) AssetDatabase.DeleteAsset(generated);
                AssetDatabase.DeleteAsset(folder);
                EditorApplication.Exit(code);
            }
        }
    }
}
