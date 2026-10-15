using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Development.PixelWater
{
    /// <summary>
    /// Horizontal movement for the visual character used in the pixel-water sandbox scene.
    /// </summary>
    public sealed class PixelWaterTestMover : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float horizontalSpeed = 1.5f;
        [SerializeField] private Vector2 horizontalLimits = new(-2f, 2f);
        [SerializeField] private bool automaticPreview = true;

        private float previewDirection = 1f;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var direction = 0f;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction += 1f;

            var position = transform.position;
            if (direction == 0f && automaticPreview)
            {
                if (position.x <= horizontalLimits.x) previewDirection = 1f;
                else if (position.x >= horizontalLimits.y) previewDirection = -1f;
                direction = previewDirection;
            }

            if (direction == 0f) return;
            position.x = Mathf.Clamp(
                position.x + direction * horizontalSpeed * Time.deltaTime,
                horizontalLimits.x,
                horizontalLimits.y);
            transform.position = position;
        }
    }
}
