using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.GridArt.Editor
{
    public static class GridArtMovingSeamAudit
    {
        public static void UpgradeGeneratedMeshes()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe.");
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/_Project/Art/Generated/GridArt" }))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                if (mesh == null || mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1)) continue;
                var uv = mesh.uv;
                if (uv.Length % 4 != 0) throw new InvalidOperationException("Not a quad mesh: " + mesh.name);
                var bounds = new System.Collections.Generic.List<Vector4>(uv.Length);
                for (int i = 0; i < uv.Length; i += 4)
                {
                    Vector2 min = uv[i], max = uv[i];
                    for (int j = 1; j < 4; j++) { min = Vector2.Min(min, uv[i+j]); max = Vector2.Max(max, uv[i+j]); }
                    for (int j = 0; j < 4; j++) bounds.Add(new Vector4(min.x, min.y, max.x, max.y));
                }
                mesh.SetUVs(1, bounds); EditorUtility.SetDirty(mesh); changed++;
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/GridArtSamplingUpgrade.txt", "Upgraded meshes=" + changed);
            EditorApplication.Exit(0);
        }

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe.");
            string folder = "Assets/__SeamAudit_" + Guid.NewGuid().ToString("N");
            string generated = null;
            int leaks = 0, frames = 0;
            bool legacy = Environment.GetCommandLineArgs().Contains("-gridArtLegacyControl");
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                EditorSceneManager.SaveScene(scene, folder + "/Probe.unity");
                generated = "Assets/_Project/Art/Generated/GridArt/" + AssetDatabase.AssetPathToGUID(scene.path);
                var palette = ScriptableObject.CreateInstance<GridArtPalette>();
                var path = AssetDatabase.GUIDToAssetPath("b8de2073d3e11024094c3f754c658974");
                var sprite = Sprite.Create(AssetDatabase.LoadAssetAtPath<Texture2D>(path), new Rect(64, 576, 64, 64), Vector2.one * .5f, 64);
                palette.brushes.Add(new GridArtBrush { sprite = sprite, visualSize = Vector2.one });
                AssetDatabase.CreateAsset(palette, folder + "/Palette.asset");
                AssetDatabase.AddObjectToAsset(sprite, palette);
                var map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette = palette;
                var layer = new GridArtLayer { label = "Seam probe", depth = 5 };
                for (int x = -125; x < -75; x++) for (int y = -45; y < -5; y++) layer.Set(new Vector2Int(x, y), 0);
                map.layers.Add(layer); AssetDatabase.CreateAsset(map, folder + "/Map.asset");
                var output = GridArtBuilder.Apply(map, scene, false);
                if (legacy)
                    foreach (var filter in output.GetComponentsInChildren<MeshFilter>())
                        filter.sharedMesh.SetUVs(1,new System.Collections.Generic.List<Vector4>());
                var camera = new GameObject("Moving seam camera").AddComponent<Camera>();
                camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.cyan; camera.allowHDR = false;
                string report = "";
                foreach (bool mask in new[] { false, true })
                {
                Material maskMaterial = null;
                if (mask)
                {
                    maskMaterial = new Material(Shader.Find("Hidden/2026TapTap/ColorObjectMask"));
                    maskMaterial.SetTexture("_ColorMaskAlphaTexture", sprite.texture);
                    maskMaterial.SetFloat("_ColorMaskOpacity",1);
                    maskMaterial.SetFloat("_ColorMaskGroup",-1);
                    maskMaterial.SetFloat("_GridArtSliceClamp",1);
                    foreach (var renderer in output.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = maskMaterial;
                }
                foreach (int samples in new[] { 1, 4 })
                {
                    int modeLeaks = 0;
                    var target = new RenderTexture(641, 359, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
                    target.Create(); camera.targetTexture = target; camera.allowMSAA = samples > 1;
                    var image = new Texture2D(641, 359, TextureFormat.RGBA32, false);
                    foreach (float zoom in new[] { 4f, 8f, 8.1234f })
                    for (int step = 0; step < 40; step++)
                    {
                        camera.orthographicSize = zoom;
                        camera.transform.position = new Vector3(-100 + step * .00317f, -25 + step * .00231f, -10);
                        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 641, 359), 0, 0); image.Apply(); RenderTexture.active = previous;
                        int count = image.GetPixels32().Count(c => mask ? c.r < 245 : c.g > 8 || c.b > 8);
                        if (count > 0 && leaks == 0) File.WriteAllBytes("Library/GridArtMovingSeam.png", image.EncodeToPNG());
                        leaks += count; modeLeaks += count; frames++;
                        if (step == 1 && zoom == 8f && samples == 1)
                            File.WriteAllBytes("Library/GridArtMovingSeam" + (legacy ? "Legacy_" : "Fixed_") + (mask ? "mask" : "color") + ".png", image.EncodeToPNG());
                    }
                    camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                    report += (mask ? "mask" : "color") + " MSAA=" + samples + " leakedPixels=" + modeLeaks + "\n";
                }
                if (maskMaterial != null) Object.DestroyImmediate(maskMaterial);
                }
                File.WriteAllText("Library/GridArtMovingSeamAudit" + (legacy ? "Legacy" : "") + ".txt", "frames=" + frames + " leakedPixels=" + leaks + "\n" + report);
                Debug.Log("GridArtMovingSeamAudit frames=" + frames + " leakedPixels=" + leaks);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                AssetDatabase.DeleteAsset(generated); AssetDatabase.DeleteAsset(folder);
                EditorApplication.Exit((legacy ? leaks > 0 : leaks == 0) ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText("Library/GridArtMovingSeamAudit.txt", e.ToString()); EditorApplication.Exit(1); }
        }
    }
}
