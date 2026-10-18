using System.Collections.Generic;
using Project.BlockFeatures;
using UnityEngine;

namespace Project.Mechanisms
{
    /// <summary>Runtime consumer registration, never a persisted base-to-platform binding list.</summary>
    internal static class MechanismTimelineSequence
    {
        private static readonly Dictionary<MechanismBaseFeature, List<MovingPlatformFeature>> consumers =
            new Dictionary<MechanismBaseFeature, List<MovingPlatformFeature>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => consumers.Clear();

        internal static void Register(MechanismBaseFeature source, MovingPlatformFeature platform)
        {
            if (source == null || platform == null) return;
            if (!consumers.TryGetValue(source, out var list))
                consumers.Add(source, list = new List<MovingPlatformFeature>());
            if (!list.Contains(platform)) list.Add(platform);
        }

        internal static void Unregister(MechanismBaseFeature source, MovingPlatformFeature platform)
        {
            if (ReferenceEquals(source, null)) return;
            platform?.CancelTimelineFrom(source);
            if (!consumers.TryGetValue(source, out var list)) return;
            list.Remove(platform);
            if (list.Count == 0) consumers.Remove(source);
        }

        internal static void Cancel(MechanismBaseFeature source)
        {
            if (source == null || !consumers.TryGetValue(source, out var list)) return;
            foreach (var platform in list) if (platform != null) platform.CancelTimelineFrom(source);
        }

        internal static void SynchronizeSignalState(MechanismBaseFeature source)
        {
            if (source == null || !consumers.TryGetValue(source, out var list)) return;
            foreach (var platform in list) if (platform != null) platform.TakeSignalTimelineEdge();
        }

        internal static void SignalChanged(MechanismBaseFeature source, bool powered)
        {
            source.RequestSignalTimeline(powered);
            if (!consumers.TryGetValue(source, out var list)) return;
            // Snapshot only on a real signal edge. Evaluate combined Any/All before claiming a shot.
            var eligible = new List<MovingPlatformFeature>();
            foreach (var platform in list)
                if (platform != null && platform.TakeSignalTimelineEdge()) eligible.Add(platform);
            eligible.Sort(Compare);
            int count = source.PlatformTimelineMode == MechanismPlatformTimelineMode.FirstOnly
                ? Mathf.Min(1, eligible.Count) : eligible.Count;
            for (int index = 0; index < count; index++) eligible[index].RequestTimelineFrom(source);
        }

        internal static void Request(MovingPlatformFeature platform)
        {
            MechanismBaseFeature source = platform.TimelineBase;
            if (source != null && source.PlatformTimelineMode == MechanismPlatformTimelineMode.FirstOnly &&
                consumers.TryGetValue(source, out var list))
            {
                MovingPlatformFeature first = null;
                foreach (var candidate in list)
                    if (candidate != null && candidate.IsTimelineAvailable && (first == null || Compare(candidate, first) < 0))
                        first = candidate;
                if (first != platform) return;
            }
            platform.RequestTimelineFrom(source);
        }

        private static int Compare(MovingPlatformFeature left, MovingPlatformFeature right)
        {
            int result = left.TimelineOrder.CompareTo(right.TimelineOrder);
            if (result != 0) return result;
            // Explicit sequence numbers are recommended; sibling order is a deterministic tie-break.
            result = left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
            return result != 0 ? result : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }
    }
}
