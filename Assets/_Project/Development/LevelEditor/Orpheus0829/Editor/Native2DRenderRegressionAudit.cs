#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using InteractiveWater;
using Object = UnityEngine.Object;

namespace Project.Migration
{
    /// <summary>GPU checks at multiple preview distances; never saves author assets.</summary>
    public static class Native2DRenderRegressionAudit
    {
        private static int checks;
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks++; Debug.Log("NATIVE_2D_RENDER_PASS " + label);
        }

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/Native2DValidation/"))
                throw new InvalidOperationException("Use the isolated Native2DValidation project.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new Exception("GPU required.");
            Directory.CreateDirectory("Library/Native2DRegressions");
            var previousPipeline = QualitySettings.renderPipeline;
            checks = 0;
            try
            {
                foreach (string platform in new[] { "PC", "Mobile" })
                {
                    QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                        "Assets/Settings/" + platform + "_RPAsset.asset");
                    foreach (int samples in new[] { 1, 4 }) RunFixture(platform, samples);
                }
                File.WriteAllText("Library/Native2DRegressions/RenderAudit.txt", "Passed " + checks);
                Debug.Log("NATIVE_2D_RENDER_AUDIT_PASS " + checks);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
            finally { QualitySettings.renderPipeline = previousPipeline; }
        }

        private static void RunFixture(string platform, int samples)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var camera = new GameObject("Regression camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.allowMSAA = samples > 1; camera.allowHDR = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(320, 240, 24) { antiAliasing = samples }; target.Create(); camera.targetTexture = target;
            var image = new Texture2D(320, 240, TextureFormat.RGBA32, false);
            var gradient = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                gradient.SetPixel(x, y, new Color(x / 63f, y / 63f, .15f, 1));
            gradient.Apply();
            var backgroundMaterial = new Material(Shader.Find("2026TapTap/ColorVineReveal"));
            backgroundMaterial.mainTexture = gradient;
            Quad("Gradient", backgroundMaterial, new Vector3(0, 0, 0), new Vector2(16, 16));
            var waterMaterial = new Material(Shader.Find("Custom/Pixel Water/Screen Distortion"));
            waterMaterial.SetFloat("_Opacity", 1); waterMaterial.SetFloat("_ColorRevealOpacity", 1);
            waterMaterial.SetFloat("_TintStrength", 0); waterMaterial.SetFloat("_AnimationSpeed", 0);
            waterMaterial.SetFloat("_DistortionChangeSpeed", 0);
            var whole = Quad("Whole water", waterMaterial, new Vector3(0, 0, -.1f), new Vector2(4, 4));
            whole.AddComponent<WaterUnderlayCaptureRequester>();
            var left = Quad("Left water owner", waterMaterial, new Vector3(-1, 0, -.1f), new Vector2(2, 4));
            left.AddComponent<WaterUnderlayCaptureRequester>();
            var right = Quad("Right water owner", waterMaterial, new Vector3(1, 0, -.1f), new Vector2(2, 4));
            right.AddComponent<WaterUnderlayCaptureRequester>();
            try
            {
                foreach (bool perspective in new[] { false, true })
                foreach (float distance in new[] { 5f, 20f, 100f })
                {
                    string label = platform + " MSAA=" + samples + " perspective=" + perspective + " distance=" + distance;
                    camera.cameraType = perspective ? CameraType.SceneView : CameraType.Game;
                    camera.orthographic = !perspective; camera.orthographicSize = distance * .4f;
                    Quaternion rotation = perspective ? Quaternion.Euler(12, -24, 0) : Quaternion.identity;
                    camera.transform.SetPositionAndRotation(-(rotation * Vector3.forward) * distance, rotation);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    whole.SetActive(false); left.SetActive(false); right.SetActive(false);
                    var background = Render(camera, target, image);
                    int painted = 0; foreach (var pixel in background) if (pixel.r + pixel.g > 60) painted++;
                    Check(painted > 100, label + " negative-control geometry is visible");
                    whole.SetActive(true); waterMaterial.SetFloat("_DistortionStrength", 0);
                    var identity = Render(camera, target, image);
                    File.WriteAllBytes("Library/Native2DRegressions/Identity_" + platform + ".png", image.EncodeToPNG());
                    Debug.Log(label + " identityMismatch=" + Different(background, identity));
                    Check(Different(background, identity) == 0, label + " zero distortion is an exact copy");
                    waterMaterial.SetFloat("_DistortionStrength", .02f);
                    var unsplit = Render(camera, target, image);
                    if (distance <= 20)
                        Check(Different(background, unsplit) > 0, label + " actual refraction is drawn");
                    whole.SetActive(false); left.SetActive(true); right.SetActive(true);
                    var split = Render(camera, target, image);
                    Check(Different(unsplit, split) <= 2, label + " field is continuous across separate owners");
                    if (distance == 20)
                        File.WriteAllBytes("Library/Native2DRegressions/Water_" + platform + "_" + perspective + ".png", image.EncodeToPNG());
                    // A previous colored frame must not survive a SceneView pan/zoom.
                    camera.transform.position = new Vector3(10000, 10000, -distance);
                    if (perspective) camera.clearFlags = CameraClearFlags.Nothing;
                    var empty = Render(camera, target, image);
                    int residual = 0; foreach (var pixel in empty) if (pixel.r + pixel.g + pixel.b > 5) residual++;
                    Check(residual == 0, label + " pan to empty space clears prior color/depth");
                }
            }
            finally
            {
                camera.targetTexture = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(gradient);
                Object.DestroyImmediate(backgroundMaterial); Object.DestroyImmediate(waterMaterial);
            }
        }

        private static GameObject Quad(string name, Material material, Vector3 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.Load<Mesh>("Planar/UnitQuad");
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.sortingOrder = material.shader.name.Contains("Screen Distortion") ? 100 : 0;
            go.transform.position = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
            return go;
        }
        private static Color32[] Render(Camera camera, RenderTexture target, Texture2D image)
        {
            camera.Render(); var old = RenderTexture.active; RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = old; return image.GetPixels32();
        }
        private static int Different(Color32[] a, Color32[] b)
        {
            int count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 4) count++;
            return count;
        }
    }
}
#endif
