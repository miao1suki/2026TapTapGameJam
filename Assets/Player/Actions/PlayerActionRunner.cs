using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class PlayerActionRunner : MonoBehaviour
    {
        [SerializeField]
        private PlayableDirector director;

        private bool suppressCompletion;

        public event Action<ActSO> Completed;

        public ActSO CurrentAction { get; private set; }

        public bool IsPlaying =>
            director != null &&
            director.state == PlayState.Playing;

        private void Reset()
        {
            director = GetComponent<PlayableDirector>();
        }

        private void Awake()
        {
            if (director == null)
            {
                director = GetComponent<PlayableDirector>();
            }
        }

        private void OnEnable()
        {
            if (director == null)
            {
                director = GetComponent<PlayableDirector>();
            }

            if (director != null)
            {
                director.stopped -= OnDirectorStopped;
                director.stopped += OnDirectorStopped;
            }
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.stopped -= OnDirectorStopped;
            }
        }

        public bool Play(ActSO action)
        {
            if (action == null || action.Timeline == null)
            {
                return false;
            }

            Stop();
            CurrentAction = action;
            director.playableAsset = action.Timeline;
            director.time = 0d;
            director.Play();
            if (director.playableGraph.IsValid())
            {
                director.playableGraph
                    .GetRootPlayable(0)
                    .SetSpeed(action.PlaybackSpeed);
            }

            return true;
        }

        public void Stop()
        {
            if (director == null || !IsPlaying)
            {
                CurrentAction = null;
                return;
            }

            suppressCompletion = true;
            director.Stop();
            suppressCompletion = false;
            CurrentAction = null;
        }

        private void OnDirectorStopped(PlayableDirector stoppedDirector)
        {
            if (suppressCompletion || CurrentAction == null)
            {
                return;
            }

            ActSO completed = CurrentAction;
            CurrentAction = null;
            Completed?.Invoke(completed);
        }
    }
}
