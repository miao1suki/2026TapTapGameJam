using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

// Actual UI Toolkit GPU comparison, not an unattached layout timing surrogate.
public static class PlanningNativePreviewRenderAudit
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static PlanningCanvas canvas;
    static VisualElement root;
    static PanelSettings settings;
    static RenderTexture target;
    static GameObject host;
    static Color32[] cached;
    static Color32[] previousView;
    static int phase, ticks, checks;
    static bool rebuilding;
    static double deadline;
    static readonly Vector2[] Pans = { new(40, 50), new(120, 80), new(-130, 90), new(180, -40) };
    static readonly float[] Zooms = { 1f, .35f, 1.5f, .75f };

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Disposable probe project only.");
        // Runtime panels render at most once per game frame. In Edit mode a
        // repeated Repaint can return the same old target and falsely pass.
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.playModeStateChanged += StartWhenPlaying;
        EditorApplication.EnterPlaymode();
    }

    static void StartWhenPlaying(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= StartWhenPlaying;
        Setup();
    }

    static void Setup()
    {
        try
        {
            var palette = LevelEditorPaletteService.GetOrCreate();
            var doc = new PlanningDocument(); var room = new PlanningRoom("原生美术 GPU 缓存验证");
            room.cells.Add(new PlanningCell(0, 0)); doc.rooms.Add(room);
            room.boxes.Add(new PlanningBox(PlanningDetailType.Water, new RectInt(2, 3, 8, 4)));
            int x = 2;
            foreach (var entry in palette.PropEntries.Where(p => p.Prefab != null && p.Prefab.TryGetComponent<Project.ContactCubes.ContactCube>(out _)).Take(3))
            {
                var box = new PlanningBox(PlanningDetailType.Prop, new RectInt(x, 1, 1, 1));
                box.propEntryId = entry.EntryId; box.propEntryName = entry.DisplayName; room.boxes.Add(box); x += 3;
            }
            if (x != 11) throw new InvalidOperationException("Three real contact cube prefabs required.");
            target = new RenderTexture(800, 500, 24, RenderTextureFormat.ARGB32); target.Create();
            settings = ScriptableObject.CreateInstance<PanelSettings>(); settings.targetTexture = target;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            host = new GameObject("Retained native artwork GPU audit");
            var document = host.AddComponent<UIDocument>(); document.panelSettings = settings;
            root = document.rootVisualElement; root.style.width = 800; root.style.height = 500;
            canvas = new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(doc);
            canvas.SetMode(PlanningCanvasMode.Detail); canvas.SelectRoom(room.id); root.Add(canvas);
            phase = ticks = checks = 0; previousView = null; rebuilding = false; deadline = EditorApplication.timeSinceStartup + 90;
            canvas.RestoreView(Pans[0], Zooms[0]); EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish(error); }
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Native preview GPU audit");
            if (root.panel == null) return;
            root.panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null)?.Invoke(root.panel, null);
            root.panel.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null)?.Invoke(root.panel, null);
            var repaint = root.panel.GetType().GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Event) }, null);
            repaint.Invoke(root.panel, new object[] { new Event { type = EventType.Repaint } });
            if (++ticks < 12) return;
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(800, 500, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 800, 500), 0, 0); image.Apply(); RenderTexture.active = previous;
            var pixels = image.GetPixels32();
            if (!rebuilding)
            {
                if (previousView != null && pixels.SequenceEqual(previousView))
                    throw new InvalidOperationException("GPU target did not update after viewport change.");
                previousView = pixels;
                cached = pixels; rebuilding = true;
                File.WriteAllBytes("Library/NativePreviewCached-" + phase + ".png", image.EncodeToPNG());
                typeof(PlanningCanvas).GetMethod("InvalidatePreviewData", Hidden).Invoke(canvas, null);
                canvas.RestoreView(Pans[phase], Zooms[phase]);
            }
            else
            {
                int changed = 0;
                for (int i = 0; i < pixels.Length; i++)
                    if (Math.Abs(pixels[i].r - cached[i].r) > 3 || Math.Abs(pixels[i].g - cached[i].g) > 3 || Math.Abs(pixels[i].b - cached[i].b) > 3) changed++;
                if (changed > 40) throw new InvalidOperationException("Cached/fresh GPU mismatch at view " + phase + ": " + changed);
                Debug.Log("NATIVE_GPU PASS view=" + phase + " changedPixels=" + changed + " zoom=" + Zooms[phase]); checks++;
                phase++; rebuilding = false;
                if (phase < Pans.Length) canvas.RestoreView(Pans[phase], Zooms[phase]);
            }
            Object.DestroyImmediate(image); ticks = 0;
            if (phase == Pans.Length) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        if (host != null) Object.DestroyImmediate(host);
        if (settings != null) Object.DestroyImmediate(settings);
        if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        if (error != null) Debug.LogException(error);
        string report = error == null ? "PASS " + checks + " GPU cached/fresh native artwork views" : error.ToString();
        File.WriteAllText("Library/PlanningNativePreviewRenderAudit.txt", report); Debug.Log(report);
        EditorApplication.Exit(error == null ? 0 : 2);
    }
}
