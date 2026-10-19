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
        [SerializeField, Min(0f)]
        private float blendDuration = .35f;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            CameraDownBehaviour behaviour =
                new CameraDownBehaviour
                {
                    relativeSizeMultiplier =
                        relativeSizeMultiplier,
                    focusDownBlocks =
                        focusDownBlocks,
                    blendDuration =
                        blendDuration
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
        public float blendDuration = .35f;

        private CameraControlManager manager;
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
                Vector3.down *
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
