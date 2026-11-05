using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(CameraTimelineClip))]
public class CameraTimelineClipInspector : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new HelpBox("平面镜头仅沿 XY 移动、绕画面 Z 轴转动。所有镜头通过相机控制管理器提交，正交投影和显示深度固定。", HelpBoxMessageType.Info));
        var anchor = Section(root, "锚点与初始追踪");
        Field(anchor, "anchorTag", "锚点标签");
        Field(anchor, "trackAnchorFirst", "先追踪玩家");
        var tracking = new VisualElement(); anchor.Add(tracking);
        Field(tracking, "trackDuration", "追踪时长");
        XY(tracking, "trackTargetLocalPos", "追踪目标偏移");
        VisibleWhen(root, tracking, "trackAnchorFirst");
        Field(anchor, "overrideOrthographicSize", "覆盖正交视野");
        var lens = Field(anchor, "orthographicSizeMultiplier", "视野倍率");
        VisibleWhen(root, lens, "overrideOrthographicSize");

        var motion = Section(root, "运镜方式");
        Field(motion, "cameraMoveMode", "运镜模式");
        Field(motion, "resetSubMode", "归位方式");
        Field(motion, "resetLerpFactor", "归位平滑速度");
        XY(motion, "cameraTargetLocalPos", "目标机位偏移");
        Roll(motion);
        Field(motion, "lockLookAtPlayer", "保持目标跟随");
        Field(motion, "smoothLerpFactor", "平滑速度");
        Field(motion, "useLastFrameAsOrigin", "从当前机位继续");
        Field(motion, "useMotionCurve", "使用运动曲线");
        VisibleWhen(root, Field(motion, "motionCurve", "运动曲线"), "useMotionCurve");
        Field(motion, "useVariableSpeed", "使用变速");
        var speed = new VisualElement(); motion.Add(speed);
        Field(speed, "startSpeed", "起始速度"); Field(speed, "endSpeed", "结束速度");
        VisibleWhen(root, speed, "useVariableSpeed");

        var arc = Section(root, "XY 圆弧运镜");
        Field(arc, "useSurroundMode", "启用圆弧");
        var arcFields = new VisualElement(); arc.Add(arcFields);
        Field(arcFields, "surroundRadius", "圆弧半径");
        Field(arcFields, "surroundTotalAngle", "扫过角度");
        Field(arcFields, "surroundFixedHeight", "圆心高度");
        Field(arcFields, "useSurroundRadiusCurve", "使用半径曲线");
        VisibleWhen(root, Field(arcFields, "surroundRadiusCurve", "半径曲线"), "useSurroundRadiusCurve");
        Field(arcFields, "useSurroundHeightCurve", "使用高度曲线");
        VisibleWhen(root, Field(arcFields, "surroundHeightCurve", "高度曲线"), "useSurroundHeightCurve");
        VisibleWhen(root, arcFields, "useSurroundMode");

        var control = Section(root, "镜头转动与边界");
        Field(control, "modeRequest", "控制申请");
        Field(control, "overrideSide2DYaw", "指定画面角度");
        VisibleWhen(root, Field(control, "side2DYawDegrees", "画面角度"), "overrideSide2DYaw");
        Field(control, "projectionTransitionDuration", "转动过渡时间");
        Field(control, "turnTiming", "转动时机");
        Field(control, "turnAngleDegrees", "转动角度");
        Field(control, "turnDuration", "转动时长");
        Field(control, "turnCurve", "转动曲线");
        Field(control, "constrainOrthographicAxes", "启用 XY 边界");
        var axes = new VisualElement(); control.Add(axes);
        Field(axes, "allowPositionX", "允许水平移动");
        Field(axes, "allowPositionY", "允许垂直移动");
        Field(axes, "clampPositionX", "限制水平范围");
        VisibleWhen(root, Field(axes, "positionXRange", "水平范围"), "clampPositionX");
        Field(axes, "clampPositionY", "限制垂直范围");
        VisibleWhen(root, Field(axes, "positionYRange", "垂直范围"), "clampPositionY");
        VisibleWhen(root, axes, "constrainOrthographicAxes");
        return root;
    }
    private static Foldout Section(VisualElement root, string text)
    {
        var group = new Foldout { text = text, value = true };
        root.Add(group); return group;
    }
    private PropertyField Field(VisualElement root, string name, string label)
    {
        var field = new PropertyField(serializedObject.FindProperty(name), label);
        root.Add(field); return field;
    }
    private void VisibleWhen(VisualElement observer, VisualElement field, string name)
    {
        var property = serializedObject.FindProperty(name);
        void Update(SerializedProperty value) => field.style.display = value.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        Update(property); observer.TrackPropertyValue(property, Update);
    }
    private void XY(VisualElement root, string name, string label)
    {
        var property = serializedObject.FindProperty(name);
        var field = new Vector2Field(label) { value = property.vector3Value };
        field.tooltip = "世界 XY 平面；镜头显示深度由控制管理器统一保持。";
        field.RegisterValueChangedCallback(evt => {
            serializedObject.Update(); var value = property.vector3Value;
            value.x = evt.newValue.x; value.y = evt.newValue.y;
            property.vector3Value = value; serializedObject.ApplyModifiedProperties();
        });
        root.TrackPropertyValue(property, value => field.SetValueWithoutNotify(value.vector3Value));
        root.Add(field);
    }
    private void Roll(VisualElement root)
    {
        var property = serializedObject.FindProperty("cameraTargetEuler");
        var field = new FloatField("目标画面角度") { value = property.vector3Value.z };
        field.tooltip = "绕画面 Z 轴，正值逆时针。";
        field.RegisterValueChangedCallback(evt => {
            serializedObject.Update(); property.vector3Value = new Vector3(0, 0, evt.newValue);
            serializedObject.ApplyModifiedProperties();
        });
        root.TrackPropertyValue(property, value => field.SetValueWithoutNotify(value.vector3Value.z));
        root.Add(field);
    }
}
