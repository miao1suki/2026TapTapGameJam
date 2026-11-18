using System;
using System.Collections.Generic;
using System.Linq;
using Project.GridArt;
using Project.BlockFeatures.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    /// <summary>Shared read-only resource view. A card changes editor selection, never Scene or assets.</summary>
    internal sealed class SceneResourceDrawer : VisualElement
    {
        private readonly Action<bool> chooseKind;
        private readonly Action<ScenePaletteItem> chooseObject;
        private readonly Action<int> chooseArt;
        private readonly Button objects, art;
        private readonly ToolbarSearchField search;
        private readonly DropdownField filter;
        private readonly Label count, current;
        private readonly VisualElement grid;
        private readonly List<(Button button, string key)> cards = new();
        private IReadOnlyList<ScenePaletteItem> items = Array.Empty<ScenePaletteItem>();
        private GridArtMap map;
        private bool artKind;
        private int paletteVersion = -1, catalogVersion = -1, paletteId, itemCount = -1;
        private int projectVersion;
        private string selectedKey;
        private IVisualElementScheduledItem searchRefresh;
        internal int RebuildCount { get; private set; }

        internal SceneResourceDrawer(Action<bool> chooseKind, Action<ScenePaletteItem> chooseObject, Action<int> chooseArt)
        {
            this.chooseKind = chooseKind; this.chooseObject = chooseObject; this.chooseArt = chooseArt;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            objects = new Button(() => SetKind(false, true)) { text = "物体" };
            art = new Button(() => SetKind(true, true)) { text = "美术" };
            foreach (var button in new[] { objects, art }) { button.style.flexGrow = 1; button.style.height = 28; row.Add(button); }
            Add(row);
            search = new ToolbarSearchField { tooltip = "搜索名称、类型或资源路径；只筛选，不修改资源。" };
            search.style.marginTop = 4;
            search.RegisterValueChangedCallback(_ => { searchRefresh?.Pause(); searchRefresh = schedule.Execute(Rebuild).StartingIn(100); }); Add(search);
            filter = new DropdownField { label = "筛选" }; filter.RegisterValueChangedCallback(_ => Rebuild()); Add(filter);
            count = new Label { style = { fontSize = 11, color = SceneWorkbenchTheme.Muted } }; Add(count);
            current = new Label { style = { fontSize = 12, whiteSpace = WhiteSpace.Normal, marginTop = 4 } }; Add(current);
            grid = new VisualElement { name = "resource-grid", style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, flexShrink = 0 } };
            Add(grid);
            RegisterCallback<AttachToPanelEvent>(_ => { EditorApplication.projectChanged -= ProjectChanged; EditorApplication.projectChanged += ProjectChanged; });
            RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= ProjectChanged);
            SetKind(false, false);
        }

        private void ProjectChanged() => projectVersion++;

        internal void Bind(IReadOnlyList<ScenePaletteItem> values, GridArtMap target, bool showArt)
        {
            // Painting changes map.revision, not the palette. Do not recreate cards or reset filters per stroke.
            bool changed = map != target || paletteVersion != projectVersion || catalogVersion != LevelEditorState.PaletteRevision || itemCount != values.Count ||
                paletteId != (target?.palette != null ? target.palette.GetInstanceID() : 0);
            items = values; map = target; paletteVersion = projectVersion; catalogVersion = LevelEditorState.PaletteRevision; itemCount = values.Count;
            paletteId = target?.palette != null ? target.palette.GetInstanceID() : 0;
            if (artKind != showArt) { SetKind(showArt, false); return; }
            if (changed) { SetFilters(); Rebuild(); } else RefreshSelection();
        }

        private void SetKind(bool value, bool notify)
        {
            artKind = value;
            SceneWorkbenchTheme.Tool(objects, !value, SceneWorkbenchTheme.Object);
            SceneWorkbenchTheme.Tool(art, value, SceneWorkbenchTheme.Art);
            SetFilters(); Rebuild(); if (notify) chooseKind(value);
        }

        private void SetFilters()
        {
            string previous = filter.value;
            filter.choices = artKind
                ? new[] { "全部" }.Concat(map?.palette?.brushes.Where(b => b != null).Select(b => b.category ?? "装饰").Distinct().OrderBy(s => s) ?? Enumerable.Empty<string>()).ToList()
                : new List<string> { "全部", "方块", "水体", "道具", "标记", "装饰" };
            filter.SetValueWithoutNotify(filter.choices.Contains(previous) ? previous : filter.choices[0]);
        }

        private void Rebuild()
        {
            RebuildCount++; selectedKey = null;
            grid.Clear(); cards.Clear(); string query = search.value?.Trim() ?? string.Empty;
            string category = filter.value; int total = 0;
            bool Matches(string text) => string.IsNullOrEmpty(query) || text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            if (artKind && map?.palette != null)
            {
                for (int i = 0; i < map.palette.brushes.Count; i++)
                {
                    GridArtBrush brush = map.palette.Get(i);
                    if (brush == null || !Matches($"{brush.label} {brush.category} {brush.sourceKey}") || category != "全部" && category != brush.category) continue;
                    int index = i; var button = Card(brush.label, brush.autoTerrain ? brush.HasWideContour ? brush.widePieces[0] : brush.GetPiece(0) ?? brush.sprite : brush.sprite, null,
                        new Color(.16f,.17f,.19f), () => { chooseArt(index); RefreshSelection(); });
                    cards.Add((button, i.ToString())); total++;
                }
            }
            else if (!artKind)
            {
                foreach (var item in items)
                {
                    string kind = item.channel switch { SceneObjectChannel.TerrainSolid => "方块", SceneObjectChannel.WaterVolume => "水体", SceneObjectChannel.MarkerTrigger => "标记", SceneObjectChannel.DecorationObject => "装饰", _ => "道具" };
                    if (!Matches(item.SearchText) || category != "全部" && category != kind) continue;
                    PlanningArtworkPreview? artwork = null;
                    if (item.prefab != null)
                    {
                        IReadOnlyList<LevelEditorComponentValueOverride> overrides = item.palette == null ? null : item.isProp
                            ? item.palette.PropEntries[item.index].ComponentValueOverrides : item.palette.Entries[item.index].ComponentValueOverrides;
                        if (ObjectArtworkPlanningPreview.TryGet(item.prefab,overrides,out var actual) && !actual.Hidden) artwork = actual;
                    }
                    Texture preview = !artwork.HasValue && item.prefab != null ? AssetPreview.GetAssetPreview(item.prefab) ?? AssetPreview.GetMiniThumbnail(item.prefab) : null;
                    var button = Card(item.displayName, artwork?.Sprite, preview, item.color, () => { chooseObject(item); RefreshSelection(); }, artwork);
                    button.tooltip = $"{kind} · {item.displayName}\n单击选择；开启落笔后在 Scene 绘制。\n{item.AssetPath}";
                    cards.Add((button, $"{item.isProp}:{item.index}")); total++;
                }
            }
            count.text = $"{total} 项 · 单击选择，Scene 落笔";
            if (total == 0)
            {
                grid.Add(new Label(artKind && map == null ? "当前场景没有美术 Map" : "没有匹配项"));
                grid.Add(new Button(() => { search.value = string.Empty; filter.value = "全部"; Rebuild(); }) { text = "清除筛选" });
            }
            RefreshSelection();
        }

        private Button Card(string title, Sprite sprite, Texture texture, Color fallback, Action click, PlanningArtworkPreview? artwork = null)
        {
            var button = new Button(click) { name = "resource-card", tooltip = title };
            button.style.width = 72; button.style.height = 86; button.style.marginRight = 4; button.style.marginBottom = 4;
            button.style.paddingTop = 3; button.style.paddingLeft = 2; button.style.paddingRight = 2;
            button.style.flexShrink = 0; button.style.flexDirection = FlexDirection.Column; button.style.alignItems = Align.Center;
            button.style.backgroundColor = SceneWorkbenchTheme.Surface;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 3;
            SceneWorkbenchTheme.Border(button, SceneWorkbenchTheme.Line, 2);
            button.RegisterCallback<PointerEnterEvent>(_ => SceneWorkbenchTheme.Border(button, artKind ? SceneWorkbenchTheme.Art : SceneWorkbenchTheme.Object, 2));
            button.RegisterCallback<PointerLeaveEvent>(_ => { selectedKey = null; RefreshSelection(); });
            var preview = new Image { sprite = sprite, image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            if (sprite != null) preview.sprite = sprite;
            if (artwork.HasValue)
            {
                var actual = artwork.Value;
                if (actual.Sprite == null) { preview.image = actual.Texture; preview.uv = actual.UV; }
                preview.style.rotate = new Rotate(new Angle(-actual.RotationDegrees,AngleUnit.Degree));
                preview.style.scale = new Scale(new Vector3(actual.FlipX ? -1 : 1,1,1));
            }
            preview.style.width = 52; preview.style.height = 48; preview.style.backgroundColor = texture == null && sprite == null && !artwork.HasValue ? fallback : SceneWorkbenchTheme.Well;
            button.Add(preview);
            button.Add(new Label(title) { pickingMode = PickingMode.Ignore, style = { fontSize = 11, height = 28, whiteSpace = WhiteSpace.Normal, overflow = Overflow.Hidden, unityTextAlign = TextAnchor.MiddleCenter } });
            grid.Add(button); return button;
        }

        private void RefreshSelection()
        {
            var selected = ScenePlacementPaletteCatalog.Current(items);
            int index = SceneArtPaletteSelection.Current(map);
            string key = artKind ? index.ToString() : selected != null ? $"{selected.isProp}:{selected.index}" : string.Empty;
            if (selectedKey == key) return;
            selectedKey = key;
            foreach (var card in cards)
            {
                Color color = card.key == key ? artKind ? SceneWorkbenchTheme.Art : SceneWorkbenchTheme.Object : SceneWorkbenchTheme.Line;
                SceneWorkbenchTheme.Border(card.button, color, 2);
            }
            current.text = artKind ? $"当前美术：{map?.palette?.Get(index)?.label ?? "未选择"}" : $"当前物体：{selected?.displayName ?? "未选择"}";
        }
    }
}
