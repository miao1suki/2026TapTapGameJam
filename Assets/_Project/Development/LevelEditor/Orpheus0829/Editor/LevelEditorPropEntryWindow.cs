using System.Collections.Generic;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal sealed class LevelEditorPropEntryWindow : EditorWindow
    {
        private bool editingExisting;
        private int editingIndex = -1;
        private TextField nameField;
        private ObjectField prefabField;
        private Label statusLabel;
        private Button editComponentsButton;
        private GameObject lastPrefab;
        private readonly List<LevelEditorComponentValueOverride>
            draftOverrides =
                new List<LevelEditorComponentValueOverride>();

        internal static void OpenForAdd()
        {
            LevelEditorPropEntryWindow window =
                GetWindow<LevelEditorPropEntryWindow>();
            window.editingExisting = false;
            window.editingIndex = -1;
            window.titleContent = new GUIContent("新增道具");
            window.minSize = new Vector2(380f, 245f);
            window.Show();
            window.Focus();
            window.LoadValues();
        }

        internal static void OpenForEdit(int index)
        {
            LevelEditorPropEntryWindow window =
                GetWindow<LevelEditorPropEntryWindow>();
            window.editingExisting = true;
            window.editingIndex = index;
            window.titleContent = new GUIContent("编辑道具");
            window.minSize = new Vector2(380f, 245f);
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

            Label title = new Label(
                editingExisting ? "编辑道具栏目" : "新增道具栏目");
            title.style.fontSize = 16f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            nameField = new TextField("名称");
            prefabField = new ObjectField("道具预制体")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            prefabField.RegisterValueChangedCallback(evt =>
            {
                GameObject selected = evt.newValue as GameObject;
                if (selected != lastPrefab)
                {
                    draftOverrides.Clear();
                }

                lastPrefab = selected;
                RefreshEditButton();
            });
            root.Add(nameField);
            root.Add(prefabField);

            Label hint = new Label(
                "组件数值用于区分同一个预制体的不同配置；" +
                "只保存到当前道具栏目，不修改预制体资产。");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.opacity = .76f;
            hint.style.marginTop = 5f;
            root.Add(hint);

            editComponentsButton = new Button(OpenComponentEditor)
            {
                text = "编辑组件数值"
            };
            editComponentsButton.style.height = 30f;
            editComponentsButton.style.marginTop = 8f;
            root.Add(editComponentsButton);

            statusLabel = new Label();
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.marginTop = 5f;
            statusLabel.style.opacity = .82f;
            root.Add(statusLabel);

            Button save = new Button(Save)
            {
                text = "保存"
            };
            save.style.height = 32f;
            save.style.marginTop = 8f;
            save.style.backgroundColor = new Color(.16f, .43f, .82f);
            save.style.color = Color.white;
            root.Add(save);

            LoadValues();
        }

        private void LoadValues()
        {
            if (nameField == null)
            {
                return;
            }

            LevelEditorPropEntry entry = GetEditingEntry();
            if (editingExisting && entry == null)
            {
                nameField.SetValueWithoutNotify(string.Empty);
                prefabField.SetValueWithoutNotify(null);
                draftOverrides.Clear();
                statusLabel.text = "道具栏目已经不存在。";
                RefreshEditButton();
                return;
            }

            draftOverrides.Clear();
            if (entry != null)
            {
                for (int index = 0;
                     index < entry.ComponentValueOverrides.Count;
                     index++)
                {
                    LevelEditorComponentValueOverride value =
                        entry.ComponentValueOverrides[index];
                    if (value != null)
                    {
                        draftOverrides.Add(value.Clone());
                    }
                }
            }

            nameField.SetValueWithoutNotify(
                entry != null ? entry.DisplayName : "新道具");
            lastPrefab = entry != null ? entry.Prefab : null;
            prefabField.SetValueWithoutNotify(lastPrefab);
            statusLabel.text = editingExisting
                ? "只允许修改名称、预制体和组件数值覆盖。"
                : "请选择道具预制体。";
            RefreshEditButton();
        }

        private void OpenComponentEditor()
        {
            GameObject prefab = prefabField?.value as GameObject;
            if (prefab == null)
            {
                statusLabel.text = "请先指定道具预制体。";
                return;
            }

            LevelEditorPropComponentEditorWindow.Open(
                prefab,
                draftOverrides,
                values =>
                {
                    draftOverrides.Clear();
                    for (int index = 0; index < values.Count; index++)
                    {
                        draftOverrides.Add(values[index].Clone());
                    }

                    statusLabel.text =
                        $"组件数值已编辑，共 {draftOverrides.Count} 项覆盖，尚未保存。";
                });
        }

        private void RefreshEditButton()
        {
            if (editComponentsButton != null)
            {
                editComponentsButton.SetEnabled(
                    prefabField?.value != null);
            }
        }

        private void Save()
        {
            GameObject prefab = prefabField.value as GameObject;
            if (prefab == null)
            {
                statusLabel.text = "道具栏目必须指定预制体。";
                return;
            }

            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null)
            {
                statusLabel.text = "没有可用的栏目数据。";
                return;
            }

            int selectedIndex;
            if (editingExisting)
            {
                if (!LevelEditorPaletteService.UpdateProp(
                        palette,
                        editingIndex,
                        nameField.value,
                        prefab))
                {
                    statusLabel.text = "保存失败。";
                    return;
                }

                selectedIndex = editingIndex;
            }
            else
            {
                selectedIndex = LevelEditorPaletteService.AddProp(
                    palette,
                    nameField.value,
                    prefab);
                if (selectedIndex < 0)
                {
                    statusLabel.text = "保存失败。";
                    return;
                }
            }

            LevelEditorPropEntry saved =
                palette.PropEntries[selectedIndex];
            saved.ReplaceComponentValueOverrides(draftOverrides);
            LevelEditorBlockFactory.ApplyPropOverridesToPlacedBlocks(
                saved);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
            LevelEditorState.SelectedPropIndex = selectedIndex;
            SceneView.RepaintAll();
            Close();
        }

        private LevelEditorPropEntry GetEditingEntry()
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null ||
                editingIndex < 0 ||
                editingIndex >= palette.PropEntries.Count)
            {
                return null;
            }

            return palette.PropEntries[editingIndex];
        }
    }
}
