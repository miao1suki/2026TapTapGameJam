using UnityEngine;

namespace Project.InputAbstraction
{
    public interface IInteractionTarget
    {
        bool CanInteract(GameObject interactor);
        bool TryInteract(GameObject interactor);
    }

    public interface IInteractionHoldTarget : IInteractionTarget
    {
        void HoldInteract(GameObject interactor);
        void EndInteract(GameObject interactor);
    }
}
