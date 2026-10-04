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

        internal static void OpenForAdd()
        {
            LevelEditorPropEntryWindow window =
                GetWindow<LevelEditorPropEntryWindow>();
            window.editingExisting = false;
            window.editingIndex = -1;
            window.titleContent = new GUIContent("新增道具");
            window.minSize = new Vector2(340f, 180f);
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
            window.minSize = new Vector2(340f, 180f);
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
            root.Add(nameField);
            root.Add(prefabField);

            Label hint = new Label(
                "道具栏目只能放入预制体，放置到网格后占用一个格子。");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.opacity = .72f;
            hint.style.marginTop = 5f;
            root.Add(hint);

            statusLabel = new Label();
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.marginTop = 6f;
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
                statusLabel.text = "道具栏目已经不存在。";
                return;
            }

            nameField.SetValueWithoutNotify(
                entry != null ? entry.DisplayName : "新道具");
            prefabField.SetValueWithoutNotify(
                entry != null ? entry.Prefab : null);
            statusLabel.text = editingExisting
                ? "只允许修改名称和道具预制体。"
                : "请选择道具预制体。";
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
