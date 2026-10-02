using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Achievements.Editor
{
    internal abstract class AchievementGraphNode : Node
    {
        public string NodeId { get; protected set; }

        public abstract Port InputPort { get; }
        public abstract Port OutputPort { get; }
        public event Action LogicTypeChanged;

        protected AchievementGraphNode(
            string nodeId,
            string title,
            Vector2 position)
        {
            NodeId = nodeId;
            this.title = title;
            expanded = true;
            SetPosition(
                new Rect(
                    position.x,
                    position.y,
                    190f,
                    120f));
        }

        protected void NotifyLogicTypeChanged()
        {
            LogicTypeChanged?.Invoke();
        }
    }

    internal sealed class AchievementConditionGraphNode :
        AchievementGraphNode
    {
        private readonly Port outputPort;
        private readonly Label modeLabel;
        private readonly Label textLabel;

        public AchievementConditionMode Mode { get; private set; }
        public int TargetCount { get; private set; }
        public float TargetProgress { get; private set; }
        public string TextPrefix { get; private set; }
        public string TextSuffix { get; private set; }

        public override Port InputPort => null;
        public override Port OutputPort => outputPort;

        public void SetConditionSettings(
            string prefix,
            string suffix,
            AchievementConditionMode mode,
            int count,
            float progress)
        {
            Mode = mode;
            TargetCount = Mathf.Max(1, count);
            TargetProgress = Mathf.Clamp(progress, 0f, 100f);
            TextPrefix = prefix ?? string.Empty;
            TextSuffix = suffix ?? string.Empty;
            modeLabel.text =
                "类型：" + ModeLabel(Mode);
            textLabel.text =
                "预览：" + BuildConditionSummary(
                    Mode,
                    TargetCount,
                    TargetProgress,
                    TextPrefix,
                    TextSuffix);
            title =
                NodeId +
                " · " +
                BuildConditionSummary(
                    Mode,
                    TargetCount,
                    TargetProgress,
                    TextPrefix,
                    TextSuffix);
        }

        private static string ModeLabel(
            AchievementConditionMode mode)
        {
            switch (mode)
            {
                case AchievementConditionMode.Count:
                    return "累计数量";
                case AchievementConditionMode.Progress:
                    return "完成百分比";
                default:
                    return "触发一次";
            }
        }

        private static string BuildConditionSummary(
            AchievementConditionMode mode,
            int targetCount,
            float targetProgress,
            string prefix,
            string suffix)
        {
            switch (mode)
            {
                case AchievementConditionMode.Count:
                    return prefix +
                           " 0/" +
                           Mathf.Max(1, targetCount) +
                           suffix;
                case AchievementConditionMode.Progress:
                    return prefix +
                           " 0/" +
                           Mathf.Clamp(
                                   targetProgress,
                                   0f,
                                   100f)
                               .ToString("0.#") +
                           "%" +
                           suffix;
                default:
                    return prefix + suffix;
            }
        }

        public AchievementConditionGraphNode(
            string conditionId,
            Vector2 position,
            AchievementConditionMode mode,
            int targetCount,
            float targetProgress,
            string textPrefix,
            string textSuffix)
            : base(
                conditionId,
                conditionId +
                " · " +
                BuildConditionSummary(
                    mode,
                    targetCount,
                    targetProgress,
                    textPrefix,
                    textSuffix),
                position)
        {
            Mode = mode;
            TargetCount = targetCount;
            TargetProgress = targetProgress;
            TextPrefix = textPrefix;
            TextSuffix = textSuffix;
            style.minWidth = 250f;
            style.height = StyleKeyword.Auto;

            outputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(bool));
            outputPort.portName = "结果";

            modeLabel = new Label(
                "达成方式：" + ModeLabel(mode));
            textLabel = new Label(
                "预览：" + BuildConditionSummary(
                    mode,
                    targetCount,
                    targetProgress,
                    textPrefix,
                    textSuffix));
            textLabel.style.whiteSpace =
                WhiteSpace.Normal;
            textLabel.style.flexGrow = 1f;

            VisualElement summaryContainer =
                new VisualElement();
            summaryContainer.style.flexDirection =
                FlexDirection.Column;
            summaryContainer.style.flexGrow = 1f;
            summaryContainer.style.marginRight = 6f;
            summaryContainer.Add(modeLabel);
            summaryContainer.Add(textLabel);

            outputContainer.style.flexDirection =
                FlexDirection.Row;
            outputContainer.style.alignItems =
                Align.Center;
            outputContainer.Add(summaryContainer);
            outputContainer.Add(outputPort);

            titleButtonContainer.style.display =
                DisplayStyle.None;
            extensionContainer.style.display =
                DisplayStyle.None;
        }
    }

    internal sealed class AchievementLogicGraphNode :
        AchievementGraphNode
    {
        private readonly Port inputPort;
        private readonly Port outputPort;
        private readonly PopupField<string> logicTypeField;
        private readonly Label logicHint;
        private readonly Label sourceLabel;

        public AchievementLogicType LogicType =>
            (AchievementLogicType)
            Mathf.Clamp(
                logicTypeField.choices.IndexOf(
                    logicTypeField.value),
                0,
                3);

        public void SetLogicType(
            AchievementLogicType value)
        {
            logicTypeField.SetValueWithoutNotify(
                LogicLabel(value));
            logicHint.text = LogicHint(value);
            title = NodeId + " · " + LogicLabel(value);
            NotifyLogicTypeChanged();
        }

        public void SetSourceSummary(
            string sourceSummary)
        {
            sourceLabel.text =
                string.IsNullOrWhiteSpace(sourceSummary)
                    ? "接入：无"
                    : "接入：" + sourceSummary;
        }

        public override Port InputPort => inputPort;
        public override Port OutputPort => outputPort;

        public AchievementLogicGraphNode(
            string nodeId,
            Vector2 position,
            AchievementLogicType logicType)
            : base(
                nodeId,
                nodeId + " · " + LogicLabel(logicType),
                position)
        {
            inputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(bool));
            inputPort.portName = "← 接入条件";
            outputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(bool));
            outputPort.portName = "组合结果 →";
            inputContainer.Add(inputPort);
            outputContainer.Add(outputPort);

            logicTypeField =
                new PopupField<string>(
                    "条件组合",
                    new System.Collections.Generic.List<string>
                    {
                        "全部达成",
                        "任意一个达成",
                        "只能有一个达成",
                        "必须未发生"
                    },
                    (int)logicType);
            logicTypeField.tooltip =
                "全部达成：连接进来的条件都必须完成。\n" +
                "任意一个达成：只要完成其中一个就可以。\n" +
                "只能有一个达成：这些条件里只能完成一个。\n" +
                "必须未发生：连接进来的条件必须没有发生。";
            logicTypeField.RegisterValueChangedCallback(evt =>
            {
                int index =
                    logicTypeField.choices.IndexOf(
                        evt.newValue);
                SetLogicType(
                    (AchievementLogicType)
                    Mathf.Clamp(index, 0, 3));
            });
            extensionContainer.Add(logicTypeField);
            logicHint = new Label(
                LogicHint(logicType));
            logicHint.style.whiteSpace =
                WhiteSpace.Normal;
            extensionContainer.Add(logicHint);
            sourceLabel = new Label(
                "接入：无");
            sourceLabel.style.whiteSpace =
                WhiteSpace.NoWrap;
            sourceLabel.style.fontSize = 10f;
            sourceLabel.style.opacity = 0.75f;
            extensionContainer.Add(sourceLabel);
        }

        private static string LogicLabel(
            AchievementLogicType logicType)
        {
            switch (logicType)
            {
                case AchievementLogicType.And:
                    return "全部达成";
                case AchievementLogicType.Or:
                    return "任意一个达成";
                case AchievementLogicType.Xor:
                    return "只能有一个达成";
                case AchievementLogicType.Not:
                    return "必须未发生";
                default:
                    return "条件关系";
            }
        }

        private static string LogicHint(
            AchievementLogicType logicType)
        {
            switch (logicType)
            {
                case AchievementLogicType.And:
                    return "左边接进来的条件都要达成";
                case AchievementLogicType.Or:
                    return "左边任意一个条件达成就行";
                case AchievementLogicType.Xor:
                    return "左边只能有一个条件达成";
                case AchievementLogicType.Not:
                    return "左边这个条件必须没有发生";
                default:
                    return "选择左边条件的计算方式";
            }
        }

    }

    internal sealed class AchievementFailureGraphNode :
        AchievementGraphNode
    {
        private readonly Port inputPort;
        private readonly Label sourceLabel;

        public override Port InputPort => inputPort;
        public override Port OutputPort => null;

        public void SetSourceSummary(
            string sourceSummary)
        {
            sourceLabel.text =
                string.IsNullOrWhiteSpace(sourceSummary)
                    ? "接入：无"
                    : "接入：" + sourceSummary;
        }

        public AchievementFailureGraphNode(
            string nodeId,
            Vector2 position)
            : base(
                nodeId,
                "失败条件",
                position)
        {
            inputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                typeof(bool));
            inputPort.portName = "← 条件";
            inputContainer.Add(inputPort);

            Label hint = new Label(
                "这个条件一旦达成，成就永久作废");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.fontSize = 10f;
            hint.style.opacity = 0.75f;
            extensionContainer.Add(hint);

            sourceLabel = new Label("接入：无");
            sourceLabel.style.whiteSpace =
                WhiteSpace.NoWrap;
            sourceLabel.style.fontSize = 10f;
            sourceLabel.style.opacity = 0.75f;
            extensionContainer.Add(sourceLabel);
        }
    }

    internal sealed class AchievementRootGraphNode :
        AchievementGraphNode
    {
        private readonly Port inputPort;
        private readonly Label sourceLabel;

        public override Port InputPort => inputPort;
        public override Port OutputPort => null;

        public void SetSourceSummary(
            string sourceSummary)
        {
            sourceLabel.text =
                string.IsNullOrWhiteSpace(sourceSummary)
                    ? "最终接入：无"
                    : "最终接入：" + sourceSummary;
        }

        public AchievementRootGraphNode(
            Vector2 position)
            : base(
                "__ROOT__",
                "完成条件",
                position)
        {
            inputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                typeof(bool));
            inputPort.portName = "← 最终结果";
            inputContainer.Add(inputPort);
            sourceLabel = new Label(
                "最终接入：无");
            sourceLabel.style.whiteSpace =
                WhiteSpace.NoWrap;
            sourceLabel.style.fontSize = 10f;
            sourceLabel.style.opacity = 0.75f;
            extensionContainer.Add(sourceLabel);
        }
    }
}
