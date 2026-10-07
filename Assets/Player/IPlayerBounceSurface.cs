namespace Project.Player
{
    /// <summary>
    /// A surface that can answer how strongly a landing player should bounce.
    /// The player owns fall tracking and applies the returned velocity.
    /// </summary>
    public interface IPlayerBounceSurface
    {
        bool TryGetBounceSpeed(
            PlayerController player,
            float fallDistance,
            float downwardSpeed,
            out float verticalSpeed);
    }
}
