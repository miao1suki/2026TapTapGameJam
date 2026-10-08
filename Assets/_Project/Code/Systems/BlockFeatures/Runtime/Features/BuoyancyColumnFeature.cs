using System.Collections.Generic;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "浮力水柱",
        DefaultColorId = "blue",
        Category = BlockFeatureCategory.Water,
        Interactions =
            BlockFeatureInteraction.PlayerContact |
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Water, BlockChannel.Motion })]
    public sealed class BuoyancyColumnFeature : WaterVisualFeature
    {
        [BlockParameter(
            Label = "上浮推力",
            Group = "浮力设置",
            Order = 0)]
        [SerializeField, Min(0f)] private float riseSpeed = 7f;

        [BlockParameter(
            Label = "出水额外上推速度",
            Group = "浮力设置",
            Order = 1,
            Tooltip = "玩家被推到水面上方时额外增加的向上速度；用于产生越过水面后回落再上浮的效果。")]
        [SerializeField, Min(0f)] private float topExitBoostSpeed = .75f;

        private readonly Collider[] blueOverlapBuffer =
            new Collider[32];
        private readonly List<Vector2> submergedIntervals =
            new List<Vector2>();
        private readonly Dictionary<int, PlayerController> liftedPlayers =
            new Dictionary<int, PlayerController>();
        private readonly List<int> stoppedLiftPlayerIds =
            new List<int>();
        private readonly HashSet<int> overshootPlayerIds =
            new HashSet<int>();

        protected override void OnPlayerEntered(
            PlayerController player)
        {
            if (!IsBlueUnlocked())
            {
                return;
            }

            player.EnterWater(this);
            TrackPlayer(player);
        }

        protected override void OnPlayerStayed(
            PlayerController player)
        {
            if (!IsBlueUnlocked())
            {
                return;
            }

            TrackPlayer(player);
        }

        protected override void OnPlayerExited(
            PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            if (IsWithinBubbleColumn(player))
            {
                if (!ApplyBubbleState(player))
                {
                    ApplyTopExitBoost(player);
                }

                return;
            }

            StopLift(player.GetInstanceID());
        }

        protected override void OnTick(float deltaTime)
        {
            base.OnTick(deltaTime);
            if (!IsBlueUnlocked())
            {
                StopAllLifts();
                return;
            }

            stoppedLiftPlayerIds.Clear();
            foreach (KeyValuePair<int, PlayerController> pair in
                     liftedPlayers)
            {
                if (pair.Value == null ||
                    !IsWithinBubbleColumn(pair.Value))
                {
                    stoppedLiftPlayerIds.Add(pair.Key);
                    continue;
                }

                if (!ApplyBubbleState(pair.Value))
                {
                    ApplyTopExitBoost(pair.Value);
                }
            }

            for (int index = 0;
                 index < stoppedLiftPlayerIds.Count;
                 index++)
            {
                StopLift(stoppedLiftPlayerIds[index]);
            }
        }

        protected override void OnDetach()
        {
            StopAllLifts();
            base.OnDetach();
        }

        protected override void OnResetForRoom()
        {
            StopAllLifts();
            base.OnResetForRoom();
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "接触玩家",
                PlayerCount));
            values.Add(new BlockDebugValue(
                "持续上浮玩家",
                liftedPlayers.Count));
            values.Add(new BlockDebugValue(
                "上浮推力",
                riseSpeed));
            values.Add(new BlockDebugValue(
                "出水额外上推",
                topExitBoostSpeed));
            values.Add(new BlockDebugValue(
                "水体表现",
                HasWaterVisual ? "已关联" : "未关联"));
        }

        private void TrackPlayer(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            liftedPlayers[player.GetInstanceID()] = player;
            ApplyBubbleState(player);
        }

        private bool ApplyBubbleState(PlayerController player)
        {
            if (CanLiftPlayer(player))
            {
                overshootPlayerIds.Remove(
                    player.GetInstanceID());
                player.EnterWater(this);
                player.SetBuoyancy(this, riseSpeed);
                return true;
            }

            player.SetBuoyancy(this, 0f);
            player.ExitWater(this);
            return false;
        }

        private void ApplyTopExitBoost(PlayerController player)
        {
            if (player == null ||
                topExitBoostSpeed <= 0f ||
                !overshootPlayerIds.Add(
                    player.GetInstanceID()))
            {
                return;
            }

            player.ApplyVerticalBounceImmediate(
                player.VerticalVelocity +
                topExitBoostSpeed);
        }

        private void StopAllLifts()
        {
            var players = new List<PlayerController>(
                liftedPlayers.Values);
            liftedPlayers.Clear();
            overshootPlayerIds.Clear();
            for (int index = 0; index < players.Count; index++)
            {
                PlayerController player = players[index];
                if (player == null)
                {
                    continue;
                }

                player.SetBuoyancy(this, 0f);
                player.ExitWater(this);
            }
        }

        private void StopLift(int playerId)
        {
            if (!liftedPlayers.TryGetValue(
                    playerId,
                    out PlayerController player))
            {
                return;
            }

            liftedPlayers.Remove(playerId);
            overshootPlayerIds.Remove(playerId);
            if (player == null)
            {
                return;
            }

            player.SetBuoyancy(this, 0f);
            player.ExitWater(this);
        }

        private bool CanLiftPlayer(PlayerController player)
        {
            Collider playerCollider =
                player.GetComponent<Collider>();
            if (playerCollider == null ||
                !playerCollider.enabled)
            {
                return false;
            }

            Bounds bounds = playerCollider.bounds;
            if (transform.position.y >= bounds.center.y)
            {
                return false;
            }

            submergedIntervals.Clear();
            Vector3 halfExtents = bounds.extents;
            halfExtents.x = Mathf.Max(.02f, halfExtents.x);
            halfExtents.y = Mathf.Max(.02f, halfExtents.y);
            halfExtents.z = Mathf.Max(.02f, halfExtents.z);
            int count = Physics.OverlapBoxNonAlloc(
                bounds.center,
                halfExtents,
                blueOverlapBuffer,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                Collider collider = blueOverlapBuffer[index];
                if (collider == null)
                {
                    continue;
                }

                PlayerController hitPlayer =
                    collider.GetComponentInParent<PlayerController>();
                if (hitPlayer == player)
                {
                    continue;
                }

                ColorObject colorObject =
                    collider.GetComponentInParent<ColorObject>();
                if (colorObject != null &&
                    string.Equals(
                        colorObject.BaseColorTypeId,
                        "blue",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    float bottom = Mathf.Max(
                        bounds.min.y,
                        collider.bounds.min.y);
                    float top = Mathf.Min(
                        bounds.max.y,
                        collider.bounds.max.y);
                    if (top - bottom > .001f)
                    {
                        submergedIntervals.Add(
                            new Vector2(bottom, top));
                    }
                }
            }

            if (submergedIntervals.Count == 0)
            {
                return false;
            }

            submergedIntervals.Sort(CompareSubmergedIntervals);
            float submergedHeight = 0f;
            float intervalStart = submergedIntervals[0].x;
            float intervalEnd = submergedIntervals[0].y;
            for (int index = 1;
                 index < submergedIntervals.Count;
                 index++)
            {
                Vector2 interval = submergedIntervals[index];
                if (interval.x <= intervalEnd + .001f)
                {
                    intervalEnd = Mathf.Max(intervalEnd, interval.y);
                    continue;
                }

                submergedHeight += intervalEnd - intervalStart;
                intervalStart = interval.x;
                intervalEnd = interval.y;
            }

            submergedHeight += intervalEnd - intervalStart;
            return submergedHeight > .01f;
        }

        private static int CompareSubmergedIntervals(
            Vector2 left,
            Vector2 right)
        {
            return left.x.CompareTo(right.x);
        }

        private bool IsWithinBubbleColumn(
            PlayerController player)
        {
            Collider playerCollider =
                player.GetComponent<Collider>();
            if (playerCollider == null ||
                !playerCollider.enabled)
            {
                return false;
            }

            Bounds bounds = playerCollider.bounds;
            if (bounds.max.y <= transform.position.y)
            {
                return false;
            }

            Vector3 offset =
                player.transform.position - transform.position;
            Collider bubbleCollider = GetComponent<Collider>();
            if (bubbleCollider == null)
            {
                float fallbackRadius =
                    Mathf.Max(.05f, GridCellWorldSize);
                Vector2 fallbackOffset =
                    new Vector2(offset.x, offset.z);
                return fallbackOffset.sqrMagnitude <=
                       fallbackRadius * fallbackRadius;
            }

            Bounds bubbleBounds = bubbleCollider.bounds;
            float overlapX =
                bubbleBounds.extents.x + bounds.extents.x;
            float overlapZ =
                bubbleBounds.extents.z + bounds.extents.z;
            return Mathf.Abs(offset.x) <= overlapX &&
                   Mathf.Abs(offset.z) <= overlapZ;
        }
    }
}
