using System;
using System.Collections.Generic;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.BlockFeatures
{
    public enum ContactTimelineFilter
    {
        [InspectorName("任意碰撞体")] AnyCollider = 0,
        [InspectorName("仅玩家")] Player = 1,
        [InspectorName("指定标签")] Tag = 2
    }

    public enum ContactTimelineTriggerMode
    {
        [InspectorName("只触发一次")] Once = 0,
        [InspectorName("可重复触发")] Repeatable = 1
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(PlayableDirector))]
    [AddComponentMenu("2026TapTap/通用/接触触发 Timeline")]
    public sealed class ContactTimelineTrigger : MonoBehaviour
    {
        [SerializeField] private TimelineAsset timeline;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private ContactTimelineFilter contactFilter =
            ContactTimelineFilter.Player;
        [SerializeField] private string requiredTag = "Player";
        [SerializeField] private ContactTimelineTriggerMode triggerMode =
            ContactTimelineTriggerMode.Once;
        [SerializeField, Min(0f)] private float repeatCooldownSeconds = .5f;
        [SerializeField] private bool restartIfPlaying = true;
        [SerializeField] private bool requestPlayerControlLockDuringTimeline;
        [SerializeField] private bool hideRenderersOnEnable = true;
        [SerializeField] private bool destroyAfterTrigger;

        private readonly HashSet<int> contacts = new HashSet<int>();
        private Renderer[] renderers = System.Array.Empty<Renderer>();
        private bool[] originalRendererStates = System.Array.Empty<bool>();
        private PlayableDirector director;
        private IDisposable controlLockHandle;
        private bool played;
        private float nextAvailableTime;

        public bool HasTriggered => played;
        public int ContactCount => contacts.Count;
        public PlayableDirector Director => director;
        public Collider ManagedCollider => ResolveCollider();
        public TimelineAsset Timeline => timeline;
        public ContactTimelineTriggerMode TriggerMode => triggerMode;
        public float RepeatCooldownSeconds => repeatCooldownSeconds;
        public bool RequestsPlayerControlLock =>
            requestPlayerControlLockDuringTimeline;
        public bool ControlLockActive => controlLockHandle != null;
        public float CooldownRemaining => triggerMode ==
                                          ContactTimelineTriggerMode.Repeatable
            ? Mathf.Max(0f, nextAvailableTime - Time.time)
            : 0f;

        private void Reset()
        {
            targetCollider = GetComponent<Collider>();
            if (targetCollider != null)
            {
                targetCollider.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            contacts.Clear();
            played = false;
            nextAvailableTime = 0f;
            director = GetComponent<PlayableDirector>();
            if (timeline != null && director != null)
            {
                director.playableAsset = timeline;
            }
            if (director != null)
            {
                director.stopped += OnDirectorStopped;
            }

            CacheRenderers();
            if (hideRenderersOnEnable)
            {
                SetRenderersVisible(false);
            }
        }

        private void OnDisable()
        {
            contacts.Clear();
            ReleasePlayerControlLock();
            if (director != null)
            {
                director.stopped -= OnDirectorStopped;
                director.Stop();
            }

            // Disabling just this component must not reveal the invisible trigger in Game.
            if (!Application.isPlaying || !hideRenderersOnEnable) RestoreRenderers();
        }

        private void Update()
        {
            if (controlLockHandle == null || director == null)
            {
                return;
            }

            double duration = director.duration;
            if (duration > 0d &&
                director.time >= duration - .0001d)
            {
                ReleasePlayerControlLock();
            }
        }

        public void OnTriggerEnter(Collider other)
        {
            TryContact(other != null ? other.gameObject : null);
        }

        public void OnTriggerExit(Collider other)
        {
            ReleaseContact(other != null ? other.gameObject : null);
        }

        public void OnCollisionEnter(Collision collision)
        {
            TryContact(collision != null
                ? collision.gameObject
                : null);
        }

        public void OnCollisionExit(Collision collision)
        {
            ReleaseContact(collision != null
                ? collision.gameObject
                : null);
        }

        [ContextMenu("重置触发状态")]
        public void ResetTrigger()
        {
            contacts.Clear();
            played = false;
            nextAvailableTime = 0f;
            if (director != null)
            {
                director.Stop();
            }
            ReleasePlayerControlLock();

            if (hideRenderersOnEnable)
            {
                SetRenderersVisible(false);
            }
        }

        [ContextMenu("调试触发 Timeline")]
        public void DebugTrigger()
        {
            played = true;
            PlayTimeline(null);
        }

        private void TryContact(GameObject actor)
        {
            if (GameplayPresentationGate.IsSuppressed) return;
            if (!isActiveAndEnabled || actor == null)
            {
                return;
            }

            if (triggerMode == ContactTimelineTriggerMode.Once &&
                played)
            {
                return;
            }

            if (triggerMode == ContactTimelineTriggerMode.Repeatable &&
                Time.time < nextAvailableTime)
            {
                return;
            }

            if (!MatchesFilter(actor))
            {
                return;
            }

            int id = actor.transform.root.GetInstanceID();
            if (!contacts.Add(id))
            {
                return;
            }

            played = true;
            nextAvailableTime =
                Time.time + Mathf.Max(0f, repeatCooldownSeconds);
            PlayTimeline(actor);
            if (destroyAfterTrigger)
            {
                Destroy(gameObject);
            }
        }

        private void ReleaseContact(GameObject actor)
        {
            if (actor == null)
            {
                return;
            }

            contacts.Remove(actor.transform.root.GetInstanceID());
        }

        private bool MatchesFilter(GameObject actor)
        {
            switch (contactFilter)
            {
                case ContactTimelineFilter.Player:
                    return actor.GetComponentInParent<PlayerController>() !=
                           null;
                case ContactTimelineFilter.Tag:
                    return HasTagInParents(actor, requiredTag);
                default:
                    return true;
            }
        }

        private static bool HasTagInParents(
            GameObject actor,
            string tag)
        {
            if (actor == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (Transform current = actor.transform;
                 current != null;
                 current = current.parent)
            {
                if (current.CompareTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayTimeline(GameObject actor)
        {
            if (director == null)
            {
                director = GetComponent<PlayableDirector>();
            }

            if (director == null || director.playableAsset == null)
            {
                Debug.LogWarning(
                    "[ContactTimelineTrigger] 没有可播放的 Timeline。",
                    this);
                return;
            }

            if (restartIfPlaying)
            {
                director.Stop();
                director.time = 0d;
            }

            if (requestPlayerControlLockDuringTimeline)
            {
                RequestPlayerControlLock(actor);
            }

            director.Play();
        }

        private void RequestPlayerControlLock(GameObject actor)
        {
            ReleasePlayerControlLock();

            IPlayerControlLockTarget target = null;
            if (actor != null)
            {
                IReadOnlyList<IPlayerControlLockTarget> targets =
                    ProjectDiscovery.GetComponents<
                        IPlayerControlLockTarget>(
                        actor,
                        true,
                        true);
                if (targets.Count > 0)
                {
                    target = targets[0];
                }
            }

            if (target == null)
            {
                target = ProjectDiscovery.FindFirstInterface<
                    IPlayerControlLockTarget>();
            }

            if (target != null)
            {
                controlLockHandle = target.AcquireControlLock(this);
            }
        }

        private void ReleasePlayerControlLock()
        {
            controlLockHandle?.Dispose();
            controlLockHandle = null;
        }

        private void OnDirectorStopped(PlayableDirector stoppedDirector)
        {
            if (stoppedDirector == director)
            {
                ReleasePlayerControlLock();
            }
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            originalRendererStates = new bool[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                originalRendererStates[index] =
                    renderers[index] != null &&
                    renderers[index].enabled;
            }
        }

        private Collider ResolveCollider()
        {
            if (targetCollider == null)
            {
                targetCollider = GetComponent<Collider>();
            }

            return targetCollider;
        }

        private void SetRenderersVisible(bool visible)
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = visible;
                }
            }
        }

        private void RestoreRenderers()
        {
            int count = Mathf.Min(
                renderers.Length,
                originalRendererStates.Length);
            for (int index = 0; index < count; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled =
                        originalRendererStates[index];
                }
            }
        }
    }
}
