using System;
using System.Collections.Generic;
using Project.Audio;
using Project.BurnEffects;
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

        [BlockParameter(
            Label = "燃烧效果配置",
            Group = "燃烧表现",
            Order = 0,
            Tooltip = "留空使用 Assets/ta_source/Burn/Resources/Burn/PlantBurnEffectSettings.asset。")]
        [SerializeField] private PlantBurnEffectSettings burnEffectSettings;

        private Collider[] colliders;
        private Renderer[] renderers;
        private PlantBurnEffectPlayer burnEffectPlayer;
        private bool intact;

        public bool IsIntact => intact && CanRunFeature;
        public event Action<PlantObstacleFeature, bool> IntactChanged;

        protected override void OnAttach()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            burnEffectPlayer = PlantBurnEffectPlayer.GetOrCreate(gameObject);
            intact = startsIntact;
            ApplyState();
        }

        protected override void OnDetach()
        {
            burnEffectPlayer?.Cancel();
            SetCollidersEnabled(true);
            SetRenderersEnabled(true);
        }

        protected override void OnResetForRoom()
        {
            burnEffectPlayer?.Cancel();
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
            if (!CanRunFeature || !intact)
            {
                return false;
            }

            intact = false;
            AudioCuePlayer.Play(AudioCueIds.PlantBurn);
            SetCollidersEnabled(false);
            if (hideRenderersWhenBroken)
            {
                bool playing = burnEffectPlayer != null &&
                    burnEffectPlayer.Play(
                        renderers,
                        burnEffectSettings,
                        0f,
                        HideBurnedRenderers);
                if (!playing)
                {
                    SetRenderersEnabled(false);
                }
            }
            IntactChanged?.Invoke(this, false);
            return true;
        }

        public void RestoreObstacle()
        {
            if (!CanRunFeature || (intact && IsIntact))
            {
                return;
            }

            burnEffectPlayer?.Cancel();
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
            if (intact)
            {
                SetRenderersEnabled(true);
            }
            else if (!hideRenderersWhenBroken)
            {
                SetRenderersEnabled(true);
            }
            else if (burnEffectPlayer == null || !burnEffectPlayer.IsPlaying)
            {
                SetRenderersEnabled(false);
            }
        }

        private void HideBurnedRenderers()
        {
            if (!intact && hideRenderersWhenBroken)
            {
                SetRenderersEnabled(false);
            }
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
