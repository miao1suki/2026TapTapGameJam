using System;
using System.Collections.Generic;
using System.Linq;
using Project.GridArt;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal static class SceneArtPaletteSelection
    {
        private const string KeyPrefix =
            "2026TapTap.SceneArtPalette.Brush.";

        internal static event Action<GridArtMap, int> Changed;

        internal static int Current(GridArtMap map)
        {
            if (map?.palette == null || map.palette.brushes.Count == 0)
            {
                return -1;
            }

            return Mathf.Clamp(
                SessionState.GetInt(Key(map), 0),
                0,
                map.palette.brushes.Count - 1);
        }

        internal static void Select(GridArtMap map, int index)
        {
            if (map?.palette == null ||
                index < 0 || index >= map.palette.brushes.Count)
            {
                return;
            }

            SessionState.SetInt(Key(map), index);
            Changed?.Invoke(map, index);
        }

        private static string Key(GridArtMap map)
        {
            string path = AssetDatabase.GetAssetPath(map);
            string identity = string.IsNullOrEmpty(path)
                ? map.GetInstanceID().ToString()
                : AssetDatabase.AssetPathToGUID(path);
            return KeyPrefix + identity;
        }
    }

    internal sealed class SceneArtPaletteWindow : EditorWindow
    {
        private GridArtMap map;
        private int selectedIndex = -1;
        private string category = "全部";
        private ToolbarSearchField search;
        private PopupField<string> categoryField;
        private ScrollView library;
        private Label resultCount;
        private Image selectionPreview;
        private Label selectionTitle;
        private Label selectionDetails;
        private Button paintButton;
        private readonly Dictionary<int, Button> cards = new();

        internal static void Open(GridArtMap target)
        {
            SceneArtPaletteWindow window =
                GetWindow<SceneArtPaletteWindow>();
            window.titleContent = new GUIContent("美术素材");
            window.minSize = new Vector2(680f, 520f);
            window.map = target;
            window.selectedIndex = SceneArtPaletteSelection.Current(target);
            window.Show();
            window.Focus();
            if (window.rootVisualElement.childCount > 0)
            {
                window.RebuildAll();
            }
        }

        internal void SetTargetForAudit(GridArtMap target)
        {
            map = target;
            selectedIndex = SceneArtPaletteSelection.Current(target);
            if (rootVisualElement.childCount > 0)
            {
                RebuildAll();
            }
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            Label title = new Label("选择要铺的美术素材");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            Label hint = new Label(
                "沿用原美术工作台的素材卡片。单击查看，双击即可回到 Scene 开始绘制。");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.opacity = .72f;
            hint.style.marginBottom = 8f;
            root.Add(hint);

            VisualElement filters = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row },
            };
            search = new ToolbarSearchField
            {
                tooltip = "按素材名称或分类搜索",
            };
            search.style.flexGrow = 1f;
            search.RegisterValueChangedCallback(_ => RebuildCards());
            filters.Add(search);
            Button clear = new Button(() =>
            {
                search.value = string.Empty;
                search.Focus();
            }) { text = "清空" };
            clear.style.width = 58f;
            clear.style.marginLeft = 5f;
            filters.Add(clear);
            root.Add(filters);

            categoryField = new PopupField<string>(
                "素材分类",
                new List<string> { "全部" },
                0);
            categoryField.RegisterValueChangedCallback(evt =>
            {
                category = evt.newValue;
                RebuildCards();
            });
            root.Add(categoryField);

            resultCount = new Label();
            resultCount.style.fontSize = 10f;
            resultCount.style.opacity = .68f;
            resultCount.style.marginBottom = 4f;
            root.Add(resultCount);

            library = new ScrollView(ScrollViewMode.Vertical);
            library.style.flexGrow = 1f;
            library.style.minHeight = 250f;
            library.style.borderTopWidth = 1f;
            library.style.borderBottomWidth = 1f;
            library.style.borderTopColor = new Color(.2f, .22f, .25f);
            library.style.borderBottomColor = new Color(.2f, .22f, .25f);
            root.Add(library);

            VisualElement selection = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 8f,
                },
            };
            selectionPreview = new Image { scaleMode = ScaleMode.ScaleToFit };
            selectionPreview.style.width = 68f;
            selectionPreview.style.height = 68f;
            selectionPreview.style.marginRight = 9f;
            selectionPreview.style.backgroundColor = new Color(.1f, .11f, .13f);
            selection.Add(selectionPreview);
            VisualElement text = new VisualElement
            {
                style = { flexGrow = 1f },
            };
            selectionTitle = new Label("尚未选择素材");
            selectionTitle.style.fontSize = 14f;
            selectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.Add(selectionTitle);
            selectionDetails = new Label();
            selectionDetails.style.whiteSpace = WhiteSpace.Normal;
            selectionDetails.style.opacity = .72f;
            text.Add(selectionDetails);
            selection.Add(text);
            root.Add(selection);

            paintButton = new Button(StartPainting)
            {
                text = "开始在 Scene 中绘制",
            };
            paintButton.style.height = 32f;
            paintButton.style.marginTop = 7f;
            paintButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            paintButton.style.backgroundColor = new Color(.12f, .42f, .72f);
            root.Add(paintButton);

            RebuildAll();
        }

        private void RebuildAll()
        {
            if (categoryField == null)
            {
                return;
            }

            List<string> categories = new List<string>
            {
                "全部",
                "自动地形",
            };
            if (map?.palette != null)
            {
                foreach (GridArtBrush brush in map.palette.brushes)
                {
                    if (brush != null &&
                        !string.IsNullOrWhiteSpace(brush.category) &&
                        !categories.Contains(brush.category))
                    {
                        categories.Add(brush.category);
                    }
                }
            }
            if (!categories.Contains(category))
            {
                category = "全部";
            }
            categoryField.choices = categories;
            categoryField.SetValueWithoutNotify(category);
            selectedIndex = map?.palette != null &&
                            selectedIndex >= 0 &&
                            selectedIndex < map.palette.brushes.Count
                ? selectedIndex
                : SceneArtPaletteSelection.Current(map);
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
            if (map?.palette == null)
            {
                resultCount.text = "没有可用素材";
                library.Add(new HelpBox(
                    "当前 Scene 没有绑定 GridArt 美术图或素材库。",
                    HelpBoxMessageType.Info));
                RefreshSelection();
                return;
            }

            string query = search?.value?.Trim() ?? string.Empty;
            List<int> filtered = Enumerable.Range(
                    0,
                    map.palette.brushes.Count)
                .Where(index => Matches(
                    map.palette.brushes[index],
                    query))
                .ToList();
            resultCount.text =
                $"找到 {filtered.Count} 项 · 单击查看，双击直接绘制";
            if (filtered.Count == 0)
            {
                VisualElement empty = new VisualElement();
                empty.style.paddingTop = 8f;
                empty.Add(new HelpBox(
                    "没有符合当前搜索和分类的素材。",
                    HelpBoxMessageType.Info));
                Button reset = new Button(() =>
                {
                    category = "全部";
                    categoryField.SetValueWithoutNotify(category);
                    search.value = string.Empty;
                    RebuildCards();
                }) { text = "显示全部素材" };
                reset.style.height = 28f;
                empty.Add(reset);
                library.Add(empty);
                return;
            }

            VisualElement grid = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    paddingTop = 6f,
                    paddingBottom = 4f,
                },
            };
            foreach (int index in filtered)
            {
                grid.Add(CreateCard(index));
            }
            library.Add(grid);
            RefreshCardBorders();
        }

        private bool Matches(GridArtBrush brush, string query)
        {
            if (brush == null)
            {
                return false;
            }

            bool categoryMatch = category == "全部" ||
                                 category == "自动地形" && brush.autoTerrain ||
                                 category != "自动地形" &&
                                 brush.category == category;
            if (!categoryMatch)
            {
                return false;
            }

            return query.Length == 0 ||
                   ((brush.label ?? string.Empty) + " " +
                    (brush.category ?? string.Empty)).IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement CreateCard(int index)
        {
            GridArtBrush brush = map.palette.brushes[index];
            Button card = new Button(() => Select(index))
            {
                text = string.Empty,
                tooltip = $"{brush.label} · {brush.DisplaySize.x:0.##} × {brush.DisplaySize.y:0.##} 格",
            };
            card.style.width = 104f;
            card.style.height = 104f;
            card.style.marginRight = 6f;
            card.style.marginBottom = 6f;
            card.style.paddingTop = 5f;
            card.style.paddingLeft = 4f;
            card.style.paddingRight = 4f;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
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
                Select(index);
                StartPainting();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);

            Image image = new Image
            {
                sprite = Preview(brush),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
            };
            image.style.width = 70f;
            image.style.height = 64f;
            image.style.backgroundColor = new Color(.1f, .11f, .13f);
            card.Add(image);
            Label label = new Label(brush.label)
            {
                pickingMode = PickingMode.Ignore,
            };
            label.style.width = Length.Percent(100f);
            label.style.maxHeight = 29f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.overflow = Overflow.Hidden;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = 11f;
            card.Add(label);
            cards[index] = card;
            return card;
        }

        private void Select(int index)
        {
            selectedIndex = index;
            SceneArtPaletteSelection.Select(map, index);
            RefreshSelection();
            RefreshCardBorders();
        }

        private void RefreshSelection()
        {
            GridArtBrush brush = map?.palette?.Get(selectedIndex);
            bool valid = brush != null;
            paintButton?.SetEnabled(valid);
            if (!valid)
            {
                if (selectionTitle != null)
                {
                    selectionTitle.text = "尚未选择素材";
                    selectionDetails.text = string.Empty;
                    selectionPreview.sprite = null;
                }
                return;
            }

            selectionPreview.sprite = Preview(brush);
            selectionTitle.text = brush.label;
            selectionDetails.text =
                $"{(brush.autoTerrain ? "自动地形" : brush.category)} · " +
                $"{brush.DisplaySize.x:0.##} × {brush.DisplaySize.y:0.##} 格" +
                (string.IsNullOrEmpty(brush.colorId)
                    ? string.Empty
                    : $" · 颜色 {brush.colorId}");
        }

        private void RefreshCardBorders()
        {
            foreach (KeyValuePair<int, Button> pair in cards)
            {
                Color border = pair.Key == selectedIndex
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
            if (map?.palette?.Get(selectedIndex) == null)
            {
                return;
            }

            SceneArtPaletteSelection.Select(map, selectedIndex);
            SceneLevelEditorOverlay.BeginArtPainting(map, selectedIndex);
            Close();
        }

        private static Sprite Preview(GridArtBrush brush) =>
            brush?.autoTerrain == true
                ? brush.GetPiece(4) ?? brush.sprite
                : brush?.sprite;
    }
}
