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

        protected override void OnPlayerStayed(PlayerController player)
        {
            // Contact registration survives a rope's temporary climb immunity.
            // Retry at the real trigger after grace expiry, without requiring exit/re-entry.
            if (player.CanAutomaticallyClimb) player.EnterClimb(this);
            else player.ExitClimb(this);
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
