using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraTimelineClip))]
public class CameraTimelineClipInspector : Editor
{
    private SerializedProperty cameraMoveMode;
    private SerializedProperty resetSubMode;
    private SerializedProperty resetLerpFactor;
    private SerializedProperty useSurroundMode;
    private SerializedProperty surroundRadius;
    private SerializedProperty surroundTotalAngle;
    private SerializedProperty surroundFixedHeight;
    private SerializedProperty useSurroundRadiusCurve;
    private SerializedProperty surroundRadiusCurve;
    private SerializedProperty useSurroundHeightCurve;
    private SerializedProperty surroundHeightCurve;
    private SerializedProperty cameraTargetLocalPos;
    private SerializedProperty cameraTargetEuler;
    private SerializedProperty lockLookAtPlayer;
    private SerializedProperty modeRequest;
    private SerializedProperty overrideSide2DYaw;
    private SerializedProperty side2DYawDegrees;
    private SerializedProperty projectionTransitionDuration;
    private SerializedProperty useMotionCurve;
    private SerializedProperty motionCurve;
    private SerializedProperty turnTiming;
    private SerializedProperty turnAngleDegrees;
    private SerializedProperty turnDuration;
    private SerializedProperty turnCurve;
    private SerializedProperty constrainOrthographicAxes;
    private SerializedProperty allowPositionX;
    private SerializedProperty allowPositionY;
    private SerializedProperty allowPositionZ;
    private SerializedProperty clampPositionX;
    private SerializedProperty positionXRange;
    private SerializedProperty clampPositionY;
    private SerializedProperty positionYRange;
    private SerializedProperty clampPositionZ;
    private SerializedProperty positionZRange;
    private SerializedProperty smoothLerpFactor;
    private SerializedProperty useVariableSpeed;
    private SerializedProperty startSpeed;
    private SerializedProperty endSpeed;
    private SerializedProperty useLastFrameAsOrigin;

