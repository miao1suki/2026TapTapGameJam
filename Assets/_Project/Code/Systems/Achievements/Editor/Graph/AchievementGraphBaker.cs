using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Project.Achievements.Editor
{
    internal static class AchievementGraphBaker
    {
        public static bool Bake(
            AchievementGraphView graphView)
        {
            if (graphView == null ||
                graphView.Achievement == null)
            {
                return false;
            }

            AchievementSO achievement =
                graphView.Achievement;
            if (!Validate(graphView, out string error))
            {
                EditorUtility.DisplayDialog(
                    "无法保存成就",
                    error,
                    "确定");
                return false;
            }

            Undo.RecordObject(
                achievement,
                "保存成就关系");
            SerializedObject serialized =
                new SerializedObject(achievement);
            serialized.Update();

            SerializedProperty conditions =
                serialized.FindProperty("conditions");
            SerializedProperty logicNodes =
                serialized.FindProperty("logicNodes");
            SerializedProperty locks =
                serialized.FindProperty("lockConditionIds");

            HashSet<string> conditionIds =
                new HashSet<string>();
            List<AchievementConditionGraphNode> conditionNodes =
                graphView.GetConditionNodes();
            for (int index = 0;
                 index < conditionNodes.Count;
                 index++)
            {
                AchievementConditionGraphNode node =
                    conditionNodes[index];
                conditionIds.Add(node.NodeId);
                SerializedProperty item =
                    FindOrAdd(
                        conditions,
                        "conditionId",
                        node.NodeId);
                item.FindPropertyRelative("conditionId")
                    .stringValue = node.NodeId;
                item.FindPropertyRelative("mode")
                    .enumValueIndex = (int)node.Mode;
                item.FindPropertyRelative("targetCount")
                    .intValue = Mathf.Max(
                        1,
                        node.TargetCount);
                item.FindPropertyRelative("targetProgress")
                    .floatValue = Mathf.Clamp(
                        node.TargetProgress,
                        0f,
                        100f);
                item.FindPropertyRelative("textPrefix")
                    .stringValue = node.TextPrefix ?? string.Empty;
                item.FindPropertyRelative("textSuffix")
                    .stringValue = node.TextSuffix ?? string.Empty;
                item.FindPropertyRelative("editorPosition")
                    .vector2Value =
                    node.GetPosition().position;
            }

            for (int index = conditions.arraySize - 1;
                 index >= 0;
                 index--)
            {
                string id = conditions
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("conditionId")
                    .stringValue;
                if (!conditionIds.Contains(id))
                {
                    conditions.DeleteArrayElementAtIndex(index);
                }
            }

            HashSet<string> logicIds =
                new HashSet<string>();
            List<AchievementLogicGraphNode> logicNodeList =
                graphView.GetLogicNodes();
            for (int index = 0;
                 index < logicNodeList.Count;
                 index++)
            {
                AchievementLogicGraphNode node =
                    logicNodeList[index];
                logicIds.Add(node.NodeId);
                SerializedProperty item =
                    FindOrAdd(
                        logicNodes,
                        "nodeId",
                        node.NodeId);
                item.FindPropertyRelative("nodeId")
                    .stringValue = node.NodeId;
                item.FindPropertyRelative("logicType")
                    .enumValueIndex = (int)node.LogicType;
                SerializedProperty inputs =
                    item.FindPropertyRelative("inputNodeIds");
                List<string> inputIds =
                    graphView.GetInputNodeIds(node);
                inputs.arraySize = inputIds.Count;
                for (int inputIndex = 0;
                     inputIndex < inputIds.Count;
                     inputIndex++)
                {
                    inputs.GetArrayElementAtIndex(inputIndex)
                        .stringValue = inputIds[inputIndex];
                }

                item.FindPropertyRelative("editorPosition")
                    .vector2Value =
                    node.GetPosition().position;
            }

            for (int index = logicNodes.arraySize - 1;
                 index >= 0;
                 index--)
            {
                string id = logicNodes
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("nodeId")
                    .stringValue;
                if (!logicIds.Contains(id))
                {
                    logicNodes.DeleteArrayElementAtIndex(index);
                }
            }

            List<AchievementFailureGraphNode> failureNodes =
                graphView.GetFailureNodes();
            locks.arraySize = failureNodes.Count;
            for (int index = 0;
                 index < failureNodes.Count;
                 index++)
            {
                List<string> inputs =
                    graphView.GetInputNodeIds(
                        failureNodes[index]);
                if (inputs.Count == 0)
                {
                    continue;
                }

                locks.GetArrayElementAtIndex(index)
                    .stringValue = inputs[0];
            }

            serialized.FindProperty("rootNodeId")
                .stringValue =
                graphView.GetRootSourceNodeId();
            serialized.FindProperty("rootEditorPosition")
                .vector2Value =
                graphView.GetRootPosition();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(achievement);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool Validate(
            AchievementGraphView graphView,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(
                    graphView.GetRootSourceNodeId()))
            {
                error =
                    "“完成条件”还没有连接任何条件。";
                return false;
            }

            List<AchievementLogicGraphNode> logicNodes =
                graphView.GetLogicNodes();
            for (int index = 0;
                 index < logicNodes.Count;
                 index++)
            {
                AchievementLogicGraphNode node =
                    logicNodes[index];
                int inputCount =
                    graphView.GetInputNodeIds(node).Count;
                if (node.LogicType ==
                    AchievementLogicType.Not &&
                    inputCount != 1)
                {
                    error =
                        node.NodeId +
                        " 的“必须未发生”只能连接一个条件。";
                    return false;
                }

                if (node.LogicType !=
                    AchievementLogicType.Not &&
                    inputCount < 2)
                {
                    error =
                        node.NodeId +
                        " 至少需要连接两个条件或条件组。";
                    return false;
                }
            }

            List<AchievementFailureGraphNode> failureNodes =
                graphView.GetFailureNodes();
            for (int index = 0;
                 index < failureNodes.Count;
                 index++)
            {
                AchievementFailureGraphNode failureNode =
                    failureNodes[index];
                int inputCount =
                    graphView.GetInputNodeIds(
                        failureNode).Count;
                if (inputCount != 1)
                {
                    error =
                        failureNode.NodeId +
                        " 的失败条件必须连接一个条件。";
                    return false;
                }
            }

            Dictionary<string, AchievementGraphNode> nodes =
                graphView.graphElements
                    .OfType<AchievementGraphNode>()
                    .ToDictionary(
                        node => node.NodeId,
                        node => node);
            Dictionary<string, List<string>> outgoing =
                new Dictionary<string, List<string>>();
            foreach (Edge edge in graphView.graphElements
                         .OfType<Edge>())
            {
                if (!(edge.output?.node is
                        AchievementGraphNode source) ||
                    !(edge.input?.node is
                        AchievementGraphNode target))
                {
                    continue;
                }

                if (!outgoing.TryGetValue(
                        source.NodeId,
                        out List<string> targets))
                {
                    targets = new List<string>();
                    outgoing.Add(source.NodeId, targets);
                }

                targets.Add(target.NodeId);
            }

            HashSet<string> visiting =
                new HashSet<string>();
            HashSet<string> visited =
                new HashSet<string>();
            foreach (string nodeId in nodes.Keys)
            {
                if (HasCycle(
                        nodeId,
                        outgoing,
                        visiting,
                        visited))
                {
                    error =
                        "存在循环连接，请先断开。节点：" +
                        nodeId;
                    return false;
                }
            }

            return true;
        }

        private static bool HasCycle(
            string nodeId,
            Dictionary<string, List<string>> outgoing,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visiting.Contains(nodeId))
            {
                return true;
            }

            if (!visited.Add(nodeId))
            {
                return false;
            }

            visiting.Add(nodeId);
            if (outgoing.TryGetValue(
                    nodeId,
                    out List<string> targets))
            {
                for (int index = 0;
                     index < targets.Count;
                     index++)
                {
                    if (HasCycle(
                            targets[index],
                            outgoing,
                            visiting,
                            visited))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(nodeId);
            return false;
        }

        private static SerializedProperty FindOrAdd(
            SerializedProperty list,
            string idField,
            string id)
        {
            for (int index = 0;
                 index < list.arraySize;
                 index++)
            {
                SerializedProperty item =
                    list.GetArrayElementAtIndex(index);
                if (item.FindPropertyRelative(idField)
                    .stringValue == id)
                {
                    return item;
                }
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            return list.GetArrayElementAtIndex(
                list.arraySize - 1);
        }
    }
}
