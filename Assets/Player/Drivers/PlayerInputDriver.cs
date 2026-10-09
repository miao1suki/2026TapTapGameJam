using System;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.Player
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInputDriver :
        MonoBehaviour,
        IPlayerDriver
    {
        [SerializeField]
        private PlayerActionBinding[] actionBindings =
            Array.Empty<PlayerActionBinding>();

        private PlayerController player;

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
            if (target.IsDead)
            {
                if (GameInput.WasTriggeredThisFrame(
                        InputActionId.Respawn))
                {
                    target.RequestRespawn();
                }

                target.ClearBufferedInput();
                target.SetJumpHeld(false);
                return;
            }

            if (target.IsPointerDragActive)
            {
                target.UpdatePointerDrag(
                    GameInput.PointerPosition,
                    Camera.main);
                target.ClearBufferedInput();
                target.SetJumpHeld(false);
                return;
            }

            if (target.IsControlLocked || Time.timeScale == 0f)
            {
                target.ClearBufferedInput();
                target.SetJumpHeld(false);
                return;
            }

            target.RefreshInteractionTarget();
            UpdateInteraction(target);
            target.UpdateCarry(
                GameInput.IsPressed(InputActionId.Carry),
                GameInput.WasTriggeredThisFrame(
                    InputActionId.Carry),
                GameInput.PointerPosition,
                Camera.main);
            if (target.IsPointerDragActive)
            {
                target.UpdatePointerDrag(
                    GameInput.PointerPosition,
                    Camera.main);
                target.ClearBufferedInput();
                target.SetJumpHeld(false);
                return;
            }

            UpdateColorWheel(target);

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
            bool interactHeld =
                GameInput.IsPressed(InputActionId.Interact);
            target.UpdateInteraction(
                interactHeld,
                GameInput.WasTriggeredThisFrame(
                    InputActionId.Interact),
                HasActionBinding(InputActionId.Interact));
        }

        private void UpdateColorWheel(PlayerController target)
        {
            bool holdMode =
                GameInput.GetActionTrigger(
                    InputActionId.CameraModeSwitch) ==
                InputActionTrigger.Hold;
            target.UpdateColorWheelInput(
                holdMode,
                GameInput.WasPressedThisFrame(
                    InputActionId.CameraModeSwitch),
                GameInput.WasReleasedThisFrame(
                    InputActionId.CameraModeSwitch),
                GameInput.WasTriggeredThisFrame(
                    InputActionId.CameraModeSwitch),
                GameInput.PointerPosition);

            if (target.IsColorWheelOpen)
            {
                if (GameInput.WasTriggeredThisFrame(
                        InputActionId.Cancel))
                {
                    target.CloseColorWheel();
                }
                else if (GameInput.WasTriggeredThisFrame(
                             InputActionId.PointerPrimary))
                {
                    target.CommitColorWheelSelection();
                }

                return;
            }

            if (GameInput.WasTriggeredThisFrame(
                    InputActionId.PointerPrimary))
            {
                target.TryUseSelectedColorAbility();
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

    }
}
