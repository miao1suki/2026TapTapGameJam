using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Project.GridArt;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Project.LevelEditor.Editor
{
    public static class SceneEditorReformBatchAudit
    {
        public static void RunAllFromCommandLine()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity", OpenSceneMode.Single);
                SceneEditorReformStage1Audit.RunFromMenu();
                SceneEditorReformStage2Audit.RunFromMenu();
                SceneEditorReformStage3To6Audit.RunFromMenu();
                SceneEditorReformFormalAudit.RunFromMenu();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }

    internal static class SceneEditorReformFormalAudit
    {
        private const string RequestPath = "Library/SceneEditorReformFormalAudit.request";
        private const string ReportPath = "Library/SceneEditorReformFormalAudit.txt";
        private static readonly string[] FormalPaths =
        {
            "Assets/_Project/Scenes/Playing/Gaming.unity",
            "Assets/_Project/Scenes/Playing/Gaming.planning.json",
            "Assets/_Project/Content/LevelMaps/正式关卡.json",
            "Assets/_Project/Scenes/Playing/Gaming_Art.asset",
        };

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证/正式 Scene 只读与性能")]
        internal static void RunFromMenu() => Run();
        [InitializeOnLoadMethod]
        private static void Requested()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.update -= RunWhenEditorIdle;
            EditorApplication.update += RunWhenEditorIdle;
        }

        private static void RunWhenEditorIdle()
        {
            if (!File.Exists(RequestPath))
            {
                EditorApplication.update -= RunWhenEditorIdle;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= RunWhenEditorIdle;
            File.Delete(RequestPath);
            Run();
        }

        private static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            var report = new StringBuilder(); int checks = 0;
            Dictionary<string,string> hashes = FormalPaths.ToDictionary(path => path, Hash, StringComparer.Ordinal);
            bool dirty = scene.isDirty; int undo = Undo.GetCurrentGroup();
            void Check(bool condition, string message)
            {
                checks++; if (!condition) throw new InvalidOperationException(message);
                report.AppendLine("PASS  " + message);
            }
            try
            {
                Check(scene.path.Replace('\\','/') == FormalPaths[0], "活动场景是正式 Gaming");
                RoomLifecycleManager manager = SceneRegionAuthoring.FindManager(scene);
                Check(manager != null && manager.Regions.Count == 25, "正式 Scene 读取 25 个房间/通道区域");
                Check(manager.Regions.Count(item => item != null && item.corridor) == 1, "正式 Scene 保留通道标记");
                var watch = Stopwatch.StartNew(); SceneObjectIndex index = SceneObjectIndex.Build(scene); watch.Stop();
                Check(index.All.Count == 3004, "正式 Scene 读取 3004 个真实作者对象");
                int missing = index.All.Count(item => string.IsNullOrEmpty(item.authorId));
                int groups = index.All.Where(item => !string.IsNullOrEmpty(item.authorId)).Select(item => item.authorId).Distinct().Count();
                int repeated = index.All.Where(item => !string.IsNullOrEmpty(item.authorId)).GroupBy(item => item.authorId).Count(group => group.Count() > 1);
                Check(missing == 0 && groups == 2998 && repeated == 1, "正式稳定身份 0 缺失 / 2998 组 / 1 个重复逻辑组");
                RoomResetObject[] resetObjects = Roots<RoomResetObject>(scene).ToArray();
                Check(resetObjects.Length > 0, $"正式当前内存房间重置成员 {resetObjects.Length} 个原样接轨");
                Check(resetObjects.All(item => manager.Regions.Any(region => region != null && region.id == item.RegionId)), "全部房间重置归属指向现有区域");

                DoorController[] doors = Roots<DoorController>(scene).ToArray();
                Check(doors.All(door => door.Buttons.Where(button => button != null).All(button => button.Owner == door)), "全部门按钮双向引用一致");
                GridArtScene art = Roots<GridArtScene>(scene).Single();
                Check(art.map != null && art.map.layers.Sum(layer => layer?.cells?.Count ?? 0) == 13817, "正式 GridArt 13817 锚点原样读取");

                Vector2Int[] probes = index.All.SelectMany(item => item.cells.Take(1)).Take(512).ToArray();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var queryWatch = Stopwatch.StartNew(); int hits = 0;
                for (int pass = 0; pass < 20; pass++) foreach (Vector2Int cell in probes) hits += index.At(cell).Count;
                queryWatch.Stop(); long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                Check(hits > 0 && allocated < 4096, "缓存命中查询不随 3000+ 对象扫描且分配低于 4 KiB");
                report.AppendLine($"PERF  Unity {Application.unityVersion} · 对象 {index.All.Count} · GridArt 13817 · 索引 {watch.Elapsed.TotalMilliseconds:0.###} ms · 10240 查询 {queryWatch.Elapsed.TotalMilliseconds:0.###} ms · 分配 {allocated} B");

                var regionWatch = Stopwatch.StartNew(); int regionRectangles = SceneRegionAuthoring.CachedRectangleCount(manager); regionWatch.Stop();
                int regionCells = manager.Regions.Sum(region => region?.cells?.Count ?? 0);
                Check(regionRectangles > 0 && regionRectangles < regionCells, $"区域覆盖由 {regionCells} 格合并为 {regionRectangles} 个缓存矩形");
                Check(SceneRegionAuthoring.StableBindIsNoOpForAudit() && SceneArtAuthoring.StableBindIsNoOpForAudit() &&
                    LevelEditorScenePainter.StableBindIsNoOpForAudit(), "Overlay 周期刷新中的相同绑定为零重绘 no-op");
                report.AppendLine($"PERF  区域矩形缓存 {regionWatch.Elapsed.TotalMilliseconds:0.###} ms · Repaint Handles {regionRectangles}（原逐格 {regionCells}）");

                Check(scene.isDirty == dirty && Undo.GetCurrentGroup() == undo, "只读扫描不改变 Scene dirty 或 Undo 组");
                foreach (string path in FormalPaths) Check(Hash(path) == hashes[path], "正式资源磁盘哈希未变：" + path);
                report.Insert(0, $"Scene Editor Reform Formal Audit\nPASS  {checks} checks\n");
                File.WriteAllText(ReportPath, report.ToString()); Debug.Log(report.ToString());
            }
            catch (Exception exception)
            {
                File.WriteAllText(ReportPath, "FAIL after " + checks + " checks\n" + report + exception);
                Debug.LogException(exception); throw;
            }
        }

        private static IEnumerable<T> Roots<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects()) foreach (T value in root.GetComponentsInChildren<T>(true)) yield return value;
        }
        private static string Hash(string path)
        {
            using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
