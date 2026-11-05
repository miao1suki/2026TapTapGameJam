using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(TimelineCamRig))]
public class TimelineCamRigEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new Label("2D 演出镜头"));
        root.Add(new PropertyField(serializedObject.FindProperty("cameraManager"), "相机管理器"));
        root.Add(new PropertyField(serializedObject.FindProperty("cameraModeController"), "2D 相机权威"));
        root.Add(new PropertyField(serializedObject.FindProperty("controlPriority"), "控制优先级"));
        root.Add(new PropertyField(serializedObject.FindProperty("returnTransitionDuration"), "交还过渡时间"));
        root.Add(new PropertyField(serializedObject.FindProperty("target"), "跟随目标"));
        root.Add(new PropertyField(serializedObject.FindProperty("horizontalOffset"), "水平偏移"));
        root.Add(new PropertyField(serializedObject.FindProperty("height"), "垂直偏移"));
        root.Add(new PropertyField(serializedObject.FindProperty("viewSize"), "正交半高"));
        return root;
    }
}
