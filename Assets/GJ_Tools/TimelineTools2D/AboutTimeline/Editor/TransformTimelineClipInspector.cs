using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TransformTimelineClip))]
public class TransformTimelineClipInspector : Editor
{
    private SerializedProperty moveMode;
    private SerializedProperty direction;
    private SerializedProperty useCollision;
    private SerializedProperty castRadius;
    private SerializedProperty jumpStartTime;
    private SerializedProperty jumpDuration;
    private SerializedProperty jumpHeight;
    private SerializedProperty jumpHeightCurve;
    private SerializedProperty endPos;
    private SerializedProperty endEuler;
    private SerializedProperty moveSpeed;
    private SerializedProperty totalDistance;
    private SerializedProperty useSpeedCurve;
    private SerializedProperty speedCurve;
    private SerializedProperty startSpeed;
    private SerializedProperty endSpeed;
    private SerializedProperty useProgressCurve;
    private SerializedProperty progressCurve;
    private SerializedProperty circleCenterLocal;
    private SerializedProperty circleRadius;
    private SerializedProperty circleTotalAngle;
    private SerializedProperty circleClockwise;
    private SerializedProperty circleVariableSpeed;
    private SerializedProperty circleStartAngSpeed;
    private SerializedProperty circleEndAngSpeed;

