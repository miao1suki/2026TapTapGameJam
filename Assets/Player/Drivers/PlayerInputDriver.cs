using System;
using Project.InputAbstraction;
using Project.WordNote;
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
        private WordNoteStyle carryHintStyle;
        private WordNoteStyle invalidPlacementStyle;
        private WordNoteStyle respawnHintStyle;
        private bool carryHintVisible;
        private bool respawnHintVisible;
        private int carryHintId = -1;
        private int invalidPlacementId = -1;
        private int respawnHintId = -1;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            player = GetComponent<PlayerController>();
            BindCarryEvents();
        }

        private void OnDisable()
        {
            UnbindCarryEvents();
            if (player != null)
            {
                player.ClearBufferedInput();
                HideCarryHint();
                HideRespawnHint();
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

            if (player != target)
            {
                player = target;
                BindCarryEvents();
            }
            UpdateCarryHint(target);
            UpdateRespawnHint(target);
            bool resetTriggered =
                GameInput.WasTriggeredThisFrame(
                    InputActionId.Respawn);
            if (target.IsDead)
            {
                if (resetTriggered)
                {
                    target.RequestRespawn();
                    HideRespawnHint();
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
                !target.IsCarryDropAnimating &&
                !target.IsCarryResetAnimating;
            if (shouldShow == carryHintVisible)
            {
                return;
            }

            carryHintVisible = shouldShow;
            WordNoteUI wordNote = WordNoteUI.Instance;
            if (wordNote == null)
            {
                return;
            }

            if (!shouldShow)
            {
                if (carryHintId >= 0)
                {
                    wordNote.ExitById(carryHintId, false);
                    carryHintId = -1;
                }
                return;
            }

            carryHintStyle ??= new WordNoteStyle
            {
                font = carryHintFont,
                fontSize = 42f,
                fontColor = WordNoteColor.White,
                useOutline = true,
                outlineColor = WordNoteColor.Black,
                position = WordNotePosition.Bottom,
                participateInStackLayout = true,
                displayMode = WordNoteDisplayMode.Persistent,
                useEnterAnimation = true,
                enterAnimation = WordNoteAnimation.Fade,
                enterMotion = new WordNoteMotionSettings
                {
                    useCombinedChannels = true,
                    useFade = true,
                    duration = .5f,
                    startAlpha = 0f,
                    endAlpha = 1f,
                    startScale = Vector3.one,
                    endScale = Vector3.one
                },
                useExitAnimation = true,
                exitAnimation = WordNoteAnimation.Fade,
                exitMotion = new WordNoteMotionSettings
                {
                    useCombinedChannels = true,
                    useFade = true,
                    duration = .5f,
                    startAlpha = 1f,
                    endAlpha = 0f,
                    startScale = Vector3.one,
                    endScale = Vector3.one
                }
            };
            string prompt = BuildCarryPrompt();
            WordNoteHandle handle = wordNote.Show(
                prompt,
                carryHintStyle);
            carryHintId = handle != null ? handle.Id : -1;
        }

        private static string BuildCarryPrompt()
        {
            if (InputPromptUtility.UseActionLabels)
            {
                return "按下放置左侧以放置方块，按下放置右侧以放置方块，按下复活以重置方块";
            }

            return
                $"按{InputPromptUtility.FormatKey(InputActionId.DropLeft)}/" +
                $"{InputPromptUtility.FormatKey(InputActionId.DropRight)}" +
                "将方块放置在左/右侧，" +
                $"按{InputPromptUtility.FormatKey(InputActionId.Respawn)}" +
                "重置方块";
        }

        private void HideCarryHint()
        {
            if (!carryHintVisible)
            {
                return;
            }

            carryHintVisible = false;
            WordNoteUI wordNote = WordNoteUI.Instance;
            if (wordNote != null && carryHintId >= 0)
            {
                wordNote.ExitById(carryHintId, false);
            }

            carryHintId = -1;
        }

        private void OnCarryPlacementRejected()
        {
            WordNoteUI wordNote = WordNoteUI.Instance;
            if (wordNote == null)
            {
                return;
            }

            if (invalidPlacementId >= 0)
            {
                wordNote.ExitById(invalidPlacementId, false);
            }

            invalidPlacementStyle ??= new WordNoteStyle
            {
                font = carryHintFont,
                fontSize = 42f,
                fontColor = WordNoteColor.White,
                useOutline = true,
                outlineColor = WordNoteColor.Black,
                position = WordNotePosition.Bottom,
                participateInStackLayout = true,
                displayMode = WordNoteDisplayMode.Timed,
                displayDuration = 2f,
                useEnterAnimation = true,
                enterAnimation = WordNoteAnimation.MoveLeft,
                enterMotion = new WordNoteMotionSettings
                {
                    useCombinedChannels = true,
                    useFade = true,
                    useHorizontalMove = true,
                    horizontalDirection =
                        WordNoteHorizontalDirection.Left,
                    horizontalDistance = 140f,
                    duration = .5f,
                    startAlpha = 1f,
                    endAlpha = 1f,
                    startScale = Vector3.one,
                    endScale = Vector3.one
                },
                useExitAnimation = true,
                exitAnimation = WordNoteAnimation.MoveRight,
                exitMotion = new WordNoteMotionSettings
                {
                    useCombinedChannels = true,
                    useFade = true,
                    useHorizontalMove = true,
                    horizontalDirection =
                        WordNoteHorizontalDirection.Right,
                    horizontalDistance = 140f,
                    duration = .5f,
                    startAlpha = 1f,
                    endAlpha = 1f,
                    startScale = Vector3.one,
                    endScale = Vector3.one
                }
            };
            WordNoteHandle handle = wordNote.Show(
                "位置非法，请留出足够空间",
                invalidPlacementStyle);
            invalidPlacementId = handle != null ? handle.Id : -1;
        }

        private void UpdateRespawnHint(PlayerController target)
        {
            bool shouldShow = target.IsDead;
            if (shouldShow == respawnHintVisible)
            {
                return;
            }

            respawnHintVisible = shouldShow;
            WordNoteUI wordNote = WordNoteUI.Instance;
            if (wordNote == null)
            {
                return;
            }

            if (!shouldShow)
            {
                HideRespawnHint();
                return;
            }

            respawnHintStyle ??= new WordNoteStyle
            {
                font = carryHintFont,
                fontSize = 92f,
                fontColor = WordNoteColor.White,
                useOutline = true,
                outlineColor = WordNoteColor.Black,
                position = WordNotePosition.Center,
                participateInStackLayout = false,
                displayMode = WordNoteDisplayMode.Persistent,
                useEnterAnimation = false,
                useExitAnimation = false
            };
            WordNoteHandle handle = wordNote.Show(
                InputPromptUtility.FormatAction(
                    InputActionId.Respawn,
                    "复活",
                    "复活"),
                respawnHintStyle);
            respawnHintId = handle != null ? handle.Id : -1;
        }

        private void HideRespawnHint()
        {
            if (!respawnHintVisible && respawnHintId < 0)
            {
                return;
            }

            respawnHintVisible = false;
            WordNoteUI wordNote = WordNoteUI.Instance;
            if (wordNote != null && respawnHintId >= 0)
            {
                wordNote.ExitById(respawnHintId, false);
            }

            respawnHintId = -1;
        }

        private void BindCarryEvents()
        {
            if (player == null)
            {
                return;
            }

            player.CarryPlacementRejected -= OnCarryPlacementRejected;
            player.CarryPlacementRejected += OnCarryPlacementRejected;
        }

        private void UnbindCarryEvents()
        {
            if (player == null)
            {
                return;
            }

            player.CarryPlacementRejected -= OnCarryPlacementRejected;
        }

    }
}
