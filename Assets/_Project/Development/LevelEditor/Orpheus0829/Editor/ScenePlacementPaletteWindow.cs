using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    /// <summary>
    /// Visual spawn-template picker. Selecting an item changes only editor state;
    /// a Scene write happens later, when the author actually paints an instance.
    /// </summary>
    internal sealed class ScenePlacementPaletteWindow : EditorWindow
    {
        private const string FilterKey =
            "2026TapTap.ScenePlacementPalette.Filter";

        private readonly List<ScenePaletteItem> items = new();
        private readonly Dictionary<ScenePaletteItem, Button> cards = new();
        private DropdownField kindFilter;
        private ToolbarSearchField search;
        private ScrollView library;
        private Label selectionTitle;
        private Label selectionDetails;
        private Button paintButton;
        private Button editButton;
        private ScenePaletteItem selected;

        [MenuItem("Tools/2026TapTap/关卡编辑器/选择方块和道具")]
        internal static void Open()
        {
            ScenePlacementPaletteWindow window =
                GetWindow<ScenePlacementPaletteWindow>();
            window.titleContent = new GUIContent("方块与道具");
            window.minSize = new Vector2(520f, 430f);
            window.Show();
            window.Focus();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            Label title = new Label("选择要画的方块或道具");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);

            Label hint = new Label(
                "单击卡片选择；双击卡片，或点击底部蓝色按钮，立即回到正交 SceneView 绘制。");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.opacity = .72f;
            hint.style.marginBottom = 8f;
            rootVisualElement.Add(hint);

            VisualElement tools = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 7f,
                },
            };
            kindFilter = new DropdownField(
                new List<string> { "全部", "方块", "道具" },
                Mathf.Clamp(SessionState.GetInt(FilterKey, 0), 0, 2));
            kindFilter.style.width = 110f;
            kindFilter.RegisterValueChangedCallback(_ =>
            {
                SessionState.SetInt(FilterKey, kindFilter.index);
                RebuildCards();
            });
            tools.Add(kindFilter);

            search = new ToolbarSearchField
            {
                tooltip = "按名称或 Prefab 路径搜索",
            };
            search.style.flexGrow = 1f;
            search.style.marginLeft = 6f;
            search.RegisterValueChangedCallback(_ => RebuildCards());
            tools.Add(search);
            rootVisualElement.Add(tools);

            library = new ScrollView(ScrollViewMode.Vertical);
            library.style.flexGrow = 1f;
            library.style.minHeight = 230f;
            library.style.borderTopWidth = 1f;
            library.style.borderBottomWidth = 1f;
            library.style.borderTopColor = new Color(.2f, .22f, .25f);
            library.style.borderBottomColor = new Color(.2f, .22f, .25f);
            library.style.paddingTop = 6f;
            library.style.paddingBottom = 6f;
            rootVisualElement.Add(library);

            VisualElement selection = new VisualElement
            {
                style =
                {
                    marginTop = 8f,
                    paddingTop = 7f,
                    paddingBottom = 2f,
                },
            };
            selectionTitle = new Label("尚未选择模板");
            selectionTitle.style.fontSize = 14f;
            selectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            selection.Add(selectionTitle);
            selectionDetails = new Label();
            selectionDetails.style.whiteSpace = WhiteSpace.Normal;
            selectionDetails.style.opacity = .72f;
            selectionDetails.style.marginBottom = 6f;
            selection.Add(selectionDetails);

            VisualElement actions = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row },
            };
            editButton = new Button(EditSelected)
            {
                text = "编辑模板",
            };
            editButton.style.width = 110f;
            actions.Add(editButton);
            paintButton = new Button(StartPainting)
            {
                text = "选择并开始绘制",
            };
            paintButton.style.flexGrow = 1f;
            paintButton.style.height = 31f;
            paintButton.style.marginLeft = 6f;
            paintButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            paintButton.style.backgroundColor = new Color(.12f, .42f, .72f);
            actions.Add(paintButton);
            selection.Add(actions);
            rootVisualElement.Add(selection);

            ReloadPalette();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged -= ReloadPalette;
            EditorApplication.projectChanged += ReloadPalette;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ReloadPalette;
        }

        private void ReloadPalette()
        {
            if (rootVisualElement == null || library == null)
            {
                return;
            }

            items.Clear();
            items.AddRange(ScenePlacementPaletteCatalog.Build(
                ScenePlacementPaletteCatalog.LoadPalette()));
            selected = ScenePlacementPaletteCatalog.Current(items) ??
                       items.FirstOrDefault();
            RebuildCards();
            RefreshSelection();
        }

        private void RebuildCards()
        {
            if (library == null)
            {
                return;
            }

            library.Clear();
            cards.Clear();
            string query = search?.value?.Trim() ?? string.Empty;
            int kind = kindFilter?.index ?? 0;
            List<ScenePaletteItem> filtered = items.Where(item =>
                    (kind == 0 || kind == 1 && !item.isProp ||
                     kind == 2 && item.isProp) &&
                    (query.Length == 0 || item.SearchText.IndexOf(
                         query, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (filtered.Count == 0)
            {
                library.Add(new HelpBox(
                    "没有符合当前筛选的模板。可清空搜索，或在场景编辑器中新增模板。",
                    HelpBoxMessageType.Info));
                return;
            }

            string[] groupNames =
            {
                "红色方块", "蓝色方块", "绿色方块", "其他有色方块", "无色方块",
                "红色道具", "蓝色道具", "绿色道具", "其他有色道具", "无色道具",
            };
            for (int group = 0; group < groupNames.Length; group++)
            {
                List<ScenePaletteItem> grouped = filtered
                    .Where(item => GroupIndex(item) == group)
                    .ToList();
                if (grouped.Count == 0)
                {
                    continue;
                }

                Foldout foldout = new Foldout
                {
                    text = $"{groupNames[group]}（{grouped.Count}）",
                    value = true,
                };
                foldout.style.marginBottom = 4f;
                VisualElement grid = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        flexWrap = Wrap.Wrap,
                        paddingTop = 4f,
                    },
                };
                foreach (ScenePaletteItem item in grouped)
                {
                    grid.Add(CreateCard(item));
                }
                foldout.Add(grid);
                library.Add(foldout);
            }

            RefreshCardBorders();
        }

        private VisualElement CreateCard(ScenePaletteItem item)
        {
            Button card = new Button(() => SelectItem(item))
            {
                text = string.Empty,
                tooltip = string.IsNullOrEmpty(item.AssetPath)
                    ? item.displayName
                    : item.AssetPath,
            };
            card.style.width = 112f;
            card.style.height = 104f;
            card.style.marginRight = 6f;
            card.style.marginBottom = 6f;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.FlexStart;
            card.style.paddingTop = 5f;
            card.style.paddingLeft = 4f;
            card.style.paddingRight = 4f;
            card.style.borderLeftWidth = 2f;
            card.style.borderRightWidth = 2f;
            card.style.borderTopWidth = 2f;
            card.style.borderBottomWidth = 2f;
            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.clickCount < 2)
                {
                    return;
                }
                SelectItem(item);
                StartPainting();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);

            VisualElement previewFrame = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            previewFrame.style.width = 66f;
            previewFrame.style.height = 62f;
            previewFrame.style.marginBottom = 3f;
            previewFrame.style.backgroundColor = item.isProp
                ? new Color(.1f, .11f, .13f)
                : item.color;
            previewFrame.style.borderLeftWidth = 1f;
            previewFrame.style.borderRightWidth = 1f;
            previewFrame.style.borderTopWidth = 1f;
            previewFrame.style.borderBottomWidth = 1f;
            previewFrame.style.borderLeftColor =
                previewFrame.style.borderRightColor =
                previewFrame.style.borderTopColor =
                previewFrame.style.borderBottomColor =
                    new Color(.28f, .3f, .34f);

            Texture preview = item.prefab != null
                ? AssetPreview.GetAssetPreview(item.prefab) ??
                  AssetPreview.GetMiniThumbnail(item.prefab)
                : null;
            if (preview != null)
            {
                Image image = new Image
                {
                    image = preview,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore,
                };
                image.style.width = Length.Percent(100f);
                image.style.height = Length.Percent(100f);
                previewFrame.Add(image);
            }
            card.Add(previewFrame);

            Label name = new Label(item.displayName)
            {
                pickingMode = PickingMode.Ignore,
            };
            name.style.width = Length.Percent(100f);
            name.style.maxHeight = 31f;
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.overflow = Overflow.Hidden;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.fontSize = 11f;
            card.Add(name);
            cards[item] = card;
            return card;
        }

        private void SelectItem(ScenePaletteItem item)
        {
            selected = item;
            ScenePlacementPaletteCatalog.Select(item);
            RefreshSelection();
            RefreshCardBorders();
        }

        private void RefreshSelection()
        {
            bool hasSelection = selected != null;
            paintButton?.SetEnabled(hasSelection);
            editButton?.SetEnabled(hasSelection);
            if (!hasSelection)
            {
                if (selectionTitle != null)
                {
                    selectionTitle.text = "尚未选择模板";
                    selectionDetails.text = string.Empty;
                }
                return;
            }

            selectionTitle.text =
                $"{selected.KindLabel} · {selected.displayName}";
            string path = string.IsNullOrEmpty(selected.AssetPath)
                ? "内置基础方块"
                : selected.AssetPath;
            selectionDetails.text =
                $"{ChannelLabel(selected.channel)} · 模板参数 {selected.overrideCount} 项\n{path}\n" +
                "生成后实例完全独立，只认 Scene 中的真实组件和 Prefab override。";
        }

        private void RefreshCardBorders()
        {
            foreach (KeyValuePair<ScenePaletteItem, Button> pair in cards)
            {
                Color border = pair.Key == selected
                    ? new Color(.24f, .72f, 1f)
                    : new Color(.18f, .2f, .24f);
                pair.Value.style.borderLeftColor = border;
                pair.Value.style.borderRightColor = border;
                pair.Value.style.borderTopColor = border;
                pair.Value.style.borderBottomColor = border;
            }
        }

        private void StartPainting()
        {
            if (selected == null)
            {
                return;
            }

            ScenePlacementPaletteCatalog.Select(selected);
            LevelEditorState.Tool = LevelEditorTool.Paint;
            SceneLevelEditorOverlay.BeginObjectPainting();
            Close();
        }

        private void EditSelected()
        {
            if (selected == null)
            {
                return;
            }

            if (selected.isProp)
            {
                LevelEditorPropEntryWindow.OpenForEdit(selected.index);
            }
            else
            {
                LevelEditorEntryWindow.OpenForEdit(selected.index);
            }
        }

        private int GroupIndex(ScenePaletteItem item)
        {
            if (item == null || item.palette == null)
            {
                return -1;
            }

            if (item.isProp)
            {
                LevelEditorPropEntry entry = item.index >= 0 &&
                                             item.index < item.palette.PropEntries.Count
                    ? item.palette.PropEntries[item.index]
                    : null;
                return 5 + (entry != null
                    ? LevelEditorPanel.GetPropColorGroup(entry)
                    : 4);
            }

            LevelEditorBlockEntry block = item.index >= 0 &&
                                          item.index < item.palette.Entries.Count
                ? item.palette.Entries[item.index]
                : null;
            return block != null
                ? LevelEditorPanel.GetBlockColorGroup(block)
                : 4;
        }

        private static string ChannelLabel(SceneObjectChannel channel) =>
            channel switch
            {
                SceneObjectChannel.TerrainSolid => "地形",
                SceneObjectChannel.WaterVolume => "水体",
                SceneObjectChannel.GameplayProp => "玩法道具",
                SceneObjectChannel.MarkerTrigger => "标记/触发器",
                SceneObjectChannel.DecorationObject => "装饰对象",
                _ => channel.ToString(),
            };
    }
}