    private void OnEnable()
    {
        moveMode = serializedObject.FindProperty("moveMode");
        direction = serializedObject.FindProperty("direction");
        useCollision = serializedObject.FindProperty("useCollision");
        castRadius = serializedObject.FindProperty("castRadius");
        jumpStartTime = serializedObject.FindProperty("jumpStartTime");
        jumpDuration = serializedObject.FindProperty("jumpDuration");
        jumpHeight = serializedObject.FindProperty("jumpHeight");
        jumpHeightCurve = serializedObject.FindProperty("jumpHeightCurve");
        endPos = serializedObject.FindProperty("endPos");
        endEuler = serializedObject.FindProperty("endEuler");
        moveSpeed = serializedObject.FindProperty("moveSpeed");
        totalDistance = serializedObject.FindProperty("totalDistance");
        useSpeedCurve = serializedObject.FindProperty("useSpeedCurve");
        speedCurve = serializedObject.FindProperty("speedCurve");
        startSpeed = serializedObject.FindProperty("startSpeed");
        endSpeed = serializedObject.FindProperty("endSpeed");
        useProgressCurve = serializedObject.FindProperty("useProgressCurve");
        progressCurve = serializedObject.FindProperty("progressCurve");
        circleCenterLocal = serializedObject.FindProperty("circleCenterLocal");
        circleRadius = serializedObject.FindProperty("circleRadius");
        circleTotalAngle = serializedObject.FindProperty("circleTotalAngle");
        circleClockwise = serializedObject.FindProperty("circleClockwise");
        circleVariableSpeed = serializedObject.FindProperty("circleVariableSpeed");
        circleStartAngSpeed = serializedObject.FindProperty("circleStartAngSpeed");
        circleEndAngSpeed = serializedObject.FindProperty("circleEndAngSpeed");
    }

    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("位移模式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(moveMode, new GUIContent("模式", "位移方式，下方参数随模式切换"));
        MoveMode mode = (MoveMode)moveMode.enumValueIndex;
        bool isLine =
            mode == MoveMode.SpeedAndDistance ||
            mode == MoveMode.VariableSpeed ||
            mode == MoveMode.Jump;
        if (isLine)
        {
            EditorGUILayout.LabelField("碰撞处理", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useCollision, new GUIContent("贴墙滑行", "直线推进撞墙时沿墙滑行，不穿透"));
            if (useCollision.boolValue)
            {
                EditorGUILayout.PropertyField(castRadius, new GUIContent("探测半径", "碰撞探测半径，数值越大越早判定碰墙"));
            }
        }

        EditorGUILayout.Space(4);
        switch (mode)
        {
            case MoveMode.FixedEndPos:
                EditorGUILayout.LabelField("终点(瞬移·可穿墙)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("片段开始时立即传送到终点，途中不检测碰撞", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(endPos, new GUIContent("终点坐标(本地)", "目标位置的本地偏移"));
                EditorGUILayout.PropertyField(endEuler, new GUIContent("终点朝向(本地)", "传送完成后的本地欧拉朝向"));
                EditorGUILayout.LabelField("在 Scene 视图中拖动终点手柄可实时调整", EditorStyles.miniLabel);
                break;

            case MoveMode.SpeedAndDistance:
                EditorGUILayout.LabelField("匀速直线", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("按固定速度或速度曲线推进；总距离大于 0 时推进完毕即停", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(direction, new GUIContent("方向(本地,留空=面朝)", "水平方向；留空时沿角色面朝方向"));
                EditorGUILayout.PropertyField(totalDistance, new GUIContent("总距离", "推进总长；0=不限，推进至片段结束"));
                DrawSpeedCurveFields(false);
                EditorGUILayout.LabelField("拖动 Scene 视图中的终点手柄可反推速度与距离", EditorStyles.miniLabel);
                break;

            case MoveMode.VariableSpeed:
                EditorGUILayout.LabelField("变速直线", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("使用旧初末速度或可编辑速度曲线", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(direction, new GUIContent("方向(本地,留空=面朝)", "水平方向；留空时沿角色面朝方向"));
                EditorGUILayout.PropertyField(totalDistance, new GUIContent("总距离", "推进总长；0=不限，推进至片段结束"));
                DrawSpeedCurveFields(true);
                EditorGUILayout.LabelField("拖动 Scene 视图中的终点手柄可反推结束速度", EditorStyles.miniLabel);
                break;

            case MoveMode.Jump:
                EditorGUILayout.LabelField("跳跃", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "水平方向复用直线速度逻辑，垂直方向独立采样跳跃曲线",
                    EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(direction, new GUIContent("方向(本地,留空=面朝)", "水平跳跃方向；留空时沿角色面朝方向"));
                EditorGUILayout.PropertyField(totalDistance, new GUIContent("水平距离", "跳跃期间的水平总距离；0=不限制"));
                DrawSpeedCurveFields(false);
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(jumpStartTime, new GUIContent("起跳时间", "相对片段起点的起跳时刻"));
                EditorGUILayout.PropertyField(jumpDuration, new GUIContent("跳跃时长", "垂直跳跃曲线的持续时间"));
                EditorGUILayout.PropertyField(
                    jumpHeight,
                    new GUIContent("高度基准值", "最终高度=基准值×高度曲线值"));
                EditorGUILayout.PropertyField(
                    jumpHeightCurve,
                    new GUIContent("高度形状曲线", "X=跳跃进度，Y=高度倍率；最终高度=基准值×曲线值"),
                    true);
                EditorGUILayout.LabelField("Scene 视图中可直接拖动跳跃轨迹峰值调整高度", EditorStyles.miniLabel);
                break;

            case MoveMode.CircleRotate:
                EditorGUILayout.LabelField("绕圈", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("以指定圆心与半径做圆弧运动", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(circleCenterLocal, new GUIContent("圆心(本地)", "相对片段开始时位置的本地偏移"));
                EditorGUILayout.PropertyField(circleRadius, new GUIContent("半径", "圆弧半径"));
                EditorGUILayout.PropertyField(circleTotalAngle, new GUIContent("总转角(度)", "360=完整一圈；负值反向"));
                EditorGUILayout.PropertyField(circleClockwise, new GUIContent("俯视顺时针", "旋转方向"));
                EditorGUILayout.PropertyField(circleVariableSpeed, new GUIContent("角速度变速", "以起始/结束角速度做线性变速"));
                if (circleVariableSpeed.boolValue)
                {
                    EditorGUILayout.PropertyField(circleStartAngSpeed, new GUIContent("起始角速度(度/秒)"));
                    EditorGUILayout.PropertyField(circleEndAngSpeed, new GUIContent("结束角速度(度/秒)"));
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        useProgressCurve,
                        new GUIContent("使用进度曲线", "用曲线控制绕圈角度进度"));
                    if (useProgressCurve.boolValue)
                    {
                        EditorGUILayout.PropertyField(
                            progressCurve,
                            new GUIContent("进度曲线", "X=时间进度，Y=绕圈角度进度"),
                            true);
                    }
                }
                EditorGUILayout.LabelField("在 Scene 视图中可拖动圆心与半径手柄", EditorStyles.miniLabel);
                break;
        }

        serializedObject.ApplyModifiedProperties();
        if (serializedObject.hasModifiedProperties)
        {
            EditorApplication.delayCall += () => { SceneView.RepaintAll(); };
        }
    }

    private void DrawSpeedCurveFields(bool allowLegacyConversion)
    {
        EditorGUILayout.PropertyField(
            useSpeedCurve,
            new GUIContent("使用速度曲线", "开启后由曲线 Y 值直接决定每秒移动速度"));
        if (useSpeedCurve.boolValue)
        {
            EditorGUILayout.PropertyField(
                speedCurve,
                new GUIContent("速度曲线", "X=片段进度，Y=实际速度(米/秒)，支持拖拽关键帧和切线"),
                true);
            if (allowLegacyConversion &&
                GUILayout.Button("从旧初末速度生成曲线"))
            {
                TransformTimelineClip editable = (TransformTimelineClip)target;
                Undo.RecordObject(editable, "转换速度曲线");
                editable.speedCurve = AnimationCurve.Linear(
                    0f,
                    editable.startSpeed,
                    1f,
                    editable.endSpeed);
                EditorUtility.SetDirty(editable);
                serializedObject.Update();
            }
            return;
        }

        if (allowLegacyConversion)
        {
            EditorGUILayout.PropertyField(startSpeed, new GUIContent("起始速度", "0=由静止起步"));
            EditorGUILayout.PropertyField(endSpeed, new GUIContent("结束速度", "片段结束时达到的速度"));
            if (GUILayout.Button("从旧初末速度生成曲线"))
            {
                TransformTimelineClip editable = (TransformTimelineClip)target;
                Undo.RecordObject(editable, "转换速度曲线");
                editable.speedCurve = AnimationCurve.Linear(
                    0f,
                    editable.startSpeed,
                    1f,
                    editable.endSpeed);
                editable.useSpeedCurve = true;
                EditorUtility.SetDirty(editable);
                serializedObject.Update();
            }
        }
        else
        {
            EditorGUILayout.PropertyField(moveSpeed, new GUIContent("速度(米/秒)", "不启用曲线时的固定推进速度"));
        }
    }
}
