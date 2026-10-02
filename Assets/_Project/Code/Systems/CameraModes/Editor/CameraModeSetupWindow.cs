using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.CameraModes.Editor
{
    public sealed class CameraModeSetupWindow : EditorWindow
    {
        [MenuItem("Tools/2026TapTap/2D-3D 相机配置")]
        public static void OpenWindow()
        {
            CameraModeSetupWindow window = GetWindow<CameraModeSetupWindow>();
            window.titleContent = new GUIContent("2D-3D 相机");
            window.minSize = new Vector2(330f, 430f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.Add(new CameraModePanel());
        }
    }
}
