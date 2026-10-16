using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.BossLogic
{
    public enum BossPathLinkKind
    {
        [InspectorName("跑动")]
        Run = 0,
        [InspectorName("爬升")]
        Climb = 1,
        [InspectorName("跳跃")]
        Jump = 2,
        [InspectorName("下落")]
        Drop = 3,
        [InspectorName("乘平台")]
        Ride = 4,
        [InspectorName("危险通过")]
        Hazard = 5,
        [InspectorName("自定义")]
        Custom = 6
    }

    [Serializable]
    public sealed class BossPathLink
    {
        [Tooltip("有向边的目标节点。")]
        [SerializeField] private BossPathNode target;
        [SerializeField] private BossPathLinkKind kind =
            BossPathLinkKind.Run;
        [Tooltip("相对基础距离的额外成本；1 表示按正常距离计算。")]
        [SerializeField, Min(.01f)] private float cost = 1f;

        public BossPathNode Target => target;
        public BossPathLinkKind Kind => kind;
        public float Cost => Mathf.Max(.01f, cost);

        public void SetTarget(BossPathNode value)
        {
            target = value;
        }

        public void Configure(
            BossPathNode value,
            BossPathLinkKind linkKind,
            float linkCost)
        {
            target = value;
            kind = linkKind;
            cost = Mathf.Max(.01f, linkCost);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/Boss/寻路节点")]
    public sealed class BossPathNode : MonoBehaviour
    {
        private static readonly List<BossPathNode> ActiveNodes =
            new List<BossPathNode>();

        [SerializeField] private List<BossPathLink> links =
            new List<BossPathLink>();

        public static IReadOnlyList<BossPathNode> Active =>
            ActiveNodes;

        public IReadOnlyList<BossPathLink> Links => links;

        private void OnEnable()
        {
            if (!ActiveNodes.Contains(this))
            {
                ActiveNodes.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveNodes.Remove(this);
        }

        public void AddLink(
            BossPathNode target,
            BossPathLinkKind kind = BossPathLinkKind.Run,
            float cost = 1f)
        {
            if (target == null || target == this)
            {
                return;
            }

            for (int index = 0; index < links.Count; index++)
            {
                if (links[index] != null &&
                    links[index].Target == target)
                {
                    links[index].Configure(
                        target,
                        kind,
                        cost);
                    return;
                }
            }

            var link = new BossPathLink();
            link.Configure(target, kind, cost);
            links.Add(link);
        }

        private void OnValidate()
        {
            for (int index = links.Count - 1;
                 index >= 0;
                 index--)
            {
                if (links[index] == null)
                {
                    links.RemoveAt(index);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(.2f, .9f, 1f, .9f);
            Gizmos.DrawWireSphere(transform.position, .12f);
            if (links == null)
            {
                return;
            }

            for (int index = 0; index < links.Count; index++)
            {
                BossPathLink link = links[index];
                if (link == null || link.Target == null)
                {
                    continue;
                }

                Gizmos.color = ColorFor(link.Kind);
                Gizmos.DrawLine(
                    transform.position,
                    link.Target.transform.position);
                Vector3 direction =
                    link.Target.transform.position - transform.position;
                if (direction.sqrMagnitude > .001f)
                {
                    Vector3 midpoint =
                        transform.position + direction * .5f;
                    Gizmos.DrawWireSphere(midpoint, .08f);
                }
            }
        }

        private static Color ColorFor(BossPathLinkKind kind)
        {
            return kind switch
            {
                BossPathLinkKind.Climb =>
                    new Color(.3f, 1f, .45f, .95f),
                BossPathLinkKind.Jump =>
                    new Color(1f, .82f, .2f, .95f),
                BossPathLinkKind.Drop =>
                    new Color(1f, .45f, .2f, .95f),
                BossPathLinkKind.Ride =>
                    new Color(.75f, .45f, 1f, .95f),
                BossPathLinkKind.Hazard =>
                    new Color(1f, .2f, .2f, .95f),
                BossPathLinkKind.Custom =>
                    new Color(1f, 1f, 1f, .9f),
                _ => new Color(.3f, .75f, 1f, .95f)
            };
        }
    }
}
