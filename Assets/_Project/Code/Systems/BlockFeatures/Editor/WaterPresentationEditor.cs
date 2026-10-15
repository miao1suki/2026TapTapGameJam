using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(WaterPresentationSettings))]
    public sealed class WaterPresentationSettingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("像素水体配置"));
            var tiles = ProjectInspectorUtility.CreateFoldout("水体面片", true);
            tiles.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "tiles", "图片（水面、水中、水下）"));
            tiles.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "tileMaterials", "渐显材质（水面、水中、水下）"));
            tiles.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "filterMaterial", "透明扰动材质"));
            root.Add(tiles);
            var bubbles = ProjectInspectorUtility.CreateFoldout("流向气泡", true);
            bubbles.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "bubblePrefab", "气泡预制体"));
            bubbles.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "bubbleMaterial", "气泡渐显材质"));
            root.Add(bubbles);
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(GameplayWaterPresentation))]
    public sealed class GameplayWaterPresentationEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("水体运行表现"));
            var state = ProjectInspectorUtility.CreateFoldout("面片统计", true);
            var label = new Label(); state.Add(label); root.Add(state);
            label.schedule.Execute(() =>
            {
                if (target is GameplayWaterPresentation water)
                    label.text = $"水面 {water.SurfaceCellCount} 格 · 水中 {water.BodyCellCount} 格 · 水下 {water.InteriorCellCount} 格";
            }).Every(250);
            return root;
        }
    }
}
