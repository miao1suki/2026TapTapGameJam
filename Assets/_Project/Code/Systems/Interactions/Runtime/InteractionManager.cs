using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.Interactions
{
    /// <summary>
    /// 物体级交互图运行时。图只负责描述关系，热点执行由这里集中调度。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class InteractionManager : MonoBehaviour
    {
        private const int MaxNodesPerEvent = 256;
        private static InteractionManager instance;
        private readonly HashSet<IInteractionObjectSource> sources =
            new HashSet<IInteractionObjectSource>();
        private readonly Dictionary<int, int> stayFrames =
            new Dictionary<int, int>();
        private readonly Dictionary<InteractionObjectDefinition, CompiledGraph>
            compiledGraphs =
            new Dictionary<InteractionObjectDefinition, CompiledGraph>();
        private InteractionObjectCatalog catalog;

        private sealed class CompiledGraph
        {
            internal readonly Dictionary<string, InteractionGraphNode> Nodes =
                new Dictionary<string, InteractionGraphNode>(StringComparer.Ordinal);
            internal readonly Dictionary<string, List<string>> Next =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
            internal readonly Dictionary<InteractionNodeKind, List<string>> Roots =
                new Dictionary<InteractionNodeKind, List<string>>();
        }

        public static InteractionManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                instance = ProjectDiscovery.FindFirst<InteractionManager>();
                if (instance != null)
                {
                    return instance;
                }

                GameObject root = new GameObject("Interaction Manager");
                instance = root.AddComponent<InteractionManager>();
                return instance;
            }
        }

        public static InteractionManager Existing => instance;
        public InteractionObjectCatalog Catalog => catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            compiledGraphs.Clear();
            catalog = Resources.Load<InteractionObjectCatalog>(
                "Interactions/InteractionObjectCatalog");
        }

        internal static void Register(IInteractionObjectSource source)
        {
            if (source == null || source.Definition == null)
            {
                return;
            }

            Instance.sources.Add(source);
            InteractionManager.ScheduleTimeTriggers(source);
        }

        internal static void Unregister(IInteractionObjectSource source)
        {
            if (instance == null || source == null)
            {
                return;
            }

            instance.sources.Remove(source);
        }

        internal static void ScheduleTimeTriggers(IInteractionObjectSource source)
        {
            if (source?.Definition == null)
            {
                return;
            }

            InteractionManager manager = Instance;
            for (int index = 0; index < source.Definition.Nodes.Count; index++)
            {
                InteractionGraphNode node = source.Definition.Nodes[index];
                if (node == null ||
                    node.kind != InteractionNodeKind.TimeElapsed ||
                    node.number <= 0f)
                {
                    continue;
                }

                manager.StartCoroutine(
                    manager.RunAfterDelay(source, node.number));
            }
        }

        internal static void Raise(
            IInteractionObjectSource source,
            InteractionNodeKind trigger,
            GameObject actor = null,
            IInteractionObjectSource other = null)
        {
            if (instance == null || source?.Definition == null)
            {
                return;
            }

            int actorId = actor != null ? actor.GetInstanceID() : 0;
            int otherId = other?.InteractionComponent != null
                ? other.InteractionComponent.GetInstanceID()
                : other?.InteractionGameObject != null
                    ? other.InteractionGameObject.GetInstanceID()
                    : 0;
            int sourceId = source.InteractionComponent != null
                ? source.InteractionComponent.GetInstanceID()
                : source.InteractionGameObject.GetInstanceID();
            int key = HashCode.Combine(sourceId, (int)trigger, actorId, otherId);
            if (trigger == InteractionNodeKind.ObjectStay)
            {
                if (instance.stayFrames.TryGetValue(key, out int frame) &&
                    frame == Time.frameCount)
                {
                    return;
                }

                instance.stayFrames[key] = Time.frameCount;
            }

            instance.StartCoroutine(
                instance.RunGraph(source, trigger, actor, other));
        }

        /// <summary>
        /// 给脚本或 Timeline 调用的手动入口。编辑器连线仍然是逻辑的唯一配置来源。
        /// </summary>
        public static bool Trigger(
            GameObject target,
            InteractionNodeKind trigger = InteractionNodeKind.Manual,
            GameObject actor = null,
            GameObject other = null)
        {
            IInteractionObjectSource source = ResolveSource(target);
            if (source == null || source.Definition == null)
            {
                return false;
            }

            if (instance == null || !instance.sources.Contains(source))
            {
                return false;
            }

            IInteractionObjectSource otherSource = ResolveSource(other);
            Raise(source, trigger, actor, otherSource);
            return true;
        }

        private IEnumerator RunAfterDelay(
            IInteractionObjectSource source,
            float delay)
        {
            yield return new WaitForSeconds(delay);
            if (source != null && sources.Contains(source))
            {
                yield return RunGraph(
                    source,
                    InteractionNodeKind.TimeElapsed,
                    null,
                    null);
            }
        }

        private IEnumerator RunGraph(
            IInteractionObjectSource source,
            InteractionNodeKind trigger,
            GameObject actor,
            IInteractionObjectSource other)
        {
            if (source == null || !sources.Contains(source))
                yield break;
            CompiledGraph graph = GetCompiledGraph(source.Definition);
            if (graph == null ||
                !graph.Roots.TryGetValue(trigger, out List<string> rootIds))
            {
                yield break;
            }

            var queue = new Queue<string>(rootIds);
            var roots = new HashSet<string>(rootIds, StringComparer.Ordinal);

            var visited = new HashSet<string>(StringComparer.Ordinal);
            int processed = 0;
            while (queue.Count > 0 && processed < MaxNodesPerEvent)
            {
                string id = queue.Dequeue();
                if (!visited.Add(id) || !graph.Nodes.TryGetValue(id, out InteractionGraphNode node))
                {
                    continue;
                }

                processed++;
                if (!roots.Contains(id))
                {
                    if (node.kind == InteractionNodeKind.Delay)
                    {
                        if (node.number > 0f)
                        {
                            yield return new WaitForSeconds(node.number);
                        }
                    }
                    else if (!Execute(node, source, actor, other))
                    {
                        continue;
                    }
                }

                if (graph.Next.TryGetValue(id, out List<string> outgoing))
                {
                    for (int index = 0; index < outgoing.Count; index++)
                    {
                        queue.Enqueue(outgoing[index]);
                    }
                }
            }

            if (queue.Count > 0)
            {
                Debug.LogWarning(
                    $"[Interactions] 物体“{source.Definition.DisplayName}”的交互图超过 {MaxNodesPerEvent} 个节点，已停止本次执行。",
                    source.InteractionGameObject);
            }
        }

        private CompiledGraph GetCompiledGraph(InteractionObjectDefinition definition)
        {
            if (definition == null) return null;
            if (compiledGraphs.TryGetValue(definition, out CompiledGraph cached))
                return cached;

            CompiledGraph graph = new CompiledGraph();
            for (int index = 0; index < definition.Nodes.Count; index++)
            {
                InteractionGraphNode node = definition.Nodes[index];
                if (node != null && !string.IsNullOrWhiteSpace(node.id))
                    graph.Nodes[node.id] = node;
            }

            for (int index = 0; index < definition.Edges.Count; index++)
            {
                InteractionGraphEdge edge = definition.Edges[index];
                if (edge == null ||
                    !graph.Nodes.ContainsKey(edge.fromId ?? string.Empty) ||
                    !graph.Nodes.ContainsKey(edge.toId ?? string.Empty))
                    continue;
                if (!graph.Next.TryGetValue(edge.fromId, out List<string> outgoing))
                {
                    outgoing = new List<string>();
                    graph.Next.Add(edge.fromId, outgoing);
                }
                outgoing.Add(edge.toId);
            }

            foreach (KeyValuePair<string, InteractionGraphNode> pair in graph.Nodes)
            {
                InteractionNodeKind kind = pair.Value.kind;
                if (!graph.Roots.TryGetValue(kind, out List<string> roots))
                {
                    roots = new List<string>();
                    graph.Roots.Add(kind, roots);
                }
                roots.Add(pair.Key);
            }

            compiledGraphs.Add(definition, graph);
            return graph;
        }

        private static bool Execute(
            InteractionGraphNode node,
            IInteractionObjectSource source,
            GameObject actor,
            IInteractionObjectSource other)
        {
            switch (node.kind)
            {
                case InteractionNodeKind.RequireSelf:
                    return source != null;
                case InteractionNodeKind.RequirePlayer:
                    return actor != null &&
                           actor.GetComponentInParent<PlayerController>() != null;
                case InteractionNodeKind.RequireOtherObject:
                    return other != null;
                case InteractionNodeKind.RequireObjectId:
                    return other?.Definition != null &&
                           other.Definition.ObjectId == node.value;
                case InteractionNodeKind.RequireOtherColor:
                    return other != null &&
                           string.Equals(
                               string.IsNullOrWhiteSpace(other.CurrentColorTypeId)
                                   ? other.BaseColorTypeId
                                   : other.CurrentColorTypeId,
                               node.value,
                               StringComparison.OrdinalIgnoreCase);
                case InteractionNodeKind.RequireColor:
                    return ResolveColor(actor, other) == node.value;
                case InteractionNodeKind.Fade:
                    return SetFade(node, source, true);
                case InteractionNodeKind.Restore:
                    return SetFade(node, source, false);
                case InteractionNodeKind.PlayTimeline:
                    return PlayTimeline(node, source, actor);
                case InteractionNodeKind.InvokeMethod:
                    return InvokeMethod(node, source, actor, other);
                case InteractionNodeKind.SetColor:
                    return source != null && source.SetCurrentColor(node.value);
                case InteractionNodeKind.Log:
                    Debug.Log(node.value ?? string.Empty, source.InteractionGameObject);
                    return true;
                case InteractionNodeKind.Delay:
                    return true;
                default:
                    return false;
            }
        }

        private static string ResolveColor(
            GameObject actor,
            IInteractionObjectSource other)
        {
            IInteractionObjectSource source = other;
            if (source == null && actor != null)
            {
                source = ResolveSource(actor);
            }

            if (source == null) return string.Empty;
            return string.IsNullOrWhiteSpace(source.CurrentColorTypeId)
                ? source.BaseColorTypeId ?? string.Empty
                : source.CurrentColorTypeId;
        }

        private static IInteractionObjectSource ResolveSource(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            InteractionObject interactionObject =
                target.GetComponentInParent<InteractionObject>();
            if (interactionObject != null)
            {
                return interactionObject;
            }

            ColorBlock colorBlock =
                target.GetComponentInParent<ColorBlock>();
            return colorBlock;
        }

        private static bool SetFade(
            InteractionGraphNode node,
            IInteractionObjectSource source,
            bool faded)
        {
            string colorId = string.IsNullOrWhiteSpace(node.value)
                ? source == null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(source.CurrentColorTypeId)
                        ? source.BaseColorTypeId
                        : source.CurrentColorTypeId
                : node.value;
            if (string.IsNullOrWhiteSpace(colorId))
            {
                return false;
            }

            HSVColorFadeManager.Instance.SetColorFaded(
                colorId,
                faded,
                Mathf.Max(0f, node.number));
            return true;
        }

        private static bool PlayTimeline(
            InteractionGraphNode node,
            IInteractionObjectSource source,
            GameObject actor)
        {
            PlayableDirector director =
                node.reference as PlayableDirector;
            if (director == null)
            {
                GameObject target = source?.InteractionGameObject;
                director = target != null
                    ? target.GetComponent<PlayableDirector>()
                    : null;
            }

            if (director == null)
            {
                GameObject target = actor;
                director = target != null
                    ? target.GetComponentInParent<PlayableDirector>()
                    : null;
            }

            if (director == null)
            {
                return false;
            }

            PlayableAsset asset = node.reference as PlayableAsset;
            if (asset != null)
            {
                director.playableAsset = asset;
            }

            director.Play();
            return true;
        }

        private static bool InvokeMethod(
            InteractionGraphNode node,
            IInteractionObjectSource source,
            GameObject actor,
            IInteractionObjectSource other)
        {
            if (source?.InteractionGameObject == null ||
                string.IsNullOrWhiteSpace(node.value))
            {
                return false;
            }

            MonoBehaviour[] behaviours =
                source.InteractionGameObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null)
                {
                    continue;
                }

                BindingFlags flags = BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;
                GameObject contextTarget = actor ?? other?.InteractionGameObject;
                Type[] parameterTypes = string.IsNullOrEmpty(node.argument)
                    ? contextTarget != null
                        ? new[] { typeof(GameObject) }
                        : Type.EmptyTypes
                    : new[] { typeof(string) };
                MethodInfo method = behaviour.GetType().GetMethod(
                    node.value,
                    flags,
                    null,
                    parameterTypes,
                    null);
                if (method == null)
                {
                    continue;
                }

                try
                {
                    object[] arguments = string.IsNullOrEmpty(node.argument)
                        ? contextTarget != null
                            ? new object[] { contextTarget }
                            : null
                        : new object[] { node.argument };
                    method.Invoke(behaviour, arguments);
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, behaviour);
                    return false;
                }
            }

            Debug.LogWarning(
                $"[Interactions] 未找到可调用方法：{node.value}",
                source.InteractionGameObject);
            return false;
        }
    }
}
