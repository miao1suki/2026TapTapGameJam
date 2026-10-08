using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    internal sealed class RespawnAnchorGraphView : GraphView
    {
        private readonly Dictionary<string, RespawnAnchorGraphNode>
            nodesById =
                new Dictionary<string, RespawnAnchorGraphNode>(
                    StringComparer.Ordinal);
        private readonly List<RespawnAnchorFeature> layoutAnchors =
            new List<RespawnAnchorFeature>();
        private bool loading;
        private float layoutSpacing = 1f;
        private Vector2 layoutOrigin;

        public event Action Changed;
        public event Action<string> StatusChanged;
        public Scene Scene { get; private set; }
        public float LayoutSpacing => layoutSpacing;

        public RespawnAnchorGraphView()
        {
            style.flexGrow = 1f;
            Insert(0, new GridBackground());
            VisualElementExtensions.AddManipulator(
                this,
                new ContentDragger());
            VisualElementExtensions.AddManipulator(
                this,
                new SelectionDragger());
            VisualElementExtensions.AddManipulator(
                this,
                new RectangleSelector());
            VisualElementExtensions.AddManipulator(
                this,
                new ContentZoomer());
            SetupZoom(.05f, 2f);
            graphViewChanged += OnGraphViewChanged;
        }

        public void Load(Scene scene)
        {
            loading = true;
            try
            {
                Scene = scene;
                DeleteElements(graphElements.ToArray());
                nodesById.Clear();
                layoutAnchors.Clear();

                List<RespawnAnchorFeature> anchors =
                    RespawnAnchorChainEditorUtility
                        .GetSceneAnchors(scene);
                layoutAnchors.AddRange(anchors);
                layoutOrigin = GetWorldOrigin(anchors);
                for (int index = 0; index < anchors.Count; index++)
                {
                    RespawnAnchorGraphNode node =
                        new RespawnAnchorGraphNode(
                            anchors[index]);
                    node.SetPosition(new Rect(
                        GetPosition(
                            anchors[index],
                            layoutSpacing),
                        new Vector2(150f, 78f)));
                    AddNode(node);
                }

                for (int index = 0; index < anchors.Count; index++)
                {
                    RespawnAnchorFeature anchor = anchors[index];
                    if (string.IsNullOrEmpty(
                            anchor.NextAnchorId) ||
                        !nodesById.TryGetValue(
                            anchor.NextAnchorId,
                            out RespawnAnchorGraphNode target))
                    {
                        continue;
                    }

                    Connect(
                        nodesById[anchor.AnchorId],
                        target);
                }
            }
            finally
            {
                loading = false;
            }

            RequestFrameAll();
        }

        public void SetLayoutSpacing(float value)
        {
            layoutSpacing = Mathf.Clamp(value, .35f, 2.5f);
            ApplyLayout();
        }

        public bool RefreshLayoutFromWorldPositions()
        {
            bool changed = false;
            for (int index = 0;
                 index < layoutAnchors.Count;
                 index++)
            {
                RespawnAnchorFeature anchor =
                    layoutAnchors[index];
                if (anchor == null ||
                    !nodesById.TryGetValue(
                        anchor.AnchorId,
                        out RespawnAnchorGraphNode node))
                {
                    continue;
                }

                Rect current = node.GetPosition();
                Vector2 target = GetPosition(
                    anchor,
                    layoutSpacing);
                if (Mathf.Abs(current.x - target.x) < .01f &&
                    Mathf.Abs(current.y - target.y) < .01f)
                {
                    continue;
                }

                node.SetPosition(new Rect(
                    target,
                    current.size));
                changed = true;
            }

            return changed;
        }

        public void RequestFrameAll()
        {
            schedule.Execute(() =>
            {
                if (panel != null)
                {
                    FrameAll();
                }
            }).StartingIn(0L);
            schedule.Execute(() =>
            {
                if (panel != null)
                {
                    FrameAll();
                }
            }).StartingIn(60L);
        }

        public override List<Port> GetCompatiblePorts(
            Port startPort,
            NodeAdapter nodeAdapter)
        {
            var result = new List<Port>();
            if (startPort == null ||
                startPort.node is not RespawnAnchorGraphNode)
            {
                return result;
            }

            foreach (Port port in ports)
            {
                if (port.direction == startPort.direction ||
                    port.node == startPort.node ||
                    port.node is not RespawnAnchorGraphNode ||
                    port.connected)
                {
                    continue;
                }

                Port output = startPort.direction ==
                              Direction.Output
                    ? startPort
                    : port;
                Port input = startPort.direction ==
                             Direction.Input
                    ? startPort
                    : port;
                if (output.node is not RespawnAnchorGraphNode ||
                    input.node is not RespawnAnchorGraphNode ||
                    !IsConnectionAllowed(
                        (RespawnAnchorGraphNode)output.node,
                        (RespawnAnchorGraphNode)input.node))
                {
                    continue;
                }

                result.Add(port);
            }

            return result;
        }

        public void RefreshNodes()
        {
            foreach (RespawnAnchorGraphNode node in
                     nodesById.Values)
            {
                node.Refresh();
            }
        }

        public void ClearPending()
        {
            foreach (RespawnAnchorGraphNode node in
                     nodesById.Values)
            {
                node.SetPending(false);
            }
        }

        public override EventPropagation DeleteSelection()
        {
            if (selection.OfType<RespawnAnchorGraphNode>().Any())
            {
                StatusChanged?.Invoke("不能删除重生点节点。");
                return EventPropagation.Stop;
            }

            Edge[] edges = selection.OfType<Edge>().ToArray();
            if (edges.Length == 0)
            {
                return EventPropagation.Continue;
            }

            for (int index = 0; index < edges.Length; index++)
            {
                CutEdge(edges[index]);
            }

            return EventPropagation.Stop;
        }

        private void AddNode(RespawnAnchorGraphNode node)
        {
            node.capabilities &= ~Capabilities.Movable;
            node.capabilities &= ~Capabilities.Deletable;
            nodesById[node.Anchor.AnchorId] = node;
            AddElement(node);
        }

        private void ApplyLayout()
        {
            for (int index = 0;
                 index < layoutAnchors.Count;
                 index++)
            {
                RespawnAnchorFeature anchor =
                    layoutAnchors[index];
                if (!nodesById.TryGetValue(
                        anchor.AnchorId,
                        out RespawnAnchorGraphNode node))
                {
                    continue;
                }

                Vector2 position = GetPosition(
                    layoutAnchors[index],
                    layoutSpacing);
                node.SetPosition(new Rect(
                    position,
                    node.GetPosition().size));
            }
        }

        private void Connect(
            RespawnAnchorGraphNode source,
            RespawnAnchorGraphNode target)
        {
            if (source == null ||
                target == null ||
                source.OutputPort.connected ||
                target.InputPort.connected)
            {
                return;
            }

            Edge edge = source.OutputPort.ConnectTo(
                target.InputPort);
            ConfigureEdge(edge);
            edge.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                CutEdge(edge);
                evt.StopPropagation();
            });
            AddElement(edge);
        }

        private void ConfigureEdge(Edge edge)
        {
            if (edge?.edgeControl == null)
            {
                return;
            }

            EdgeControl control = edge.edgeControl;
            control.drawFromCap = false;
            control.drawToCap = true;
            control.capRadius = 5f;
            Color color = EditorGUIUtility.isProSkin
                ? new Color(.36f, .62f, 1f, 1f)
                : new Color(.08f, .38f, .78f, 1f);
            control.inputColor = color;
            control.outputColor = color;
            control.toCapColor = color;
        }

        private void CutEdge(Edge edge)
        {
            if (edge?.input?.node is not
                RespawnAnchorGraphNode target)
            {
                return;
            }

            bool changed =
                RespawnAnchorChainEditorUtility
                    .TryDisconnectIncoming(
                        target.Anchor,
                        out string message);
            StatusChanged?.Invoke(message);
            if (!changed)
            {
                return;
            }

            Changed?.Invoke();
            Load(Scene);
        }

        private bool IsConnectionAllowed(
            RespawnAnchorGraphNode source,
            RespawnAnchorGraphNode target)
        {
            if (source == null ||
                target == null ||
                source == target)
            {
                return false;
            }

            List<RespawnAnchorFeature> anchors =
                RespawnAnchorChainEditorUtility
                    .GetSceneAnchors(Scene);
            List<RespawnAnchorFeature> mainChain =
                RespawnAnchorChainRuntime.BuildMainChain(
                    anchors);
            if (mainChain.Count == 0)
            {
                return !HasIncoming(source.Anchor, anchors) &&
                       !HasIncoming(target.Anchor, anchors);
            }

            RespawnAnchorFeature tail =
                mainChain[mainChain.Count - 1];
            if (source.Anchor != tail)
            {
                return false;
            }

            return !mainChain.Contains(target.Anchor) &&
                   !HasIncoming(target.Anchor, anchors);
        }

        private static bool HasIncoming(
            RespawnAnchorFeature target,
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            if (target == null ||
                string.IsNullOrEmpty(target.AnchorId))
            {
                return false;
            }

            for (int index = 0; index < anchors.Count; index++)
            {
                if (anchors[index].NextAnchorId ==
                    target.AnchorId)
                {
                    return true;
                }
            }

            return false;
        }

        private GraphViewChange OnGraphViewChanged(
            GraphViewChange change)
        {
            if (loading)
            {
                return change;
            }

            if (change.edgesToCreate != null)
            {
                Edge[] edges = change.edgesToCreate.ToArray();
                for (int index = 0; index < edges.Length; index++)
                {
                    Edge edge = edges[index];
                    if (edge.output?.node is not
                            RespawnAnchorGraphNode source ||
                        edge.input?.node is not
                            RespawnAnchorGraphNode target)
                    {
                        continue;
                    }

                    bool success =
                        RespawnAnchorChainEditorUtility
                            .TryConnect(
                                source.Anchor,
                                target.Anchor,
                                out string message);
                    StatusChanged?.Invoke(message);
                    if (!success)
                    {
                        change.edgesToCreate.Remove(edge);
                        RemoveElement(edge);
                        continue;
                    }

                    target.SetFeedback(true);
                    target.schedule.Execute(
                            () => target.SetFeedback(false))
                        .StartingIn(900L);
                    RefreshNodes();
                    Changed?.Invoke();
                }
            }

            if (change.elementsToRemove != null)
            {
                for (int index = 0;
                     index < change.elementsToRemove.Count;
                     index++)
                {
                    if (change.elementsToRemove[index] is
                        Edge edge)
                    {
                        CutEdge(edge);
                    }
                }
            }

            return change;
        }

        private static Vector2 GetWorldOrigin(
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            if (anchors == null || anchors.Count == 0)
            {
                return Vector2.zero;
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            for (int index = 0; index < anchors.Count; index++)
            {
                Vector3 position = anchors[index].transform.position;
                minX = Mathf.Min(minX, position.x);
                minY = Mathf.Min(minY, position.y);
            }

            return new Vector2(minX, minY);
        }

        private Vector2 GetPosition(
            RespawnAnchorFeature anchor,
            float spacing)
        {
            if (anchor == null)
            {
                return Vector2.zero;
            }

            Vector3 current = anchor.transform.position;
            return new Vector2(
                40f +
                (current.x - layoutOrigin.x) * 120f * spacing,
                40f +
                (layoutOrigin.y - current.y) * 90f * spacing);
        }
    }
}
