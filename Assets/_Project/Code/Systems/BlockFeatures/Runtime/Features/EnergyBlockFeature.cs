using System.Collections.Generic;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "能源方块",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Mechanism,
        Interactions = BlockFeatureInteraction.RoomReset,
        Provides = new[] { typeof(IEnergySource) },
        Writes = new[] { BlockChannel.Power })]
    public sealed class EnergyBlockFeature : BlockFeature,
        IEnergySource
    {
        public bool IsEnergyAvailable => CanRunFeature;

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "供能状态",
                IsEnergyAvailable ? "可用" : "不可用"));
        }
    }
}
