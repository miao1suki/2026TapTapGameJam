using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

[CustomEditor(typeof(TimelineActorHost))]
public class TimelineActorHostInspector : Editor
{
    private SerializedProperty script;
    private SerializedProperty attackPoint;
    private SerializedProperty targetMask;
    private SerializedProperty ignoreTags;
    private SerializedProperty useHitForce;
    private SerializedProperty autoExcludeSelf;
    private SerializedProperty ignoreNonDamageable;
    private SerializedProperty sfxSource;
    private SerializedProperty randomizePitch;
    private SerializedProperty pitchRange;

    private void OnEnable()
    {
        script = serializedObject.FindProperty("m_Script");
        attackPoint = serializedObject.FindProperty("attackPoint");
        targetMask = serializedObject.FindProperty("targetMask");
        ignoreTags = serializedObject.FindProperty("ignoreTags");
        useHitForce = serializedObject.FindProperty("useHitForce");
        autoExcludeSelf =
            serializedObject.FindProperty("autoExcludeSelf");
        ignoreNonDamageable =
            serializedObject.FindProperty("ignoreNonDamageable");
        sfxSource = serializedObject.FindProperty("sfxSource");
        randomizePitch =
            serializedObject.FindProperty("randomizePitch");
        pitchRange = serializedObject.FindProperty("pitchRange");
    }

    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        TimelineActorHost host = (TimelineActorHost)target;
        if (host != null &&
            host.GetComponent<PlayableDirector>() == null)
        {
            Undo.AddComponent<PlayableDirector>(host.gameObject);
        }

        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(
                script,
                new GUIContent("脚本"));
        }

        DrawSection("命中扫描");
        EditorGUILayout.PropertyField(
            attackPoint,
            new GUIContent("判定锚点"));
        EditorGUILayout.PropertyField(
            targetMask,
            new GUIContent("可命中的层"));
        DrawIgnoreTags();
        EditorGUILayout.PropertyField(
            useHitForce,
            new GUIContent("命中击退"));
        EditorGUILayout.PropertyField(
            autoExcludeSelf,
            new GUIContent("排除自身"));
        EditorGUILayout.PropertyField(
            ignoreNonDamageable,
            new GUIContent("只打可伤害目标"));

        DrawSection("音效与特效");
        EditorGUILayout.PropertyField(
            sfxSource,
            new GUIContent(
                "音效源",
                "留空时自动查找或创建同物体上的 AudioSource"));
        EditorGUILayout.PropertyField(
            randomizePitch,
            new GUIContent("随机音调"));
        if (randomizePitch.boolValue)
        {
            EditorGUILayout.PropertyField(
                pitchRange,
                new GUIContent("音调范围"));
        }

        serializedObject.ApplyModifiedProperties();

        if (!Application.isPlaying)
        {
            return;
        }

        DrawSection("运行状态");
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle(
                "判定窗口开启",
                host.HasHitWindow);
            EditorGUILayout.FloatField(
                "当前伤害",
                host.CurrentData.damage);
        }
    }

    private void DrawIgnoreTags()
    {
        string[] tags =
            UnityEditorInternal.InternalEditorUtility.tags;
        int currentMask = GetTagMask(tags);
        int nextMask = EditorGUILayout.MaskField(
            new GUIContent(
                "忽略标签",
                "点击展开项目 Tag 列表，可多选"),
            currentMask,
            tags);
        if (nextMask != currentMask)
        {
            SetIgnoreTagsFromMask(tags, nextMask);
        }

        bool hasUnknown = false;
        for (int index = 0;
             index < ignoreTags.arraySize;
             index++)
        {
            string value =
                ignoreTags.GetArrayElementAtIndex(index)
                    .stringValue;
            if (GetTagIndex(tags, value) < 0)
            {
                hasUnknown = true;
                break;
            }
        }

        if (hasUnknown)
        {
            EditorGUILayout.HelpBox(
                "存在不在当前项目 Tag 列表中的旧标签，已保留但不会显示。",
                MessageType.Warning);
        }
    }

    private int GetTagMask(string[] tags)
    {
        int mask = 0;
        for (int index = 0;
             index < ignoreTags.arraySize;
             index++)
        {
            string value =
                ignoreTags.GetArrayElementAtIndex(index)
                    .stringValue;
            int tagIndex = GetTagIndex(tags, value);
            if (tagIndex >= 0 && tagIndex < 32)
            {
                mask |= 1 << tagIndex;
            }
        }

        return mask;
    }

    private void SetIgnoreTagsFromMask(
        string[] tags,
        int mask)
    {
        string[] unknownTags = GetUnknownTags(tags);
        ignoreTags.arraySize = 0;

        for (int index = 0; index < tags.Length; index++)
        {
            if (index >= 32 ||
                (mask & (1 << index)) == 0)
            {
                continue;
            }

            AddIgnoreTag(tags[index]);
        }

        for (int index = 0;
             index < unknownTags.Length;
             index++)
        {
            AddIgnoreTag(unknownTags[index]);
        }
    }

    private void AddIgnoreTag(string tag)
    {
        int index = ignoreTags.arraySize;
        ignoreTags.InsertArrayElementAtIndex(index);
        ignoreTags.GetArrayElementAtIndex(index)
            .stringValue = tag;
    }

    private string[] GetUnknownTags(string[] tags)
    {
        string[] unknown =
            new string[ignoreTags.arraySize];
        int count = 0;
        for (int index = 0;
             index < ignoreTags.arraySize;
             index++)
        {
            string value =
                ignoreTags.GetArrayElementAtIndex(index)
                    .stringValue;
            if (GetTagIndex(tags, value) < 0)
            {
                unknown[count++] = value;
            }
        }

        if (count == unknown.Length)
        {
            return unknown;
        }

        string[] result = new string[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = unknown[index];
        }

        return result;
    }

    private static int GetTagIndex(
        string[] tags,
        string tag)
    {
        for (int index = 0; index < tags.Length; index++)
        {
            if (tags[index] == tag)
            {
                return index;
            }
        }

        return -1;
    }

    private static void DrawSection(string title)
    {
        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField(
            title,
            EditorStyles.boldLabel);
    }
}
