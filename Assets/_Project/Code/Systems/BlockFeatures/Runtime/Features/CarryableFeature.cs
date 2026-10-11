using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>
    /// Shared carry ability. An object with an IColorObject is only carryable
    /// while that color group is active; objects without one are always valid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarryableFeature : MonoBehaviour,
        IPlayerCarryTarget
    {
        [SerializeField] private bool canCarry = true;

        private CarryTargetMotion carryMotion;
        private IPlayerCarryHost carryPlayer;
        private IColorObject colorObject;

        public Transform CarryTransform => transform;
        public bool IsCarryActive =>
            carryMotion != null && carryMotion.IsCarrying;
        public bool IsCarryDropAnimating =>
            carryMotion != null && carryMotion.IsDropAnimating;
        public bool IsCarryResetAnimating =>
            carryMotion != null && carryMotion.IsResetAnimating;
        public bool CanCarryNow =>
            isActiveAndEnabled &&
            canCarry &&
            !IsCarryDropAnimating &&
            !IsCarryResetAnimating &&
            (colorObject == null || colorObject.IsActive);

        private void Awake()
        {
            colorObject = GetComponent<IColorObject>();
        }

        private void OnEnable()
        {
            if (colorObject == null)
            {
                colorObject = GetComponent<IColorObject>();
            }

            PlayerCarryTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            PlayerCarryTargetRegistry.Unregister(this);
            CancelCarry();
        }

        public bool CanCarry(IPlayerCarryHost player)
        {
            if (!CanCarryNow ||
                player == null)
            {
                return false;
            }

            return true;
        }

        public bool BeginCarry(IPlayerCarryHost player)
        {
            if (!CanCarry(player))
            {
                return false;
            }

            carryMotion ??= new CarryTargetMotion(transform);
            if (!carryMotion.BeginCarry(
                    player.CarryAnchorWorldPosition,
                    player.CarryCenterWorldPosition))
            {
                return false;
            }

            carryPlayer = player;
            return true;
        }

        public void UpdateCarryAnchor(Vector3 anchorWorldPosition)
        {
            carryMotion?.UpdateAnchor(anchorWorldPosition);
        }

        public bool BeginCarryDrop(
            IPlayerCarryHost player,
            Vector3 worldPosition)
        {
            if (player == null || carryPlayer != player)
            {
                return false;
            }

            return carryMotion != null &&
                   carryMotion.BeginDrop(worldPosition);
        }

        public bool BeginCarryReset(IPlayerCarryHost player)
        {
            if (player == null || carryPlayer != player)
            {
                return false;
            }

            return carryMotion != null &&
                   carryMotion.BeginReset();
        }

        public void CancelCarry()
        {
            carryMotion?.Cancel();
            carryPlayer = null;
        }
    }
}
