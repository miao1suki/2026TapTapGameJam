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
        private readonly System.Action restored;

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
        private bool waitingForSeparation;
        private Vector3 dropDestination;
        private float dropDeadline;
        private Transform carrier;
        private IPlayerCarrySeparationHost separationHost;
        private readonly RaycastHit[] carryHits = new RaycastHit[128];

        public bool IsCarrying { get; private set; }
        public bool IsDropAnimating { get; private set; }
        public bool IsResetAnimating { get; private set; }
        private readonly Collider[] placementHits = new Collider[128];

        public Bounds GetPlacementBounds(Vector3 position)
        {
            Bounds result = new Bounds(position, Vector3.zero);
            bool found = false;
            if (colliders == null) return result;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider shape = colliders[i];
                if (shape == null || !colliderStates[i] || shape.isTrigger) continue;
                Bounds local;
                if (shape is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (shape is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
                else if (shape is SphereCollider sphere) local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                else if (shape is CapsuleCollider capsule)
                {
                    Vector3 size = Vector3.one * capsule.radius * 2f;
                    size[capsule.direction] = Mathf.Max(capsule.height, size[capsule.direction]);
                    local = new Bounds(capsule.center, size);
                }
                else continue;
                Vector3 offset = position - root.position;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = shape.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))) + offset;
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        public bool CanPlaceAt(Vector3 position)
        {
            Bounds bounds = GetPlacementBounds(position);
            if (bounds.size.sqrMagnitude < .00001f) return false;
            int count = Physics.OverlapBoxNonAlloc(bounds.center,
                Vector3.Max(bounds.extents - Vector3.one * .01f, Vector3.one * .0001f),
                placementHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == placementHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider other = placementHits[i];
                if (other == null || other.transform.IsChildOf(root) ||
                    Project.Mechanisms.ButtonPressureGeometry.IsPressureVolume(other)) continue;
                if (carrier != null && other.transform.IsChildOf(carrier) && separationHost != null) continue;
                return false;
            }
            return separationHost == null || separationHost.ResolveCarryOverlap(bounds, root, false);
        }

        public CarryTargetMotion(Transform root, System.Action restored = null)
        {
            this.root = root;
            this.restored = restored;
        }

        public bool BeginCarry(
            Vector3 anchorWorldPosition,
            Vector3 playerWorldCenter,
            Transform carrierTransform = null)
        {
            if (root == null || IsCarrying || IsResetAnimating)
            {
                return false;
            }

            CacheTargets();
            carrier = carrierTransform;
            separationHost = carrier != null ? carrier.GetComponent<IPlayerCarrySeparationHost>() : null;
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

        public Vector3 ConstrainDisplacement(Vector3 anchor, Vector3 displacement, float skin)
        {
            if (!IsCarrying || IsDropAnimating || IsResetAnimating || colliders == null)
                return displacement;

            // Query the intended head pose, not the interpolated render pose or pickup Tween.
            anchor.z = originalPosition.z;
            Vector3 offset = anchor - root.position;
            Vector3 result = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                float distance = Mathf.Abs(displacement[axis]);
                if (distance < .000001f) continue;
                Vector3 direction = Vector3.zero;
                direction[axis] = Mathf.Sign(displacement[axis]);
                float allowed = distance;
                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider shape = colliders[index];
                    if (!colliderStates[index] || shape == null || shape.isTrigger ||
                        !shape.gameObject.activeInHierarchy) continue;
                    Vector3 center, extents;
                    Quaternion rotation;
                    if (shape is BoxCollider box)
                    {
                        center = box.transform.TransformPoint(box.center);
                        Vector3 scale = box.transform.lossyScale;
                        extents = Vector3.Scale(box.size * .5f,
                            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                        rotation = box.transform.rotation;
                    }
                    else
                    {
                        // Non-box authored shapes use a conservative local mesh/primitive envelope.
                        Bounds local;
                        if (shape is SphereCollider sphere)
                            local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                        else if (shape is CapsuleCollider capsule)
                        {
                            Vector3 size = Vector3.one * capsule.radius * 2f;
                            size[capsule.direction] = Mathf.Max(capsule.height, size[capsule.direction]);
                            local = new Bounds(capsule.center, size);
                        }
                        else if (shape is MeshCollider mesh && mesh.sharedMesh != null)
                            local = mesh.sharedMesh.bounds;
                        else continue;
                        center = shape.transform.TransformPoint(local.center);
                        Vector3 scale = shape.transform.lossyScale;
                        extents = Vector3.Scale(local.extents,
                            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                        rotation = shape.transform.rotation;
                    }
                    // Slightly inset tangential faces so a resting floor doesn't block sideways motion.
                    extents = Vector3.Max(extents - Vector3.one * skin * .25f, Vector3.one * .0001f);
                    int count = Physics.BoxCastNonAlloc(center + offset + result, extents, direction,
                        carryHits, rotation, distance + skin, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    if (count == carryHits.Length) allowed = 0f; // Never tunnel when a dense query overflows.
                    for (int hitIndex = 0; hitIndex < count; hitIndex++)
                    {
                        RaycastHit hit = carryHits[hitIndex];
                        Collider other = hit.collider;
                        if (other == null || other.transform.IsChildOf(root) ||
                            (carrier != null && other.transform.IsChildOf(carrier)) ||
                            Physics.GetIgnoreLayerCollision(shape.gameObject.layer, other.gameObject.layer) ||
                            Vector3.Dot(hit.normal, direction) >= -.001f) continue;
                        allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - skin));
                    }
                }
                result[axis] = direction[axis] * allowed;
            }
            return result;
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

            if (separationHost != null &&
                !separationHost.ResolveCarryOverlap(GetPlacementBounds(worldPosition), root, true)) return false;

            tween?.Kill(false);
            tween = null;
            movingToCarrier = false;
            IsDropAnimating = true;
            waitingForSeparation = false;
            dropDestination = worldPosition;
            dropDeadline = Time.time + .8f;
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
                    waitingForSeparation = true;
                    TickDrop();
                });
            return true;
        }

        public void TickDrop()
        {
            if (!waitingForSeparation || !IsDropAnimating) return;
            if (!CanPlaceAt(dropDestination) || Time.time > dropDeadline)
            {
                AbortDrop();
                return;
            }
            if (separationHost != null && separationHost.IsCarrySeparationPending(root)) return;
            if (separationHost != null && !separationHost.IsCarrySeparated(GetPlacementBounds(dropDestination)))
            {
                AbortDrop();
                return;
            }
            waitingForSeparation = false;
            IsDropAnimating = false;
            IsCarrying = false;
            Restore();
        }

        private void AbortDrop()
        {
            separationHost?.CancelCarrySeparation(root);
            waitingForSeparation = false;
            IsDropAnimating = false;
            // The player's anchor may have moved while the object was in flight.
            if (separationHost is IPlayerCarryHost host) currentAnchor = host.CarryAnchorWorldPosition;
            root.position = currentAnchor;
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
            separationHost?.CancelCarrySeparation(root);
            waitingForSeparation = false;
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
            separationHost?.CancelCarrySeparation(root);
            waitingForSeparation = false;
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
            // A reset or growth can replace/resize authored shapes between two pickups.
            colliders = root.GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                colliderStates[index] = colliders[index] != null && colliders[index].enabled;
            }
            renderers = root.GetComponentsInChildren<Renderer>(true);
            sortingLayerIds = new int[renderers.Length];
            sortingOrders = new int[renderers.Length];
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
            restored?.Invoke();
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
