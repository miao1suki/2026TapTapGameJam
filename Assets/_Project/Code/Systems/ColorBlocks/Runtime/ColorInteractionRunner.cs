using System;
using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>Executes only explicitly typed flow nodes. Legacy note nodes remain inert.</summary>
    public static class ColorInteractionRunner
    {
        private const int MaxNodesPerEvent = 64;

        public static int Run(ColorTypeDefinition owner, ColorGraphNodeKind trigger,
            ColorBlock self = null, GameObject actor = null, ColorBlock other = null)
        {
            if (owner == null || owner.nodes == null || owner.edges == null) return 0;
            var nodes = new Dictionary<string, ColorGraphNode>(StringComparer.Ordinal);
            var next = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var node in owner.nodes)
                if (node != null && !string.IsNullOrEmpty(node.id) && !nodes.ContainsKey(node.id))
                    nodes.Add(node.id, node);
            foreach (var edge in owner.edges)
            {
                if (edge == null || !nodes.ContainsKey(edge.fromId ?? "") || !nodes.ContainsKey(edge.toId ?? ""))
                    continue;
                if (!next.TryGetValue(edge.fromId, out var outgoing))
                    next.Add(edge.fromId, outgoing = new List<string>());
                outgoing.Add(edge.toId);
            }

            var queue = new Queue<string>();
            var roots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in owner.nodes)
                if (node != null && node.kind == trigger && nodes.ContainsKey(node.id ?? ""))
                {
                    queue.Enqueue(node.id);
                    roots.Add(node.id);
                }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            int actions = 0;
            while (queue.Count > 0 && visited.Count < MaxNodesPerEvent)
            {
                var id = queue.Dequeue();
                if (!visited.Add(id) || !nodes.TryGetValue(id, out var node)) continue;
                if (!roots.Contains(id) && !Execute(node, owner, self, actor, other, ref actions)) continue;
                if (next.TryGetValue(id, out var outgoing))
                    foreach (var target in outgoing) queue.Enqueue(target);
            }
            if (queue.Count > 0)
                Debug.LogWarning("[ColorBlocks] 交互图执行超过 64 个节点；请检查环路或过大的连线图。");
            return actions;
        }

        private static bool Execute(ColorGraphNode node, ColorTypeDefinition owner,
            ColorBlock self, GameObject actor, ColorBlock other, ref int actions)
        {
            var manager = ColorWorldManager.Instance;
            string colorId = string.IsNullOrEmpty(node.colorTypeId) ? owner.id : node.colorTypeId;
            switch (node.kind)
            {
                case ColorGraphNodeKind.RequirePlayer:
                    return actor != null && actor.GetComponentInParent<PlayerController>() != null;
                case ColorGraphNodeKind.RequireSelfBlock:
                    return self != null;
                case ColorGraphNodeKind.RequireOtherColor:
                    return other != null && (other.CurrentColorTypeId ?? other.BaseColorTypeId) == colorId;
                case ColorGraphNodeKind.RecolorSelf:
                    if (self == null || !manager.TryRecolor(self, colorId)) return false;
                    actions++;
                    return true;
                case ColorGraphNodeKind.UnlockColor:
                    if (!manager.Unlock(colorId)) return false;
                    actions++;
                    return true;
                case ColorGraphNodeKind.FadeColor:
                case ColorGraphNodeKind.RestoreColor:
                    if (manager.Catalog?.Find(colorId) == null) return false;
                    HSVColorFadeManager.Instance.SetColorFaded(colorId, node.kind == ColorGraphNodeKind.FadeColor);
                    actions++;
                    return true;
                default:
                    // Notes and event nodes cannot be executed as effects.
                    return false;
            }
        }
    }
}
