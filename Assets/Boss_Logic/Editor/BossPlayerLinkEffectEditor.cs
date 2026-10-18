using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossPlayerLinkEffect))]
public sealed class BossPlayerLinkEffectEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root =
            ProjectInspectorUtility.CreateRoot(
                serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 玩家连线"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "纯显示效果：叠加项目现有红、蓝、绿三种线条样式，距离越近越粗越明显；不参与伤害、碰撞或颜色交互。"));

        Foldout target = ProjectInspectorUtility.CreateFoldout(
            "连接目标",
            true);
        Add(target, "playerTag", "玩家标签");
        Add(target, "bossOffset", "Boss 连接偏移");
        Add(target, "playerOffset", "玩家连接偏移");
        Add(target, "cameraFacingOffset", "朝向相机偏移");
        root.Add(target);

        Foldout materials = ProjectInspectorUtility.CreateFoldout(
            "三种线条材质",
            true);
        Add(materials, "redMaterial", "红色线条材质");
        Add(materials, "blueMaterial", "蓝色线条材质");
        Add(materials, "greenMaterial", "绿色线条材质");
        Add(materials, "redTint", "红色颜色");
        Add(materials, "blueTint", "蓝色颜色");
        Add(materials, "greenTint", "绿色颜色");
        root.Add(materials);

        Foldout lifecycle = ProjectInspectorUtility.CreateFoldout(
            "出现与消失",
            true);
        Add(lifecycle, "generationDuration", "生成动画时长");
        Add(lifecycle, "dissolveDuration", "消散动画时长");
        root.Add(lifecycle);

        Foldout distance = ProjectInspectorUtility.CreateFoldout(
            "距离表现",
            true);
        Add(distance, "minimumDistanceBlocks", "最近距离（格）");
        Add(distance, "maximumDistanceBlocks", "最远距离（格）");
        Add(distance, "farOpacity", "最远透明度");
        Add(distance, "nearOpacity", "最近透明度");
        Add(distance, "farLineThickness", "最远线粗");
        Add(distance, "nearLineThickness", "最近线粗");
        Add(distance, "lerpSharpness", "距离变化曲线");
        root.Add(distance);

        Foldout shape = ProjectInspectorUtility.CreateFoldout(
            "线条形状",
            true);
        Add(shape, "curveSegments", "曲线分段");
        Add(shape, "arcHeight", "弧高");
        Add(shape, "waveHeight", "波动高度");
        Add(shape, "styleSeparationRatio", "三种样式中心距（相对线粗）");
        Add(shape, "sortingLayerName", "排序图层");
        Add(shape, "sortingOrder", "排序值");
        root.Add(shape);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }

    private void Add(
        VisualElement parent,
        string propertyName,
        string label)
    {
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            propertyName,
            label));
    }
}
