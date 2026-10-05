using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GameFlow.Editor
{
    internal static class GameFlowInspectorUi
    {
        public static VisualElement Root(
            SerializedObject serializedObject,
            string title)
        {
            var root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;

            var label = new Label(title);
            label.style.fontSize = 17f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6f;
            root.Add(label);
            root.Add(Property(
                serializedObject,
                "m_Script",
                null));
            return root;
        }

        public static Foldout Foldout(
            string title,
            bool expanded = true)
        {
            var foldout = new Foldout
            {
                text = title,
                value = expanded
            };
            foldout.style.marginTop = 6f;
            return foldout;
        }

        public static PropertyField Property(
            SerializedObject serializedObject,
            string path,
            string label,
            string tooltip = null)
        {
            SerializedProperty property =
                serializedObject.FindProperty(path);
            var field = string.IsNullOrEmpty(label)
                ? new PropertyField(property)
                : new PropertyField(property, label);
            if (!string.IsNullOrEmpty(tooltip))
            {
                field.tooltip = tooltip;
            }

            return field;
        }

        public static VisualElement ReadOnly(
            string label,
            Func<string> getter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2f;

            var name = new Label(label);
            name.style.width = 112f;
            name.style.opacity = .72f;
            row.Add(name);

            var value = new Label();
            value.style.flexGrow = 1f;
            value.style.whiteSpace = WhiteSpace.Normal;
            row.Add(value);

            Action refresh = () =>
                value.text = getter != null
                    ? getter() ?? "无"
                    : "无";
            refresh();
            row.schedule.Execute(refresh).Every(120);
            return row;
        }

        public static HelpBox Help(string text)
        {
            var help = new HelpBox(
                text,
                HelpBoxMessageType.Info);
            help.style.marginTop = 6f;
            help.style.whiteSpace = WhiteSpace.Normal;
            return help;
        }
    }

    [CustomEditor(typeof(GameFlowController))]
    public sealed class GameFlowControllerEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "游戏流程控制器");
            Foldout flow = GameFlowInspectorUi.Foldout(
                "场景流程");
            flow.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "catalog",
                "场景目录"));
            flow.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "initialScene",
                "初始场景"));
            flow.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "initializeOnStart",
                "启动时初始化"));
            root.Add(flow);

            GameFlowController controller =
                (GameFlowController)target;
            Foldout status = GameFlowInspectorUi.Foldout(
                "运行时状态");
            status.Add(GameFlowInspectorUi.ReadOnly(
                "初始化",
                () => controller != null &&
                       controller.IsInitialized
                    ? "已完成"
                    : "未完成"));
            status.Add(GameFlowInspectorUi.ReadOnly(
                "切换中",
                () => controller != null &&
                       controller.IsTransitioning
                    ? "是"
                    : "否"));
            status.Add(GameFlowInspectorUi.ReadOnly(
                "当前场景",
                () => controller?.ActiveSceneId?.ToString() ??
                      "无"));
            root.Add(status);
            root.Add(GameFlowInspectorUi.Help(
                "场景加载、常驻系统初始化和关卡切换由此统一控制。"));
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(GameFlowSceneRoot))]
    public sealed class GameFlowSceneRootEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "流程场景标识");
            Foldout scene = GameFlowInspectorUi.Foldout(
                "场景");
            scene.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "sceneId",
                "场景编号"));
            root.Add(scene);
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(GameSystemSceneRoot))]
    public sealed class GameSystemSceneRootEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "常驻系统场景");
            Foldout setup = GameFlowInspectorUi.Foldout(
                "系统根");
            setup.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "kind",
                "系统类型"));
            root.Add(setup);

            GameSystemSceneRoot sceneRoot =
                (GameSystemSceneRoot)target;
            Foldout status = GameFlowInspectorUi.Foldout(
                "初始化状态");
            status.Add(GameFlowInspectorUi.ReadOnly(
                "状态",
                () => sceneRoot != null &&
                       sceneRoot.IsInitialized
                    ? "已初始化"
                    : "待初始化"));
            root.Add(status);
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(LevelSceneContext))]
    public sealed class LevelSceneContextEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "关卡场景上下文");
            Foldout scene = GameFlowInspectorUi.Foldout(
                "场景配置");
            scene.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "sceneId",
                "关卡编号"));
            scene.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "playerSpawn",
                "玩家出生点"));
            scene.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "playerPrefab",
                "玩家预制体"));
            root.Add(scene);

            LevelSceneContext context =
                (LevelSceneContext)target;
            Foldout runtime = GameFlowInspectorUi.Foldout(
                "运行时状态");
            runtime.Add(GameFlowInspectorUi.ReadOnly(
                "当前玩家",
                () => context?.SpawnedPlayer != null
                    ? context.SpawnedPlayer.name
                    : "未生成"));
            root.Add(runtime);
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(LevelGoal))]
    public sealed class LevelGoalEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "关卡终点");
            Foldout goal = GameFlowInspectorUi.Foldout(
                "完成配置");
            goal.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "nextScene",
                "下一场景"));
            goal.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "endsGame",
                "结束游戏"));
            goal.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "transitionDelay",
                "转场延迟"));
            goal.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "animatedVisual",
                "动画物体"));
            root.Add(goal);

            LevelGoal levelGoal = (LevelGoal)target;
            Foldout status = GameFlowInspectorUi.Foldout(
                "运行时状态");
            status.Add(GameFlowInspectorUi.ReadOnly(
                "完成中",
                () => levelGoal != null &&
                       levelGoal.IsCompleting
                    ? "是"
                    : "否"));
            root.Add(status);
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(PlayerSpawnPoint))]
    public sealed class PlayerSpawnPointEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "玩家出生点");
            root.Add(GameFlowInspectorUi.Help(
                "在 Scene 中把此组件放在玩家出生位置；关卡上下文会读取其 Transform。"));
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(StartupCameraGuard))]
    public sealed class StartupCameraGuardEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "启动相机保护");
            root.Add(GameFlowInspectorUi.Help(
                "仅在没有任何可用相机时临时创建；真实相机出现后会自动销毁。"));
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(GameAudioService))]
    public sealed class GameAudioServiceEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "游戏音频服务");
            Foldout sources = GameFlowInspectorUi.Foldout(
                "音源");
            sources.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "musicSource",
                "音乐音源"));
            sources.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "soundEffectSource",
                "音效音源"));
            root.Add(sources);

            GameAudioService service =
                (GameAudioService)target;
            Foldout status = GameFlowInspectorUi.Foldout(
                "运行时状态");
            status.Add(GameFlowInspectorUi.ReadOnly(
                "初始化",
                () => service != null &&
                       service.IsInitialized
                    ? "已完成"
                    : "未完成"));
            root.Add(status);
            root.Bind(serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(GameUiRouter))]
    public sealed class GameUiRouterEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = GameFlowInspectorUi.Root(
                serializedObject,
                "游戏界面路由");
            Foldout screens = GameFlowInspectorUi.Foldout(
                "界面");
            screens.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "mainMenuScreen",
                "主菜单"));
            screens.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "gameplayHud",
                "游戏 HUD"));
            screens.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "pauseScreen",
                "暂停界面"));
            screens.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "endingScreen",
                "结局界面"));
            screens.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "loadingScreen",
                "加载界面"));
            root.Add(screens);

            Foldout buttons = GameFlowInspectorUi.Foldout(
                "按钮");
            buttons.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "startGameButton",
                "开始游戏"));
            buttons.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "mainMenuQuitButton",
                "主菜单退出"));
            buttons.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "resumeButton",
                "继续"));
            buttons.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "reloadLevelButton",
                "重新开始"));
            buttons.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "returnToMenuButton",
                "返回主菜单"));
            buttons.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "endingReturnToMenuButton",
                "结局返回主菜单"));
            buttons.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "endingQuitButton",
                "结局退出"));
            root.Add(buttons);

            Foldout labels = GameFlowInspectorUi.Foldout(
                "文本");
            labels.Add(GameFlowInspectorUi.Property(
                serializedObject,
                "gameplaySceneLabel",
                "关卡名称"));
            root.Add(labels);

            GameUiRouter router = (GameUiRouter)target;
            Foldout status = GameFlowInspectorUi.Foldout(
                "运行时状态");
            status.Add(GameFlowInspectorUi.ReadOnly(
                "暂停",
                () => router != null && router.IsPaused
                    ? "是"
                    : "否"));
            root.Add(status);
            root.Bind(serializedObject);
            return root;
        }
    }
}
