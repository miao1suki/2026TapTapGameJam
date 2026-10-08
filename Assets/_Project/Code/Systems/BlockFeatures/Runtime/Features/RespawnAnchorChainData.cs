using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.BlockFeatures
{
    public interface IRespawnAnchorChainProvider
    {
        string RespawnAnchorId { get; }
        IReadOnlyList<RespawnAnchorChainEntry>
            GetRespawnChainSnapshot();
    }

    public interface IRespawnAnchorStateProvider
    {
        string RespawnAnchorId { get; }
        string NextAnchorId { get; }
        int ChainIndex { get; }
        bool IsInitialAnchor { get; }
        bool IsActivated { get; }

        void RestoreSavedState(
            string nextAnchorId,
            int chainIndex,
            bool initialAnchor,
            bool activated);

        void ResetSavedState();
    }

    [Serializable]
    public sealed class RespawnAnchorChainSaveData
    {
        public int version = 1;
        public string sceneName;
        public List<RespawnAnchorChainEntry> anchors =
            new List<RespawnAnchorChainEntry>();
    }

    [Serializable]
    public sealed class RespawnAnchorChainEntry
    {
        public string anchorId;
        public string nextAnchorId;
        public int chainIndex;
        public Vector3 worldPosition;
        public bool initialAnchor;
        public bool activated;

        public RespawnAnchorChainEntry()
        {
        }

        public RespawnAnchorChainEntry(
            string anchorId,
            string nextAnchorId,
            int chainIndex,
            Vector3 worldPosition,
            bool initialAnchor,
            bool activated)
        {
            this.anchorId = anchorId;
            this.nextAnchorId = nextAnchorId;
            this.chainIndex = chainIndex;
            this.worldPosition = worldPosition;
            this.initialAnchor = initialAnchor;
            this.activated = activated;
        }
    }

    public static class RespawnAnchorChainRuntime
    {
        private static readonly List<RespawnAnchorFeature> Registered =
            new List<RespawnAnchorFeature>();
        private static List<RespawnAnchorFeature> cachedOrder;

        internal static void Register(RespawnAnchorFeature anchor)
        {
            if (anchor == null || Registered.Contains(anchor))
            {
                return;
            }

            Registered.Add(anchor);
            Invalidate();
        }

        internal static void Unregister(RespawnAnchorFeature anchor)
        {
            if (anchor == null || !Registered.Remove(anchor))
            {
                return;
            }

            Invalidate();
        }

        public static IReadOnlyList<RespawnAnchorFeature>
            GetOrderedAnchors()
        {
            return cachedOrder ??
                   (cachedOrder = BuildOrder(Registered));
        }

        public static List<RespawnAnchorFeature> BuildOrder(
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            BuildOrderCore(
                anchors,
                out List<RespawnAnchorFeature> sorted,
                out List<RespawnAnchorFeature> mainChain);
            var ordered = new List<RespawnAnchorFeature>(
                sorted.Count);
            ordered.AddRange(mainChain);
            var visited = new HashSet<RespawnAnchorFeature>(
                mainChain);
            for (int index = 0; index < sorted.Count; index++)
            {
                RespawnAnchorFeature candidate = sorted[index];
                if (visited.Add(candidate))
                {
                    ordered.Add(candidate);
                }
            }

            return ordered;
        }

        public static List<RespawnAnchorFeature> BuildMainChain(
            IReadOnlyList<RespawnAnchorFeature> anchors)
        {
            BuildOrderCore(
                anchors,
                out _,
                out List<RespawnAnchorFeature> mainChain);
            return mainChain;
        }

        public static int ResolveIndex(
            RespawnAnchorFeature anchor)
        {
            return anchor != null ? anchor.ChainIndex : -1;
        }

        public static bool IsInitial(
            RespawnAnchorFeature anchor)
        {
            IReadOnlyList<RespawnAnchorFeature> ordered =
                GetOrderedAnchors();
            return ordered.Count > 0 && ordered[0] == anchor;
        }

        public static bool ActivateThrough(
            RespawnAnchorFeature anchor)
        {
            if (anchor == null)
            {
                return false;
            }

            List<RespawnAnchorFeature> mainChain =
                BuildMainChain(Registered);
            int targetIndex = mainChain.IndexOf(anchor);
            if (targetIndex < 0)
            {
                return anchor.Activate();
            }

            bool changed = false;
            for (int index = 0;
                 index <= targetIndex;
                 index++)
            {
                changed |= mainChain[index].Activate();
            }

            return changed;
        }

        public static IReadOnlyList<RespawnAnchorChainEntry>
            CreateSnapshot()
        {
            return CreateSnapshot(GetOrderedAnchors());
        }

        public static IReadOnlyList<RespawnAnchorChainEntry>
            CreateSnapshot(
                IReadOnlyList<RespawnAnchorFeature> ordered)
        {
            var snapshot = new List<RespawnAnchorChainEntry>(
                ordered != null ? ordered.Count : 0);
            if (ordered == null)
            {
                return snapshot;
            }

            for (int index = 0; index < ordered.Count; index++)
            {
                RespawnAnchorFeature anchor = ordered[index];
                snapshot.Add(new RespawnAnchorChainEntry(
                    anchor.AnchorId,
                    anchor.NextAnchorId,
                    anchor.ChainIndex,
                    anchor.transform.position,
                    anchor.IsInitialAnchor,
                    anchor.IsActivated));
            }

            return snapshot;
        }

        public static RespawnAnchorChainSaveData CaptureSaveData()
        {
            return CaptureSaveData(
                SceneManager.GetActiveScene());
        }

        public static RespawnAnchorChainSaveData CaptureSaveData(
            Scene scene)
        {
            List<RespawnAnchorFeature> anchors =
                GetRegisteredAnchors(scene);
            return new RespawnAnchorChainSaveData
            {
                version = 1,
                sceneName = scene.IsValid()
                    ? scene.name
                    : string.Empty,
                anchors = new List<RespawnAnchorChainEntry>(
                    CreateSnapshot(BuildOrder(anchors)))
            };
        }

        public static bool TryRestoreSaveData(
            RespawnAnchorChainSaveData saveData,
            out string error)
        {
            if (saveData == null)
            {
                error = "保存数据为空。";
                return false;
            }

            if (saveData.version != 1)
            {
                error =
                    $"不支持的重生锚保存版本：{saveData.version}。";
                return false;
            }

            List<RespawnAnchorFeature> targets =
                GetRegisteredAnchors(saveData.sceneName);
            if (!TryValidateSaveData(
                    saveData,
                    targets,
                    out Dictionary<string, RespawnAnchorFeature>
                        anchorsById,
                    out error))
            {
                return false;
            }

            for (int index = 0; index < targets.Count; index++)
            {
                targets[index].ResetSavedState();
            }

            for (int index = 0;
                 index < saveData.anchors.Count;
                 index++)
            {
                RespawnAnchorChainEntry entry =
                    saveData.anchors[index];
                if (entry == null ||
                    !anchorsById.TryGetValue(
                        entry.anchorId,
                        out RespawnAnchorFeature anchor))
                {
                    continue;
                }

                anchor.RestoreSavedState(
                    entry.nextAnchorId,
                    entry.chainIndex,
                    entry.initialAnchor,
                    entry.activated);
            }

            Invalidate();
            error = string.Empty;
            return true;
        }

        public static void ResetRuntimeState()
        {
            for (int index = 0; index < Registered.Count; index++)
            {
                Registered[index].ResetSavedState();
            }

            Invalidate();
        }

        internal static void Invalidate()
        {
            cachedOrder = null;
        }

        private static RespawnAnchorFeature FindNext(
            RespawnAnchorFeature anchor,
            IReadOnlyDictionary<string, RespawnAnchorFeature> byId)
        {
            if (anchor == null ||
                string.IsNullOrEmpty(anchor.NextAnchorId))
            {
                return null;
            }

            return byId.TryGetValue(
                anchor.NextAnchorId,
                out RespawnAnchorFeature next)
                ? next
                : null;
        }

        private static void BuildOrderCore(
            IReadOnlyList<RespawnAnchorFeature> anchors,
            out List<RespawnAnchorFeature> sorted,
            out List<RespawnAnchorFeature> mainChain)
        {
            sorted = new List<RespawnAnchorFeature>();
            mainChain = new List<RespawnAnchorFeature>();
            if (anchors == null || anchors.Count == 0)
            {
                return;
            }

            for (int index = 0; index < anchors.Count; index++)
            {
                RespawnAnchorFeature anchor = anchors[index];
                if (anchor == null)
                {
                    continue;
                }

                anchor.EnsureRuntimeAnchorId();
                sorted.Add(anchor);
            }

            sorted.Sort(CompareSceneOrder);
            if (sorted.Count == 0)
            {
                return;
            }

            var byId = new Dictionary<string, RespawnAnchorFeature>(
                StringComparer.Ordinal);
            var referenced = new HashSet<string>(
                StringComparer.Ordinal);
            bool hasLinks = false;
            for (int index = 0; index < sorted.Count; index++)
            {
                RespawnAnchorFeature anchor = sorted[index];
                if (!string.IsNullOrEmpty(anchor.AnchorId))
                {
                    byId[anchor.AnchorId] = anchor;
                }

                if (string.IsNullOrEmpty(anchor.NextAnchorId))
                {
                    continue;
                }

                hasLinks = true;
                referenced.Add(anchor.NextAnchorId);
            }

            if (!hasLinks)
            {
                return;
            }

            RespawnAnchorFeature root = null;
            for (int index = 0; index < sorted.Count; index++)
            {
                RespawnAnchorFeature candidate = sorted[index];
                if (candidate.IsInitialAnchor &&
                    (string.IsNullOrEmpty(candidate.AnchorId) ||
                     !referenced.Contains(candidate.AnchorId)))
                {
                    root = candidate;
                    break;
                }
            }

            if (root == null)
            {
                for (int index = 0; index < sorted.Count; index++)
                {
                    RespawnAnchorFeature candidate = sorted[index];
                    if (string.IsNullOrEmpty(candidate.AnchorId) ||
                        !referenced.Contains(candidate.AnchorId))
                    {
                        root = candidate;
                        break;
                    }
                }
            }

            if (root == null)
            {
                root = sorted[0];
            }

            var visited = new HashSet<RespawnAnchorFeature>();
            RespawnAnchorFeature current = root;
            while (current != null &&
                   visited.Add(current))
            {
                mainChain.Add(current);
                current = FindNext(current, byId);
            }
        }

        private static List<RespawnAnchorFeature> GetRegisteredAnchors(
            Scene scene)
        {
            var result = new List<RespawnAnchorFeature>();
            for (int index = 0; index < Registered.Count; index++)
            {
                RespawnAnchorFeature anchor = Registered[index];
                if (anchor != null && anchor.gameObject.scene == scene)
                {
                    result.Add(anchor);
                }
            }

            return result;
        }

        private static List<RespawnAnchorFeature> GetRegisteredAnchors(
            string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return new List<RespawnAnchorFeature>(Registered);
            }

            var result = new List<RespawnAnchorFeature>();
            for (int index = 0; index < Registered.Count; index++)
            {
                RespawnAnchorFeature anchor = Registered[index];
                if (anchor != null &&
                    anchor.gameObject.scene.name == sceneName)
                {
                    result.Add(anchor);
                }
            }

            return result;
        }

        private static bool TryValidateSaveData(
            RespawnAnchorChainSaveData saveData,
            IReadOnlyList<RespawnAnchorFeature> targets,
            out Dictionary<string, RespawnAnchorFeature> anchorsById,
            out string error)
        {
            anchorsById =
                new Dictionary<string, RespawnAnchorFeature>(
                    StringComparer.Ordinal);
            for (int index = 0; index < targets.Count; index++)
            {
                RespawnAnchorFeature anchor = targets[index];
                anchor.EnsureRuntimeAnchorId();
                if (anchorsById.ContainsKey(anchor.AnchorId))
                {
                    error =
                        $"场景中存在重复出生点 ID：{anchor.AnchorId}。";
                    return false;
                }

                anchorsById[anchor.AnchorId] = anchor;
            }

            var entriesById =
                new Dictionary<string, RespawnAnchorChainEntry>(
                    StringComparer.Ordinal);
            for (int index = 0;
                 index < saveData.anchors.Count;
                 index++)
            {
                RespawnAnchorChainEntry entry =
                    saveData.anchors[index];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.anchorId))
                {
                    error = "保存数据包含空出生点 ID。";
                    return false;
                }

                if (entriesById.ContainsKey(entry.anchorId))
                {
                    error =
                        $"保存数据包含重复出生点 ID：{entry.anchorId}。";
                    return false;
                }

                if (!anchorsById.ContainsKey(entry.anchorId))
                {
                    error =
                        $"保存数据中的出生点不存在于当前场景：{entry.anchorId}。";
                    return false;
                }

                entriesById[entry.anchorId] = entry;
            }

            var incomingCount =
                new Dictionary<string, int>(
                    StringComparer.Ordinal);
            var linkedIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0;
                 index < saveData.anchors.Count;
                 index++)
            {
                RespawnAnchorChainEntry entry =
                    saveData.anchors[index];
                if (string.IsNullOrWhiteSpace(entry.nextAnchorId))
                {
                    continue;
                }

                if (!entriesById.ContainsKey(entry.nextAnchorId))
                {
                    error =
                        $"保存数据缺少连接目标：{entry.nextAnchorId}。";
                    return false;
                }

                linkedIds.Add(entry.anchorId);
                linkedIds.Add(entry.nextAnchorId);
                incomingCount.TryGetValue(
                    entry.nextAnchorId,
                    out int count);
                incomingCount[entry.nextAnchorId] = count + 1;
            }

            if (linkedIds.Count == 0)
            {
                error = string.Empty;
                return true;
            }

            string rootId = string.Empty;
            int rootCount = 0;
            foreach (string id in linkedIds)
            {
                if (incomingCount.TryGetValue(id, out int count) &&
                    count > 1)
                {
                    error = $"出生点存在多条入口：{id}。";
                    return false;
                }

                if (count == 0)
                {
                    rootId = id;
                    rootCount++;
                }
            }

            if (rootCount != 1)
            {
                error = "保存数据只能包含一条出生点链表。";
                return false;
            }

            var visited = new HashSet<string>(
                StringComparer.Ordinal);
            string currentId = rootId;
            while (!string.IsNullOrEmpty(currentId) &&
                   visited.Add(currentId))
            {
                if (!entriesById.TryGetValue(
                        currentId,
                        out RespawnAnchorChainEntry current))
                {
                    error = $"出生点连接数据缺失：{currentId}。";
                    return false;
                }

                currentId = current.nextAnchorId;
            }

            if (visited.Count != linkedIds.Count)
            {
                error = "保存数据包含循环或多条链表。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static int CompareSceneOrder(
            RespawnAnchorFeature left,
            RespawnAnchorFeature right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            Vector3 leftPosition = left.transform.position;
            Vector3 rightPosition = right.transform.position;
            int result = leftPosition.x.CompareTo(rightPosition.x);
            if (result != 0)
            {
                return result;
            }

            result = leftPosition.y.CompareTo(rightPosition.y);
            return result != 0
                ? result
                : left.GetInstanceID().CompareTo(
                    right.GetInstanceID());
        }
    }
}
