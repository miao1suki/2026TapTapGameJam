using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Project.GridArt;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    /// <summary>Explicit, non-authoring verification for the stage-one safety gate.</summary>
    internal static class SceneEditorReformStage1Audit
    {
        private const string ReportPath = "Library/SceneEditorReformStage1Audit.txt";
        private const string RequestPath = "Library/SceneEditorReformStage1Audit.request";
        private static readonly IReadOnlyDictionary<string, string> FormalHashes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Assets/_Project/Scenes/Playing/Gaming.unity", "1763418D57F60AFBB2B83DB7B6111EDB368A853908D1E5D793CEBFF1711CA057" },
                { "Assets/_Project/Scenes/Playing/Gaming.planning.json", "5714D40118DD1109D0F56F80DD6DD9CCD9A0B622A93867BBA27F200E37EA3C90" },
                { "Assets/_Project/Content/LevelMaps/正式关卡.json", "5714D40118DD1109D0F56F80DD6DD9CCD9A0B622A93867BBA27F200E37EA3C90" },
                { "Assets/_Project/Scenes/Playing/Gaming_Art.asset", "056395010966A03C8F5AC277051A67BE5173F6EE043E45C743CCA824AC1683F1" },
            };

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证/阶段 1 零写入")]
        internal static void RunFromMenu() => Run(false);

        internal static void RunFromCommandLine() => Run(true);

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
            RunFromMenu();
        }

        private static void Run(bool commandLine)
        {
            var report = new StringBuilder();
            var beforeHashes = ReadFormalHashes();
            Scene activeScene = SceneManager.GetActiveScene();
            bool activeDirty = activeScene.IsValid() && activeScene.isDirty;
            int undoGroup = Undo.GetCurrentGroup();
            try
            {
                ValidateFormalBaseline(beforeHashes);
                ValidateSourceGuards();
                ValidateGridSpaceAndSummary();
                ValidateObjectLibraryUi();
                ValidateArtLibraryUi();
                ValidateViewRoundTrip();
                Require(Undo.GetCurrentGroup() == undoGroup, "只读验证不应创建 Undo 组。");
                Require(!activeScene.IsValid() || activeScene.isDirty == activeDirty, "只读验证改变了当前 Scene dirty 状态。");
                Require(HashesEqual(beforeHashes, ReadFormalHashes()), "正式资源在验证过程中发生变化。");
                report.AppendLine("PASS  阶段 1 零写入保护");
                report.AppendLine("PASS  旧 Scene 区域/对象摘要直接读取");
                report.AppendLine("PASS  SceneCell / RegionCell / World 坐标往返（含非零 Transform）");
                report.AppendLine("PASS  方块/道具与美术卡片选择器、实例参数入口完整");
                report.AppendLine("PASS  SceneView 正交锁定与逐字段恢复");
                report.AppendLine("PASS  正式 Scene / JSON / GridArt 四项哈希保持基线");
                File.WriteAllText(ReportPath, report.ToString());
                Debug.Log(report.ToString());
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL  " + exception);
                File.WriteAllText(ReportPath, report.ToString());
                Debug.LogException(exception);
                if (commandLine) EditorApplication.Exit(1);
                throw;
            }
            if (commandLine) EditorApplication.Exit(0);
        }

        private static void ValidateSourceGuards()
        {
            string window = File.ReadAllText("Assets/_Project/Development/LevelEditor/Orpheus0829/Editor/LevelEditorWindow.cs");
            Require(!window.Contains("MigratePlacedBlocks();"), "旧窗口仍在打开或进入模式时隐式迁移对象。");

            string automation = File.ReadAllText("Assets/_Project/Code/Systems/GridArt/Editor/GridArtWindow.Automation.cs");
            int bindStart = automation.IndexOf("private void BindAutomation()", StringComparison.Ordinal);
            int bindEnd = automation.IndexOf("private static string DiskSignature", bindStart, StringComparison.Ordinal);
            Require(bindStart >= 0 && bindEnd > bindStart, "无法定位 GridArt BindAutomation。 ");
            string bindBody = automation.Substring(bindStart, bindEnd - bindStart);
            Require(!bindBody.Contains("buildPending = true"), "打开 GridArt 仍会排队构建。");

            string builder = File.ReadAllText("Assets/_Project/Code/Systems/GridArt/Editor/GridArtBuilder.cs");
            int earlyReturn = builder.IndexOf("output.IsBuiltFor(map.revision, paletteHash)", StringComparison.Ordinal);
            int ensureFolder = builder.IndexOf("EnsureFolder(folder)", StringComparison.Ordinal);
            Require(earlyReturn >= 0 && ensureFolder > earlyReturn, "GridArt 相同签名没有在任何写入前返回。");
        }

        private static void ValidateGridSpaceAndSummary()
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var managerObject = new GameObject("区域管理器");
                SceneManager.MoveGameObjectToScene(managerObject, preview);
                managerObject.transform.SetPositionAndRotation(new Vector3(7f, -3f, 2f), Quaternion.Euler(0f, 0f, 17f));
                managerObject.transform.localScale = new Vector3(1.25f, .8f, 1f);
                RoomLifecycleManager manager = managerObject.AddComponent<RoomLifecycleManager>();
                manager.Configure(new List<RoomRegion>
                {
                    new RoomRegion { id = "room_a", label = "房间 A", cells = new List<Vector2Int> { new Vector2Int(-2, 3), new Vector2Int(-1, 3) } },
                    new RoomRegion { id = "corridor_a", label = "通道 A", corridor = true, cells = new List<Vector2Int> { new Vector2Int(0, 3) } },
                }, 1.75f, "legacy-json-must-remain-untouched");

                SceneGridSpace grid = SceneGridSpace.For(manager);
                var regionCell = new Vector2Int(-2, 3);
                Vector3 world = grid.RegionCellCenter(regionCell, .4f);
                Require(grid.WorldToRegionCell(world) == regionCell, "RegionCell 世界坐标往返失败。");
                Vector2Int sceneCell = SceneGridSpace.RegionToScene(regionCell);
                Require(SceneGridSpace.SceneToRegion(sceneCell) == regionCell, "SceneCell 与 RegionCell 换算失败。");
                Require(grid.WorldToSceneCell(grid.SceneCellCenter(sceneCell, .4f)) == sceneCell, "SceneCell 世界坐标往返失败。");

                CreatePlaced(preview, "group_a", Vector2Int.zero);
                CreatePlaced(preview, "group_a", Vector2Int.right);
                CreatePlaced(preview, "group_b", Vector2Int.up);
                CreatePlaced(preview, string.Empty, Vector2Int.one);
                SceneLevelSummary summary = SceneLevelSummary.Read(preview);
                Require(summary.managers == 1 && summary.rooms == 1 && summary.corridors == 1 && summary.regionCells == 3,
                    "区域/通道摘要不一致。");
                Require(summary.placedObjects == 4 && summary.authorGroups == 2 && summary.repeatedGroups == 1 && summary.missingAuthorIds == 1,
                    "旧对象作者组摘要不一致。");
                Require(manager.PlanningJson == "legacy-json-must-remain-untouched", "只读接轨改写了旧 planningJson。");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void CreatePlaced(Scene scene, string id, Vector2Int cell)
        {
            var target = new GameObject("旧场景对象");
            SceneManager.MoveGameObjectToScene(target, scene);
            var placed = target.AddComponent<LevelEditorPlacedBlock>();
            placed.Configure(cell, "测试对象", Color.white, false, true, 1.75f);
            placed.SetPlanningSource(id, "signature", cell);
        }

        private static void ValidateViewRoundTrip()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return;
            bool in2D = view.in2DMode;
            bool orthographic = view.orthographic;
            Quaternion rotation = view.rotation;
            Vector3 pivot = view.pivot;
            float size = view.size;
            const string owner = "Stage1Audit";
            SceneEditSession.Enter(view, owner);
            Require(view.in2DMode && view.orthographic && Quaternion.Angle(view.rotation, Quaternion.identity) < .001f,
                "SceneView 未锁定为正交前视图。");
            SceneEditSession.Exit(view, owner);
            Require(view.in2DMode == in2D && view.orthographic == orthographic &&
                    Quaternion.Angle(view.rotation, rotation) < .001f &&
                    (view.pivot - pivot).sqrMagnitude < .000001f && Mathf.Approximately(view.size, size),
                "SceneView 状态没有逐字段恢复。");
        }

        private static void ValidateObjectLibraryUi()
        {
            var panel = new SceneLevelEditorPanel(null);
            Require(panel is ScrollView, "Overlay 正文没有可滚动容器。");
            List<string> buttons = panel.Query<Button>().ToList().Select(button => button.text).ToList();
            foreach (string required in new[]
                     {
                         "打开方块与道具列表…", "用当前内容开始画",
                         "点选并改参数", "连续放置", "擦除",
                         "编辑当前模板", "新建方块模板", "添加道具 Prefab",
                     })
                Require(buttons.Contains(required), "对象资源库缺少入口：" + required);

            ScenePlacementPaletteWindow picker =
                ScriptableObject.CreateInstance<ScenePlacementPaletteWindow>();
            try
            {
                picker.CreateGUI();
                Require(picker.rootVisualElement.Q<ToolbarSearchField>() != null,
                    "卡片选择器缺少搜索输入。");
                List<string> pickerButtons = picker.rootVisualElement.Query<Button>()
                    .ToList().Select(button => button.text).ToList();
                foreach (string filter in new[]
                         {
                             "全部内容", "只看方块", "只看道具",
                         })
                    Require(pickerButtons.Contains(filter),
                        "卡片选择器缺少直观分类按钮：" + filter);
                Require(pickerButtons.Contains("开始在 Scene 中放置"),
                    "卡片选择器缺少醒目的绘制主操作。");
                Require(pickerButtons.Contains("编辑初始模板"),
                    "卡片选择器缺少模板编辑入口。");
                Require(picker.rootVisualElement.Query<Label>().ToList()
                        .Any(label => label.text != null &&
                                      label.text.StartsWith("找到 ")),
                    "卡片选择器缺少结果数量与操作提示。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(picker);
            }
        }

        private static void ValidateArtLibraryUi()
        {
            GridArtPalette palette =
                ScriptableObject.CreateInstance<GridArtPalette>();
            GridArtMap map = ScriptableObject.CreateInstance<GridArtMap>();
            SceneArtPaletteWindow picker =
                ScriptableObject.CreateInstance<SceneArtPaletteWindow>();
            try
            {
                palette.brushes.Add(new GridArtBrush
                {
                    label = "测试装饰",
                    category = "装饰",
                });
                palette.brushes.Add(new GridArtBrush
                {
                    label = "测试自动地形",
                    category = "地表",
                    autoTerrain = true,
                });
                map.palette = palette;
                picker.SetTargetForAudit(map);
                picker.CreateGUI();

                Require(picker.rootVisualElement.Q<ToolbarSearchField>() != null,
                    "美术素材卡片缺少搜索输入。");
                Require(picker.rootVisualElement.Query<PopupField<string>>()
                        .ToList().Any(),
                    "美术素材卡片缺少分类筛选。");
                List<string> buttons = picker.rootVisualElement.Query<Button>()
                    .ToList().Select(button => button.text).ToList();
                Require(buttons.Contains("开始在 Scene 中绘制"),
                    "美术素材卡片缺少醒目的 Scene 绘制主操作。");
                Require(buttons.Count(text => string.IsNullOrEmpty(text)) >= 2,
                    "美术素材卡片没有完整显示素材库内容。");
                SceneArtPaletteSelection.Select(map, 1);
                Require(SceneArtPaletteSelection.Current(map) == 1,
                    "美术素材选择没有保持当前卡片。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(picker);
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(palette);
            }
        }

        private static Dictionary<string, string> ReadFormalHashes() =>
            FormalHashes.Keys.ToDictionary(path => path, Sha256, StringComparer.Ordinal);

        private static void ValidateFormalBaseline(IReadOnlyDictionary<string, string> actual)
        {
            foreach (var pair in FormalHashes)
                Require(actual.TryGetValue(pair.Key, out string hash) && hash == pair.Value,
                    "正式资源已偏离阶段基线：" + pair.Key);
        }

        private static bool HashesEqual(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
            left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out string value) && value == pair.Value);

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
