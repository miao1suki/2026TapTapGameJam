using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    internal sealed class RespawnAnchorGraphNode : Node
    {
        public RespawnAnchorFeature Anchor { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        public RespawnAnchorGraphNode(
            RespawnAnchorFeature anchor)
        {
            Anchor = anchor;
            title = anchor.DisplayLabel;
            style.minWidth = 150f;
            style.height = StyleKeyword.Auto;

            InputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                typeof(bool));
            InputPort.portName = "← 接入";
            OutputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Single,
                typeof(bool));
            OutputPort.portName = "连出 →";
            inputContainer.Add(InputPort);
            outputContainer.Add(OutputPort);

            titleButtonContainer.style.display =
                DisplayStyle.None;
            Refresh();
        }

        public void Refresh()
        {
            title = Anchor.DisplayLabel;
            tooltip = Anchor.IsActivated
                ? "已激活"
                : "未激活";
        }

        public void SetPending(bool value)
        {
            style.backgroundColor = value
                ? new Color(1f, .72f, .2f, .28f)
                : StyleKeyword.Null;
            SetBorderColor(value
                ? new Color(1f, .78f, .25f, 1f)
                : new Color(.25f, .55f, .8f, .9f));
        }

        public void SetFeedback(bool value)
        {
            SetBorderColor(value
                ? new Color(.45f, 1f, .62f, 1f)
                : new Color(.25f, .55f, .8f, .9f));
        }

        private void SetBorderColor(Color color)
        {
            style.borderLeftColor =
                style.borderRightColor =
                style.borderTopColor =
                style.borderBottomColor = color;
        }
    }
}
