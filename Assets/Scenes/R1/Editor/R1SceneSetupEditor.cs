using Project.Levels.ILorR;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(R1SceneSetup))]
public sealed class R1SceneSetupEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new Label("R1 开局能力") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        var settings = new Foldout { text = "固定关卡规则", value = true };
        settings.Add(new Label("场景红、绿、蓝色：已解锁"));
        settings.Add(new Label("玩家染色：仅红色；蓝色、绿色锁定"));
        settings.Add(new Label("普通跳跃：使用标准玩家默认 4 格"));
        root.Add(settings);
        return root;
    }
}
