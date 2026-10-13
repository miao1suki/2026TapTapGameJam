using Project.SteamEfect;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.SteamEfect.Editor
{
    [CustomEditor(typeof(SteamBurstEffect))]
    public sealed class SteamBurstEffectEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("井喷蒸汽特效")
            {
                style =
                {
                    unityFontStyleAndWeight = UnityEngine.FontStyle.Bold,
                    marginBottom = 6
                }
            });

            root.Add(new HelpBox(
                "独立的一次性蒸汽喷发模块。效果大小、喷发高度、像素化与雾气浓度可在粒子材质的 Shader 属性中调整；雾气浓度也会改变粒子数量。",
                HelpBoxMessageType.Info));

            var settings = new Foldout { text = "喷发与材质", value = true };
            settings.Add(new PropertyField(serializedObject.FindProperty("particleMaterial"), "粒子材质"));
            settings.Add(new PropertyField(serializedObject.FindProperty("lifetimeSeconds"), "特效生命周期（秒）"));
            settings.Add(new PropertyField(serializedObject.FindProperty("waterCellWorldSize"), "水格世界尺寸"));
            settings.Add(new PropertyField(serializedObject.FindProperty("cloudParticleCount"), "喷柱粒子数"));
            settings.Add(new PropertyField(serializedObject.FindProperty("coreParticleCount"), "亮芯粒子数"));
            settings.Add(new PropertyField(serializedObject.FindProperty("crownParticleCount"), "顶部雾冠粒子数"));
            settings.Bind(serializedObject);
            root.Add(settings);
            return root;
        }
    }
}
