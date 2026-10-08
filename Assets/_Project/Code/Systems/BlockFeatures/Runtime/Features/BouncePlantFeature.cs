using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    public enum BounceTriggerMode
    {
        [InspectorName("仅摔落触发")] FallingOnly = 0,
        [InspectorName("仅跳跃触发")] JumpOnly = 1,
        [InspectorName("两者都可以")] Both = 2
    }

    [BlockFeature(
        DisplayName = "弹性植物",
        DefaultColorId = "green",
        Category = BlockFeatureCategory.Plant,
        Interactions =
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Motion })]
    public sealed class BouncePlantFeature : BlockFeature,
        IPlayerBounceSurface
    {
        [BlockParameter(
            Label = "玩家标签",
            Group = "弹性设置",
            Order = 0)]
        [SerializeField] private string playerTag = "Player";

        [BlockParameter(
            Label = "基础弹跳速度",
            Group = "弹性设置",
            Order = 1)]
        [SerializeField, Min(0f)] private float baseBounceSpeed = 10.5f;

        [BlockParameter(
            Label = "摔落高度转化比例",
            Group = "弹性设置",
            Order = 2)]
        [SerializeField, Min(0f)] private float fallDistanceScale = .7f;

        [BlockParameter(
            Label = "最大弹跳速度",
            Group = "弹性设置",
            Order = 3)]
        [SerializeField, Min(0f)] private float maxBounceSpeed = 16.5f;

        [BlockParameter(
            Label = "最短触发间隔",
            Group = "弹性设置",
            Order = 4)]
        [SerializeField, Min(0f)] private float minTriggerInterval = .15f;

        [BlockParameter(
            Label = "触发方式",
            Group = "弹性设置",
            Order = 5)]
        [SerializeField] private BounceTriggerMode triggerMode =
            BounceTriggerMode.Both;

        [BlockParameter(
            Label = "连续弹跳衰减比例",
            Group = "弹性设置",
            Order = 6,
            Tooltip = "每次连续弹跳后的速度倍率；1 表示不衰减，越小衰减越快。")]
        [SerializeField, Range(0f, 1f)]
        private float repeatedBounceDecay = .85f;

        [BlockParameter(
            Label = "衰减重置时间",
            Group = "弹性设置",
            Order = 7,
            Tooltip = "多久没有再次触发弹跳后，恢复完整弹速。")]
        [SerializeField, Min(.05f)]
        private float decayResetSeconds = 1.5f;

        [BlockParameter(
            Label = "最低弹跳高度（格）",
            Group = "弹性设置",
            Order = 8,
            Tooltip = "连续衰减后的预测弹跳高度低于这个值时，不再触发弹跳。")]
        [SerializeField, Min(0f)]
        private float minimumBounceHeightGrid = 1.5f;

        private readonly HashSet<int> jumpBounceLatched =
            new HashSet<int>();

        public bool IsBounceSurfaceAvailable => CanRunFeature;

        private void OnCollisionEnter(Collision collision)
        {
            if (!CanRunFeature)
            {
                return;
            }

            PlayerController player = ResolvePlayer(collision);
            if (player == null ||
                !IsTopContact(collision, player) ||
                player.VerticalVelocity > .1f)
            {
                return;
            }

            player.EnterBounceSurface(this);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!CanRunFeature)
            {
                return;
            }

            PlayerController player = ResolvePlayer(collision);
            if (player == null)
            {
                return;
            }

            if (!IsTopContact(collision, player))
            {
                player.ExitBounceSurface(this);
                return;
            }

            player.EnterBounceSurface(this);
            int id = player.GetInstanceID();
            if (triggerMode != BounceTriggerMode.FallingOnly)
            {
                if (!player.JumpHeld)
                {
                    jumpBounceLatched.Remove(id);
                }
                else if (!jumpBounceLatched.Contains(id) &&
                         TryApplyJumpBounce(player))
                {
                    jumpBounceLatched.Add(id);
                    return;
                }
            }

        }

        private void OnCollisionExit(Collision collision)
        {
            PlayerController player = ResolvePlayer(collision);
            if (player != null)
            {
                player.ExitBounceSurface(this);
                int id = player.GetInstanceID();
                jumpBounceLatched.Remove(id);
            }
        }

        protected override void OnResetForRoom()
        {
            jumpBounceLatched.Clear();
        }

        protected override bool OnColorApplied(
            string colorId,
            GameObject actor)
        {
            if (!string.Equals(
                    colorId,
                    "red",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Destroy(gameObject);
            return true;
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "基础弹跳速度",
                baseBounceSpeed));
            values.Add(new BlockDebugValue(
                "摔落转化比例",
                fallDistanceScale));
            values.Add(new BlockDebugValue(
                "触发方式",
                triggerMode == BounceTriggerMode.FallingOnly
                    ? "仅摔落"
                    : triggerMode == BounceTriggerMode.JumpOnly
                        ? "仅跳跃"
                        : "两者都可以"));
            values.Add(new BlockDebugValue(
                "连续衰减比例",
                repeatedBounceDecay));
            values.Add(new BlockDebugValue(
                "最低弹跳高度",
                $"{minimumBounceHeightGrid:0.###} 格"));
            values.Add(new BlockDebugValue(
                "连续弹跳锁定",
                jumpBounceLatched.Count));
        }

        public bool TryGetBounceSpeed(
            PlayerController player,
            float fallDistance,
            float downwardSpeed,
            out float verticalSpeed)
        {
            verticalSpeed = 0f;
            if (player == null ||
                triggerMode == BounceTriggerMode.JumpOnly ||
                player.MoveInput.y < -.1f ||
                !player.CanBounce(minTriggerInterval) ||
                fallDistance <= .2f)
            {
                return false;
            }

            float speed = Mathf.Min(
                maxBounceSpeed,
                baseBounceSpeed +
                fallDistance * fallDistanceScale);
            speed *= player.GetBounceDecay(
                repeatedBounceDecay,
                decayResetSeconds);
            float projectedHeight =
                player.CalculateVerticalRiseHeight(
                    speed,
                    player.JumpHeld);
            float minimumHeight =
                minimumBounceHeightGrid * GridCellWorldSize;
            if (projectedHeight < minimumHeight)
            {
                return false;
            }

            player.RegisterBounce(
                repeatedBounceDecay,
                decayResetSeconds);
            verticalSpeed = speed;
            return true;
        }

        private bool TryApplyJumpBounce(
            PlayerController player)
        {
            if (player == null ||
                player.MoveInput.y < -.1f ||
                !player.CanBounce(minTriggerInterval))
            {
                return false;
            }

            float speed = baseBounceSpeed *
                          player.GetBounceDecay(
                              repeatedBounceDecay,
                              decayResetSeconds);
            float projectedHeight =
                player.CalculateVerticalRiseHeight(
                    speed,
                    player.JumpHeld);
            float minimumHeight =
                minimumBounceHeightGrid * GridCellWorldSize;
            if (projectedHeight < minimumHeight)
            {
                return false;
            }

            player.RegisterBounce(
                repeatedBounceDecay,
                decayResetSeconds);
            player.ApplyJumpBoost(speed);
            return true;
        }

        private PlayerController ResolvePlayer(Collision collision)
        {
            if (collision == null)
            {
                return null;
            }

            PlayerController player =
                collision.collider.GetComponentInParent<PlayerController>();
            return player != null &&
                   player.gameObject.CompareTag(playerTag)
                ? player
                : null;
        }

        private bool IsTopContact(
            Collision collision,
            PlayerController player)
        {
            if (player.transform.position.y <= transform.position.y)
            {
                return false;
            }

            Collider plantCollider = GetComponent<Collider>();
            Collider playerCollider = player.GetComponent<Collider>();
            if (plantCollider != null && playerCollider != null)
            {
                Bounds plantBounds = plantCollider.bounds;
                Bounds playerBounds = playerCollider.bounds;
                float tolerance = Mathf.Max(
                    .08f,
                    GridCellWorldSize * .12f);
                if (playerBounds.min.y >=
                    plantBounds.max.y - tolerance)
                {
                    return true;
                }
            }

            ContactPoint[] contacts = collision.contacts;
            for (int index = 0; index < contacts.Length; index++)
            {
                if (contacts[index].normal.y > .5f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
