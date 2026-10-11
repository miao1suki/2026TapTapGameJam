using UnityEngine;

namespace Project.Mechanisms
{
    [CreateAssetMenu(menuName = "2026TapTap/机关/门提示素材", fileName = "DoorHintStyle")]
    public sealed class DoorHintStyle : ScriptableObject
    {
        [SerializeField, InspectorName("1 按钮：按下 0、1")] private Sprite[] oneButton = new Sprite[2];
        [SerializeField, InspectorName("2 按钮：按下 0、1、2")] private Sprite[] twoButtons = new Sprite[3];
        [SerializeField, InspectorName("3 按钮：按下 0、1、2、3")] private Sprite[] threeButtons = new Sprite[4];
        public Sprite Resolve(int total, int pressed)
        {
            var values = total == 1 ? oneButton : total == 2 ? twoButtons : total == 3 ? threeButtons : null;
            return values != null && pressed >= 0 && pressed < values.Length ? values[pressed] : null;
        }
    }
}
