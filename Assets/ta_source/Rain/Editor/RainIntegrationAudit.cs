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

namespace Project.Rain.Editor
{
    /// <summary>Silent, disposable-project rain asset, persistence and actual GPU mask audit.</summary>
    public static class RainIntegrationAudit
    {
        private const string Directory = "Assets/ta_source/Rain/";
        private const string TestScene = Directory + "Editor/__RainAuditScene.unity";
        private static int checks;
        private static readonly List<string> results = new List<string>();

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("雨区审计仅可在静音 Library/FlowRetirementProbe 隔离工程执行。");
            int code = 0;
            checks = 0;
            results.Clear();
            bool saved = false;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assets();
                GeometryAndSeed();
                Inspector();
                Gpu();
                Check(!File.Exists(TestScene), "audit scene path is unused");
                Persistence(ref saved);
                results.Add("PASS " + checks + " rain assertions; GPU=" + SystemInfo.graphicsDeviceType);
            }
            catch (Exception error)
            {
                code = 1;
                results.Add(error.ToString());
                Debug.LogException(error);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (saved) AssetDatabase.DeleteAsset(TestScene);
                File.WriteAllLines("Library/RainIntegrationAudit.txt", results);
            }
            EditorApplication.Exit(code);
        }

        private static void Check(bool success, string label)
        {
            if (!success) throw new InvalidOperationException("RAIN: " + label);
            checks++;
            results.Add("PASS " + label);
            Debug.Log("RAIN PASS: " + label);
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        private static GameObject Prefab(string suffix) => AssetDatabase.LoadAssetAtPath<GameObject>(Directory + "Rain_" + suffix + ".prefab");
        private static RainSpriteField Spawn(string suffix) => ((GameObject)PrefabUtility.InstantiatePrefab(Prefab(suffix))).GetComponent<RainSpriteField>();

        private static void Assets()
        {
            string path = Directory + "471dca30b9bcb2527867589ad677e3e3.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Check(importer != null && importer.textureType == TextureImporterType.Default, "atlas is Default, not Sprite or a particle import");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Check(width == 447 && height == 336 && importer.maxTextureSize == 512, "source447x336 remains intact with max512");
            Check(!importer.mipmapEnabled && importer.filterMode == FilterMode.Point && importer.wrapMode == TextureWrapMode.Clamp &&
                importer.npotScale == TextureImporterNPOTScale.None && importer.textureCompression == TextureImporterCompression.Uncompressed,
                "point/clamp/uncompressed/noMip/noNPOTresizing");
            foreach (string platform in new[] { "Standalone", "Android", "WindowsStoreApps" })
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                Check(!settings.overridden || settings.maxTextureSize == 512 && settings.textureCompression == TextureImporterCompression.Uncompressed,
                    platform + " cannot downsample or compress the atlas");
            }
            Check(!File.Exists(Directory + "RainPreview.unity") && !File.Exists(Directory + "RainPreview.unity.meta"), "TA preview Scene excluded");
            var catalog = Resources.Load<ColorCatalog>("ColorBlocks/ColorCatalog");
            Check(catalog != null, "ColorCatalog is available");
            foreach (string suffix in new[] { "Red", "Green", "Blue" })
            {
                GameObject prefab = Prefab(suffix);
                Check(prefab != null && prefab.GetComponentsInChildren<RainSpriteField>(true).Length == 1, suffix + " has one field");
                Check(prefab.transform.childCount == 0 && prefab.GetComponent<Collider>() == null && prefab.GetComponent<ColorObject>() == null,
                    suffix + " is decoration only, with no children or gameplay components");
                Check(prefab.layer == catalog.Find(suffix.ToLowerInvariant()).unityLayer, suffix + " prefab already has its catalog color Layer");
                Material material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
                Check(material != null && material.shader.name == "TapTap/Rain/Sprite Field" && !ShaderUtil.ShaderHasError(material.shader), suffix + " material and Shader resolve");
                Check(material.mainTexture != null && material.mainTexture.width == 447 && material.mainTexture.height == 336,
                    suffix + " imports the native atlas without resizing");
            }
            foreach (RainSpriteField.RainColor color in Enum.GetValues(typeof(RainSpriteField.RainColor)))
            {
                for (int frame = 0; frame < 4; frame++)
                {
                    Rect uv = RainSpriteField.FrameUV(color, frame);
                    int row = color == RainSpriteField.RainColor.Red ? 160 : color == RainSpriteField.RainColor.Blue ? 224 : 288;
                    Check(Near(uv.width * 447, 16) && Near(uv.height * 336, 16) &&
                        Near(uv.xMin * 447, 320 + frame % 2 * 32) && Near((1 - uv.yMax) * 336, row + frame / 2 * 32),
                        color + " frame" + frame + " addresses exactly16x16 source pixels");
                }
                Check(RainSpriteField.FrameUV(color, -1) == RainSpriteField.FrameUV(color, 3), color + " frame indices wrap safely");
            }
        }

        private static void GeometryAndSeed()
        {
            RainSpriteField first = Spawn("Blue"), second = Spawn("Blue");
            try
            {
                first.Configure(RainSpriteField.RainColor.Blue, new Vector2(8, 6), 18, 119);
                second.Configure(RainSpriteField.RainColor.Blue, new Vector2(8, 6), 18, 119);
                Check(first.DropCount == 144 && first.GeneratedMesh.vertexCount == 576, "one mesh contains width*density quads");
                Check(first.GeneratedMesh.vertices.SequenceEqual(second.GeneratedMesh.vertices) &&
                    first.GeneratedMesh.colors32.SequenceEqual(second.GeneratedMesh.colors32), "same author seed ignores different instanceIDs");
                Vector3[] before = first.GeneratedMesh.vertices;
                first.RefreshPreview();
                Check(before.SequenceEqual(first.GeneratedMesh.vertices), "refresh/save-reload seed is deterministic");
                Mesh identity = first.GeneratedMesh;
                first.transform.position = new Vector3(3, -2, 0);
                first.SendMessage("Update");
                Check(identity == first.GeneratedMesh && before.Zip(first.GeneratedMesh.vertices, (a, b) => (a - b).sqrMagnitude < .000001f).All(value => value),
                    "translation updates positions without rebuilding or re-randomizing");
                first.Configure(RainSpriteField.RainColor.Blue, new Vector2(8, 6), 18, 120);
                Check(!before.SequenceEqual(first.GeneratedMesh.vertices), "changing author seed changes the distribution");
                first.transform.localScale = new Vector3(2, .5f, 1);
                first.RefreshPreview();
                CheckPixels(first, 1);
                var parent = new GameObject("Rain audit grid owner");
                try
                {
                    parent.AddComponent<LevelEditorPlacedBlock>().SetCellWorldSize(2);
                    first.transform.SetParent(parent.transform, true);
                    first.RefreshPreview();
                    Check(Near(first.CellWorldSize, 2), "parent grid provider supplies the cell size");
                    CheckPixels(first, 2);
                    first.transform.SetParent(null, true);
                }
                finally { Object.DestroyImmediate(parent); }
                first.RefreshPreview();
                var block = new MaterialPropertyBlock();
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Check(Near(block.GetFloat("_ColorMaskGroup"), -1) && Near(block.GetFloat("_ColorMaskReal"), 1), "rain uses decoration HSV, not unlock handoff");
                Check(Near(block.GetFloat("_ColorMaskUseVertexAlpha"), 1) && Near(block.GetFloat("_ColorMaskOpacity"), 1) &&
                    block.GetTexture("_ColorMaskAlphaTexture") == first.GetComponent<MeshRenderer>().sharedMaterial.mainTexture,
                    "mask receives atlas alpha and per-droplet vertex alpha");
                Check((first.GetComponent<MeshRenderer>().renderingLayerMask & 128u) == 0, "native source pixels bypass second pixelation");
                UnityEngine.Random.State randomState = UnityEngine.Random.state;
                UnityEngine.Random.InitState(491);
                float expectedRandom = UnityEngine.Random.value;
                UnityEngine.Random.InitState(491);
                first.RefreshPreview();
                float actualRandom = UnityEngine.Random.value;
                UnityEngine.Random.state = randomState;
                Check(expectedRandom == actualRandom, "author seed does not mutate Unity global RNG");
                first.Configure(RainSpriteField.RainColor.Green, new Vector2(100, 6), 100, 37);
                Check(first.DropCount == 512 && first.GeneratedMesh.vertexCount == 2048, "512 cap bounds CPU/mesh cost");
                identity = first.GeneratedMesh;
                for (int frame = 0; frame < 60; frame++) first.SendMessage("Animate", 1f / 60f);
                Check(first.GeneratedMesh == identity && first.transform.childCount == 0, "motion reuses one mesh with no droplet objects");
                first.Configure(RainSpriteField.RainColor.Red, new Vector2(8, 6), 0, 37);
                Check(first.DropCount == 0 && first.GeneratedMesh.vertexCount == 0, "zero density creates no geometry");
                identity = second.GeneratedMesh;
                second.enabled = false;
                Check(identity == null && second.GetComponent<MeshFilter>().sharedMesh == null, "disable disposes transient mesh");
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first.gameObject);
                if (second != null) Object.DestroyImmediate(second.gameObject);
            }
        }

        private static void CheckPixels(RainSpriteField rain, float cell)
        {
            Vector3[] positions = rain.GeneratedMesh.vertices;
            bool correct = true;
            float pixel = GridArtworkMetrics.PixelWorldSize(cell);
            for (int v = 0; v < positions.Length; v += 4)
            {
                Vector3 a = rain.transform.TransformPoint(positions[v]), b = rain.transform.TransformPoint(positions[v + 1]);
                Vector3 c = rain.transform.TransformPoint(positions[v + 2]);
                correct &= Near(Vector3.Distance(a, b), cell) && Near(Vector3.Distance(a, c), cell);
                correct &= Near(a.x / pixel, Mathf.Round(a.x / pixel)) && Near(a.y / pixel, Mathf.Round(a.y / pixel));
            }
            Check(correct, "all droplets have identical16px cell geometry and snapped world pixels at cell=" + cell);
        }

        private static void Inspector()
        {
            RainSpriteField rain = Spawn("Green");
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(rain, typeof(RainSpriteFieldEditor));
            VisualElement root = null;
            try
            {
                root = editor.CreateInspectorGUI();
                Check(root != null && root.Query<IMGUIContainer>().ToList().Count == 0 && root.Query<PropertyField>().ToList().Count >= 14,
                    "Chinese Inspector is native UI Toolkit, with only valid properties");
                Check(root.Query<Button>().ToList().Any(button => button.text == "打开雨区创建"), "Inspector exposes region creation entry");
            }
            finally
            {
                root?.Unbind();
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(rain.gameObject);
            }
        }

        private static void Persistence(ref bool saved)
        {
            Undo.IncrementCurrentGroup();
            RainSpriteField rain = RainRegionWindow.CreateRegion(RainSpriteField.RainColor.Green, new Vector2(7, 5), 12, 817, new Vector3(1.25f, 3, 1));
            int group = Undo.GetCurrentGroup();
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            Check(rain.transform.parent == null && rain.gameObject.scene == EditorSceneManager.GetActiveScene(), "creation is a normal Scene root, outside planning generated roots");
            Undo.PerformUndo();
            Check(rain == null, "creation Undo removes the entire field");
            Undo.PerformRedo();
            rain = EditorSceneManager.GetActiveScene().GetRootGameObjects().Select(go => go.GetComponent<RainSpriteField>()).Single(value => value != null);
            Check(rain.Seed == 817 && rain.AreaCells == new Vector2(7, 5), "creation Redo retains authored parameters");
            rain.RefreshPreview();
            Vector3[] expected = rain.GeneratedMesh.vertices;
            Check(EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), TestScene), "Scene with rain saves");
            saved = true;
            Check(!File.ReadAllText(TestScene).Contains("Rain Sprite Field (generated)"), "generated mesh is excluded from Scene serialization");
            EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            rain = EditorSceneManager.GetActiveScene().GetRootGameObjects().Select(go => go.GetComponent<RainSpriteField>()).Single(value => value != null);
            rain.RefreshPreview();
            Check(rain.Seed == 817 && rain.AreaCells == new Vector2(7, 5) && rain.ColorId == "green" && rain.DropCount == 84,
                "saved Prefab overrides survive Scene reload");
            Check(rain.transform.position == new Vector3(1.25f, 3, 1) && expected.SequenceEqual(rain.GeneratedMesh.vertices), "Scene reload preserves position and exact seeded distribution");
            Check(rain.transform.parent == null && rain.GetComponent<Collider>() == null && rain.GetComponent<ColorObject>() == null,
                "Scene reload preserves decoration-only ownership");
        }

        private static Texture2D Render(Mesh mesh, Material material, MaterialPropertyBlock properties)
        {
            const int size = 128;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) { antiAliasing = 1 };
            var command = new CommandBuffer { name = "Rain pixel/mask audit" };
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                command.SetRenderTarget(target);
                command.ClearRenderTarget(true, true, Color.clear);
                command.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-.5f, .5f, -.5f, .5f, -1, 1), true));
                command.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0, properties);
                Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = target;
                var read = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                read.Apply();
                return read;
            }
            finally
            {
                RenderTexture.active = previous;
                command.Release();
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void Gpu()
        {
            Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "actual GPU required; do not use -nographics");
            var material = new Material(Prefab("Blue").GetComponent<MeshRenderer>().sharedMaterial);
            var maskMaterial = new Material(Shader.Find("Hidden/2026TapTap/ColorObjectMask"));
            var mesh = new Mesh { vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) }, triangles = new[] { 0, 2, 1, 2, 3, 1 } };
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_ColorMaskAlphaTexture", material.mainTexture);
            properties.SetFloat("_ColorMaskGroup", -1);
            properties.SetFloat("_ColorMaskReal", 1);
            properties.SetFloat("_ColorMaskUseVertexAlpha", 1);
            try
            {
                Check(maskMaterial.shader != null && !ShaderUtil.ShaderHasError(maskMaterial.shader), "project HSV mask shader compiles");
                foreach (RainSpriteField.RainColor color in Enum.GetValues(typeof(RainSpriteField.RainColor)))
                {
                    for (int frame = 0; frame < 4; frame++)
                    {
                        Rect uv = RainSpriteField.FrameUV(color, frame);
                        mesh.uv = new[] { uv.min, new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMin, uv.yMax), uv.max };
                        foreach (float vertexAlpha in new[] { 0, .4f, 1 })
                        {
                            material.SetColor("_Color", new Color(1, 1, 1, .7f));
                            properties.SetFloat("_ColorMaskOpacity", .7f);
                            Color32 tint = new Color(1, 1, 1, vertexAlpha);
                            mesh.colors32 = new[] { tint, tint, tint, tint };
                            Texture2D visible = Render(mesh, material, properties), mask = Render(mesh, maskMaterial, properties);
                            try
                            {
                                Color[] a = visible.GetPixels(), b = mask.GetPixels();
                                int populated = 0, differences = 0;
                                for (int pixel = 0; pixel < a.Length; pixel++)
                                {
                                    bool active = a[pixel].a > .000001f;
                                    if (active) populated++;
                                    if (active != (b[pixel].a > .000001f)) differences++;
                                }
                                string label = color + " frame" + frame + " alpha" + vertexAlpha;
                                Check(differences == 0 && (vertexAlpha == 0 ? populated == 0 : populated > 0), label + " texture/vertex/material alpha matches HSV silhouette exactly");
                                if (vertexAlpha != 1) continue;
                                int mixed = 0;
                                for (int y = 0; y < 128; y += 8) for (int x = 0; x < 128; x += 8)
                                {
                                    Color reference = a[y * 128 + x];
                                    bool differs = false;
                                    for (int iy = 0; iy < 8; iy++) for (int ix = 0; ix < 8; ix++)
                                    {
                                        Color actual = a[(y + iy) * 128 + x + ix];
                                        differs |= Mathf.Abs(actual.r - reference.r) > .001f || Mathf.Abs(actual.g - reference.g) > .001f ||
                                            Mathf.Abs(actual.b - reference.b) > .001f || Mathf.Abs(actual.a - reference.a) > .001f;
                                    }
                                    if (differs) mixed++;
                                }
                                int authoredPixelCount = frame == 0 ? 13 : frame == 1 ? 11 : frame == 2 ? 8 : 9;
                                Check(mixed == 0 && populated == authoredPixelCount * 64, label + " uses uniform8x8 screen pixels with no adjacent-frame leakage");
                                if (frame == 0)
                                {
                                    var png = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
                                    png.SetPixels(a); png.Apply();
                                    File.WriteAllBytes("Library/Rain_" + color + "_pixels.png", png.EncodeToPNG());
                                    Object.DestroyImmediate(png);
                                }
                            }
                            finally { Object.DestroyImmediate(visible); Object.DestroyImmediate(mask); }
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); Object.DestroyImmediate(maskMaterial); }
        }
    }
}
