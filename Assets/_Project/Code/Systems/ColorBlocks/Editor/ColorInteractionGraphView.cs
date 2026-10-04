using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.ColorBlocks.Editor
{
    internal static class ColorNodeLibrary
    {
        internal static readonly ColorGraphNodeKind[] Templates =
        {
            ColorGraphNodeKind.PlayerEntered, ColorGraphNodeKind.PlayerLeft,
            ColorGraphNodeKind.ColorBlockTouched, ColorGraphNodeKind.ColorUnlocked,
            ColorGraphNodeKind.RequirePlayer, ColorGraphNodeKind.RequireSelfBlock,
            ColorGraphNodeKind.RequireOtherColor, ColorGraphNodeKind.RecolorSelf,
            ColorGraphNodeKind.UnlockColor, ColorGraphNodeKind.FadeColor,
            ColorGraphNodeKind.RestoreColor
        };

        internal static bool IsEvent(ColorGraphNodeKind kind) =>
            kind >= ColorGraphNodeKind.PlayerEntered && kind <= ColorGraphNodeKind.ColorUnlocked;

        internal static bool UsesColor(ColorGraphNodeKind kind) =>
            kind == ColorGraphNodeKind.RequireOtherColor || kind == ColorGraphNodeKind.RecolorSelf ||
            kind == ColorGraphNodeKind.UnlockColor || kind == ColorGraphNodeKind.FadeColor ||
            kind == ColorGraphNodeKind.RestoreColor;

        internal static string Label(ColorGraphNodeKind kind)
        {
            switch (kind)
            {
                case ColorGraphNodeKind.PlayerEntered: return "触发 / 玩家碰到本方块";
                case ColorGraphNodeKind.PlayerLeft: return "触发 / 玩家离开本方块";
                case ColorGraphNodeKind.ColorBlockTouched: return "触发 / 两个颜色方块接触";
                case ColorGraphNodeKind.ColorUnlocked: return "触发 / 颜色首次解锁";
                case ColorGraphNodeKind.RequirePlayer: return "对象 / 需要玩家";
                case ColorGraphNodeKind.RequireSelfBlock: return "对象 / 需要本方块";
                case ColorGraphNodeKind.RequireOtherColor: return "颜色 / 对方必须是指定色";
                case ColorGraphNodeKind.RecolorSelf: return "操作 / 本方块改色";
                case ColorGraphNodeKind.UnlockColor: return "操作 / 解锁颜色";
                case ColorGraphNodeKind.FadeColor: return "表现 / 褪去指定颜色";
                case ColorGraphNodeKind.RestoreColor: return "表现 / 恢复指定颜色";
                default: return "旧备注 / 不执行";
            }
        }

        internal static string Help(ColorGraphNodeKind kind)
        {
            switch (kind)
            {
                case ColorGraphNodeKind.RecolorSelf:
                    return "需先解锁目标色并授予玩家调色能力。";
                case ColorGraphNodeKind.RequireOtherColor:
                    return "检查接触的另一个 ColorBlock；无当前色时用其基础色。";
                case ColorGraphNodeKind.ColorUnlocked:
                    return "由颜色管理器首次解锁触发，不需场景碰撞。";
                case ColorGraphNodeKind.Note:
                    return "旧节点保留供查看；连线不会执行。";
                default:
                    return string.Empty;
            }
        }
    }

    internal sealed class ColorInteractionNode : Node
    {
        internal readonly string NodeId;
        internal readonly ColorGraphNodeKind Kind;
        internal readonly TextField TitleField;
        internal readonly PopupField<string> ColorField;
        internal readonly Port Input;
        internal readonly Port Output;

        internal ColorInteractionNode(ColorGraphNode data, IReadOnlyList<string> colorIds, Action changed)
        {
            NodeId = data.id;
            Kind = data.kind;
            title = ColorNodeLibrary.Label(data.kind);
            viewDataKey = data.id;
            SetPosition(new Rect(data.position, new Vector2(215, 132)));
            if (!ColorNodeLibrary.IsEvent(data.kind))
            {
                Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                Input.portName = "流程";
                inputContainer.Add(Input);
            }
            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Output.portName = "下一步";
            outputContainer.Add(Output);
            TitleField = new TextField("备注") { value = data.title };
            TitleField.RegisterValueChangedCallback(_ => changed());
            extensionContainer.Add(TitleField);
            if (ColorNodeLibrary.UsesColor(data.kind))
            {
                var choices = colorIds.ToList();
                if (!string.IsNullOrEmpty(data.colorTypeId) && !choices.Contains(data.colorTypeId))
                    choices.Add(data.colorTypeId);
                if (choices.Count == 0) choices.Add(string.Empty);
                string selected = choices.Contains(data.colorTypeId) ? data.colorTypeId : choices[0];
                ColorField = new PopupField<string>("颜色", choices, selected);
                ColorField.RegisterValueChangedCallback(_ => changed());
                extensionContainer.Add(ColorField);
            }
            var help = ColorNodeLibrary.Help(data.kind);
            if (!string.IsNullOrEmpty(help))
                extensionContainer.Add(new Label(help) { style = { whiteSpace = WhiteSpace.Normal } });
            RefreshExpandedState();
            RefreshPorts();
        }
    }

    internal sealed class ColorInteractionGraphView : GraphView
    {
        private readonly Dictionary<string, ColorInteractionNode> nodeById =
            new Dictionary<string, ColorInteractionNode>(StringComparer.Ordinal);
        private ColorTypeDefinition definition;
        private ColorCatalog catalog;
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
                port.node != startPort.node && port.direction != startPort.direction &&
                (port.direction != Direction.Input || !(port.node is ColorInteractionNode node) ||
                 !ColorNodeLibrary.IsEvent(node.Kind))).ToList();
        }

        internal void Load(ColorTypeDefinition color, ColorCatalog colorCatalog, Action onChanged)
        {
            loading = true;
            definition = color;
            catalog = colorCatalog;
            changed = onChanged;
            DeleteElements(graphElements.ToList());
            nodeById.Clear();
            if (color != null)
            {
                foreach (var data in color.nodes)
                {
                    if (data == null || string.IsNullOrEmpty(data.id) || nodeById.ContainsKey(data.id)) continue;
                    var node = new ColorInteractionNode(data, ColorIds(), ScheduleSave);
                    nodeById.Add(data.id, node);
                    AddElement(node);
                }
                foreach (var data in color.edges)
                {
                    if (data == null || !nodeById.TryGetValue(data.fromId ?? "", out var from) ||
                        !nodeById.TryGetValue(data.toId ?? "", out var to) || to.Input == null) continue;
                    AddElement(from.Output.ConnectTo(to.Input));
                }
            }
            loading = false;
            if (color != null) schedule.Execute(() => FrameAll()).ExecuteLater(50);
        }

        internal void AddInteractionNode(ColorGraphNodeKind kind)
        {
            if (definition == null) return;
            var data = new ColorGraphNode
            {
                kind = kind,
                title = string.Empty,
                colorTypeId = definition.id,
                position = new Vector2(35 + nodeById.Count * 35, 180 + nodeById.Count * 35)
            };
            var node = new ColorInteractionNode(data, ColorIds(), ScheduleSave);
            nodeById.Add(data.id, node);
            AddElement(node);
            ScheduleSave();
        }

        private IReadOnlyList<string> ColorIds() =>
            catalog?.Colors.Where(color => color != null).Select(color => color.id).ToList() ??
            new List<string>();

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
                    kind = node.Kind,
                    colorTypeId = node.ColorField?.value,
                    position = node.GetPosition().position
                });
            foreach (var edge in graphElements.OfType<Edge>())
                if (edge.output?.node is ColorInteractionNode from &&
                    edge.input?.node is ColorInteractionNode to)
                    color.edges.Add(new ColorGraphEdge { fromId = from.NodeId, toId = to.NodeId });
        }
    }
}
