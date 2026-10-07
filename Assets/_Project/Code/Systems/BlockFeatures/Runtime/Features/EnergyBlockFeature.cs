using System.Collections.Generic;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "能源方块",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Mechanism,
        Interactions = BlockFeatureInteraction.RoomReset,
        Provides = new[] { typeof(IEnergySource) },
        Writes = new[] { BlockChannel.Power })]
    public sealed class EnergyBlockFeature : BlockFeature,
        IEnergySource,
        IPlayerCarryTarget
    {
        [BlockParameter(
            Label = "允许迁移",
            Group = "能源设置",
            Order = 0,
            Tooltip = "全游戏只有能源方块可以在规则允许的玩法中迁移位置。")]
        [SerializeField] private bool canMigrate = true;

        [BlockParameter(
            Label = "长按拿起时间（秒）",
            Group = "拖拽设置",
            Order = 0,
            Tooltip = "长按交互键达到该时间后进入拖拽。")]
        [SerializeField, Min(0f)] private float holdDurationSeconds = .8f;

        [BlockParameter(
            Label = "原位发散搜索半径（格）",
            Group = "拖拽设置",
            Order = 1,
            Tooltip = "目标格和原位都无效时，从原位向外寻找新的合法格。")]
        [SerializeField, Min(1)] private int fallbackSearchRadiusBlocks = 4;

        private readonly Collider[] overlapBuffer =
            new Collider[32];

        private PlayerController dragPlayer;
        private Vector3 originalPosition;
        private Vector3Int originalCell;
        private float holdStartedAt = -1f;
        private bool dragging;
        private Collider[] cachedColliders;
        private Renderer[] cachedRenderers;
        private int[] originalSortingLayerIds;
        private int[] originalSortingOrders;

        public bool IsEnergyAvailable => isActiveAndEnabled;
        public bool CanMigrate => canMigrate;
        public bool IsDragging => dragging;

        protected override void OnDetach()
        {
            if (dragging)
            {
                CancelPointerDrag();
            }
        }

        protected override void OnResetForRoom()
        {
            if (dragging)
            {
                CancelPointerDrag();
            }
        }

        public bool CanCarry(PlayerController player)
        {
            return isActiveAndEnabled &&
                   canMigrate &&
                   !dragging &&
                   player != null;
        }

        public void BeginCarry(
            PlayerController player,
            Ray pointerRay)
        {
            if (!CanCarry(player))
            {
                return;
            }

            dragPlayer = player;
            holdStartedAt = Time.time;
        }

        public void UpdateCarry(
            PlayerController player,
            Ray pointerRay)
        {
            if (dragPlayer != player)
            {
                return;
            }

            if (dragging)
            {
                UpdatePointer(pointerRay);
                return;
            }

            if (Time.time - holdStartedAt >=
                Mathf.Max(0f, holdDurationSeconds))
            {
                BeginDrag(player);
            }
        }

        public void EndCarry(PlayerController player)
        {
            if (dragPlayer != player)
            {
                return;
            }

            if (dragging)
            {
                FinishDrag();
            }
            else
            {
                ResetHold();
                dragPlayer = null;
            }
        }

        public void UpdatePointer(Ray pointerRay)
        {
            if (!dragging)
            {
                return;
            }

            Plane plane = new Plane(
                Vector3.forward,
                originalPosition);
            if (!plane.Raycast(
                    pointerRay,
                    out float distance))
            {
                return;
            }

            Vector3 point = pointerRay.GetPoint(distance);
            point = ClampToCarryRange(point);
            transform.position = CellToWorld(
                WorldToCell(point));
        }

        public void CancelPointerDrag()
        {
            if (!dragging)
            {
                return;
            }

            transform.position = originalPosition;
            RestoreAfterDrag();
        }

        public void CancelCarry()
        {
            if (dragging)
            {
                CancelPointerDrag();
            }
            else
            {
                ResetHold();
                dragPlayer = null;
            }
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "供能状态",
                IsEnergyAvailable ? "可用" : "不可用"));
            values.Add(new BlockDebugValue(
                "允许迁移",
                canMigrate ? "是" : "否"));
            values.Add(new BlockDebugValue(
                "长按拿起时间",
                $"{holdDurationSeconds:0.##} 秒"));
            values.Add(new BlockDebugValue(
                "原位发散搜索半径",
                $"{fallbackSearchRadiusBlocks} 格"));
            values.Add(new BlockDebugValue(
                "当前拖拽",
                dragging ? "是" : "否"));
        }

        private void BeginDrag(PlayerController player)
        {
            if (player == null || dragging)
            {
                return;
            }

            dragPlayer = player;
            originalPosition = transform.position;
            originalCell = WorldToCell(originalPosition);
            originalPosition = CellToWorld(originalCell);
            transform.position = originalPosition;
            ResetHold();
            dragging = true;
            CacheDragTargets();
            SetCollidersEnabled(false);
            SetRenderersTopmost(true);
            player.BeginPointerDrag(this);
        }

        private void FinishDrag()
        {
            if (!dragging)
            {
                return;
            }

            Vector3Int desiredCell =
                WorldToCell(transform.position);
            Vector3Int dropCell =
                ResolveDropCell(desiredCell);
            transform.position = CellToWorld(dropCell);
            RestoreAfterDrag();
        }

        private Vector3Int ResolveDropCell(
            Vector3Int desiredCell)
        {
            if (IsCellValid(desiredCell))
            {
                return desiredCell;
            }

            if (IsCellValid(originalCell))
            {
                return originalCell;
            }

            return TryFindFallbackCell(
                out Vector3Int fallback)
                ? fallback
                : originalCell;
        }

        private bool TryFindFallbackCell(
            out Vector3Int result)
        {
            int radius = Mathf.Max(
                1,
                fallbackSearchRadiusBlocks);
            for (int ring = 1; ring <= radius; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Max(
                                Mathf.Abs(x),
                                Mathf.Abs(y)) != ring)
                        {
                            continue;
                        }

                        Vector3Int candidate =
                            originalCell +
                            new Vector3Int(x, y, 0);
                        if (IsCellValid(candidate))
                        {
                            result = candidate;
                            return true;
                        }
                    }
                }
            }

            result = originalCell;
            return false;
        }

        private bool IsCellValid(Vector3Int cell)
        {
            return !IsCellBlocked(cell) &&
                   IsWithinCarryRange(cell) &&
                   !WouldTrapPlayer(cell);
        }

        private bool IsWithinCarryRange(Vector3Int cell)
        {
            if (dragPlayer == null)
            {
                return true;
            }

            float radius =
                dragPlayer.InteractionScanRadius +
                GridCellWorldSize * .5f;
            Vector3 delta =
                CellToWorld(cell) -
                dragPlayer.transform.position;
            return new Vector2(delta.x, delta.y).sqrMagnitude <=
                   radius * radius;
        }

        private Vector3 ClampToCarryRange(Vector3 worldPosition)
        {
            if (dragPlayer == null)
            {
                return worldPosition;
            }

            float radius =
                Mathf.Max(0f, dragPlayer.InteractionScanRadius);
            Vector3 playerPosition =
                dragPlayer.transform.position;
            Vector2 delta = new Vector2(
                worldPosition.x - playerPosition.x,
                worldPosition.y - playerPosition.y);
            if (delta.sqrMagnitude <= radius * radius)
            {
                return worldPosition;
            }

            Vector2 clamped =
                new Vector2(playerPosition.x, playerPosition.y) +
                delta.normalized * radius;
            return new Vector3(
                clamped.x,
                clamped.y,
                worldPosition.z);
        }

        private bool IsCellBlocked(Vector3Int cell)
        {
            float cellSize = GridCellWorldSize;
            Vector3 center = CellToWorld(cell);
            int count = Physics.OverlapBoxNonAlloc(
                center,
                Vector3.one * (cellSize * .4f),
                overlapBuffer,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                Collider collider = overlapBuffer[index];
                if (collider == null ||
                    collider.gameObject == gameObject ||
                    (dragPlayer != null &&
                     collider.transform.root ==
                     dragPlayer.transform.root))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool WouldTrapPlayer(Vector3Int cell)
        {
            if (dragPlayer == null)
            {
                return false;
            }

            Collider playerCollider =
                dragPlayer.GetComponent<Collider>();
            if (playerCollider == null)
            {
                return false;
            }

            float cellSize = GridCellWorldSize;
            Vector3 cellCenter = CellToWorld(cell);
            Vector3 playerCenter = playerCollider.bounds.center;
            float halfExtent = cellSize * .45f;
            return Mathf.Abs(playerCenter.x - cellCenter.x) <=
                   halfExtent &&
                   Mathf.Abs(playerCenter.y - cellCenter.y) <=
                   halfExtent;
        }

        private void CacheDragTargets()
        {
            if (cachedColliders == null)
            {
                cachedColliders =
                    GetComponentsInChildren<Collider>(true);
            }

            if (cachedRenderers == null)
            {
                cachedRenderers =
                    GetComponentsInChildren<Renderer>(true);
                originalSortingLayerIds =
                    new int[cachedRenderers.Length];
                originalSortingOrders =
                    new int[cachedRenderers.Length];
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            CacheDragTargets();
            for (int index = 0;
                 index < cachedColliders.Length;
                 index++)
            {
                if (cachedColliders[index] != null)
                {
                    cachedColliders[index].enabled = enabled;
                }
            }
        }

        private void SetRenderersTopmost(bool topmost)
        {
            CacheDragTargets();
            int topLayerId = SortingLayer.layers.Length > 0
                ? SortingLayer.layers[
                    SortingLayer.layers.Length - 1].id
                : 0;
            for (int index = 0;
                 index < cachedRenderers.Length;
                 index++)
            {
                Renderer renderer = cachedRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (topmost)
                {
                    originalSortingLayerIds[index] =
                        renderer.sortingLayerID;
                    originalSortingOrders[index] =
                        renderer.sortingOrder;
                    renderer.sortingLayerID = topLayerId;
                    renderer.sortingOrder = short.MaxValue;
                }
                else
                {
                    renderer.sortingLayerID =
                        originalSortingLayerIds[index];
                    renderer.sortingOrder =
                        originalSortingOrders[index];
                }
            }
        }

        private void RestoreAfterDrag()
        {
            SetCollidersEnabled(true);
            SetRenderersTopmost(false);
            dragging = false;
            ResetHold();
            PlayerController player = dragPlayer;
            dragPlayer = null;
            player?.EndPointerDrag(this);
        }

        private void ResetHold()
        {
            holdStartedAt = -1f;
        }

        private Vector3Int WorldToCell(Vector3 position)
        {
            float cellSize = GridCellWorldSize;
            return new Vector3Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize),
                0);
        }

        private Vector3 CellToWorld(Vector3Int cell)
        {
            float cellSize = GridCellWorldSize;
            return new Vector3(
                (cell.x + .5f) * cellSize,
                (cell.y + .5f) * cellSize,
                originalPosition.z);
        }
    }
}
