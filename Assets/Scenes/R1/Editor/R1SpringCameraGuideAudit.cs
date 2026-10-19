using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.LevelEditor;
using Project.Levels.ILorR;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>Explicit, short edit-mode checks. Never saves an authored scene.</summary>
public static class R1SpringCameraGuideAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlaying)
            throw new InvalidOperationException("仅允许在关闭主编辑器后的独立批处理检查中运行。");
        const string path = "Assets/Scenes/R1/R1.unity";
        byte[] before = File.ReadAllBytes(path);
        byte[] planning = File.ReadAllBytes("Assets/Scenes/R1/R1.planning.json");
        try
        {
            EditorSceneManager.OpenScene(path);
            R1SpringCameraGuide authored = Object.FindFirstObjectByType<R1SpringCameraGuide>();
            Check(authored != null && authored.GetComponent<CameraControlManager>() != null, "保存场景正确挂在现有相机");
            Check(!authored.AutomaticFraming, "实际R1保存为手动尺寸模式");
            Check(authored.ButtonAnchor.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_button_middle", "绑定U26按钮稳定ID");
            Check(authored.LandingPlantAnchor.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_spring_lower", "绑定Q41:S41植物稳定ID");
            Vector3 button = authored.ButtonAnchor.position;
            Vector3 spring = authored.LandingPlantAnchor.position;
            Check(Vector3.Distance(button, new Vector3(42.5f, -20.5f, 0f)) < .001f, "使用镜像后的实际按钮位置");
            Check(Vector3.Distance(spring, new Vector3(45.5f, -35.5f, 0f)) < .001f, "使用镜像后的实际植物位置");
            Check(new SerializedObject(authored.GetComponent<CameraModeController>()).FindProperty("side2D.orthographicSize").floatValue == 12f,
                "普通视野仍为12");
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "不新增玩法相机");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("镜头专项相机");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.aspect = 16f / 9f;
            CameraControlManager manager = cameraObject.AddComponent<CameraControlManager>();
            manager.ConfigureOutput(camera);
            CameraModeController mode = cameraObject.AddComponent<CameraModeController>();
            Set(mode, "side2D.orthographicSize", 12f);
            manager.SetViewModeAuthority(mode);
            manager.RequestControl(mode);
            PlayerController player = new GameObject("镜头专项玩家").AddComponent<PlayerController>();
            manager.SetFocusTarget(player.transform);
            player.GetComponent<Rigidbody>().useGravity = false;
            var anchor = new GameObject("起点按钮");
            anchor.transform.position = button;
            var plantObject = new GameObject("落点植物");
            BoxCollider surface = plantObject.AddComponent<BoxCollider>();
            surface.size = new Vector3(3f, 1f, 1f);
            plantObject.transform.position = spring;
            BouncePlantFeature plant = plantObject.AddComponent<BouncePlantFeature>();
            plant.enabled = true;
            plant.GetComponent<BlockRuntime>().RefreshFeatureSet();
            Check(plant.IsBounceSurfaceAvailable, "夹具启用现有植物契约");
            R1SpringCameraGuide guide = cameraObject.AddComponent<R1SpringCameraGuide>();
            Set(guide, "buttonAnchor", anchor.transform);
            Set(guide, "landingPlantAnchor", plant.transform);
            Set(guide, "enterSeconds", 0f);
            Set(guide, "restoreSeconds", 0f);
            Invoke(guide, "OnEnable");

            Check(!guide.AutomaticFraming, "新增组件默认手动尺寸");
            Move(player, button + Vector3.up);
            Set(guide, "expandedSize", 16f);
            Tick(guide, manager);
            Check(guide.GuideActive && Mathf.Approximately(camera.orthographicSize, 16f), "手动16不会被自动入镜下限顶住");
            Set(guide, "expandedSize", 24f);
            Tick(guide, manager);
            Check(Mathf.Approximately(camera.orthographicSize, 24f), "触发后实时调大");
            Set(guide, "expandedSize", 14f);
            Tick(guide, manager);
            Check(Mathf.Approximately(camera.orthographicSize, 14f), "触发后实时调小，不锁历史最大值");
            Set(guide, "expandedSize", 14.01f);
            Tick(guide, manager);
            Check(Mathf.Abs(camera.orthographicSize - 14.01f) < .001f, "小幅数值修改同样生效");
            camera.aspect = .2f;
            Tick(guide, manager);
            Check(Mathf.Abs(camera.orthographicSize - 14.01f) < .001f, "手动模式不因画面比例自动放大");
            camera.aspect = 16f / 9f;
            Set(guide, "autoFramePlant", true);
            Tick(guide, manager);
            float fitted = camera.orthographicSize;
            Check(fitted > 14.01f, "运行中开启自动入镜即时补足视野");
            Set(guide, "expandedSize", 13f);
            Tick(guide, manager);
            Check(Mathf.Approximately(camera.orthographicSize, fitted), "自动模式仍保留完整入镜与最大值锁存");
            Set(guide, "autoFramePlant", false);
            Tick(guide, manager);
            Check(Mathf.Approximately(camera.orthographicSize, 13f), "关闭自动模式立即摆脱其历史最大尺寸");
            Set(guide, "expandedSize", 8f);
            Tick(guide, manager);
            Check(Mathf.Approximately(camera.orthographicSize, 12f), "手动尺寸不小于普通视野");
            Set(mode, "side2D.orthographicSize", 15f);
            Tick(guide, manager);
            Check(Mathf.Approximately(camera.orthographicSize, 15f), "下限读取当前基础尺寸而非硬编码12");
            Set(mode, "side2D.orthographicSize", 12f);
            Set(guide, "expandedSize", 17f);
            Move(player, Vector3.Lerp(button, spring, .5f));
            Tick(guide, manager);
            Check(guide.GuideActive && Mathf.Approximately(camera.orthographicSize, 17f), "离开按钮后仍保持手动尺寸");
            Set(guide, "expandedSize", 15f);
            Set(guide, "enterSeconds", 1f);
            Tick(guide, manager, .5f);
            Check(camera.orthographicSize > 15f && camera.orthographicSize < 17f, "实时调小仍有平滑中间帧");
            TickManager(manager, .5f);
            Check(Mathf.Approximately(camera.orthographicSize, 15f), "实时调小最终抵达填写值");
            Set(guide, "enterSeconds", 0f);
            Move(player, spring + Vector3.up);
            Tick(guide, manager);
            Check(!guide.GuideActive && Mathf.Approximately(camera.orthographicSize, 12f), "手动模式仍落到指定植物后恢复");

            VerifyModeInspector(guide, false);
            VerifyModeInspector(guide, true);
            Set(guide, "autoFramePlant", false);
            // Use Unity's native serialized copy; transient fixture transforms do not
            // have persistent scene IDs suitable for an editor JSON file round-trip.
            var serializedCopyObject = new GameObject("尺寸序列化夹具");
            var serializedCopy = serializedCopyObject.AddComponent<R1SpringCameraGuide>();
            EditorUtility.CopySerialized(guide, serializedCopy);
            Check(!serializedCopy.AutomaticFraming &&
                new SerializedObject(serializedCopy).FindProperty("expandedSize").floatValue == 15f,
                "模式和手动尺寸均可序列化复制");
            Check(serializedCopy.ButtonAnchor == anchor.transform && serializedCopy.LandingPlantAnchor == plant.transform,
                "序列化复制保留目标引用");
            Object.DestroyImmediate(serializedCopyObject);

            // Preserve the original automatic-framing / landing lifecycle regression.
            Set(guide, "autoFramePlant", true);
            Set(guide, "expandedSize", 20f);

            Move(player, button + Vector3.left * 10f);
            Tick(guide, manager);
            Check(!guide.GuideActive && Mathf.Approximately(camera.orthographicSize, 12f), "范围外保持普通视野");
            Move(player, button + Vector3.up);
            Tick(guide, manager);
            Check(guide.GuideActive && camera.orthographicSize >= 19.999f,
                $"接近已踩过也可触发，不依赖按钮一次性事件；激活={guide.GuideActive}，请求={guide.RequestedSize}，输出={camera.orthographicSize}");
            CheckVisible(camera, surface.bounds);
            float expanded = camera.orthographicSize;
            for (int i = 0; i < 5; i++) Tick(guide, manager);
            Check(Mathf.Abs(camera.orthographicSize - expanded) < .001f, "不从缩放后输出重复放大");
            Move(player, Vector3.Lerp(button, spring, .5f));
            Tick(guide, manager);
            Check(guide.GuideActive && camera.orthographicSize >= expanded, "离开按钮后跳跃途中保持拉远");
            Check(Mathf.Abs(camera.transform.position.x - player.transform.position.x) < .001f, "保留玩家跟随");
            Move(player, spring + new Vector3(-2f, 0f, 0f));
            Tick(guide, manager);
            Check(guide.GuideActive, "仅碰植物侧面不恢复");
            Move(player, spring + Vector3.up * 2f);
            Tick(guide, manager);
            Check(guide.GuideActive, "从植物上方经过尚未落地不恢复");
            Move(player, spring + Vector3.up);
            Tick(guide, manager);
            Check(!guide.GuideActive && Mathf.Approximately(camera.orthographicSize, 12f), "真实顶面接触恢复，无需触发回弹");

            Move(player, button + Vector3.up);
            Tick(guide, manager);
            Check(guide.GuideActive, "再次走到按钮可再次触发");
            Move(player, spring + Vector3.up * 1.2f);
            Check(plant.TryGetBounceSpeed(player, 3f, 8f, out _), "植物现有回弹入口触发");
            TickManager(manager, 1f);
            Check(!guide.GuideActive && Mathf.Approximately(camera.orthographicSize, 12f), "回弹当帧释放，不等玩家已弹离后的区域检测");

            Move(player, button + Vector3.up);
            Tick(guide, manager);
            plant.ResetForRoom();
            Check(!guide.GuideActive && !manager.HasLensMultiplierRequest, "房间R/离区复原事件释放请求");
            Tick(guide, manager);
            Check(guide.GuideActive, "按钮进度保留时，复原后仍可再次触发");
            Invoke(guide, "OnPlayerRelocated");
            Check(!manager.HasLensMultiplierRequest, "复活或传送清理");
            Tick(guide, manager);
            using (Project.GameplayPresentationGate.Suppress())
            {
                Tick(guide, manager);
                Check(!manager.HasLensMultiplierRequest, "菜单预览不占用玩法镜头");
            }
            Tick(guide, manager);
            CameraLensRequestHandle other = manager.RequestLensMultiplier(new object(), 2.5f);
            Invoke(guide, "OnDisable");
            TickManager(manager, 1f);
            Check(manager.HasLensMultiplierRequest && Mathf.Approximately(camera.orthographicSize, 30f), "停用只释放自身，不清其他镜头请求");
            other.Release();
            Invoke(guide, "OnEnable");
            Tick(guide, manager);
            Set(guide, "restoreSeconds", 1f);
            Move(player, spring + Vector3.up);
            Tick(guide, manager, .5f);
            Check(camera.orthographicSize > 12f && camera.orthographicSize < 20f, "恢复有平滑中间帧");
            TickManager(manager, .5f);
            Check(Mathf.Approximately(camera.orthographicSize, 12f), "恢复最终回到基础12");

            Set(guide, "enterSeconds", 1f);
            Move(player, button + Vector3.up);
            Tick(guide, manager, .5f);
            Check(camera.orthographicSize > 12f && camera.orthographicSize < 20f, "拉远有平滑中间帧");
            TickManager(manager, .5f);
            Check(camera.orthographicSize >= 20f, "拉远过渡完成");
            camera.aspect = .3f;
            Tick(guide, manager);
            TickManager(manager, 1f);
            CheckVisible(camera, surface.bounds);

            var inspector = UnityEditor.Editor.CreateEditor(guide);
            try
            {
                VisualElement root = inspector.CreateInspectorGUI();
                Check(root != null && root.Query<Foldout>().ToList().Count == 4, "中文折叠面板完整");
                Check(root.Query<Button>().ToList().Count == 1, "调试按钮存在且编辑态禁用");
                Check(!root.Q<Button>().enabledSelf, "编辑态不会误执行调试");
            }
            finally { Object.DestroyImmediate(inspector); }
            var second = new GameObject("多选夹具").AddComponent<R1SpringCameraGuide>();
            var multiple = UnityEditor.Editor.CreateEditor(new Object[] { guide, second });
            try { Check(multiple.CreateInspectorGUI() != null, "多选且空引用不抛异常"); }
            finally { Object.DestroyImmediate(multiple); }
            Check(before.SequenceEqual(File.ReadAllBytes(path)), "作者场景文件字节不变");
            Check(planning.SequenceEqual(File.ReadAllBytes("Assets/Scenes/R1/R1.planning.json")), "作者规划文件字节不变");
            Debug.Log($"[R1SpringCameraGuideAudit] PASS {checks} checks; editor-only, no authored scene saved.");
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private static void VerifyModeInspector(R1SpringCameraGuide guide, bool automatic)
    {
        Set(guide, "autoFramePlant", automatic);
        var inspector = UnityEditor.Editor.CreateEditor(guide);
        try
        {
            VisualElement root = inspector.CreateInspectorGUI();
            Check(root.Q("spring-camera-framing-margin").style.display.value ==
                (automatic ? DisplayStyle.Flex : DisplayStyle.None), "构图边距仅自动模式显示");
            Check(root.Q("spring-camera-manual-help").style.display.value ==
                (automatic ? DisplayStyle.None : DisplayStyle.Flex), "手动说明按模式显隐");
            Check(root.Q("spring-camera-auto-help").style.display.value ==
                (automatic ? DisplayStyle.Flex : DisplayStyle.None), "自动说明按模式显隐");
        }
        finally { Object.DestroyImmediate(inspector); }
    }

    private static void Move(PlayerController player, Vector3 point)
    {
        player.transform.position = point;
        player.GetComponent<Rigidbody>().position = point;
        Physics.SyncTransforms();
    }

    private static void Tick(R1SpringCameraGuide guide, CameraControlManager manager, float seconds = 1f)
    {
        manager.SetFocusPoint(manager.FocusTarget.position);
        Physics.SyncTransforms();
        Invoke(guide, "TickGuide");
        TickManager(manager, seconds);
    }

    private static void TickManager(CameraControlManager manager, float seconds) =>
        typeof(CameraControlManager).GetMethod("Tick", Private).Invoke(manager, new object[] { seconds });
    private static void Invoke(R1SpringCameraGuide guide, string method) =>
        typeof(R1SpringCameraGuide).GetMethod(method, Private).Invoke(guide, null);
    private static void Set(Object target, string name, object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (value is float number) property.floatValue = number;
        else if (value is bool boolean) property.boolValue = boolean;
        else property.objectReferenceValue = (Object)value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CheckVisible(Camera camera, Bounds bounds)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 screen = camera.WorldToViewportPoint(point);
            Check(screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1 && screen.z > 0, "目标植物完整进入镜头");
        }
    }

    private static void Check(bool passed, string label)
    {
        if (!passed) throw new InvalidOperationException("[R1SpringCameraGuideAudit] " + label);
        checks++;
    }
}
