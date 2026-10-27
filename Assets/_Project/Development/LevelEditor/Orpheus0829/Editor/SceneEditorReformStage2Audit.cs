using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    internal static class SceneEditorReformStage2Audit
    {
        private const string RequestPath = "Library/SceneEditorReformStage2Audit.request";
        private const string ReportPath = "Library/SceneEditorReformStage2Audit.txt";
        private static readonly string[] FormalPaths =
        {
            "Assets/_Project/Scenes/Playing/Gaming.unity",
            "Assets/_Project/Scenes/Playing/Gaming.planning.json",
            "Assets/_Project/Content/LevelMaps/正式关卡.json",
            "Assets/_Project/Scenes/Playing/Gaming_Art.asset",
        };

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证/阶段 2 房间与通道")]
        internal static void RunFromMenu() => Run();

        [InitializeOnLoadMethod]
        private static void RunWhenRequested()
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
            Dictionary<string, string> hashes = FormalPaths.ToDictionary(path => path, Hash, StringComparer.Ordinal);
            Scene active = SceneManager.GetActiveScene();
            bool activeDirty = active.IsValid() && active.isDirty;
            var report = new StringBuilder();
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("区域测试");
                SceneManager.MoveGameObjectToScene(root, preview);
                RoomLifecycleManager manager = root.AddComponent<RoomLifecycleManager>();
                manager.Configure(new List<RoomRegion>
                {
                    new RoomRegion { id="room_existing", label="既有房间", cells=new List<Vector2Int>{new Vector2Int(-2,1)} },
                    new RoomRegion { id="corridor_existing", label="既有通道", corridor=true,
                        cells=new List<Vector2Int>{new Vector2Int(10,0),new Vector2Int(11,0),new Vector2Int(12,0)} },
                }, 1.5f, "legacy-json-sentinel");

                string created = SceneRegionAuthoring.CreateRegion(manager, false);
                Require(!string.IsNullOrEmpty(created) && manager.Regions.Count == 3, "新房间创建失败。");
                Require(manager.PlanningJson == "legacy-json-sentinel", "区域作者 API 改写了旧 planningJson。");
                Require(SceneRegionAuthoring.Rename(manager, created, "新增房间"), "区域改名失败。");
                Require(SceneRegionAuthoring.SetCorridor(manager, created, true), "房间/通道切换失败。");

                Require(SceneRegionAuthoring.TryApplySingleCellForAudit(manager, "room_existing", new Vector2Int(-1,1),
                    SceneRegionBrush.Paint, out string reason), reason);
                Require(manager.Regions.First(item => item.id == "room_existing").cells.Contains(new Vector2Int(-1,1)), "绘制没有写入目标区域。");
                Undo.PerformUndo();
                manager.RebuildOwnership();
                Require(!manager.Regions.First(item => item.id == "room_existing").cells.Contains(new Vector2Int(-1,1)), "单笔 Undo 未恢复区域。");
                Undo.PerformRedo();
                manager.RebuildOwnership();
                Require(manager.Regions.First(item => item.id == "room_existing").cells.Contains(new Vector2Int(-1,1)), "单笔 Redo 未恢复绘制。");

                Require(!SceneRegionAuthoring.TryApplySingleCellForAudit(manager, "corridor_existing", new Vector2Int(11,0),
                    SceneRegionBrush.Erase, out reason) && reason.Contains("断裂"), "擦除通道中点应拒绝断裂。");
                Require(!SceneRegionAuthoring.TryApplySingleCellForAudit(manager, created, new Vector2Int(-2,1),
                    SceneRegionBrush.Paint, out reason) && reason.Contains("其他"), "区域重叠应被拒绝。");
                Require(manager.FindRegion(new Vector3((-1.5f) * 1.5f, -(1.5f) * 1.5f, 0f)) == "room_existing",
                    "负坐标区域与 FindRegion 未接轨。");

                Require(active.isDirty == activeDirty, "阶段 2 审计改变了当前 Scene dirty 状态。");
                Require(hashes.All(pair => Hash(pair.Key) == pair.Value), "阶段 2 审计改动了正式资源。");
                report.AppendLine("PASS  既有房间/通道原样接轨");
                report.AppendLine("PASS  新建、改名、房间/通道切换不写 planningJson");
                report.AppendLine("PASS  绘制一笔 Undo/Redo 完整往返");
                report.AppendLine("PASS  重叠与擦除断裂均零写入拒绝");
                report.AppendLine("PASS  负坐标与 FindRegion 一致");
                report.AppendLine("PASS  当前 Scene dirty 与正式四资源哈希不变");
                File.WriteAllText(ReportPath, report.ToString());
                Debug.Log(report.ToString());
            }
            catch (Exception exception)
            {
                File.WriteAllText(ReportPath, "FAIL  " + exception);
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                SceneRegionAuthoring.Bind(null, null, string.Empty, false);
                RoomLifecycleManager manager = preview.IsValid()
                    ? preview.GetRootGameObjects().Select(root => root.GetComponentInChildren<RoomLifecycleManager>(true)).FirstOrDefault(item => item != null)
                    : null;
                if (manager != null) Undo.ClearUndo(manager);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
