using DG.Tweening;
using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 提取自拾取飞行动画的头顶搬运表现。
    /// </summary>
    internal sealed class CarryTargetMotion
    {
        private readonly Transform root;

        private Collider[] colliders;
        private bool[] colliderStates;
        private Renderer[] renderers;
        private int[] sortingLayerIds;
        private int[] sortingOrders;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private int originalSiblingIndex;
        private Vector3 currentAnchor;
        private Vector3 playerCenter;
        private Tween tween;
        private bool movingToCarrier;

        public bool IsCarrying { get; private set; }
        public bool IsDropAnimating { get; private set; }
        public bool IsResetAnimating { get; private set; }

        public CarryTargetMotion(Transform root)
        {
            this.root = root;
        }

        public bool BeginCarry(
            Vector3 anchorWorldPosition,
            Vector3 playerWorldCenter)
        {
            if (root == null || IsCarrying || IsResetAnimating)
            {
                return false;
            }

            CacheTargets();
            originalPosition = root.position;
            originalScale = root.localScale;
            originalSiblingIndex = root.GetSiblingIndex();
            currentAnchor = new Vector3(
                anchorWorldPosition.x,
                anchorWorldPosition.y,
                originalPosition.z);
            playerCenter = playerWorldCenter;
            IsCarrying = true;
            IsDropAnimating = false;
            IsResetAnimating = false;
            movingToCarrier = true;
            SetCollidersEnabled(false);
            SetRenderersTopmost(true);
            root.SetAsLastSibling();

            Vector3 start = root.position;
            Vector3 away = start - playerCenter;
            away.z = 0f;
            if (away.sqrMagnitude < .0001f)
            {
                away = Vector3.right;
            }
            else
            {
                away.Normalize();
            }

            Vector3 recoilPoint =
                start + away * .16f + Vector3.up * .06f;
            tween = DOTween.Sequence()
                .Append(DOTween.To(
                        () => 0f,
                        progress => root.position = Vector3.Lerp(
                            start,
                            recoilPoint,
                            progress),
                        1f,
                        .12f)
                    .SetEase(Ease.Linear))
                .Append(DOTween.To(
                        () => 0f,
                        progress => root.position = Cubic(
                            recoilPoint,
                            recoilPoint +
                            (currentAnchor - recoilPoint) * .28f +
                            Vector3.up * .18f,
                            recoilPoint +
                            (currentAnchor - recoilPoint) * .72f,
                            currentAnchor,
                            progress),
                        1f,
                        .28f)
                    .SetEase(Ease.OutSine))
                .OnComplete(() =>
                {
                    tween = null;
                    movingToCarrier = false;
                    if (IsCarrying && !IsDropAnimating)
                    {
                        root.position = currentAnchor;
                    }
                });
            tween.Play();
            return true;
        }

        public void UpdateAnchor(Vector3 anchorWorldPosition)
        {
            currentAnchor = new Vector3(
                anchorWorldPosition.x,
                anchorWorldPosition.y,
                originalPosition.z);
            if (!IsCarrying || IsDropAnimating ||
                movingToCarrier || root == null)
            {
                return;
            }

            root.position = currentAnchor;
        }

        public bool BeginDrop(Vector3 worldPosition)
        {
            if (root == null ||
                !IsCarrying ||
                IsDropAnimating ||
                IsResetAnimating)
            {
                return false;
            }

            tween?.Kill(false);
            tween = null;
            movingToCarrier = false;
            IsDropAnimating = true;
            Vector3 start = root.position;
            tween = DOTween.To(
                    () => 0f,
                    progress => root.position = Vector3.Lerp(
                        start,
                        worldPosition,
                        progress),
                    1f,
                    .22f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    tween = null;
                    IsDropAnimating = false;
                    IsCarrying = false;
                    Restore();
                });
            return true;
        }

        public bool BeginReset()
        {
            if (root == null ||
                IsResetAnimating ||
                (!IsCarrying && !IsDropAnimating))
            {
                return false;
            }

            tween?.Kill(false);
            tween = null;
            movingToCarrier = false;
            IsCarrying = false;
            IsDropAnimating = false;
            IsResetAnimating = true;
            Vector3 start = root.position;
            tween = DOTween.To(
                    () => 0f,
                    progress => root.position = Vector3.Lerp(
                        start,
                        originalPosition,
                        progress),
                    1f,
                    .3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    tween = null;
                    IsResetAnimating = false;
                    Restore();
                });
            return true;
        }

        public void Cancel()
        {
            if (root == null ||
                (!IsCarrying &&
                 !IsDropAnimating &&
                 !IsResetAnimating))
            {
                return;
            }

            tween?.Kill(false);
            tween = null;
            movingToCarrier = false;
            IsDropAnimating = false;
            IsResetAnimating = false;
            IsCarrying = false;
            root.position = originalPosition;
            root.localScale = originalScale;
            Restore();
        }

        private void CacheTargets()
        {
            if (colliders == null)
            {
                colliders = root.GetComponentsInChildren<Collider>(true);
                colliderStates = new bool[colliders.Length];
                for (int index = 0;
                     index < colliders.Length;
                     index++)
                {
                    colliderStates[index] =
                        colliders[index] != null &&
                        colliders[index].enabled;
                }
            }

            if (renderers == null)
            {
                renderers = root.GetComponentsInChildren<Renderer>(true);
                sortingLayerIds = new int[renderers.Length];
                sortingOrders = new int[renderers.Length];
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider = colliders[index];
                if (collider != null)
                {
                    collider.enabled =
                        enabled && colliderStates[index];
                }
            }
        }

        private void SetRenderersTopmost(bool topmost)
        {
            int topLayerId = SortingLayer.layers.Length > 0
                ? SortingLayer.layers[
                    SortingLayer.layers.Length - 1].id
                : 0;
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (topmost)
                {
                    sortingLayerIds[index] =
                        renderer.sortingLayerID;
                    sortingOrders[index] =
                        renderer.sortingOrder;
                    renderer.sortingLayerID = topLayerId;
                    renderer.sortingOrder = short.MaxValue;
                }
                else
                {
                    renderer.sortingLayerID =
                        sortingLayerIds[index];
                    renderer.sortingOrder =
                        sortingOrders[index];
                }
            }
        }

        private void Restore()
        {
            SetCollidersEnabled(true);
            SetRenderersTopmost(false);
            if (root != null)
            {
                root.SetSiblingIndex(originalSiblingIndex);
            }
        }

        private static Vector3 Cubic(
            Vector3 start,
            Vector3 controlA,
            Vector3 controlB,
            Vector3 end,
            float progress)
        {
            float remaining = 1f - progress;
            return remaining * remaining * remaining * start +
                   3f * remaining * remaining * progress * controlA +
                   3f * remaining * progress * progress * controlB +
                   progress * progress * progress * end;
        }
    }
}
