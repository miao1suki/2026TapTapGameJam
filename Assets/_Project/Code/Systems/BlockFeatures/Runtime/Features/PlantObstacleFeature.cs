using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "植物障碍",
        DefaultColorId = "green",
        Category = BlockFeatureCategory.Plant,
        Interactions =
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.RoomReset)]
    public sealed class PlantObstacleFeature : BlockFeature
    {
        [BlockParameter(
            Label = "初始完整",
            Group = "障碍设置",
            Order = 0)]
        [SerializeField] private bool startsIntact = true;

        [BlockParameter(
            Label = "破坏后隐藏渲染",
            Group = "障碍设置",
            Order = 1)]
        [SerializeField] private bool hideRenderersWhenBroken = true;

        private Collider[] colliders;
        private Renderer[] renderers;
        private bool intact;

        public bool IsIntact => intact && isActiveAndEnabled;
        public event Action<PlantObstacleFeature, bool> IntactChanged;

        protected override void OnAttach()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            intact = startsIntact;
            ApplyState();
        }

        protected override void OnDetach()
        {
            SetCollidersEnabled(false);
            SetRenderersEnabled(true);
        }

        protected override void OnResetForRoom()
        {
            intact = startsIntact;
            ApplyState();
            IntactChanged?.Invoke(this, intact);
        }

        protected override bool OnColorApplied(
            string colorId,
            GameObject actor)
        {
            if (!string.Equals(
                    colorId,
                    "red",
                    System.StringComparison.OrdinalIgnoreCase) ||
                !IsIntact)
            {
                return false;
            }

            return BreakObstacle(
                actor != null ? actor.transform : null);
        }

        public bool BreakObstacle(Component source = null)
        {
            if (!intact)
            {
                return false;
            }

            intact = false;
            ApplyState();
            IntactChanged?.Invoke(this, false);
            return true;
        }

        public void RestoreObstacle()
        {
            if (intact && IsIntact)
            {
                return;
            }

            intact = true;
            ApplyState();
            IntactChanged?.Invoke(this, true);
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "障碍状态",
                IsIntact ? "完整" : "已破坏"));
            values.Add(new BlockDebugValue(
                "碰撞体数量",
                colliders?.Length ?? 0));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                "破坏障碍",
                () => BreakObstacle(),
                Application.isPlaying && IsIntact));
            actions.Add(new BlockDebugAction(
                "恢复障碍",
                RestoreObstacle,
                Application.isPlaying && !IsIntact));
        }

        private void ApplyState()
        {
            SetCollidersEnabled(intact);
            SetRenderersEnabled(
                intact || !hideRenderersWhenBroken);
        }

        private void SetCollidersEnabled(bool value)
        {
            if (colliders == null)
            {
                return;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = value;
                }
            }
        }

        private void SetRenderersEnabled(bool value)
        {
            if (renderers == null)
            {
                return;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = value;
                }
            }
        }
    }
}
