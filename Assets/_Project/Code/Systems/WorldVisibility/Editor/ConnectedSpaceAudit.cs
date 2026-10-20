using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Project.Mechanisms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Project.WorldVisibility.Editor
{
    public static class ConnectedSpaceAudit
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/ConnectedSpaceProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only use the disposable ConnectedSpaceProbe.");
            int checks = 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var camera = new GameObject("连通空间审计相机").AddComponent<Camera>();
                camera.transform.position = new Vector3(2, 0, -10); camera.orthographic = true; camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white; camera.allowHDR = false;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                var viewer = new GameObject("观察者").transform;
                var system = camera.gameObject.AddComponent<WorldVisibility2D>();
                system.targetCamera = camera; system.viewer = viewer; system.findPlayer = false; system.previewInEditor = true;
                system.sightRadiusCells = 12; system.maskResolution = .5f;
                var left = Wall("可绕行墙", new Vector2(1, 0), new Vector2(.5f, 4));
                var target = new RenderTexture(800, 500, 24); camera.targetTexture = target; camera.aspect = 1.6f;
                var image = new Texture2D(800, 500, TextureFormat.RGBA32, false);
                Capture("Connected_Corner.png");
                Check(Sample(new Vector2(3, 0)) > .98f, "space behind corner is fully visible through surrounding passage");
                Check(Sample(new Vector2(-1, 0)) > .98f, "original brightness retained near player");
                var right = Wall("右墙", new Vector2(5, 0), new Vector2(.5f, 4));
                var top = Wall("顶墙", new Vector2(3, 2), new Vector2(4.5f, .5f));
                var bottom = Wall("底墙", new Vector2(3, -2), new Vector2(4.5f, .5f));
                Capture("Connected_Sealed.png");
                Check(Sample(new Vector2(3, 0)) < .01f, "sealed adjacent room stays hidden");
                for (int i = 0; i < 32; i++)
                {
                    var lamp = new GameObject("未参与合成的光源").AddComponent<VisibilityLight2D>();
                    lamp.transform.position = new Vector3(3, 0, 0); lamp.radiusCells = 20; lamp.intensity = 2;
                }
                Capture("Connected_SealedWithLights.png");
                Check(Sample(new Vector2(3, 0)) < .01f, "many lamps cannot reveal a sealed room");
                foreach (var lamp in Object.FindObjectsByType<VisibilityLight2D>(FindObjectsSortMode.None))
                {
                    var polygon = (VisibilityGeometry.Polygon)typeof(VisibilityLight2D).GetField("polygon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lamp);
                    Check(polygon.Mesh == null, "no mesh built for extra light in connected mode");
                }
                int built = system.ConnectedBuildCount;
                for (int i = 0; i < 100; i++) system.Prepare(camera);
                Check(system.ConnectedBuildCount == built, "stationary frames do not rebuild flood or mesh");
                viewer.position = new Vector3(.1f, .1f, 0); system.Prepare(camera);
                Check(system.ConnectedBuildCount == built, "sub-cell motion reuses mesh");
                left.enabled = false; Capture("Connected_Open.png");
                Check(Sample(new Vector2(3, 0)) > .98f, "opening passage reveals entire interior");
                left.enabled = true; Capture("Connected_ClosedAgain.png");
                Check(Sample(new Vector2(3, 0)) < .01f, "closing passage hides room immediately");
                left.transform.position = new Vector3(1, 1.25f, 0); left.size = new Vector3(.5f, 1.5f, 1);
                var lower = Wall("一格通路下墙", new Vector2(1, -1.25f), new Vector2(.5f, 1.5f));
                Capture("Connected_OneCellGap.png");
                Check(Sample(new Vector2(3, 0)) > .98f, "one-cell passage connects the whole room");
                Object.DestroyImmediate(lower.gameObject); left.transform.position = new Vector3(1, 0, 0); left.size = new Vector3(.5f, 4, 1);
                viewer.position = new Vector3(3, 0, 0); Capture("Connected_Teleported.png");
                Check(Sample(new Vector2(3, 0)) > .98f && Sample(Vector2.zero) < .01f, "teleport immediately changes connected component");
                viewer.position = Vector3.zero;
                // Replace left wall occluder with the actual DoorController registration path.
                Object.DestroyImmediate(left.GetComponent<VisibilityOccluder2D>());
                var door = left.gameObject.AddComponent<DoorController>();
                WorldVisibility2D.InvalidateAll(); Capture("Connected_DoorClosed.png");
                Check(Sample(new Vector2(3, 0)) < .01f, "locked real door blocks connectivity");
                typeof(DoorController).GetProperty("State").SetValue(door, DoorState.Unlocked);
                Capture("Connected_DoorOpen.png");
                Check(Sample(new Vector2(3, 0)) > .98f, "unlocked door restores connectivity without cache invalidation");
                typeof(DoorController).GetProperty("State").SetValue(door, DoorState.Locked);
                Capture("Connected_DoorClosedAgain.png");
                Check(Sample(new Vector2(3, 0)) < .01f, "relocked door restores occlusion");
                Object.DestroyImmediate(door); Object.DestroyImmediate(left.gameObject);
                Object.DestroyImmediate(right.gameObject); Object.DestroyImmediate(top.gameObject); Object.DestroyImmediate(bottom.gameObject);
                system.sightRadiusCells = 4;
                Capture("Connected_Range.png");
                Check(Sample(new Vector2(1, 0)) > .98f && Sample(new Vector2(5, 0)) < .01f, "bounded range retains full brightness inside and hides exterior");
                float fade = Sample(new Vector2(3.9f, .25f));
                Check(fade > .02f && fade < .95f, "outer range has gradual fade");
                var previousPipeline = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                camera.GetComponent<UniversalAdditionalCameraData>().SetRenderer(0);
                Capture("Connected_MobileRenderer.png");
                Check(Sample(new Vector2(1, 0)) > .98f && Sample(new Vector2(5, 0)) < .01f, "Mobile URP renderer preserves brightness and occlusion");
                Check(SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Render), "R8 render target supported on validation GPU");
                QualitySettings.renderPipeline = previousPipeline;
                camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                double formalCold = 0, formalWarm = 0, formalRebuild = 0; int segments = 0, cells = 0, actualLights = 0;
                foreach (var path in new[] { "Assets/_Project/Scenes/Playing/Gaming.unity", "Assets/_Project/Development/MapVisibilityTest/MapVisibilityTest.unity" })
                {
                    EditorSceneManager.OpenScene(path);
                    system = Object.FindFirstObjectByType<WorldVisibility2D>();
                    Check(system && system.connectedSpaceVisibility && system.viewer && system.targetCamera, "actual scene wired to connected mode");
                    // Formal scene starts in the menu's perspective pose; Side2D is applied by gameplay flow.
                    system.targetCamera.orthographic = true;
                    var timer = Stopwatch.StartNew(); system.Prepare(system.targetCamera); timer.Stop();
                    if (path.Contains("/Playing/"))
                    {
                        formalCold = timer.Elapsed.TotalMilliseconds; segments = system.CachedSegments; cells = system.ConnectedCellCount;
                        actualLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                        timer.Restart(); for (int i = 0; i < 200; i++) system.Prepare(system.targetCamera);
                        timer.Stop(); formalWarm = timer.Elapsed.TotalMilliseconds / 200;
                        Vector3 position = system.viewer.position;
                        var nextBuild = typeof(WorldVisibility2D).GetField("nextConnectedBuild", BindingFlags.NonPublic | BindingFlags.Instance);
                        timer.Restart();
                        for (int i = 1; i <= 20; i++)
                        {
                            system.viewer.position = position + new Vector3(i * .5f, 0, 0);
                            nextBuild.SetValue(system, 0f); system.Prepare(system.targetCamera);
                        }
                        timer.Stop(); formalRebuild = timer.Elapsed.TotalMilliseconds / 20;
                        system.viewer.position = position; nextBuild.SetValue(system, 0f); system.Prepare(system.targetCamera);
                    }
                    Check(system.SightMesh && system.ConnectedCellCount > 0, "actual scene has reachable space mesh");
                    int builds = system.ConnectedBuildCount; system.Prepare(system.targetCamera);
                    Check(system.ConnectedBuildCount == builds, "actual scene caches stationary topology");
                    camera = system.targetCamera; system.previewInEditor = true;
                    camera.transform.position = system.viewer.position + new Vector3(0, 1, -10);
                    camera.orthographicSize = 8; camera.aspect = 1.6f;
                    target = new RenderTexture(800, 500, 24); image = new Texture2D(800, 500, TextureFormat.RGBA32, false);
                    camera.targetTexture = target;
                    Capture(path.Contains("/Playing/") ? "Connected_Formal.png" : "Connected_ManualTest.png");
                    Check(WorldVisibilityRendererFeature.LastExecutedFrame == Time.frameCount, "actual scene camera executes connected GPU composition");
                    camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                }
                var shader = Shader.Find("Hidden/2026TapTap/WorldVisibility2D");
                Check(shader && !ShaderUtil.ShaderHasError(shader), "shader compiled without errors");
                string report = $"PASS {checks} assertions. Actual PC+Mobile URP/D3D11 GPU: around corner/full original brightness/sealed room/32 unused lamps/one-cell passage/open-close/teleport/door/range fade. Formal scene {segments} segments, {cells} reachable samples, {actualLights} Unity Light components. CPU cold Prepare={formalCold:F3}ms, cached Prepare={formalWarm:F3}ms, moving rebuild={formalRebuild:F3}ms in local Editor; not Android GPU or whole-frame performance. No scenes saved.\n";
                File.WriteAllText("Library/ConnectedSpaceAudit.txt", report); Debug.Log(report); EditorApplication.Exit(0);

                BoxCollider Wall(string name, Vector2 position, Vector2 size)
                {
                    var box = new GameObject(name).AddComponent<BoxCollider>(); box.transform.position = position;
                    box.size = new Vector3(size.x, size.y, 1); box.gameObject.AddComponent<VisibilityOccluder2D>().shapes = new[] { box }; return box;
                }
                void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
                void Capture(string name)
                {
                    camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); RenderTexture.active = previous;
                    File.WriteAllBytes("Library/" + name, image.EncodeToPNG());
                }
                float Sample(Vector2 world)
                {
                    var p = camera.WorldToViewportPoint(world);
                    return image.GetPixel(Mathf.Clamp((int)(p.x * image.width), 0, image.width - 1), Mathf.Clamp((int)(p.y * image.height), 0, image.height - 1)).r;
                }
            }
            catch (Exception error) { Debug.LogException(error); File.WriteAllText("Library/ConnectedSpaceAudit.txt", "FAIL " + error); EditorApplication.Exit(1); }
        }
    }
}
