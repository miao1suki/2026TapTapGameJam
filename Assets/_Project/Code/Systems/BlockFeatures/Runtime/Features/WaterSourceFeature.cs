using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "水域",
        DefaultColorId = "blue",
        Category = BlockFeatureCategory.Water,
        Interactions =
            BlockFeatureInteraction.PlayerContact |
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Water })]
    public sealed class WaterSourceFeature : WaterVisualFeature
    {
        [BlockParameter(
            Label = "游泳速度倍率",
            Group = "水域设置",
            Order = 0)]
        [SerializeField, Min(.05f)] private float swimSpeedMultiplier = .7f;

        protected override void OnPlayerEntered(
            PlayerController player)
        {
            player.EnterWater(this);
            player.SetSwimSpeedMultiplier(
                this,
                swimSpeedMultiplier);
        }

        protected override void OnPlayerExited(
            PlayerController player)
        {
            player.ExitWater(this);
            player.SetSwimSpeedMultiplier(this, 0f);
        }

        protected override void OnResetForRoom()
        {
            base.OnResetForRoom();
            DisconnectAllPlayers();
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "接触玩家",
                PlayerCount));
            values.Add(new BlockDebugValue(
                "游泳倍率",
                swimSpeedMultiplier));
            values.Add(new BlockDebugValue(
                "水体表现",
                HasWaterVisual ? "已关联" : "未关联"));
        }
    }
}
