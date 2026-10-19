using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Project.Fireflies.Editor
{
    public static class FireflyIntegrationAudit
    {
        private const string Directory = "Assets/ta_source/firefly/";
        private const string TestScene = Directory + "Editor/__FireflyAuditScene.unity";
        private static int checks;
        private static readonly List<string> report = new List<string>();
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(FireflyRegionWindow.PrefabPath);
        private static FireflyField Spawn() => ((GameObject)PrefabUtility.InstantiatePrefab(Prefab)).GetComponent<FireflyField>();

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("萤火虫验证仅可在静音 Library/FlowRetirementProbe 隔离工程执行。");
            int code = 0;
            bool saved = false;
            checks = 0;
            report.Clear();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assets(); GeometryAndCache(); LightsAndMotion(); Inspector(); Gpu();
                Check(!File.Exists(TestScene), "temporary Scene path is unused");
                Persistence(ref saved);
                report.Add("PASS " + checks + " firefly assertions; GPU=" + SystemInfo.graphicsDeviceType);
            }
            catch (Exception error)
            {
                code = 1; report.Add(error.ToString()); Debug.LogException(error);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (saved) AssetDatabase.DeleteAsset(TestScene);
                File.WriteAllLines("Library/FireflyIntegrationAudit.txt", report);
            }
            EditorApplication.Exit(code);
        }

        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("FIREFLY: " + label);
            checks++; report.Add("PASS " + label); Debug.Log("FIREFLY PASS: " + label);
        }

        private static void Assets()
        {
            Check(Prefab != null && Prefab.GetComponent<FireflyField>() != null, "TA prefab and script resolve");
            Check(Prefab.layer == 0 && Prefab.GetComponent<ColorObject>() == null && Prefab.GetComponent<Collider>() == null &&
                Prefab.GetComponent<Rigidbody>() == null, "warm neutral decoration has no color unlock, physics or interaction");
            var data = new SerializedObject(Prefab.GetComponent<FireflyField>());
            Check(data.FindProperty("pixelSize") == null && data.FindProperty("realtimeLightCount").intValue == 0,
                "hard pixel size cannot drift; realtime lights default to zero");
            Material[] materials = Prefab.GetComponent<MeshRenderer>().sharedMaterials;
            Check(materials.Length == 2 && materials.All(material => material != null && material.shader.name == "TapTap/Firefly/Soft Pixel Light" && !ShaderUtil.ShaderHasError(material.shader)),
                "two shared TA materials compile without modifying renderer assets");
            Check(Near(materials[0].GetFloat("_Softness"), 1) && Near(materials[1].GetFloat("_Softness"), 0), "halo stays soft while core stays a solid square");
            Check(!File.Exists(Directory + "FireflyPreview.unity") && !File.Exists(Directory + "FireflyPreviewThreat.cs") &&
                !File.Exists(Directory + "FireflyPreviewActor.mat"), "preview Scene, threat actor and reference materials excluded");
        }

        private static void GeometryAndCache()
        {
            FireflyField first = Spawn(), second = Spawn();
            try
            {
                first.Configure(new Vector2(3, 2), 22, 107, 293, 521);
                second.Configure(new Vector2(3, 2), 22, 107, 293, 521);
                Check(first.Count == 22 && first.GeneratedMesh.vertexCount == 176 && first.GeneratedMesh.subMeshCount == 2,
                    "each fly is exactly two quads in one bounded mesh");
                Check((first.GeneratedMesh.hideFlags & HideFlags.DontSave) != 0, "generated mesh cannot enter Scene serialization");
                Check(first.GeneratedMesh.vertices.SequenceEqual(second.GeneratedMesh.vertices) &&
                    first.GeneratedMesh.colors32.SequenceEqual(second.GeneratedMesh.colors32), "three author seeds are independent of instanceID");
                Mesh identity = first.GeneratedMesh;
                int revision = first.GeometryRevision;
                for (int frame = 0; frame < 120; frame++) first.SendMessage("Update");
                Check(first.GeneratedMesh == identity && first.GeometryRevision == revision, "120 idle editor updates perform no mesh upload or rebuild");
                Vector3[] before = first.GeneratedMesh.vertices;
                first.transform.position = new Vector3(3, -2, 0);
                first.SendMessage("Update");
                Check(first.GeneratedMesh == identity && first.GeometryRevision == revision + 1 &&
                    before.Zip(first.GeneratedMesh.vertices, (a, b) => (a - b).sqrMagnitude < .000001f).All(value => value),
                    "translation refreshes once and never reshuffles layout");
                first.transform.localScale = new Vector3(2, .5f, 3);
                first.transform.rotation = Quaternion.Euler(0, 0, 37);
                first.SendMessage("Update");
                Pixels(first, 1);
                var parent = new GameObject("Firefly audit grid");
                try
                {
                    parent.AddComponent<LevelEditorPlacedBlock>().SetCellWorldSize(2);
                    first.transform.SetParent(parent.transform, true);
                    first.RefreshPreview();
                    Check(Near(first.CellWorldSize, 2), "custom grid provider is respected");
                    Pixels(first, 2);
                    first.transform.SetParent(null, true);
                }
                finally { Object.DestroyImmediate(parent); }
                first.RefreshPreview();
                first.Configure(new Vector2(3, 2), 999, 107, 293, 521);
                Check(first.Count == 128 && first.GeneratedMesh.vertexCount == 1024 && first.transform.childCount == 0,
                    "128-fly cap requires no per-fly GameObjects");
                first.Configure(new Vector2(3, 2), 0, 107, 293, 521);
                Check(first.GeneratedMesh.vertexCount == 0 && first.Count == 0, "zero count creates no visual geometry");
                var randomState = UnityEngine.Random.state;
                UnityEngine.Random.InitState(29); float expected = UnityEngine.Random.value;
                UnityEngine.Random.InitState(29); first.RefreshPreview(); float actual = UnityEngine.Random.value;
                UnityEngine.Random.state = randomState;
                Check(expected == actual, "independent seed streams do not mutate Unity global random state");
                identity = second.GeneratedMesh;
                second.enabled = false;
                Check(identity == null && second.GetComponent<MeshFilter>().sharedMesh == null, "disable releases generated mesh");
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first.gameObject);
                if (second != null) Object.DestroyImmediate(second.gameObject);
            }
        }

        private static void Pixels(FireflyField field, float cell)
        {
            Vector3[] vertices = field.GeneratedMesh.vertices;
            float pixel = GridArtworkMetrics.PixelWorldSize(cell);
            bool valid = true;
            for (int v = 4; v < vertices.Length; v += 8)
            {
                Vector3 a = field.transform.TransformPoint(vertices[v]), b = field.transform.TransformPoint(vertices[v + 1]);
                Vector3 c = field.transform.TransformPoint(vertices[v + 2]);
                valid &= Near(b.x - a.x, pixel) && Near(b.y - a.y, 0) && Near(c.y - a.y, pixel) && Near(c.x - a.x, 0);
                valid &= Near(a.x / pixel, Mathf.Round(a.x / pixel)) && Near(a.y / pixel, Mathf.Round(a.y / pixel));
            }
            Check(valid, "every hard point is one world-grid pixel, even with nonuniform scale and rotation; cell=" + cell);
        }

        private static void LightsAndMotion()
        {
            FireflyField first = Spawn(), second = Spawn();
            var threat = new GameObject("Firefly audit approaching actor");
            try
            {
                first.Configure(new Vector2(3, 2), 128, 107, 293, 521);
                second.Configure(new Vector2(3, 2), 128, 107, 293, 521);
                first.SetThreat(threat.transform); second.SetThreat(threat.transform);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Mesh identity = first.GeneratedMesh;
                Vector3[] before = first.GeneratedMesh.vertices;
                for (int frame = 0; frame < 60; frame++)
                {
                    first.SendMessage("Simulate", 1f / 60f); first.SendMessage("Draw");
                    second.SendMessage("Simulate", 1f / 60f); second.SendMessage("Draw");
                    first.SendMessage("UpdateLights");
                }
                watch.Stop();
                report.Add("128-fly motion helper, with threat, average " + (watch.Elapsed.TotalMilliseconds / 120).ToString("F3") + " ms per simulation/draw (editor audit includes SendMessage overhead).");
                Check(!before.SequenceEqual(first.GeneratedMesh.vertices) && first.GeneratedMesh == identity,
                    "wandering/fleeing moves the same mesh rather than rebuilding instances");
                Check(first.GeneratedMesh.vertices.SequenceEqual(second.GeneratedMesh.vertices), "equal seeds and actor yield repeatable motion");
                Check(first.ActiveLightCount == 0 && first.transform.childCount == 0, "default runtime light helper allocates no lights");
                var properties = new SerializedObject(first);
                properties.FindProperty("realtimeLightCount").intValue = 12;
                properties.ApplyModifiedPropertiesWithoutUndo();
                first.RefreshPreview();
                first.SendMessage("UpdateLights");
                Check(first.ActiveLightCount == 4 && first.transform.childCount == 4, "explicit light budget is hard capped at four");
                Light[] pool = first.GetComponentsInChildren<Light>();
                for (int frame = 0; frame < 120; frame++) first.SendMessage("UpdateLights");
                Check(pool.SequenceEqual(first.GetComponentsInChildren<Light>()) && pool.All(light => light.shadows == LightShadows.None && (light.gameObject.hideFlags & HideFlags.DontSave) != 0),
                    "120 light updates reuse the same no-shadow, nonpersistent objects");
                properties.Update(); properties.FindProperty("realtimeLightCount").intValue = 0;
                properties.ApplyModifiedPropertiesWithoutUndo(); first.SendMessage("UpdateLights");
                Check(first.ActiveLightCount == 0 && first.transform.childCount == 0 && pool.All(light => light == null), "turning off lights releases the entire pool");
                properties.Update(); properties.FindProperty("realtimeLightCount").intValue = 2;
                properties.ApplyModifiedPropertiesWithoutUndo(); first.SendMessage("UpdateLights");
                properties.Update(); properties.FindProperty("haloMaterial").objectReferenceValue = null;
                properties.ApplyModifiedPropertiesWithoutUndo(); first.RefreshPreview();
                Check(first.GeneratedMesh == null && first.ActiveLightCount == 0, "missing material cannot leave stale glowing lights behind");
            }
            finally
            {
                Object.DestroyImmediate(threat); Object.DestroyImmediate(first.gameObject); Object.DestroyImmediate(second.gameObject);
            }
        }

        private static void Inspector()
        {
            FireflyField field = Spawn();
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(field, typeof(FireflyFieldEditor));
            VisualElement root = null;
            try
            {
                root = editor.CreateInspectorGUI();
                Check(root != null && root.Query<IMGUIContainer>().ToList().Count == 0 && root.Query<PropertyField>().ToList().Count == 30,
                    "all 29 component properties plus script use Chinese native UI Toolkit");
                Check(root.Query<Button>().ToList().Any(button => button.text == "打开萤火虫区域创建"), "Inspector has visible creation entry");
            }
            finally { root?.Unbind(); Object.DestroyImmediate(editor); Object.DestroyImmediate(field.gameObject); }
        }

        private static void Persistence(ref bool saved)
        {
            Undo.IncrementCurrentGroup();
            FireflyField field = FireflyRegionWindow.CreateRegion(new Vector2(5, 3), 29, 113, 331, 557, new Vector3(2.25f, -1, 1));
            Undo.FlushUndoRecordObjects();
            Check(field.transform.parent == null && field.gameObject.scene == EditorSceneManager.GetActiveScene(), "created region is outside planning generated roots");
            Undo.PerformUndo(); Check(field == null, "creation Undo removes the whole region");
            Undo.PerformRedo();
            field = EditorSceneManager.GetActiveScene().GetRootGameObjects().Select(go => go.GetComponent<FireflyField>()).Single(value => value != null);
            field.RefreshPreview();
            Check(field.Count == 29 && field.LayoutSeed == 113 && field.MotionSeed == 331 && field.PulseSeed == 557,
                "creation Redo preserves configuration");
            Vector3[] expected = field.GeneratedMesh.vertices;
            Check(EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), TestScene), "authored region saves as a Scene Prefab override");
            saved = true;
            Check(!File.ReadAllText(TestScene).Contains("Firefly field (generated)"), "Scene contains no transient mesh");
            EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            field = EditorSceneManager.GetActiveScene().GetRootGameObjects().Select(go => go.GetComponent<FireflyField>()).Single(value => value != null);
            field.RefreshPreview();
            Check(field.AreaCells == new Vector2(5, 3) && field.Count == 29 && field.LayoutSeed == 113 && field.MotionSeed == 331 && field.PulseSeed == 557 &&
                field.transform.position == new Vector3(2.25f, -1, 1) && expected.SequenceEqual(field.GeneratedMesh.vertices), "Scene reload preserves all seeds, bounds, position and exact static layout");
            Check(field.gameObject.layer == 0 && field.GetComponent<ColorObject>() == null && field.ActiveLightCount == 0 && field.transform.parent == null,
                "Scene reload remains a neutral standalone decoration");
        }

        private static void Gpu()
        {
            Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "actual GPU available; no -nographics");
            FireflyField field = Spawn();
            var grid = field.gameObject.AddComponent<LevelEditorPlacedBlock>();
            try
            {
                foreach (float cell in new[] { 1f, 2f })
                {
                    grid.SetCellWorldSize(cell);
                    field.transform.localScale = new Vector3(2, .5f, 3);
                    field.transform.rotation = Quaternion.Euler(0, 0, 37);
                    field.Configure(new Vector2(.1f, .1f), 1, 107, 293, 521);
                    const int size = 512;
                    var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) { antiAliasing = 1 };
                    var command = new CommandBuffer { name = "Firefly hard-pixel audit" };
                    RenderTexture previous = RenderTexture.active;
                    Texture2D read = null;
                    try
                    {
                        target.Create();
                        command.SetRenderTarget(target); command.ClearRenderTarget(true, true, Color.clear);
                        command.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-2, 2, -2, 2, -1, 1), true));
                        command.DrawMesh(field.GeneratedMesh, field.transform.localToWorldMatrix, field.GetComponent<MeshRenderer>().sharedMaterials[1], 1, 0);
                        Graphics.ExecuteCommandBuffer(command);
                        RenderTexture.active = target;
                        read = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
                        read.ReadPixels(new Rect(0, 0, size, size), 0, 0); read.Apply();
                        Color[] pixels = read.GetPixels();
                        int visible = 0, minX = size, minY = size, maxX = -1, maxY = -1;
                        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                        {
                            if (pixels[y * size + x].a <= .000001f) continue;
                            visible++; minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                        }
                        int side = Mathf.RoundToInt(8 * cell);
                        Check(visible == side * side && maxX - minX + 1 == side && maxY - minY + 1 == side && minX % side == 0 && minY % side == 0,
                            "GPU core is exactly one16th-cell, axis-aligned, native grid pixel under root scale/rotation; cell=" + cell);
                        var png = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                        png.SetPixels(pixels); png.Apply();
                        File.WriteAllBytes("Library/Firefly_HardPixel_Cell" + cell + ".png", png.EncodeToPNG());
                        Object.DestroyImmediate(png);
                    }
                    finally
                    {
                        RenderTexture.active = previous; command.Release(); target.Release(); Object.DestroyImmediate(target);
                        if (read != null) Object.DestroyImmediate(read);
                    }
                }
            }
            finally { Object.DestroyImmediate(field.gameObject); }
        }
    }
}
