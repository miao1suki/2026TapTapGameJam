using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal sealed class MapGridWindow : EditorWindow
    {
        [MenuItem("Tools/2026TapTap/关卡编辑器/地图网格显示")]
        private static void Open()
        {
            MapGridWindow window = GetWindow<MapGridWindow>();
            window.titleContent =
                new GUIContent("地图网格显示");
            window.minSize = new Vector2(180f, 48f);
            window.Show();
            window.Focus();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            Toggle showGrid = new Toggle("显示地图网格")
            {
                value = LevelEditorScenePainter.ShowMapGrid
            };
            showGrid.RegisterValueChangedCallback(evt =>
            {
                LevelEditorScenePainter.ShowMapGrid = evt.newValue;
            });
            rootVisualElement.Add(showGrid);
        }
    }
}