    private void OnEnable()
    {
        cameraMoveMode = serializedObject.FindProperty("cameraMoveMode");
        resetSubMode = serializedObject.FindProperty("resetSubMode");
        resetLerpFactor = serializedObject.FindProperty("resetLerpFactor");
        useSurroundMode = serializedObject.FindProperty("useSurroundMode");
        surroundRadius = serializedObject.FindProperty("surroundRadius");
        surroundTotalAngle = serializedObject.FindProperty("surroundTotalAngle");
        surroundFixedHeight = serializedObject.FindProperty("surroundFixedHeight");
        useSurroundRadiusCurve = serializedObject.FindProperty("useSurroundRadiusCurve");
        surroundRadiusCurve = serializedObject.FindProperty("surroundRadiusCurve");
        useSurroundHeightCurve = serializedObject.FindProperty("useSurroundHeightCurve");
        surroundHeightCurve = serializedObject.FindProperty("surroundHeightCurve");
        cameraTargetLocalPos = serializedObject.FindProperty("cameraTargetLocalPos");
        cameraTargetEuler = serializedObject.FindProperty("cameraTargetEuler");
        lockLookAtPlayer = serializedObject.FindProperty("lockLookAtPlayer");
        modeRequest = serializedObject.FindProperty("modeRequest");
        overrideSide2DYaw =
            serializedObject.FindProperty("overrideSide2DYaw");
        side2DYawDegrees =
            serializedObject.FindProperty("side2DYawDegrees");
        projectionTransitionDuration = serializedObject.FindProperty("projectionTransitionDuration");
        useMotionCurve = serializedObject.FindProperty("useMotionCurve");
        motionCurve = serializedObject.FindProperty("motionCurve");
        turnTiming = serializedObject.FindProperty("turnTiming");
        turnAngleDegrees = serializedObject.FindProperty("turnAngleDegrees");
        turnDuration = serializedObject.FindProperty("turnDuration");
        turnCurve = serializedObject.FindProperty("turnCurve");
        constrainOrthographicAxes = serializedObject.FindProperty("constrainOrthographicAxes");
        allowPositionX = serializedObject.FindProperty("allowPositionX");
        allowPositionY = serializedObject.FindProperty("allowPositionY");
        allowPositionZ = serializedObject.FindProperty("allowPositionZ");
        clampPositionX = serializedObject.FindProperty("clampPositionX");
        positionXRange = serializedObject.FindProperty("positionXRange");
        clampPositionY = serializedObject.FindProperty("clampPositionY");
        positionYRange = serializedObject.FindProperty("positionYRange");
        clampPositionZ = serializedObject.FindProperty("clampPositionZ");
        positionZRange = serializedObject.FindProperty("positionZRange");
        smoothLerpFactor = serializedObject.FindProperty("smoothLerpFactor");
        useVariableSpeed = serializedObject.FindProperty("useVariableSpeed");
        startSpeed = serializedObject.FindProperty("startSpeed");
        endSpeed = serializedObject.FindProperty("endSpeed");
        useLastFrameAsOrigin = serializedObject.FindProperty("useLastFrameAsOrigin");
    }

    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("运镜方式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cameraMoveMode, new GUIContent("模式", "平滑运镜 / 瞬移切镜 / 归位至片段前机位"));
        CamMoveMode mode = (CamMoveMode)cameraMoveMode.enumValueIndex;
        EditorGUILayout.Space(2);

        if (mode == CamMoveMode.ResetOrigin)
        {
            EditorGUILayout.LabelField("归位", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("拉回片段开始前的机位，适合动作演出后的收镜", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(resetSubMode, new GUIContent("归位方式", "瞬移直接回 / 平滑拉回"));
            if (resetSubMode.enumValueIndex == 1)
            {
                EditorGUILayout.PropertyField(resetLerpFactor, new GUIContent("归位平滑速度", "数值越大拉回越快"));
            }
            EditorGUILayout.PropertyField(lockLookAtPlayer, new GUIContent("看向角色", "归位过程中持续看向角色"));
            DrawProjectionSection();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField(
            mode == CamMoveMode.Teleport ? "瞬移切镜" : "平滑运镜",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            mode == CamMoveMode.Teleport ? "片段开始即切到目标机位，适合甩镜/切镜" : "镜头自起始机位平滑过渡到目标机位",
            EditorStyles.miniLabel);

        EditorGUILayout.PropertyField(useLastFrameAsOrigin, new GUIContent("以上一帧位置为起点", "以片段开始瞬间的相机位置作为插值起点；连续多段运镜建议开启"));
        EditorGUILayout.PropertyField(useSurroundMode, new GUIContent("圆弧环绕", "按圆弧轨迹环绕角色，忽略目标坐标的距离分量"));
        if (useSurroundMode.boolValue)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("环绕参数", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(surroundRadius, new GUIContent("环绕半径", "圆弧半径"));
            EditorGUILayout.PropertyField(surroundTotalAngle, new GUIContent("扫过角度", "180=绕至侧面/脑后；负值反向"));
            EditorGUILayout.PropertyField(surroundFixedHeight, new GUIContent("圆心高度", "圆弧圆心相对角色脚底的高度"));
            EditorGUILayout.PropertyField(
                useSurroundRadiusCurve,
                new GUIContent("半径曲线", "用曲线控制环绕期间的远近变化"));
            if (useSurroundRadiusCurve.boolValue)
            {
                EditorGUILayout.PropertyField(
                    surroundRadiusCurve,
                    new GUIContent("半径倍率", "1=基础环绕半径"),
                    true);
            }
            EditorGUILayout.PropertyField(
                useSurroundHeightCurve,
                new GUIContent("高度曲线", "用曲线控制环绕期间的升降变化"));
            if (useSurroundHeightCurve.boolValue)
            {
                EditorGUILayout.PropertyField(
                    surroundHeightCurve,
                    new GUIContent("高度倍率", "1=基础圆心高度"),
                    true);
            }
            EditorGUILayout.LabelField(
                "Scene 视图拖拽轨迹终点可改半径、总角度和高度",
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("目标机位", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cameraTargetLocalPos, new GUIContent("机位坐标(相对角色)", "目标坐标随角色移动与转向换算"));
            EditorGUILayout.PropertyField(cameraTargetEuler, new GUIContent("机位朝向(相对角色)", "目标朝向的本地欧拉角"));
            EditorGUILayout.LabelField(
                "Scene 视图拖拽轨迹终点可改位置，关闭看向角色后可继续拖旋转",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.PropertyField(lockLookAtPlayer, new GUIContent("看向角色", "全程看向角色；关闭则使用机位朝向"));
        DrawMotionCurveSection();
        DrawProjectionSection();
        DrawAxisConstraintSection();
        Draw2DTurnSection();

        if (mode == CamMoveMode.SmoothLerp)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("过渡手感", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(smoothLerpFactor, new GUIContent("平滑系数", "插值系数，数值越大过渡越跟手"));
            EditorGUILayout.PropertyField(useVariableSpeed, new GUIContent("变速过渡", "速度由起始值线性过渡至结束值"));
            if (useVariableSpeed.boolValue)
            {
                EditorGUILayout.PropertyField(startSpeed, new GUIContent("起始速度", "0=静止起步"));
                EditorGUILayout.PropertyField(endSpeed, new GUIContent("结束速度", "片段结束时达到的速度"));
            }
        }

        serializedObject.ApplyModifiedProperties();
        if (serializedObject.hasModifiedProperties)
        {
            EditorApplication.delayCall += () => { SceneView.RepaintAll(); };
        }
    }

    private void DrawProjectionSection()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Project 2D / 3D 模式申请", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            modeRequest,
            new GUIContent("请求模式", "不申请 / 申请正交 2D / 申请透视 3D"));
        if ((TimelineCameraModeRequest)modeRequest.enumValueIndex ==
            TimelineCameraModeRequest.None)
        {
            EditorGUILayout.LabelField(
                "保持 Project CameraModeController 当前模式",
                EditorStyles.miniLabel);
            return;
        }

        if ((TimelineCameraModeRequest)modeRequest.enumValueIndex ==
            TimelineCameraModeRequest.Side2D)
        {
            EditorGUILayout.PropertyField(
                overrideSide2DYaw,
                new GUIContent("指定 2D 角度", "申请 2D 时同时设置 Project 的绝对 Yaw"));
            if (overrideSide2DYaw.boolValue)
            {
                EditorGUILayout.PropertyField(
                    side2DYawDegrees,
                    new GUIContent("绝对 Yaw", "0=+Z，90=右一个面"));
            }
        }

        EditorGUILayout.PropertyField(
            projectionTransitionDuration,
            new GUIContent("模式切换时间", "申请通过后由 Project CameraModeController 执行过渡"));
    }

    private void DrawMotionCurveSection()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("运动曲线", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            useMotionCurve,
            new GUIContent("使用运动曲线", "用曲线控制镜头的归一化运动进度"));
        if (!useMotionCurve.boolValue)
        {
            return;
        }

        EditorGUILayout.PropertyField(
            motionCurve,
            new GUIContent("运动曲线", "X=时间进度，Y=镜头运动进度"),
            true);
    }

    private void Draw2DTurnSection()
    {
        bool is2D =
            (TimelineCameraModeRequest)modeRequest.enumValueIndex !=
            TimelineCameraModeRequest.Perspective3D;
        if (!is2D)
        {
            return;
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("2D 平面转向", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            turnTiming,
            new GUIContent("转向时机", "不处理 / 片段开始时 / 片段结束时"));
        if ((Camera2DTurnTiming)turnTiming.enumValueIndex ==
            Camera2DTurnTiming.None)
        {
            return;
        }

        EditorGUILayout.PropertyField(
            turnAngleDegrees,
            new GUIContent("转向角度", "平面右转为正；90=转到下一个面"));
        EditorGUILayout.PropertyField(
            turnDuration,
            new GUIContent("转向时长"));
        EditorGUILayout.PropertyField(
            turnCurve,
            new GUIContent("转向速度曲线", "X=转向时间进度，Y=角度进度"),
            true);
    }

    private void DrawAxisConstraintSection()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("2D 正交轴约束", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            constrainOrthographicAxes,
            new GUIContent("启用轴约束", "只在目标为正交投影时生效"));
        if (!constrainOrthographicAxes.boolValue)
        {
            return;
        }

        EditorGUILayout.LabelField("允许移动轴", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(allowPositionX, new GUIContent("允许 X"));
        EditorGUILayout.PropertyField(allowPositionY, new GUIContent("允许 Y"));
        EditorGUILayout.PropertyField(allowPositionZ, new GUIContent("允许 Z"));
        EditorGUILayout.LabelField(
            "例如只保留 X，即可锁定为左右移动的 2D 相机",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(2);
        DrawAxisClamp("X 范围", clampPositionX, positionXRange);
        DrawAxisClamp("Y 范围", clampPositionY, positionYRange);
        DrawAxisClamp("Z 范围", clampPositionZ, positionZRange);
    }

    private static void DrawAxisClamp(
        string label,
        SerializedProperty enabled,
        SerializedProperty range)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(enabled, new GUIContent(label));
        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            EditorGUILayout.PropertyField(range, GUIContent.none);
        }
        EditorGUILayout.EndHorizontal();
    }

}
