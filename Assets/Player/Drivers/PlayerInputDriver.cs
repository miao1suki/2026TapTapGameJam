using System;
using Project.CameraModes;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.Player
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInputDriver :
        MonoBehaviour,
        IPlayerDriver,
        ICameraViewModeRequester
    {
        [SerializeField]
        private PlayerActionBinding[] actionBindings =
            Array.Empty<PlayerActionBinding>();

        [SerializeField]
        private CameraModeController cameraModeController;

        [SerializeField, Min(0f)]
        private float cameraModeTransitionDuration = .2f;

        private PlayerController player;
        private PlayerInteractionSensor interactionSensor;
        private CameraViewModeRequestHandle cameraModeRequestHandle;

        public string CameraModeRequesterName => "Player Input";
        public int CameraModeRequestPriority =>
            CameraControlPriorities.GameplayAbility;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.ClearBufferedInput();
            }

            ReleaseCameraModeRequest();
        }

        private void Update()
        {
            if (player == null)
            {
                player = GetComponent<PlayerController>();
            }

            Drive(player);
        }

        public void Drive(PlayerController target)
        {
            if (target == null)
            {
                return;
            }

            player = target;
            EnsureInteractionSensor();
            interactionSensor?.RefreshTarget();
            UpdateInteraction(target);

            if (target.IsControlLocked || Time.timeScale == 0f)
            {
                target.ClearBufferedInput();
                target.SetJumpHeld(false);
                return;
            }

            target.SetMoveInput(
                GameInput.ReadVector2(InputActionId.Move));
            target.SetSprintInput(
                GameInput.IsPressed(InputActionId.Sprint));

            if (GameInput.WasTriggeredThisFrame(InputActionId.Jump))
            {
                target.RequestJump();
            }
            target.SetJumpHeld(
                GameInput.IsPressed(InputActionId.Jump));

            if (GameInput.WasTriggeredThisFrame(
                    InputActionId.CameraModeSwitch))
            {
                ToggleCameraMode();
            }

            for (int index = 0;
                 index < actionBindings.Length;
                 index++)
            {
                PlayerActionBinding binding = actionBindings[index];
                if (binding.action == null ||
                    !GameInput.WasTriggeredThisFrame(
                        binding.inputAction))
                {
                    continue;
                }

                target.TryPlayAction(binding.action);
            }
        }

        private void UpdateInteraction(PlayerController target)
        {
            if (interactionSensor == null)
            {
                return;
            }

            if (!GameInput.WasTriggeredThisFrame(
                    InputActionId.Interact))
            {
                return;
            }

            if (target.CurrentStateId != PlayerStateId.Normal ||
                HasActionBinding(InputActionId.Interact) ||
                !interactionSensor.HasTarget)
            {
                return;
            }

            interactionSensor.TryInteract();
        }

        private void EnsureInteractionSensor()
        {
            if (interactionSensor == null)
            {
                interactionSensor =
                    GetComponent<PlayerInteractionSensor>();
            }
        }

        private bool HasActionBinding(InputActionId id)
        {
            for (int index = 0;
                 index < actionBindings.Length;
                 index++)
            {
                PlayerActionBinding binding = actionBindings[index];
                if (binding.inputAction == id &&
                    binding.action != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ToggleCameraMode()
        {
            ResolveCameraModeController();
            if (cameraModeController == null)
            {
                return;
            }

            CameraViewMode next =
                cameraModeController.TargetMode ==
                CameraViewMode.Side2D
                    ? CameraViewMode.Perspective3D
                    : CameraViewMode.Side2D;

            if (cameraModeRequestHandle.IsValid)
            {
                cameraModeRequestHandle.Release(true);
            }

            CameraTransition transition =
                cameraModeTransitionDuration <= 0f
                    ? CameraTransition.Immediate
                    : CameraTransition.Ease(
                        cameraModeTransitionDuration);
            cameraModeRequestHandle =
                cameraModeController.RequestMode(
                    this,
                    next,
                    transition);
        }

        private void ResolveCameraModeController()
        {
            if (cameraModeController == null)
            {
                cameraModeController =
                    FindFirstObjectByType<CameraModeController>();
            }
        }

        private void ReleaseCameraModeRequest()
        {
            if (cameraModeRequestHandle.IsValid)
            {
                cameraModeRequestHandle.Release(true);
                cameraModeRequestHandle = default;
            }
        }
    }
}
