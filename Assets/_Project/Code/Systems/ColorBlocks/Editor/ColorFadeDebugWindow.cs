using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.ColorBlocks.Editor
{
    public sealed class ColorFadeDebugWindow : EditorWindow
    {
        private readonly Dictionary<string, Label> readouts = new Dictionary<string, Label>();
        private FloatField duration;
        private Label message;

        [MenuItem("Tools/2026TapTap/颜色/褪色调试")]
        public static void Open()
        {
            var window = GetWindow<ColorFadeDebugWindow>("褪色调试");
            window.minSize = new Vector2(390, 330);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += RefreshReadouts;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Build();
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshReadouts;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange _) => Build();

        private void Build()
        {
            readouts.Clear();
            var root = rootVisualElement;
            root.Clear();
            root.style.backgroundColor = new Color(0.10f, 0.13f, 0.17f);
            var scroll = new ScrollView { style = { flexGrow = 1, paddingLeft = 16, paddingRight = 16, paddingTop = 14 } };
            root.Add(scroll);
            scroll.Add(new Label("颜色层 · 褪色调试")
            {
                style = { fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
            });
            scroll.Add(new Label("场景物体按 Unity 层参与褪色；不需要挂 ColorBlock。运行时按钮不保存状态。")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 12 }
            });
            duration = new FloatField("过渡时长（秒）") { value = 1.5f };
            scroll.Add(duration);
            var ability = Button("授予调色能力（测试）", () =>
            {
                ColorWorldManager.Instance.GrantRecolorAbility();
                message.text = "已在本次运行中授予调色能力。";
            });
            ability.SetEnabled(EditorApplication.isPlaying);
            scroll.Add(ability);
            message = new Label(EditorApplication.isPlaying ? "运行中，可触发褪色。" : "进入 Play Mode 后可触发褪色；编辑态可分配层。")
            {
                style = { marginTop = 8, marginBottom = 12, whiteSpace = WhiteSpace.Normal }
            };
            scroll.Add(message);
            var catalog = AssetDatabase.LoadAssetAtPath<ColorCatalog>(ColorProjectSetup.CatalogPath);
            if (catalog == null)
            {
                scroll.Add(new Label("尚无颜色目录；先在颜色工作台初始化。"));
                return;
            }
            foreach (var color in catalog.Colors.Where(value => value != null))
            {
                var captured = color;
                var section = new VisualElement
                {
                    style = { backgroundColor = new Color(0.15f, 0.19f, 0.24f),
                        paddingLeft = 10, paddingRight = 10, paddingTop = 8, paddingBottom = 9,
                        marginBottom = 8 }
                };
                scroll.Add(section);
                section.Add(new Label($"{color.displayName}  ·  {color.id}  ·  Layer {color.unityLayer} ({LayerMask.LayerToName(color.unityLayer)})")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold, color = color.swatch }
                });
                var readout = new Label("饱和度：—") { style = { marginTop = 4, marginBottom = 6 } };
                readouts[color.id] = readout;
                section.Add(readout);
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                section.Add(row);
                var fade = Button("褪色", () => Trigger(captured.id, true));
                var restore = Button("恢复", () => Trigger(captured.id, false));
                var unlock = Button("解锁", () =>
                {
                    bool changed = ColorWorldManager.Instance.Unlock(captured.id);
                    message.text = changed ? $"{captured.displayName}已解锁。" : $"{captured.displayName}已经解锁。";
                });
                fade.SetEnabled(EditorApplication.isPlaying);
                restore.SetEnabled(EditorApplication.isPlaying);
                unlock.SetEnabled(EditorApplication.isPlaying);
                row.Add(unlock);
                row.Add(fade);
                row.Add(restore);
                section.Add(Button("将选中场景物体标记到此颜色层", () => AssignSelectedLayer(captured)));
            }
            scroll.Add(new Label("仅不透明 Renderer 参与当前 HSV 遮罩。Unity 层也可能影响碰撞矩阵与相机剔除，请检查项目设置。")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginTop = 8, marginBottom = 14 }
            });
            RefreshReadouts();
        }

        private static Button Button(string text, System.Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 6;
            button.style.marginBottom = 5;
            button.style.minHeight = 26;
            return button;
        }

        private void Trigger(string colorId, bool faded)
        {
            if (!EditorApplication.isPlaying) return;
            HSVColorFadeManager.Instance.SetColorFaded(colorId, faded, Mathf.Max(0f, duration.value));
            message.text = $"{colorId}：已触发{(faded ? "褪色" : "恢复")}。";
        }

        private void RefreshReadouts()
        {
            if (!EditorApplication.isPlaying) return;
            foreach (var pair in readouts)
                pair.Value.text = $"饱和度：{HSVColorFadeManager.Instance.GetSaturation(pair.Key):0.00}  ·  " +
                    (ColorWorldManager.Instance.IsUnlocked(pair.Key) ? "已解锁" : "未解锁");
        }

        private void AssignSelectedLayer(ColorTypeDefinition color)
        {
            if (EditorApplication.isPlaying)
            {
                message.text = "退出 Play Mode 后再修改场景层。";
                return;
            }
            if (color.unityLayer < 8 || color.unityLayer > 31)
            {
                message.text = "颜色层无效；请先在颜色工作台修复。";
                return;
            }
            int changed = 0;
            int skipped = 0;
            var visited = new HashSet<GameObject>();
            foreach (var root in Selection.gameObjects)
            {
                if (root == null || !root.scene.IsValid() || PrefabUtility.IsPartOfPrefabAsset(root)) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var target = renderer.gameObject;
                    if (!visited.Add(target)) continue;
                    if (target.GetComponentInParent<ColorBlock>() != null)
                    {
                        skipped++;
                        continue;
                    }
                    if (target.layer == color.unityLayer) continue;
                    Undo.RecordObject(target, "设置场景物体颜色层");
                    target.layer = color.unityLayer;
                    EditorSceneManager.MarkSceneDirty(target.scene);
                    changed++;
                }
            }
            message.text = changed > 0
                ? $"已标记 {changed} 个 Renderer 物体到 {color.displayName} 层；跳过 {skipped} 个 ColorBlock。"
                : $"没有可修改的场景 Renderer；跳过 {skipped} 个 ColorBlock。";
        }
    }
}
