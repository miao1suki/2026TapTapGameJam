using Project.Levels.ILorR;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(R2SceneSetup))]
public sealed class R2SceneSetupEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new Label("R2 开局能力") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        var rules = new Foldout { text = "独立测试关卡规则", value = true };
        rules.Add(new Label("场景红、绿、蓝色：全部启用"));
        rules.Add(new Label("玩家能力：仅蓝色；红色、绿色锁定"));
        rules.Add(new Label("普通跳跃：标准玩家默认 4 格"));
        rules.Add(new Label("仅供 R2 独立测试；整合片段时不复制此组件。"));
        root.Add(rules);
        return root;
    }
}
