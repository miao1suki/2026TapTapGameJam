using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.InputAbstraction
{
    /// <summary>Visual-only feedback: the stationary hit area and input owner never follow the artwork.</summary>
    [DisallowMultipleComponent]
    public sealed class TouchPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private int pointer = int.MinValue;
        private RectTransform face;
        private Image highlight;
        private Vector2 pressPoint, targetOffset;
        private bool direction;
        private bool latched;
        private VirtualInputButton inputButton;
        private MobileButtonArtwork artwork;
        public Vector2 VisualOffset => face != null ? face.anchoredPosition : Vector2.zero;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            direction = GetComponent<VirtualDirectionButton>() != null;
            EnsureFace();
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                e.position, e.pressEventCamera, out pressPoint);
            face.SetAsLastSibling();
            Paint(true);
        }

        private void EnsureFace()
        {
            if (face != null) return;
            inputButton = GetComponent<VirtualInputButton>();
            artwork = GetComponent<MobileButtonArtwork>();
            face = artwork != null ? transform.Find("Artwork") as RectTransform : null;
            if (face == null)
            {
                var obj = new GameObject("Press Feedback", typeof(RectTransform), typeof(Image));
                face = obj.GetComponent<RectTransform>(); face.SetParent(transform, false);
                face.anchorMin = Vector2.zero; face.anchorMax = Vector2.one;
                face.offsetMin = face.offsetMax = Vector2.zero;
            }
            highlight = face.GetComponent<Image>(); highlight.raycastTarget = false;
            // Keep a stable hit area while only the visible button follows the finger.
            Transform label = transform.Find("Label");
            if (label != null) label.SetParent(face, false);
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnBeginDrag(PointerEventData e) => OnDrag(e);
        public void OnDrag(PointerEventData e)
        {
            if (!direction || e.pointerId != pointer) return;
            RectTransform rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var point);
            targetOffset = Vector2.ClampMagnitude((point - pressPoint) * .55f, Mathf.Min(rect.rect.width, rect.rect.height) * .22f);
        }
        public void OnEndDrag(PointerEventData e) { /* PointerUp owns release, not drag termination. */ }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) Release(); }
        private void Update()
        {
            if (face == null) return;
            bool next = inputButton != null && inputButton.ToggleOn;
            if (latched != next) { latched = next; Paint(pointer != int.MinValue); }
            face.anchoredPosition = Vector2.Lerp(face.anchoredPosition, targetOffset,
                1f - Mathf.Exp(-28f * Time.unscaledDeltaTime));
            if (pointer == int.MinValue && face.anchoredPosition.sqrMagnitude < .01f)
                face.anchoredPosition = Vector2.zero;
        }
        private void Paint(bool pressed)
        {
            if (face == null) return;
            if (artwork != null) artwork.SetPressed(pressed || latched);
            else highlight.color = pressed || latched ? new Color(.18f, .52f, .68f, .96f) : Color.clear;
            face.localScale = Vector3.one * (pressed ? .94f : 1f);
        }
        private void Release() { pointer = int.MinValue; targetOffset = Vector2.zero; Paint(false); }
        private void OnDisable() { Release(); if (face != null) face.anchoredPosition = Vector2.zero; }
        private void OnApplicationFocus(bool focus) { if (!focus) Release(); }
        private void OnApplicationPause(bool pause) { if (pause) Release(); }
    }
}
