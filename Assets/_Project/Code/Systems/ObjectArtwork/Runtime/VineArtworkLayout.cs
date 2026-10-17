using UnityEngine;

namespace Project.ObjectArtwork
{
    /// <summary>One bottom-anchored image per logical vine root, independent of author scale.</summary>
    public static class VineArtworkLayout
    {
        public static Bounds RootBounds(Transform owner, BoxCollider shape, float cell)
        {
            if (shape == null) return new Bounds(owner.position, Vector3.one * cell);
            // Collider.bounds is empty while locked/disabled. Reading the authored shape
            // also works in edit mode without enabling a gameplay component or collider.
            Vector3 x = owner.TransformVector(Vector3.right * shape.size.x);
            Vector3 y = owner.TransformVector(Vector3.up * shape.size.y);
            Vector3 z = owner.TransformVector(Vector3.forward * shape.size.z);
            return new Bounds(owner.TransformPoint(shape.center), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)));
        }

        public static void Apply(Transform visual, Transform owner, Sprite frame,
            float widthCells, float heightCells, float cell, Bounds rootBounds)
        {
            if (visual == null || owner == null || frame == null) return;
            Vector3 scale = owner.lossyScale;
            visual.localScale = new Vector3(
                Mathf.Max(.001f, widthCells * cell) / Mathf.Max(.001f, frame.bounds.size.x * Mathf.Abs(scale.x)),
                Mathf.Max(.001f, heightCells * cell) / Mathf.Max(.001f, frame.bounds.size.y * Mathf.Abs(scale.y)),
                1f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            // The atlas pivot is BottomCenter. Preserve the real root bottom and the
            // same depth in world cells even beneath a scaled planning-map instance.
            visual.localPosition = owner.InverseTransformPoint(new Vector3(
                rootBounds.center.x, rootBounds.min.y, owner.position.z - .51f * cell));
        }
    }
}
