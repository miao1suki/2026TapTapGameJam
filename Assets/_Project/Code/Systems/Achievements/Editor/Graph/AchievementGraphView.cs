using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Achievements.Editor
{
    internal sealed class AchievementGraphSnapshot
    {
        public readonly List<AchievementConditionSnapshot>
            Conditions =
                new List<AchievementConditionSnapshot>();

        public readonly List<AchievementLogicSnapshot>
            LogicNodes =
                new List<AchievementLogicSnapshot>();

        public readonly List<AchievementFailureSnapshot>
            FailureNodes =
                new List<AchievementFailureSnapshot>();

        public string RootSourceNodeId;
        public Vector2 RootPosition;
    }

    internal sealed class AchievementConditionSnapshot
    {
        public string NodeId;
        public Vector2 Position;
        public AchievementConditionMode Mode;
        public int TargetCount;
        public float TargetProgress;
        public string TextPrefix;
        public string TextSuffix;
    }

    internal sealed class AchievementLogicSnapshot
    {
        public string NodeId;
        public Vector2 Position;
        public AchievementLogicType LogicType;
        public readonly List<string> InputNodeIds =
            new List<string>();
    }

    internal sealed class AchievementFailureSnapshot
    {
        public string NodeId;
        public Vector2 Position;
        public string SourceNodeId;
    }

    internal sealed class AchievementGraphView : GraphView
    {
        private readonly Dictionary<string, AchievementGraphNode>
            nodesById =
                new Dictionary<string, AchievementGraphNode>(
                    StringComparer.Ordinal);

        public event Action Changed;

        public AchievementSO Achievement { get; private set; }

        public AchievementGraphView()
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
            SetupZoom(0.2f, 2f);
            graphViewChanged += OnGraphViewChanged;
        }

        public void Load(AchievementSO achievement)
        {
            Achievement = achievement;
            DeleteElements(graphElements.ToArray());
            nodesById.Clear();
            if (achievement == null)
            {
                return;
            }

            int created = 0;
            for (int index = 0;
                 index < achievement.Conditions.Count;
                 index++)
            {
                AchievementConditionDefinition condition =
                    achievement.Conditions[index];
                AchievementConditionGraphNode node =
                    new AchievementConditionGraphNode(
                        condition.ConditionId,
                        GetPosition(
                            condition.EditorPosition,
                            created++),
                        condition.Mode,
                        condition.TargetCount,
                        condition.TargetProgress,
                        condition.TextPrefix,
                        condition.TextSuffix);
                AddNode(node);
            }

            for (int index = 0;
                 index < achievement.LogicNodes.Count;
                 index++)
            {
                AchievementLogicNodeDefinition logic =
                    achievement.LogicNodes[index];
                AchievementLogicGraphNode node =
                    new AchievementLogicGraphNode(
                        logic.NodeId,
                        GetPosition(
                            logic.EditorPosition,
                            created++),
                        logic.LogicType);
                AddNode(node);
            }

            Dictionary<string, string> failureSources =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);
            for (int index = 0;
                 index < achievement.LockConditionIds.Count;
                 index++)
            {
                string failureId = GetNextId(
                    "F",
                    node => node is AchievementFailureGraphNode);
                AchievementFailureGraphNode failureNode =
                    new AchievementFailureGraphNode(
                        failureId,
                        GetPosition(
                            Vector2.zero,
                            created++));
                AddNode(failureNode);
                failureSources[failureId] =
                    achievement.LockConditionIds[index];
            }

            AchievementRootGraphNode root =
                new AchievementRootGraphNode(
                    GetPosition(
                        achievement.RootEditorPosition,
                        created));
            AddNode(root);

            for (int index = 0;
                 index < achievement.LogicNodes.Count;
                 index++)
            {
                AchievementLogicNodeDefinition logic =
                    achievement.LogicNodes[index];
                for (int inputIndex = 0;
                     inputIndex < logic.InputNodeIds.Count;
                     inputIndex++)
                {
                    Connect(
                        logic.InputNodeIds[inputIndex],
                        logic.NodeId);
                }
            }

            foreach (KeyValuePair<string, string> pair in
                     failureSources)
            {
                Connect(
                    pair.Value,
                    pair.Key);
            }

            Connect(achievement.RootNodeId, "__ROOT__");
            RefreshSourceSummaries();
        }

        public AchievementGraphSnapshot CaptureSnapshot()
        {
            AchievementGraphSnapshot snapshot =
                new AchievementGraphSnapshot
                {
                    RootSourceNodeId =
                        GetRootSourceNodeId(),
                    RootPosition = GetRootPosition()
                };

            foreach (AchievementConditionGraphNode node in
                     nodesById.Values
                         .OfType<AchievementConditionGraphNode>())
            {
                snapshot.Conditions.Add(
                    new AchievementConditionSnapshot
                    {
                        NodeId = node.NodeId,
                        Position = node.GetPosition().position,
                        Mode = node.Mode,
                        TargetCount = node.TargetCount,
                        TargetProgress = node.TargetProgress,
                        TextPrefix = node.TextPrefix,
                        TextSuffix = node.TextSuffix
                    });
            }

            foreach (AchievementLogicGraphNode node in
                     nodesById.Values
                         .OfType<AchievementLogicGraphNode>())
            {
                AchievementLogicSnapshot logicSnapshot =
                    new AchievementLogicSnapshot
                    {
                        NodeId = node.NodeId,
                        Position = node.GetPosition().position,
                        LogicType = node.LogicType
                    };
                logicSnapshot.InputNodeIds.AddRange(
                    GetInputNodeIds(node));
                snapshot.LogicNodes.Add(logicSnapshot);
            }

            foreach (AchievementFailureGraphNode node in
                     nodesById.Values
                         .OfType<AchievementFailureGraphNode>())
            {
                List<string> inputs =
                    GetInputNodeIds(node);
                snapshot.FailureNodes.Add(
                    new AchievementFailureSnapshot
                    {
                        NodeId = node.NodeId,
                        Position = node.GetPosition().position,
                        SourceNodeId =
                            inputs.Count > 0
                                ? inputs[0]
                                : string.Empty
                    });
            }

            return snapshot;
        }

        public void RestoreSnapshot(
            AchievementGraphSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            DeleteElements(graphElements.ToArray());
            nodesById.Clear();

            for (int index = 0;
                 index < snapshot.Conditions.Count;
                 index++)
            {
                AchievementConditionSnapshot condition =
                    snapshot.Conditions[index];
                AddNode(
                    new AchievementConditionGraphNode(
                        condition.NodeId,
                        condition.Position,
                        condition.Mode,
                        condition.TargetCount,
                        condition.TargetProgress,
                        condition.TextPrefix,
                        condition.TextSuffix));
            }

            for (int index = 0;
                 index < snapshot.LogicNodes.Count;
                 index++)
            {
                AchievementLogicSnapshot logic =
                    snapshot.LogicNodes[index];
                AddNode(
                    new AchievementLogicGraphNode(
                        logic.NodeId,
                        logic.Position,
                        logic.LogicType));
            }

            for (int index = 0;
                 index < snapshot.FailureNodes.Count;
                 index++)
            {
                AchievementFailureSnapshot failure =
                    snapshot.FailureNodes[index];
                AddNode(
                    new AchievementFailureGraphNode(
                        failure.NodeId,
                        failure.Position));
            }

            AddNode(
                new AchievementRootGraphNode(
                    snapshot.RootPosition));

            for (int index = 0;
                 index < snapshot.LogicNodes.Count;
                 index++)
            {
                AchievementLogicSnapshot logic =
                    snapshot.LogicNodes[index];
                for (int inputIndex = 0;
                     inputIndex < logic.InputNodeIds.Count;
                     inputIndex++)
                {
                    Connect(
                        logic.InputNodeIds[inputIndex],
                        logic.NodeId);
                }
            }

            for (int index = 0;
                 index < snapshot.FailureNodes.Count;
                 index++)
            {
                AchievementFailureSnapshot failure =
                    snapshot.FailureNodes[index];
                Connect(
                    failure.SourceNodeId,
                    failure.NodeId);
            }

            Connect(
                snapshot.RootSourceNodeId,
                "__ROOT__");
            RefreshSourceSummaries();
        }

        public override List<Port> GetCompatiblePorts(
            Port startPort,
            NodeAdapter nodeAdapter)
        {
            List<Port> result = new List<Port>();
            foreach (Port port in ports)
            {
                if (port.direction == startPort.direction ||
                    port.node == startPort.node ||
                    port.node is AchievementConditionGraphNode ||
                    port.node is AchievementRootGraphNode &&
                    port.direction == Direction.Output)
                {
                    continue;
                }

                result.Add(port);
            }

            return result;
        }

        public void AddConditionNode()
        {
            string id = GetNextId(
                string.Empty,
                node => node is AchievementConditionGraphNode);
            AddNode(
                new AchievementConditionGraphNode(
                    id,
                    GetPosition(Vector2.zero, nodesById.Count),
                    AchievementConditionMode.Trigger,
                    1,
                    100f,
                    string.Empty,
                    string.Empty));
            Changed?.Invoke();
        }

        public void AddLogicNode(
            AchievementLogicType logicType)
        {
            string id = GetNextId(
                "L",
                node => node is AchievementLogicGraphNode);
            AddNode(
                new AchievementLogicGraphNode(
                    id,
                    GetPosition(Vector2.zero, nodesById.Count),
                    logicType));
            Changed?.Invoke();
        }

        public void AddFailureNode()
        {
            string id = GetNextId(
                "F",
                node => node is AchievementFailureGraphNode);
            AddNode(
                new AchievementFailureGraphNode(
                    id,
                    GetPosition(Vector2.zero, nodesById.Count)));
            Changed?.Invoke();
        }

        public List<AchievementConditionGraphNode>
            GetConditionNodes()
        {
            return nodesById.Values
                .OfType<AchievementConditionGraphNode>()
                .ToList();
        }

        public List<AchievementLogicGraphNode>
            GetLogicNodes()
        {
            return nodesById.Values
                .OfType<AchievementLogicGraphNode>()
                .ToList();
        }

        public List<AchievementFailureGraphNode>
            GetFailureNodes()
        {
            return nodesById.Values
                .OfType<AchievementFailureGraphNode>()
                .ToList();
        }

        public string GetRootSourceNodeId()
        {
            AchievementRootGraphNode root =
                nodesById.Values
                    .OfType<AchievementRootGraphNode>()
                    .FirstOrDefault();
            if (root == null ||
                root.InputPort == null ||
                !root.InputPort.connected)
            {
                return string.Empty;
            }

            Edge edge = root.InputPort.connections.FirstOrDefault();
            return edge?.output?.node is AchievementGraphNode node
                ? node.NodeId
                : string.Empty;
        }

        public List<string> GetInputNodeIds(
            AchievementGraphNode graphNode)
        {
            List<string> result = new List<string>();
            if (graphNode?.InputPort == null)
            {
                return result;
            }

            foreach (Edge edge in graphNode.InputPort.connections)
            {
                if (edge.output?.node is AchievementGraphNode node)
                {
                    result.Add(node.NodeId);
                }
            }

            return result;
        }

        public void RefreshSourceSummaries()
        {
            foreach (AchievementLogicGraphNode logicNode in
                     nodesById.Values
                         .OfType<AchievementLogicGraphNode>())
            {
                List<string> sources =
                    GetInputNodeIds(logicNode);
                logicNode.SetSourceSummary(
                    string.Join(
                        "、",
                        sources));
            }

            foreach (AchievementFailureGraphNode failureNode in
                     nodesById.Values
                         .OfType<AchievementFailureGraphNode>())
            {
                List<string> sources =
                    GetInputNodeIds(failureNode);
                failureNode.SetSourceSummary(
                    string.Join(
                        "、",
                        sources));
            }

            AchievementRootGraphNode root =
                nodesById.Values
                    .OfType<AchievementRootGraphNode>()
                    .FirstOrDefault();
            if (root == null)
            {
                return;
            }

            string rootSource =
                GetRootSourceNodeId();
            root.SetSourceSummary(rootSource);
        }

        public Vector2 GetRootPosition()
        {
            AchievementRootGraphNode root =
                nodesById.Values
                    .OfType<AchievementRootGraphNode>()
                    .FirstOrDefault();
            return root?.GetPosition().position ??
                   Vector2.zero;
        }

        private void AddNode(AchievementGraphNode node)
        {
            nodesById[node.NodeId] = node;
            node.LogicTypeChanged += () =>
            {
                RefreshSourceSummaries();
                Changed?.Invoke();
            };
            AddElement(node);
        }

        private void Connect(
            string sourceId,
            string targetId)
        {
            if (string.IsNullOrWhiteSpace(sourceId) ||
                !nodesById.TryGetValue(
                    sourceId,
                    out AchievementGraphNode source) ||
                !nodesById.TryGetValue(
                    targetId,
                    out AchievementGraphNode target) ||
                source.OutputPort == null ||
                target.InputPort == null)
            {
                return;
            }

            Edge edge = source.OutputPort.ConnectTo(
                target.InputPort);
            AddElement(edge);
            ConfigureEdgeDirection(edge);
        }

        private static void ConfigureEdgeDirection(
            Edge edge)
        {
            if (edge?.edgeControl == null)
            {
                return;
            }

            EdgeControl control = edge.edgeControl;
            control.drawFromCap = false;
            control.drawToCap = true;
            control.capRadius = 5f;
            Color edgeColor = EditorGUIUtility.isProSkin
                ? new Color(0.36f, 0.62f, 1f, 1f)
                : new Color(0.08f, 0.38f, 0.78f, 1f);
            control.inputColor = edgeColor;
            control.outputColor = edgeColor;
            control.toCapColor = edgeColor;
        }

        private string GetNextId(
            string prefix,
            Func<AchievementGraphNode, bool> predicate)
        {
            int value = 1;
            while (true)
            {
                string candidate =
                    prefix + value.ToString("0000");
                bool exists = nodesById.Values.Any(
                    node =>
                        predicate(node) &&
                        node.NodeId == candidate);
                if (!exists)
                {
                    return candidate;
                }

                value++;
            }
        }

        private static Vector2 GetPosition(
            Vector2 stored,
            int index)
        {
            if (stored != Vector2.zero)
            {
                return stored;
            }

            return new Vector2(
                40f + index % 3 * 240f,
                40f + index / 3 * 150f);
        }

        private GraphViewChange OnGraphViewChanged(
            GraphViewChange change)
        {
            if (change.elementsToRemove != null)
            {
                for (int index = 0;
                     index < change.elementsToRemove.Count;
                     index++)
                {
                    if (change.elementsToRemove[index] is
                        AchievementGraphNode node)
                    {
                        nodesById.Remove(node.NodeId);
                    }
                }
            }

            RefreshSourceSummaries();
            Changed?.Invoke();
            return change;
        }
    }
}
