using System;
using System.IO;
using TapTap.TA.RgbGlitch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Real PC/Mobile RenderGraph frames, not an API-only shader test.</summary>
public static class TAEffectsGpuAudit
{
    private const string Flag = "TAEffectsGpuAudit.Running";
    private static Camera camera;
    private static RenderTexture target;
    private static Texture2D pattern;
    private static Material material;
    private static RenderPipelineAsset previousQuality, previousDefault;
    private static Color32[] baseline;
    private static int phase, mode, frame, checks;
    private static double started;
    private static readonly string[] Modes = { "PC", "Mobile" };

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            !Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-noaudio") ||
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            throw new InvalidOperationException("Silent GPU isolation project required; omit -nographics.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Flag, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        phase = mode = checks = 0;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        checks++;
        Debug.Log("TA GPU PASS: " + description);
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 150d) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
        try
        {
            if (phase == 0)
            {
                previousQuality = QualitySettings.renderPipeline;
                previousDefault = GraphicsSettings.defaultRenderPipeline;
                pattern = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var pixels = new Color[64];
                var colors = new[] { Color.red, Color.green, Color.blue, Color.yellow };
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) pixels[y * 8 + x] = colors[(x + y) % 4];
                pattern.SetPixels(pixels); pattern.Apply();
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                Check(shader != null && shader.isSupported, "URP test surface shader supported");
                material = new Material(shader); material.SetTexture("_BaseMap", pattern);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.localScale = new Vector3(8f, 8f, 1f);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                camera = new GameObject("TA GPU audit camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true; camera.orthographicSize = 4f;
                camera.allowMSAA = false; camera.allowHDR = false;
                target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
                target.Create(); camera.targetTexture = target;
                phase = 1;
            }
            if (phase == 1)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/" + Modes[mode] + "_RPAsset.asset");
                Check(pipeline != null, Modes[mode] + " pipeline exists");
                QualitySettings.renderPipeline = pipeline;
                GraphicsSettings.defaultRenderPipeline = pipeline;
                RgbGlitchEffect.ResetEffect(); RgbGlitchEffect.UseRendererDefaults();
                frame = Time.frameCount; phase = 2;
                return;
            }
            if (Time.frameCount - frame < 5) return;
            if (phase == 2)
            {
                baseline = Capture(Modes[mode] + "-normal");
                int chromatic = 0;
                foreach (var p in baseline) if (Mathf.Max(p.r, p.g, p.b) - Mathf.Min(p.r, p.g, p.b) > 80) chromatic++;
                Check(chromatic > baseline.Length / 2, Modes[mode] + " renders actual colored scene");
                RgbGlitchEffect.PlayDeath(0f);
                frame = Time.frameCount; phase = 3;
                return;
            }
            if (phase == 3)
            {
                var death = Capture(Modes[mode] + "-death");
                int different = Difference(baseline, death);
                Check(different > death.Length / 8, Modes[mode] + " RGB death pass visibly changes real GPU frame: " + different);
                RgbGlitchEffect.ResetEffect();
                frame = Time.frameCount; phase = 4;
                return;
            }
            if (phase == 4)
            {
                var restored = Capture(Modes[mode] + "-restored");
                Check(Difference(baseline, restored) < restored.Length / 100, Modes[mode] + " zero intensity restores unprocessed frame");
                if (++mode < Modes.Length) phase = 1;
                else Finish(true, "PASS " + checks);
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
    }

    private static int Difference(Color32[] first, Color32[] second)
    {
        int count = 0;
        for (int i = 0; i < first.Length; i++)
            if (Mathf.Abs(first[i].r - second[i].r) + Mathf.Abs(first[i].g - second[i].g) + Mathf.Abs(first[i].b - second[i].b) > 15) count++;
        return count;
    }

    private static Color32[] Capture(string name)
    {
        var oldTarget = RenderTexture.active;
        RenderTexture.active = target;
        var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        readback.Apply();
        File.WriteAllBytes("Library/TA-GPU-" + name + ".png", readback.EncodeToPNG());
        var pixels = readback.GetPixels32();
        Object.Destroy(readback); RenderTexture.active = oldTarget;
        return pixels;
    }

    private static void Finish(bool success, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        RgbGlitchEffect.ResetEffect(); RgbGlitchEffect.UseRendererDefaults();
        QualitySettings.renderPipeline = previousQuality;
        GraphicsSettings.defaultRenderPipeline = previousDefault;
        if (camera != null) camera.targetTexture = null;
        if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        if (pattern != null) Object.DestroyImmediate(pattern);
        if (material != null) Object.DestroyImmediate(material);
        File.WriteAllText("Library/TAEffectsGpuAudit.txt", message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
