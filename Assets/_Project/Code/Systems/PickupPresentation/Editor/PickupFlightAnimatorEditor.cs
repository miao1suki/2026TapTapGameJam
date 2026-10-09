using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.Pickups.Editor
{
    [CustomEditor(typeof(PickupFlightAnimator))]
    public sealed class PickupFlightAnimatorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("拾取飞行动画"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));

            Foldout references = ProjectInspectorUtility.CreateFoldout("对象引用");
            references.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "visual", "飞行外观", "只移动并隐藏此外观，不修改奖励数据。"));
            references.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "pickupCollider", "拾取碰撞体", "开始飞行后关闭，避免重复触发。"));
            root.Add(references);

            Foldout recoil = ProjectInspectorUtility.CreateFoldout("远离玩家的反弹");
            recoil.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "recoilDuration", "反弹时间"));
            recoil.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "recoilDistance", "反弹距离"));
            recoil.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "recoilDistanceVariation", "距离随机比例"));
            recoil.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "recoilHeightVariation", "高度随机范围"));
            root.Add(recoil);

            Foldout rhythm = ProjectInspectorUtility.CreateFoldout("收束节奏");
            rhythm.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "heightAboveHead", "高出头顶"));
            rhythm.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "riseDuration", "快速上升时间"));
            rhythm.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "slowDuration", "短暂减速时间"));
            rhythm.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "finishDuration", "快速收束时间"));
            rhythm.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "slowApproach", "减速段靠近比例"));
            root.Add(rhythm);

            Foldout variation = ProjectInspectorUtility.CreateFoldout("随机路径");
            variation.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "horizontalVariation", "水平弯曲范围"));
            variation.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "verticalVariation", "垂直弯曲范围"));
            variation.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "depthVariation", "深度弯曲范围"));
            root.Add(variation);

            PickupFlightAnimator animator = (PickupFlightAnimator)target;
            root.Add(ProjectInspectorUtility.CreateReadOnlyRow("当前状态",
                () => animator.IsPlaying ? "飞行中" : "待播放"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
