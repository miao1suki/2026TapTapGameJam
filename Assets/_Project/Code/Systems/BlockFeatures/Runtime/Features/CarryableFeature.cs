using Project.ColorBlocks;
using Project.Audio;
using Project.InputAbstraction;
using Project.Player;
using Project.WordNote;
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
        IPlayerCarryTarget, IPlayerCarryCollisionTarget, IPlayerCarryPlacementTarget
    {
        [SerializeField] private bool canCarry = true;
        [SerializeField] private TMP_FontAsset indicatorFont;
        [SerializeField, Min(1f)] private float indicatorFontSize = 2.7f;
        [SerializeField] private Vector3 indicatorOffset;
        [SerializeField, Min(0f)] private float indicatorOutlineWidth = .1f;

        private CarryTargetMotion carryMotion;
        private IPlayerCarryHost carryPlayer;
        private IColorObject colorObject;
        private ICarryMotionParticipant motionParticipant;
        private TextMeshPro indicator;
        private Material indicatorMaterial;
        private TMP_FontAsset indicatorMaterialFont;
        private float appliedOutlineWidth = -1f;
        private Collider[] indicatorShapes;

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
            RefreshIndicatorPose();
            indicator.text = InputPromptUtility.UseActionLabels
                ? "搬运"
                : InputPromptUtility.FormatKey(
                    InputActionId.Interact) +
                  "\n搬运";
            indicator.font = indicatorFont != null
                ? indicatorFont
                : WordNoteUI.Instance != null ? WordNoteUI.Instance.DefaultFont : TMP_Settings.defaultFontAsset;
            indicator.fontSize = indicatorFontSize;
            indicator.color = Color.black;
            indicator.fontStyle = FontStyles.Bold;
            indicator.alignment = TextAlignmentOptions.Center;
            indicator.textWrappingMode = TextWrappingModes.NoWrap;
            indicator.overflowMode = TextOverflowModes.Overflow;
            indicator.sortingLayerID =
                SortingLayer.layers.Length > 0
                    ? SortingLayer.layers[
                        SortingLayer.layers.Length - 1].id
                    : 0;
            indicator.sortingOrder = short.MaxValue;
            ApplyIndicatorOutline();
        }

        private void LateUpdate()
        {
            // Only the queue head has a visible label. Re-evaluate bindings/device while it remains selected.
            if (indicator != null && indicator.gameObject.activeSelf) SetCarryIndicator(true);
        }

        private void RefreshIndicatorPose()
        {
            indicatorShapes ??= GetComponentsInChildren<Collider>(true);
            Bounds bounds = default;
            bool found = false;
            foreach (Collider shape in indicatorShapes)
            {
                if (shape == null || !shape.enabled || shape.isTrigger || !shape.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = shape.bounds; found = true; }
                else bounds.Encapsulate(shape.bounds);
            }
            indicator.transform.position = (found ? bounds.center : transform.position) + transform.TransformVector(indicatorOffset);
            indicator.transform.rotation = Quaternion.identity;
            Vector3 scale = transform.lossyScale;
            indicator.transform.localScale = new Vector3(
                1f / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(scale.y)),
                1f / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
        }

        private void Awake()
        {
            colorObject = GetComponent<IColorObject>();
            motionParticipant = GetComponent<ICarryMotionParticipant>();
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

            return carryPlayer == null || ReferenceEquals(carryPlayer, player);
        }

        public bool BeginCarry(IPlayerCarryHost player)
        {
            if (!CanCarry(player))
            {
                return false;
            }

            if (IsCarryActive) return false;
            carryMotion ??= new CarryTargetMotion(transform, OnCarryRestored);
            motionParticipant?.SetCarrySuspended(true);
            if (!carryMotion.BeginCarry(
                    player.CarryAnchorWorldPosition,
                    player.CarryCenterWorldPosition,
                    (player as Component)?.transform))
            {
                motionParticipant?.SetCarrySuspended(false);
                return false;
            }

            carryPlayer = player;
            AudioCuePlayer.Play(AudioCueIds.BlockPickup);
            return true;
        }

        public void UpdateCarryAnchor(Vector3 anchorWorldPosition)
        {
            carryMotion?.UpdateAnchor(anchorWorldPosition);
        }

        public Vector3 ConstrainCarryDisplacement(Vector3 anchor, Vector3 displacement, float skin) =>
            carryMotion != null ? carryMotion.ConstrainDisplacement(anchor, displacement, skin) : displacement;

        public bool BeginCarryDrop(
            IPlayerCarryHost player,
            Vector3 worldPosition)
        {
            if (!CanPlaceCarryAt(player, worldPosition)) return false;

            bool started = carryMotion != null &&
                           carryMotion.BeginDrop(worldPosition);
            if (started) AudioCuePlayer.Play(AudioCueIds.BlockDrop);
            return started;
        }

        public Bounds GetCarryPlacementBounds(Vector3 worldPosition) => carryMotion != null
            ? carryMotion.GetPlacementBounds(worldPosition) : new Bounds(worldPosition, Vector3.one);

        public bool CanPlaceCarryAt(IPlayerCarryHost player, Vector3 worldPosition)
        {
            if (player == null || carryPlayer != player || carryMotion == null || !IsCarryActive) return false;
            var member = GetComponent<Project.Rooms.RoomResetObject>();
            if (member != null && member.Manager != null && !member.Manager.CanPlaceInAuthoredRegion(member, worldPosition)) return false;
            return (motionParticipant == null || motionParticipant.CanPlaceCarriedAt(worldPosition)) && carryMotion.CanPlaceAt(worldPosition);
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

        private void OnCarryRestored()
        {
            motionParticipant?.SetCarrySuspended(false);
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

            if (indicatorMaterial != null && indicatorMaterialFont != font)
            {
                Destroy(indicatorMaterial);
                indicatorMaterial = null;
            }
            if (indicatorMaterial == null)
            {
                indicatorMaterial = new Material(font.material);
                indicator.fontMaterial = indicatorMaterial;
                indicatorMaterialFont = font;
                appliedOutlineWidth = -1f;
            }

            if (Mathf.Approximately(appliedOutlineWidth, indicatorOutlineWidth)) return;
            appliedOutlineWidth = indicatorOutlineWidth;

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
            indicator.UpdateMeshPadding();
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
