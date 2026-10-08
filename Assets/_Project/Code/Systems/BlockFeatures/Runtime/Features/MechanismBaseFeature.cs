using System;
using System.Collections.Generic;
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
        IMechanismSignalSource
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

        private readonly Collider[] overlapBuffer = new Collider[32];
        private bool powered;
        private bool energyDetected;

        public bool IsSignalActive => powered && CanRunFeature;
        public event Action<bool> SignalChanged;

        protected override void OnAttach()
        {
            powered = false;
            energyDetected = false;
            ScanForEnergy();
        }

        protected override void OnDetach()
        {
            SetPowered(false);
        }

        protected override void OnTick(float deltaTime)
        {
            ScanForEnergy();
        }

        protected override void OnResetForRoom()
        {
            energyDetected = false;
            SetPowered(false);
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "能源接触",
                energyDetected ? "检测到" : "未检测到"));
            values.Add(new BlockDebugValue(
                "输出状态",
                IsSignalActive ? "开启" : "关闭"));
        }

        private void ScanForEnergy()
        {
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

        private bool HasEnergyAt(Vector3 offset)
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
                if (energy != null && energy.IsEnergyAvailable)
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
            SignalChanged?.Invoke(IsSignalActive);
        }
    }
}
