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
        private Button locateButton;
        private Foldout technicalDetails;
        private Label technicalDetailsLabel;
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

            locateButton = new Button(FrameTarget)
            {
                text = "在 Scene 中定位",
                tooltip = "让 Scene 视图居中显示这个物体",
            };
            locateButton.style.height = 29f;
            locateButton.style.marginBottom = 6f;
            rootVisualElement.Add(locateButton);

            rootVisualElement.Add(new HelpBox(
                "这里改的是当前场景里的这个物体。修改可用 Ctrl+Z 撤销，不会影响以后新放置的模板。",
                HelpBoxMessageType.Info));

            technicalDetails = new Foldout
            {
                text = "详细信息",
                value = false,
            };
            technicalDetails.style.marginTop = 6f;
            technicalDetailsLabel = new Label();
            technicalDetailsLabel.style.whiteSpace = WhiteSpace.Normal;
            technicalDetailsLabel.style.opacity = .72f;
            technicalDetails.Add(technicalDetailsLabel);
            rootVisualElement.Add(technicalDetails);

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
            locateButton?.SetEnabled(valid);
            if (technicalDetails != null)
            {
                technicalDetails.style.display = valid
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
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
            string channel = record != null
                ? ChannelLabel(record.channel)
                : targetBlock.IsProp ? "道具" : "地形";

            titleLabel.text = targetBlock.gameObject.name;
            summaryLabel.text =
                $"{channel} · {region}\n" +
                $"位置格 {targetBlock.Cell} · 大小 {targetBlock.SizeCells.x} × {targetBlock.SizeCells.y} 格";
            string author = string.IsNullOrEmpty(targetBlock.AuthoringId)
                ? "无（这个实例已独立）"
                : targetBlock.AuthoringId;
            technicalDetailsLabel.text =
                $"作者标识：{author}\n同组实例：{groupCount}\n" +
                $"场景对象：{targetBlock.gameObject.name}";
            foreach (Component component in
                     targetBlock.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                Foldout componentFoldout = new Foldout
                {
                    text = ComponentLabel(component),
                    value = !IsEngineComponent(component),
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

        private void FrameTarget()
        {
            if (targetBlock == null || targetBlock.gameObject == null)
            {
                return;
            }

            Selection.activeGameObject = targetBlock.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static bool IsEngineComponent(Component component) =>
            component is Transform ||
            component is Renderer ||
            component is Collider ||
            component is Collider2D ||
            component is Rigidbody ||
            component is Rigidbody2D ||
            component is Animator;

        private static string ComponentLabel(Component component)
        {
            if (component is Transform) return "位置、旋转与缩放";
            if (component is Renderer) return "显示外观（Renderer）";
            if (component is Collider || component is Collider2D)
                return "碰撞范围（Collider）";
            if (component is Rigidbody || component is Rigidbody2D)
                return "物理属性（Rigidbody）";
            if (component is Animator) return "动画（Animator）";
            return ObjectNames.NicifyVariableName(component.GetType().Name);
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
