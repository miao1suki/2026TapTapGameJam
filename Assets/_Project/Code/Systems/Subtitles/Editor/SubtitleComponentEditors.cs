using Project.Editor;
using Project.Subtitles;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Subtitles.Editor
{
    internal static class SubtitleInspectorFields
    {
        public static VisualElement Root(
            SerializedObject serializedObject,
            string title)
        {
            VisualElement root =
                ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(title));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            return root;
        }

        public static void AddStyle(
            VisualElement parent,
            SerializedObject serializedObject,
            string path)
        {
            parent.Add(Property(
                serializedObject,
                path + ".font",
                "字体文件",
                "留空时使用 TMP 默认字体。"));
            parent.Add(Property(
                serializedObject,
                path + ".fontSize",
                "字号"));
            parent.Add(Property(
                serializedObject,
                path + ".color",
                "文字颜色"));
            parent.Add(Property(
                serializedObject,
                path + ".anchor",
                "屏幕位置"));
            parent.Add(Property(
                serializedObject,
                path + ".normalizedX",
                "自定义 X",
                "仅在“自定义位置”时生效。"));
            parent.Add(Property(
                serializedObject,
                path + ".normalizedY",
                "自定义 Y",
                "仅在“自定义位置”时生效。"));
            parent.Add(Property(
                serializedObject,
                path + ".offset",
                "位置偏移"));
            parent.Add(Property(
                serializedObject,
                path + ".holdDuration",
                "停留时间"));
            parent.Add(Property(
                serializedObject,
                path + ".inAnimation",
                "进入动画"));
            parent.Add(Property(
                serializedObject,
                path + ".inDuration",
                "进入时长"));
            parent.Add(Property(
                serializedObject,
                path + ".inCurve",
                "进入曲线"));
            parent.Add(Property(
                serializedObject,
                path + ".outAnimation",
                "退出动画"));
            parent.Add(Property(
                serializedObject,
                path + ".outDuration",
                "退出时长"));
            parent.Add(Property(
                serializedObject,
                path + ".outCurve",
                "退出曲线"));
            parent.Add(Property(
                serializedObject,
                path + ".motionDistance",
                "移动距离"));
            parent.Add(Property(
                serializedObject,
                path + ".scaleFrom",
                "起始缩放"));

            Foldout animationDetails =
                ProjectInspectorUtility.CreateFoldout(
                    "动画细节",
                    false);
            animationDetails.Add(Property(
                serializedObject,
                path + ".startRotation",
                "旋转起始角"));
            animationDetails.Add(Property(
                serializedObject,
                path + ".overshoot",
                "过冲强度"));
            animationDetails.Add(Property(
                serializedObject,
                path + ".floatAmplitude",
                "漂浮振幅"));
            animationDetails.Add(Property(
                serializedObject,
                path + ".floatSpeed",
                "漂浮速度"));
            animationDetails.Add(Property(
                serializedObject,
                path + ".glitchStrength",
                "故障强度"));
            animationDetails.Add(Property(
                serializedObject,
                path + ".charactersPerSecond",
                "打字速度",
                "仅“打字机”动画使用。"));
            parent.Add(animationDetails);
        }

        private static PropertyField Property(
            SerializedObject serializedObject,
            string path,
            string label,
            string tooltip = null)
        {
            PropertyField field =
                ProjectInspectorUtility.CreateProperty(
                    serializedObject,
                    path,
                    label,
                    tooltip);
            field.userData = path;
            return field;
        }
    }

    [CustomEditor(typeof(SubtitleManager))]
    public sealed class SubtitleManagerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root =
                SubtitleInspectorFields.Root(
                    serializedObject,
                    "字幕管理器");
            root.Add(ProjectInspectorUtility.CreateHelp(
                "统一管理中间大字、下方小字、字体、位置、停留时间和进出动画。"));

            Foldout center = ProjectInspectorUtility.CreateFoldout(
                "中间大字默认样式",
                true);
            SubtitleInspectorFields.AddStyle(
                center,
                serializedObject,
                "centerDefault");
            root.Add(center);

            Foldout bottom = ProjectInspectorUtility.CreateFoldout(
                "下方小字默认样式",
                true);
            SubtitleInspectorFields.AddStyle(
                bottom,
                serializedObject,
                "bottomDefault");
            root.Add(bottom);

            Foldout canvas = ProjectInspectorUtility.CreateFoldout(
                "画布设置",
                false);
            canvas.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "canvasSortingOrder",
                "层级顺序"));
            canvas.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "useUnscaledTime",
                "使用非缩放时间",
                "暂停游戏时字幕动画是否继续。"));
            root.Add(canvas);

            Foldout debug = ProjectInspectorUtility.CreateFoldout(
                "Play 测试",
                true);
            var showCenter = new Button(() =>
                    SubtitleManager.Instance.ShowLarge(
                        "中间大字测试"))
            {
                text = "显示中间大字"
            };
            var showBottom = new Button(() =>
                    SubtitleManager.Instance.ShowBottom(
                        "下方小字测试"))
            {
                text = "显示下方小字"
            };
            var hideAll = new Button(() =>
                    SubtitleManager.Instance.HideAll())
            {
                text = "隐藏全部字幕"
            };
            debug.Add(showCenter);
            debug.Add(showBottom);
            debug.Add(hideAll);
            debug.Add(ProjectInspectorUtility.CreateHelp(
                "测试按钮只在 Play Mode 中可用。"));
            root.Add(debug);

            root.schedule.Execute(() =>
            {
                bool playing = Application.isPlaying;
                showCenter.SetEnabled(playing);
                showBottom.SetEnabled(playing);
                hideAll.SetEnabled(playing);
            }).Every(100);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(SubtitleTrigger))]
    public sealed class SubtitleTriggerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root =
                SubtitleInspectorFields.Root(
                    serializedObject,
                    "字幕触发道具");

            Foldout content = ProjectInspectorUtility.CreateFoldout(
                "字幕内容",
                true);
            content.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "cue.layer",
                "显示层级"));
            content.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "cue.text",
                "字幕文字"));
            content.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "cue.useManagerDefaults",
                "使用管理器默认样式"));
            root.Add(content);

            Foldout style = ProjectInspectorUtility.CreateFoldout(
                "字幕样式",
                true);
            SubtitleInspectorFields.AddStyle(
                style,
                serializedObject,
                "cue.style");
            root.Add(style);
            style.schedule.Execute(() =>
            {
                SerializedProperty useDefaults =
                    serializedObject.FindProperty(
                        "cue.useManagerDefaults");
                style.style.display = useDefaults != null &&
                                      useDefaults.boolValue
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }).Every(100);

            Foldout trigger = ProjectInspectorUtility.CreateFoldout(
                "触发设置",
                true);
            SerializedProperty playOnce =
                serializedObject.FindProperty("playOnce");
            Toggle allowRepeat = new Toggle("允许重复激发")
            {
                value = playOnce != null && !playOnce.boolValue,
                tooltip =
                    "开启后，玩家每次重新进入触发区域都会再次显示字幕。"
            };
            allowRepeat.RegisterValueChangedCallback(evt =>
            {
                if (playOnce == null)
                {
                    return;
                }

                playOnce.boolValue = !evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            trigger.Add(allowRepeat);
            trigger.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "hideVisualOnAwake",
                "进入游戏时隐藏物体外观",
                "默认关闭所有 Renderer，但保留 Collider 用于触发。"));
            trigger.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "destroyAfterPlay",
                "播放后销毁物体"));
            trigger.Add(ProjectInspectorUtility.CreateHelp(
                "“允许重复激发”关闭时，该触发物只会显示一次。" +
                "开启后可在玩家重新进入触发区域时再次显示。"));
            root.Add(trigger);

            allowRepeat.schedule.Execute(() =>
            {
                if (playOnce != null)
                {
                    allowRepeat.SetValueWithoutNotify(
                        !playOnce.boolValue);
                }
            }).Every(100);

            var preview = new Button(() =>
            {
                ((SubtitleTrigger)target).ShowDebug();
            })
            {
                text = "预览字幕"
            };
            preview.schedule.Execute(() =>
                preview.SetEnabled(Application.isPlaying)).Every(100);
            root.Add(preview);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(DisplayBlockFeature))]
    public sealed class DisplayBlockFeatureEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root =
                SubtitleInspectorFields.Root(
                    serializedObject,
                    "显示方块");
            root.Add(ProjectInspectorUtility.CreateHelp(
                "进入游戏后隐藏自身碰撞体与外观，并按世界空间显示文字。" +
                "文字位置以方块本地包围盒为基准。"));

            Foldout content = ProjectInspectorUtility.CreateFoldout(
                "文字内容",
                true);
            content.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "text",
                "显示文字"));
            content.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "font",
                "字体文件",
                "留空时使用 TMP 默认字体。"));
            content.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "fontSizeRatio",
                "字号比例（相对方块高度）",
                "0.5 表示文字行高约占方块高度的 50%。"));
            content.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "color",
                "文字颜色"));
            root.Add(content);

            Foldout placement = ProjectInspectorUtility.CreateFoldout(
                "位置与图层",
                true);
            placement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "anchor",
                "相对方块位置"));
            placement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "textAlignment",
                "文字对齐"));
            placement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "offset",
                "位置偏移"));
            placement.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "layerOrder",
                "玩家图层关系",
                "以玩家 Tag 找到玩家的 Renderer 后，将文字排在其上方或下方。"));
            SerializedProperty playerTag =
                serializedObject.FindProperty("playerTag");
            TagField playerTagField = new TagField("玩家标签")
            {
                value = playerTag != null
                    ? playerTag.stringValue
                    : string.Empty,
                tooltip = "使用 Unity Tag 选择玩家对象。"
            };
            playerTagField.RegisterValueChangedCallback(evt =>
            {
                if (playerTag == null)
                {
                    return;
                }

                playerTag.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            placement.Add(playerTagField);
            root.Add(placement);

            Foldout runtime = ProjectInspectorUtility.CreateFoldout(
                "运行设置",
                true);
            runtime.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "showOnAwake",
                "进入游戏时显示"));
            runtime.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "hideColliderOnAwake",
                "隐藏碰撞体积"));
            runtime.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "hideVisualOnAwake",
                "隐藏方块外观"));
            root.Add(runtime);

            DisplayBlockFeature displayBlock =
                (DisplayBlockFeature)target;
            Foldout debug = ProjectInspectorUtility.CreateFoldout(
                "Play 测试",
                true);
            Button show = new Button(displayBlock.Show)
            {
                text = "显示文字"
            };
            Button hide = new Button(displayBlock.Hide)
            {
                text = "隐藏文字"
            };
            debug.Add(show);
            debug.Add(hide);
            root.Add(debug);
            debug.schedule.Execute(() =>
            {
                bool playing = Application.isPlaying;
                show.SetEnabled(playing);
                hide.SetEnabled(playing);
            }).Every(100);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
