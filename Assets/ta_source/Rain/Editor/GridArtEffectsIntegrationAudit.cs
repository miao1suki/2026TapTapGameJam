using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.ArtEffects;
using Project.ColorBlocks;
using Project.Fireflies;
using Project.GridArt.Editor;
using Project.LevelEditor;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Project.Rain.Editor
{
    public static class GridArtEffectsIntegrationAudit
    {
        private static int checks;
        private static string scenePath;
        private static readonly List<Object> scratch = new List<Object>();
        private static readonly List<string> report = new List<string>();

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Use the silent isolated probe project only.");
            int exit = 0; checks = 0; report.Clear();
            scenePath = "Assets/__GridArtEffectsAudit_" + Guid.NewGuid().ToString("N") + ".unity";
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Authoring(scene); GpuForeground();
                report.Add("PASS " + checks + " workbench TA region checks; GPU=" + SystemInfo.graphicsDeviceType);
            }
            catch (Exception error) { exit = 1; report.Add(error.ToString()); Debug.LogException(error); }
            finally
            {
                foreach (Object value in scratch) if (value != null) Object.DestroyImmediate(value);
                scratch.Clear();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null) AssetDatabase.DeleteAsset(scenePath);
                File.WriteAllLines("Library/GridArtEffectsIntegrationAudit.txt", report);
            }
            EditorApplication.Exit(exit);
        }

        private static void Check(bool valid, string name)
        {
            checks++; if (!valid) throw new InvalidOperationException("GRID ART EFFECTS: " + name);
            report.Add("PASS " + name);
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        private static bool Same(Rect a, Rect b) => (a.position - b.position).sqrMagnitude < .000001f && (a.size - b.size).sqrMagnitude < .000001f;

        private static void Authoring(Scene scene)
        {
            Check(GridArtEffectsSource.Available, "asmdef workbench delegates are installed by the TA editor adapter");
            var rainSettings = new GridArtEffectSettings { kind = GridArtEffectKind.Rain, rainColor = 2, rainDensity = 100f };
            var rain = (RainSpriteField)GridArtEffectsSource.Create(rainSettings, new RectInt(-4, -3, 8, 6), 2f);
            Check(rain.transform.parent == null && rain.transform.position == new Vector3(0, 0, 1), "rain is an independent Scene root and keeps its background depth");
            Check(rain.AreaCells == new Vector2(8, 6) && Near(rain.CellWorldSize, 2) && rain.DropCount == 512,
                "painted bounds are 8x6 cells, custom grid survives without parent scaling, 512 drops cap");
            Check(rain.ColorId == "green" && rain.GetComponent<ColorObject>() == null && rain.GetComponent<RoomResetObject>() == null &&
                rain.GetComponent<LevelEditorPlacedBlock>() == null && rain.GetComponent<Collider>() == null,
                "rain has visual color ownership but no planning object, room reset, ability or collision");
            Vector3[] rv = rain.GeneratedMesh.vertices;
            Check(Near(Vector3.Distance(rain.transform.TransformPoint(rv[0]), rain.transform.TransformPoint(rv[1])), 2f),
                "a 16px rain frame remains one full custom-grid cell instead of being fitted to a region thumbnail");
            var fireflySettings = new GridArtEffectSettings { kind = GridArtEffectKind.Fireflies, fireflyCount = 999 };
            var field = (FireflyField)GridArtEffectsSource.Create(fireflySettings, new RectInt(2, 1, 5, 3), 2f);
            Check(field.Count == 128 && field.ActiveLightCount == 0 && field.gameObject.layer == 0,
                "firefly region stays neutral with 128 cap and no realtime lights");
            Check(field.transform.position == new Vector3(9, 5, 0) && Near(field.RenderDepthCells, -.25f),
                "firefly area root remains on the level plane; only its render plane moves camera-side");
            Check(field.GetComponent<ArtEffectRegionGrid>() != null && field.GetComponent<Collider>() == null &&
                field.GetComponent<LevelEditorPlacedBlock>() == null && field.GetComponent<ColorObject>() == null,
                "standalone grid metadata does not turn decoration into a gameplay object");
            Vector3[] fv = field.GeneratedMesh.vertices;
            Check(Near(Vector3.Distance(field.transform.TransformPoint(fv[4]), field.transform.TransformPoint(fv[5])), 2f / 16f) &&
                Near(fv[0].z, fv[4].z) && Near(fv[4].z, -.5f), "hard core is one native pixel and halo/core share the custom-grid visible plane");
            var regions = GridArtEffectsSource.Read(scene);
            Check(regions.Count == 2 && Same(regions[0].worldBounds, new Rect(-8, -6, 16, 12)) &&
                Same(regions[1].worldBounds, new Rect(4, 2, 10, 6)), "workbench previews full world bounds rather than a one-cell dense effect stamp");
            int revision = field.GeometryRevision; Mesh identity = field.GeneratedMesh;
            for (int index = 0; index < 120; index++) GridArtEffectsSource.Read(scene);
            Check(field.GeometryRevision == revision && field.GeneratedMesh == identity, "reading region bounds never simulates or rebuilds TA effects");
            var editor = GridArtEffectsSource.BuildInspector(field, () => { });
            Check(editor != null && editor.Query<IMGUIContainer>().ToList().Count == 0 && editor.Q<Vector3Field>() != null,
                "workbench has its own native inspector and position editing without Hierarchy selection");
            editor.Unbind();
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Check(field == null && GridArtEffectsSource.Read(scene).Count == 1, "one Undo removes the whole newly painted firefly region");
            Undo.PerformRedo();
            field = scene.GetRootGameObjects().Select(value => value.GetComponent<FireflyField>()).Single(value => value != null);
            field.RefreshPreview();
            Check(field.Count == 128 && field.GetComponent<ArtEffectRegionGrid>() != null && Near(field.CellWorldSize, 2f),
                "Redo restores TA parameters and grid metadata as one authored action");
            field.gameObject.SetActive(false);
            Check(GridArtEffectsSource.Read(scene).Count == 2, "disabled authored regions remain selectable in the workbench");
            field.gameObject.SetActive(true);
            Check(EditorSceneManager.SaveScene(scene, scenePath), "regions save in Scene, independently of a GridArtMap or planning document");
            string serialized = File.ReadAllText(scenePath);
            Check(!serialized.Contains("Firefly field (generated)") && !serialized.Contains("Rain Sprite Field (generated)"),
                "transient TA mesh data is not serialized into the Scene");
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            scene = SceneManager.GetActiveScene(); regions = GridArtEffectsSource.Read(scene);
            field = scene.GetRootGameObjects().Select(value => value.GetComponent<FireflyField>()).Single(value => value != null);
            field.RefreshPreview();
            Check(regions.Count == 2 && field.Count == 128 && Near(field.CellWorldSize, 2) && Near(field.RenderDepthCells, -.25f),
                "save/reload keeps authored region bounds, fixed seeds, custom grid and visible depth");
            Object.DestroyImmediate(field.gameObject);
            foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        }

        private static void GpuForeground()
        {
            Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "actual GPU is available for wall-occlusion regression");
            var field = (FireflyField)GridArtEffectsSource.Create(new GridArtEffectSettings { kind = GridArtEffectKind.Fireflies,
                fireflyCount = 1 }, new RectInt(0, 0, 1, 1), 1f);
            field.transform.position = Vector3.zero;
            field.Configure(new Vector2(.1f, .1f), 1, 107, 293, 521);
            var cameraObject = new GameObject("Effect GPU view"); scratch.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 30f; camera.aspect = 1;
            var spriteShader = Shader.Find("Project/Objects/Pixel Sprite Reveal");
            var wallShader = Shader.Find("Universal Render Pipeline/Unlit");
            Check(spriteShader != null && wallShader != null, "production native-sprite and opaque-wall shaders exist");
            var ordinary = new Material(spriteShader); scratch.Add(ordinary);
            ordinary.SetTexture("_MainTex", Texture2D.whiteTexture); ordinary.SetColor("_Color", new Color(.1f, .15f, .2f, 1));
            var wall = new Material(wallShader); scratch.Add(wall); wall.SetColor("_BaseColor", new Color(.2f, .2f, .2f, 1));
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); scratch.Add(cube);
            Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); scratch.Add(quad);
            Mesh quadMesh = quad.GetComponent<MeshFilter>().sharedMesh;
            Color[] ordinaryBase = Capture(camera, field, ordinary, wall, quadMesh, cubeMesh, false, false);
            Color[] ordinaryWithFirefly = Capture(camera, field, ordinary, wall, quadMesh, cubeMesh, false, true);
            Color[] wallBase = Capture(camera, field, ordinary, wall, quadMesh, cubeMesh, true, false);
            Color[] wallWithFirefly = Capture(camera, field, ordinary, wall, quadMesh, cubeMesh, true, true);
            float foregroundDifference = Difference(ordinaryBase, ordinaryWithFirefly);
            float wallDifference = Difference(wallBase, wallWithFirefly);
            Check(foregroundDifference > .1f, "GPU: core and halo overlay ordinary transparent props in the production sprite shader");
            Check(wallDifference < .0001f, "GPU: opaque wall front face blocks both firefly submeshes; no Always ZTest or through-wall sorting");
            Object.DestroyImmediate(field.gameObject);
        }

        private static float Difference(Color[] a, Color[] b)
        {
            float sum = 0f;
            for (int i = 0; i < a.Length; i++) sum += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
            return sum;
        }

        private static Color[] Capture(Camera camera, FireflyField field, Material ordinary, Material wall,
            Mesh quad, Mesh cube, bool hasWall, bool hasFirefly)
        {
            const int size = 256;
            var texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) { antiAliasing = 1 };
            var command = new CommandBuffer { name = "TA workbench wall / prop depth regression" };
            RenderTexture previous = RenderTexture.active; Texture2D read = null;
            try
            {
                texture.Create(); command.SetRenderTarget(texture); command.ClearRenderTarget(true, true, Color.clear);
                command.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
                if (hasWall) command.DrawMesh(cube, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(2, 2, 1)), wall, 0, 0);
                command.DrawMesh(quad, Matrix4x4.TRS(new Vector3(0, 0, -.515f), Quaternion.identity, new Vector3(2, 2, 1)), ordinary, 0, 0);
                if (hasFirefly)
                {
                    var materials = field.GetComponent<MeshRenderer>().sharedMaterials;
                    command.DrawMesh(field.GeneratedMesh, field.transform.localToWorldMatrix, materials[0], 0, 0);
                    command.DrawMesh(field.GeneratedMesh, field.transform.localToWorldMatrix, materials[1], 1, 0);
                }
                Graphics.ExecuteCommandBuffer(command); RenderTexture.active = texture;
                read = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0); read.Apply(); return read.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous; command.Release(); texture.Release(); Object.DestroyImmediate(texture);
                if (read != null) Object.DestroyImmediate(read);
            }
        }
    }
}
