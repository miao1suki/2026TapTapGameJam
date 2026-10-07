using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    public enum CurrentFlowDirection
    {
        [InspectorName("向左")] Left = 0,
        [InspectorName("向右")] Right = 1
    }

    [BlockFeature(
        DisplayName = "定向水流",
        DefaultColorId = "blue",
        Category = BlockFeatureCategory.Water,
        Interactions =
            BlockFeatureInteraction.PlayerContact |
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Water, BlockChannel.Motion })]
    public sealed class DirectionalCurrentFeature : WaterVisualFeature
    {
        [BlockParameter(
            Label = "流向",
            Group = "水流设置",
            Order = 0)]
        [SerializeField] private CurrentFlowDirection flowDirection =
            CurrentFlowDirection.Right;

        [BlockParameter(
            Label = "推搡速度",
            Group = "水流设置",
            Order = 1)]
        [SerializeField, Min(0f)] private float currentSpeed = 4f;

        protected override void OnPlayerEntered(
            PlayerController player)
        {
            player.EnterWater(this);
            ApplyCurrent(player);
        }

        protected override void OnPlayerStayed(
            PlayerController player)
        {
            ApplyCurrent(player);
        }

        protected override void OnPlayerExited(
            PlayerController player)
        {
            player.SetWaterVelocity(this, Vector2.zero);
            player.ExitWater(this);
        }

        protected override void OnResetForRoom()
        {
            base.OnResetForRoom();
            DisconnectAllPlayers();
        }

        private void ApplyCurrent(PlayerController player)
        {
            Vector3 localDirection =
                flowDirection == CurrentFlowDirection.Left
                    ? Vector3.left
                    : Vector3.right;
            Vector3 worldDirection = transform.TransformDirection(
                localDirection);
            worldDirection.z = 0f;
            if (worldDirection.sqrMagnitude < .0001f)
            {
                return;
            }

            worldDirection.Normalize();
            player.SetWaterVelocity(
                this,
                new Vector2(
                    worldDirection.x,
                    worldDirection.y) * currentSpeed);
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "接触玩家",
                PlayerCount));
            values.Add(new BlockDebugValue(
                "当前流向",
                flowDirection == CurrentFlowDirection.Left
                    ? "向左"
                    : "向右"));
            values.Add(new BlockDebugValue(
                "水流速度",
                currentSpeed));
            values.Add(new BlockDebugValue(
                "水体表现",
                HasWaterVisual ? "已关联" : "未关联"));
        }
    }
}
