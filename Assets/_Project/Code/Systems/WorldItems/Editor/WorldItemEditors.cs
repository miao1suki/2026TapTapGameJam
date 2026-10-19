using Project.Editor;
using Project.GreenHook;
using Project.Items;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.WorldItems.Editor
{
    [CustomEditor(typeof(CollectiblePickup)), CanEditMultipleObjects]
    public sealed class CollectiblePickupEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("收集品"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var settings = ProjectInspectorUtility.CreateFoldout("收集设置");
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "itemId", "收集品编号"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "amount", "数量"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "excludedFromProgress", "仅演出，不计入进度"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "onCollected", "收集完成"));
            root.Add(settings);
            var state = ProjectInspectorUtility.CreateFoldout("运行状态", false);
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow("状态", () => target is CollectiblePickup item ?
                item.IsCollected ? "已收集（重置保留）" : item.CollectionRequested ? "收集中" : "可收集" : "未选择"));
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow("稳定实例编号", () =>
                target is CollectiblePickup item && item.TryGetStableInstanceId(out string id) ? id : "未分配规划身份"));
            root.Add(state);
            root.Add(ProjectInspectorUtility.CreateHelp("接触玩家后播放拾取动画，可被三色方块和移动中的基座推动。未拾取物在重生或离区时复原；收集中动画继续完成，已收集物不会重新出现。本组件预留稳定实例查询与已收集状态恢复，不读写永久存档。"));
            ProjectInspectorUtility.Bind(root, serializedObject); return root;
        }
    }

    [CustomEditor(typeof(NeutralSpike)), CanEditMultipleObjects]
    public sealed class NeutralSpikeEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("无色尖刺"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var settings = ProjectInspectorUtility.CreateFoldout("尖刺设置");
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "size", "尺寸"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "initiallyBloody", "初始带血"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "facing", "朝向")); root.Add(settings);
            var state = ProjectInspectorUtility.CreateFoldout("运行状态", false);
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow("本次击杀染血格数", () => target is NeutralSpike spike ? spike.BloodyCellCount.ToString() : "—"));
            root.Add(state);
            root.Add(ProjectInspectorUtility.CreateHelp("触碰可见尖刺使玩家死亡；保护期不生效。击杀后只有命中的格变血，R、死亡和离区不清除；运行染血不写入存档。旧预制体方向可继续沿用，也可改为上下左右。"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(GreenHookBodyMotion)), CanEditMultipleObjects]
    public sealed class GreenHookBodyMotionEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("钩子物理运动"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var state = ProjectInspectorUtility.CreateFoldout("运行状态");
            state.Add(ProjectInspectorUtility.CreateReadOnlyRow("运动状态", () =>
                target is GreenHookBodyMotion motion && motion.IsHookMoving ? "钩子接管" : "原始物理配置"));
            root.Add(state);
            root.Add(ProjectInspectorUtility.CreateHelp("仅在绿钩子拉取期间接管刚体；结束后恢复重力、约束和运动类型。"));
            return root;
        }
    }
}
