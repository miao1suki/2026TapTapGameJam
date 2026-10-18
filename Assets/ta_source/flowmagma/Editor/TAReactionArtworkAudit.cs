using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.BlockFeatures;
using Project.BurnEffects;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.FlowMagma.Editor
{
    /// <summary>Disposable real-Play lifecycle, exact coverage and real-GPU alpha tests.</summary>
    public static class TAReactionArtworkAudit
    {
        private const string Flag = "TAReactionArtworkAudit.Running";
        private static int phase, checks;
        private static double deadline;
        private static float phaseTime;
        private static RoomLifecycleManager manager;
        private static LavaHazardFeature lava;
        private static PlantObstacleFeature first, next, diagonal;
        private static BouncePlantFeature spring;
        private static ClimbableVineFeature vine;
        private static FlowMagmaVisual flow;
        private static Material plantMaterial;
        private static int previousEmissions;
        private static Camera sceneCamera;
        private static RenderTexture sceneTarget;
        private static GameObject Spawn(string name, Vector3 position, Action<GameObject> configure = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/" + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing prefab " + name);
            var staging = new GameObject("TA reaction inactive authoring");
            staging.SetActive(false);
            GameObject item = Object.Instantiate(prefab, position, Quaternion.identity, staging.transform);
            configure?.Invoke(item);
            var member = item.GetComponent<RoomResetObject>() ?? item.AddComponent<RoomResetObject>();
            item.transform.SetParent(null, true);
            manager.Register(member);
            Object.Destroy(staging);
            return item;
        }
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException("TA REACTION: " + message);
            Debug.Log("TA REACTION PASS: " + message);
        }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Only run in the silent isolated probe, with a real graphics device.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = checks = 0; deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(new TimeoutException("Phase " + phase)); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
            try
            {
                if (phase == 0)
                {
                    AudioListener.volume = 0; AudioListener.pause = true;
                    Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "real GPU available");
                    foreach (var entry in new[] {
                        ("TapTap/FlowMagma/Flowing Body", "Assets/ta_source/flowmagma/FlowMagma.shader"),
                        ("TapTap/FlowMagma/Square Particles", "Assets/ta_source/flowmagma/MagmaSplash.shader"),
                        ("TapTap/Steam/岩浆冷却蒸汽", "Assets/ta_source/Steam/CoolingSteam.shader"),
                        ("Project/Objects/Pixel Sprite Reveal", "Assets/_Project/Code/Systems/ObjectArtwork/Runtime/WorldObjectSprite.shader"),
                        ("Hidden/2026TapTap/ColorObjectMask", "Assets/_Project/Code/Systems/ColorBlocks/Shaders/ColorObjectMask.shader") })
                    {
                        // New assets need an explicit load before Shader.Find is a valid discovery test.
                        Shader loaded = AssetDatabase.LoadAssetAtPath<Shader>(entry.Item2);
                        Shader shader = Shader.Find(entry.Item1);
                        bool pass = false;
                        if (shader != null)
                        {
                            var material = new Material(shader);
                            pass = material.SetPass(0); Object.Destroy(material);
                        }
                        string diagnostics = shader == null ? "no Shader.Find result" :
                            string.Join(" | ", ShaderUtil.GetShaderMessages(shader).Select(message =>
                                message.severity + ": " + message.message + " (" + message.file + ":" + message.line + ")"));
                        Debug.Log("TA shader diagnostic: expected=" + entry.Item1 + ", asset=" +
                            (loaded == null ? "NULL" : loaded.name) + ", found=" + (shader == null ? "NULL" : shader.name) +
                            ", supported=" + (shader != null && shader.isSupported) + ", pass=" + pass + ", messages=" + diagnostics);
                        Check(loaded != null && loaded == shader && shader.isSupported && pass && !ShaderUtil.ShaderHasError(shader),
                            "shader compiles: " + entry.Item1 + "; " + diagnostics);
                    }
                    var settings = ReactionArtworkSettings.Load();
                    Check(settings != null && settings.magmaPrefab != null && settings.coolingSteamPrefab != null, "Resources fallback and prefab dependencies exist");
                    var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/ta_source/flowmagma/0-5 (1).png");
                    Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.maxTextureSize == 256, "magma atlas is point sampled and minimally capped");
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    manager = new GameObject("TA reaction reset test").AddComponent<RoomLifecycleManager>();
                    var region = new RoomRegion { id = "ta", label = "TA" };
                    foreach (Vector2Int cell in new RectInt(-20,-20,50,40).allPositionsWithin) region.cells.Add(cell);
                    manager.Configure(new List<RoomRegion> { region }, 1, "");
                    sceneCamera = new GameObject("TA real visible magma camera").AddComponent<Camera>();
                    sceneCamera.gameObject.AddComponent<AudioListener>();
                    sceneCamera.transform.position = new Vector3(-6,0,-10);
                    sceneCamera.orthographic = true; sceneCamera.orthographicSize = 4;
                    sceneCamera.allowHDR = false; sceneCamera.allowMSAA = false;
                    sceneCamera.clearFlags = CameraClearFlags.SolidColor; sceneCamera.backgroundColor = Color.black;
                    // Batchmode has no GameView backbuffer. An actual camera target makes URP
                    // submit real scene frames, including culling/Renderer.isVisible; manual
                    // DrawRenderer silhouette probes alone do not exercise that lifecycle.
                    sceneTarget = new RenderTexture(256,256,24,RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                    sceneTarget.Create(); sceneCamera.targetTexture = sceneTarget;
                    lava = Spawn("Red_Lava", new Vector3(-6,0,0), item =>
                    {
                        item.transform.localScale = new Vector3(3,2,1);
                        item.AddComponent<LevelEditorMergedShape>().Configure(new Vector2Int(3,2),
                            new[] { new RectInt(0,0,1,2), new RectInt(2,0,1,2), new RectInt(1,1,1,1) });
                    }).GetComponent<LavaHazardFeature>();
                    first = Spawn("Green_PlantObstacle", Vector3.zero).GetComponent<PlantObstacleFeature>();
                    next = Spawn("Green_PlantObstacle", Vector3.right).GetComponent<PlantObstacleFeature>();
                    diagonal = Spawn("Green_PlantObstacle", new Vector3(2,1,0)).GetComponent<PlantObstacleFeature>();
                    spring = Spawn("Green_JumpPlant", new Vector3(4,0,0)).GetComponent<BouncePlantFeature>();
                    vine = Spawn("Green_Ladder", new Vector3(7,0,0)).GetComponent<ClimbableVineFeature>();
                    phase = 1; phaseTime = Time.time;
                }
                else if (phase == 1 && Time.time - phaseTime > .55f)
                {
                    ColorAppearanceManager.Instance.Play("red", true, 0f);
                    ColorAppearanceManager.Instance.Play("green", true, 0f);
                    flow = CurrentLiveFlow();
                    Check(flow != null && flow.SurfaceCellCount == 3 && flow.ParticleCapacity == 96, "merged magma uses one bounded pool and three exposed surface cells");
                    Check(Mathf.Approximately(flow.FlowSpeed,.28f) && Mathf.Approximately(flow.WaveAmplitude,.008f) &&
                        Mathf.Approximately(flow.VerticalWarpStrength,.006f), "formal prefab uses TA subtle warp without standalone Scene speed overrides");
                    Check(Mathf.Approximately(flow.EmissionIntervalRandomness,.75f) && Mathf.Approximately(flow.ClusterChance,.22f) &&
                        Mathf.Approximately(flow.ShrinkingParticleChance,.55f) && Mathf.Approximately(flow.LargeParticleChance,.22f) &&
                        Mathf.Approximately(flow.TrajectoryRandomness,.6f), "latest random intervals, bursts, size tiers and trajectory parameters are active");
                    Check(flow.GetComponent<MeshFilter>().sharedMesh.vertexCount == 20, "five covered cells, no enclosing-rectangle fill");
                    Check(flow.GetComponentsInChildren<MeshRenderer>().Length == 2 && flow.GetComponentsInChildren<MeshRenderer>().All(x => x.enabled), "one body and one splash renderer remain enabled");
                    Check(!lava.GetComponent<MechanismArtworkPresentation>().Visual.enabled, "static lava sprite is not double-rendered");
                    Check(Mathf.Abs(flow.transform.lossyScale.x - 1) < .001 && Mathf.Abs(flow.transform.lossyScale.y - 1) < .001, "merged parent scale cannot enlarge artwork pixels twice");
                    Check(lava.GetComponent<LevelEditorMergedShape>().Volumes.All(x => x.enabled && x.isTrigger), "visual hookup preserves dangerous merged triggers");
                    foreach (var item in new[] { first, next, diagonal })
                    {
                        var presentation = item.GetComponent<MechanismArtworkPresentation>();
                        Check(presentation != null && presentation.Visual != null && presentation.Visual.sprite != null,
                            "runtime obstacle prefab installs formal 2D artwork without a SceneLoaded rescan");
                    }
                    plantMaterial = first.GetComponent<MechanismArtworkPresentation>().Visual.sharedMaterial;
                    Check(first.BreakObstacle(), "plant group accepts red burn");
                    Check(!first.IsIntact && !next.IsIntact && diagonal.IsIntact, "four-neighbour burn still excludes diagonal contact");
                    Check(first.ShowsArtwork && first.GetComponent<PlantBurnEffectPlayer>().IsPlaying, "broken plant displays TA burn while collision is off");
                    Check(first.GetComponentsInChildren<Collider>().All(x => !x.enabled), "burning obstacle has no blocking collision");
                    Check(first.GetComponent<MechanismArtworkPresentation>().Visual.sharedMaterial == plantMaterial, "burn preserves shared reveal material identity");
                    Check(spring.NormalRiseCells == 8 && spring.HighRiseCells == 8 && spring.CarryRiseCells == 4, "spring tuning is unchanged");
                    Check(spring.ApplyColor("red",null) && !spring.IsBounceSurfaceAvailable && !spring.CanBeGreenHooked(null), "burning spring cannot bounce or act as a hook anchor");
                    vine.BeginBurn();
                    Check(vine.IsBurning && vine.GetComponent<PlantBurnEffectPlayer>().IsPlaying, "vine adds burn without replacing its owned-root sequence");
                    phase = 2; phaseTime = Time.time;
                }
                else if (phase == 2 && Time.time - phaseTime > .12f)
                {
                    GpuPair(flow.GetComponent<MeshRenderer>(), new Vector2(-6,0), "TA_magma");
                    GpuPair(first.GetComponent<MechanismArtworkPresentation>().Visual, Vector2.zero, "TA_plant_burn");
                    VerifyPaletteAndSolidCenter();
                    Check(lava.ApplyColor("blue",null), "blue lava reaction succeeds");
                    Check(!lava.gameObject.activeSelf, "authored lava is dormant, not destroyed");
                    var steam = Object.FindObjectsByType<Project.Steam.CoolingSteamEffect>(FindObjectsSortMode.None);
                    Check(steam.Length == 3, "cooling emits only at real exposed cells, with bounded count");
                    foreach (var item in steam)
                    {
                        Check(Mathf.Abs(item.CellWorldSize - 1) < .001 && Mathf.Abs(item.transform.lossyScale.x - 1) < .001,
                            "cooling cell size is independent of merged replacement dimensions");
                        Check(item.GetComponentsInChildren<ParticleSystem>().Length == 3, "TA cooling cloud/core/crown are present");
                        foreach (var part in item.GetComponentsInChildren<ParticleSystemRenderer>())
                        {
                            var properties = new MaterialPropertyBlock(); part.GetPropertyBlock(properties);
                            Check(Mathf.Abs(properties.GetFloat("_SteamPixelWorldSize") - 1f/16) < .0001, "steam uses world-grid pixel sampling");
                        }
                    }
                    manager.ResetRegion("ta");
                    Check(lava != null && lava.gameObject.activeSelf && first.IsIntact && next.IsIntact && spring.IsBounceSurfaceAvailable,
                        "reset during active effects restores author gameplay");
                    Check(!first.GetComponent<PlantBurnEffectPlayer>().IsPlaying && !spring.GetComponent<PlantBurnEffectPlayer>().IsPlaying,
                        "reset cancels burning callbacks and property overrides");
                    phase = 3; phaseTime = Time.time;
                }
                else if (phase == 3 && Time.time - phaseTime > .3f)
                {
                    Check(Object.FindObjectsByType<Project.Steam.CoolingSteamEffect>(FindObjectsSortMode.None).Length == 0, "reset removes replacement-owned steam");
                    Check(lava.GetComponentsInChildren<FlowMagmaVisual>().Length == 1, "reset keeps exactly one live flow renderer");
                    flow = CurrentLiveFlow();
                    Check(!vine.IsBurned && !vine.IsBurning, "root burn state is cleared by room reset");
                    Check(first.BreakObstacle() && spring.ApplyColor("red",null), "effects can be triggered again after reset");
                    phase = 4; phaseTime = Time.time;
                }
                else if (phase == 4 && Time.time - phaseTime > 1.6f)
                {
                    Check(!first.ShowsArtwork && !next.ShowsArtwork && diagonal.IsIntact, "finished group burn hides only selected connected plants");
                    Check(!spring.gameObject.activeSelf, "finished spring is dormant for room restore");
                    manager.ResetRegion("ta");
                    Check(first.IsIntact && next.IsIntact && diagonal.IsIntact && spring.gameObject.activeSelf,
                        "finished burn and a second reset restore the whole authored group");
                    // Reset restores feature attachments, retiring their old display children.
                    // Rebind before restarting; a deferred-Destroy component still permits access
                    // to its managed counter but is not the live renderer in subsequent frames.
                    flow = CurrentLiveFlow();
                    flow.Restart(); phase = 5; phaseTime = Time.time;
                }
                else if (phase == 5 && Time.time - phaseTime > 1.62f)
                {
                    Check(flow != null && flow == CurrentLiveFlow(), "emission probe tracks the currently attached restored lava renderer");
                    VerifySceneCameraFrame();
                    Check(flow.EmittedParticleCount > 0, "visible exact-coverage magma actually emits pooled particles");
                    previousEmissions = flow.EmittedParticleCount; phase = 6; phaseTime = Time.time;
                }
                else if (phase == 6 && Time.time - phaseTime > .3f)
                {
                    Check(flow.EmittedParticleCount > previousEmissions, "continuous emission continues inside the obsolete 1.5–2.4 second silent window");
                    VerifyParticlePixels();
                    GpuPair(flow.GetComponentsInChildren<MeshRenderer>().First(item => item.gameObject != flow.gameObject),
                        new Vector2(-6,0), "TA_solid_particles");
                    Finish(null);
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static FlowMagmaVisual CurrentLiveFlow()
        {
            var live = lava.GetComponentsInChildren<FlowMagmaVisual>().Where(item => item.isActiveAndEnabled).ToArray();
            Check(lava.gameObject.activeInHierarchy && lava.HasFlowArtwork && live.Length == 1,
                "authored lava owns exactly one active current flow after lifecycle attachment");
            return live[0];
        }

        private static void VerifySceneCameraFrame()
        {
            var body = flow.GetComponent<MeshRenderer>();
            var properties = new MaterialPropertyBlock(); body.GetPropertyBlock(properties);
            Debug.Log("TA real scene diagnostic: frame=" + Time.frameCount + ", flowActive=" +
                flow.isActiveAndEnabled + ", bodyEnabled=" + body.enabled + ", bodyVisible=" + body.isVisible +
                ", bounds=" + body.bounds + ", cameraActive=" + sceneCamera.isActiveAndEnabled +
                ", targetCreated=" + sceneTarget.IsCreated() + ", layer=" + body.gameObject.layer +
                ", cameraMask=" + sceneCamera.cullingMask + ", globalReveal=" +
                Shader.GetGlobalVector("_ColorAppearanceRealAlpha") + ", materialReveal=" +
                body.sharedMaterial.GetFloat("_ColorRevealOpacity") + ", propertyReveal=" + properties.GetFloat("_ColorRevealOpacity") +
                ", emissions=" + flow.EmittedParticleCount);
            Check(sceneCamera.isActiveAndEnabled && sceneTarget.IsCreated() && body.isVisible,
                "real scene camera culls the formal exact-coverage magma as visible");
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(sceneTarget.width,sceneTarget.height,TextureFormat.RGBA32,false,true);
            try
            {
                RenderTexture.active = sceneTarget;
                image.ReadPixels(new Rect(0,0,sceneTarget.width,sceneTarget.height),0,0); image.Apply();
                int visible = image.GetPixels32().Count(pixel => pixel.r > 12 || pixel.g > 12 || pixel.b > 12);
                Check(visible > 100, "actual URP camera frame contains visible magma rather than an unrendered backbuffer; pixels=" + visible);
                File.WriteAllBytes("Library/TA_magma_scene_camera.png",image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        private static void VerifyParticlePixels()
        {
            var particle = flow.GetComponentsInChildren<MeshFilter>().First(item => item.gameObject != flow.gameObject);
            var mesh = particle.sharedMesh; var positions = mesh.vertices; var colors = mesh.colors32;
            int visible = 0; float pixel = 1f / 16f;
            for (int i = 0; i < positions.Length; i += 4)
            {
                if (colors[i].a == 0) continue;
                visible++;
                Vector3 a = particle.transform.TransformPoint(positions[i]);
                Vector3 d = particle.transform.TransformPoint(positions[i + 3]);
                Check(Mathf.Abs(a.x / pixel - Mathf.Round(a.x / pixel)) < .0005f &&
                    Mathf.Abs(a.y / pixel - Mathf.Round(a.y / pixel)) < .0005f &&
                    Mathf.Abs(d.x / pixel - Mathf.Round(d.x / pixel)) < .0005f &&
                    Mathf.Abs(d.y / pixel - Mathf.Round(d.y / pixel)) < .0005f,
                    "solid particle edges align with integer world artwork pixels");
                Check(d.x - a.x >= pixel - .00001f && Mathf.Abs((d.x-a.x) - (d.y-a.y)) < .00001f,
                    "cooling and settled particles remain square and at least one complete artwork pixel");
            }
            Check(visible > 0 && visible <= 96, "emission produces visible particles without exceeding fixed capacity");
        }

        private static void VerifyPaletteAndSolidCenter()
        {
            var owner = new GameObject("TA four-stage real shader probe");
            var mesh = new Mesh { vertices = new[] { new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),
                new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0) },
                uv = new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one },
                triangles = new[] { 0,2,1,1,2,3 } };
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = flow.GetComponentsInChildren<MeshRenderer>().First(item => item.gameObject != flow.gameObject).sharedMaterial;
            foreach (string property in new[] { "_IgnitionColor","_FlameColor","_EmberColor","_CinderColor" })
                Check(renderer.sharedMaterial.HasProperty(property), "latest shader exposes four-stage cooling palette: " + property);
            float previousBrightness = float.MaxValue;
            try
            {
                foreach (float age in new[] { 0f,.18f,.62f,1f })
                {
                    var color = new Color32((byte)Mathf.RoundToInt(age * 255),255,255,255);
                    mesh.colors32 = new[] { color,color,color,color };
                    var image = Render(renderer,renderer.sharedMaterial,Vector2.zero);
                    try
                    {
                        Color center = image.GetPixel(256,256);
                        Check(center.a > .99f, "latest particle has a solid center, not an obsolete hollow ring; age=" + age);
                        float brightness = center.r + center.g + center.b;
                        Check(brightness < previousBrightness - .01f, "actual GPU palette darkens through the four cooling stages; age=" + age);
                        previousBrightness = brightness;
                    }
                    finally { Object.DestroyImmediate(image); }
                }
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(mesh); }
        }

        private static Texture2D Render(Renderer renderer, Material material, Vector2 center)
        {
            var target = new RenderTexture(512,512,24,RenderTextureFormat.ARGBFloat) { antiAliasing = 1 };
            var previous = RenderTexture.active;
            var command = new CommandBuffer { name = "TA artwork color/mask" };
            try
            {
                target.Create(); command.SetRenderTarget(target); command.ClearRenderTarget(true,true,Color.clear);
                command.SetGlobalVector("_ColorAppearanceRealAlpha",Vector4.one);
                command.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(
                    Matrix4x4.Ortho(center.x-2,center.x+2,center.y-2,center.y+2,-4,4),true));
                command.DrawRenderer(renderer,material,0,0); Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = target;
                var texture = new Texture2D(512,512,TextureFormat.RGBAFloat,false,true);
                texture.ReadPixels(new Rect(0,0,512,512),0,0); texture.Apply(); return texture;
            }
            finally { RenderTexture.active = previous; command.Release(); target.Release(); Object.DestroyImmediate(target); }
        }
        private static void GpuPair(Renderer renderer, Vector2 center, string label)
        {
            var maskMaterial = new Material(Shader.Find("Hidden/2026TapTap/ColorObjectMask"));
            Vector4 previous = Shader.GetGlobalVector("_ColorAppearanceRealAlpha");
            var color = Render(renderer, renderer.sharedMaterial, center);
            var mask = Render(renderer, maskMaterial, center);
            try
            {
                var a = color.GetPixels(); var b = mask.GetPixels(); int visible = 0, difference = 0;
                for (int i = 0; i < a.Length; i++) { if (a[i].a > .00000001f) visible++; if ((a[i].a > .00000001f) != (b[i].a > .00000001f)) difference++; }
                Check(visible > 100 && difference <= 2, label + " GPU color and HSV silhouettes agree; differing pixels=" + difference);
                foreach (var pair in new[] { (color,label+"_color"), (mask,label+"_mask") })
                {
                    var image = new Texture2D(512,512,TextureFormat.RGBA32,false,true);
                    image.SetPixels(pair.Item1.GetPixels()); image.Apply();
                    File.WriteAllBytes("Library/"+pair.Item2+".png",image.EncodeToPNG()); Object.DestroyImmediate(image);
                }
            }
            finally { Shader.SetGlobalVector("_ColorAppearanceRealAlpha",previous); Object.DestroyImmediate(maskMaterial); Object.DestroyImmediate(color); Object.DestroyImmediate(mask); }
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Flag,false);
            if (sceneCamera != null) sceneCamera.targetTexture = null;
            if (sceneTarget != null) { sceneTarget.Release(); Object.DestroyImmediate(sceneTarget); sceneTarget = null; }
            string report = error == null ? "PASS " + checks + " TA reaction artwork/runtime/GPU assertions" : "FAIL phase="+phase+" checks="+checks+"\n"+error;
            File.WriteAllText("Library/TAReactionArtworkAudit.txt", report); Debug.Log(report);
            if (error != null) Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
