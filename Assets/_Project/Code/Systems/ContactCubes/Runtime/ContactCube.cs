using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.GreenHook;
using UnityEngine;

namespace Project.ContactCubes
{
    public enum CubeColor { [InspectorName("红")] Red, [InspectorName("绿")] Green, [InspectorName("蓝")] Blue }
    public enum CubeReactionState { [InspectorName("静止")] Idle, [InspectorName("生长")] Growing, [InspectorName("缩短")] Shrinking, [InspectorName("锁长")] Locked }

    // Fixed identity; blue clicks can request green growth, never recolor a cube.
    [DisallowMultipleComponent]
    public sealed class ContactCube : MonoBehaviour,
        IRoomColorResettable,
        IEnergySource
    {
        [SerializeField, InspectorName("固定类型")] private CubeColor color;
        [SerializeField, InspectorName("实体碰撞箱")] private BoxCollider shape;
        [SerializeField, InspectorName("模型")] private Transform model;
        [SerializeField, Min(1), InspectorName("初始长度（格）")] private int initialLength = 1;
        [SerializeField, Min(1), InspectorName("最大长度（格）")] private int maximumLength = 6;
        [SerializeField, Min(.05f), InspectorName("变化间隔（秒）"), Tooltip("绿块持续接触时每次增加或减少一格的时间。")] private float stepSeconds = 1f;
        [SerializeField, Min(.001f), InspectorName("接触容差（格）")] private float contactToleranceCells = .01f;
        [SerializeField, Min(1), InspectorName("水柱射程（格）")] private float jetLengthCells = 6f;
        [SerializeField, Min(.05f), InspectorName("水柱宽度（格）")] private float jetWidthCells = .5f;
        [SerializeField, Min(0), InspectorName("水柱推动速度（格/秒）")] private float jetSpeed = 5f;
        [SerializeField, Min(0), InspectorName("玩家推动倍率")] private float jetPlayerPushMultiplier = 1.6f;
        [SerializeField, Range(.2f,4f), InspectorName("气泡密度（排/格）")] private float jetBubbleDensity = 2.5f;
        [SerializeField, Range(.1f,12f), InspectorName("气泡速度（格/秒）")] private float jetBubbleSpeed = 6f;
        [SerializeField, HideInInspector] private Material jetMaterial; // Legacy serialized reference, formal visuals use WaterPresentation.
        private Collider[] hits = new Collider[32];
        private readonly HashSet<ContactCube> neighbours = new HashSet<ContactCube>();
        // Real contact pairs may have asymmetric/non-default contact offsets.
        // This is only a candidate set: each step rechecks actual geometry, not a timer.
        private readonly HashSet<ContactCube> collisionNeighbours = new HashSet<ContactCube>();
        private readonly List<ContactCube> staleCollisionNeighbours = new List<ContactCube>();
        private readonly HashSet<Component> touchingTargets = new HashSet<Component>();
        private readonly HashSet<Component> appliedTargets = new HashSet<Component>();
        private readonly Dictionary<ContactCube, CubeWaterJet> jets = new Dictionary<ContactCube, CubeWaterJet>();
        private readonly List<ContactCube> staleJets = new List<ContactCube>();
        private float elapsed;
        private float cellSize = 1f;
        private Vector3 localGrowthDirection = Vector3.right;
        private ContactCube selectedBlue;
        private ContactCubeMotion motion;
        private bool carrySuspended;
        private bool hookSuspended;
        private bool playerGrowthRequested;
        public bool IsPlayerGrowthRequested => playerGrowthRequested;
        public void SetCarrySuspended(bool suspended)
        {
            carrySuspended = suspended;
            if (suspended) ClearContacts();
        }
        public CubeColor Color => color;
        public string ColorId => color == CubeColor.Red ? "red" : color == CubeColor.Blue ? "blue" : "green";
        public bool IsUnlocked => ColorRuntimeService.Existing != null && ColorRuntimeService.Existing.IsUnlocked(ColorId);
        // Hook ownership pauses cube reactions, not its passive electrical contact.
        // Carrying still disables supply (and normally disables the real collider).
        public bool IsEnergyAvailable => color == CubeColor.Red && HasContactShape;
        public bool IsHookSuspended => hookSuspended;
        internal bool HasContactShape => isActiveAndEnabled && IsUnlocked && !carrySuspended &&
            shape != null && shape.enabled && !shape.isTrigger && shape.gameObject.activeInHierarchy;
        internal bool CanEmitContactReactions => HasContactShape && !hookSuspended;
        internal bool CanMaintainJetWith(ContactCube red) => color == CubeColor.Blue && CanEmitContactReactions &&
            red != null && red.Color == CubeColor.Red && red.HasContactShape &&
            IsWithinContactShell(red, contactToleranceCells * cellSize);
        public int Length { get; private set; }
        public int MaximumLength => maximumLength;
        public float CellWorldSize => cellSize;
        public CubeReactionState State { get; private set; }
        public Vector3 GrowthDirection => transform.TransformDirection(localGrowthDirection).normalized;
        public event Action<CubeReactionState> StateChanged;
        public event Action<int> LengthChanged;
        // Motion controller owns displacement, gravity and sticky constraints.
        public event Action<Component, Vector3, float> PushRequested;
        public void RequestPush(Component source, Vector3 velocity, float deltaTime) => PushRequested?.Invoke(source, velocity, deltaTime);

        private void Awake()
        {
            motion = GetComponent<ContactCubeMotion>();
            cellSize = GridCellSizeUtility.Resolve(this);
            ResetForRoom();
            // Existing scene instances need the same click entry as newly placed prefabs.
            if (color == CubeColor.Green && GetComponent<BlueContactCubeGrowthTarget>() == null)
                gameObject.AddComponent<BlueContactCubeGrowthTarget>();
        }
        private void FixedUpdate()
        {
            if (!CanEmitContactReactions)
            {
                ClearContacts();
                return;
            }
            ScanContacts();
            TickReaction(Time.fixedDeltaTime);
            UpdateJets();
        }

        public void SetHookSuspended(bool suspended)
        {
            hookSuspended = suspended;
            if (suspended)
            {
                ClearContacts();
            }
        }
        private void ScanContacts()
        {
            neighbours.Clear(); touchingTargets.Clear();
            Transform t = shape.transform;
            // The original tolerance is smaller than a pair's contact shell:
            // solver-separated resting boxes need not geometrically overlap.
            // Broad-phase extra room is narrowed per cube pair below; it must
            // not extend color application to unrelated nearby world objects.
            float worldTolerance = contactToleranceCells * cellSize;
            Vector3 half = Vector3.Scale(shape.size * .5f, Abs(t.lossyScale)) + Vector3.one * worldTolerance;
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(t.TransformPoint(shape.center), half, hits, t.rotation, ~0, QueryTriggerInteraction.Collide);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            } while (hits.Length <= 4096);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<CubeWaterJet>() != null) continue;
                ContactCube cube = hit.GetComponentInParent<ContactCube>();
                if (cube != null)
                {
                    if (cube != this && cube.HasContactShape && IsWithinContactShell(cube, worldTolerance)) neighbours.Add(cube);
                    continue;
                }
                foreach (MonoBehaviour component in hit.GetComponentsInParent<MonoBehaviour>())
                {
                    if (!component.isActiveAndEnabled || !(component is IColorApplicationTarget target)) continue;
                    touchingTargets.Add(component);
                    if (!appliedTargets.Contains(component) && target.CanApplyColor(ColorId, gameObject) && target.ApplyColor(ColorId, gameObject))
                        appliedTargets.Add(component);
                }
            }
            // Only cube/cube reactions use the physical contact shell; do not
            // enlarge burn/growth/color application to unrelated world objects.
            // Movable boxes keep a small physical horizontal clearance so they
            // can fall through a one-cell opening. Color contact must still use
            // the authored one-cell face, not that smaller collision shape.
            // This extra margin only finds candidates (including the other
            // box's physical inset); the logical OBB test below owns acceptance.
            var logicalBox = new ContactBox(this);
            float candidateMargin = Mathf.Max(0f, shape.contactOffset) +
                Mathf.Max(0f, Physics.defaultContactOffset) + cellSize * .05f;
            Vector3 contactHalf = logicalBox.Half + Vector3.one * (worldTolerance + candidateMargin);
            do
            {
                count = Physics.OverlapBoxNonAlloc(logicalBox.Center, contactHalf, hits, t.rotation, ~0, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            } while (hits.Length <= 4096);
            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index];
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<CubeWaterJet>() != null) continue;
                ContactCube cube = hit.GetComponentInParent<ContactCube>();
                if (cube != null && cube != this && cube.HasContactShape && IsWithinContactShell(cube, worldTolerance)) neighbours.Add(cube);
            }
            staleCollisionNeighbours.Clear();
            foreach (ContactCube cube in collisionNeighbours)
            {
                if (cube == null || !cube.HasContactShape || !IsWithinContactShell(cube, worldTolerance))
                    staleCollisionNeighbours.Add(cube);
                else neighbours.Add(cube);
            }
            foreach (ContactCube cube in staleCollisionNeighbours) collisionNeighbours.Remove(cube);
            appliedTargets.RemoveWhere(target => target == null || !touchingTargets.Contains(target));
        }

        private void OnCollisionEnter(Collision collision) => RememberContact(collision);
        private void OnCollisionStay(Collision collision) => RememberContact(collision);
        private void OnCollisionExit(Collision collision)
        {
            ContactCube cube = collision.collider != null ? collision.collider.GetComponentInParent<ContactCube>() : null;
            if (cube != null) collisionNeighbours.Remove(cube);
        }
        private void RememberContact(Collision collision)
        {
            ContactCube cube = collision.collider != null ? collision.collider.GetComponentInParent<ContactCube>() : null;
            if (cube != null && cube != this) collisionNeighbours.Add(cube);
        }

        private readonly struct ContactBox
        {
            public readonly Vector3 Center, Half, X, Y, Z;
            public ContactBox(ContactCube cube)
            {
                BoxCollider box = cube.shape;
                Transform frame = box.transform;
                Center = frame.TransformPoint(box.center);
                Vector3 logicalSize = cube.motion != null ? cube.motion.LogicalContactShapeSize : box.size;
                // Components assembled by test/tools may not have completed
                // Motion.Awake yet. Never turn that into a zero-sized contact.
                if (logicalSize.x <= 0f || logicalSize.y <= 0f || logicalSize.z <= 0f) logicalSize = box.size;
                Half = Vector3.Scale(logicalSize * .5f, Abs(frame.lossyScale));
                X = frame.right; Y = frame.up; Z = frame.forward;
            }
            public Vector3 Axis(int index) => index == 0 ? X : index == 1 ? Y : Z;
            public float Radius(Vector3 axis) => Mathf.Abs(Vector3.Dot(axis, X)) * Half.x +
                Mathf.Abs(Vector3.Dot(axis, Y)) * Half.y + Mathf.Abs(Vector3.Dot(axis, Z)) * Half.z;
        }
        private bool IsWithinContactShell(ContactCube other, float worldTolerance)
        {
            // A fixed geometric envelope, not hysteresis: a moved/disabled/held
            // neighbour cannot keep a jet alive through stale collision callbacks.
            float padding = worldTolerance + Mathf.Max(0f, shape.contactOffset) + Mathf.Max(0f, other.shape.contactOffset);
            var a = new ContactBox(this); var b = new ContactBox(other);
            Vector3 delta = b.Center - a.Center;
            for (int index = 0; index < 3; index++)
                if (Separated(a, b, delta, a.Axis(index), padding) || Separated(a, b, delta, b.Axis(index), padding)) return false;
            for (int first = 0; first < 3; first++)
                for (int second = 0; second < 3; second++)
                    if (Separated(a, b, delta, Vector3.Cross(a.Axis(first), b.Axis(second)), padding)) return false;
            return true;
        }
        private static bool Separated(ContactBox a, ContactBox b, Vector3 delta, Vector3 axis, float padding)
        {
            float lengthSquared = axis.sqrMagnitude;
            return lengthSquared > .000001f && Mathf.Abs(Vector3.Dot(delta, axis)) >
                a.Radius(axis) + b.Radius(axis) + padding * Mathf.Sqrt(lengthSquared) + .00001f;
        }
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        public static CubeReactionState Evaluate(bool red, bool blue) => red && blue ? CubeReactionState.Locked : blue ? CubeReactionState.Growing : red ? CubeReactionState.Shrinking : CubeReactionState.Idle;
        internal bool CanRequestPlayerGrowth => color == CubeColor.Green && CanEmitContactReactions && Length < maximumLength;
        internal bool RequestPlayerGrowth(Vector3 playerCenter)
        {
            if (!CanRequestPlayerGrowth) return false;
            // Repeated clicks neither postpone the next step nor turn an extended body.
            if (Length == 1)
                localGrowthDirection = transform.InverseTransformDirection(PlayerGrowthDirection(playerCenter));
            playerGrowthRequested = true;
            return true;
        }
        private Vector3 PlayerGrowthDirection(Vector3 playerCenter)
        {
            Vector3 root = transform.position;
            var manager = Project.Rooms.RoomLifecycleManager.ForScene(gameObject.scene);
            Vector3 gridRoot = manager != null ? manager.transform.InverseTransformPoint(root) : root;
            Vector3 gridPlayer = manager != null ? manager.transform.InverseTransformPoint(playerCenter) : playerCenter;
            // Resting physics can straddle an exact grid boundary by a few millimetres.
            bool sameRow = Mathf.Abs(gridPlayer.y - gridRoot.y) <= cellSize * .01f ||
                Mathf.FloorToInt(gridPlayer.y / cellSize) == Mathf.FloorToInt(gridRoot.y / cellSize);
            if (!sameRow) return playerCenter.y < root.y ? Vector3.down : Vector3.up;
            return playerCenter.x < root.x ? Vector3.right : Vector3.left;
        }
        internal void CancelPlayerGrowth()
        {
            if (!playerGrowthRequested) return;
            playerGrowthRequested = false;
            elapsed = 0;
            TickReaction(0f);
        }
        private void TickReaction(float dt)
        {
            if (color != CubeColor.Green) return;
            bool red = false; ContactCube blue = null;
            foreach (ContactCube neighbour in neighbours)
            {
                if (neighbour.Color == CubeColor.Red) red = true;
                if (neighbour.Color == CubeColor.Blue && (blue == null || neighbour.GetInstanceID() < blue.GetInstanceID())) blue = neighbour;
            }
            CubeReactionState next = Evaluate(red, blue != null || playerGrowthRequested);
            if (State != next || selectedBlue != blue)
            {
                elapsed = 0; selectedBlue = blue;
                if (State != next) { State = next; StateChanged?.Invoke(next); }
            }
            if (State == CubeReactionState.Idle || State == CubeReactionState.Locked) return;
            elapsed += Mathf.Max(0, dt);
            if (elapsed < stepSeconds) return;
            elapsed %= stepSeconds;
            if (State == CubeReactionState.Growing)
            {
                // Direction is fixed while extended; never swing an existing long body.
                if (Length == 1 && blue != null && !playerGrowthRequested)
                {
                    Vector3 away = transform.position - blue.transform.position;
                    if (away.sqrMagnitude > .0001f) localGrowthDirection = transform.InverseTransformDirection(away.normalized);
                }
                if (Length < maximumLength && CanGrowOneCell()) SetLength(Length + 1);
                if (Length >= maximumLength) playerGrowthRequested = false;
            }
            else SetLength(Mathf.Max(1, Length - 1));
        }
        private bool CanGrowOneCell()
        {
            // Only the newly occupied cell is tested: a supporting floor beside
            // the original body must not block growth along a clear face.
            Quaternion rotation = transform.rotation * Quaternion.FromToRotation(Vector3.right, localGrowthDirection);
            Vector3 center = transform.TransformPoint(localGrowthDirection * (Length * cellSize));
            Vector3 half = Vector3.Scale(Vector3.one * (cellSize * .5f), Abs(transform.lossyScale));
            // Ignore touching faces while still rejecting thin walls anywhere in the new cell.
            float skin = cellSize * .001f;
            half = new Vector3(Mathf.Max(skin, half.x - skin), Mathf.Max(skin, half.y - skin), Mathf.Max(skin, half.z - skin));
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(center, half, hits, rotation, ~0, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                if (hits.Length >= 4096) return false;
                Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index];
                if (hit == null || hit.transform.IsChildOf(transform) ||
                    Project.Mechanisms.ButtonPressureGeometry.IsPressureVolume(hit) ||
                    Physics.GetIgnoreLayerCollision(shape.gameObject.layer, hit.gameObject.layer) ||
                    Physics.GetIgnoreCollision(shape, hit)) continue;
                // A player occupying the added cell blocks this whole-cell step
                // too. Never resize a collider around a player and rely on deep
                // penetration recovery; the pending request resumes when clear.
                return false;
            }
            return true;
        }
        private void SetLength(int value)
        {
            if (value == Length) return;
            Length = value; ApplyGeometry(); LengthChanged?.Invoke(Length);
        }
        private void ApplyGeometry()
        {
            if (shape == null) return;
            Transform body = shape.transform;
            if (body == transform) return;
            body.localPosition = localGrowthDirection * ((Length - 1) * cellSize * .5f);
            body.localRotation = Quaternion.FromToRotation(Vector3.right, localGrowthDirection);
            body.localScale = Vector3.one;
            shape.size = new Vector3(Length, 1, 1) * cellSize;
            shape.center = Vector3.zero;
            if (model != null) model.localScale = shape.size;
        }
        private void UpdateJets()
        {
            if (color != CubeColor.Blue) return;
            foreach (ContactCube neighbour in neighbours)
            {
                if (neighbour.Color != CubeColor.Red) continue;
                if (!jets.TryGetValue(neighbour, out CubeWaterJet jet) || jet == null)
                {
                    GameObject go = new GameObject("水柱");
                    go.transform.SetParent(transform, false);
                    jet = go.AddComponent<CubeWaterJet>(); jets[neighbour] = jet;
                }
                jet.Configure(this, neighbour, jetLengthCells * cellSize, jetWidthCells * cellSize, jetSpeed * cellSize, jetMaterial,
                    jetPlayerPushMultiplier,jetBubbleDensity,jetBubbleSpeed);
            }
            staleJets.Clear();
            foreach (var pair in jets) if (pair.Key == null || !neighbours.Contains(pair.Key) || !pair.Key.isActiveAndEnabled) staleJets.Add(pair.Key);
            foreach (ContactCube key in staleJets) { if (jets[key] != null) { jets[key].gameObject.SetActive(false); Destroy(jets[key].gameObject); } jets.Remove(key); }
        }
        private void ClearContacts()
        {
            playerGrowthRequested = false;
            neighbours.Clear(); touchingTargets.Clear(); appliedTargets.Clear(); selectedBlue = null; elapsed = 0;
            collisionNeighbours.Clear(); staleCollisionNeighbours.Clear();
            if (State != CubeReactionState.Idle) { State = CubeReactionState.Idle; StateChanged?.Invoke(State); }
            foreach (CubeWaterJet jet in jets.Values) if (jet != null) { jet.gameObject.SetActive(false); Destroy(jet.gameObject); }
            jets.Clear();
        }
        public void ResetForRoom() { ClearContacts(); localGrowthDirection = Vector3.right; Length = Mathf.Clamp(initialLength, 1, maximumLength); ApplyGeometry(); LengthChanged?.Invoke(Length); }
        private void OnDisable() => ClearContacts();
        private void OnValidate() { maximumLength = Mathf.Max(1, maximumLength); initialLength = Mathf.Clamp(initialLength, 1, maximumLength); stepSeconds = Mathf.Max(.05f, stepSeconds); }
    }
}
