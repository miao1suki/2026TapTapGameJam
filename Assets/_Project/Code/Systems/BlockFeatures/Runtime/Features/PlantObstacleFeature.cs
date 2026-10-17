using System;
using System.Collections.Generic;
using Project.Audio;
using Project.Rooms;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "植物障碍",
        DefaultColorId = "green",
        Category = BlockFeatureCategory.Plant,
        Interactions =
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.RoomReset)]
    public sealed class PlantObstacleFeature : BlockFeature
    {
        [BlockParameter(
            Label = "初始完整",
            Group = "障碍设置",
            Order = 0)]
        [SerializeField] private bool startsIntact = true;

        [BlockParameter(
            Label = "破坏后隐藏渲染",
            Group = "障碍设置",
            Order = 1)]
        [SerializeField] private bool hideRenderersWhenBroken = true;

        private Collider[] colliders;
        private Renderer[] renderers;
        private bool intact;
        private static readonly HashSet<PlantObstacleFeature> Attached = new HashSet<PlantObstacleFeature>();

        private sealed class BurnNode
        {
            public PlantObstacleFeature feature;
            public string region;
            public readonly List<Bounds> volumes = new List<Bounds>();
            public Bounds bounds;
        }

        public bool IsIntact => intact && CanRunFeature;
        public bool ShowsArtwork => intact || !hideRenderersWhenBroken;
        public event Action<PlantObstacleFeature, bool> IntactChanged;

        protected override void OnAttach()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            intact = startsIntact;
            Attached.Add(this);
            ApplyState();
        }

        protected override void OnDetach()
        {
            Attached.Remove(this);
            SetCollidersEnabled(true);
            SetRenderersEnabled(true);
        }

        protected override void OnResetForRoom()
        {
            intact = startsIntact;
            ApplyState();
            IntactChanged?.Invoke(this, intact);
        }

        protected override bool OnColorApplied(
            string colorId,
            GameObject actor)
        {
            if (!string.Equals(
                    colorId,
                    "red",
                    System.StringComparison.OrdinalIgnoreCase) ||
                !IsIntact)
            {
                return false;
            }

            return BreakObstacle(
                actor != null ? actor.transform : null);
        }

        public bool BreakObstacle(Component source = null)
        {
            if (!CanRunFeature || !intact)
            {
                return false;
            }

            // Snapshot connectivity before disabling any volume. The merged object's
            // individual volumes, not its enclosing rectangle, define adjacency.
            List<BurnNode> nodes = CollectBurnNodes();
            var connected = new HashSet<PlantObstacleFeature> { this };
            var frontier = new Queue<BurnNode>();
            BurnNode origin = nodes.Find(node => node.feature == this);
            if (origin != null) frontier.Enqueue(origin);
            float tolerance = Mathf.Max(.0001f, GridCellWorldSize * .002f);
            while (frontier.Count > 0)
            {
                BurnNode current = frontier.Dequeue();
                foreach (BurnNode next in nodes)
                {
                    if (connected.Contains(next.feature) || current.region != next.region ||
                        !TouchesSide(current, next, tolerance)) continue;
                    connected.Add(next.feature);
                    frontier.Enqueue(next);
                }
            }

            // Commit the whole group before notifying listeners; callbacks cannot
            // observe a half-burned group or recursively play overlapping sounds.
            foreach (PlantObstacleFeature obstacle in connected) obstacle.intact = false;
            AudioCuePlayer.Play(AudioCueIds.PlantBurn);
            foreach (PlantObstacleFeature obstacle in connected)
                if (obstacle != null) obstacle.ApplyState();
            foreach (PlantObstacleFeature obstacle in connected)
                if (obstacle != null) obstacle.IntactChanged?.Invoke(obstacle, false);
            return true;
        }

        private List<BurnNode> CollectBurnNodes()
        {
            var nodes = new List<BurnNode>();
            RoomLifecycleManager manager = RoomLifecycleManager.ForScene(gameObject.scene);
            foreach (PlantObstacleFeature obstacle in Attached)
            {
                if (obstacle == null || !obstacle.IsIntact || obstacle.gameObject.scene != gameObject.scene) continue;
                var member = obstacle.GetComponentInParent<RoomResetObject>();
                var node = new BurnNode
                {
                    feature = obstacle,
                    region = !string.IsNullOrEmpty(member?.RegionId)
                        ? member.RegionId : manager?.FindRegion(obstacle.transform.position)
                };
                // Merging and runtime artwork can add geometry after OnAttach.
                obstacle.colliders = obstacle.GetComponentsInChildren<Collider>(true);
                obstacle.renderers = obstacle.GetComponentsInChildren<Renderer>(true);
                foreach (Collider volume in obstacle.colliders)
                {
                    if (!volume.enabled || volume.isTrigger || !volume.gameObject.activeInHierarchy ||
                        volume.GetComponentInParent<PlantObstacleFeature>() != obstacle) continue;
                    Bounds bounds = volume.bounds;
                    if (volume is BoxCollider box)
                    {
                        // Do not depend on whether Physics.SyncTransforms ran this frame.
                        Vector3 half = box.size * .5f;
                        Matrix4x4 matrix = box.transform.localToWorldMatrix;
                        Vector3 x = matrix.MultiplyVector(new Vector3(half.x, 0, 0));
                        Vector3 y = matrix.MultiplyVector(new Vector3(0, half.y, 0));
                        Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, half.z));
                        bounds = new Bounds(matrix.MultiplyPoint3x4(box.center), 2f * new Vector3(
                            Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)));
                    }
                    if (bounds.size.sqrMagnitude < .000001f) continue;
                    if (node.volumes.Count == 0) node.bounds = bounds;
                    else node.bounds.Encapsulate(bounds);
                    node.volumes.Add(bounds);
                }
                nodes.Add(node);
            }
            return nodes;
        }

        private static bool TouchesSide(BurnNode a, BurnNode b, float tolerance)
        {
            Bounds broad = a.bounds;
            broad.Expand(tolerance * 2f);
            if (!broad.Intersects(b.bounds)) return false;
            foreach (Bounds first in a.volumes)
            foreach (Bounds second in b.volumes)
            {
                float overlapX = Mathf.Min(first.max.x, second.max.x) - Mathf.Max(first.min.x, second.min.x);
                float overlapY = Mathf.Min(first.max.y, second.max.y) - Mathf.Max(first.min.y, second.min.y);
                float overlapZ = Mathf.Min(first.max.z, second.max.z) - Mathf.Max(first.min.z, second.min.z);
                if (overlapZ <= tolerance) continue;
                bool horizontal = overlapY > tolerance &&
                    (Mathf.Abs(first.max.x - second.min.x) <= tolerance ||
                     Mathf.Abs(second.max.x - first.min.x) <= tolerance);
                bool vertical = overlapX > tolerance &&
                    (Mathf.Abs(first.max.y - second.min.y) <= tolerance ||
                     Mathf.Abs(second.max.y - first.min.y) <= tolerance);
                if (horizontal || vertical) return true;
            }
            return false;
        }

        public void RestoreObstacle()
        {
            if (!CanRunFeature || (intact && IsIntact))
            {
                return;
            }

            intact = true;
            ApplyState();
            IntactChanged?.Invoke(this, true);
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "障碍状态",
                IsIntact ? "完整" : "已破坏"));
            values.Add(new BlockDebugValue(
                "碰撞体数量",
                colliders?.Length ?? 0));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                "破坏障碍",
                () => BreakObstacle(),
                Application.isPlaying && IsIntact));
            actions.Add(new BlockDebugAction(
                "恢复障碍",
                RestoreObstacle,
                Application.isPlaying && !IsIntact));
        }

        private void ApplyState()
        {
            SetCollidersEnabled(intact);
            SetRenderersEnabled(
                intact || !hideRenderersWhenBroken);
        }

        private void SetCollidersEnabled(bool value)
        {
            if (colliders == null)
            {
                return;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = value;
                }
            }
        }

        private void SetRenderersEnabled(bool value)
        {
            if (renderers == null)
            {
                return;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = value;
                }
            }
        }
    }
}
