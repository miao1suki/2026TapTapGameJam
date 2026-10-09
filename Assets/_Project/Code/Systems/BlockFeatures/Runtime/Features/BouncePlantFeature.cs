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
            Label = "低落差回弹比例",
            Group = "弹性设置",
            Order = 2,
            Tooltip = "低落差时的回弹高度 / 摔落高度比例。越高回弹越接近原落差。")]
        [SerializeField, Range(0f, 1f)]
        private float lowFallReturnHeightRatio = .9f;

        [BlockParameter(
            Label = "最大弹跳速度",
            Group = "弹性设置",
            Order = 3,
            Tooltip = "弹速安全上限；0 表示不限制，按回弹高度比例计算结果。")]
        [SerializeField, Min(0f)] private float maxBounceSpeed = 0f;

        [BlockParameter(
            Label = "高落差回弹比例",
            Group = "弹性设置",
            Order = 4,
            Tooltip = "落差达到参考高度后的最低回弹高度比例；0.5 表示至少回弹摔落高度的一半。")]
        [SerializeField, Range(0f, 1f)]
        private float highFallReturnHeightRatio = .5f;

        [BlockParameter(
            Label = "高落差参考高度（格）",
            Group = "弹性设置",
            Order = 5,
            Tooltip = "摔落高度达到该格数后，回弹比例插值到高落差比例。")]
        [SerializeField, Min(.1f)]
        private float highFallReferenceHeightGrid = 8f;

        [BlockParameter(
            Label = "最短触发间隔",
            Group = "弹性设置",
            Order = 6)]
        [SerializeField, Min(0f)] private float minTriggerInterval = .15f;

        [BlockParameter(
            Label = "触发方式",
            Group = "弹性设置",
            Order = 7)]
        [SerializeField] private BounceTriggerMode triggerMode =
            BounceTriggerMode.Both;

        [BlockParameter(
            Label = "连续弹跳衰减比例",
            Group = "弹性设置",
            Order = 8,
            Tooltip = "每次连续弹跳后的速度倍率；1 表示不衰减，越小衰减越快。")]
        [SerializeField, Range(0f, 1f)]
        private float repeatedBounceDecay = .85f;

        [BlockParameter(
            Label = "衰减重置时间",
            Group = "弹性设置",
            Order = 9,
            Tooltip = "多久没有再次触发弹跳后，恢复完整弹速。")]
        [SerializeField, Min(.05f)]
        private float decayResetSeconds = 1.5f;

        [BlockParameter(
            Label = "最低弹跳高度（格）",
            Group = "弹性设置",
            Order = 10,
            Tooltip = "连续衰减后的预测弹跳高度低于这个值时，不再触发弹跳。")]
        [SerializeField, Min(0f)]
        private float minimumBounceHeightGrid = .3f;

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
                "低落差回弹比例",
                lowFallReturnHeightRatio));
            values.Add(new BlockDebugValue(
                "高落差回弹比例",
                highFallReturnHeightRatio));
            values.Add(new BlockDebugValue(
                "高落差参考高度",
                $"{highFallReferenceHeightGrid:0.###} 格"));
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

            float baseHeight = player.CalculateVerticalRiseHeight(
                baseBounceSpeed,
                player.JumpHeld);
            float returnHeight =
                fallDistance * CalculateFallReturnRatio(fallDistance);
            float targetHeight = Mathf.Max(
                baseHeight,
                returnHeight);
            float speed = player.CalculateVerticalRiseSpeed(
                targetHeight,
                player.JumpHeld);
            speed *= player.GetBounceDecay(
                repeatedBounceDecay,
                decayResetSeconds);
            speed = ClampBounceSpeed(speed);
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
            speed = ClampBounceSpeed(speed);
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

        private float CalculateFallReturnRatio(float fallDistance)
        {
            float referenceHeight = Mathf.Max(
                .1f,
                highFallReferenceHeightGrid * GridCellWorldSize);
            float progress = Mathf.Clamp01(
                fallDistance / referenceHeight);
            return Mathf.Lerp(
                Mathf.Max(
                    lowFallReturnHeightRatio,
                    highFallReturnHeightRatio),
                highFallReturnHeightRatio,
                progress);
        }

        private float ClampBounceSpeed(float speed)
        {
            return maxBounceSpeed > 0f
                ? Mathf.Min(maxBounceSpeed, speed)
                : speed;
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
