using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>Chooses an exposed cell of the actual water geometry, never its bounding-box centre.</summary>
    public static class WaterSteamPlacement
    {
        public static bool TryFind(WaterVisualFeature water, Vector3 reference, float cell,
            out Vector3 surface, out Vector2 halfWidthDepth)
        {
            surface = default;
            halfWidthDepth = default;
            if (water == null) return false;
            cell = Mathf.Max(.001f, cell);
            // Merged water has several exact colliders on this owner. Do not include
            // presentation children, nor fill holes by using the owner's WorldBounds.
            Collider[] volumes = water.GetComponents<Collider>();
            float nearest = float.PositiveInfinity;
            Vector3 actualPoint = reference;
            foreach (Collider volume in volumes)
            {
                if (!Usable(volume)) continue;
                Vector3 point = volume.ClosestPoint(new Vector3(reference.x, reference.y, volume.bounds.center.z));
                float distance = ((Vector2)(point - reference)).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                actualPoint = point;
            }
            if (float.IsPositiveInfinity(nearest)) return false;

            bool found = false, bestSameColumn = false;
            float bestDistance = float.PositiveInfinity;
            int clickedColumn = Mathf.FloorToInt(actualPoint.x / cell);
            float skin = cell * .002f;
            foreach (Collider volume in volumes)
            {
                if (!Usable(volume)) continue;
                Bounds bounds = volume.bounds;
                int first = Mathf.FloorToInt((bounds.min.x + skin) / cell);
                int last = Mathf.FloorToInt((bounds.max.x - skin) / cell);
                for (int x = first; x <= last; x++)
                {
                    float minX = Mathf.Max(bounds.min.x, x * cell);
                    float maxX = Mathf.Min(bounds.max.x, (x + 1) * cell);
                    Vector3 candidate = new Vector3((minX + maxX) * .5f, bounds.max.y, bounds.center.z);
                    Vector3 below = candidate - Vector3.up * skin;
                    Vector3 above = candidate + Vector3.up * skin;
                    if (!Contains(volume, below, skin * .1f)) continue;
                    // Water/current/buoyancy sources may occupy or touch the same cell.
                    // Follow only contiguous water at this column; never spray at the
                    // internal seam between two independent gameplay owners.
                    bool blocked = false;
                    int hops = 0;
                    while (GameplayWaterPresentation.ContainsWater(water.gameObject.scene, above, out float nextTop))
                    {
                        if (++hops > 4096 || nextTop <= candidate.y + skin)
                        {
                            blocked = true;
                            break;
                        }
                        candidate.y = nextTop;
                        below = candidate - Vector3.up * skin;
                        above = candidate + Vector3.up * skin;
                    }
                    if (blocked || ContainsAny(volumes, above, skin * .1f) ||
                        WaterCellVisibility.Covered(water, below, cell) ||
                        WaterCellVisibility.Covered(water, above, cell)) continue;
                    bool sameColumn = x == clickedColumn;
                    float distance = ((Vector2)(candidate - actualPoint)).sqrMagnitude;
                    if (found && (bestSameColumn && !sameColumn ||
                        bestSameColumn == sameColumn && distance >= bestDistance)) continue;
                    found = true;
                    bestSameColumn = sameColumn;
                    bestDistance = distance;
                    surface = candidate;
                    halfWidthDepth = new Vector2(Mathf.Max(.01f, (maxX - minX) * .5f),
                        Mathf.Max(.01f, bounds.extents.z));
                }
            }
            return found;
        }

        private static bool Usable(Collider collider) => collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        private static bool Contains(Collider collider, Vector3 point, float skin) =>
            (collider.ClosestPoint(point) - point).sqrMagnitude <= skin * skin;
        private static bool ContainsAny(Collider[] volumes, Vector3 point, float skin)
        {
            foreach (Collider volume in volumes)
                if (Usable(volume) && Contains(volume, point, skin)) return true;
            return false;
        }
    }
}
