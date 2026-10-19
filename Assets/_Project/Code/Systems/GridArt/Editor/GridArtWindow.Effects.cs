using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public sealed partial class GridArtWindow
    {
        // These modes belong to the art canvas, not to the planning editor or its gameplay palette.
        [SerializeField] private int effectMode;
        [SerializeField] private GridArtEffectSettings effectSettings = new GridArtEffectSettings();
        [SerializeField] private Component selectedEffect;
        private bool effectDragging, effectCacheDirty = true;
        private Vector2Int effectStart, effectLast;
        private int effectSceneHandle;
        private IReadOnlyList<GridArtEffectRegion> effectRegions = Array.Empty<GridArtEffectRegion>();
        internal bool EffectToolActive => effectMode != 0;
        private float EffectCellSize => Mathf.Max(.01f, map != null ? map.cellSize : levelSource?.cellSize ?? 1f);

        private void BuildEffectsPanel(VisualElement parent)
        {
            var panel = new Foldout { name = "art-effects-panel", text = "区域效果 · 雨 / 萤火", value = EffectToolActive };
            parent.Add(panel);
            panel.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                GridArtEffectsSource.Changed += OnEffectsChanged;
                EditorApplication.hierarchyChanged += OnEffectHierarchyChanged;
            });
            panel.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                GridArtEffectsSource.Changed -= OnEffectsChanged;
                EditorApplication.hierarchyChanged -= OnEffectHierarchyChanged;
                EffectCancelGesture();
            });
            PopulateEffectsPanel(panel);
        }

        private void OnEffectsChanged()
        {
            effectCacheDirty = true;
            canvas?.RepaintView();
        }

        private void OnEffectHierarchyChanged()
        {
            OnEffectsChanged();
            // A hierarchy notification may arrive while the window is rebuilding; defer the UI work.
            var panel = rootVisualElement.Q<Foldout>("art-effects-panel");
            panel?.schedule.Execute(() =>
            {
                if (panel.panel != null) PopulateEffectsPanel(panel);
            });
        }

        private void RefreshEffectsPanel()
        {
            var panel = rootVisualElement.Q<Foldout>("art-effects-panel");
            if (panel != null) PopulateEffectsPanel(panel);
            canvas?.RepaintView();
        }

        private void PopulateEffectsPanel(Foldout panel)
        {
            panel.contentContainer.Clear();
            effectSettings ??= new GridArtEffectSettings();
            var modes = new List<string> { "图案绘制", "选择区域", "框画雨区", "框画萤火区" };
            effectMode = Mathf.Clamp(effectMode, 0, modes.Count - 1);
            var mode = new PopupField<string>("操作", modes, effectMode) { name = "art-effect-mode" };
            mode.RegisterValueChangedCallback(e =>
            {
                EffectCancelGesture(); effectMode = modes.IndexOf(e.newValue);
                if (EffectToolActive) { EndPreview(); StopScenePainting(); panel.value = true; }
                RefreshEffectsPanel();
            });
            panel.Add(mode);
            if (!GridArtEffectsSource.Available)
            {
                panel.Add(new HelpBox("区域效果适配器未载入，请等待脚本编译完成。", HelpBoxMessageType.Warning));
                return;
            }
            bool canAuthor = CanAuthorEffects();
            mode.SetEnabled(canAuthor);
            if (!canAuthor) panel.Add(new HelpBox("请退出运行 / Prefab 模式，在普通关卡场景编辑区域。", HelpBoxMessageType.Info));
            if (effectMode >= 2)
            {
                panel.Add(new HelpBox("在画布拖出活动范围。松开创建，Esc取消；中键平移，滚轮缩放。", HelpBoxMessageType.Info));
                if (effectMode == 2)
                {
                    var colors = new List<string> { "红色", "蓝色", "绿色" };
                    var color = new PopupField<string>("雨滴颜色", colors, Mathf.Clamp(effectSettings.rainColor, 0, 2));
                    color.RegisterValueChangedCallback(e => effectSettings.rainColor = colors.IndexOf(e.newValue)); panel.Add(color);
                    var density = new Slider("密度 / 格宽", 0, 100) { value = effectSettings.rainDensity, showInputField = true };
                    density.RegisterValueChangedCallback(e => effectSettings.rainDensity = e.newValue); panel.Add(density);
                    var seed = new IntegerField("固定种子") { value = effectSettings.rainSeed, isDelayed = true };
                    seed.RegisterValueChangedCallback(e => effectSettings.rainSeed = e.newValue); panel.Add(seed);
                    var depth = new FloatField("Z 深度") { value = effectSettings.rainDepth, isDelayed = true };
                    depth.RegisterValueChangedCallback(e => effectSettings.rainDepth = e.newValue); panel.Add(depth);
                    panel.Add(new Label("固定原像素雨滴 · 每区最多 512 颗"));
                }
                else
                {
                    var count = new IntegerField("萤火虫数量") { value = effectSettings.fireflyCount, isDelayed = true };
                    count.RegisterValueChangedCallback(e => { effectSettings.fireflyCount = Mathf.Clamp(e.newValue, 0, 128); count.SetValueWithoutNotify(effectSettings.fireflyCount); }); panel.Add(count);
                    var depth = new FloatField("可见平面（格）") { value = effectSettings.fireflyDepthCells, isDelayed = true };
                    depth.tooltip = "相对区域根的Z偏移；负值靠近相机。默认 −0.25，角色 / 道具前显示，实体墙仍遮挡。";
                    depth.RegisterValueChangedCallback(e => effectSettings.fireflyDepthCells = e.newValue); panel.Add(depth);
                    panel.Add(new Label("硬点 1 美术像素 · 每区最多 128 只 · 默认不补光"));
                }
            }
            else panel.Add(new Label("选择范围框或下列区域，可在此直接调参。"));
            EnsureEffectsCache();
            var list = new Foldout { text = "当前场景区域（" + effectRegions.Count + "）", value = true };
            panel.Add(list);
            foreach (var region in effectRegions)
            {
                Component target = region.target;
                var button = ActionButton(region.label, () => { selectedEffect = target; effectMode = 1; RefreshEffectsPanel(); });
                button.EnableInClassList("art-selected", target == selectedEffect);
                button.tooltip = "点击在工作台编辑；不需要Hierarchy选择。";
                list.Add(button);
            }
            if (effectRegions.Count == 0) list.Add(new Label("暂无区域 · 选择框画工具后在画布创建"));
            if (selectedEffect != null && selectedEffect.gameObject.scene == SceneManager.GetActiveScene())
            {
                var editor = GridArtEffectsSource.BuildInspector?.Invoke(selectedEffect, OnEffectsChanged);
                if (editor != null) { editor.SetEnabled(canAuthor); panel.Add(editor); }
                var row = Row();
                row.Add(ActionButton("定位场景", () =>
                {
                    Selection.activeGameObject = selectedEffect.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }));
                var delete = ActionButton("删除此区域", DeleteSelectedEffect); delete.style.color = new Color(1f, .45f, .4f);
                delete.SetEnabled(canAuthor); row.Add(delete); panel.Add(row);
            }
            panel.Add(new HelpBox("区域独立保存在Scene；不进入玩法存档、不参与颜色能力线。使用上方“保存场景”交付，须连同 planning 和依赖一起提交。", HelpBoxMessageType.Info));
        }

        private static bool CanAuthorEffects()
        {
            Scene scene = SceneManager.GetActiveScene();
            return GridArtEffectsSource.Available && !EditorApplication.isPlayingOrWillChangePlaymode &&
                scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene) &&
                PrefabStageUtility.GetCurrentPrefabStage() == null;
        }

        private void EnsureEffectsCache()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!effectCacheDirty && effectSceneHandle == scene.handle) return;
            effectCacheDirty = false; effectSceneHandle = scene.handle;
            effectRegions = GridArtEffectsSource.Read?.Invoke(scene) ?? Array.Empty<GridArtEffectRegion>();
            if (selectedEffect != null && selectedEffect.gameObject.scene != scene) selectedEffect = null;
        }

        internal bool EffectPointerDown(Vector2Int cell, int button)
        {
            if (!EffectToolActive || button == 2) return false;
            if (button == 1) { EffectCancelGesture(); return true; }
            if (!CanAuthorEffects()) return true;
            if (effectMode == 1)
            {
                EnsureEffectsCache();
                Vector2 world = ((Vector2)cell + Vector2.one * .5f) * EffectCellSize;
                Component match = null;
                float smallest = float.PositiveInfinity;
                foreach (var region in effectRegions)
                    if (region.worldBounds.Contains(world) && region.worldBounds.width * region.worldBounds.height < smallest)
                    { match = region.target; smallest = region.worldBounds.width * region.worldBounds.height; }
                selectedEffect = match; RefreshEffectsPanel();
                return true;
            }
            effectStart = effectLast = cell; effectDragging = true;
            canvas?.RepaintView(); return true;
        }

        internal void EffectPointerMove(Vector2Int cell)
        { if (effectDragging && effectLast != cell) { effectLast = cell; canvas?.RepaintView(); } }

        internal void EffectPointerUp(Vector2Int cell)
        {
            if (!effectDragging) return;
            effectLast = cell; effectDragging = false;
            if (!CanAuthorEffects()) { canvas?.RepaintView(); return; }
            RectInt cells = EffectSelection(effectStart, effectLast);
            if (cells.width > 4096 || cells.height > 4096)
            { ShowNotification(new GUIContent("区域边长最多4096格，请分区布置。")); canvas?.RepaintView(); return; }
            try
            {
                effectSettings.kind = effectMode == 2 ? GridArtEffectKind.Rain : GridArtEffectKind.Fireflies;
                selectedEffect = GridArtEffectsSource.Create(effectSettings, cells, EffectCellSize);
                effectCacheDirty = true; effectMode = 1;
                if (status != null) status.text = "已创建独立美术区域 · 保存场景后交付";
                RefreshEffectsPanel(); SceneView.RepaintAll();
            }
            catch (Exception error) { ShowNotification(new GUIContent(error.Message)); }
        }

        internal void EffectCancelGesture() { effectDragging = false; canvas?.RepaintView(); }
        private static RectInt EffectSelection(Vector2Int a, Vector2Int b) => new RectInt(
            Vector2Int.Min(a, b), new Vector2Int(Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1));

        private void DeleteSelectedEffect()
        {
            if (!CanAuthorEffects() || selectedEffect == null ||
                !EditorUtility.DisplayDialog("删除美术区域", "删除“" + selectedEffect.gameObject.name + "”？不会修改玩法布局。", "删除", "取消")) return;
            Scene scene = selectedEffect.gameObject.scene;
            Undo.DestroyObjectImmediate(selectedEffect.gameObject); selectedEffect = null;
            EditorSceneManager.MarkSceneDirty(scene); OnEffectsChanged(); RefreshEffectsPanel();
        }

        internal void DrawEffectOverlay(Painter2D painter, Func<Rect, Rect> toScreen, Rect clip)
        {
            if (!EffectToolActive) return;
            EnsureEffectsCache();
            foreach (var region in effectRegions)
            {
                if (region.target == null) continue;
                Rect bounds = region.worldBounds;
                float size = EffectCellSize;
                Rect screen = toScreen(new Rect(bounds.position / size, bounds.size / size));
                if (!screen.Overlaps(clip)) continue;
                Color tint = region.tint; tint.a = selectedEffect == region.target ? 1f : .65f;
                DrawEffectRect(painter, screen, tint, selectedEffect == region.target ? 3f : 1.5f);
            }
            if (effectDragging)
            {
                RectInt area = EffectSelection(effectStart, effectLast);
                DrawEffectRect(painter, toScreen(new Rect(area.position, area.size)), new Color(.45f, .9f, 1f), 2.5f);
            }
        }

        private static void DrawEffectRect(Painter2D painter, Rect box, Color color, float width)
        {
            painter.lineWidth = width; painter.strokeColor = color; painter.BeginPath();
            painter.MoveTo(box.min); painter.LineTo(new Vector2(box.xMax, box.yMin));
            painter.LineTo(box.max); painter.LineTo(new Vector2(box.xMin, box.yMax)); painter.ClosePath(); painter.Stroke();
        }
    }
}
