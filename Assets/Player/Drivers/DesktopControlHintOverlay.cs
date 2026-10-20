using System.Text;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.InputAbstraction;
using Project.WordNote;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Player
{
    [DisallowMultipleComponent]
    public sealed class DesktopControlHintOverlay : MonoBehaviour
    {
        private const float FontSize = 17f;
        private const float EdgePadding = 14f;
        private const float RefreshInterval = .1f;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private readonly StringBuilder builder = new StringBuilder(192);
        private PlayerController player;
        private PlayerInputDriver inputDriver;
        private PlayerColorWheel colorWheel;
        private WordNoteUI wordNote;
        private RectTransform safeRoot;
        private ScreenSafeArea safeArea;
        private RectTransform hintRect;
        private TextMeshProUGUI hint;
        private Material hintMaterial;
        private InputDeviceMode lastDevice;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private Rect lastSafeArea;
        private float nextRefresh;
        private string lastText = string.Empty;

        public void Configure(
            PlayerController owner,
            PlayerInputDriver driver,
            PlayerColorWheel wheel)
        {
            player = owner;
            inputDriver = driver;
            colorWheel = wheel;
            nextRefresh = 0f;
        }

        private void OnEnable()
        {
            nextRefresh = 0f;
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (hintMaterial != null)
            {
                Destroy(hintMaterial);
                hintMaterial = null;
            }
        }

        private void Update()
        {
            if (!CanShow())
            {
                SetVisible(false);
                return;
            }

            if (!EnsureView())
            {
                SetVisible(false);
                return;
            }

            bool screenChanged = lastScreenWidth != Screen.width ||
                                 lastScreenHeight != Screen.height ||
                                 lastSafeArea != Screen.safeArea;
            bool deviceChanged = lastDevice != GameInput.ActiveDeviceMode;
            if (!screenChanged && !deviceChanged && Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + RefreshInterval;
            lastDevice = GameInput.ActiveDeviceMode;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
            RefreshText(screenChanged);
        }

        private bool CanShow()
        {
            if (GameInput.PlatformMode != InputPlatformMode.Desktop ||
                player == null || inputDriver == null ||
                !player.isActiveAndEnabled || !inputDriver.isActiveAndEnabled ||
                player.IsRespawning || GameplayPresentationGate.IsSuppressed ||
                Time.timeScale <= 0f)
            {
                return false;
            }

            GameFlowController flow = GameFlowController.Instance;
            return flow == null ||
                   !flow.IsTransitioning &&
                   flow.ActiveSceneId == GameFlowSceneId.Gaming;
        }

        private bool EnsureView()
        {
            WordNoteUI current = WordNoteUI.Instance;
            if (current == null)
            {
                wordNote = null;
                safeRoot = null;
                hintRect = null;
                hint = null;
                return false;
            }

            if (wordNote == current && safeRoot != null && hint != null)
            {
                return true;
            }

            wordNote = current;
            BuildView();
            return safeRoot != null && hint != null;
        }

        private void BuildView()
        {
            if (hintMaterial != null)
            {
                Destroy(hintMaterial);
                hintMaterial = null;
            }

            var rootObject = new GameObject(
                "DesktopControlHints",
                typeof(RectTransform),
                typeof(ScreenSafeArea));
            safeRoot = rootObject.GetComponent<RectTransform>();
            safeArea = rootObject.GetComponent<ScreenSafeArea>();
            safeRoot.SetParent(wordNote.transform, false);
            safeRoot.anchorMin = Vector2.zero;
            safeRoot.anchorMax = Vector2.one;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;

            var hintObject = new GameObject(
                "CurrentControls",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            hintRect = hintObject.GetComponent<RectTransform>();
            hintRect.SetParent(safeRoot, false);
            hintRect.anchorMin = hintRect.anchorMax = TopLeft;
            hintRect.pivot = TopLeft;
            hintRect.anchoredPosition = new Vector2(EdgePadding, -EdgePadding);
            hint = hintObject.GetComponent<TextMeshProUGUI>();
            hint.font = wordNote.DefaultFont;
            hintMaterial = new Material(hint.fontSharedMaterial);
            hint.fontMaterial = hintMaterial;
            hint.fontSize = FontSize;
            hint.color = Color.white;
            hint.richText = true;
            hint.outlineColor = Color.black;
            hint.outlineWidth = .12f;
            hint.alignment = TextAlignmentOptions.TopLeft;
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.overflowMode = TextOverflowModes.Overflow;
            hint.raycastTarget = false;
            hint.lineSpacing = 4f;
            lastText = string.Empty;
            Canvas.ForceUpdateCanvases();
            ApplyWidth();
        }

        private void RefreshText(bool forceLayout)
        {
            safeArea?.Refresh();
            bool layoutChanged = ApplyWidth();
            string value = BuildHintText();
            if (layoutChanged || forceLayout || value != lastText)
            {
                lastText = value;
                hint.text = value;
                hint.ForceMeshUpdate(true, true);
                float width = hintRect.sizeDelta.x;
                float availableHeight = Mathf.Max(
                    FontSize,
                    safeRoot.rect.height - EdgePadding * 2f);
                float preferredHeight = Mathf.Max(FontSize, hint.preferredHeight);
                float height = Mathf.Min(preferredHeight, availableHeight);
                hintRect.sizeDelta = new Vector2(width, height);
                hint.overflowMode = preferredHeight > availableHeight
                    ? TextOverflowModes.Ellipsis
                    : TextOverflowModes.Overflow;
            }

            SetVisible(!string.IsNullOrWhiteSpace(value));
        }

        private bool ApplyWidth()
        {
            if (safeRoot == null || hintRect == null) return false;
            float width = Mathf.Max(
                1f,
                safeRoot.rect.width / 6f - EdgePadding * 2f);
            if (Mathf.Abs(hintRect.sizeDelta.x - width) < .01f)
            {
                return false;
            }

            hintRect.sizeDelta = new Vector2(width, hintRect.sizeDelta.y);
            return true;
        }

        private string BuildHintText()
        {
            builder.Clear();
            if (player == null) return string.Empty;

            if (player.IsDead)
            {
                AppendAction(InputActionId.Respawn, "复活");
                return builder.ToString();
            }

            if (player.IsControlLocked) return string.Empty;
            if (player.IsColorWheelOpen)
            {
                AppendAction(InputActionId.PointerPrimary, "选择颜色");
                AppendAction(InputActionId.Cancel, "关闭");
                return builder.ToString();
            }

            bool carrying = player.IsCarrying;
            AppendAction(InputActionId.Move, "移动");
            if (!carrying || player.CanCarryPlantJump)
                AppendAction(InputActionId.Jump, "跳跃");
            if (!player.IsSprintLocked)
                AppendAction(InputActionId.Sprint, "冲刺");

            if (carrying)
            {
                AppendAction(InputActionId.DropLeft, "放左");
                AppendAction(InputActionId.DropRight, "放右");
            }
            else if (player.IsGreenHookActive)
            {
                AppendAction(InputActionId.Interact, "脱钩");
            }
            else
            {
                if (player.HasContextInteraction || player.CarryQueueCount > 0)
                    AppendAction(InputActionId.Interact, "交互");
                AppendAction(InputActionId.CameraModeSwitch, "选色");
                if (colorWheel != null && colorWheel.HasSelectedColor)
                    AppendAction(InputActionId.PointerPrimary, "染色");
            }

            AppendAction(InputActionId.Respawn, "重生");
            AppendAction(InputActionId.WorldMap, "地图");
            AppendAction(InputActionId.Pause, "暂停");
            return builder.ToString();
        }

        private void AppendAction(InputActionId action, string label)
        {
            string binding =
                InputBindingDisplayUtility.GetPrimaryDisplayString(action);
            if (string.IsNullOrWhiteSpace(binding) ||
                binding == action.ToString())
            {
                return;
            }

            binding = binding.Replace(' ', '\u00A0');
            if (builder.Length > 0) builder.Append(' ');
            builder.Append("<b><color=#FFFFFF>");
            builder.Append(binding);
            builder.Append("</color></b><color=#C6DCE6>");
            builder.Append(label);
            builder.Append("</color>");
        }

        private void SetVisible(bool value)
        {
            if (safeRoot != null && safeRoot.gameObject.activeSelf != value)
                safeRoot.gameObject.SetActive(value);
        }
    }
}
