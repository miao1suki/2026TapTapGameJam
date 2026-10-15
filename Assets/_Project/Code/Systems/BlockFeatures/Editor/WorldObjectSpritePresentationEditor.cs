using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(MechanismArtworkPresentation))]
    public sealed class MechanismArtworkPresentationEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("机关精灵表现"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var appearance = ProjectInspectorUtility.CreateFoldout("外观", true);
            appearance.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "artwork", "美术配置"));
            appearance.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "visual", "精灵渲染器"));
            root.Add(appearance);
            root.Add(ProjectInspectorUtility.CreateHelp("动画跟随机关状态与曲柄进度。显示范围沿用物体碰撞范围，不改变玩法参数。"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
    [CustomEditor(typeof(WorldObjectSpritePresentation))]
    public sealed class WorldObjectSpritePresentationEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("物体精灵表现"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var appearance = ProjectInspectorUtility.CreateFoldout("外观", true);
            appearance.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "artwork", "美术配置"));
            appearance.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "visual", "精灵渲染器"));
            appearance.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "depthOffsetCells", "表面偏移（格）"));
            root.Add(appearance);
            root.Add(ProjectInspectorUtility.CreateHelp("外观跟随重生绑定或弹跳状态。合并植物按单元格重复显示，不改变碰撞范围。"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(Project.ObjectArtwork.ObjectArtworkLibrary))]
    public sealed class ObjectArtworkLibraryEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("物体美术配置"));
            var respawn = ProjectInspectorUtility.CreateFoldout("重生点", true);
            foreach (var field in new[] { ("respawnLocked", "未激活"), ("respawnUsed", "已激活·非当前"),
                ("respawnCurrent", "当前使用动画"), ("respawnFrameSeconds", "每帧时长（秒）") })
                respawn.Add(ProjectInspectorUtility.CreateProperty(serializedObject, field.Item1, field.Item2));
            root.Add(respawn);
            var plant = ProjectInspectorUtility.CreateFoldout("弹性植物", true);
            foreach (var field in new[] { ("plantBounce", "弹跳动画"), ("plantFrameSeconds", "每帧时长（秒）"),
                ("plantIdleFrame", "静止帧") })
                plant.Add(ProjectInspectorUtility.CreateProperty(serializedObject, field.Item1, field.Item2));
            root.Add(plant);
            var vine = ProjectInspectorUtility.CreateFoldout("藤蔓序列帧", false);
            foreach (var field in new[] { ("vineGrowth", "生长帧"), ("vineInitialFrame", "初始帧"),
                ("vineGrownFrame", "长成帧"), ("vineIdleFrameSeconds", "末段循环每帧时长（秒）") })
                vine.Add(ProjectInspectorUtility.CreateProperty(serializedObject, field.Item1, field.Item2));
            root.Add(vine);
            var mechanisms = ProjectInspectorUtility.CreateFoldout("机关与道具", false);
            mechanisms.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "mechanisms", "物体动画"));
            mechanisms.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "doorIndicators", "门按钮标记（绿、红、蓝）"));
            root.Add(mechanisms);
            var materials = ProjectInspectorUtility.CreateFoldout("材质", false);
            materials.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "neutralSpriteMaterial", "无色物体材质"));
            materials.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "colorSpriteMaterial", "颜色物体材质"));
            root.Add(materials);
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
