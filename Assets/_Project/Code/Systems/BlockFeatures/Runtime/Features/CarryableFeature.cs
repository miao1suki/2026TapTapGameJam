using Project.ColorBlocks;
using Project.InputAbstraction;
using Project.Player;
using TMPro;
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
        [SerializeField] private TMP_FontAsset indicatorFont;
        [SerializeField, Min(1f)] private float indicatorFontSize = 2.7f;
        [SerializeField] private Vector3 indicatorOffset;
        [SerializeField, Min(0f)] private float indicatorOutlineWidth = .1f;

        private CarryTargetMotion carryMotion;
        private IPlayerCarryHost carryPlayer;
        private IColorObject colorObject;
        private TextMeshPro indicator;
        private Material indicatorMaterial;

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

        public void SetCarryIndicator(bool visible)
        {
            if (!visible)
            {
                if (indicator != null)
                {
                    indicator.gameObject.SetActive(false);
                }

                return;
            }

            EnsureIndicator();
            if (indicator == null)
            {
                return;
            }

            indicator.gameObject.SetActive(true);
            indicator.transform.localPosition = indicatorOffset;
            indicator.text = InputPromptUtility.UseActionLabels
                ? "搬运"
                : InputPromptUtility.FormatKey(
                    InputActionId.Interact) +
                  "\n搬运";
            indicator.font = indicatorFont != null
                ? indicatorFont
                : TMP_Settings.defaultFontAsset;
            indicator.fontSize = indicatorFontSize;
            indicator.color = Color.black;
            indicator.fontStyle = FontStyles.Bold;
            indicator.alignment = TextAlignmentOptions.Center;
            indicator.textWrappingMode = TextWrappingModes.NoWrap;
            indicator.sortingLayerID =
                SortingLayer.layers.Length > 0
                    ? SortingLayer.layers[
                        SortingLayer.layers.Length - 1].id
                    : 0;
            indicator.sortingOrder = short.MaxValue;
            ApplyIndicatorOutline();
        }

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
            SetCarryIndicator(false);
        }

        private void OnDestroy()
        {
            if (indicatorMaterial != null)
            {
                Destroy(indicatorMaterial);
                indicatorMaterial = null;
            }
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

        private void EnsureIndicator()
        {
            if (indicator != null)
            {
                return;
            }

            GameObject label = new GameObject(
                "搬运",
                typeof(TextMeshPro));
            label.transform.SetParent(transform, false);
            indicator = label.GetComponent<TextMeshPro>();
            indicator.raycastTarget = false;
        }

        private void ApplyIndicatorOutline()
        {
            TMP_FontAsset font = indicator.font;
            if (font == null || font.material == null)
            {
                return;
            }

            if (indicatorMaterial == null)
            {
                indicatorMaterial = new Material(font.material);
                indicator.fontMaterial = indicatorMaterial;
            }

            indicatorMaterial.SetColor(
                ShaderUtilities.ID_FaceColor,
                Color.black);
            indicatorMaterial.SetColor(
                ShaderUtilities.ID_OutlineColor,
                Color.white);
            indicatorMaterial.SetFloat(
                ShaderUtilities.ID_OutlineWidth,
                indicatorOutlineWidth);
            indicatorMaterial.SetFloat(
                ShaderUtilities.ID_OutlineSoftness,
                0f);
            indicatorMaterial.EnableKeyword("OUTLINE_ON");
            indicatorMaterial.renderQueue = 5000;
            indicatorMaterial.SetInt("_ZTestMode", 8);
            if (indicator.renderer != null)
            {
                indicator.renderer.sortingLayerID =
                    SortingLayer.layers.Length > 0
                        ? SortingLayer.layers[
                            SortingLayer.layers.Length - 1].id
                        : 0;
                indicator.renderer.sortingOrder = short.MaxValue;
            }
        }
    }
}
