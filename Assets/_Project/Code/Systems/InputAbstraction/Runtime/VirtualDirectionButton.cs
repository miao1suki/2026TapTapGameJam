using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.InputAbstraction
{
    [DisallowMultipleComponent]
    public sealed class VirtualDirectionButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Vector2 direction;
        private int pointerId = int.MinValue;
        public Vector2 Direction => direction;
        public void Configure(Vector2 value) => direction = Vector2.ClampMagnitude(value, 1f);
        public void OnPointerDown(PointerEventData data)
        {
            if (pointerId != int.MinValue) return;
            pointerId = data.pointerId;
            VirtualInputState.SetVector(GetInstanceID(), InputActionId.Move, direction);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == pointerId) Release();
        }
        public void OnPointerExit(PointerEventData data)
        {
            if (data.pointerId == pointerId) Release();
        }
        private void OnDisable() => Release();
        private void OnApplicationFocus(bool focused) { if (!focused) Release(); }
        private void OnApplicationPause(bool paused) { if (paused) Release(); }
        private void Release()
        {
            pointerId = int.MinValue;
            VirtualInputState.ReleaseOwner(GetInstanceID());
        }
    }
}
