using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.WorldVisibility.Editor
{
    internal static class VisibilityInspectorUI
    {
        public static VisualElement Root(string title, string help)
        {
            var root = new VisualElement(); root.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold,fontSize = 16,marginBottom = 8 } });
            root.Add(new HelpBox(help,HelpBoxMessageType.Info)); return root;
        }
        public static Foldout Group(VisualElement root,string name,bool open = true)
        { var group = new Foldout { text = name,value = open }; root.Add(group); return group; }
        public static void Field(VisualElement root,SerializedObject data,string path,string label)
        { root.Add(new PropertyField(data.FindProperty(path),label)); }
    }
    [CustomEditor(typeof(WorldVisibility2D)),CanEditMultipleObjects]
    public sealed class WorldVisibilityInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = VisibilityInspectorUI.Root("二维遮挡光照","独立可见性合成，不改变房间、碰撞、Unlit或颜色解锁。默认仅此组件所在场景生效。须使用已接入光照功能的URP渲染器。");
            var reference = VisibilityInspectorUI.Group(root,"相机与观察者");
            Field(reference,"targetCamera","目标相机"); Field(reference,"findPlayer","自动寻找玩家"); Field(reference,"viewer","观察者");
            Field(reference,"previewInEditor","编辑态预览");
            var lighting = VisibilityInspectorUI.Group(root,"光照表现");
            Field(lighting,"connectedSpaceVisibility","连通空间可见（推荐）");
            Field(lighting,"cellWorldSize","单格世界尺寸"); Field(lighting,"sightRadiusCells","视线范围（格）");
            lighting.Add(new HelpBox("连通模式：半径内通路可绕转角，封闭墙体和关闭的门阻断；保持原画亮度，仅范围外沿渐隐。半径限制为2～48格，半格采样；不计算任何附加灯光或阴影。",HelpBoxMessageType.Info));
            var legacy = VisibilityInspectorUI.Group(lighting,"直线视线与灯光参数",false);
            Field(legacy,"playerLightRadiusCells","玩家光范围（格）"); Field(legacy,"ambient","可见区域环境亮度");
            Field(legacy,"lightCore","明亮核心比例"); Field(legacy,"respectPlayerSight","墙后不可见（含远处灯光）");
            Field(legacy,"shadowSoftnessCells","遮挡柔和范围（格）");
            Field(legacy,"maxExtraLights","最多附加灯光");
            void UpdateMode() { legacy.style.display = serializedObject.FindProperty("connectedSpaceVisibility").boolValue ? DisplayStyle.None : DisplayStyle.Flex; }
            root.TrackPropertyValue(serializedObject.FindProperty("connectedSpaceVisibility"), _ => UpdateMode()); UpdateMode();
            var advanced = VisibilityInspectorUI.Group(root,"性能与诊断",false);
            Field(advanced,"maskResolution","遮罩分辨率比例");
            var status = new Label(); advanced.Add(status);
            status.schedule.Execute(() => { if (target is WorldVisibility2D value) status.text = $"已缓存静态边：{value.CachedSegments}；几何版本：{value.GeometryRevision}；连通采样：{value.ConnectedCellCount}；重建次数：{value.ConnectedBuildCount}"; }).Every(500);
            var rebuild = new Button(WorldVisibility2D.InvalidateAll) { text = "刷新墙体遮挡缓存" }; advanced.Add(rebuild);
            return root;
        }
        void Field(VisualElement root,string path,string label) => VisibilityInspectorUI.Field(root,serializedObject,path,label);
    }
    [CustomEditor(typeof(VisibilityLight2D)),CanEditMultipleObjects]
    public sealed class VisibilityLightInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = VisibilityInspectorUI.Root("二维场景光源","需要当前相机启用二维遮挡光照。位置取世界XY，Z不改变可见性；不会替代颜色解锁。");
            var group = VisibilityInspectorUI.Group(root,"光源参数");
            VisibilityInspectorUI.Field(group,serializedObject,"radiusCells","照射半径（格）");
            VisibilityInspectorUI.Field(group,serializedObject,"tint","光色");
            VisibilityInspectorUI.Field(group,serializedObject,"intensity","强度"); return root;
        }
    }
    [CustomEditor(typeof(VisibilityOccluder2D)),CanEditMultipleObjects]
    public sealed class VisibilityOccluderInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = VisibilityInspectorUI.Root("附加二维遮光体","规划墙体与关闭的门自动遮挡。其他物体需明确添加此组件，水、角色、搬运物默认不遮光。停用或Trigger形状不遮光。");
            var group = VisibilityInspectorUI.Group(root,"遮挡形状");
            VisibilityInspectorUI.Field(group,serializedObject,"shapes","实体碰撞箱"); return root;
        }
    }
    [CustomEditor(typeof(WorldVisibilityRendererFeature))]
    public sealed class VisibilityRendererInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = VisibilityInspectorUI.Root("二维光照渲染功能","水与HSV完成后合成，屏幕覆盖式菜单和HUD不受影响。没有相机光照组件时不产生渲染通道。");
            VisibilityInspectorUI.Field(root,serializedObject,"visibilityShader","光照着色器"); return root;
        }
    }
}
