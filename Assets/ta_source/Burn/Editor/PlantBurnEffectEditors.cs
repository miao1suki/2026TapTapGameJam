using Project.BurnEffects;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.BurnEffectsEditor
{
    [CustomEditor(typeof(PlantBurnEffectSettings))]
    public sealed class PlantBurnEffectSettingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("植物燃烧效果配置"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));

            var motion = ProjectInspectorUtility.CreateFoldout("燃烧推进", true);
            motion.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "durationSeconds", "燃烧时长（秒）"));
            motion.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "speedMultiplier", "燃烧速度（倍）"));
            root.Add(motion);

            var flame = ProjectInspectorUtility.CreateFoldout("火焰轮廓", true);
            flame.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "flameEdgeWidth", "火焰边宽度"));
            flame.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "flameEdgeIntensity", "橙色焰边强度"));
            flame.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "edgeNoise", "火焰边缘扰动"));
            flame.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "flameCoreColor", "火焰内芯"));
            flame.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "flameOuterColor", "火焰外焰"));
            root.Add(flame);

            var ash = ProjectInspectorUtility.CreateFoldout("像素与灰烬", true);
            ash.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "pixelation", "像素化程度"));
            ash.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "ashFadeSeconds", "灰烬消散时长（秒）"));
            ash.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject, "ashColor", "灰烬颜色"));
            root.Add(ash);

            root.Add(ProjectInspectorUtility.CreateHelp(
                "障碍与弹性植物从底部向上渐消；藤蔓保持整根顶到底逐段倒放并叠加焰边。显示与HSV遮罩共用16像素/格采样，不复制逐物体材质。"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(PlantBurnEffectPlayer))]
    public sealed class PlantBurnEffectPlayerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("植物燃烧动画"));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "运行时表现控制器由植物功能自动创建。参数在 PlantBurnEffectSettings 配置资产中调整。"));
            root.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "播放状态",
                () => target is PlantBurnEffectPlayer player && player.IsPlaying
                    ? "燃烧中"
                    : "空闲"));
            return root;
        }
    }
}
