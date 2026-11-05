#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(OrbitCameraControl))]
public class OrbitCameraControlEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new Label("平面镜头拖动缩放"));
        root.Add(new PropertyField(serializedObject.FindProperty("requireMouseButton"), "按住右键拖动"));
        root.Add(new PropertyField(serializedObject.FindProperty("lookSpeedX"), "水平拖动速度"));
        root.Add(new PropertyField(serializedObject.FindProperty("lookSpeedY"), "垂直拖动速度"));
        root.Add(new PropertyField(serializedObject.FindProperty("invertY"), "反转垂直方向"));
        root.Add(new PropertyField(serializedObject.FindProperty("scrollSpeed"), "滚轮缩放速度"));
        root.Add(new PropertyField(serializedObject.FindProperty("viewSizeRange"), "正交半高范围"));
        return root;
    }
}
#endif
