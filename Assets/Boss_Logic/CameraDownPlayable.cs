using Project.BlockFeatures;
using Project.CameraModes;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public sealed class CameraDownPlayable : PlayableAsset
    {
        [SerializeField, Min(1f)]
        private float relativeSizeMultiplier = 1.3f;
        [SerializeField, Min(0f)]
        private float focusDownBlocks = 5f;
        [Tooltip("表演坐标系中的下移方向，长度由 focusDownBlocks 决定。")]
        [SerializeField]
        private Vector3 triggerLocalDown =
            new Vector3(0f, -1f, 0f);
        [SerializeField, Min(0f)]
        private float blendDuration = .35f;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                BossTimelinePerformanceFrame.ResolveDirector(
                    graph,
                    owner);
            CameraDownBehaviour behaviour =
                new CameraDownBehaviour
                {
                    relativeSizeMultiplier =
                        relativeSizeMultiplier,
                    focusDownBlocks =
                        focusDownBlocks,
                    triggerLocalDown =
                        triggerLocalDown,
                    blendDuration =
                        blendDuration,
                    director = resolvedDirector
                };
            return ScriptPlayable<CameraDownBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class CameraDownBehaviour :
        PlayableBehaviour
    {
        public float relativeSizeMultiplier = 1.3f;
        public float focusDownBlocks = 5f;
        public Vector3 triggerLocalDown =
            new Vector3(0f, -1f, 0f);
        public float blendDuration = .35f;
        public PlayableDirector director;

        private CameraControlManager manager;
        private BossTimelinePerformanceFrame frame;
        private CameraLensRequestHandle lensHandle;
        private CameraViewOffsetRequestHandle offsetHandle;
        private bool warnedMissingCamera;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Release();
            manager = ResolveManager();
            if (manager == null)
            {
                if (!warnedMissingCamera)
                {
                    Debug.LogWarning(
                        "[CameraDown] 场景中缺少 CameraControlManager，视角下移不会生效。");
                    warnedMissingCamera = true;
                }
                return;
            }
            if (!BossTimelinePerformanceFrame.TryResolve(
                    director,
                    nameof(CameraDownPlayable),
                    out frame))
            {
                return;
            }

            warnedMissingCamera = false;
            CameraTransition transition =
                CameraTransition.Ease(blendDuration);
            lensHandle =
                manager.RequestRelativeLensMultiplier(
                    this,
                    relativeSizeMultiplier,
                    transition);

            PlayerController player =
                ProjectDiscovery
                    .FindFirst<PlayerController>();
            float cellSize = player != null
                ? GridCellSizeUtility.Resolve(player)
                : 1f;
            Vector3 offset =
                frame.ToWorldOffset(triggerLocalDown) *
                Mathf.Max(0f, focusDownBlocks) *
                Mathf.Max(.01f, cellSize);
            offsetHandle =
                manager.RequestViewOffset(
                    this,
                    offset,
                    transition);
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
            CameraTransition transition =
                CameraTransition.Ease(blendDuration);
            if (lensHandle.IsValid)
            {
                lensHandle.Release(transition);
            }
            if (offsetHandle.IsValid)
            {
                offsetHandle.Release(transition);
            }
            lensHandle = default;
            offsetHandle = default;
            frame = default;
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
