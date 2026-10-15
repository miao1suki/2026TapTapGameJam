using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Project.LevelEditor;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    // Batch-only regression probe. Never opens or writes the user's project/scene.
    internal static class PlanningPerformanceVerification
    {
        internal static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("Use the isolated verification project.");
            var report = new StringBuilder();
            try
            {
                var doc = PlanningSaveStore.Read("Assets/Scenes/Pick.planning.json");
                var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
                var canvas = new PlanningCanvas();
                canvas.SetDocument(doc); canvas.SetPalette(palette);
                canvas.SelectRoom(doc.rooms.OrderByDescending(r => r.boxes.Count).First().id);
                canvas.SetMode(PlanningCanvasMode.Detail);
                var images = typeof(PlanningCanvas).GetField("detailPreviewImages", Private);
                report.AppendLine($"rooms={doc.rooms.Count}, cells={doc.rooms.Sum(r => r.cells.Count)}, boxes={doc.rooms.Sum(r => r.boxes.Count)}, images={((System.Collections.IDictionary)images.GetValue(canvas)).Count}");
                Measure(report, "Preview refresh", 8, canvas.RefreshPreviewImages);
                Measure(report, "Unchanged palette", 8, () => canvas.SetPalette(palette));
                Measure(report, "Persistent JSON", 3, () => doc.ToPersistentJson(true));
                string path = Path.GetFullPath("Library/ResponsivenessMap.json");
                PlanningSaveStore.Save(path, doc);
                Measure(report, "Save unchanged map", 3, () => PlanningSaveStore.Save(path, doc));
                var moving = doc.rooms.First(r => r.boxes.Count > 0).boxes[0];
                Measure(report, "Save edited map", 3, () => { moving.x++; PlanningSaveStore.Save(path, doc); });
                UnityEngine.Debug.Log(report.ToString());
                File.WriteAllText("Library/PlanningPerformance.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                File.WriteAllText("Library/PlanningPerformance.txt", report + "\nFAIL " + e);
                UnityEngine.Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        private static void Measure(StringBuilder output, string name, int iterations, Action action)
        {
            var time = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            time.Stop();
            output.AppendLine($"{name}: {time.Elapsed.TotalMilliseconds / iterations:F3} ms");
        }
    }
}
