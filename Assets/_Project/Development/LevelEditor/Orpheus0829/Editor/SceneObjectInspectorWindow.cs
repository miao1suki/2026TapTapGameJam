using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    /// <summary>
    /// Inspector for the real Scene author root. It never creates a planning copy
    /// and never binds to a clicked render/collider child.
    /// </summary>
    internal sealed class SceneObjectInspectorWindow : EditorWindow
    {
        private LevelEditorPlacedBlock targetBlock;
        private Label titleLabel;
        private Label summaryLabel;
        private HelpBox emptyState;
        private VisualElement inspectorContainer;
        private readonly List<InspectorElement> inspectors = new();

        [MenuItem("Tools/2026TapTap/关卡编辑器/所选对象实例参数")]
        private static void OpenSelected()
        {
            LevelEditorPlacedBlock block = ResolveAuthorRoot(
                Selection.activeGameObject);
            if (block == null)
            {
                EditorUtility.DisplayDialog(
                    "没有关卡对象",
                    "请先在正交 SceneView 中点击一个方块或道具。",
                    "知道了");
                return;
            }

            OpenFor(block);
        }

        internal static void OpenFor(LevelEditorPlacedBlock block)
        {
            if (block == null)
            {
                return;
            }

            // Always select the author root. A renderer/collider child must never
            // replace the editable author instance in Hierarchy or this window.
            if (Selection.activeGameObject != block.gameObject)
            {
                Selection.activeGameObject = block.gameObject;
            }

            SceneObjectInspectorWindow window =
                GetWindow<SceneObjectInspectorWindow>();
            window.titleContent = new GUIContent("实例参数");
            window.minSize = new Vector2(390f, 420f);
            window.Show();
            window.SetTarget(block);
            window.Repaint();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 9f;
            rootVisualElement.style.paddingBottom = 9f;

            titleLabel = new Label("实例参数");
            titleLabel.style.fontSize = 17f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(titleLabel);

            summaryLabel = new Label();
            summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            summaryLabel.style.opacity = .72f;
            summaryLabel.style.marginBottom = 7f;
            rootVisualElement.Add(summaryLabel);

            rootVisualElement.Add(new HelpBox(
                "这里直接编辑 Scene 中的真实实例。修改会进入 Unity Undo，并保存为该实例的组件字段或 Prefab override；不会写入 JSON，也不会回改生成模板。",
                HelpBoxMessageType.Info));

            emptyState = new HelpBox(
                "在锁定正交 2D 的对象模式中点击方块或道具，这里会显示它的完整 Inspector。",
                HelpBoxMessageType.Info);
            emptyState.style.marginTop = 8f;
            rootVisualElement.Add(emptyState);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.marginTop = 7f;
            inspectorContainer = new VisualElement();
            scroll.Add(inspectorContainer);
            rootVisualElement.Add(scroll);

            Rebuild();
        }

        private void OnEnable()
        {
            Selection.selectionChanged -= FollowSelection;
            Selection.selectionChanged += FollowSelection;
            Undo.undoRedoPerformed -= Rebuild;
            Undo.undoRedoPerformed += Rebuild;
            EditorApplication.hierarchyChanged -= ValidateTarget;
            EditorApplication.hierarchyChanged += ValidateTarget;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= FollowSelection;
            Undo.undoRedoPerformed -= Rebuild;
            EditorApplication.hierarchyChanged -= ValidateTarget;
            UnbindInspectors();
        }

        private void FollowSelection()
        {
            LevelEditorPlacedBlock block = ResolveAuthorRoot(
                Selection.activeGameObject);
            if (block != targetBlock)
            {
                SetTarget(block);
            }
        }

        private void ValidateTarget()
        {
            if (targetBlock == null)
            {
                targetBlock = null;
            }
            Rebuild();
        }

        private void SetTarget(LevelEditorPlacedBlock block)
        {
            targetBlock = block;
            Rebuild();
        }

        private void Rebuild()
        {
            if (inspectorContainer == null || titleLabel == null)
            {
                return;
            }

            UnbindInspectors();
            inspectorContainer.Clear();
            bool valid = targetBlock != null &&
                         targetBlock.gameObject != null &&
                         targetBlock.gameObject.scene.IsValid();
            emptyState.style.display = valid
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            inspectorContainer.style.display = valid
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!valid)
            {
                titleLabel.text = "实例参数";
                summaryLabel.text = "没有选择可编辑的关卡对象。";
                return;
            }

            SceneObjectIndex index = SceneObjectAuthoring.Read(
                targetBlock.gameObject.scene);
            SceneObjectRecord record = index.For(targetBlock);
            int groupCount = record != null
                ? index.Group(record).Count
                : 1;
            string region = string.IsNullOrEmpty(record?.regionId)
                ? "未归属区域"
                : record.regionId;
            string author = string.IsNullOrEmpty(targetBlock.AuthoringId)
                ? "缺失作者 ID"
                : targetBlock.AuthoringId;
            string channel = record != null
                ? ChannelLabel(record.channel)
                : targetBlock.IsProp ? "道具" : "地形";

            titleLabel.text = targetBlock.gameObject.name;
            summaryLabel.text =
                $"{channel} · 起始格 {targetBlock.Cell} · 占格 {targetBlock.SizeCells}\n" +
                $"{region} · 作者组 {author} · 组内 {groupCount} 个实例";
            foreach (Component component in
                     targetBlock.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                Foldout componentFoldout = new Foldout
                {
                    text = component.GetType().Name,
                    value = true,
                };
                componentFoldout.style.marginBottom = 5f;
                InspectorElement componentInspector =
                    new InspectorElement();
                componentInspector.Bind(
                    new SerializedObject(component));
                inspectors.Add(componentInspector);
                componentFoldout.Add(componentInspector);
                inspectorContainer.Add(componentFoldout);
            }
        }

        private void UnbindInspectors()
        {
            foreach (InspectorElement value in inspectors)
            {
                value?.Unbind();
            }
            inspectors.Clear();
        }

        internal static LevelEditorPlacedBlock ResolveAuthorRoot(
            GameObject clicked)
        {
            if (clicked == null)
            {
                return null;
            }

            LevelEditorPlacedBlock direct =
                clicked.GetComponent<LevelEditorPlacedBlock>();
            return direct != null
                ? direct
                : clicked.GetComponentInParent<LevelEditorPlacedBlock>();
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
