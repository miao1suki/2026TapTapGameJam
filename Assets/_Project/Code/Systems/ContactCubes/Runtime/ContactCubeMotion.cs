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
        [BlockParameter(Label = "启用重力", Group = "物理", Order = 0,
            Tooltip = "三色方块均可单独设置。关闭后空闲悬浮，仍可搬运、接触反应或被绿钩拉动；保留作者刚体的锁轴设置。")]
        [SerializeField, InspectorName("启用重力")]
        private bool useGravity = true;
        [SerializeField, Min(0), InspectorName("水平喷出重力保护（秒）")] private float launchGravityGrace = .25f;
        [SerializeField, Min(.001f), InspectorName("落稳竖直速度容差")] private float settleVelocity = .08f;
        [SerializeField, Min(0), InspectorName("绿钩子优先搜索半径（格）")] private int greenHookSnapSearchRadius = 2;
        private Rigidbody body;
        private ContactCube cube;
        private PlatformMount platformMount;
        private BoxCollider shape;
        private RigidbodyConstraints baseConstraints;
        private bool authoredKinematic;
        private RigidbodyInterpolation baseInterpolation;
        private bool mountOwnsInterpolation;
        private bool mountWasKinematic, mountUsedGravity;
        private readonly Dictionary<Component, Vector3> velocities = new Dictionary<Component, Vector3>();
        private readonly List<Component> deadSources = new List<Component>();
        private readonly Dictionary<Collider, bool> playerPairs = new Dictionary<Collider, bool>();
        private readonly Collider[] nearby = new Collider[64];
        private readonly RaycastHit[] supportHits = new RaycastHit[32];
        private float gravityProtectedUntil;
        private bool hadHorizontalPush;
        private bool greenHookActive;
        private float cellSize = 1f;
        private HorizontalPhysicsClearance horizontalClearance;
        // Logical color contact retains the authored grid face even though
        // the physical box has a small clearance for equal-width openings.
        public Vector3 LogicalContactShapeSize => horizontalClearance.LogicalSize(shape);
        public bool IsSettled { get; private set; }
        // Author-pinned shapes need no invisible support collider for climbing.
        // Runtime settling locks do not count as author constraints.
        public bool IsAuthoredFixed => (baseConstraints & RigidbodyConstraints.FreezePosition) == RigidbodyConstraints.FreezePosition;
        public bool UsesGravity => useGravity;
        public bool IsStableClimbAnchor => !IsSuspended && !greenHookActive &&
            (IsSettled || IsAuthoredFixed || authoredKinematic ||
             !useGravity && !hadHorizontalPush && velocities.Count == 0);
        public bool GravityProtected => Time.time < gravityProtectedUntil;
        public bool IsSuspended { get; private set; }
        public bool IsGreenHookPullActive => greenHookActive;
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
            (!body.isKinematic || !useGravity || cube.Color == CubeColor.Green);
        private void Awake()
        {
            body = GetComponent<Rigidbody>(); cube = GetComponent<ContactCube>(); shape = GetComponentInChildren<BoxCollider>();
            platformMount = GetComponent<PlatformMount>();
            baseConstraints = body.constraints;
            authoredKinematic = body.isKinematic;
            baseInterpolation = body.interpolation;
            if (shape != null) shape.isTrigger = false;
            cellSize = Mathf.Max(.01f, GridCellSizeUtility.Resolve(this));
            ApplyHorizontalClearance();
        }
        public void ConfigureGravity(bool enabled)
        {
            useGravity = enabled;
            if (body != null && !IsSuspended && !greenHookActive)
            {
                body.useGravity = useGravity && !authoredKinematic;
                if (!useGravity && !body.isKinematic) body.linearVelocity = Vector3.zero;
                body.isKinematic = authoredKinematic || !useGravity;
            }
        }
        private void OnEnable()
        {
            if (cube == null) cube = GetComponent<ContactCube>();
            cube.PushRequested += OnPush;
            cube.LengthChanged += OnGeometryChanged;
            ApplyHorizontalClearance();
        }
        private void OnGeometryChanged(int length) => ApplyHorizontalClearance();
        private void ApplyHorizontalClearance()
        {
            // Green shapes are fixed-position climbing/growth anchors, not
            // movable boxes, and must keep their existing solid geometry.
            if (cube != null && cube.Color != CubeColor.Green)
                horizontalClearance.Apply(shape, cellSize);
        }
        private void OnPush(Component source, Vector3 velocity, float dt)
        {
            if (source == null) return;
            if (velocity.sqrMagnitude < .0001f) velocities.Remove(source); else velocities[source] = velocity;
        }
        // Carry controller calls true while held, false after placing; it owns held poses.
        public void SuspendMotion(bool suspended)
        {
            RestoreMountInterpolation();
            VineGrowthLift.Cancel(body);
            if (suspended && greenHookActive) EndGreenHookPull(false);
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
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.isKinematic = authoredKinematic || !useGravity;
                body.useGravity = useGravity && !authoredKinematic; body.constraints = baseConstraints;
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
            ApplyHorizontalClearance();
            velocities.Clear();
            gravityProtectedUntil = 0f;
            hadHorizontalPush = false;
            IsSettled = false;
            body.isKinematic = false;
            body.useGravity = useGravity;
            body.constraints =
                baseConstraints |
                RigidbodyConstraints.FreezeRotation |
                RigidbodyConstraints.FreezePositionZ;
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
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

            body.useGravity = useGravity;
            GreenHookBodyMotion.ApplyPullVelocity(body, velocity, shape);
            // Pull ownership may relax runtime settling locks, never author locks.
            body.constraints |= baseConstraints;
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
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = authoredKinematic || !useGravity;
                body.useGravity = useGravity && !authoredKinematic;
                body.constraints = baseConstraints;
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
                    !Project.Mechanisms.ButtonPressureGeometry.IsPressureVolume(nearby[i]) &&
                    nearby[i].GetComponentInParent<IPlayerCarrySeparationHost>() == null) return false;
            return true;
        }
        private bool HasActivePlatformMount => !IsSuspended && !greenHookActive && platformMount != null && platformMount.IsMounted &&
            shape != null && shape.enabled && (mountOwnsInterpolation || !body.isKinematic);
        // PlatformMount invokes this after the platform has moved. The optional
        // coordinator never writes a cube's Rigidbody or Transform itself.
        internal void ApplyPlatformMount()
        {
            if (!HasActivePlatformMount) return;
            // The platform moves without interpolation; do not visually lag it.
            if (!mountOwnsInterpolation)
            {
                mountWasKinematic = body.isKinematic; mountUsedGravity = body.useGravity;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                mountOwnsInterpolation = true;
            }
            body.position = platformMount.TargetPosition;
            body.useGravity = false;
            body.constraints = baseConstraints;
            IsSettled = true;
            RefreshPlayerCollision();
        }
        private void FixedUpdate()
        {
            if (HasActivePlatformMount) return;
            RestoreMountInterpolation();
            if (VineGrowthLift.IsLifting(body)) { RestorePlayerCollision(); return; }
            if (shape == null || !shape.enabled || IsSuspended ||
                greenHookActive || authoredKinematic)
            {
                return;
            }
            deadSources.Clear(); foreach (var pair in velocities)
                if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || (pair.Key is Behaviour behaviour && !behaviour.isActiveAndEnabled)) deadSources.Add(pair.Key);
            foreach (var source in deadSources) velocities.Remove(source);
            Vector3 flow = Vector3.zero; foreach (Vector3 velocity in velocities.Values) flow += velocity;
            bool flowing = flow.sqrMagnitude > .0001f;
            if (!useGravity && !flowing)
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.useGravity = false;
                body.constraints = baseConstraints;
                body.isKinematic = true;
                IsSettled = true;
                hadHorizontalPush = false;
                RefreshPlayerCollision();
                return;
            }
            if (body.isKinematic) body.isKinematic = false;
            bool horizontal = Mathf.Abs(flow.x) > .01f && Mathf.Abs(flow.y) < .01f;
            if (horizontal && !hadHorizontalPush)
            {
                gravityProtectedUntil = Time.time + launchGravityGrace;
                body.linearVelocity = new Vector3(flow.x, 0, 0);
            }
            hadHorizontalPush = horizontal;
            body.useGravity = useGravity && !GravityProtected;
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
            float inset = .01f * cellSize;
            float halfHeight = .005f * cellSize;
            Vector3 half = new Vector3(Mathf.Max(.005f * cellSize, bounds.extents.x - inset), halfHeight,
                Mathf.Max(.005f * cellSize, bounds.extents.z - inset));
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + inset + halfHeight, bounds.center.z);
            int count = Physics.BoxCastNonAlloc(origin, half, Vector3.down, supportHits,
                Quaternion.identity, inset * 2f + shape.contactOffset, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = supportHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerController>() != null ||
                    Physics.GetIgnoreLayerCollision(shape.gameObject.layer, hit.gameObject.layer) ||
                    Physics.GetIgnoreCollision(shape, hit)) continue;
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
            {
                Collider actor = pair.Key;
                if (actor == null) continue;
                Bounds actorBounds = actor.bounds;
                // Red/blue still pass an airborne player's sides/underside, but their
                // top is a real support even before the first settled frame. Otherwise
                // releasing a falling block briefly ignores a player landing on it and
                // lets the player pass through the filled block into the lava below.
                float topContactSkin = cellSize * .03f + shape.contactOffset + actor.contactOffset;
                bool topContact = !actor.isTrigger && actorBounds.center.y > bounds.center.y &&
                    actorBounds.min.y >= bounds.max.y - topContactSkin;
                Physics.IgnoreCollision(shape, actor, cube.Color == CubeColor.Green ? false :
                    pair.Value || !IsSettled && !topContact);
            }
        }
        private void RestoreMountInterpolation()
        {
            if (!mountOwnsInterpolation || body == null) return;
            body.interpolation = baseInterpolation;
            body.isKinematic = mountWasKinematic;
            body.useGravity = mountUsedGravity;
            body.constraints = baseConstraints;
            mountOwnsInterpolation = false;
        }
        public void ResetForRoom()
        {
            VineGrowthLift.Cancel(body);
            if (greenHookActive)
            {
                EndGreenHookPull(false);
            }
            velocities.Clear(); gravityProtectedUntil = 0; hadHorizontalPush = false; IsSettled = false;
            SetCarrySuspended(false);
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            ApplyHorizontalClearance();
            RestorePlayerCollision();
        }
        private void RestorePlayerCollision()
        {
            foreach (var pair in playerPairs) if (pair.Key != null && shape != null) Physics.IgnoreCollision(shape, pair.Key, pair.Value);
            playerPairs.Clear();
        }
        private void OnDisable()
        {
            RestoreMountInterpolation();
            VineGrowthLift.Cancel(body);
            if (greenHookActive)
            {
                EndGreenHookPull(false);
            }
            if (cube != null) { cube.PushRequested -= OnPush; cube.LengthChanged -= OnGeometryChanged; }
            velocities.Clear(); RestorePlayerCollision();
            if (body != null) { body.useGravity = useGravity && !authoredKinematic; body.constraints = baseConstraints; body.isKinematic = authoredKinematic || !useGravity; }
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
