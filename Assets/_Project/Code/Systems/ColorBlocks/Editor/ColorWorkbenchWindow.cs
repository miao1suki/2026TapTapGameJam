using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.ColorBlocks.Editor
{
    public sealed class ColorWorkbenchWindow : EditorWindow
    {
        private ColorCatalog catalog;
        private string selectedId;
        private VisualElement listPanel;
        private VisualElement detailPanel;
        private ColorInteractionGraphView graph;
        private Label status;

        [MenuItem("Tools/2026TapTap/颜色/颜色工作台")]
        public static void Open()
        {
            var window = CreateWindow<ColorWorkbenchWindow>("颜色工作台");
            window.minSize = new Vector2(920, 580);
            window.Show();
        }

        private void OnEnable()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ColorCatalog>(ColorProjectSetup.CatalogPath);
            if (catalog != null && string.IsNullOrEmpty(selectedId) && catalog.Colors.Count > 0)
                selectedId = catalog.Colors[0].id;
            Build();
        }

        private void Build()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = new Color(0.09f, 0.12f, 0.17f);
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 16, paddingRight = 16, paddingTop = 12, paddingBottom = 12 } };
            header.style.backgroundColor = new Color(0.12f, 0.18f, 0.25f);
            var title = new Label("颜色工作台") { style = { fontSize = 19, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } };
            header.Add(title);
            header.Add(ActionButton("初始化 / 修复资源", () =>
            {
                ColorProjectSetup.InitializeFromMenu();
                catalog = ColorProjectSetup.EnsureCatalog();
                selectedId = catalog.Colors.FirstOrDefault()?.id;
                Build();
            }));
            header.Add(ActionButton("褪色调试", ColorFadeDebugWindow.Open));
            rootVisualElement.Add(header);

            if (catalog == null)
            {
                var empty = new VisualElement { style = { flexGrow = 1, alignItems = Align.Center, justifyContent = Justify.Center } };
                empty.Add(new Label("尚无颜色目录") { style = { fontSize = 22, marginBottom = 10 } });
                empty.Add(new Label("初始化后将创建红、绿、蓝三类，以及各自的材质和渲染配置。"));
                empty.Add(ActionButton("创建颜色目录", () =>
                {
                    ColorProjectSetup.InitializeFromMenu();
                    catalog = ColorProjectSetup.EnsureCatalog();
                    selectedId = catalog.Colors[0].id;
                    Build();
                }, true));
                rootVisualElement.Add(empty);
                return;
            }

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            rootVisualElement.Add(body);
            listPanel = new ScrollView { style = { width = 220, flexShrink = 0, paddingLeft = 10, paddingRight = 10, paddingTop = 14 } };
            listPanel.style.backgroundColor = new Color(0.11f, 0.15f, 0.2f);
            body.Add(listPanel);
            var center = new VisualElement { style = { width = 290, flexShrink = 0, paddingLeft = 14, paddingRight = 14, paddingTop = 14 } };
            detailPanel = center;
            body.Add(center);
            var graphColumn = new VisualElement { style = { flexGrow = 1, paddingTop = 10, paddingRight = 10 } };
            body.Add(graphColumn);
            var graphHeader = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            graphHeader.Add(new Label("交互图") { style = { fontSize = 15, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            graphHeader.Add(ActionButton("适配视图", () => graph?.FrameAll()));
            var addNode = new ToolbarMenu { text = "＋ 节点" };
            foreach (var kind in ColorNodeLibrary.Templates)
            {
                var selectedKind = kind;
                addNode.menu.AppendAction(ColorNodeLibrary.Label(kind), _ => graph?.AddInteractionNode(selectedKind));
            }
            graphHeader.Add(addNode);
            graphColumn.Add(graphHeader);
            graph = new ColorInteractionGraphView();
            graph.style.borderTopWidth = 1;
            graph.style.borderLeftWidth = 1;
            graph.style.borderRightWidth = 1;
            graph.style.borderBottomWidth = 1;
            graphColumn.Add(graph);
            status = new Label("连接触发、条件和操作节点；修改后保存项目资源。")
            {
                style = { marginTop = 8, marginBottom = 8, color = new Color(0.67f, 0.75f, 0.82f) }
            };
            graphColumn.Add(status);
            RefreshList();
            RefreshDetails();
        }

        private void RefreshList()
        {
            listPanel.Clear();
            listPanel.Add(Section("颜色类型"));
            foreach (var color in catalog.Colors)
            {
                var captured = color;
                var button = ActionButton("●  " + color.displayName, () => Select(captured.id));
                button.style.height = 42;
                button.style.marginBottom = 6;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.color = color.swatch;
                button.style.backgroundColor = selectedId == color.id
                    ? new Color(0.19f, 0.31f, 0.43f) : new Color(0.16f, 0.21f, 0.28f);
                listPanel.Add(button);
            }
            for (int i = catalog.Colors.Count; i < 6; i++)
            {
                var placeholder = ActionButton("＋  空颜色位", AddColor);
                placeholder.style.height = 36;
                placeholder.style.marginBottom = 6;
                listPanel.Add(placeholder);
            }
            if (catalog.Colors.Count >= 6) listPanel.Add(ActionButton("＋ 添加颜色", AddColor));
        }

        private void RefreshDetails()
        {
            detailPanel.Clear();
            var color = catalog.Find(selectedId);
            graph.Load(color, catalog, SaveGraph);
            if (color == null)
            {
                detailPanel.Add(Section("选择一个颜色"));
                return;
            }
            detailPanel.Add(Section("颜色设置"));
            var nameField = new TextField("名称") { value = color.displayName };
            nameField.RegisterValueChangedCallback(evt => ApplyEdit(color, () => color.displayName = evt.newValue, true));
            detailPanel.Add(nameField);
            var swatch = new ColorField("标识色") { value = color.swatch };
            swatch.RegisterValueChangedCallback(evt => ApplyEdit(color, () => color.swatch = evt.newValue, true));
            detailPanel.Add(swatch);
            var material = new ObjectField("编辑识别材质") { objectType = typeof(Material), value = color.targetMaterial };
            material.RegisterValueChangedCallback(evt => ApplyEdit(color, () => color.targetMaterial = evt.newValue as Material));
            detailPanel.Add(material);
            var layer = new IntegerField("Unity 层") { value = color.unityLayer };
            layer.RegisterValueChangedCallback(evt =>
            {
                int requested = evt.newValue;
                if (requested < 8 || requested > 31 ||
                    catalog.Colors.Any(other => other != color && other.unityLayer == requested))
                {
                    layer.SetValueWithoutNotify(color.unityLayer);
                    status.text = "颜色层须在 8–31 之间，且不能与其他颜色共用。";
                    return;
                }
                ApplyEdit(color, () => color.unityLayer = requested);
            });
            detailPanel.Add(layer);
            var advanced = new Foldout { text = "触发事件与高级配置", value = false };
            var eventField = new TextField("解锁事件 ID") { value = color.unlockEventId };
            eventField.RegisterValueChangedCallback(evt => ApplyEdit(color, () => color.unlockEventId = evt.newValue));
            advanced.Add(eventField);
            advanced.Add(new Label("解锁时发出 EventMgr.OnColorTypeEvent(eventId, colorId)。"));
            detailPanel.Add(advanced);
            detailPanel.Add(Section("场景制作"));
            detailPanel.Add(ActionButton("选中物体 → 可变色方块", () => MakeSelectedBlock(color), true));
            detailPanel.Add(ActionButton("在场景创建颜色钥匙", () => CreatePickup(color)));
            detailPanel.Add(new Label("方块按类型自动分层。颜色钥匙可配置 Timeline 演出。")
            {
                style = { whiteSpace = WhiteSpace.Normal, color = new Color(0.65f, 0.73f, 0.8f), marginTop = 8 }
            });
            var runtimeDebug = new Foldout { text = "运行时预览", value = false };
            var unlockPreview = ActionButton("解锁此颜色", () => ColorWorldManager.Instance.Unlock(color.id));
            var fadePreview = ActionButton("重新褪色", () => HSVColorFadeManager.Instance.SetColorFaded(color.id, true));
            var restorePreview = ActionButton("恢复颜色", () => HSVColorFadeManager.Instance.SetColorFaded(color.id, false));
            foreach (var button in new[] { unlockPreview, fadePreview, restorePreview })
            {
                button.SetEnabled(EditorApplication.isPlaying);
                runtimeDebug.Add(button);
            }
            runtimeDebug.Add(new Label("仅 Play Mode 可用；不修改颜色目录。"));
            detailPanel.Add(runtimeDebug);
            detailPanel.Add(Section("维护"));
            detailPanel.Add(ActionButton("删除此颜色类型", () => DeleteColor(color)));
        }

        private void Select(string id)
        {
            selectedId = id;
            RefreshList();
            RefreshDetails();
        }

        private void AddColor()
        {
            int layer = ColorProjectSetup.FindAvailableLayer("Color_Custom_" + Guid.NewGuid().ToString("N").Substring(0, 5));
            if (layer < 0)
            {
                EditorUtility.DisplayDialog("无法添加颜色", "Unity 层已用完；请先释放一个用户层。", "确定");
                return;
            }
            Undo.RecordObject(catalog, "添加颜色类型");
            var color = new ColorTypeDefinition { unityLayer = layer, swatch = Color.white };
            color.targetMaterial = ColorProjectSetup.CreateMaterial("Color_" + color.id, Color.white);
            catalog.EditableColors.Add(color);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Select(color.id);
        }

        private void DeleteColor(ColorTypeDefinition color)
        {
            if (!EditorUtility.DisplayDialog("删除颜色类型", "引用此类型的方块和钥匙不会被自动改写，确定删除？", "删除", "取消")) return;
            Undo.RecordObject(catalog, "删除颜色类型");
            catalog.EditableColors.Remove(color);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            selectedId = catalog.Colors.FirstOrDefault()?.id;
            RefreshList();
            RefreshDetails();
        }

        private void SaveGraph()
        {
            var color = catalog != null ? catalog.Find(selectedId) : null;
            if (color == null || graph == null) return;
            Undo.RecordObject(catalog, "编辑颜色交互图");
            graph.CopyTo(color);
            EditorUtility.SetDirty(catalog);
            status.text = "交互图已修改；保存 Project 资源后写入磁盘。";
        }

        private void ApplyEdit(ColorTypeDefinition color, Action action, bool refreshList = false)
        {
            Undo.RecordObject(catalog, "编辑颜色类型");
            action();
            EditorUtility.SetDirty(catalog);
            if (refreshList) RefreshList();
        }

        private static void MakeSelectedBlock(ColorTypeDefinition color)
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("选择场景物体", "先在 Hierarchy 选择要转换的方块。", "确定");
                return;
            }
            var renderer = selected.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("缺少 Renderer", "所选物体需要 MeshRenderer 或其他 Renderer。", "确定");
                return;
            }
            var block = selected.GetComponent<ColorBlock>();
            if (block == null) block = Undo.AddComponent<ColorBlock>(selected);
            Undo.RecordObject(block, "设置基础颜色");
            block.EditorConfigure(color.id, renderer);
            Undo.RecordObject(renderer.gameObject, "设置颜色层");
            renderer.gameObject.layer = color.unityLayer;
            if (color.targetMaterial != null)
            {
                Undo.RecordObject(renderer, "设置编辑预览材质");
                renderer.sharedMaterial = color.targetMaterial;
            }
            EditorUtility.SetDirty(block);
            Selection.activeGameObject = selected;
        }

        private static void CreatePickup(ColorTypeDefinition color)
        {
            string path = "Assets/_Project/Content/ColorBlocks/Prefabs/ColorKey_" +
                          color.id + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[ColorBlocks] 缺少颜色钥匙预制体：" + path);
                return;
            }
            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go == null) return;
            Undo.RegisterCreatedObjectUndo(go, "创建颜色钥匙");
            go.name = color.displayName + "色钥匙";
            go.transform.position = Selection.activeTransform != null ? Selection.activeTransform.position + Vector3.up : Vector3.up;
            Selection.activeGameObject = go;
        }

        private static Label Section(string text) => new Label(text)
        {
            style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 12, marginBottom = 9 }
        };

        private static Button ActionButton(string text, Action action, bool primary = false)
        {
            var button = new Button(action) { text = text };
            button.style.height = 31;
            button.style.marginBottom = 6;
            button.style.backgroundColor = primary ? new Color(0.15f, 0.47f, 0.72f) : new Color(0.19f, 0.26f, 0.34f);
            button.style.color = Color.white;
            return button;
        }
    }
}
