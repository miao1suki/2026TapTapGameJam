using Project.ColorBlocks;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal sealed class LevelEditorEntryWindow : EditorWindow
    {
        private bool editingExisting;
        private int editingIndex = -1;
        private LevelEditorBlockMode workingMode =
            LevelEditorBlockMode.Custom;
        private Label titleLabel;
        private TextField nameField;
        private Button customModeButton;
        private Button prefabModeButton;
        private Label modeHint;
        private ObjectField prefabField;
        private Foldout customSection;
        private ColorField colorField;
        private Button paintButton;
        private Button componentButton;
        private Label templateStatus;
        private Button saveButton;
        private Label statusLabel;

        internal static void OpenForAdd()
        {
            LevelEditorEntryWindow window =
                GetWindow<LevelEditorEntryWindow>();
            window.editingExisting = false;
            window.editingIndex = -1;
            window.workingMode = LevelEditorBlockMode.Custom;
            window.titleContent = new GUIContent("新增方块");
            window.minSize = new Vector2(360f, 430f);
            window.Show();
            window.Focus();
            window.LoadValues();
        }

        internal static void OpenForEdit(int index)
        {
            LevelEditorEntryWindow window =
                GetWindow<LevelEditorEntryWindow>();
            window.editingExisting = true;
            window.editingIndex = index;
            LevelEditorBlockEntry entry = window.GetEditingEntry();
            if (entry != null)
            {
                window.workingMode = entry.Mode;
            }

            window.titleContent = new GUIContent("编辑方块");
            window.minSize = new Vector2(360f, 470f);
            window.Show();
            window.Focus();
            window.LoadValues();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 12f;
            root.style.paddingRight = 12f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            titleLabel = new Label();
            titleLabel.style.fontSize = 16f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(titleLabel);

            nameField = new TextField("名称");
            root.Add(nameField);

            VisualElement modeRow = Row();
            modeRow.style.marginTop = 5f;
            customModeButton = ModeButton(
                "自己编辑",
                () => SelectMode(LevelEditorBlockMode.Custom));
            prefabModeButton = ModeButton(
                "直接使用预制体",
                () => SelectMode(LevelEditorBlockMode.Prefab));
            modeRow.Add(customModeButton);
            modeRow.Add(prefabModeButton);
            root.Add(modeRow);

            modeHint = new Label();
            modeHint.style.whiteSpace = WhiteSpace.Normal;
            modeHint.style.opacity = .78f;
            modeHint.style.marginTop = 3f;
            modeHint.style.marginBottom = 5f;
            root.Add(modeHint);

            prefabField = new ObjectField("基础预制体（可选）")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            root.Add(prefabField);

            customSection = new Foldout
            {
                text = "自己编辑设置",
                value = true
            };
            colorField = new ColorField("颜色");
            customSection.Add(colorField);

            paintButton = new Button(OpenDecoration)
            {
                text = "编辑贴画"
            };
            paintButton.style.height = 30f;
            paintButton.style.marginTop = 5f;
            paintButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            customSection.Add(paintButton);

            componentButton = new Button(OpenComponents)
            {
                text = "组件与调试"
            };
            componentButton.style.height = 30f;
            componentButton.style.marginTop = 4f;
            componentButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            customSection.Add(componentButton);

            templateStatus = new Label();
            templateStatus.style.whiteSpace = WhiteSpace.Normal;
            templateStatus.style.opacity = .78f;
            templateStatus.style.marginTop = 5f;
            customSection.Add(templateStatus);
            root.Add(customSection);

            statusLabel = new Label();
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.opacity = .82f;
            statusLabel.style.marginTop = 6f;
            root.Add(statusLabel);

            VisualElement actions = Row();
            actions.style.marginTop = 10f;
            saveButton = new Button(Save)
            {
                text = "保存"
            };
            saveButton.style.flexGrow = 1f;
            saveButton.style.height = 32f;
            saveButton.style.fontSize = 14f;
            saveButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            saveButton.style.backgroundColor =
                new Color(.16f, .43f, .82f, 1f);
            saveButton.style.color = Color.white;
            actions.Add(saveButton);
            root.Add(actions);

            LoadValues();
        }

        private void LoadValues()
        {
            if (nameField == null)
            {
                return;
            }

            titleLabel.text = editingExisting
                ? "编辑方块栏目"
                : "新增方块栏目";
            if (!editingExisting)
            {
                workingMode = LevelEditorBlockMode.Custom;
                nameField.SetValueWithoutNotify("新方块");
                colorField.SetValueWithoutNotify(
                    new Color(.24f, .56f, 1f));
                prefabField.SetValueWithoutNotify(null);
                saveButton.SetEnabled(true);
                customModeButton.SetEnabled(true);
                prefabModeButton.SetEnabled(true);
                statusLabel.text = "保存栏目后可以编辑贴画和组件。";
                RefreshModeVisuals();
                return;
            }

            LevelEditorBlockEntry entry = GetEditingEntry();
            if (entry == null)
            {
                nameField.SetValueWithoutNotify(string.Empty);
                statusLabel.text = "这个栏目已经不存在。";
                saveButton.SetEnabled(false);
                customModeButton.SetEnabled(false);
                prefabModeButton.SetEnabled(false);
                paintButton.SetEnabled(false);
                componentButton.SetEnabled(false);
                return;
            }

            workingMode = entry.Mode;
            nameField.SetValueWithoutNotify(entry.DisplayName);
            colorField.SetValueWithoutNotify(entry.Color);
            prefabField.SetValueWithoutNotify(entry.Prefab);
            saveButton.SetEnabled(true);
            customModeButton.SetEnabled(true);
            prefabModeButton.SetEnabled(true);
            statusLabel.text =
                "修改后保存；已有场景方块可用“替换为当前”更新。";
            RefreshModeVisuals();
        }

        private void SelectMode(LevelEditorBlockMode mode)
        {
            workingMode = mode;
            RefreshModeVisuals();
        }

        private void RefreshModeVisuals()
        {
            bool custom = workingMode == LevelEditorBlockMode.Custom;
            customSection.style.display = custom
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            prefabField.label = custom
                ? "基础预制体（可选）"
                : "预制体（必填）";
            prefabField.tooltip = custom
                ? "只作为自定义方块的模型和组件基础，仍会应用颜色、贴画和组件模板。"
                : "放置时原样实例化；不会应用颜色、贴画或栏目组件模板，仅附加无行为的格子标记。";
            modeHint.text = custom
                ? "自己编辑：应用栏目颜色、贴画和组件模板。"
                : "直接使用预制体：只实例化预制体本体（仅附加格子标记）。";
            SetModeButtonState(customModeButton, custom);
            SetModeButtonState(prefabModeButton, !custom);

            LevelEditorBlockEntry entry = GetEditingEntry();
            bool canEditExisting = editingExisting && entry != null;
            bool entryUsesCustomMode =
                entry != null && !entry.UsesPrefabDirectly;
            bool canEditCustom = canEditExisting &&
                                 custom &&
                                 entryUsesCustomMode;
            paintButton.SetEnabled(canEditCustom);
            componentButton.SetEnabled(canEditCustom);
            if (!editingExisting)
            {
                componentButton.text = "保存后可编辑组件";
                templateStatus.text = "尚未生成组件模板。";
            }
            else if (entry == null)
            {
                componentButton.text = "组件与调试";
                templateStatus.text = "栏目不存在。";
            }
            else
            {
                if (!entryUsesCustomMode)
                {
                    componentButton.text = "保存后编辑组件";
                    templateStatus.text =
                        "先保存为“自己编辑”模式，才能编辑贴画和组件。";
                }
                else
                {
                    componentButton.text = entry.CustomTemplate != null
                        ? "编辑组件与调试"
                        : "添加组件与调试";
                    templateStatus.text = entry.CustomTemplate != null
                        ? $"组件模板：{entry.CustomTemplate.name}"
                        : "组件模板：未生成，使用基础模型。";
                }
            }
        }

        private static void SetModeButtonState(
            Button button,
            bool selected)
        {
            button.style.backgroundColor = selected
                ? new Color(.16f, .43f, .82f, 1f)
                : new Color(.22f, .24f, .28f, 1f);
            button.style.color = selected
                ? Color.white
                : new Color(.82f, .84f, .88f, 1f);
            button.style.unityFontStyleAndWeight = selected
                ? FontStyle.Bold
                : FontStyle.Normal;
        }

        private LevelEditorBlockEntry GetEditingEntry()
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null || editingIndex < 0 ||
                editingIndex >= palette.Entries.Count)
            {
                return null;
            }

            return palette.Entries[editingIndex];
        }

        private void Save()
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null)
            {
                statusLabel.text = "没有可保存的方块栏目。";
                return;
            }

            string displayName = nameField.value;
            Color color = colorField.value;
            GameObject prefab = prefabField.value as GameObject;
            if (workingMode == LevelEditorBlockMode.Prefab &&
                prefab == null)
            {
                statusLabel.text = "直接使用预制体模式需要指定预制体。";
                return;
            }

            LevelEditorBlockEntry editingEntry = GetEditingEntry();
            if (editingEntry != null &&
                !string.IsNullOrEmpty(editingEntry.ManagedColorTypeId))
            {
                ColorBlock colorBlock = prefab != null
                    ? prefab.GetComponent<ColorBlock>()
                    : null;
                if (workingMode != LevelEditorBlockMode.Prefab ||
                    colorBlock == null ||
                    colorBlock.BaseColorTypeId != editingEntry.ManagedColorTypeId)
                {
                    statusLabel.text = $"此栏目需使用基础色为 {editingEntry.ManagedColorTypeId} 的 ColorBlock 预制体。";
                    return;
                }
            }

            int selectedIndex;
            if (editingExisting)
            {
                if (!LevelEditorPaletteService.UpdateEntry(
                        palette,
                        editingIndex,
                        displayName,
                        color,
                        prefab,
                        workingMode))
                {
                    statusLabel.text = "栏目已经不存在，无法保存。";
                    return;
                }

                selectedIndex = editingIndex;
            }
            else
            {
                selectedIndex = LevelEditorPaletteService.AddEntry(
                    palette,
                    displayName,
                    color,
                    prefab,
                    workingMode);
                if (selectedIndex < 0)
                {
                    statusLabel.text = "新增失败。";
                    return;
                }
            }

            LevelEditorState.SelectedEntryIndex = selectedIndex;
            SceneView.RepaintAll();
            Close();
        }

        private void OpenDecoration()
        {
            LevelEditorBlockEntry entry = GetEditingEntry();
            if (editingExisting &&
                workingMode == LevelEditorBlockMode.Custom &&
                entry != null &&
                !entry.UsesPrefabDirectly)
            {
                LevelEditorDecorationService.Open(editingIndex);
            }
        }

        private void OpenComponents()
        {
            LevelEditorBlockEntry entry = GetEditingEntry();
            if (editingExisting &&
                workingMode == LevelEditorBlockMode.Custom &&
                entry != null &&
                !entry.UsesPrefabDirectly)
            {
                LevelEditorComponentEditorWindow.Open(editingIndex);
            }
        }

        private static Button ModeButton(
            string text,
            System.Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.style.flexGrow = 1f;
            button.style.height = 30f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            return button;
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }
    }
}
