using System;
using System.Collections.Generic;
using System.Linq;
using Project.ColorBlocks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Interactions.Editor
{
    public sealed class InteractionManagerWindow : EditorWindow
    {
        private InteractionObjectCatalog catalog;
        private string selectedId;
        private VisualElement listPanel;
        private VisualElement detailPanel;
        private InteractionGraphView graph;
        private Label status;

        [MenuItem("Tools/2026TapTap/交互/物体交互管理器")]
        [MenuItem("Tools/2026TapTap/物体交互管理器")]
        [MenuItem("Tools/2026TapTap/关卡规划原型/物体交互管理器")]
        [MenuItem("Window/2026TapTap/物体交互管理器")]
        public static void Open()
        {
            InteractionManagerWindow window =
                GetWindow<InteractionManagerWindow>("物体交互管理器");
            window.minSize = new Vector2(1120f, 650f);
            window.catalog = InteractionEditorService.EnsureCatalog();
            InteractionEditorService.SynchronizeWithLevelEditor();
            window.catalog = InteractionEditorService.EnsureCatalog();
            if (string.IsNullOrWhiteSpace(window.selectedId))
                window.selectedId = window.catalog.Objects.FirstOrDefault()?.ObjectId;
            window.Build();
            window.Show();
        }

        public static void OpenFor(InteractionObjectDefinition definition)
        {
            Open();
            InteractionManagerWindow window =
                GetWindow<InteractionManagerWindow>();
            window.selectedId = definition != null ? definition.ObjectId : null;
            window.RefreshList();
            window.RefreshDetails();
            window.Focus();
        }

        public static void OpenForObject(GameObject target)
        {
            if (target == null)
            {
                Open();
                return;
            }

            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(target)
                as GameObject;
            if (prefab == null && PrefabUtility.IsPartOfPrefabAsset(target))
                prefab = target;

            InteractionObjectDefinition definition = null;
            InteractionObject source = target.GetComponent<InteractionObject>();
            ColorBlock block = target.GetComponent<ColorBlock>();
            if (source != null) definition = source.Definition;
            if (definition == null && block != null) definition = block.Definition;
            if (definition == null)
            {
                InteractionObjectCatalog objectCatalog =
                    InteractionEditorService.EnsureCatalog();
                definition = InteractionEditorService.CreateOrGetDefinition(
                    objectCatalog,
                    prefab,
                    target.name,
                    block != null ? block.BaseColorTypeId : string.Empty);
                InteractionEditorService.BindDefinitionToSceneObject(
                    target,
                    definition);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }

            OpenFor(definition);
        }

        private void OnEnable()
        {
            if (catalog == null)
                catalog = InteractionEditorService.EnsureCatalog();
            InteractionEditorService.SynchronizeWithLevelEditor();
            catalog = InteractionEditorService.EnsureCatalog();
            Build();
        }

        private void Build()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor =
                new Color(.08f, .1f, .14f);

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingLeft = 14f;
            header.style.paddingRight = 14f;
            header.style.paddingTop = 10f;
            header.style.paddingBottom = 10f;
            header.style.backgroundColor = new Color(.12f, .18f, .25f);
            Label title = new Label("物体交互管理器");
            title.style.fontSize = 19f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1f;
            header.Add(title);
            header.Add(ActionButton("导入选中物体", ImportSelection, true));
            header.Add(ActionButton("同步关卡物体", () =>
            {
                int count = InteractionEditorService.SynchronizeWithLevelEditor();
                catalog = InteractionEditorService.EnsureCatalog();
                RefreshList();
                RefreshDetails();
                if (status != null)
                    status.text = $"已同步 {count} 个关卡物体。";
            }));
            header.Add(ActionButton("刷新目录", () =>
            {
                InteractionEditorService.SynchronizeWithLevelEditor();
                catalog = InteractionEditorService.EnsureCatalog();
                RefreshList();
                RefreshDetails();
            }));
            rootVisualElement.Add(header);

            if (catalog == null)
            {
                rootVisualElement.Add(new Label("尚无物体交互目录，请先导入一个预制体。"));
                return;
            }

            VisualElement body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            rootVisualElement.Add(body);

            listPanel = new ScrollView();
            listPanel.style.width = 230f;
            listPanel.style.flexShrink = 0f;
            listPanel.style.paddingLeft = 10f;
            listPanel.style.paddingRight = 10f;
            listPanel.style.paddingTop = 12f;
            listPanel.style.backgroundColor = new Color(.1f, .14f, .19f);
            body.Add(listPanel);

            detailPanel = new VisualElement();
            detailPanel.style.width = 300f;
            detailPanel.style.flexShrink = 0f;
            detailPanel.style.paddingLeft = 14f;
            detailPanel.style.paddingRight = 14f;
            detailPanel.style.paddingTop = 12f;
            body.Add(detailPanel);

            VisualElement graphColumn = new VisualElement();
            graphColumn.style.flexGrow = 1f;
            graphColumn.style.paddingTop = 10f;
            graphColumn.style.paddingRight = 10f;
            body.Add(graphColumn);
            VisualElement graphHeader = new VisualElement();
            graphHeader.style.flexDirection = FlexDirection.Row;
            graphHeader.style.marginBottom = 8f;
            Label graphTitle = new Label("交互图（连连看入口）");
            graphTitle.style.fontSize = 15f;
            graphTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            graphTitle.style.flexGrow = 1f;
            graphHeader.Add(graphTitle);
            graphHeader.Add(ActionButton("适配视图", () => graph?.FrameAll()));
            graphHeader.Add(ActionButton("删除选中节点", () => graph?.DeleteSelectedNodes()));
            UnityEditor.UIElements.ToolbarMenu addNode =
                new UnityEditor.UIElements.ToolbarMenu { text = "＋ 节点" };
            foreach (InteractionNodeKind kind in InteractionNodeLibrary.Templates)
            {
                InteractionNodeKind captured = kind;
                addNode.menu.AppendAction(
                    InteractionNodeLibrary.Label(kind),
                    _ => graph?.AddInteractionNode(captured));
            }
            graphHeader.Add(addNode);
            graphColumn.Add(graphHeader);

            graph = new InteractionGraphView();
            graph.style.borderTopWidth = 1f;
            graph.style.borderBottomWidth = 1f;
            graph.style.borderLeftWidth = 1f;
            graph.style.borderRightWidth = 1f;
            graphColumn.Add(graph);
            status = new Label("触发节点从左侧开始；条件节点返回 false 时会停止该分支。")
            {
                style =
                {
                    marginTop = 8f,
                    marginBottom = 8f,
                    color = new Color(.67f, .75f, .82f),
                    whiteSpace = WhiteSpace.Normal
                }
            };
            graphColumn.Add(status);
            RefreshList();
            RefreshDetails();
        }

        private void RefreshList()
        {
            if (listPanel == null) return;
            listPanel.Clear();
            listPanel.Add(Section("物体定义"));
            if (catalog == null || catalog.Objects.Count == 0)
            {
                listPanel.Add(new Label("目录为空。\n点击顶部“导入选中物体”。")
                {
                    style = { whiteSpace = WhiteSpace.Normal }
                });
                return;
            }

            foreach (InteractionObjectDefinition definition in catalog.Objects)
            {
                if (definition == null) continue;
                InteractionObjectDefinition captured = definition;
                Button button = ActionButton(
                    "●  " + definition.DisplayName,
                    () => Select(captured.ObjectId));
                button.style.height = 42f;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginBottom = 6f;
                button.style.backgroundColor = selectedId == definition.ObjectId
                    ? new Color(.19f, .31f, .43f)
                    : new Color(.16f, .21f, .28f);
                listPanel.Add(button);
            }
        }

        private void RefreshDetails()
        {
            if (detailPanel == null || graph == null) return;
            detailPanel.Clear();
            InteractionObjectDefinition definition = catalog?.Find(selectedId);
            graph.Load(definition, SaveGraph);
            if (definition == null)
            {
                detailPanel.Add(Section("选择一个物体"));
                detailPanel.Add(new Label("每个物体拥有自己的交互图；颜色只是基础属性。")
                {
                    style = { whiteSpace = WhiteSpace.Normal }
                });
                return;
            }

            detailPanel.Add(Section("物体属性"));
            TextField name = new TextField("名称") { value = definition.DisplayName };
            name.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(definition, "修改物体名称");
                definition.SetDisplayName(evt.newValue);
                EditorUtility.SetDirty(definition);
                RefreshList();
            });
            detailPanel.Add(name);
            UnityEditor.UIElements.ObjectField prefab =
                new UnityEditor.UIElements.ObjectField("物体预制体")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false,
                value = definition.Prefab
            };
            prefab.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(definition, "修改物体预制体");
                definition.SetPrefab(evt.newValue as GameObject);
                EditorUtility.SetDirty(definition);
            });
            detailPanel.Add(prefab);

            List<string> colors = BuildColorIds();
            if (!colors.Contains(definition.BaseColorTypeId))
                colors.Insert(0, definition.BaseColorTypeId ?? string.Empty);
            if (colors.Count == 0) colors.Add(string.Empty);
            int colorIndex = Mathf.Max(0, colors.IndexOf(definition.BaseColorTypeId));
            PopupField<string> colorField = new PopupField<string>(
                "基础颜色属性", colors, colorIndex);
            colorField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(definition, "修改物体基础颜色");
                definition.SetBaseColorTypeId(evt.newValue);
                EditorUtility.SetDirty(definition);
            });
            detailPanel.Add(colorField);
            detailPanel.Add(ActionButton("绑定到预制体", () =>
            {
                InteractionEditorService.BindDefinitionToPrefab(
                    definition.Prefab,
                    definition);
                AssetDatabase.SaveAssets();
                status.text = "交互定义已绑定到预制体。";
            }, true));
            detailPanel.Add(ActionButton("导入到关卡编辑器", () =>
            {
                InteractionEditorService.ImportToLevelEditor(definition);
                status.text = "已加入关卡编辑器的道具栏目。";
            }));
            detailPanel.Add(new Label(
                "本物体、玩家、触碰、停留、时间、延迟、受到颜色、褪色、恢复、Timeline 和方法调用都可以组合在图中。")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginTop = 8f }
            });
        }

        private List<string> BuildColorIds()
        {
            ColorCatalog colors = AssetDatabase.LoadAssetAtPath<ColorCatalog>(
                "Assets/_Project/Resources/ColorBlocks/ColorCatalog.asset");
            return colors == null
                ? new List<string>()
                : colors.Colors.Where(item => item != null)
                    .Select(item => item.id).ToList();
        }

        private void Select(string id)
        {
            selectedId = id;
            RefreshList();
            RefreshDetails();
        }

        private void SaveGraph()
        {
            InteractionObjectDefinition definition = catalog?.Find(selectedId);
            if (definition == null || graph == null) return;
            Undo.RecordObject(definition, "编辑物体交互图");
            graph.CopyTo(definition);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            if (status != null) status.text = "交互图已修改并写入物体定义。";
        }

        private void ImportSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("导入物体", "请先在 Project 或 Hierarchy 中选择物体。", "确定");
                return;
            }

            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(selected)
                as GameObject;
            if (prefab == null && PrefabUtility.IsPartOfPrefabAsset(selected))
                prefab = selected;
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "需要预制体",
                    "关卡编辑器栏目需要预制体；请先把场景物体制作成预制体，或直接选择一个预制体资产。",
                    "确定");
                return;
            }

            ColorBlock block = selected.GetComponent<ColorBlock>() ??
                prefab.GetComponent<ColorBlock>();
            InteractionObjectDefinition definition =
                InteractionEditorService.EnsureDefinitionForPrefab(
                    prefab,
                    prefab.name,
                    block != null ? block.BaseColorTypeId : string.Empty);
            selectedId = definition?.ObjectId;
            RefreshList();
            RefreshDetails();
        }

        private static Label Section(string text) => new Label(text)
        {
            style =
            {
                fontSize = 14f,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginTop = 12f,
                marginBottom = 9f
            }
        };

        private static Button ActionButton(
            string text,
            Action action,
            bool primary = false)
        {
            Button button = new Button(action) { text = text };
            button.style.height = 31f;
            button.style.marginLeft = 5f;
            button.style.marginBottom = 6f;
            button.style.backgroundColor = primary
                ? new Color(.15f, .47f, .72f)
                : new Color(.19f, .26f, .34f);
            button.style.color = Color.white;
            return button;
        }
    }
}
