using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public sealed partial class GridArtWindow
    {
        private bool autoSave = true, autoPreview = true, savePending, buildPending, sourcePending;
        private bool automationBusy;
        private double saveAt, sourceAt;
        private string sourceGroup = "", saveError, diskSignature;
        private GridArtLevelSnapshot levelSource;
        private Label sourceSummary;
        private readonly Dictionary<string, HashSet<Vector2Int>> sourceCellCache = new Dictionary<string, HashSet<Vector2Int>>();
        internal bool AutoSave => autoSave;
        internal bool HasPendingSave => savePending;
        internal bool IsStroking => canvas != null && canvas.IsStroking || GridArtScenePainter.IsStroking;
        private void BindAutomation()
        {
            autoSave = EditorPrefs.GetBool(SceneKey + ".AutoSave", true);
            autoPreview = EditorPrefs.GetBool(SceneKey + ".AutoPreview", true);
            rootVisualElement.Q<Toggle>("art-autosave")?.SetValueWithoutNotify(autoSave);
            rootVisualElement.Q<Toggle>("art-autopreview")?.SetValueWithoutNotify(autoPreview);
            savePending = buildPending = false; saveError = null;
            sourceGroup = Layer != null && Layer.followLevel ? Layer.sourceGroup : "";
            levelSource = null; sourceCellCache.Clear(); guides.Clear();
            diskSignature = DiskSignature(map);
            RequestSource();
            // A saved map is sufficient to restore the preview even if the Scene was not saved.
            if (CanEditMap && map.palette != null) { buildPending = true; saveAt = EditorApplication.timeSinceStartup + .6; }
        }
        private static string DiskSignature(GridArtMap data)
        {
            string path = data != null ? AssetDatabase.GetAssetPath(data) : "";
            return File.Exists(path) ? Hash128.Compute(File.ReadAllText(path)).ToString() : "";
        }
        private void RequestSource()
        { sourcePending = true; sourceAt = EditorApplication.timeSinceStartup + .35; }
        private void QueuePersistence()
        {
            savePending = buildPending = true; saveError = null;
            saveAt = EditorApplication.timeSinceStartup + .65;
            UpdateSaveStatus();
        }
        private void FlushDataForPreviousScene()
        {
            // Active Scene has already switched. Only persist the old map, never generate in the new Scene.
            if (!autoSave || !savePending || map == null) return;
            try { SaveAuthoredData(); }
            catch (Exception e) { Debug.LogWarning(e.Message); }
        }
        private void SaveAuthoredData()
        {
            string actual = DiskSignature(map);
            if (diskSignature != null && actual != diskSignature)
            {
                string backup = "Library/GridArtRecovery/" + map.name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json";
                Directory.CreateDirectory("Library/GridArtRecovery");
                File.WriteAllText(backup, EditorJsonUtility.ToJson(map, true));
                throw new IOException("美术图已被外部更新，停止自动写入。当前副本：" + backup);
            }
            AssetDatabase.SaveAssetIfDirty(map);
            if (map.palette != null) AssetDatabase.SaveAssetIfDirty(map.palette);
            diskSignature = DiskSignature(map); savePending = false;
        }
        private void AutomationTick()
        {
            if (automationBusy || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || IsStroking) return;
            double now = EditorApplication.timeSinceStartup;
            if (sourcePending && now >= sourceAt) RefreshLevelSource();
            if (now >= saveAt && (autoSave && savePending || autoPreview && buildPending)) FlushPending(false);
        }
        private void FlushPending(bool closing)
        {
            if (automationBusy || map == null || !CanEditMap || EditorApplication.isPlaying || IsStroking) return;
            automationBusy = true;
            try
            {
                if (savePending && autoSave)
                {
                    SaveAuthoredData();
                }
                if (buildPending && autoPreview && !closing && map.palette != null && !string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && PrefabStageUtility.GetCurrentPrefabStage() == null)
                {
                    // Generated output is derived data: do not pollute the authored Undo stack.
                    GridArtBuilder.Apply(map, SceneManager.GetActiveScene(), false, autoSave);
                    diskSignature = DiskSignature(map); buildPending = false;
                }
                UpdateSaveStatus();
            }
            catch (Exception e)
            { saveError = e.Message; savePending = buildPending = false; UpdateSaveStatus(); }
            finally { automationBusy = false; }
        }
        private void UpdateSaveStatus()
        {
            if (status == null || map == null) return;
            if (!string.IsNullOrEmpty(saveError)) { status.text = saveError; return; }
            string state = savePending ? autoSave ? "等待自动保存" : "美术未保存" : "美术已保存";
            string preview = buildPending ? "预览待更新" : "场景预览已更新";
            status.text = state + " · " + preview + (SceneManager.GetActiveScene().isDirty ? " · 场景待保存" : "") + " · " + (Layer?.label ?? "无图层") + " · " + (Layer?.cells.Count ?? 0) + " 格";
            status.tooltip = AssetDatabase.GetAssetPath(map);
        }
        private void SaveAll()
        {
            if (!CanEditMap || IsStroking || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Explicit save may save the whole Scene; autosave deliberately does not.
            bool previous = autoSave; autoSave = true;
            savePending = true; FlushPending(false); autoSave = previous;
            if (!string.IsNullOrEmpty(saveError)) return;
            if (buildPending && map.palette != null) Apply();
            if (string.IsNullOrEmpty(saveError) && EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) UpdateSaveStatus();
        }
        private void OnArtUndo()
        {
            if (map != null) foreach (var layer in map.layers) layer.InvalidateLookup();
            RequestSource(); Refresh(); QueuePersistence();
        }
        private void ArtChanged(bool rebuildPanels = false)
        {
            canvas?.Invalidate();
            if (rebuildPanels) Refresh();
            QueuePersistence();
        }
        private void RefreshLevelSource()
        {
            sourcePending = false;
            try
            {
                if (GridArtLevelSource.Read == null) { if(sourceSummary!=null) sourceSummary.text="关卡来源尚未连接"; return; }
                levelSource = GridArtLevelSource.Read.Invoke(SceneManager.GetActiveScene());
                sourceCellCache.Clear(); guides.Clear();
                float size = map != null ? map.cellSize : levelSource.cellSize;
                foreach (var g in levelSource.groups) foreach (var r in g.bounds)
                    guides.Add(new Rect(r.position / size, r.size / size));
                bool changed = false;
                if (CanEditMap && map.palette != null)
                    foreach (var layer in map.layers)
                        if (layer.followLevel && map.palette.Get(layer.sourceBrush) != null)
                            changed |= GridArtLevelSource.Synchronize(layer, SourceCells(layer.sourceGroup));
                if (changed) { map.revision++; EditorUtility.SetDirty(map); ArtChanged(); }
                RefreshSourcePanel(); canvas?.RepaintView();
            }
            catch (Exception e) { levelSource=null; sourceCellCache.Clear(); guides.Clear(); canvas?.RepaintView(); if (sourceSummary != null) sourceSummary.text = e.Message; }
        }
        private HashSet<Vector2Int> SourceCells(string group)
        {
            group ??= "";
            if (!sourceCellCache.TryGetValue(group, out var cells))
                sourceCellCache[group] = cells = levelSource?.Cells(group, map != null ? map.cellSize : 1) ?? new HashSet<Vector2Int>();
            return cells;
        }
        private void FillLevel(bool linked)
        {
            if (!CanEditMap || Brush == null || Layer == null || Layer.locked || !Layer.visible) return;
            if (!Brush.autoTerrain && Brush.Size != Vector2Int.one) { status.text = "墙地铺绘请选择单格素材或自动地形。"; return; }
            if (sourcePending) RefreshLevelSource();
            var cells = SourceCells(sourceGroup);
            if (cells.Count == 0) { status.text = "该范围内没有白块；先在关卡编辑器搭建墙地。"; return; }
            var layer = Layer;
            if (layer.cells.Count > 0 && !layer.followLevel && !EditorUtility.DisplayDialog("铺绘墙地", "替换当前图层的美术？其他图层和玩法物体不变。", "替换", "取消")) return;
            Change("铺绘关卡墙地", () =>
            {
                layer.sourceGroup = sourceGroup; layer.sourceBrush = brushIndex; layer.followLevel = linked;
                GridArtLevelSource.Synchronize(layer, cells);
            });
            canvas?.FrameAll();
        }
        private void RefreshSourcePanel()
        {
            var panel = rootVisualElement.Q<VisualElement>("art-level-source");
            if (panel == null) return;
            panel.Clear(); panel.Add(Heading("关卡墙地"));
            sourceSummary = new Label(levelSource?.label ?? "正在读取当前关卡…"); sourceSummary.style.whiteSpace = WhiteSpace.Normal; panel.Add(sourceSummary);
            var ids = new List<string> { "" }; var names = new List<string> { "整关墙地" };
            if (levelSource != null) foreach (var group in levelSource.groups) { ids.Add(group.id); names.Add(names.Contains(group.label) ? group.label+" · "+(names.Count+1) : group.label); }
            if (!ids.Contains(sourceGroup)) sourceGroup = "";
            var picker = new PopupField<string>("铺绘范围", names, ids.IndexOf(sourceGroup));
            picker.RegisterValueChangedCallback(e => { sourceGroup = ids[names.IndexOf(e.newValue)]; }); panel.Add(picker);
            var follow = ActionButton("自动铺绘并跟随白块", () => FillLevel(true)); follow.name = "art-follow-level";
            follow.tooltip = "当前层由关卡墙地自动生成，新增、擦除及合并白块后更新；手工装饰放其他图层。";
            panel.Add(follow); panel.Add(ActionButton("仅铺绘一次", () => FillLevel(false)));
            bool canFill = CanEditMap && Layer != null && !Layer.locked && Layer.visible && Brush != null && !EditorApplication.isPlayingOrWillChangePlaymode;
            follow.SetEnabled(canFill); panel.Children().OfType<Button>().ToList().ForEach(b => b.SetEnabled(canFill));
            if (Layer != null && Layer.followLevel)
            {
                panel.Add(new HelpBox("当前层随白块更新；装饰请放到独立图层。", HelpBoxMessageType.Info));
                panel.Add(ActionButton("转为手工绘制", () => Change("转为手工美术", () => Layer.followLevel = false)));
            }
            panel.Add(ActionButton("刷新关卡轮廓", () => { RequestSource(); RefreshLevelSource(); }));
            panel.Add(ActionButton("打开关卡编辑器", () => EditorApplication.ExecuteMenuItem("Tools/2026TapTap/关卡规划原型/打开融合编辑器")));
        }
    }
}
