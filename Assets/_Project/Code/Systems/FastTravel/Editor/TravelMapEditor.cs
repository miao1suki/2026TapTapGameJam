using Project.BlockFeatures;
using Project.FastTravel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.FastTravel.Editor
{
    [CustomEditor(typeof(TravelMapSettings))]
    public sealed class TravelMapSettingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("达到收集比例并击败Boss后开放；Boss房及战斗期间不可用。16有效像素/世界格不适用于屏幕UI。", HelpBoxMessageType.Info));
            var display = new Foldout { text = "地图与解锁", value = true };
            display.Add(new PropertyField(serializedObject.FindProperty("font"), "统一字体"));
            display.Add(new PropertyField(serializedObject.FindProperty("requiredCollectibleRatio"), "收集比例门槛"));
            root.Add(display);
            var timing = new Foldout { text = "传送运镜", value = true };
            timing.Add(new PropertyField(serializedObject.FindProperty("zoomOutSeconds"), "拉远秒数"));
            timing.Add(new PropertyField(serializedObject.FindProperty("travelSeconds"), "平移秒数"));
            timing.Add(new PropertyField(serializedObject.FindProperty("zoomInSeconds"), "拉近秒数"));
            root.Add(timing); return root;
        }
    }
    [CustomEditor(typeof(WorldTravelMap))]
    public sealed class WorldTravelMapEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("运行时自动装到标准玩家，不需要额外玩家预制体。配置位于 Resources/UI/TravelMapSettings。", HelpBoxMessageType.Info));
            var state = new Label(); root.Add(state);
            root.schedule.Execute(() => { var value = target as WorldTravelMap; if (value != null) state.text = $"已解锁：{value.IsUnlocked}  当前可用：{value.CanUseHere}\n地图：{value.IsOpen}  传送：{value.IsTravelling}"; }).Every(500);
            return root;
        }
    }
}
