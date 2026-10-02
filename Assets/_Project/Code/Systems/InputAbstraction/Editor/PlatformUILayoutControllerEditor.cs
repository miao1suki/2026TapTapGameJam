using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.InputAbstraction.Editor
{
    [CustomEditor(typeof(PlatformUILayoutController))]
    internal sealed class PlatformUILayoutControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            PlatformUILayoutController controller =
                (PlatformUILayoutController)target;
            InputPlatformMode current = InputBuildTargetUtility.Current;

            EditorGUILayout.HelpBox(
                $"当前编辑预设：{current}\n切换 Build Target 时会先保存旧平台布局，再自动应用新平台布局。",
                MessageType.Info);

            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("收集所有子 UI 为布局对象"))
            {
                Undo.RecordObject(controller, "Collect platform UI layout targets");
                RectTransform[] targets = controller.GetComponentsInChildren<RectTransform>(true);
                controller.ReplaceLayoutTargets(targets);
                EditorUtility.SetDirty(controller);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"保存到 {current} 预设"))
                {
                    RecordLayoutObjects(controller, "Capture platform UI layout");
                    controller.CaptureLayout(current);
                    MarkDirty(controller);
                }

                if (GUILayout.Button($"应用 {current} 预设"))
                {
                    RecordLayoutObjects(controller, "Apply platform UI layout");
                    controller.ApplyLayout(current);
                    MarkDirty(controller);
                }
            }

        }

        internal static void RecordLayoutObjects(
            PlatformUILayoutController controller,
            string undoName)
        {
            List<Object> objects = new List<Object> { controller };
            for (int index = 0; index < controller.LayoutBindings.Count; index++)
            {
                RectTransform target = controller.LayoutBindings[index]?.Target;
                if (target != null)
                {
                    objects.Add(target);
                }
            }

            AddObjects(objects, controller.DesktopOnlyObjects);
            AddObjects(objects, controller.MobileOnlyObjects);
            Undo.RecordObjects(objects.ToArray(), undoName);
        }

        internal static void MarkDirty(PlatformUILayoutController controller)
        {
            EditorUtility.SetDirty(controller);
            if (controller.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }

        private static void AddObjects(List<Object> destination, IReadOnlyList<GameObject> source)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    destination.Add(source[index]);
                }
            }
        }
    }
}
