using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.ColorBlocks.Editor
{
    internal sealed class ColorInteractionNode : Node
    {
        internal readonly string NodeId;
        internal readonly TextField TitleField;
        internal readonly Port Input;
        internal readonly Port Output;

        internal ColorInteractionNode(ColorGraphNode data, Action changed)
        {
            NodeId = data.id;
            title = "交互节点";
            viewDataKey = data.id;
            SetPosition(new Rect(data.position, new Vector2(170, 110)));
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = "输入";
            inputContainer.Add(Input);
            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Output.portName = "输出";
            outputContainer.Add(Output);
            TitleField = new TextField("名称") { value = data.title };
            TitleField.RegisterValueChangedCallback(_ => changed());
            extensionContainer.Add(TitleField);
            RefreshExpandedState();
            RefreshPorts();
        }
    }

    internal sealed class ColorInteractionGraphView : GraphView
    {
        private readonly Dictionary<string, ColorInteractionNode> nodeById =
            new Dictionary<string, ColorInteractionNode>(StringComparer.Ordinal);
        private ColorTypeDefinition definition;
        private Action changed;
        private bool loading;

        internal ColorInteractionGraphView()
        {
            style.flexGrow = 1;
            Insert(0, new GridBackground());
            SetupZoom(0.25f, 2f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
            graphViewChanged += OnGraphChanged;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            return ports.ToList().Where(port => port != startPort &&
                port.node != startPort.node && port.direction != startPort.direction).ToList();
        }

        internal void Load(ColorTypeDefinition color, Action onChanged)
        {
            loading = true;
            definition = color;
            changed = onChanged;
            DeleteElements(graphElements.ToList());
            nodeById.Clear();
            if (color != null)
            {
                foreach (var data in color.nodes)
                {
                    if (data == null || string.IsNullOrEmpty(data.id) || nodeById.ContainsKey(data.id)) continue;
                    var node = new ColorInteractionNode(data, ScheduleSave);
                    nodeById.Add(data.id, node);
                    AddElement(node);
                }
                foreach (var data in color.edges)
                {
                    if (!nodeById.TryGetValue(data.fromId, out var from) ||
                        !nodeById.TryGetValue(data.toId, out var to)) continue;
                    var edge = from.Output.ConnectTo(to.Input);
                    AddElement(edge);
                }
            }
            loading = false;
            if (color != null) schedule.Execute(() => FrameAll()).ExecuteLater(50);
        }

        internal void AddInteractionNode()
        {
            if (definition == null) return;
            var data = new ColorGraphNode
            {
                title = "新交互",
                position = new Vector2(35 + nodeById.Count * 35, 280 + nodeById.Count * 45)
            };
            var node = new ColorInteractionNode(data, ScheduleSave);
            nodeById.Add(data.id, node);
            AddElement(node);
            ScheduleSave();
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change)
        {
            if (!loading) ScheduleSave();
            return change;
        }

        private void ScheduleSave()
        {
            if (!loading) schedule.Execute(() => changed?.Invoke()).ExecuteLater(1);
        }

        internal void CopyTo(ColorTypeDefinition color)
        {
            color.nodes.Clear();
            color.edges.Clear();
            foreach (var node in graphElements.OfType<ColorInteractionNode>())
                color.nodes.Add(new ColorGraphNode
                {
                    id = node.NodeId,
                    title = node.TitleField.value,
                    position = node.GetPosition().position
                });
            foreach (var edge in graphElements.OfType<Edge>())
                if (edge.output?.node is ColorInteractionNode from &&
                    edge.input?.node is ColorInteractionNode to)
                    color.edges.Add(new ColorGraphEdge { fromId = from.NodeId, toId = to.NodeId });
        }
    }
}
