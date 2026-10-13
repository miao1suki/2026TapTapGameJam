using System;
using Project.ColorBlocks;
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
        private PlayerColorWheel colorWheel;
        private WordNoteStyle carryHintStyle;
        private WordNoteStyle bottomFeedbackStyle;
        private WordNoteStyle respawnHintStyle;
        private WordNoteStyle colorSelectionStyle;
        private bool carryHintVisible;
        private bool respawnHintVisible;
        private int carryHintId = -1;
        private int invalidPlacementId = -1;
        private int respawnHintId = -1;
        private int colorSelectionHintId = -1;
        private int colorDyeFeedbackId = -1;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            colorWheel = GetComponent<PlayerColorWheel>();
        }

        private void OnEnable()
        {
            player = GetComponent<PlayerController>();
            colorWheel = GetComponent<PlayerColorWheel>();
            BindCarryEvents();
            BindColorWheelEvents();
        }

        private void OnDisable()
        {
            UnbindCarryEvents();
            UnbindColorWheelEvents();
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
                BindColorWheelEvents();
            }

            if (colorWheel == null)
            {
                BindColorWheelEvents();
            }
            UpdateCarryHint(target);
            UpdateRespawnHint(target);
            bool resetTriggered =
                GameInput.WasTriggeredThisFrame(
                    InputActionId.Respawn);
            if (target.IsDead)
            {
                if (resetTriggered && Time.timeScale > 0f)
                {
                    target.RequestRespawn();
                    HideRespawnHint();
                }

                target.ClearBufferedInput();
                target.SetJumpHeld(false);
                return;
            }

            if (resetTriggered && Time.timeScale > 0f &&
                (!target.IsControlLocked || target.IsCarryPreparing || target.IsCarryDropAnimating || target.IsCarryResetAnimating))
            {
                target.RequestRespawn();
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
                    false);

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
                target.SetJumpHeld(GameInput.IsPressed(InputActionId.Jump));
                if (target.CanCarryPlantJump && GameInput.WasPressedThisFrame(InputActionId.Jump)) target.RequestJump();
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
                    useHorizontalMove = true,
                    horizontalDirection =
                        WordNoteHorizontalDirection.Left,
                    horizontalDistance = 140f,
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
                    useHorizontalMove = true,
                    horizontalDirection =
                        WordNoteHorizontalDirection.Right,
                    horizontalDistance = 140f,
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
                return "按下放置左侧或放置右侧以放置方块，按下复活以返回重生点";
            }

            return
                $"按{InputPromptUtility.FormatKey(InputActionId.DropLeft)}/" +
                $"{InputPromptUtility.FormatKey(InputActionId.DropRight)}" +
                "将方块放置在左/右侧，" +
                $"按{InputPromptUtility.FormatKey(InputActionId.Respawn)}" +
                "返回重生点";
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

            WordNoteHandle handle = wordNote.Show(
                "位置非法，请留出足够空间",
                GetBottomFeedbackStyle());
            invalidPlacementId = handle != null ? handle.Id : -1;
        }

        private void OnColorApplicationSucceeded()
        {
            ShowColorDyeFeedback("染色成功");
        }

        private void OnColorApplicationRejected()
        {
            ShowColorDyeFeedback("这个物体好像不能与这个颜色反应");
        }

        private void OnColorApplicationOutOfRange()
        {
            ShowColorDyeFeedback("无法触碰到这个物体");
        }

        private void ShowColorDyeFeedback(string content)
        {
            WordNoteUI wordNote = WordNoteUI.Instance;
            if (wordNote == null)
            {
                return;
            }

            if (colorDyeFeedbackId >= 0)
            {
                wordNote.ExitById(colorDyeFeedbackId, false);
                colorDyeFeedbackId = -1;
            }

            WordNoteHandle handle = wordNote.Show(
                content,
                GetBottomFeedbackStyle());
            colorDyeFeedbackId =
                handle != null ? handle.Id : -1;
        }

        private WordNoteStyle GetBottomFeedbackStyle()
        {
            bottomFeedbackStyle ??= new WordNoteStyle
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
            return bottomFeedbackStyle;
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

        private void BindColorWheelEvents()
        {
            if (colorWheel == null)
            {
                colorWheel = GetComponent<PlayerColorWheel>();
            }

            if (colorWheel == null)
            {
                return;
            }

            colorWheel.SelectedColorChanged -=
                OnSelectedColorChanged;
            colorWheel.SelectedColorChanged +=
                OnSelectedColorChanged;
            colorWheel.ColorApplicationSucceeded -=
                OnColorApplicationSucceeded;
            colorWheel.ColorApplicationSucceeded +=
                OnColorApplicationSucceeded;
            colorWheel.ColorApplicationOutOfRange -=
                OnColorApplicationOutOfRange;
            colorWheel.ColorApplicationOutOfRange +=
                OnColorApplicationOutOfRange;
            colorWheel.ColorApplicationRejected -=
                OnColorApplicationRejected;
            colorWheel.ColorApplicationRejected +=
                OnColorApplicationRejected;
        }

        private void UnbindColorWheelEvents()
        {
            if (colorWheel == null)
            {
                return;
            }

            colorWheel.SelectedColorChanged -=
                OnSelectedColorChanged;
            colorWheel.ColorApplicationSucceeded -=
                OnColorApplicationSucceeded;
            colorWheel.ColorApplicationOutOfRange -=
                OnColorApplicationOutOfRange;
            colorWheel.ColorApplicationRejected -=
                OnColorApplicationRejected;
        }

        private void OnSelectedColorChanged(string colorId)
        {
            WordNoteUI wordNote = WordNoteUI.Instance;
            if (wordNote == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(colorId))
            {
                return;
            }

            if (colorSelectionHintId >= 0)
            {
                wordNote.ExitById(colorSelectionHintId, false);
                colorSelectionHintId = -1;
            }

            ColorTypeDefinition definition =
                ColorRuntimeService.Existing?.Catalog?.Find(
                    colorId);
            string colorName =
                definition != null &&
                !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : colorId;
            colorSelectionStyle ??= new WordNoteStyle
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
                enterAnimation = WordNoteAnimation.Fade,
                enterMotion = new WordNoteMotionSettings
                {
                    useCombinedChannels = true,
                    useFade = true,
                    useHorizontalMove = true,
                    horizontalDirection =
                        WordNoteHorizontalDirection.Left,
                    horizontalDistance = 140f,
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
                    useHorizontalMove = true,
                    horizontalDirection =
                        WordNoteHorizontalDirection.Right,
                    horizontalDistance = 140f,
                    duration = .5f,
                    startAlpha = 1f,
                    endAlpha = 0f,
                    startScale = Vector3.one,
                    endScale = Vector3.one
                }
            };
            WordNoteHandle handle = wordNote.Show(
                $"当前选择<color=#{ColorUtility.ToHtmlStringRGB(definition != null ? definition.swatch : Color.white)}>{colorName}色</color>",
                colorSelectionStyle);
            colorSelectionHintId =
                handle != null ? handle.Id : -1;
        }

    }
}
