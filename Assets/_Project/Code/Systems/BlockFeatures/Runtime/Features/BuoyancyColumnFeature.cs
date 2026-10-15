using System.Collections.Generic;
using Project.ColorBlocks;
using Project.Audio;
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
            Label = "出水喷出高度（格）",
            Group = "浮力设置",
            Order = 1,
            Tooltip = "离开浮力区后继续上升的高度，按单元格尺寸和玩家重力计算，不累加原有上浮速度。")]
        [SerializeField, Min(0f)] private float topExitRiseCells = 3f;

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
        private readonly HashSet<int> buoyantPlayerIds = new HashSet<int>();
        private bool bubbleAudioPlaying;
        private bool disconnecting;

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
            if (player == null || disconnecting)
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
            disconnecting = true;
            try
            {
                StopAllLifts();
                base.OnDetach();
            }
            finally { disconnecting = false; }
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
                "出水喷出高度（格）",
                topExitRiseCells));
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
                SetBubbleAudio(player.GetInstanceID(), true);
                return true;
            }

            player.SetBuoyancy(this, 0f);
            SetBubbleAudio(player.GetInstanceID(), false);
            player.ExitWater(this);
            return false;
        }

        private void ApplyTopExitBoost(PlayerController player)
        {
            if (player == null ||
                topExitRiseCells <= 0f ||
                !overshootPlayerIds.Add(
                    player.GetInstanceID()))
            {
                return;
            }

            player.ApplyGridRiseImmediate(topExitRiseCells);
        }

        private void StopAllLifts()
        {
            buoyantPlayerIds.Clear();
            StopBubbleAudio();
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
            SetBubbleAudio(playerId, false);
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
            if (player == null || player.IsDead) return false;
            Collider playerCollider =
                player.GetComponent<Collider>();
            if (playerCollider == null ||
                !playerCollider.enabled)
            {
                return false;
            }

            Bounds bounds = playerCollider.bounds;
            Collider columnCollider = GetComponent<Collider>();
            float columnBottom = columnCollider != null
                ? columnCollider.bounds.min.y
                : transform.position.y - GridCellWorldSize * .5f;
            if (columnBottom >= bounds.center.y)
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
            return submergedHeight > bounds.size.y * .5f;
        }

        private static int CompareSubmergedIntervals(
            Vector2 left,
            Vector2 right)
        {
            return left.x.CompareTo(right.x);
        }

        private void SetBubbleAudio(int playerId, bool active)
        {
            if (active) buoyantPlayerIds.Add(playerId);
            else buoyantPlayerIds.Remove(playerId);
            if (buoyantPlayerIds.Count == 0) StopBubbleAudio();
            else if (!bubbleAudioPlaying)
                bubbleAudioPlaying = AudioCuePlayer.PlayLoop(AudioCueIds.BubbleLoop, this, .6f);
        }

        private void StopBubbleAudio()
        {
            if (!bubbleAudioPlaying) return;
            bubbleAudioPlaying = false;
            AudioManager.Existing?.StopLoop(this);
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
            Collider bubbleCollider = GetComponent<Collider>();
            float columnBottom = bubbleCollider != null
                ? bubbleCollider.bounds.min.y
                : transform.position.y - GridCellWorldSize * .5f;
            if (bounds.max.y <= columnBottom)
            {
                return false;
            }

            Vector3 offset =
                player.transform.position - transform.position;
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
            return Mathf.Abs(bounds.center.x - bubbleBounds.center.x) <= overlapX &&
                   Mathf.Abs(bounds.center.z - bubbleBounds.center.z) <= overlapZ;
        }
    }
}
