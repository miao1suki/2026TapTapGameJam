using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.InputAbstraction
{
    /// <summary>Pixel artwork only; hit rectangles and input ownership remain on the parent.</summary>
    public sealed class MobileButtonArtwork : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private Image face;
        private string key;
        private bool pressed;
        public static bool Apply(GameObject target, string label)
        {
            string name = Key(label);
            Sprite sprite = Load(name, false);
            if (sprite == null) return false;
            var art = target.AddComponent<MobileButtonArtwork>();
            art.key = name;
            var obj = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(target.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            art.face = obj.GetComponent<Image>();
            art.face.sprite = sprite; art.face.preserveAspect = true; art.face.raycastTarget = false;
            target.GetComponent<Image>().color = Color.clear;
            var button = target.GetComponent<Button>();
            button.targetGraphic = art.face;
            button.transition = Selectable.Transition.None;
            return true;
        }
        public void SetPressed(bool value)
        {
            pressed = value;
            if (face != null) { face.sprite = Load(key, value) ?? Load(key, false); face.color = Color.white; }
        }
        public void SetRunning(bool running)
        {
            string next = running ? "Run" : "Walk";
            if (key == next) return;
            key = next;
            SetPressed(pressed);
        }
        private static Sprite Load(string name, bool down)
        {
            string path = "MobileControls/" + name + (down ? "_Pressed" : "_Normal");
            if (!Sprites.TryGetValue(path, out var sprite))
            { sprite = Resources.Load<Sprite>(path); Sprites[path] = sprite; }
            return sprite;
        }
        private static string Key(string label) => label switch
        {
            "上" => "Up", "下" => "Down", "左" or "右手左" => "Left",
            "右" or "右手右" => "Right", "跳跃" or "左跳跃" => "Jump",
            "跑" => "Walk", "交互" => "Interact", "放左" => "DropLeft",
            "放右" => "DropRight", "选色" => "Color", "重生" => "Respawn",
            "暂停" => "Pause", _ => label
        };
    }
}
