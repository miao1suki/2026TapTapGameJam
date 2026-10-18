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
                "重生点"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "进入虚线范围后设为当前重生点。再次进入已激活的点也会更新绑定；死亡或重生操作返回最近触碰的点。"));

            RespawnAnchorFeature feature =
                (RespawnAnchorFeature)target;
            Foldout levelType =
                ProjectInspectorUtility.CreateFoldout(
                    "关卡类型",
                    true);
            PropertyField bossToggle =
                ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "isBossArenaRespawnPoint",
                "是否为 Boss 关卡重生点",
                "开启后，玩家从该点完成复活时同步设置 Boss 血量与位置。");
            bossToggle.RegisterCallback<SerializedPropertyChangeEvent>(
                _ =>
                {
                    serializedObject.ApplyModifiedProperties();
                    feature.EnsureBossRespawnMarker(true);
                    feature.ApplyBossRespawnMarkerState();
                    EditorUtility.SetDirty(feature);
                });
            levelType.Add(bossToggle);
            root.Add(levelType);

            Foldout bossRespawn =
                ProjectInspectorUtility.CreateFoldout(
                    "Boss 关卡重生",
                    true);
            bossRespawn.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "bossRespawnHealth",
                "复活后 Boss 血量",
                "玩家从该点复活后，将 Boss 当前血量设置为该值。"));
            PropertyField bossX =
                ProjectInspectorUtility.CreateProperty(
                    serializedObject,
                    "bossRespawnLocalX",
                    "Boss 横向位置（格）",
                    "相对重生点的 X 偏移；Y 始终与重生点相同。");
            bossX.RegisterCallback<SerializedPropertyChangeEvent>(
                _ =>
                {
                    serializedObject.ApplyModifiedProperties();
                    feature.ApplyBossRespawnMarkerState();
                    EditorUtility.SetDirty(feature);
                });
            bossRespawn.Add(bossX);
            var locateMarker = new Button(() =>
            {
                BossRespawnMarker marker =
                    feature.EnsureBossRespawnMarker(true);
                if (marker == null)
                {
                    return;
                }

                Selection.activeGameObject = marker.gameObject;
                EditorGUIUtility.PingObject(marker.gameObject);
                SceneView.lastActiveSceneView?.FrameSelected();
            })
            {
                text = "定位附属标记"
            };
            locateMarker.style.marginTop = 4f;
            bossRespawn.Add(locateMarker);
            bossRespawn.Add(ProjectInspectorUtility.CreateHelp(
                "附属标记没有 Collider 和 Renderer，只能沿世界 X 拖动。删除后会自动补建并重新链接。"));
            root.Add(bossRespawn);

            SerializedProperty bossEnabled =
                serializedObject.FindProperty(
                    "isBossArenaRespawnPoint");
            void RefreshBossVisibility()
            {
                bool visible = bossEnabled != null &&
                               bossEnabled.boolValue;
                bossRespawn.style.display = visible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                locateMarker.SetEnabled(visible);
            }

            RefreshBossVisibility();
            levelType.schedule.Execute(RefreshBossVisibility)
                .Every(100);

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
            root.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "regionId", "所属房间 / 通道", "关卡编辑器应用到场景时自动绑定区域 ID。"));

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
                "非当前材质", "尚未绑定或已切换到其他重生点时使用。"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "activatedMaterial",
                "当前重生点材质", "仅当前绑定点使用；离开范围不取消绑定。"));
            visual.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "activationAnimation",
                "绑定动画"));
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
                    false);
            var openChainButton = new Button(() =>
                RespawnAnchorChainWindow.Open(
                    (RespawnAnchorFeature)target))
            {
                text = "配置重生点链表"
            };
            openChainButton.style.marginTop = 4f;
            openChainButton.style.marginBottom = 4f;
            chain.Add(openChainButton);
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
                "当前绑定",
                () => feature.IsCurrent ? "是" : "否"));
            debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "当前可用",
                () => feature.IsRespawnPointAvailable
                    ? "是"
                    : "否"));
            debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "Boss 同步状态",
                () => feature.BossRespawnDebugStatus));
            debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "待应用 Boss 状态",
                () => feature.HasPendingBossRespawnState
                    ? "是"
                    : "否"));
            debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "最近应用 Boss 血量",
                () => feature.LastAppliedBossRespawnHealth >= 0
                    ? feature.LastAppliedBossRespawnHealth
                        .ToString()
                    : "未应用"));
            debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "最近应用 Boss 位置",
                () => feature.LastAppliedBossRespawnHealth >= 0
                    ? feature.LastAppliedBossRespawnPosition
                        .ToString("F2")
                    : "未应用"));
            var activateButton = new Button(
                () => feature.ActivateForDebug())
            {
                text = "模拟激活"
            };
            activateButton.style.marginTop = 4f;
            debug.Add(activateButton);
            var bossDebugButton = new Button(
                () => feature
                    .RequestBossRespawnStateForDebug())
            {
                text = "模拟应用 Boss 血量与位置"
            };
            bossDebugButton.style.marginTop = 4f;
            debug.Add(bossDebugButton);
            debug.schedule.Execute(() =>
            {
                activateButton.SetEnabled(
                    Application.isPlaying &&
                    !feature.IsCurrent);
                bossDebugButton.SetEnabled(
                    Application.isPlaying &&
                    feature.IsBossArenaRespawnPoint);
            }).Every(100);
            root.Add(debug);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
