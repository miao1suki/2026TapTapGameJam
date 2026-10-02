using UnityEditor;
using UnityEngine;

namespace TimelineTools3D.Editor
{
    [CustomEditor(typeof(ActSO))]
    public sealed class ActSOEditor : UnityEditor.Editor
    {
        private SerializedProperty script;
        private SerializedProperty actionId;
        private SerializedProperty actionName;
        private SerializedProperty timeline;
        private SerializedProperty defaultNextAction;
        private SerializedProperty priority;
        private SerializedProperty interruptible;
        private SerializedProperty lockMovement;
        private SerializedProperty playbackSpeed;

        private void OnEnable()
        {
            script = serializedObject.FindProperty("m_Script");
            actionId = serializedObject.FindProperty("actionId");
            actionName = serializedObject.FindProperty("actionName");
            timeline = serializedObject.FindProperty("timeline");
            defaultNextAction =
                serializedObject.FindProperty("defaultNextAction");
            priority = serializedObject.FindProperty("priority");
            interruptible =
                serializedObject.FindProperty("interruptible");
            lockMovement =
                serializedObject.FindProperty("lockMovement");
            playbackSpeed =
                serializedObject.FindProperty("playbackSpeed");
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

            DrawSection("基本");
            EditorGUILayout.PropertyField(
                actionId,
                new GUIContent("动作编号"));
            EditorGUILayout.PropertyField(
                actionName,
                new GUIContent("动作名字"));

            DrawSection("Timeline");
            EditorGUILayout.PropertyField(
                timeline,
                new GUIContent("动作 Timeline"));
            if (timeline.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "没有 Timeline 时 PlayerController 不会接受这个动作。",
                    MessageType.Warning);
            }

            DrawSection("切换规则");
            EditorGUILayout.PropertyField(
                defaultNextAction,
                new GUIContent(
                    "默认下一动作",
                    "动作结束后无输入时自动切换到的动作"));
            EditorGUILayout.PropertyField(
                priority,
                new GUIContent("优先级", "数值越大越优先"));
            EditorGUILayout.PropertyField(
                interruptible,
                new GUIContent("允许打断", "关闭后不能被其他动作打断"));
            EditorGUILayout.PropertyField(
                lockMovement,
                new GUIContent("锁定移动", "开启后动作期间停止玩家水平移动"));
            if (lockMovement.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "动作期间玩家水平速度会被清除。",
                    MessageType.None);
            }

            DrawSection("播放");
            EditorGUILayout.PropertyField(
                playbackSpeed,
                new GUIContent("播放速度"));

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(
                title,
                EditorStyles.boldLabel);
        }
    }
}
