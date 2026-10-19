using Project.CameraModes;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    /// <summary>
    /// Timeline-owned, region-scoped lens scale request. The trigger director
    /// plays for as long as the actor stays inside a WhileInside trigger.
    /// </summary>
    public sealed class CameraClosePlayable : PlayableAsset
    {
        [SerializeField, Min(.01f)]
        private float sizeScale = .5f;

        [SerializeField, Min(0f)]
        private float blendDuration = 1.25f;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            CameraCloseBehaviour behaviour =
                new CameraCloseBehaviour
                {
                    sizeScale = sizeScale,
                    blendDuration = blendDuration
                };
            return ScriptPlayable<CameraCloseBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class CameraCloseBehaviour :
        PlayableBehaviour
    {
        public float sizeScale = .5f;
        public float blendDuration = 1.25f;

        private CameraControlManager manager;
        private CameraLensRequestHandle lensHandle;
        private PlayableDirector director;
        private bool warnedMissingCamera;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Release();
            director =
                playable.GetGraph().GetResolver()
                as PlayableDirector;
            if (director != null)
            {
                director.stopped -=
                    OnDirectorStopped;
                director.stopped +=
                    OnDirectorStopped;
            }
            manager = ResolveManager();
            if (manager == null)
            {
                if (!warnedMissingCamera)
                {
                    Debug.LogWarning(
                        "[CameraClose] 场景中缺少 CameraControlManager，视角缩放不会生效。");
                    warnedMissingCamera = true;
                }
                return;
            }

            warnedMissingCamera = false;
            lensHandle = manager.RequestLensScale(
                this,
                Mathf.Max(.01f, sizeScale),
                CameraTransition.Ease(
                    Mathf.Max(0f, blendDuration)));
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            Release();
        }

        public override void OnGraphStop(
            Playable playable)
        {
            Release();
        }

        public override void OnPlayableDestroy(
            Playable playable)
        {
            Release();
        }

        private void Release()
        {
            if (director != null)
            {
                director.stopped -=
                    OnDirectorStopped;
                director = null;
            }
            if (lensHandle.IsValid)
            {
                lensHandle.Release(
                    CameraTransition.Ease(
                        Mathf.Max(0f, blendDuration)));
            }
            lensHandle = default;
        }

        private void OnDirectorStopped(
            PlayableDirector stoppedDirector)
        {
            Release();
        }

        private static CameraControlManager ResolveManager()
        {
            Camera mainCamera = Camera.main;
            CameraControlManager resolved =
                mainCamera != null
                    ? mainCamera.GetComponent<
                        CameraControlManager>()
                    : null;
            return resolved ??
                   ProjectDiscovery
                       .FindFirst<CameraControlManager>();
        }
    }
}
