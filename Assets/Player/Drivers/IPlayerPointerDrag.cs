using UnityEngine;

namespace Project.Player
{
    public interface IPlayerPointerDrag
    {
        void UpdatePointer(Ray pointerRay);
        void CancelPointerDrag();
    }
}
