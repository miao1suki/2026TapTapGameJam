using Project.BlockFeatures;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(PlatformMount)), CanEditMultipleObjects]
public sealed class PlatformMountEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new Label("平台附着物") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        var binding = new Foldout { text = "附着关系", value = true };
        var owner = new PropertyField(serializedObject.FindProperty("platform"), "附着平台");
        var anchor = new PropertyField(serializedObject.FindProperty("localAnchor"), "平台局部锚点");
        // Planning owns these links; Scene-only edits would not survive rebuilding.
        owner.SetEnabled(false); anchor.SetEnabled(false);
        binding.Add(owner); binding.Add(anchor);
        binding.Add(new Label("搬起后脱离；房间重置恢复原附着关系。"));
        root.Add(binding);
        var state = new Foldout { text = "运行状态", value = false };
        var status = new Label(); state.Add(status);
        status.schedule.Execute(() =>
        {
            var item = target as PlatformMount;
            status.text = targets.Length > 1 ? "已选择多个附着物" : item == null ? "未选择附着物" :
                !Application.isPlaying ? "编辑状态（关系由关卡存档维护）" : item.IsMounted ? "正在跟随平台" : "已脱离或平台不可用";
        }).Every(200);
        root.Add(state); return root;
    }
}
