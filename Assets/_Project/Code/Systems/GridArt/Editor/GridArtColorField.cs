using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    internal static class GridArtColorField
    {
        internal static VisualElement Build(string value, bool inherit, Action<string> changed)
        {
            var values = new List<string> { inherit ? "" : "none" };
            var labels = new List<string> { inherit ? "继承素材" : "无色" };
            foreach (var color in GridArtColorSource.Colors) { values.Add(color.id); labels.Add(color.label); }
            if (inherit) { values.Add("none"); labels.Add("全部无色"); }
            string selected = string.IsNullOrEmpty(value) ? inherit ? "" : "none" : value;
            if (!values.Contains(selected)) { values.Add(selected); labels.Add("其他 · " + selected); }
            var field = new PopupField<string>("解锁颜色归属", labels, values.IndexOf(selected));
            field.tooltip = "仅决定全局颜色解锁时的褪色归属，不赋予交互能力。";
            field.RegisterValueChangedCallback(_ => changed(values[field.index] == "none" && !inherit ? "" : values[field.index]));
            return field;
        }
    }
}
