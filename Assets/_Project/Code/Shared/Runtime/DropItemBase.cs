using System;
using DG.Tweening;
using UnityEngine;

namespace Project.Items
{
    public interface IDropItemCollectionAnimation
    {
        bool Play(Transform collector, Action onComplete);
    }

    /// <summary>
    /// Shared lifecycle for collectible world items.
    /// Appearing and collection are explicit phases so pooled items can be
    /// reset safely before the next activation.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class DropItemBase : MonoBehaviour
    {
        private bool collectionRequested;
        private bool collectionFinished;
        private bool attractionRequested;
        private bool attractionFinished;
        private Tween appearTween;
        private Tween attractTween;
        private Vector3 initialScale;

        public bool CollectionRequested => collectionRequested;
        public bool CollectionFinished => collectionFinished;
        public bool AttractionRequested => attractionRequested;
        public bool AttractionFinished => attractionFinished;
        public bool IsAttracting =>
            attractionRequested && !attractionFinished;
        protected GameObject CollectionActor { get; private set; }
        protected Transform CollectionTarget =>
            CollectionActor != null
                ? CollectionActor.transform
                : null;

        public bool CanCollect =>
            isActiveAndEnabled &&
            !collectionRequested &&
            !collectionFinished &&
            !IsAttracting;

        public bool CanAttract =>
            isActiveAndEnabled &&
            !attractionRequested &&
            !attractionFinished &&
            !collectionRequested &&
            !collectionFinished;

        private void Awake()
        {
            initialScale = transform.localScale;
        }

        protected virtual void OnEnable()
        {
            collectionRequested = false;
            collectionFinished = false;
            attractionRequested = false;
            attractionFinished = false;
            CollectionActor = null;
            ResetItemState();
            PlayAppearAnimation();
        }

        protected virtual void OnDisable()
        {
            StopItemAnimations();
        }

        protected bool TryBeginAttraction(GameObject actor = null)
        {
            if (!CanAttract)
            {
                return false;
            }

            attractionRequested = true;
            attractionFinished = false;
            CollectionActor = actor;
            OnAttractionStarted();
            PlayAttractAnimation(CompleteAttraction);
            return true;
        }

        protected bool TryBeginCollection(GameObject actor = null)
        {
            if (!CanCollect)
            {
                return false;
            }

            collectionRequested = true;
            attractTween?.Kill(false);
            attractTween = null;
            CollectionActor = actor != null
                ? actor
                : CollectionActor;
            OnCollectionStarted();
            PlayCollectAnimation(FinishCollection);
            return true;
        }

        /// <summary>
        /// Restore all state that an animation or pooled reuse may change.
        /// </summary>
        protected virtual void ResetItemState()
        {
            transform.localScale = initialScale;
        }

        /// <summary>
        /// DOTween appearance animation hook. Called once on every enable.
        /// </summary>
        protected virtual void PlayAppearAnimation()
        {
            appearTween?.Kill(false);
            appearTween = transform.DOPunchScale(initialScale * .12f, .28f, 4, .5f);
        }

        /// <summary>
        /// DOTween attraction hook. Move and shrink toward CollectionTarget
        /// while this item approaches the collecting actor.
        /// </summary>
        protected virtual void PlayAttractAnimation(
            Action onComplete)
        {
            appearTween?.Kill(false);
            appearTween = null;
            Transform target = CollectionTarget;
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Vector3 start = transform.position;
            Vector3 startScale = transform.localScale;
            attractTween = DOTween.To(() => 0f, progress =>
            {
                if (target == null) return;
                transform.position = Vector3.Lerp(start, target.position, progress);
                transform.localScale = Vector3.Lerp(startScale, initialScale * .72f, progress);
            }, 1f, .36f).SetEase(Ease.InQuad).OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>
        /// DOTween collection animation hook. Implementations must invoke
        /// <paramref name="onComplete"/> exactly once when the animation ends.
        /// This runs after attraction has physically reached the actor.
        /// </summary>
        protected virtual void PlayCollectAnimation(
            Action onComplete)
        {
            var animation = GetComponent<IDropItemCollectionAnimation>();
            if (animation != null && CollectionTarget != null &&
                animation.Play(CollectionTarget, onComplete))
                return;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Kill or detach active tweens before disable/pool reuse.
        /// </summary>
        protected virtual void StopItemAnimations()
        {
            appearTween?.Kill(false);
            attractTween?.Kill(false);
            appearTween = null;
            attractTween = null;
        }

        protected virtual void OnCollectionStarted()
        {
        }

        protected virtual void OnAttractionStarted()
        {
        }

        protected virtual void OnAttractionFinished()
        {
        }

        protected virtual void OnCollectionFinished()
        {
        }

        protected virtual void RecycleItem()
        {
            Destroy(gameObject);
        }

        private void FinishCollection()
        {
            if (!collectionRequested || collectionFinished)
            {
                return;
            }

            collectionFinished = true;
            OnCollectionFinished();
            RecycleItem();
        }

        protected void CompleteAttraction()
        {
            if (!attractionRequested || attractionFinished)
            {
                return;
            }

            attractionFinished = true;
            attractTween?.Kill(false);
            attractTween = null;
            OnAttractionFinished();
            TryBeginCollection(CollectionActor);
        }
    }
}
