using System.Collections.Generic;
using Project.LevelEditor;
using UnityEngine;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/Boss/自动寻路烘焙器")]
    public sealed class BossNavAutoBaker : MonoBehaviour
    {
        [Header("烘焙")]
        [SerializeField] private bool bakeOnStart = true;
        [SerializeField] private bool bakeOnlyWhenNoManualNodes = true;
        [SerializeField, Min(.1f)] private float fallbackCellSize = 1f;

        [Header("Boss 体型")]
        [SerializeField, Min(.1f)] private float agentWidth = 3f;
        [SerializeField, Min(.1f)] private float agentHeight = 3f;
        [SerializeField, Min(.05f)] private float clearanceShrink = .08f;

        [Header("可通行范围")]
        [SerializeField, Min(0f)] private float stepUpHeight = 1.1f;
        [SerializeField, Min(0f)] private float jumpUpHeight = 4.5f;
        [SerializeField, Min(0f)] private float jumpHorizontalDistance = 4.5f;
        [SerializeField, Min(0f)] private float dropHeight = 12f;
        [SerializeField, Min(0f)] private float dropHorizontalDistance = 5f;
        [SerializeField, Min(.1f)] private float runHorizontalDistance = 1.5f;
        [SerializeField, Min(1)] private int maxEdgesPerNode = 10;
        [SerializeField, Min(1)] private int maxGeneratedNodes = 2000;

        [Header("运行时")]
        [SerializeField] private bool logSummary;

        private Transform runtimeRoot;

        public Transform RuntimeRoot => runtimeRoot;

        private void Start()
        {
            if (bakeOnStart)
            {
                Bake();
            }
        }

        private void OnDestroy()
        {
            ClearRuntimeGraph();
        }

        [ContextMenu("立即烘焙当前场景")]
        public void Bake()
        {
            if (bakeOnlyWhenNoManualNodes &&
                BossPathNode.Active.Count > 0)
            {
                return;
            }

            ClearRuntimeGraph();

            IReadOnlyList<LevelEditorPlacedBlock> blocks =
                LevelEditorPlacedBlock.ActiveInstances;
            if (blocks == null || blocks.Count == 0)
            {
                if (logSummary)
                {
                    Debug.LogWarning(
                        "[BossNavAutoBaker] 场景中没有 LevelEditorPlacedBlock。",
                        this);
                }
                return;
            }

            float cellSize = ResolveCellSize(blocks);
            var positions = new List<Vector3>();
            var occupied = new Dictionary<Vector2Int, int>();
            int nodeLimit = Mathf.Max(1, maxGeneratedNodes);

            for (int blockIndex = 0;
                 blockIndex < blocks.Count;
                 blockIndex++)
            {
                LevelEditorPlacedBlock block = blocks[blockIndex];
                if (block == null ||
                    block.IsProp ||
                    !block.isActiveAndEnabled)
                {
                    continue;
                }

                Collider blockCollider =
                    block.GetComponent<Collider>();
                if (blockCollider == null ||
                    !blockCollider.enabled)
                {
                    continue;
                }

                Bounds bounds = blockCollider.bounds;
                float startX = Mathf.Floor(
                    (bounds.min.x - transform.position.x) / cellSize) *
                    cellSize + transform.position.x + cellSize * .5f;
                float endX = bounds.max.x - cellSize * .1f;
                for (float x = startX;
                     x <= endX && positions.Count < nodeLimit;
                     x += cellSize)
                {
                    Vector3 candidate = new Vector3(
                        x,
                        bounds.max.y + agentHeight * .5f + clearanceShrink,
                        0f);
                    Vector2Int key = new Vector2Int(
                        Mathf.RoundToInt(candidate.x / cellSize),
                        Mathf.RoundToInt(candidate.y / cellSize));
                    if (occupied.ContainsKey(key) ||
                        !HasClearance(candidate, blockCollider))
                    {
                        continue;
                    }

                    occupied[key] = positions.Count;
                    positions.Add(candidate);
                }
            }

            if (positions.Count == 0)
            {
                if (logSummary)
                {
                    Debug.LogWarning(
                        "[BossNavAutoBaker] 没有生成可用 Boss 节点。",
                        this);
                }
                return;
            }

            runtimeRoot = new GameObject("BossNavBake_Runtime").transform;
            runtimeRoot.SetParent(null, true);
            var nodes = new List<BossPathNode>(positions.Count);
            for (int index = 0; index < positions.Count; index++)
            {
                GameObject nodeObject = new GameObject(
                    $"BossNode_{index:000}");
                nodeObject.transform.SetParent(runtimeRoot, false);
                nodeObject.transform.position = positions[index];
                nodes.Add(nodeObject.AddComponent<BossPathNode>());
            }

            int edgeCount = 0;
            for (int fromIndex = 0;
                 fromIndex < nodes.Count;
                 fromIndex++)
            {
                BossPathNode from = nodes[fromIndex];
                int added = 0;
                for (int toIndex = 0;
                     toIndex < nodes.Count &&
                     added < maxEdgesPerNode;
                     toIndex++)
                {
                    if (fromIndex == toIndex)
                    {
                        continue;
                    }

                    BossPathNode to = nodes[toIndex];
                    Vector3 delta =
                        to.transform.position - from.transform.position;
                    float horizontal = Mathf.Abs(delta.x);
                    float vertical = delta.y;
                    if (TryResolveKind(
                            horizontal,
                            vertical,
                            out BossPathLinkKind kind))
                    {
                        from.AddLink(to, kind);
                        added++;
                        edgeCount++;
                    }
                }
            }

            if (logSummary)
            {
                Debug.Log(
                    $"[BossNavAutoBaker] 烘焙完成：{nodes.Count} 个节点，" +
                    $"{edgeCount} 条有向边。",
                    this);
            }
        }

        public void ClearRuntimeGraph()
        {
            if (runtimeRoot != null)
            {
                Destroy(runtimeRoot.gameObject);
                runtimeRoot = null;
            }
        }

        private bool TryResolveKind(
            float horizontal,
            float vertical,
            out BossPathLinkKind kind)
        {
            kind = BossPathLinkKind.Run;
            if (horizontal <= runHorizontalDistance &&
                Mathf.Abs(vertical) <= stepUpHeight)
            {
                return true;
            }

            if (vertical > stepUpHeight &&
                vertical <= jumpUpHeight &&
                horizontal <= jumpHorizontalDistance)
            {
                kind = BossPathLinkKind.Jump;
                return true;
            }

            if (vertical < -stepUpHeight &&
                vertical >= -dropHeight &&
                horizontal <= dropHorizontalDistance)
            {
                kind = BossPathLinkKind.Drop;
                return true;
            }

            return false;
        }

        private bool HasClearance(
            Vector3 position,
            Collider blockCollider)
        {
            Vector3 halfExtents = new Vector3(
                Mathf.Max(.1f, agentWidth * .5f - clearanceShrink),
                Mathf.Max(.1f, agentHeight * .5f - clearanceShrink),
                .25f);
            Collider[] hits = Physics.OverlapBox(
                position,
                halfExtents,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < hits.Length; index++)
            {
                Collider hit = hits[index];
                if (hit == null ||
                    hit == blockCollider ||
                    hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private float ResolveCellSize(
            IReadOnlyList<LevelEditorPlacedBlock> blocks)
        {
            for (int index = 0; index < blocks.Count; index++)
            {
                if (blocks[index] != null &&
                    blocks[index].CellWorldSize > 0f)
                {
                    return blocks[index].CellWorldSize;
                }
            }

            return Mathf.Max(.1f, fallbackCellSize);
        }
    }
}
