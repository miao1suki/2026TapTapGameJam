using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.GridArt.Editor
{
    public static class GridArtParallaxAudit
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe.");
            int checks = 0;
            void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException(message); }
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
                var output = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GridArtScene>(true)).Single();
                Check(output.map != null && output.map.layers.Count == 4, "TA four layers and map reference retained");
                var map = output.map;
                Check(map.layers.Sum(l => l.cells.Count) == 2424, "TA authored cells unchanged");
                foreach (var renderer in output.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null, "art material and shader available");
                    Check(renderer.GetComponent<MeshFilter>().sharedMesh != null, "generated art mesh available");
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    var slices = new System.Collections.Generic.List<Vector4>(); mesh.GetUVs(1,slices);
                    Check(slices.Count == mesh.vertexCount, "existing Gaming generated mesh includes per-quad sampling bounds");
                    Check(renderer.sharedMaterial.GetTexture("_BaseMap") != null, "art source texture available");
                }
                var camera = new GameObject("audit camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                // These are runtime-only callbacks; opening a Scene in EditMode does not dispatch them.
                typeof(GridArtScene).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(output, null);
                foreach (var renderer in output.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.gameObject.layer == 0) continue; // Neutral art does not enter HSV masks.
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                    Check(properties.GetFloat("_GridArtSliceClamp") == 1, "Gaming alpha mask uses the same slice clamp");
                }
                output.SetCamera(camera);
                var update = typeof(GridArtScene).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                update.Invoke(output, null);
                for (int pass = 0; pass < 3; pass++)
                foreach (float travel in new[] { 100f, -100f, 0f })
                {
                    camera.transform.position = new Vector3(travel, travel, -10); update.Invoke(output, null);
                    foreach (var layer in map.layers)
                    {
                        var root = Enumerable.Range(0, output.transform.childCount).Select(i => output.transform.GetChild(i))
                            .Single(t => t.name.StartsWith("美术层_" + layer.id + " · ", StringComparison.Ordinal));
                        var expected = new Vector3(0, 0, layer.depth) + GridArtScene.LayerOffset(new Vector3(travel, travel, 0), layer, map.cellSize, 16);
                        Check((root.localPosition - expected).sqrMagnitude < .000001f, "real Gaming layer follows bounded pixel-aligned target: " + layer.label + " actual=" + root.localPosition + " expected=" + expected + " parent=" + output.transform.position);
                        if (layer.depth > 0) Check(root.localPosition.y == 0 && Mathf.Abs(root.localPosition.x) <= layer.maxParallaxOffset.x, "background locked vertically and within authored limit");
                    }
                }
                camera.transform.position = new Vector3(100, 100, -10); update.Invoke(output, null);
                typeof(GridArtScene).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(output, null);
                foreach (Transform root in output.transform)
                    Check(root.localPosition.x == 0 && root.localPosition.y == 0, "disable restores authored rest position");
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                string result = "PASS " + checks + ": Gaming art references, bounded axes, return path and no accumulated drift; scene not saved";
                File.WriteAllText("Library/GridArtParallaxAudit.txt", result); Debug.Log(result); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/GridArtParallaxAudit.txt", "FAIL " + checks + "\n" + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
