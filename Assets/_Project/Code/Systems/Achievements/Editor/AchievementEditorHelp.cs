using UnityEditor;
using UnityEngine;

namespace Project.Achievements.Editor
{
    internal static class AchievementEditorHelp
    {
        private const string ManagerKey =
            "2026TapTap.Achievements.ManagerHelp";
        private const string DetectorKey =
            "2026TapTap.Achievements.DetectorHelp";
        private const string BridgeKey =
            "2026TapTap.Achievements.BridgeHelp";

        internal static void DrawManagerHelp()
        {
            Draw(
                ManagerKey,
                "AchievementManager · 成就管理器",
                "【这是什么】\n" +
                "成就系统的运行时中心，负责加载已发布的成就目录、维护成就状态、" +
                "接收条件信号、计算条件关系、保存 JSON 和通知外部显示组件。\n\n" +
                "【调用方式】\n" +
                "代码可以通过成就编号或成就名字发出条件信号。\n" +
                "“触发一次”忽略数量和进度；\n" +
                "“累计数量”使用增加计数；“完成百分比”使用增加进度，1 表示 1%。\n\n" +
                "【显示接口】\n" +
                "Canvas 后续实现 IAchievementUnlockReceiver 并注册，" +
                "管理器只发送成就名字，不负责 UI 展示。\n\n" +
                "【注意】\n" +
                "管理器只负责逻辑和状态，不直接依赖 Canvas、输入或 Timeline。");
        }

        internal static void DrawDetectorHelp()
        {
            Draw(
                DetectorKey,
                "AchievementDetector · 成就检测组件",
                "【这是什么】\n" +
                "把同一物体上其他组件的事件转换成成就触发请求。\n\n" +
                "【支持来源】\n" +
                "玩法组件通常通过 AchievementSignalBridge 提供信号；" +
                "自定义脚本也可以通过 IAchievementSignalSource 直接提供信号；" +
                "官方 Button、Slider、Toggle、Dropdown、InputField、ScrollRect " +
                "可以直接监听其 UnityEvent。\n\n" +
                "【检测项】\n" +
                "每项可以选择来源组件、信号、成就编号或成就名字、" +
                "条件编号、增加计数和增加进度。\n\n" +
                "【注意】\n" +
                "检测组件不判断成就是否完成，只负责上报和转发。");
        }

        internal static void DrawBridgeHelp()
        {
            Draw(
                BridgeKey,
                "AchievementSignalBridge · 成就信号桥",
                "【这是什么】\n" +
                "把玩法系统发出的 GameplaySignalHub 信号转换成成就检测器可订阅的信号。\n\n" +
                "【信号来源】\n" +
                "可以指向同物体上实现 IAchievementSignalProvider 的玩家、相机、平台、" +
                "按钮、游戏流程或战斗组件，并从下拉框选择它支持的信号。\n" +
                "来源留空时，它会作为全局桥接收所有物体发出的同名信号。\n\n" +
                "【调用接口】\n" +
                "Emit() 使用当前选择的信号名；EmitWithName、EmitWithCount、" +
                "EmitWithProgress 和 EmitWithValues 可供 UnityEvent 或脚本调用。\n\n" +
                "【注意】\n" +
                "Bridge 只转发信号，不判断成就是否完成。玩法组件仍然只依赖 " +
                "GameplaySignalHub，不直接依赖成就系统。");
        }

        private static void Draw(
            string preferenceKey,
            string title,
            string body)
        {
            bool open = EditorPrefs.GetBool(
                preferenceKey,
                false);
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox);
            bool next = EditorGUILayout.Foldout(
                open,
                "组件说明 · " + title,
                true);
            if (next != open)
            {
                EditorPrefs.SetBool(
                    preferenceKey,
                    next);
            }

            if (next)
            {
                EditorGUILayout.LabelField(
                    body,
                    EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
