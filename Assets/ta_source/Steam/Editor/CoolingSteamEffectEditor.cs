using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Steam.Editor
{
    [CustomEditor(typeof(CoolingSteamEffect))]
    public sealed class CoolingSteamEffectEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();

            var root = new VisualElement();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;

            var title = new Label("岩浆冷却蒸汽")
            {
                style =
                {
                    fontSize = 15f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            };
            root.Add(title);

            var script = new PropertyField(
                serializedObject.FindProperty("m_Script"),
                "脚本");
            script.SetEnabled(false);
            root.Add(script);

            root.Add(new HelpBox(
                "蓝色成功作用于岩浆后，在冷却块上方播放一次短雾团；此组件只负责粒子表现。",
                HelpBoxMessageType.Info));

            var main = new Foldout
            {
                text = "表现参数",
                value = true
            };
            AddField(main, "particleMaterial", "蒸汽材质");
            AddField(main, "lifetimeSeconds", "消散时间（秒）");
            AddField(main, "cellWorldSize", "未关联方块时尺寸");
            root.Add(main);

            var counts = new Foldout
            {
                text = "粒子数量",
                value = false
            };
            AddField(counts, "cloudParticleCount", "雾团粒子数");
            AddField(counts, "coreParticleCount", "雾芯粒子数");
            AddField(counts, "crownParticleCount", "雾边粒子数");
            root.Add(counts);

            SerializedProperty material =
                serializedObject.FindProperty("particleMaterial");
            var warning = new HelpBox(
                "请指定蒸汽材质，否则运行时不会生成雾团。",
                HelpBoxMessageType.Warning);
            warning.style.display = material.objectReferenceValue == null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            root.Add(warning);
            root.schedule.Execute(() =>
            {
                warning.style.display = material.objectReferenceValue == null
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }).Every(100);

            root.Bind(serializedObject);
            return root;
        }

        private void AddField(
            VisualElement parent,
            string propertyName,
            string label)
        {
            var field = new PropertyField(
                serializedObject.FindProperty(propertyName),
                label);
            parent.Add(field);
        }
    }
}
