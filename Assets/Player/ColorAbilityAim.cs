using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Rooms;
using UnityEngine;

namespace Project.Player
{
    // Click-time query only. The render-frame indicator never performs physics queries.
    internal sealed class ColorAbilityAim
    {
        internal enum Result { Success, Rejected, OutOfRange }
        private Collider[] overlaps = new Collider[64];
        private readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();

        internal Result Find(PlayerInteractionSensor sensor, Camera camera, Vector2 pointer,
            string colorId, float tolerance, out IColorApplicationTarget target, out Vector3 point)
        {
            target = null;
            point = default;
            Vector3 origin = sensor.transform.position;
            if (!TryPointer(camera, pointer, origin, out Vector3 cursor)) return Result.Rejected;
            // Neither the pointer nor the chosen point may extend the original player circle.
            if (!InsideRange(origin, cursor, sensor.ScanRadius)) return Result.OutOfRange;
            if (sensor.TryGetScreenHit(camera, pointer, out RaycastHit direct))
            {
                point = direct.point;
                if (!InsideRange(origin, point, sensor.ScanRadius)) return Result.OutOfRange;
                target = Resolve(direct.collider);
                // Preserve the public direct-hit contract, including custom targets.
                // Unknown targets are not silently excluded by the tolerance allowlist.
                return Valid(sensor, target, colorId, point) ? Result.Success : Result.Rejected;
            }

            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(cursor,
                    new Vector3(tolerance, tolerance, Mathf.Max(1f, tolerance * 2f)),
                    overlaps, Quaternion.identity, sensor.InteractionMask, QueryTriggerInteraction.Collide);
                if (count < overlaps.Length) break;
                if (overlaps.Length >= 4096) return Result.Rejected;
                Array.Resize(ref overlaps, overlaps.Length * 2);
            } while (true);

            float best = tolerance * tolerance;
            int bestId = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || collider.transform.IsChildOf(sensor.transform) ||
                    collider.gameObject.scene != sensor.gameObject.scene) continue;
                IColorApplicationTarget candidate = Resolve(collider);
                Vector3 hit = collider.ClosestPoint(new Vector3(cursor.x, cursor.y, collider.bounds.center.z));
                float distance = ((Vector2)(hit - cursor)).sqrMagnitude;
                int id = collider.GetInstanceID();
                if (distance > best || (Mathf.Approximately(distance, best) && id >= bestId) ||
                    !InsideRange(origin, hit, sensor.ScanRadius) || !Valid(sensor, candidate, colorId, hit) ||
                    !CanTolerate(candidate, colorId)) continue;
                // Re-query the candidate point, not the empty cursor: a wall or an item
                // layered over water still owns that position and must not be bypassed.
                Vector3 screen = camera.WorldToScreenPoint(hit);
                if (screen.z <= 0f || !sensor.TryGetScreenHit(camera, screen, out RaycastHit foreground) ||
                    !ReferenceEquals(Resolve(foreground.collider), candidate)) continue;
                target = candidate;
                point = hit;
                best = distance;
                bestId = id;
            }
            return target != null ? Result.Success : Result.Rejected;
        }

        private IColorApplicationTarget Resolve(Collider collider)
        {
            if (collider == null) return null;
            behaviours.Clear();
            collider.GetComponentsInParent(false, behaviours);
            foreach (MonoBehaviour behaviour in behaviours)
                if (behaviour is IColorApplicationTarget target && behaviour.isActiveAndEnabled) return target;
            return null;
        }

        private static bool Valid(PlayerInteractionSensor sensor, IColorApplicationTarget target, string colorId, Vector3 point)
        {
            if (!(target is MonoBehaviour behaviour) || !behaviour.isActiveAndEnabled ||
                behaviour.gameObject.scene != sensor.gameObject.scene ||
                !target.CanApplyColor(colorId, sensor.gameObject) || !WithinRoom(sensor, behaviour, point)) return false;
            return !(behaviour is WaterVisualFeature) ||
                !WaterCellVisibility.Covered(behaviour, point, GridCellSizeUtility.Resolve(behaviour));
        }

        private static bool CanTolerate(IColorApplicationTarget target, string colorId)
        {
            // Base BlockFeature.CanApplyColor only tests unlock/activation, not whether
            // OnColorApplied has a reaction. Do not let an inert feature steal a snap.
            // Non-feature custom interface implementations retain their own contract.
            if (!(target is BlockFeature behaviour)) return true;
            bool red = string.Equals(colorId, "red", StringComparison.OrdinalIgnoreCase);
            bool blue = string.Equals(colorId, "blue", StringComparison.OrdinalIgnoreCase);
            if (behaviour is BouncePlantFeature) return red;
            if (behaviour is PlantObstacleFeature plant) return red && plant.IsIntact;
            if (behaviour is LavaHazardFeature lava) return blue && lava.BlueReplacementPrefab != null;
            if (behaviour is ClimbableVineFeature vine)
                return red || (blue && !(vine is LadderSonFeature) && vine.MaxGrowthBlocks > 0 &&
                    vine.CurrentGrowthBlocks < vine.MaxGrowthBlocks);
            return behaviour is WaterVisualFeature && red;
        }

        private static bool WithinRoom(PlayerInteractionSensor sensor, Component target, Vector3 point)
        {
            RoomLifecycleManager manager = RoomLifecycleManager.ForScene(sensor.gameObject.scene);
            if (manager == null) return true;
            Vector3 origin = sensor.transform.position;
            string room = manager.FindRegion(origin);
            if (string.IsNullOrEmpty(room)) return false;
            RoomResetObject member = target.GetComponentInParent<RoomResetObject>();
            if (member != null && !string.IsNullOrEmpty(member.RegionId) && member.RegionId != room) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(origin, point) /
                Mathf.Max(.01f, manager.CellWorldSize * .25f)));
            for (int i = 1; i <= steps; i++)
                if (manager.FindRegion(Vector3.Lerp(origin, point, i / (float)steps)) != room) return false;
            return true;
        }

        internal static bool InsideRange(Vector3 origin, Vector3 point, float radius) =>
            ((Vector2)(point - origin)).sqrMagnitude <= radius * radius;

        internal static bool TryPointer(Camera camera, Vector2 screen, Vector3 planePoint, out Vector3 point)
        {
            point = default;
            if (camera == null) return false;
            Ray ray = camera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.forward, planePoint).Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }
    }
}
