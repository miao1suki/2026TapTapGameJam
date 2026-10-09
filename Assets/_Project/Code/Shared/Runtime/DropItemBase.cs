using System;
using UnityEngine;

namespace Project.Items
{
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
        }

        /// <summary>
        /// DOTween appearance animation hook. Called once on every enable.
        /// </summary>
        protected virtual void PlayAppearAnimation()
        {
        }

        /// <summary>
        /// DOTween attraction hook. Move and shrink toward CollectionTarget
        /// while this item approaches the collecting actor.
        /// </summary>
        protected virtual void PlayAttractAnimation(
            Action onComplete)
        {
        }

        /// <summary>
        /// DOTween collection animation hook. Implementations must invoke
        /// <paramref name="onComplete"/> exactly once when the animation ends.
        /// This runs after attraction has physically reached the actor.
        /// </summary>
        protected virtual void PlayCollectAnimation(
            Action onComplete)
        {
            onComplete?.Invoke();
        }

        /// <summary>
        /// Kill or detach active tweens before disable/pool reuse.
        /// </summary>
        protected virtual void StopItemAnimations()
        {
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
            OnAttractionFinished();
            TryBeginCollection(CollectionActor);
        }
    }
}
