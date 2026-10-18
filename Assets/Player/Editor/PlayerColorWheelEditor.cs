using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    [CustomEditor(typeof(PlayerColorWheel))]
    public sealed class PlayerColorWheelEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(PlayerInspectorFields.CreateScriptField(serializedObject));
            var aim = PlayerInspectorFields.CreateFoldout("红蓝能力瞄准");
            aim.Add(PlayerInspectorFields.Create(serializedObject, "aimToleranceCells", "鼠标容差半径（格）",
                "仅让原染色圈内的目标更容易选中，不增加染色总距离；圈外弧段灰显。绿色使用独立钩索设置。"));
            root.Add(aim);
            var range = PlayerInspectorFields.CreateFoldout("染色范围提示", false);
            range.Add(PlayerInspectorFields.Create(serializedObject, "rangeLineWidth", "虚线宽度"));
            range.Add(PlayerInspectorFields.Create(serializedObject, "rangeDashCount", "虚线段数"));
            range.Add(PlayerInspectorFields.Create(serializedObject, "rangeColor", "提示颜色"));
            range.Add(PlayerInspectorFields.Create(serializedObject, "rangePulsePeriod", "呼吸周期（秒）"));
            range.Add(PlayerInspectorFields.Create(serializedObject, "rangeFadeDuration", "显隐时长（秒）"));
            range.Add(PlayerInspectorFields.Create(serializedObject, "rangeMinAlpha", "最低透明度"));
            range.Add(PlayerInspectorFields.Create(serializedObject, "rangeMaxAlpha", "最高透明度"));
            root.Add(range);
            var wheel = PlayerInspectorFields.CreateFoldout("后备轮盘布局", false);
            wheel.Add(PlayerInspectorFields.Create(serializedObject, "wheelSize", "轮盘尺寸"));
            wheel.Add(PlayerInspectorFields.Create(serializedObject, "wheelButtonSize", "按钮尺寸"));
            root.Add(wheel);
            root.Bind(serializedObject);
            return root;
        }
    }
}
