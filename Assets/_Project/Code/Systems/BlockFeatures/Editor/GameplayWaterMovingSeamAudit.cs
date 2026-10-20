using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.BlockFeatures.Editor
{
    /// <summary>Real GPU comparison; never modifies source art, author maps or scenes.</summary>
    public static class GameplayWaterMovingSeamAudit
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe, with a real GPU.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A GPU is required; do not pass -nographics.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var camera = new GameObject("Moving water seam camera").AddComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta; camera.allowHDR = false;
            camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            var settings = WaterPresentationSettings.Load();
            if (settings == null || !settings.IsValid) throw new InvalidOperationException("Real water artwork missing.");
            int fixedLeaks = 0, legacyLeaks = 0, frames = 0;
            string report = "Real 16x17 Body/Interior textures; unchanged UV0, moving negative-coordinate camera.\n";
            try
            {
                foreach (int kind in new[] { 1, 2 })
                {
                    Sprite sprite = settings.tiles[kind];
                    Rect source = sprite.textureRect;
                    Rect uv = Rect.MinMaxRect(source.xMin / sprite.texture.width, (source.yMin + 1) / sprite.texture.height,
                        source.xMax / sprite.texture.width, source.yMax / sprite.texture.height);
                    var mesh = BuildFixture(uv);
                    var go = new GameObject("Real water " + kind);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    foreach (bool mask in new[] { false, true })
                    {
                        var material = new Material(Shader.Find(mask ? "Hidden/2026TapTap/ColorObjectMask" : "2026TapTap/ColorVineReveal"));
                        material.SetTexture(mask ? "_ColorMaskAlphaTexture" : "_MainTex", sprite.texture);
                        material.SetFloat("_ColorMaskGroup", -1); material.SetFloat("_ColorMaskOpacity", 1);
                        material.SetFloat("_ColorRevealOpacity", 1); material.SetColor("_TintColor", Color.white);
                        renderer.sharedMaterial = material;
                        foreach (bool fixedSampling in new[] { false, true })
                        {
                            var block = new MaterialPropertyBlock();
                            block.SetFloat("_GridArtSliceClamp", fixedSampling ? 1 : 0); renderer.SetPropertyBlock(block);
                            int modeLeaks = 0;
                            foreach (int samples in new[] { 1, 4 })
                            {
                                var target = new RenderTexture(641, 359, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
                                target.Create(); camera.targetTexture = target; camera.allowMSAA = samples > 1;
                                var image = new Texture2D(641, 359, TextureFormat.RGBA32, false);
                                foreach (float zoom in new[] { 4f, 8f, 8.1234f })
                                for (int step = 0; step < 24; step++)
                                {
                                    camera.orthographicSize = zoom;
                                    camera.transform.position = new Vector3(-100 + step * .00317f, -25 + step * .00231f, -10);
                                    camera.Render();
                                    var previous = RenderTexture.active; RenderTexture.active = target;
                                    image.ReadPixels(new Rect(0, 0, 641, 359), 0, 0); image.Apply(); RenderTexture.active = previous;
                                    int count = 0;
                                    foreach (Color32 color in image.GetPixels32())
                                        if (mask ? color.g < 200 : color.r > 180) count++;
                                    modeLeaks += count; frames++;
                                    if (step == 0 && samples == 1 && zoom == 8f || count > 0 && modeLeaks == count)
                                        File.WriteAllBytes("Library/WaterSeam_" + kind + "_" + (mask ? "Mask_" : "Color_") + (fixedSampling ? "Fixed" : "Legacy") + ".png", image.EncodeToPNG());
                                }
                                camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                            }
                            if (fixedSampling) fixedLeaks += modeLeaks; else legacyLeaks += modeLeaks;
                            report += "kind=" + kind + " mask=" + mask + " clamp=" + fixedSampling + " leakedPixels=" + modeLeaks + "\n";
                        }
                        Object.DestroyImmediate(material);
                    }
                    Object.DestroyImmediate(go); Object.DestroyImmediate(mesh);
                }
                report += "frames=" + frames + " legacyLeaks=" + legacyLeaks + " fixedLeaks=" + fixedLeaks + "\n";
                File.WriteAllText("Library/GameplayWaterMovingSeamAudit.txt", report); Debug.Log(report);
                if (fixedLeaks != 0) throw new InvalidOperationException("Fixed water sampling still leaks: " + fixedLeaks);
                if (legacyLeaks == 0) throw new InvalidOperationException("No negative-control leak reproduced; do not claim a reproduced fix from this run.");
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
        }

        private static Mesh BuildFixture(Rect uv)
        {
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>();
            var slices = new List<Vector4>(); var indices = new List<int>(); var colors = new List<Color>();
            for (int y = -45; y < -5; y++) for (int x = -125; x < -75; x++)
            {
                int start = vertices.Count;
                vertices.Add(new Vector3(x, y, 0)); vertices.Add(new Vector3(x + 1, y, 0));
                vertices.Add(new Vector3(x + 1, y + 1, 0)); vertices.Add(new Vector3(x, y + 1, 0));
                uvs.Add(new Vector2(uv.xMin, uv.yMin)); uvs.Add(new Vector2(uv.xMax, uv.yMin));
                uvs.Add(new Vector2(uv.xMax, uv.yMax)); uvs.Add(new Vector2(uv.xMin, uv.yMax));
                for (int j = 0; j < 4; j++) { slices.Add(new Vector4(uv.xMin, uv.yMin, uv.xMax, uv.yMax)); colors.Add(Color.white); }
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
                indices.Add(start); indices.Add(start + 3); indices.Add(start + 2);
            }
            var mesh = new Mesh { name = "Water exact 16/17 UV GPU fixture" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetUVs(1, slices); mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); return mesh;
        }
    }
}
