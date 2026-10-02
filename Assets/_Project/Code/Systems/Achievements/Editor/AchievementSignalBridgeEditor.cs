using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEditor;
using UnityEngine;

namespace Project.Achievements.Editor
{
    [CustomEditor(typeof(AchievementSignalBridge), true)]
    public sealed class AchievementSignalBridgeEditor :
        UnityEditor.Editor
    {
        private SerializedProperty script;
        private SerializedProperty signalProvider;
        private SerializedProperty signalId;

        private void OnEnable()
        {
            script = serializedObject.FindProperty("m_Script");
            signalProvider =
                serializedObject.FindProperty("signalProvider");
            signalId = serializedObject.FindProperty("signalId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    script,
                    new GUIContent("脚本"));
            }

            AchievementEditorHelp.DrawBridgeHelp();
            EditorGUILayout.HelpBox(
                "全局成就信号桥接。任何脚本或 UnityEvent 都可以调用 Emit()，"
                + "再让 AchievementDetector 监听本组件。",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                signalProvider,
                new GUIContent(
                    "信号来源（可选）",
                    "实现 IAchievementSignalProvider 的组件"));
            if (GUILayout.Button("使用同物体信号来源"))
            {
                signalProvider.objectReferenceValue =
                    FindProviderOnSameObject();
            }

            AchievementSignalBridge bridge =
                (AchievementSignalBridge)target;
            IReadOnlyList<string> available =
                bridge.GetAvailableSignalIds();
            if (available != null && available.Count > 0)
            {
                List<string> options =
                    new List<string>(available);
                int current = Mathf.Clamp(
                    options.IndexOf(signalId.stringValue),
                    0,
                    options.Count - 1);
                int selected = EditorGUILayout.Popup(
                    "信号",
                    current,
                    options.ToArray());
                if (selected != current)
                {
                    signalId.stringValue = options[selected];
                }
            }
            else
            {
                EditorGUILayout.PropertyField(
                    signalId,
                    new GUIContent(
                        "信号名（自定义）",
                        "没有信号来源时手动填写"));
            }
            serializedObject.ApplyModifiedProperties();
        }

        private Component FindProviderOnSameObject()
        {
            Component component = (Component)target;
            MonoBehaviour[] behaviours =
                component.GetComponents<MonoBehaviour>();
            for (int index = 0;
                 index < behaviours.Length;
                 index++)
            {
                if (behaviours[index] is
                    IAchievementSignalProvider)
                {
                    return behaviours[index];
                }
            }

            return null;
        }
    }
}
