using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.GreenHook;
using Project.LevelEditor;
using Project.Player;
using Project.Rooms;
using UnityEngine;

namespace Project.ContactCubes
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-100), RequireComponent(typeof(ContactCube), typeof(Rigidbody))]
    public sealed class ContactCubeMotion : MonoBehaviour,
        IRoomColorResettable,
        ICarryMotionParticipant,
        IGreenHookTarget, IGreenHookMotion
    {
        [SerializeField, Min(0), InspectorName("水平喷出重力保护（秒）")] private float launchGravityGrace = .25f;
        [SerializeField, Min(.001f), InspectorName("落稳竖直速度容差")] private float settleVelocity = .08f;
        [SerializeField, Min(0), InspectorName("绿钩子优先搜索半径（格）")] private int greenHookSnapSearchRadius = 2;
        private Rigidbody body;
        private ContactCube cube;
        private BoxCollider shape;
        private RigidbodyConstraints baseConstraints;
        private readonly Dictionary<Component, Vector3> velocities = new Dictionary<Component, Vector3>();
        private readonly List<Component> deadSources = new List<Component>();
        private readonly Dictionary<Collider, bool> playerPairs = new Dictionary<Collider, bool>();
        private readonly Collider[] nearby = new Collider[64];
        private readonly RaycastHit[] supportHits = new RaycastHit[32];
        private float gravityProtectedUntil;
        private bool hadHorizontalPush;
        private bool greenHookActive;
        public bool IsSettled { get; private set; }
        public bool GravityProtected => Time.time < gravityProtectedUntil;
        public bool IsSuspended { get; private set; }
        public GreenHookMotionMode GreenHookMotionMode =>
            cube != null && cube.Color == CubeColor.Green ? GreenHookMotionMode.Swing : GreenHookMotionMode.PullTarget;
        public Transform GreenHookTransform => transform;
        public Vector3 GreenHookPosition =>
            body != null ? body.position : transform.position;
        public bool CanBeGreenHooked(GameObject actor) =>
            isActiveAndEnabled &&
            cube != null &&
            cube.IsUnlocked &&
            !IsSuspended &&
            !greenHookActive &&
            body != null &&
            !body.isKinematic;
        private void Awake()
        {
            body = GetComponent<Rigidbody>(); cube = GetComponent<ContactCube>(); shape = GetComponentInChildren<BoxCollider>();
            baseConstraints = body.constraints;
            if (shape != null) shape.isTrigger = false;
        }
        private void OnEnable() { if (cube == null) cube = GetComponent<ContactCube>(); cube.PushRequested += OnPush; }
        private void OnPush(Component source, Vector3 velocity, float dt)
        {
            if (source == null) return;
            if (velocity.sqrMagnitude < .0001f) velocities.Remove(source); else velocities[source] = velocity;
        }
        // Carry controller calls true while held, false after placing; it owns held poses.
        public void SuspendMotion(bool suspended)
        {
            VineGrowthLift.Cancel(body);
            IsSuspended = suspended; velocities.Clear(); hadHorizontalPush = false;
            gravityProtectedUntil = 0;
            if (suspended)
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true; body.useGravity = false; IsSettled = false;
                RestorePlayerCollision();
            }
            else
            {
                body.position = transform.position; body.rotation = transform.rotation;
                body.isKinematic = false; body.useGravity = true; body.constraints = baseConstraints;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            }
        }
        public void SetCarrySuspended(bool suspended)
        {
            cube.SetCarrySuspended(suspended);
            SuspendMotion(suspended);
        }

        public void BeginGreenHookPull()
        {
            VineGrowthLift.Cancel(body);
            if (greenHookActive || body == null || cube == null)
            {
                return;
            }

            greenHookActive = true;
            velocities.Clear();
            gravityProtectedUntil = 0f;
            hadHorizontalPush = false;
            IsSettled = false;
            body.isKinematic = false;
            body.useGravity = false;
            body.constraints =
                RigidbodyConstraints.FreezeRotation |
                RigidbodyConstraints.FreezePositionZ;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            cube.SetHookSuspended(true);
            RestorePlayerCollision();
        }

        public void SetGreenHookVelocity(Vector3 velocity)
        {
            if (!greenHookActive || body == null)
            {
                return;
            }

            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ |
                (velocity.sqrMagnitude < .000001f ? RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            body.linearVelocity = new Vector3(
                velocity.x,
                velocity.y,
                0f);
        }


        public void EndGreenHookPull()
        {
            EndGreenHookPull(false, Vector3.zero, 0f);
        }

        public void EndGreenHookPull(bool snapToGrid)
        {
            EndGreenHookPull(snapToGrid, Vector3.zero, 0f);
        }

        public void EndGreenHookPull(
            bool snapToGrid,
            Vector3 separationDirection,
            float separationDistance)
        {
            if (!greenHookActive)
            {
                return;
            }

            greenHookActive = false;
            cube?.SetHookSuspended(false);
            if (body != null)
            {
                Vector3 finalPosition = body.position;
                separationDirection.z = 0f;
                if (separationDistance > 0f &&
                    separationDirection.sqrMagnitude > .0001f)
                {
                    finalPosition +=
                        separationDirection.normalized *
                        separationDistance;
                }
                body.position = finalPosition;
                body.rotation = transform.rotation;
                body.isKinematic = false;
                body.useGravity = true;
                body.constraints = baseConstraints;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            if (snapToGrid)
            {
                CalibrateToNearestGridCell();
            }
        }
        public bool CanPlaceCarriedAt(Vector3 worldPosition)
        {
            if (shape == null) return false;
            Transform t = shape.transform;
            Vector3 center = t.TransformPoint(shape.center) + worldPosition - transform.position;
            Vector3 scale = t.lossyScale;
            Vector3 half = Vector3.Scale(shape.size * .49f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(center, half, nearby, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == nearby.Length) return false;
            for (int i = 0; i < count; i++)
                if (nearby[i] != null && !nearby[i].transform.IsChildOf(transform) &&
                    nearby[i].GetComponentInParent<IPlayerCarrySeparationHost>() == null) return false;
            return true;
        }
        private void FixedUpdate()
        {
            if (VineGrowthLift.IsLifting(body)) { RestorePlayerCollision(); return; }
            if (shape == null || !shape.enabled || IsSuspended ||
                greenHookActive || body.isKinematic)
            {
                return;
            }
            deadSources.Clear(); foreach (var pair in velocities)
                if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || (pair.Key is Behaviour behaviour && !behaviour.isActiveAndEnabled)) deadSources.Add(pair.Key);
            foreach (var source in deadSources) velocities.Remove(source);
            Vector3 flow = Vector3.zero; foreach (Vector3 velocity in velocities.Values) flow += velocity;
            bool horizontal = Mathf.Abs(flow.x) > .01f && Mathf.Abs(flow.y) < .01f;
            if (horizontal && !hadHorizontalPush)
            {
                gravityProtectedUntil = Time.time + launchGravityGrace;
                body.linearVelocity = new Vector3(flow.x, 0, 0);
            }
            hadHorizontalPush = horizontal;
            body.useGravity = !GravityProtected;
            bool flowing = flow.sqrMagnitude > .0001f;
            bool supported = HasSupport();
            IsSettled = !GravityProtected && supported &&
                (IsSettled && !flowing || Mathf.Abs(body.linearVelocity.y) <= settleVelocity);
            // Green remains a solid climbing surface even while its shape changes.
            // Only an explicit jet/carry command may release its horizontal position.
            RigidbodyConstraints locks = IsSettled && !flowing
                ? RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY
                : RigidbodyConstraints.None;
            if (cube.Color == CubeColor.Green && !flowing) locks |= RigidbodyConstraints.FreezePositionX;
            body.constraints = baseConstraints | locks;
            if (flowing)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.x = flow.x;
                if (Mathf.Abs(flow.y) > .01f) velocity.y = flow.y;
                else if (GravityProtected) velocity.y = 0;
                velocity.z = 0; body.linearVelocity = velocity;
            }
            RefreshPlayerCollision();
        }
        private bool HasSupport()
        {
            Bounds bounds = shape.bounds;
            // A constant-width bottom probe: shrinking the entire tall box by a
            // percentage leaves an ever-larger gap and loses support as it grows.
            Vector3 half = new Vector3(Mathf.Max(.005f,bounds.extents.x-.01f), .01f,
                Mathf.Max(.005f,bounds.extents.z-.01f));
            Vector3 origin = new Vector3(bounds.center.x,bounds.min.y+.035f,bounds.center.z);
            int count = Physics.BoxCastNonAlloc(origin, half, Vector3.down, supportHits,
                Quaternion.identity, .1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = supportHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerController>() != null) continue;
                if (supportHits[i].normal.y > .5f) return true;
            }
            return false;
        }
        private void RefreshPlayerCollision()
        {
            Bounds bounds = shape.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one, nearby,
                Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = nearby[i];
                if (hit != null && hit.GetComponentInParent<PlayerController>() != null && !playerPairs.ContainsKey(hit))
                    playerPairs.Add(hit, Physics.GetIgnoreCollision(shape, hit));
            }
            foreach (var pair in playerPairs)
                if (pair.Key != null) Physics.IgnoreCollision(shape, pair.Key,
                    cube.Color == CubeColor.Green ? false : pair.Value || !IsSettled);
        }
        public void ResetForRoom()
        {
            VineGrowthLift.Cancel(body);
            if (greenHookActive)
            {
                EndGreenHookPull(false);
            }
            velocities.Clear(); gravityProtectedUntil = 0; hadHorizontalPush = false; IsSettled = false;
            SetCarrySuspended(false); body.linearVelocity = Vector3.zero;
            RestorePlayerCollision();
        }
        private void RestorePlayerCollision()
        {
            foreach (var pair in playerPairs) if (pair.Key != null && shape != null) Physics.IgnoreCollision(shape, pair.Key, pair.Value);
            playerPairs.Clear();
        }
        private void OnDisable()
        {
            VineGrowthLift.Cancel(body);
            if (greenHookActive)
            {
                EndGreenHookPull(false);
            }
            if (cube != null) cube.PushRequested -= OnPush;
            velocities.Clear(); RestorePlayerCollision();
            if (body != null) { body.useGravity = true; body.constraints = baseConstraints; }
        }

        private void CalibrateToNearestGridCell()
        {
            if (body == null || shape == null)
            {
                return;
            }

            Vector3 position;
            RoomResetObject member =
                GetComponent<RoomResetObject>();
            if (TryFindNearestGridPosition(out position))
            {
                // The resolver already guarantees that the target cell is
                // legal, in-room, unoccupied, and physically clear.
            }
            else if (member != null)
            {
                // A full room lookup can still fail only in malformed data.
                // Returning to the authored cell keeps the object on-grid.
                position = member.AuthoredWorldPosition;
            }
            else
            {
                return;
            }

            body.position = position;
            body.rotation = transform.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private bool TryFindNearestGridPosition(
            out Vector3 result)
        {
            result = transform.position;
            float cellSize = Mathf.Max(
                .01f,
                GridCellSizeUtility.Resolve(this));
            RoomResetObject member =
                GetComponent<RoomResetObject>();
            RoomLifecycleManager manager =
                member != null ? member.Manager : null;
            IReadOnlyList<LevelEditorPlacedBlock> placedBlocks =
                ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true);
            IReadOnlyList<IPlayerCarryTarget> carryTargets =
                PlayerCarryTargetRegistry.All;
            HashSet<Vector2Int> occupiedCells =
                BuildOccupiedCells(
                    placedBlocks,
                    carryTargets,
                    manager,
                    cellSize);

            float searchRadius = Mathf.Max(
                0,
                greenHookSnapSearchRadius);
            float bestDistance = float.MaxValue;
            bool found = false;
            if (manager != null)
            {
                Vector3 local = manager.transform.InverseTransformPoint(
                    transform.position);
                int originX = Mathf.FloorToInt(local.x / cellSize);
                int originY = -Mathf.FloorToInt(local.y / cellSize) - 1;
                for (int radius = 0;
                     radius <= searchRadius;
                     radius++)
                {
                    for (int offsetX = -radius;
                         offsetX <= radius;
                         offsetX++)
                    for (int offsetY = -radius;
                         offsetY <= radius;
                         offsetY++)
                    {
                        int cellX = originX + offsetX;
                        int cellY = originY + offsetY;
                        Vector3 candidate =
                            manager.transform.TransformPoint(
                                new Vector3(
                                    (cellX + .5f) * cellSize,
                                    (-cellY - .5f) * cellSize,
                                    local.z));
                        Vector2Int gridCell =
                            new Vector2Int(cellX, cellY);
                        if (!manager.CanPlaceInAuthoredRegion(
                                member,
                                candidate) ||
                            occupiedCells.Contains(gridCell) ||
                            !CanSnapAt(candidate))
                        {
                            continue;
                        }

                        float distance =
                            (candidate - transform.position).sqrMagnitude;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            result = candidate;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    string regionId = member != null
                        ? member.RegionId
                        : manager.FindRegion(transform.position);
                    RoomRegion region = null;
                    for (int index = 0;
                         index < manager.Regions.Count;
                         index++)
                    {
                        RoomRegion candidateRegion =
                            manager.Regions[index];
                        if (candidateRegion != null &&
                            candidateRegion.id == regionId)
                        {
                            region = candidateRegion;
                            break;
                        }
                    }

                    if (region != null)
                    {
                        for (int index = 0;
                             index < region.cells.Count;
                             index++)
                        {
                            Vector2Int gridCell =
                                region.cells[index];
                            Vector3 candidate =
                                manager.transform.TransformPoint(
                                    new Vector3(
                                        (gridCell.x + .5f) * cellSize,
                                        (-gridCell.y - .5f) * cellSize,
                                        local.z));
                            if (!manager.CanPlaceInAuthoredRegion(
                                    member,
                                    candidate) ||
                                occupiedCells.Contains(gridCell) ||
                                !CanSnapAt(candidate))
                            {
                                continue;
                            }

                            float distance =
                                (candidate - transform.position)
                                .sqrMagnitude;
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                result = candidate;
                                found = true;
                            }
                        }
                    }
                }

                if (!found && member != null)
                {
                    result = member.AuthoredWorldPosition;
                    found = true;
                }
            }
            else
            {
                int cellX = Mathf.FloorToInt(
                    transform.position.x / cellSize);
                int cellY = Mathf.FloorToInt(
                    transform.position.y / cellSize);
                result = new Vector3(
                    (cellX + .5f) * cellSize,
                    (cellY + .5f) * cellSize,
                    transform.position.z);
                found = CanSnapAt(result);
                if (!found && member != null)
                {
                    result = member.AuthoredWorldPosition;
                    found = true;
                }
            }

            return found;
        }

        private HashSet<Vector2Int> BuildOccupiedCells(
            IReadOnlyList<LevelEditorPlacedBlock> blocks,
            IReadOnlyList<IPlayerCarryTarget> targets,
            RoomLifecycleManager manager,
            float cellSize)
        {
            HashSet<Vector2Int> occupied =
                new HashSet<Vector2Int>();
            for (int index = 0; index < blocks.Count; index++)
            {
                LevelEditorPlacedBlock block = blocks[index];
                if (block == null ||
                    !block.isActiveAndEnabled ||
                    block.gameObject.scene != gameObject.scene ||
                    block.transform == transform ||
                    block.transform.IsChildOf(transform))
                {
                    continue;
                }

                Vector2Int size = block.SizeCells;
                for (int offsetY = 0;
                     offsetY < size.y;
                     offsetY++)
                for (int offsetX = 0;
                     offsetX < size.x;
                     offsetX++)
                {
                    occupied.Add(
                        new Vector2Int(
                            block.Cell.x + offsetX,
                            block.Cell.y + offsetY));
                }
            }

            for (int index = 0; index < targets.Count; index++)
            {
                IPlayerCarryTarget target = targets[index];
                if (target == null ||
                    target.CarryTransform == null ||
                    target.CarryTransform == transform ||
                    target.CarryTransform.IsChildOf(transform))
                {
                    continue;
                }
                Vector3 local = manager != null
                    ? manager.transform.InverseTransformPoint(
                        target.CarryTransform.position)
                    : target.CarryTransform.position;
                int cellX = Mathf.FloorToInt(
                    local.x / cellSize);
                int cellY = -Mathf.FloorToInt(
                    local.y / cellSize) - 1;
                occupied.Add(new Vector2Int(cellX, cellY));
            }

            return occupied;
        }

        private bool CanSnapAt(Vector3 worldPosition)
        {
            Transform t = shape.transform;
            Vector3 center =
                t.TransformPoint(shape.center) +
                worldPosition -
                transform.position;
            Vector3 scale = t.lossyScale;
            Vector3 half = Vector3.Scale(
                shape.size * .49f,
                new Vector3(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(
                center,
                half,
                nearby,
                t.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider hit = nearby[index];
                if (hit == null ||
                    hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
