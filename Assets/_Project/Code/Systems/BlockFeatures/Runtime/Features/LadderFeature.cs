using System.Collections.Generic;
using Project.Player;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "机关梯子",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Mechanism,
        Interactions =
            BlockFeatureInteraction.PlayerContact |
            BlockFeatureInteraction.RoomReset)]
    public sealed class LadderFeature : PlayerContactFeature
    {
        protected override void OnPlayerEntered(
            PlayerController player)
        {
            player.EnterClimb(this);
        }

        protected override void OnPlayerExited(
            PlayerController player)
        {
            player.ExitClimb(this);
        }

        protected override void OnResetForRoom()
        {
            DisconnectAllPlayers();
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "接触玩家",
                PlayerCount));
        }
    }
}
