using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    // Shared by the regular Inspector and the planning override preview.
    internal static class PlatformParameterUI
    {
        internal static bool Applies(Component component) => component is MovingPlatformFeature || component is CrankPlatformFeature;

        internal static HashSet<string> Build(VisualElement parent, SerializedObject so, Action<SerializedProperty> changed = null)
        {
            var used = new HashSet<string>();
            var core = new Foldout { text = "核心移动参数", value = true, name = "platform-core" };
            parent.Add(core);
            bool crank = so.targetObject is CrankPlatformFeature;
            if (crank)
            {
                Field("progressPerSecond", "转动速度（进度/秒）");
                Field("reboundWhenIdle", "无人操作时回弹");
                Field("reboundProgressPerSecond", "回弹速度（进度/秒）", () => so.FindProperty("reboundWhenIdle").boolValue);
                core.Add(new HelpBox("移动方向和距离在各升降平台上设置。平台速度 = 到目标端点的距离 × 曲柄每秒进度；载人下降另受下降倍率影响。", HelpBoxMessageType.Info));
            }
            else
            {
                Field("driveMode", "驱动方式");
                Field("pathMode", "路线模式");
                Field("route", "移动方向", () => Mode() == 0);
                Field("reverseRoute", "反向（向左 / 向下）", () => Mode() == 0);
                Field("travelBlocks", "移动距离（格）", () => Mode() == 0);
                Field("leftDistance", "向左距离（格）", () => Mode() == 2 || Mode() == 5);
                Field("rightDistance", "向右距离（格）", () => Mode() == 1 || Mode() == 5);
                Field("downDistance", "向下距离（格）", () => Mode() == 4 || Mode() == 6);
                Field("upDistance", "向上距离（格）", () => Mode() == 3 || Mode() == 6);
                Field("centerOffset", "中心偏移（格）", () => Mode() == 5 || Mode() == 6);
                Field("startNegative", "首次向左 / 向下", () => Mode() == 5 || Mode() == 6);
                Field("signalMotion", "充能移动方式", () => Drive() == 1);
                var speed = new FloatField("平均速度（格/秒）") { name = "platform-speed", isDelayed = true };
                speed.tooltip = "按路线全程距离 ÷ 单程时间换算；修改速度会换算单程时间。缓动会影响瞬时速度，端点停留不计入。";
                core.Add(speed);
                Field("travelDuration", "单程时间（秒）");
                var note = new HelpBox("曲柄联动时，速度由所绑定曲柄的转动/回弹进度控制。平均速度与单程时间仅在自动/信号驱动时生效，切换驱动方式后可编辑。", HelpBoxMessageType.Info);
                core.Add(note);
                void RefreshSpeed()
                {
                    bool linked = Drive() == 2;
                    core.Q("platform-travelDuration").SetEnabled(!linked);
                    note.style.display = linked ? DisplayStyle.Flex : DisplayStyle.None;
                    float span = ((MovingPlatformFeature)so.targetObject).Path.Span;
                    speed.SetValueWithoutNotify(span / Mathf.Max(.05f, so.FindProperty("travelDuration").floatValue));
                    speed.SetEnabled(!linked && span > .00001f);
                }
                speed.RegisterValueChangedCallback(evt =>
                {
                    if (Drive() == 2) { RefreshSpeed(); return; }
                    if (float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue) || evt.newValue <= 0f) { RefreshSpeed(); return; }
                    var duration = so.FindProperty("travelDuration");
                    duration.floatValue = Mathf.Max(.05f, ((MovingPlatformFeature)so.targetObject).Path.Span / evt.newValue);
                    so.ApplyModifiedProperties();
                    changed?.Invoke(duration);
                    RefreshSpeed();
                });
                core.TrackSerializedObjectValue(so, _ => RefreshSpeed());
                RefreshSpeed();
            }
            return used;

            int Mode() => so.FindProperty("pathMode").intValue;
            int Drive() => so.FindProperty("driveMode").intValue;
            void Field(string key, string label, Func<bool> visible = null)
            {
                var property = so.FindProperty(key);
                if (property == null) return;
                used.Add(key);
                var field = new PropertyField(property.Copy(), label) { name = "platform-" + key };
                field.BindProperty(property);
                field.RegisterCallback<SerializedPropertyChangeEvent>(_ => { so.ApplyModifiedProperties(); changed?.Invoke(property); });
                if (visible != null)
                {
                    void UpdateVisibility() => field.style.display = visible() ? DisplayStyle.Flex : DisplayStyle.None;
                    UpdateVisibility();
                    field.TrackSerializedObjectValue(so, _ => UpdateVisibility());
                }
                core.Add(field);
            }
        }
    }
}
