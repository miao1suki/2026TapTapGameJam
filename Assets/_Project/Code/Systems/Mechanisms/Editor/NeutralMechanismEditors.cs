using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Mechanisms.Editor
{
    [CustomEditor(typeof(NeutralLadder)), CanEditMultipleObjects]
    public sealed class NeutralLadderEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("无色梯子"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            var settings = ProjectInspectorUtility.CreateFoldout("攀爬设置", true);
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "climbMultiplier", "攀爬倍率", "沿用玩家攀爬输入，倍率作用于上爬速度。"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "slideSpeed", "自然下滑速度", "无输入时的下滑速度，0 表示停留。"));
            settings.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "fastSlideSpeed", "加速下滑速度", "向下输入时的下滑速度。"));
            var orientation = new PropertyField(serializedObject.FindProperty("sideFacing"));
            orientation.SetEnabled(false); settings.Add(orientation);
            root.Add(settings);
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
    [CustomEditor(typeof(DoorHintStyle))]
    public sealed class DoorHintStyleEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("门提示素材"));
            root.Add(new HelpBox("按已按下数量从 0 开始依次配置。缺少素材时显示孔位占位模型。", HelpBoxMessageType.Info));
            foreach (var pair in new[] { ("oneButton", 1), ("twoButtons", 2), ("threeButtons", 3) })
            {
                var foldout = ProjectInspectorUtility.CreateFoldout(pair.Item2 + " 个按钮", true);
                var array = serializedObject.FindProperty(pair.Item1);
                if (array.arraySize != pair.Item2 + 1) { array.arraySize = pair.Item2 + 1; serializedObject.ApplyModifiedProperties(); }
                for (int index = 0; index <= pair.Item2; index++)
                    foldout.Add(new PropertyField(array.GetArrayElementAtIndex(index), "已按下 " + index));
                root.Add(foldout);
            }
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
    [CustomEditor(typeof(DoorHint))]
    public sealed class DoorHintEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("门提示方块"));
            var owner = new PropertyField(serializedObject.FindProperty("owner")); owner.SetEnabled(false); root.Add(owner);
            root.Add(new PropertyField(serializedObject.FindProperty("style")));
            root.Add(new Button(() => { ((DoorHint)target).Refresh(); EditorUtility.SetDirty(target); }) { text = "刷新提示" });
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
