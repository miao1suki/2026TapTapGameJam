using UnityEngine;

namespace Project.Fireflies
{
    /// <summary>Moves a sample actor through the flock in the standalone preview scene.</summary>
    public sealed class FireflyPreviewThreat : MonoBehaviour
    {
        [SerializeField] private Vector2 center = new Vector2(0f, 0f);
        [SerializeField] private Vector2 sweep = new Vector2(1.8f, 0.55f);
        [SerializeField, Min(0.01f)] private float frequency = 0.55f;

        private void Update()
        {
            float time = Time.time * frequency;
            transform.localPosition = new Vector3(
                center.x + Mathf.Sin(time) * sweep.x,
                center.y + Mathf.Sin(time * 1.37f) * sweep.y,
                -0.2f);
        }
    }
}
