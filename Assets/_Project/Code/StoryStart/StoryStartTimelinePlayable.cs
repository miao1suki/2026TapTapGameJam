using System;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.StoryStart
{
    [Serializable]
    public sealed class StoryStartTimelineSettings
    {
        public TMP_FontAsset font;
        [TextArea]
        public string[] openingLines =
        {
            "很久以前，世界陷入危机，",
            "原来五彩缤纷的世界，有一天突然变成黑白。",
            "传说里，只有收集到足够的“调色盘”，投入到涌现之泉里，才能让世界恢复原样。"
        };
        [TextArea]
        public string[] closingLines =
        {
            "就这样，",
            "史莱姆大人踏上了拯救世界的征途。"
        };
        [Min(1f)] public float fontSize = 72f;
        [Min(0f)] public float messageDisplaySeconds = 2f;
        [Min(0f)] public float messageGapSeconds = .5f;
        [Min(1f)] public float typewriterCharactersPerSecond = 14f;
        public Color curtainColor = Color.black;
        [Min(0)] public int curtainSortingOrder = 1100;
        [Min(0f)] public float curtainCoverDuration = 1.2f;
        [Min(0f)] public float curtainRevealDuration = 1f;
        [Min(0f)] public float curtainOverscan = .02f;
        [Min(0f)] public float lensMultiplier = 1.4f;
        [Min(0f)] public float lensExpandDuration = .6f;
        [Min(0f)] public float playerLeftDistanceBlocks = 16f;
        [Min(0f)]
        public float playerReturnSpeedBlocksPerSecond = 8f;
        [Min(0f)] public float cameraReleaseSeconds = .25f;
        public string playerTag = "Player";
    }

    public sealed class StoryStartTimelinePlayable :
        PlayableAsset
    {
        [SerializeField]
        private StoryStartTimelineSettings settings =
            new StoryStartTimelineSettings();

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                graph.IsValid()
                    ? graph.GetResolver() as PlayableDirector
                    : null;
            if (resolvedDirector == null && owner != null)
            {
                resolvedDirector =
                    owner.GetComponentInParent<
                        PlayableDirector>();
            }

            StoryStartTimelineBehaviour behaviour =
                new StoryStartTimelineBehaviour
                {
                    settings = settings,
                    director = resolvedDirector
                };
            return ScriptPlayable<StoryStartTimelineBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class StoryStartTimelineBehaviour :
        PlayableBehaviour
    {
        public StoryStartTimelineSettings settings;
        public PlayableDirector director;

        private StoryStartCutsceneRunner runner;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            CleanupRunner();
            if (!Application.isPlaying)
            {
                return;
            }

            GameObject runnerObject = new GameObject(
                "Story Start Cutscene Runner");
            runner = runnerObject.AddComponent<
                StoryStartCutsceneRunner>();
            runner.Begin(settings, director);
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            CleanupRunner();
        }

        public override void OnGraphStop(
            Playable playable)
        {
            CleanupRunner();
        }

        public override void OnPlayableDestroy(
            Playable playable)
        {
            CleanupRunner();
        }

        private void CleanupRunner()
        {
            if (runner == null)
            {
                return;
            }

            runner.Abort();
            if (runner.gameObject != null)
            {
                UnityEngine.Object.Destroy(
                    runner.gameObject);
            }
            runner = null;
        }
    }
}
