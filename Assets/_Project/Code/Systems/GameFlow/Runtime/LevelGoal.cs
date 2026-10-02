using System.Collections;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LevelGoal :
        MonoBehaviour,
        IAchievementSignalProvider
    {
        private static readonly string[] AchievementSignals =
        {
            AchievementSignalIds.LevelGoalReached
        };

        [SerializeField] private GameFlowSceneId nextScene = GameFlowSceneId.Level02;
        [SerializeField] private bool endsGame;
        [SerializeField, Min(0f)] private float transitionDelay = 0.35f;
        [SerializeField] private Transform animatedVisual;

        private bool completing;
        private Vector3 initialScale;

        public GameFlowSceneId NextScene => nextScene;
        public bool EndsGame => endsGame;
        public bool IsCompleting => completing;

        public IReadOnlyList<string> GetAchievementSignalIds()
        {
            return AchievementSignals;
        }

        public void Configure(
            GameFlowSceneId valueNextScene,
            bool valueEndsGame,
            float valueTransitionDelay = 0.35f,
            Transform valueAnimatedVisual = null)
        {
            nextScene = valueNextScene;
            endsGame = valueEndsGame;
            transitionDelay = Mathf.Max(0f, valueTransitionDelay);
            animatedVisual = valueAnimatedVisual;
        }

        public bool BeginCompletion()
        {
            if (completing || GameFlowController.Instance == null)
            {
                return false;
            }

            completing = true;
            GameplaySignalHub.Emit(
                AchievementSignalIds.LevelGoalReached,
                gameObject);
            StartCoroutine(CompleteRoutine());
            return true;
        }

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            if (animatedVisual == null)
            {
                animatedVisual = transform;
            }
            initialScale = animatedVisual.localScale;
        }

        private void Update()
        {
            if (animatedVisual == null)
            {
                return;
            }

            animatedVisual.Rotate(0f, 65f * Time.unscaledDeltaTime, 0f, Space.World);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.5f) * 0.08f;
            animatedVisual.localScale = initialScale * pulse;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && other.CompareTag("Player"))
            {
                BeginCompletion();
            }
        }

        private IEnumerator CompleteRoutine()
        {
            GameAudioService.Instance?.PlayLevelComplete();
            if (transitionDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(transitionDelay);
            }

            GameFlowSceneId target = endsGame
                ? GameFlowSceneId.Ending
                : nextScene;
            if (!GameFlowController.Instance.RequestTransition(target))
            {
                completing = false;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = endsGame
                ? new Color(1f, 0.75f, 0.1f, 0.9f)
                : new Color(0.3f, 1f, 0.45f, 0.9f);
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
}
