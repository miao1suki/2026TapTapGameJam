using UnityEngine;

namespace Project.InputAbstraction
{
    public interface IInteractionTarget
    {
        bool CanInteract(GameObject interactor);
        bool TryInteract(GameObject interactor);
    }
}
