using UnityEngine;

namespace Project.Planar
{
    /// <summary>Small native contact skin, in world units. Never skips occupied cells or intervening walls.</summary>
    public static class PlanarPlacementTolerance
    {
        public static float ForCell(float size) => Mathf.Clamp(
            Mathf.Max(Mathf.Abs(size) * .02f, Physics2D.defaultContactOffset),
            Mathf.Abs(size) * .02f, Mathf.Abs(size) * .04f);

        public static bool IntersectsInterior(Bounds target, Collider2D other, float size)
        {
            Bounds b = other.bounds;
            float skin = ForCell(size);
            // Box2D overlap queries include polygon skin. Large scaled floor colliders can
            // be returned even across a visible gap; they are candidates, not proof of intrusion.
            // Inset only the target boundary. A thin obstacle inside that boundary is still
            // occupied space; requiring overlap thicker than skin would incorrectly ignore it.
            return Mathf.Min(target.max.x - skin, b.max.x) > Mathf.Max(target.min.x + skin, b.min.x) &&
                Mathf.Min(target.max.y - skin, b.max.y) > Mathf.Max(target.min.y + skin, b.min.y);
        }
    }
}
