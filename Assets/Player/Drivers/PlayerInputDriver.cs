using System;
using Project.InputAbstraction;
using Project.Subtitles;
using TMPro;
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
        [SerializeField] private TMP_FontAsset carryHintFont;

        private PlayerController player;
        private SubtitleStyle carryHintStyle;
        private bool carryHintVisible;

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
                HideCarryHint();
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
            UpdateCarryHint(target);
            bool resetTriggered =
                GameInput.WasTriggeredThisFrame(
                    InputActionId.Respawn);
            if (target.IsDead)
            {
                if (resetTriggered)
                {
                    target.RequestRespawn();
                }

                target.ClearBufferedInput();
                target.SetJumpHeld(false);
                return;
            }

            bool carryInteractionConsumed =
                target.UpdateCarryInput(
                    GameInput.WasTriggeredThisFrame(
                        InputActionId.Interact),
                    GameInput.WasTriggeredThisFrame(
                        InputActionId.DropLeft),
                    GameInput.WasTriggeredThisFrame(
                        InputActionId.DropRight),
                    resetTriggered);

            if (target.IsCarryPreparing ||
                target.IsCarryDropAnimating ||
                target.IsCarryResetAnimating)
            {
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

            if (target.IsCarrying)
            {
                target.SetMoveInput(
                    GameInput.ReadVector2(InputActionId.Move));
                target.SetSprintInput(
                    GameInput.IsPressed(InputActionId.Sprint));
                target.SetJumpHeld(false);
                return;
            }

            target.RefreshInteractionTarget();
            if (!carryInteractionConsumed)
            {
                UpdateInteraction(target);
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
                false);
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

        private void UpdateCarryHint(PlayerController target)
        {
            bool shouldShow =
                target.IsCarrying &&
                !target.IsCarryResetAnimating;
            if (shouldShow == carryHintVisible)
            {
                return;
            }

            carryHintVisible = shouldShow;
            ISubtitleService subtitles =
                SubtitleServiceRegistry.Current;
            if (subtitles == null)
            {
                return;
            }

            if (!shouldShow)
            {
                subtitles.Hide(SubtitleLayer.Bottom);
                return;
            }

            carryHintStyle ??= new SubtitleStyle
            {
                font = carryHintFont,
                fontSize = 42f,
                anchor = SubtitleAnchor.Bottom,
                normalizedX = .5f,
                normalizedY = .12f,
                holdDuration = 999999f,
                inAnimation = SubtitleAnimation.Fade,
                outAnimation = SubtitleAnimation.Fade,
                inDuration = .15f,
                outDuration = .15f
            };
            string dropLeft =
                InputBindingDisplayUtility.GetPrimaryDisplayString(
                    InputActionId.DropLeft);
            string dropRight =
                InputBindingDisplayUtility.GetPrimaryDisplayString(
                    InputActionId.DropRight);
            string reset =
                InputBindingDisplayUtility.GetPrimaryDisplayString(
                    InputActionId.Respawn);
            subtitles.ShowBottom(
                $"按下 {dropLeft}/{dropRight} 放在自身左/右侧\n" +
                $"按 {reset} 重置搬运方块",
                carryHintStyle);
        }

        private void HideCarryHint()
        {
            if (!carryHintVisible)
            {
                return;
            }

            carryHintVisible = false;
            SubtitleServiceRegistry.Current?.Hide(
                SubtitleLayer.Bottom);
        }

    }
}
