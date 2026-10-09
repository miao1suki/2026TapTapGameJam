using System;
using DG.Tweening;
using UnityEngine;

namespace Project.Pickups
{
    /// <summary>
    /// 仅负责拾取物的飞行表现；何时拾取以及奖励结算由调用者决定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PickupFlightAnimator : MonoBehaviour,
        Project.Items.IDropItemCollectionAnimation
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Collider pickupCollider;
        [SerializeField, Min(.01f)] private float recoilDuration = .12f;
        [SerializeField, Min(0f)] private float recoilDistance = .7f;
        [SerializeField, Range(0f, .9f)] private float recoilDistanceVariation = .35f;
        [SerializeField, Min(0f)] private float recoilHeightVariation = .25f;
        [SerializeField, Min(0f)] private float heightAboveHead = .75f;
        [SerializeField, Min(.01f)] private float riseDuration = .28f;
        [SerializeField, Min(.01f)] private float slowDuration = .14f;
        [SerializeField, Min(.01f)] private float finishDuration = .22f;
        [SerializeField, Range(.05f, .8f)] private float slowApproach = .12f;
        [SerializeField, Min(0f)] private float horizontalVariation = .55f;
        [SerializeField, Min(0f)] private float verticalVariation = .35f;
        [SerializeField, Min(0f)] private float depthVariation = .12f;

        private Sequence flight;
        private Transform collector;
        private Collider collectorCollider;
        private Vector3 segmentStart;
        private Vector3 segmentControlA;
        private Vector3 segmentControlB;
        private Vector3 recoilPoint;
        private Vector3 recoilDirection;
        private Vector3 lastCenter;
        private Action completion;

        public bool IsPlaying { get; private set; }

        private void Reset()
        {
            pickupCollider = GetComponent<Collider>();
        }

        private void Awake()
        {
            if (visual == null)
                visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
            if (pickupCollider == null)
                pickupCollider = GetComponent<Collider>();
        }

        /// <summary>
        /// 播放一次拾取飞行。返回 false 表示目标或表现无效，或动画已在播放。
        /// 完成后只隐藏 visual，并调用回调；不发奖励、不销毁拾取对象。
        /// </summary>
        public bool Play(Transform collectorTransform, Action onComplete = null)
        {
            if (IsPlaying || collectorTransform == null || visual == null ||
                !visual.gameObject.activeInHierarchy)
                return false;

            collector = collectorTransform;
            collectorCollider = collector.GetComponent<Collider>();
            lastCenter = GetCenter();
            recoilPoint = ChooseRecoilPoint(visual.position, lastCenter);
            completion = onComplete;
            IsPlaying = true;
            if (pickupCollider != null) pickupCollider.enabled = false;

            flight = DOTween.Sequence().Pause();
            AppendSegment(() => recoilPoint, recoilDuration,
                Ease.Linear, .7f, recoilDistance * .2f);
            AppendSegment(GetHeadPoint, riseDuration,
                Ease.OutSine, 1.2f, recoilDistance * .9f, true);
            AppendSegment(GetSlowPoint, slowDuration,
                Ease.Linear, .15f, 0f);
            AppendSegment(GetCenter, finishDuration,
                Ease.OutQuad, .4f, 0f);
            flight.OnComplete(Finish);
            flight.Play();
            return true;
        }

        private void AppendSegment(Func<Vector3> target, float duration,
            Ease ease, float variationScale, float outwardCarry,
            bool continueOutward = false)
        {
            flight.AppendCallback(() =>
            {
                segmentStart = visual.position;
                Vector3 chord = target() - segmentStart;
                Vector2 planar = UnityEngine.Random.insideUnitCircle;
                Vector3 deviation = new Vector3(
                    planar.x * horizontalVariation,
                    planar.y * verticalVariation,
                    UnityEngine.Random.Range(-depthVariation, depthVariation)) *
                    variationScale;
                segmentControlA = continueOutward
                    ? segmentStart + recoilDirection * outwardCarry +
                      deviation * .35f
                    : segmentStart + chord * .25f + deviation * .8f +
                      recoilDirection * outwardCarry;
                segmentControlB = segmentStart + chord * .72f + deviation;
            });
            flight.Append(DOTween.To(() => 0f, progress =>
            {
                if (visual == null) return;
                visual.position = Cubic(segmentStart, segmentControlA,
                    segmentControlB, target(), progress);
            }, 1f, duration).SetEase(ease));
        }

        private Vector3 GetCenter()
        {
            if (collector != null)
                lastCenter = collectorCollider != null
                    ? collectorCollider.bounds.center : collector.position;
            return lastCenter;
        }

        private Vector3 ChooseRecoilPoint(Vector3 origin, Vector3 playerCenter)
        {
            Vector3 away = origin - playerCenter;
            away.y = 0f;
            if (away.sqrMagnitude < .0001f)
                away = UnityEngine.Random.value < .5f ? Vector3.left : Vector3.right;
            away.Normalize();
            recoilDirection = away;

            float distance = recoilDistance * UnityEngine.Random.Range(
                1f - recoilDistanceVariation, 1f + recoilDistanceVariation);
            float height = UnityEngine.Random.Range(
                -recoilHeightVariation, recoilHeightVariation);
            Vector3 side = Vector3.Cross(away, Vector3.up) *
                UnityEngine.Random.Range(-depthVariation, depthVariation);
            return origin + away * distance + Vector3.up * height + side;
        }

        private Vector3 GetHeadPoint()
        {
            Vector3 center = GetCenter();
            float top = collectorCollider != null
                ? collectorCollider.bounds.max.y : center.y + 1f;
            return new Vector3(center.x, top + heightAboveHead, center.z);
        }

        private Vector3 GetSlowPoint()
        {
            return Vector3.Lerp(GetHeadPoint(), GetCenter(), slowApproach);
        }

        private static Vector3 Cubic(Vector3 a, Vector3 controlA,
            Vector3 controlB, Vector3 b, float progress)
        {
            float remaining = 1f - progress;
            return remaining * remaining * remaining * a +
                3f * remaining * remaining * progress * controlA +
                3f * remaining * progress * progress * controlB +
                progress * progress * progress * b;
        }

        private void Finish()
        {
            IsPlaying = false;
            flight = null;
            if (visual != null)
            {
                visual.position = GetCenter();
                if (visual == transform)
                {
                    foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;
                }
                else
                    visual.gameObject.SetActive(false);
            }
            Action callback = completion;
            completion = null;
            callback?.Invoke();
        }

        private void OnDisable()
        {
            flight?.Kill(false);
            flight = null;
            completion = null;
            IsPlaying = false;
        }
    }
}
