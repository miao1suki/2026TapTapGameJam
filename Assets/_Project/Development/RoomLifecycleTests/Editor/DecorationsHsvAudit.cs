using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.ColorBlocks;
using Project.GridArt;
using Project.GridArt.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real URP readback of saved workbench output. Restricted to the isolated probe project.</summary>
public static class DecorationsHsvAudit
{
    private const string Flag = "DecorationsHsvAudit.Running";
    private static readonly Color Background = new Color(.18f, .08f, .33f, 1);
    private static int phase, checks, requestedFrame;
    private static double started;
    private static float deadline;
    private static RenderTexture target;
    private static Camera camera;
    private static string failure;
    private static Color[] priorColors;
    private static readonly List<Texture2D> captures = new List<Texture2D>();

    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException("DECORATIONS HSV: " + message);
        checks++; Debug.Log("DECORATIONS HSV PASS: " + message);
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("此验证仅可在隔离工程执行，不在作者工程生成资产。");
        checks = 0;
        string folder = "Assets/_Project/Development/Generated/DecorationsHsvAudit_" + Guid.NewGuid().ToString("N");
        GridArtBuilder.EnsureFolder(folder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string scenePath = folder + "/DecorationsHsvAudit.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        var palette = ScriptableObject.CreateInstance<GridArtPalette>();
        AssetDatabase.CreateAsset(palette, folder + "/Palette.asset");
        var ids = new[] { "red", "green", "blue" };
        var colors = new[] { new Color(1, .03f, .02f), new Color(.02f, 1, .03f), new Color(.03f, .02f, 1) };
        for (int i = 0; i < ids.Length; i++)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "Audit " + ids[i], filterMode = FilterMode.Point };
            var pixels = new Color[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                pixels[y * 16 + x] = x >= 6 && x < 10 && y >= 6 && y < 10 ? Color.clear : colors[i];
            texture.SetPixels(pixels); texture.Apply();
            AssetDatabase.CreateAsset(texture, folder + "/" + ids[i] + ".asset");
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16, 0, SpriteMeshType.FullRect);
            sprite.name = "Audit " + ids[i]; AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture); AssetDatabase.SaveAssetIfDirty(texture);
            palette.brushes.Add(new GridArtBrush { label = ids[i], sprite = sprite, colorId = ids[i] });
        }
        EditorUtility.SetDirty(palette); AssetDatabase.SaveAssetIfDirty(palette);
        var map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette = palette;
        var layer = new GridArtLayer { label = "三色与透明洞", depth = 0, parallax = Vector2.one };
        for (int i = 0; i < 3; i++) layer.cells.Add(new GridArtCell(new Vector2Int(i * 2, 0), i));
        map.layers.Add(layer); AssetDatabase.CreateAsset(map, folder + "/Map.asset");
        var catalog = Resources.Load<ColorCatalog>("ColorBlocks/ColorCatalog");
        if (catalog == null) throw new InvalidOperationException("缺少颜色目录。");
        var readColors = GridArtColorSource.Read;
        GridArtColorSource.Read = () => catalog.Colors.Where(c => c != null)
            .Select(c => new GridArtColorDefinition(c.id, c.displayName, c.unityLayer)).ToArray();
        try { GridArtBuilder.Apply(map, scene, false, true); }
        finally { GridArtColorSource.Read = readColors; }
        var view = new GameObject("Decorations Audit Camera").AddComponent<Camera>();
        view.tag = "MainCamera"; view.transform.position = new Vector3(2.5f, .5f, -10);
        view.orthographic = true; view.orthographicSize = 1.5f; view.nearClipPlane = .1f; view.farClipPlane = 50;
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Background;
        view.allowHDR = false; view.allowMSAA = false;
        var urp = view.GetUniversalAdditionalCameraData(); urp.renderPostProcessing = true;
        urp.requiresDepthOption = CameraOverrideOption.On;
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Check(Object.FindObjectsByType<GridArtScene>(FindObjectsSortMode.None).Length == 1,
            "workbench output survives saved scene reopen before play");
        SessionState.SetString(Flag + ".Scene", scenePath);
        SessionState.SetInt(Flag + ".Checks", checks);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        checks = SessionState.GetInt(Flag + ".Checks", 0); phase = 0; deadline = .5f;
        requestedFrame = 0; started = EditorApplication.timeSinceStartup; failure = null;
        camera = null; target = null; priorColors = null;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            failure ??= message + "\n" + stack;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 100) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < deadline) return;
        try
        {
            if (failure != null) throw new InvalidOperationException(failure);
            EnsureRenderTarget();
            RenderAuditCamera();
            if (failure != null) throw new InvalidOperationException(failure);
            if (phase == 0)
            {
                Check(RenderPipelineManager.currentPipeline is UniversalRenderPipeline, "real URP pipeline is active");
                ColorRuntimeService.Instance.ResetProgress();
                foreach (string id in new[] { "red", "green", "blue" }) ColorAppearanceManager.Instance.Play(id, false, 0);
                Next(1); return;
            }
            if (SelectiveHsvRendererFeature.LastExecutedFrame < requestedFrame) return;
            if (phase == 1)
            {
                var image = Capture("InitialGray");
                AssertGray(image, 0); AssertGray(image, 1); AssertGray(image, 2); AssertHoles(image);
                Check(ColorRuntimeService.Instance.CollectColorKey("green") == ColorKeyUnlockResult.SceneColor &&
                    !ColorRuntimeService.Instance.IsAbilityUnlocked("green"), "first green key unlocks scene only");
                ColorAppearanceManager.Instance.Play("green", true, 0);
                Next(2); return;
            }
            if (phase == 2)
            {
                var image = Capture("OnlyGreen"); AssertOnlyGreen(image); AssertHoles(image);
                priorColors = SampleColors(image);
                Check(ColorRuntimeService.Instance.CollectColorKey("green") == ColorKeyUnlockResult.PlayerAbility &&
                    ColorRuntimeService.Instance.IsAbilityUnlocked("green"), "second green key unlocks ability only");
                Check(ColorAppearanceManager.Instance.IsComplete("green"), "ability unlock does not restart scene appearance fade");
                Next(3); return;
            }
            if (phase == 3)
            {
                var image = Capture("AbilityOnly"); AssertOnlyGreen(image);
                Color[] colors = SampleColors(image);
                Check(Enumerable.Range(0, 3).All(i => ColorDistance(priorColors[i], colors[i]) < .025f),
                    "second key leaves all three rendered colors unchanged");
                var output = Object.FindFirstObjectByType<GridArtScene>();
                foreach (var renderer in output.GetComponentsInChildren<MeshRenderer>()) renderer.SetPropertyBlock(null);
                output.enabled = false; output.enabled = true;
                Next(4); return;
            }
            if (phase == 4)
            {
                var image = Capture("Reenabled"); AssertOnlyGreen(image); AssertHoles(image);
                Check(true, "disable/enable reapplies alpha masks without per-frame material updates");
                EditorSceneManager.LoadSceneInPlayMode(SessionState.GetString(Flag + ".Scene", ""),
                    new LoadSceneParameters(LoadSceneMode.Single));
                Next(5); return;
            }
            if (phase == 5)
            {
                var image = Capture("Reloaded"); AssertOnlyGreen(image); AssertHoles(image);
                Check(ColorRuntimeService.Instance.IsUnlocked("green") && ColorRuntimeService.Instance.IsAbilityUnlocked("green"),
                    "scene reload retains progress while serialized mask bindings rebuild");
                Check(SelectiveHsvRendererFeature.LastExecutedFrame >= requestedFrame,
                    "actual after-pixel HSV render graph executed during final readback");
                Finish(true, "PASS " + checks + " real-URP workbench/HSV assertions");
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Next(int next)
    {
        phase = next; requestedFrame = Time.frameCount + 2; deadline = Time.time + .35f;
    }

    private static void EnsureRenderTarget()
    {
        if (camera == null) camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Audit camera was not loaded.");
        if (target == null)
        {
            target = new RenderTexture(768, 384, 24, RenderTextureFormat.ARGB32) { name = "Decorations HSV Readback", antiAliasing = 1 };
            target.Create();
        }
        camera.targetTexture = target; camera.aspect = 2;
    }

    private static void RenderAuditCamera()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            throw new InvalidOperationException("Real URP readback requires a graphics device; do not use -nographics.");
        if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
            throw new InvalidOperationException("The active Quality/Graphics asset is not URP: " +
                (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in"));

        // Batchmode has no visible GameView and does not guarantee an automatic camera render.
        // URP's own NoLeaksOnEnterLeavePlaymode test warms the pipeline with Camera.Render.
        if (RenderPipelineManager.currentPipeline == null) camera.Render();
        var request = new RenderPipeline.StandardRequest
        {
            destination = target, mipLevel = 0, slice = 0, face = CubemapFace.Unknown
        };
        if (!RenderPipeline.SupportsRenderRequest(camera, request))
            throw new InvalidOperationException("Configured URP does not support a real StandardRequest for the audit camera.");
        RenderPipeline.SubmitRenderRequest(camera, request);
    }

    private static Texture2D Capture(string name)
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); RenderTexture.active = previous;
        File.WriteAllBytes("Library/DecorationsHsv_" + name + ".png", image.EncodeToPNG());
        captures.Add(image);
        return image;
    }

    private static Color Sample(Texture2D image, Vector3 world)
    {
        Vector3 point = camera.WorldToViewportPoint(world);
        int x = Mathf.Clamp(Mathf.RoundToInt(point.x * image.width), 2, image.width - 3);
        int y = Mathf.Clamp(Mathf.RoundToInt(point.y * image.height), 2, image.height - 3);
        Color color = Color.clear;
        for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) color += image.GetPixel(x + dx, y + dy);
        return color / 25;
    }
    private static Color[] SampleColors(Texture2D image) => Enumerable.Range(0, 3)
        .Select(i => Sample(image, new Vector3(i * 2 + .2f, .2f, 0))).ToArray();
    private static float ColorDistance(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
    private static void AssertGray(Texture2D image, int index)
    {
        Color color = SampleColors(image)[index];
        Check(Mathf.Max(color.r, color.g, color.b) - Mathf.Min(color.r, color.g, color.b) < .035f,
            "rendered " + new[] { "red", "green", "blue" }[index] + " decoration is grayscale: " + color);
        Check(color.maxColorComponent > .05f, "grayscale decoration is visible, not accidentally hidden");
    }
    private static void AssertOnlyGreen(Texture2D image)
    {
        AssertGray(image, 0); AssertGray(image, 2);
        Color green = SampleColors(image)[1];
        Check(green.g > green.r + .25f && green.g > green.b + .25f,
            "only green decoration restores its real color: " + green);
    }
    private static void AssertHoles(Texture2D image)
    {
        Color background = Sample(image, new Vector3(-.25f, .5f, 0));
        for (int i = 0; i < 3; i++)
            Check(ColorDistance(Sample(image, new Vector3(i * 2 + .5f, .5f, 0)), background) < .035f,
                "transparent hole in " + new[] { "red", "green", "blue" }[i] + " does not fade the background");
    }

    private static void Finish(bool success, string message)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        if (camera != null) camera.targetTexture = null;
        if (target != null) { target.Release(); Object.DestroyImmediate(target); target = null; }
        foreach (var capture in captures) if (capture != null) Object.DestroyImmediate(capture);
        captures.Clear();
        File.WriteAllText("Library/DecorationsHsvResult.txt", message); Debug.Log(message);
        if (EditorApplication.isPlaying)
        { EditorApplication.ExitPlaymode(); EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1); }
        else EditorApplication.Exit(success ? 0 : 1);
    }
}
