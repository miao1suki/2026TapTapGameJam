namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "藤蔓生长物",
        DefaultColorId = "green",
        Category = BlockFeatureCategory.Plant,
        Interactions =
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.ObjectContact |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Growth })]
    public sealed class LadderSonFeature : ClimbableVineFeature
    {
        protected override bool CanGrowFromColor => false;
    }
}
