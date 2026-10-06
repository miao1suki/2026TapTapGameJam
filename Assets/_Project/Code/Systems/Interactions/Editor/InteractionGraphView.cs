using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Interactions.Editor
{
    internal static class InteractionNodeLibrary
    {
        internal static readonly InteractionNodeKind[] Templates =
        {
            InteractionNodeKind.PlayerEntered,
            InteractionNodeKind.PlayerLeft,
            InteractionNodeKind.ObjectTouched,
            InteractionNodeKind.ObjectStay,
            InteractionNodeKind.TimeElapsed,
            InteractionNodeKind.Manual,
            InteractionNodeKind.RequireSelf,
            InteractionNodeKind.RequirePlayer,
            InteractionNodeKind.RequireOtherObject,
            InteractionNodeKind.RequireColor,
            InteractionNodeKind.RequireObjectId,
            InteractionNodeKind.RequireOtherColor,
            InteractionNodeKind.RequirePlayerColor,
            InteractionNodeKind.Delay,
            InteractionNodeKind.Fade,
            InteractionNodeKind.Restore,
            InteractionNodeKind.PlayTimeline,
            InteractionNodeKind.InvokeMethod,
            InteractionNodeKind.SetColor,
            InteractionNodeKind.Log
        };

        internal static bool IsTrigger(InteractionNodeKind kind) =>
            kind >= InteractionNodeKind.PlayerEntered &&
            kind <= InteractionNodeKind.Manual;

        internal static string Label(InteractionNodeKind kind)
        {
            switch (kind)
            {
                case InteractionNodeKind.PlayerEntered: return "触发 / 玩家进入";
                case InteractionNodeKind.PlayerLeft: return "触发 / 玩家离开";
                case InteractionNodeKind.ObjectTouched: return "触发 / 物体触碰";
                case InteractionNodeKind.ObjectStay: return "触发 / 物体停留";
                case InteractionNodeKind.TimeElapsed: return "触发 / 经过时间";
                case InteractionNodeKind.Manual: return "触发 / 手动执行";
                case InteractionNodeKind.RequireSelf: return "条件 / 本物体";
                case InteractionNodeKind.RequirePlayer: return "条件 / 需要玩家";
                case InteractionNodeKind.RequireOtherObject: return "条件 / 需要其他物体";
                case InteractionNodeKind.RequireColor: return "条件 / 受到颜色";
                case InteractionNodeKind.RequireObjectId: return "条件 / 受到物体";
                case InteractionNodeKind.RequireOtherColor: return "条件 / 受到其他物体颜色";
                case InteractionNodeKind.RequirePlayerColor: return "条件 / 玩家选中颜色";
                case InteractionNodeKind.Delay: return "调度 / 延迟执行";
                case InteractionNodeKind.Fade: return "表现 / 褪色";
                case InteractionNodeKind.Restore: return "表现 / 恢复";
                case InteractionNodeKind.PlayTimeline: return "表现 / 播放 Timeline";
                case InteractionNodeKind.InvokeMethod: return "执行 / 调用方法";
                case InteractionNodeKind.SetColor: return "属性 / 设置颜色";
                case InteractionNodeKind.Log: return "调试 / 输出日志";
                default: return "无效节点";
            }
        }

        internal static bool UsesNumber(InteractionNodeKind kind) =>
            kind == InteractionNodeKind.TimeElapsed ||
            kind == InteractionNodeKind.Delay ||
            kind == InteractionNodeKind.Fade ||
            kind == InteractionNodeKind.Restore;

        internal static bool UsesValue(InteractionNodeKind kind) =>
            kind == InteractionNodeKind.RequireColor ||
            kind == InteractionNodeKind.RequireObjectId ||
            kind == InteractionNodeKind.RequireOtherColor ||
            kind == InteractionNodeKind.RequirePlayerColor ||
            kind == InteractionNodeKind.Fade ||
            kind == InteractionNodeKind.Restore ||
            kind == InteractionNodeKind.InvokeMethod ||
            kind == InteractionNodeKind.SetColor ||
            kind == InteractionNodeKind.Log;

        internal static bool UsesReference(InteractionNodeKind kind) =>
            kind == InteractionNodeKind.PlayTimeline;
    }

    internal sealed class InteractionGraphNodeView : Node
    {
        internal readonly string NodeId;
        internal readonly InteractionNodeKind Kind;
        internal readonly Port Input;
        internal readonly Port Output;
        internal readonly TextField ValueField;
        internal readonly FloatField NumberField;
        internal readonly TextField ArgumentField;
        internal readonly UnityEditor.UIElements.ObjectField ReferenceField;

        internal InteractionGraphNodeView(
            InteractionGraphNode data,
            Action changed)
        {
            NodeId = data.id;
            Kind = data.kind;
            title = InteractionNodeLibrary.Label(data.kind);
            viewDataKey = data.id;
            capabilities |= Capabilities.Deletable;
            SetPosition(new Rect(data.position, new Vector2(250f, 180f)));

            if (!InteractionNodeLibrary.IsTrigger(data.kind))
            {
                Input = InstantiatePort(
                    Orientation.Horizontal,
                    Direction.Input,
                    Port.Capacity.Multi,
                    typeof(bool));
                Input.portName = "流程";
                inputContainer.Add(Input);
            }

            Output = InstantiatePort(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(bool));
            Output.portName = "下一步";
            outputContainer.Add(Output);

            if (InteractionNodeLibrary.UsesValue(data.kind))
            {
                ValueField = new TextField("值") { value = data.value ?? string.Empty };
                ValueField.RegisterValueChangedCallback(_ => changed());
                extensionContainer.Add(ValueField);
            }

            if (InteractionNodeLibrary.UsesNumber(data.kind))
            {
                NumberField = new FloatField("秒数") { value = data.number };
                NumberField.RegisterValueChangedCallback(_ => changed());
                extensionContainer.Add(NumberField);
            }

            if (data.kind == InteractionNodeKind.InvokeMethod)
            {
                ArgumentField = new TextField("字符串参数")
                {
                    value = data.argument ?? string.Empty
                };
                ArgumentField.RegisterValueChangedCallback(_ => changed());
                extensionContainer.Add(ArgumentField);
            }

            if (InteractionNodeLibrary.UsesReference(data.kind))
            {
                ReferenceField = new UnityEditor.UIElements.ObjectField("Timeline 资源")
                {
                    objectType = typeof(UnityEngine.Playables.PlayableAsset),
                    allowSceneObjects = true,
                    value = data.reference
                };
                ReferenceField.RegisterValueChangedCallback(_ => changed());
                extensionContainer.Add(ReferenceField);
            }

            RefreshExpandedState();
            RefreshPorts();
        }
    }

    internal sealed class InteractionGraphView : GraphView
    {
        private readonly Dictionary<string, InteractionGraphNodeView> nodeById =
            new Dictionary<string, InteractionGraphNodeView>(StringComparer.Ordinal);
        private InteractionObjectDefinition definition;
        private Action changed;
        private bool loading;

        internal InteractionGraphView()
        {
            style.flexGrow = 1f;
            Insert(0, new GridBackground());
            SetupZoom(.25f, 2f);
            VisualElementExtensions.AddManipulator(this, new ContentDragger());
            VisualElementExtensions.AddManipulator(this, new SelectionDragger());
            VisualElementExtensions.AddManipulator(this, new RectangleSelector());
            VisualElementExtensions.AddManipulator(this, new ContentZoomer());
            graphViewChanged += OnGraphChanged;
        }

        public override List<Port> GetCompatiblePorts(
            Port startPort,
            NodeAdapter adapter)
        {
            return ports.ToList().Where(port =>
                port != startPort &&
                port.node != startPort.node &&
                port.direction != startPort.direction &&
                (port.direction != Direction.Input ||
                 !(port.node is InteractionGraphNodeView target) ||
                 !InteractionNodeLibrary.IsTrigger(target.Kind))).ToList();
        }

        internal void Load(
            InteractionObjectDefinition value,
            Action onChanged)
        {
            loading = true;
            definition = value;
            changed = onChanged;
            DeleteElements(graphElements.ToList());
            nodeById.Clear();
            if (definition != null)
            {
                for (int index = 0; index < definition.Nodes.Count; index++)
                {
                    InteractionGraphNode data = definition.Nodes[index];
                    if (data == null || data.kind == InteractionNodeKind.Invalid ||
                        string.IsNullOrWhiteSpace(data.id) || nodeById.ContainsKey(data.id))
                        continue;
                    InteractionGraphNodeView node =
                        new InteractionGraphNodeView(data, ScheduleSave);
                    nodeById.Add(data.id, node);
                    AddElement(node);
                }

                for (int index = 0; index < definition.Edges.Count; index++)
                {
                    InteractionGraphEdge edge = definition.Edges[index];
                    if (edge == null ||
                        !nodeById.TryGetValue(edge.fromId ?? string.Empty, out InteractionGraphNodeView from) ||
                        !nodeById.TryGetValue(edge.toId ?? string.Empty, out InteractionGraphNodeView to) ||
                        to.Input == null)
                        continue;
                    AddElement(from.Output.ConnectTo(to.Input));
                }
            }

            loading = false;
            if (definition != null)
                schedule.Execute(() => { FrameAll(); }).ExecuteLater(50);
        }

        internal void AddInteractionNode(InteractionNodeKind kind)
        {
            if (definition == null) return;
            InteractionGraphNode data = new InteractionGraphNode
            {
                kind = kind,
                position = new Vector2(40f + nodeById.Count * 28f,
                    80f + nodeById.Count * 24f)
            };
            InteractionGraphNodeView node =
                new InteractionGraphNodeView(data, ScheduleSave);
            nodeById.Add(data.id, node);
            AddElement(node);
            ScheduleSave();
        }

        internal void DeleteSelectedNodes()
        {
            List<GraphElement> selectedElements = selection
                .OfType<GraphElement>()
                .ToList();
            if (selectedElements.Count == 0) return;
            DeleteElements(selectedElements);
            ScheduleSave();
        }

        internal void CopyTo(InteractionObjectDefinition target)
        {
            if (target == null) return;
            target.EditableNodes.Clear();
            target.EditableEdges.Clear();
            foreach (InteractionGraphNodeView node in graphElements
                         .OfType<InteractionGraphNodeView>())
            {
                target.EditableNodes.Add(new InteractionGraphNode
                {
                    id = node.NodeId,
                    kind = node.Kind,
                    position = node.GetPosition().position,
                    value = node.ValueField?.value,
                    number = node.NumberField?.value ?? 0f,
                    argument = node.ArgumentField?.value,
                    reference = node.ReferenceField?.value
                });
            }

            foreach (Edge edge in graphElements.OfType<Edge>())
            {
                if (edge.output?.node is InteractionGraphNodeView from &&
                    edge.input?.node is InteractionGraphNodeView to)
                {
                    target.EditableEdges.Add(new InteractionGraphEdge
                    {
                        fromId = from.NodeId,
                        toId = to.NodeId
                    });
                }
            }
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change)
        {
            if (!loading) ScheduleSave();
            return change;
        }

        private void ScheduleSave()
        {
            if (!loading)
                schedule.Execute(() => changed?.Invoke()).ExecuteLater(1);
        }
    }
}
