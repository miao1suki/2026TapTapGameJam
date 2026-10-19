using Project.BlockFeatures;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    internal readonly struct BossTimelinePerformanceFrame
    {
        private readonly Transform anchor;

        private BossTimelinePerformanceFrame(Transform anchor)
        {
            this.anchor = anchor;
        }

        public bool IsValid => anchor != null;

        public Transform Anchor => anchor;

        public Vector3 ToWorldPoint(Vector3 localPoint)
        {
            return anchor != null
                ? anchor.TransformPoint(localPoint)
                : localPoint;
        }

        public Vector3 ToWorldOffset(Vector3 localOffset)
        {
            return anchor != null
                ? anchor.TransformVector(localOffset)
                : localOffset;
        }

        public Vector3 ToLocalPoint(Vector3 worldPoint)
        {
            return anchor != null
                ? anchor.InverseTransformPoint(worldPoint)
                : worldPoint;
        }

        public static PlayableDirector ResolveDirector(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector director = null;
            if (graph.IsValid())
            {
                director =
                    graph.GetResolver() as PlayableDirector;
            }

            if (director == null && owner != null)
            {
                director =
                    owner.GetComponentInParent<
                        PlayableDirector>();
            }

            return director;
        }

        public static bool TryResolve(
            PlayableDirector director,
            string ownerName,
            out BossTimelinePerformanceFrame frame)
        {
            frame = default;
            if (director == null)
            {
                Debug.LogError(
                    $"[{ownerName}] 找不到 owning PlayableDirector，演出已停止。");
                return false;
            }

            ContactTimelineTrigger trigger =
                director.GetComponent<
                    ContactTimelineTrigger>();
            if (trigger == null)
            {
                trigger =
                    director.GetComponentInParent<
                        ContactTimelineTrigger>();
            }

            Transform anchor = trigger != null
                ? trigger.transform
                : director.transform;
            if (anchor == null)
            {
                Debug.LogError(
                    $"[{ownerName}] Timeline 没有可用的表演坐标系。",
                    director);
                return false;
            }

            frame = new BossTimelinePerformanceFrame(
                anchor);
            return true;
        }
    }
}
