using UnityEngine;

namespace Project.Player
{
    public interface IPlayerCarryTarget : IPlayerPointerDrag
    {
        bool CanCarry(PlayerController player);
        void BeginCarry(
            PlayerController player,
            Ray pointerRay);
        void UpdateCarry(
            PlayerController player,
            Ray pointerRay);
        void EndCarry(PlayerController player);
        void CancelCarry();
    }
}
