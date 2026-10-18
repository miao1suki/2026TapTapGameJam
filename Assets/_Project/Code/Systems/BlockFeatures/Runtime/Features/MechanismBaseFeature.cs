using System;
using System.Collections.Generic;
using Project.GreenHook;
using Project.Audio;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "机关基座",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Mechanism,
        Interactions = BlockFeatureInteraction.RoomReset,
        Provides = new[] { typeof(IMechanismSignalSource) },
        Writes = new[] { BlockChannel.Power })]
    public sealed class MechanismBaseFeature : BlockFeature,
        IMechanismSignalSource,
        IGreenHookTarget,
        ICarryMotionParticipant
    {
        [BlockParameter(
            Label = "检测左方",
            Group = "能源检测",
            Order = 0)]
        [SerializeField] private bool checkLeft = true;

        [BlockParameter(
            Label = "检测右方",
            Group = "能源检测",
            Order = 1)]
        [SerializeField] private bool checkRight = true;

        [BlockParameter(
            Label = "检测上方",
            Group = "能源检测",
            Order = 2)]
        [SerializeField] private bool checkUp = true;

        [BlockParameter(
            Label = "检测下方",
            Group = "能源检测",
            Order = 3)]
        [SerializeField] private bool checkDown = true;

        [BlockParameter(Label = "启用重力", Group = "物理", Order = 0,
            Tooltip = "开启后基座会向下落至实体支撑面；玩家不能推动。关闭后保持悬浮位置，仍可搬运与绿钩子拉动。")]
        [SerializeField] private bool useGravity = true;

        private readonly Collider[] overlapBuffer = new Collider[32];
        private bool powered;
        private bool energyDetected;
        private bool carrySuspended;
        private Rigidbody carryBody;
        private bool beforeCarryKinematic, beforeCarryGravity;
        private RigidbodyConstraints beforeCarryConstraints;
        private Rigidbody motionBody;
        private GreenHookBodyMotion hookMotion;
        private Collider supportShape;
        private readonly RaycastHit[] supportHits = new RaycastHit[32];
        public bool UsesGravity => useGravity;

        public bool IsSignalActive => powered && CanRunFeature && !carrySuspended;
        public GreenHookMotionMode GreenHookMotionMode =>
            GreenHookMotionMode.PullTarget;
        public Transform GreenHookTransform => transform;
        public bool CanBeGreenHooked(GameObject actor) =>
            CanRunFeature && !carrySuspended;
        public event Action<bool> SignalChanged;

        protected override void OnAttach()
        {
            motionBody = GetComponent<Rigidbody>();
            if (motionBody == null) motionBody = gameObject.AddComponent<Rigidbody>();
            supportShape = GetComponent<Collider>();
            hookMotion = GetComponent<GreenHookBodyMotion>() ?? gameObject.AddComponent<GreenHookBodyMotion>();
            // Also upgrades already-authored scene instances without a map rebuild.
            if (GetComponent<CarryableFeature>() == null) gameObject.AddComponent<CarryableFeature>();
            var member = GetComponent<Project.Rooms.RoomResetObject>();
            if (member != null && member.Manager != null) member.Manager.Register(member);
            carrySuspended = false;
            powered = false;
            energyDetected = false;
            RefreshGravityMotion();
            ScanForEnergy();
        }

        protected override void OnDetach()
        {
            VineGrowthLift.Cancel(motionBody);
            GetComponent<CarryableFeature>()?.CancelCarry();
            SetCarrySuspended(false);
            hookMotion?.ResetForRoom();
            if (motionBody != null)
            {
                if (!motionBody.isKinematic) { motionBody.linearVelocity = Vector3.zero; motionBody.angularVelocity = Vector3.zero; }
                motionBody.isKinematic = true;
                motionBody.useGravity = false;
            }
            SetPowered(false);
        }

        protected override void OnTick(float deltaTime)
        {
            if (!carrySuspended) ScanForEnergy();
        }

        protected override void OnResetForRoom()
        {
            VineGrowthLift.Cancel(motionBody);
            SetCarrySuspended(false);
            hookMotion?.ResetForRoom();
            RefreshGravityMotion();
            energyDetected = false;
            SetPowered(false);
        }

        public void SetCarrySuspended(bool suspended)
        {
            if (suspended) VineGrowthLift.Cancel(motionBody);
            if (carrySuspended == suspended) return;
            carrySuspended = suspended;
            if (suspended)
            {
                hookMotion?.ResetForRoom();
                carryBody = GetComponent<Rigidbody>();
                if (carryBody != null)
                {
                    beforeCarryKinematic = carryBody.isKinematic;
                    beforeCarryGravity = carryBody.useGravity;
                    beforeCarryConstraints = carryBody.constraints;
                    if (!carryBody.isKinematic)
                    { carryBody.linearVelocity = Vector3.zero; carryBody.angularVelocity = Vector3.zero; }
                    carryBody.isKinematic = true;
                    carryBody.useGravity = false;
                }
                energyDetected = false;
                SetPowered(false);
            }
            else
            {
                if (carryBody != null)
                {
                    carryBody.position = transform.position;
                    carryBody.rotation = transform.rotation;
                    carryBody.constraints = beforeCarryConstraints;
                    carryBody.useGravity = beforeCarryGravity;
                    carryBody.isKinematic = beforeCarryKinematic;
                    if (!carryBody.isKinematic)
                    { carryBody.linearVelocity = Vector3.zero; carryBody.angularVelocity = Vector3.zero; }
                    carryBody = null;
                }
                RefreshGravityMotion();
                if (CanRunFeature) ScanForEnergy();
            }
        }

        private void FixedUpdate()
        {
            if (CanRunFeature) RefreshGravityMotion();
        }

        private void RefreshGravityMotion()
        {
            if (motionBody == null || carrySuspended || VineGrowthLift.IsLifting(motionBody) || (hookMotion != null && hookMotion.IsHookMoving)) return;
            if (!useGravity)
            {
                if (!motionBody.isKinematic) { motionBody.linearVelocity = Vector3.zero; motionBody.angularVelocity = Vector3.zero; }
                motionBody.useGravity = false;
                motionBody.isKinematic = true;
                return;
            }
            if (motionBody.isKinematic) motionBody.isKinematic = false;
            if (!motionBody.useGravity) motionBody.useGravity = true;
            bool supported = HasSolidSupport() && Mathf.Abs(motionBody.linearVelocity.y) <= .08f;
            var constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezePositionX | (supported ? RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            if (motionBody.constraints != constraints) motionBody.constraints = constraints;
        }

        private bool HasSolidSupport()
        {
            if (supportShape == null || !supportShape.enabled) return false;
            Bounds bounds = supportShape.bounds;
            float cell = GridCellWorldSize;
            float inset = .01f * cell;
            float halfHeight = .005f * cell;
            var half = new Vector3(Mathf.Max(.005f * cell, bounds.extents.x - inset), halfHeight,
                Mathf.Max(.005f * cell, bounds.extents.z - inset));
            var origin = new Vector3(bounds.center.x, bounds.min.y + inset + halfHeight, bounds.center.z);
            int count = Physics.BoxCastNonAlloc(origin, half, Vector3.down, supportHits,
                Quaternion.identity, inset * 2f + supportShape.contactOffset, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = supportHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerController>() != null ||
                    Physics.GetIgnoreLayerCollision(supportShape.gameObject.layer, hit.gameObject.layer) ||
                    Physics.GetIgnoreCollision(supportShape, hit)) continue;
                if (supportHits[i].normal.y > .5f) return true;
            }
            return false;
        }

        // CarryTargetMotion owns the real volume check and RoomResetObject owns boundaries.
        public bool CanPlaceCarriedAt(Vector3 worldPosition) => true;

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "能源接触",
                energyDetected ? "检测到" : "未检测到"));
            values.Add(new BlockDebugValue(
                "输出状态",
                IsSignalActive ? "开启" : "关闭"));
            values.Add(new BlockDebugValue("搬运状态", carrySuspended ? "搬运中" : "未搬运"));
            values.Add(new BlockDebugValue("重力", useGravity ? "开启" : "关闭"));
        }

        private void ScanForEnergy()
        {
            if (carrySuspended)
            {
                energyDetected = false;
                SetPowered(false);
                return;
            }
            float cellSize = GridCellWorldSize;
            bool found = false;
            if (checkLeft)
            {
                found |= HasEnergyAt(Vector3.left * cellSize);
            }

            if (checkRight)
            {
                found |= HasEnergyAt(Vector3.right * cellSize);
            }

            if (checkUp)
            {
                found |= HasEnergyAt(Vector3.up * cellSize);
            }

            if (checkDown)
            {
                found |= HasEnergyAt(Vector3.down * cellSize);
            }

            energyDetected = found;
            SetPowered(found);
        }

        /// <summary>Read-only source-specific contact; another red cube powering this base is not a match.</summary>
        public bool IsPoweredBy(IEnergySource source)
        {
            if (source == null || !source.IsEnergyAvailable || !IsSignalActive) return false;
            float cell = GridCellWorldSize;
            return checkLeft && HasEnergyAt(Vector3.left * cell, source) ||
                checkRight && HasEnergyAt(Vector3.right * cell, source) ||
                checkUp && HasEnergyAt(Vector3.up * cell, source) ||
                checkDown && HasEnergyAt(Vector3.down * cell, source);
        }

        private bool HasEnergyAt(Vector3 offset, IEnergySource requiredSource = null)
        {
            float cellSize = GridCellWorldSize;
            int count = Physics.OverlapBoxNonAlloc(
                transform.position + offset,
                Vector3.one * (cellSize * .45f),
                overlapBuffer,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                Collider collider = overlapBuffer[index];
                if (collider == null ||
                    collider.gameObject == gameObject)
                {
                    continue;
                }

                IEnergySource energy =
                    collider.GetComponentInParent<IEnergySource>();
                if (energy != null && energy.IsEnergyAvailable &&
                    (requiredSource == null || ReferenceEquals(energy, requiredSource)))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetPowered(bool value)
        {
            if (powered == value)
            {
                return;
            }

            powered = value;
            if (Application.isPlaying)
                AudioCuePlayer.Play(value ? AudioCueIds.MachineStart : AudioCueIds.MachineStop);
            SignalChanged?.Invoke(IsSignalActive);
        }
    }
}
