using System;
using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BossLogic
{
    public enum BossPathHazardPolicy
    {
        [InspectorName("危险也直接通过")]
        PassThrough = 0,
        [InspectorName("危险边不作为路径")]
        Avoid = 1
    }

    public readonly struct BossPathStep
    {
        public BossPathStep(
            BossPathNode from,
            BossPathNode to,
            BossPathLinkKind kind,
            float cost)
        {
            From = from;
            To = to;
            Kind = kind;
            Cost = cost;
        }

        public BossPathNode From { get; }
        public BossPathNode To { get; }
        public BossPathLinkKind Kind { get; }
        public float Cost { get; }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/Boss/追击寻路器")]
    public sealed class BossPathfinder : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] private Transform target;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private bool autoResolveTarget = true;

        [Header("重算")]
        [SerializeField] private bool autoRepath = true;
        [SerializeField, Min(.05f)] private float repathInterval = .25f;
        [SerializeField, Min(.1f)] private float nodeSearchRadius = 6f;
        [SerializeField] private BossPathHazardPolicy hazardPolicy =
            BossPathHazardPolicy.PassThrough;

        private readonly List<BossPathNode> currentPath =
            new List<BossPathNode>();
        private readonly List<BossPathStep> currentSteps =
            new List<BossPathStep>();
        private readonly List<BossPathNode> searchBuffer =
            new List<BossPathNode>();
        private float nextRepathTime;
        private BossPathNode lastGoalNode;
        private BossPathNode lastStartNode;
        private int pathVersion;

        public event Action<IReadOnlyList<BossPathNode>> PathChanged;
        public event Action<IReadOnlyList<BossPathStep>> StepsChanged;

        public Transform Target => target;
        public bool HasPath => currentPath.Count > 0;
        public IReadOnlyList<BossPathNode> CurrentPath =>
            currentPath;
        public BossPathNode StartNode =>
            currentPath.Count > 0 ? currentPath[0] : null;
        public BossPathNode GoalNode =>
            currentPath.Count > 0
                ? currentPath[currentPath.Count - 1]
                : null;
        public int PathVersion => pathVersion;
        public IReadOnlyList<BossPathStep> CurrentSteps =>
            currentSteps;

        private void Update()
        {
            if (!autoRepath)
            {
                return;
            }

            ResolveTarget();
            if (target == null || Time.time < nextRepathTime)
            {
                return;
            }

            nextRepathTime =
                Time.time + Mathf.Max(.05f, repathInterval);
            BossPathNode start = FindNearestNode(
                transform.position,
                nodeSearchRadius);
            BossPathNode goal = FindNearestNode(
                target.position,
                nodeSearchRadius);
            if (start == null || goal == null)
            {
                return;
            }

            if (start != lastStartNode || goal != lastGoalNode)
            {
                lastStartNode = start;
                lastGoalNode = goal;
            }

            TryBuildPath(start, goal);
        }

        public bool RequestRepath()
        {
            ResolveTarget();
            if (target == null)
            {
                ClearPath();
                return false;
            }

            BossPathNode start = FindNearestNode(
                transform.position,
                nodeSearchRadius);
            BossPathNode goal = FindNearestNode(
                target.position,
                nodeSearchRadius);
            return start != null &&
                   goal != null &&
                   TryBuildPath(start, goal);
        }

        public void SetTarget(Transform value)
        {
            target = value;
            ClearPath();
        }

        public bool TryGetNextNode(out BossPathNode node)
        {
            if (currentPath.Count < 2)
            {
                node = null;
                return false;
            }

            node = currentPath[1];
            return true;
        }

        public bool TryGetNextStep(out BossPathStep step)
        {
            if (currentSteps.Count == 0)
            {
                step = default;
                return false;
            }

            step = currentSteps[0];
            return true;
        }

        public void ClearPath()
        {
            if (currentPath.Count == 0)
            {
                return;
            }

            currentPath.Clear();
            currentSteps.Clear();
            lastStartNode = null;
            lastGoalNode = null;
            pathVersion++;
            PathChanged?.Invoke(currentPath);
            StepsChanged?.Invoke(currentSteps);
        }

        private bool TryBuildPath(
            BossPathNode start,
            BossPathNode goal)
        {
            if (start == null || goal == null)
            {
                ClearPath();
                return false;
            }

            if (start == goal)
            {
                currentPath.Clear();
                currentSteps.Clear();
                currentPath.Add(goal);
                pathVersion++;
                PathChanged?.Invoke(currentPath);
                StepsChanged?.Invoke(currentSteps);
                return true;
            }

            var open = new List<BossPathNode> { start };
            var cameFrom = new Dictionary<BossPathNode, BossPathNode>();
            var gScore = new Dictionary<BossPathNode, float>
            {
                [start] = 0f
            };
            var fScore = new Dictionary<BossPathNode, float>
            {
                [start] = Heuristic(start, goal)
            };
            searchBuffer.Clear();

            while (open.Count > 0)
            {
                BossPathNode current = TakeBest(open, fScore);
                if (current == goal)
                {
                    CommitPath(
                        Reconstruct(cameFrom, current));
                    return true;
                }

                searchBuffer.Add(current);
                IReadOnlyList<BossPathLink> links = current.Links;
                for (int index = 0; index < links.Count; index++)
                {
                    BossPathLink link = links[index];
                    if (link == null || link.Target == null)
                    {
                        continue;
                    }

                    if (hazardPolicy ==
                        BossPathHazardPolicy.Avoid &&
                        link.Kind == BossPathLinkKind.Hazard)
                    {
                        continue;
                    }

                    BossPathNode neighbor = link.Target;
                    if (searchBuffer.Contains(neighbor))
                    {
                        continue;
                    }

                    float tentative =
                        GetScore(gScore, current) +
                        Vector3.Distance(
                            current.transform.position,
                            neighbor.transform.position) *
                        link.Cost;
                    if (tentative >= GetScore(gScore, neighbor))
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative;
                    fScore[neighbor] =
                        tentative + Heuristic(neighbor, goal);
                    if (!open.Contains(neighbor))
                    {
                        open.Add(neighbor);
                    }
                }
            }

            ClearPath();
            return false;
        }

        private void CommitPath(List<BossPathNode> path)
        {
            if (PathEquals(currentPath, path))
            {
                return;
            }

            currentPath.Clear();
            currentPath.AddRange(path);
            BuildSteps();
            pathVersion++;
            PathChanged?.Invoke(currentPath);
            StepsChanged?.Invoke(currentSteps);
        }

        private void BuildSteps()
        {
            currentSteps.Clear();
            for (int index = 0; index < currentPath.Count - 1; index++)
            {
                BossPathNode from = currentPath[index];
                BossPathNode to = currentPath[index + 1];
                BossPathLinkKind kind = BossPathLinkKind.Run;
                float cost = 1f;
                IReadOnlyList<BossPathLink> links = from.Links;
                for (int linkIndex = 0;
                     linkIndex < links.Count;
                     linkIndex++)
                {
                    BossPathLink link = links[linkIndex];
                    if (link != null && link.Target == to)
                    {
                        kind = link.Kind;
                        cost = link.Cost;
                        break;
                    }
                }

                currentSteps.Add(
                    new BossPathStep(from, to, kind, cost));
            }
        }

        private static bool PathEquals(
            IReadOnlyList<BossPathNode> left,
            IReadOnlyList<BossPathNode> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static BossPathNode TakeBest(
            List<BossPathNode> open,
            Dictionary<BossPathNode, float> scores)
        {
            int bestIndex = 0;
            float bestScore = GetScore(scores, open[0]);
            for (int index = 1; index < open.Count; index++)
            {
                float score = GetScore(scores, open[index]);
                if (score < bestScore)
                {
                    bestIndex = index;
                    bestScore = score;
                }
            }

            BossPathNode result = open[bestIndex];
            open.RemoveAt(bestIndex);
            return result;
        }

        private static List<BossPathNode> Reconstruct(
            Dictionary<BossPathNode, BossPathNode> cameFrom,
            BossPathNode current)
        {
            var result = new List<BossPathNode> { current };
            while (cameFrom.TryGetValue(
                       current,
                       out BossPathNode previous))
            {
                current = previous;
                result.Add(current);
            }

            result.Reverse();
            return result;
        }

        private BossPathNode FindNearestNode(
            Vector3 position,
            float maxDistance)
        {
            float best = maxDistance * maxDistance;
            BossPathNode result = null;
            IReadOnlyList<BossPathNode> nodes =
                BossPathNode.Active;
            for (int index = 0; index < nodes.Count; index++)
            {
                BossPathNode node = nodes[index];
                if (node == null)
                {
                    continue;
                }

                float distance =
                    (node.transform.position - position).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    result = node;
                }
            }

            return result;
        }

        private static float Heuristic(
            BossPathNode from,
            BossPathNode to)
        {
            return Vector3.Distance(
                from.transform.position,
                to.transform.position);
        }

        private static float GetScore(
            Dictionary<BossPathNode, float> scores,
            BossPathNode node)
        {
            return scores.TryGetValue(node, out float score)
                ? score
                : float.PositiveInfinity;
        }

        private void ResolveTarget()
        {
            if (!autoResolveTarget)
            {
                return;
            }

            if (target != null &&
                (string.IsNullOrWhiteSpace(targetTag) ||
                 target.CompareTag(targetTag)))
            {
                return;
            }

            PlayerController player =
                ProjectDiscovery.FindFirst<PlayerController>();
            if (player != null &&
                (string.IsNullOrWhiteSpace(targetTag) ||
                 player.CompareTag(targetTag)))
            {
                target = player.transform;
                return;
            }

            GameObject tagged = string.IsNullOrWhiteSpace(targetTag)
                ? null
                : GameObject.FindGameObjectWithTag(targetTag);
            target = tagged != null ? tagged.transform : null;
        }

        private void OnDrawGizmosSelected()
        {
            if (currentPath.Count < 2)
            {
                return;
            }

            Gizmos.color = new Color(.2f, 1f, .45f, .95f);
            for (int index = 0; index < currentPath.Count - 1; index++)
            {
                Gizmos.DrawLine(
                    currentPath[index].transform.position,
                    currentPath[index + 1].transform.position);
            }
        }
    }
}
