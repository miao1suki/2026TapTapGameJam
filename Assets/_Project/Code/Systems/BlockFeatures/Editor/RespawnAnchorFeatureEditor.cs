using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(RespawnAnchorFeature))]
    public sealed class RespawnAnchorFeatureEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "重生锚"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "玩家进入激活范围后会永久激活该锚点。死亡时使用当前已激活链表中序号最大的锚点，并优先选择安全落地点。"));

            Foldout activation =
                ProjectInspectorUtility.CreateFoldout(
                    "激活设置",
                    true);
            activation.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "activationRadiusBlocks",
                "激活距离（格）",
                "0 表示玩家必须与重生锚重合；1 表示一格距离。"));
            SerializedProperty playerTag =
                serializedObject.FindProperty("playerTag");
            TagField playerTagField = new TagField("玩家标签")
            {
                value = playerTag != null
                    ? playerTag.stringValue
                    : string.Empty,
                tooltip = "使用 Unity Tag 查找玩家对象。"
            };
            playerTagField.RegisterValueChangedCallback(evt =>
            {
                if (playerTag == null)
                {
                    return;
                }

                playerTag.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            activation.Add(playerTagField);
            root.Add(activation);

            Foldout respawn =
                ProjectInspectorUtility.CreateFoldout(
                    "复活设置",
                    true);
            respawn.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "respawnHeightBlocks",
                "复活高度（格）",
                "复活点相对锚点向上偏移的格数。"));
            respawn.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "respawnRadiusBlocks",
                "复活随机半径（格）",
                "0 表示必须使用此重生点；大于 0 时在该半径内寻找安全落地点。"));
            root.Add(respawn);

            Foldout visual =
                ProjectInspectorUtility.CreateFoldout(
                    "表现设置",
                    true);
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "targetRenderer",
                "状态渲染器"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "inactiveMaterial",
                "未激活材质"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "activatedMaterial",
                "已激活材质"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "activationAnimation",
                "激活动画"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "activationAnimationDuration",
                "激活动画时长",
                "0 表示使用动画资源自身长度。"));
            visual.Add(ProjectInspectorUtility.CreateHelp(
                "渲染顺序固定跟随玩家并低一层，不提供可修改字段。"));
            root.Add(visual);

            Foldout chain =
                ProjectInspectorUtility.CreateFoldout(
                    "重生点链表",
                    true);
            var openChainButton = new Button(() =>
                RespawnAnchorChainWindow.Open(
                    (RespawnAnchorFeature)target))
            {
                text = "配置重生点链表"
            };
            openChainButton.style.marginTop = 4f;
            openChainButton.style.marginBottom = 4f;
            chain.Add(openChainButton);
            RespawnAnchorFeature feature =
                (RespawnAnchorFeature)target;
            chain.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "出生点编号",
                () => feature.DisplayLabel));
            chain.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "激活状态",
                () => feature.IsActivated ? "已激活" : "未激活"));
            root.Add(chain);

            Foldout debug =
                ProjectInspectorUtility.CreateFoldout(
                    "运行时调试",
                    false);
            debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "当前可用",
                () => feature.IsRespawnPointAvailable
                    ? "是"
                    : "否"));
            var activateButton = new Button(
                () => feature.ActivateForDebug())
            {
                text = "模拟激活"
            };
            activateButton.style.marginTop = 4f;
            debug.Add(activateButton);
            debug.schedule.Execute(() =>
            {
                activateButton.SetEnabled(
                    Application.isPlaying &&
                    !feature.IsActivated);
            }).Every(100);
            root.Add(debug);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
