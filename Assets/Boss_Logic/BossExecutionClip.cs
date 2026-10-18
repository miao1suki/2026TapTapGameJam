using System.Collections.Generic;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.BossLogic
{
    public sealed class BossExecutionClip :
        PlayableAsset,
        ITimelineClipAsset
    {
        [SerializeField] private string bossTag = "Boss";
        [SerializeField, Min(0f)]
        private float cameraMoveDuration = 1.2f;
        [SerializeField, Min(0f)]
        private float moveSpeedBlocksPerSecond = 10f;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                BossExecutionBehaviour.FindOwningDirector(
                    graph);
            if (resolvedDirector == null && owner != null)
            {
                resolvedDirector =
                    owner.GetComponent<PlayableDirector>();
            }
            BossExecutionBehaviour behaviour =
                new BossExecutionBehaviour
                {
                    bossTag = bossTag,
                    cameraMoveDuration =
                        cameraMoveDuration,
                    moveSpeedBlocksPerSecond =
                        moveSpeedBlocksPerSecond,
                    director = resolvedDirector
                };
            return ScriptPlayable<BossExecutionBehaviour>
                .Create(graph, behaviour);
        }

        public ClipCaps clipCaps => ClipCaps.None;
    }

    public sealed class BossExecutionBehaviour :
        PlayableBehaviour
    {
        public string bossTag = "Boss";
        public float cameraMoveDuration = 1.2f;
        public float moveSpeedBlocksPerSecond = 10f;
        public PlayableDirector director;

        private BossController boss;
        private PlayerController player;
        private BossRightMoveExecutor mover;
        private bool started;
        private bool movementStarted;
        private bool stopping;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            started = false;
            movementStarted = false;
            stopping = false;
            boss = null;
            player = null;
            mover = null;
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (stopping)
            {
                return;
            }
            if (director == null)
            {
                director = FindOwningDirector(
                    playable.GetGraph());
            }

            ResolveBoss();
            if (boss == null)
            {
                return;
            }
            if (boss.IsDead)
            {
                stopping = true;
                director?.Stop();
                return;
            }
            if (!started)
            {
                started = true;
                AlignBossHeightWithPlayer();
                mover?.SetExecutionSuspended(true);
                boss.BeginExecutionSequence(
                    moveSpeedBlocksPerSecond);
            }
            if (!movementStarted &&
                playable.GetTime() >= cameraMoveDuration)
            {
                movementStarted = true;
                boss.StartExecutionMovement();
            }
        }

        private void AlignBossHeightWithPlayer()
        {
            if (boss == null)
            {
                return;
            }

            if (player == null)
            {
                GameObject found;
                try
                {
                    found = GameObject.FindGameObjectWithTag(
                        "Player");
                }
                catch (UnityException)
                {
                    found = null;
                }
                if (found != null)
                {
                    player = found.GetComponent<
                        PlayerController>();
                }
            }
            if (player == null)
            {
                return;
            }

            Vector3 position = boss.transform.position;
            position.y = player.transform.position.y;
            boss.TeleportTo(
                position,
                boss.transform.rotation);
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            Cleanup();
        }

        public override void OnGraphStop(Playable playable)
        {
            Cleanup();
        }

        public override void OnPlayableDestroy(
            Playable playable)
        {
            Cleanup();
        }

        private void ResolveBoss()
        {
            if (boss != null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(bossTag))
            {
                return;
            }

            GameObject found;
            try
            {
                found = GameObject.FindGameObjectWithTag(
                    bossTag);
            }
            catch (UnityException)
            {
                return;
            }
            if (found == null)
            {
                return;
            }

            boss = found.GetComponent<BossController>();
            mover = found.GetComponent<BossRightMoveExecutor>();
            boss.ExecutionSequenceTerminated -=
                HandleExecutionTerminated;
            boss.ExecutionSequenceTerminated +=
                HandleExecutionTerminated;
            boss.Died -= HandleExecutionTerminated;
            boss.Died += HandleExecutionTerminated;
        }

        private void Cleanup()
        {
            if (boss != null)
            {
                boss.ExecutionSequenceTerminated -=
                    HandleExecutionTerminated;
                boss.Died -= HandleExecutionTerminated;
                boss.EndExecutionSequence();
            }
            mover?.SetExecutionSuspended(false);
            boss = null;
            player = null;
            mover = null;
            started = false;
            movementStarted = false;
        }

        private void HandleExecutionTerminated()
        {
            if (stopping)
            {
                return;
            }

            stopping = true;
            director?.Stop();
        }

        internal static PlayableDirector FindOwningDirector(
            PlayableGraph graph)
        {
            PlayableDirector resolved =
                graph.GetResolver() as PlayableDirector;
            if (resolved != null)
            {
                return resolved;
            }

            IReadOnlyList<PlayableDirector> directors =
                ProjectDiscovery.FindAll<PlayableDirector>(true);
            for (int index = 0;
                 index < directors.Count;
                 index++)
            {
                PlayableDirector candidate =
                    directors[index];
                if (candidate == null ||
                    candidate.state != PlayState.Playing ||
                    candidate.playableAsset is not TimelineAsset timeline)
                {
                    continue;
                }

                foreach (TrackAsset track in
                         timeline.GetOutputTracks())
                {
                    if (track is
                        BossExecutionTimelineTrack)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
