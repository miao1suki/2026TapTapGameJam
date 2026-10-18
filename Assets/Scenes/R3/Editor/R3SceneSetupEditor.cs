using Project.Levels.ILorR;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(R3SceneSetup))]
public sealed class R3SceneSetupEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new Label("R3 开局能力") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        var rules = new Foldout { text = "独立测试关卡规则", value = true };
        rules.Add(new Label("场景三色全部启用；玩家仅蓝色能力。"));
        rules.Add(new Label("普通跳跃保持标准玩家默认 4 格。"));
        rules.Add(new Label("仅供 R3 独立试玩；整合片段时不复制此组件。"));
        root.Add(rules); return root;
    }
}
