using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    [CustomEditor(typeof(PlayerInteractionSensor))]
    public sealed class PlayerInteractionSensorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.Add(PlayerInspectorFields.CreateScriptField(
                serializedObject));

            Foldout scan = PlayerInspectorFields.CreateFoldout(
                "交互扫描");
            scan.Add(PlayerInspectorFields.Create(
                serializedObject,
                "scanRadius",
                "扫描半径",
                "以玩家位置搜索可交互对象的最大半径。"));
            scan.Add(PlayerInspectorFields.Create(
                serializedObject,
                "interactionMask",
                "交互层",
                "参与交互扫描的物理层。"));
            root.Add(scan);

            root.Bind(serializedObject);
            return root;
        }
    }
}
