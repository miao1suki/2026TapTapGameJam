using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Project.GridArt.Editor
{
    public static class GridArtCanvasDepthAudit
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GridArtWindow window;
        private static GridArtMap map;
        private static GridArtPalette palette;
        private static Sprite sprite;
        private static Texture2D texture;
        private static VisualElement canvas;
        private static GameObject host;
        private static PanelSettings settings;
        private static RenderTexture target;
        private static int phase, ticks, checks;
        private static double started;
        private static string before;
        private static readonly List<string> Report = new();
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe.");
            phase = ticks = checks = 0; Report.Clear(); started = EditorApplication.timeSinceStartup;
            try
            {
                texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var pixels = new Color[256]; Array.Fill(pixels, Color.white); texture.SetPixels(pixels); texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16);
                palette = ScriptableObject.CreateInstance<GridArtPalette>();
                palette.brushes.Add(new GridArtBrush { sprite = sprite, visualSize = Vector2.one });
                map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette = palette;
                var far = new GridArtLayer { depth = 5, tint = Color.red, sortingOrder = 200 };
                var near = new GridArtLayer { depth = 2, tint = Color.blue, sortingOrder = 210 };
                var front = new GridArtLayer { depth = -.51f, tint = Color.yellow, sortingOrder = -100 };
                for (int x = 0; x < 5; x++) far.Set(new Vector2Int(x, 0), 0);
                near.Set(new Vector2Int(2, 0), 0); front.Set(new Vector2Int(3, 0), 0);
                map.layers.Add(far); map.layers.Add(near); map.layers.Add(front);
                before = EditorJsonUtility.ToJson(map);
                var snapshot = new GridArtLevelSnapshot();
                for (int x = 0; x < 4; x++) snapshot.AddReference("reference" + x,
                    new GridArtQuad(new Rect(x, 0, 1, 1), new Rect(0, 0, 1, 1), texture),
                    x == 1 ? Color.green : Color.white, x == 1 ? GridArtReferenceLayer.Object : GridArtReferenceLayer.Wall);
                window = ScriptableObject.CreateInstance<GridArtWindow>();
                // The detached owner supplies real canvas behavior only; disable
                // its periodic source/persistence subscriptions before injection.
                Invoke(window, "OnDisable");
                Set(window, "autoSave", false); Set(window, "map", map); Set(window, "levelSource", snapshot);
                Set(window, "showLevelReference", true); Set(window, "levelReferenceOpacity", .5f);
                Type type = typeof(GridArtWindow).GetNestedType("ArtCanvas", BindingFlags.NonPublic);
                canvas = (VisualElement)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new object[] { window }, null);
                canvas.pickingMode = PickingMode.Ignore;
                canvas.style.width = 640; canvas.style.height = 128;
                canvas.style.backgroundColor = Color.black;
                Set(canvas, "needsInitialFrame", false); Set(canvas, "pan", new Vector2(0, 128)); Set(canvas, "zoom", 128f);
                target = new RenderTexture(640, 128, 24); target.Create();
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ConstantPixelSize; settings.targetTexture = target;
                host = new GameObject("Actual art canvas GPU depth audit");
                var document = host.AddComponent<UIDocument>(); document.panelSettings = settings;
                document.rootVisualElement.style.width = 640; document.rootVisualElement.style.height = 128;
                document.rootVisualElement.Add(canvas);
                // AttachToPanel restores the user's view; override only AFTER
                // attaching so stale probe EditorPrefs cannot affect this fixture.
                Set(canvas, "needsInitialFrame", false); Set(canvas, "initialFrameBounds", null);
                Set(canvas, "pan", new Vector2(0, 128)); Set(canvas, "zoom", 128f);
                Set(canvas, "pointer", new Vector2(64, 64));
                Invoke(canvas, "UpdateReference"); Invoke(canvas, "Invalidate");
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 60) throw new TimeoutException("canvas phase " + phase);
                // Runtime panel attaches/layouts descendants asynchronously. Keep
                // this fixture's fixed viewport established during warm-up too,
                // not just during the readback update (GPU uploads are deferred).
                Set(canvas, "needsInitialFrame", false); Set(canvas, "initialFrameBounds", null);
                Set(canvas, "pan", new Vector2(0, 128)); Set(canvas, "zoom", 128f);
                Set(canvas, "pointer", new Vector2(64, 64)); Invoke(canvas, "RepaintView");
                if (++ticks < 12 || canvas.contentRect.width < 600 || canvas.contentRect.height < 100) return;
                string Diagnostic()
                {
                    var result = "phase=" + phase + " rect=" + canvas.contentRect + " world=" + canvas.worldBound + " pan=" + Get(canvas, "pan") + " zoom=" + Get(canvas, "zoom") + " pointer=" + Get(canvas, "pointer") + " frame=" + Get(canvas, "initialFrameBounds");
                    foreach (var child in canvas.Children())
                    {
                        result += "\n  " + child.name + " local=" + child.layout + " world=" + child.worldBound + " display=" + child.resolvedStyle.display + " children=" + child.childCount;
                        foreach (var batch in child.Children()) result += "\n    " + batch.name + " local=" + batch.layout + " world=" + batch.worldBound + " display=" + batch.resolvedStyle.display;
                    }
                    return result;
                }
                Debug.Log("CANVAS DEPTH BEFORE " + Diagnostic());
                Set(canvas, "needsInitialFrame", false); Set(canvas, "initialFrameBounds", null);
                Set(canvas, "pan", new Vector2(0, 128)); Set(canvas, "zoom", 128f);
                Set(canvas, "pointer", new Vector2(64, 64)); Invoke(canvas, "RepaintView");
                var panel = canvas.panel;
                panel.GetType().GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                Debug.Log("CANVAS DEPTH AFTER " + Diagnostic());
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(640, 128, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 640, 128), 0, 0); image.Apply(); RenderTexture.active = previous;
                Color Pixel(int cell) => image.GetPixel(cell * 128 + 64, 64);
                Color wall = Pixel(0), prop = Pixel(1), near = Pixel(2), front = Pixel(3), bare = Pixel(4);
                try
                {
                    if (phase == 0)
                    {
                        File.WriteAllBytes("Library/GridArtCanvasDepth.png", image.EncodeToPNG());
                        Check(wall.r > .8f && wall.g > .2f && wall.b > .2f, "half-opacity wall remains visible above far background: " + wall);
                        Check(prop.g > .2f && prop.r > .2f && prop.b < .1f, "half-opacity prop remains visible above far background: " + prop);
                        Check(near.b > .8f && near.r > .2f && near.g > .2f, "half-opacity reference above near background: " + near);
                        Set(window, "showLevelReference", false);
                    }
                    else if (phase == 1)
                    {
                        Check(wall.r > .8f && wall.g < .1f && wall.b < .1f, "hiding reference reveals far background: " + wall);
                        Check(prop.r > .8f && prop.g < .1f, "hiding reference also hides prop");
                        Check(near.b > .8f && near.r < .1f && near.g < .1f, "near background remains when reference hidden");
                        Set(window, "showLevelReference", true); Set(window, "levelReferenceOpacity", 1f);
                    }
                    else
                    {
                        Check(wall.r > .8f && wall.g > .8f && wall.b > .8f, "opaque reference wall covers background");
                        Check(prop.g > .8f && prop.r < .1f && prop.b < .1f, "opaque reference prop covers background");
                        Check(near.r > .8f && near.g > .8f && near.b > .8f, "opaque reference above near layer");
                    }
                    Check(front.r > .8f && front.g > .8f && front.b < .1f, "negative-depth foreground above reference at phase " + phase);
                    Check(bare.r > .8f && bare.g < .1f && bare.b < .1f, "uncovered background remains visible at phase " + phase);
                    Check(EditorJsonUtility.ToJson(map) == before, "render does not mutate art map at phase " + phase);
                }
                finally { Object.DestroyImmediate(image); }
                if (++phase == 3) { Finish(null); return; }
                ticks = 0; Invoke(canvas, "RepaintView");
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Set(object targetObject, string name, object value) => targetObject.GetType().GetField(name, Hidden).SetValue(targetObject, value);
        private static object Get(object targetObject, string name) => targetObject.GetType().GetField(name, Hidden).GetValue(targetObject);
        private static void Invoke(object targetObject, string method) => targetObject.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(targetObject, null);
        private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); Report.Add("PASS " + checks + ": " + message); }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (window != null) { Set(window, "map", null); Object.DestroyImmediate(window); }
            if (host != null) Object.DestroyImmediate(host);
            if (settings != null) Object.DestroyImmediate(settings);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (map != null) Object.DestroyImmediate(map); if (palette != null) Object.DestroyImmediate(palette);
            if (sprite != null) Object.DestroyImmediate(sprite); if (texture != null) Object.DestroyImmediate(texture);
            string result = (error == null ? "PASS " : "FAIL ") + checks + "\n" + string.Join("\n", Report) + "\n" + error;
            File.WriteAllText("Library/GridArtCanvasDepthAudit.txt", result);
            if (error == null) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
