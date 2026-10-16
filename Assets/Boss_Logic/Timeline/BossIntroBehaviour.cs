using Project.CameraModes;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public sealed class BossIntroBehaviour : PlayableBehaviour
    {
        public BossIntroClip clip;
        public GameObject owner;

        private BossController boss;
        private TimelineCamRig cameraRig;
        private CameraViewModeRequestHandle cameraModeHandle;
        private Vector3 cameraStartPosition;
        private Quaternion cameraStartRotation;
        private Vector3 cameraShotPosition;
        private Quaternion cameraShotRotation;
        private Vector3 spawnPosition;
        private bool initialized;
        private bool jumpRequested;
        private bool finished;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Initialize();
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (clip == null)
            {
                return;
            }

            if (!initialized)
            {
                Initialize();
            }

            float time = (float)playable.GetTime();
            TickCamera(time);

            float jumpTime =
                clip.cameraMoveSeconds +
                Mathf.Max(0f, clip.standSeconds);
            if (!jumpRequested && time >= jumpTime)
            {
                jumpRequested = true;
                if (boss != null)
                {
                    Vector3 landing =
                        spawnPosition + clip.landingOffset;
                    if (!boss.RequestJumpTo(
                            landing,
                            clip.jumpArcHeight,
                            clip.jumpDuration))
                    {
                        boss.TeleportTo(
                            landing,
                            boss.transform.rotation);
                    }
                }
            }

            float finishTime =
                jumpTime +
                Mathf.Max(.05f, clip.jumpDuration) +
                Mathf.Max(0f, clip.landingHoldSeconds);
            if (!finished &&
                time >= finishTime - .0001f)
            {
                Finish();
            }
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            Finish();
        }

        public override void OnGraphStop(Playable playable)
        {
            Finish();
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            Finish();
        }

        private void Initialize()
        {
            if (clip == null)
            {
                return;
            }

            initialized = true;
            jumpRequested = false;
            finished = false;
            boss = ResolveBoss();
            if (boss == null)
            {
                Debug.LogWarning(
                    "[BossIntro] 没有找到 Boss，也没有配置 Boss 预制体。",
                    owner);
                return;
            }

            spawnPosition = GetSpawnPosition();
            boss.TeleportTo(
                spawnPosition,
                boss.transform.rotation);
            if (clip.disableTrackingAtStart)
            {
                boss.SetTrackingEnabled(false);
            }

            cameraRig = ProjectDiscovery.FindFirst<TimelineCamRig>();
            if (clip.takeCameraControl &&
                cameraRig != null)
            {
                cameraRig.SetReturnDurationForCurrentShot(
                    clip.cameraReturnSeconds);
                cameraRig.Acquire(.2f);
                if (clip.requestSide2D)
                {
                    cameraModeHandle = cameraRig.RequestViewMode(
                        CameraViewMode.Side2D,
                        clip.side2DYawDegrees,
                        clip.side2DTransitionSeconds);
                }

                cameraStartPosition = cameraRig.CurrentPosition;
                cameraStartRotation = cameraRig.CurrentRotation;
                BuildCameraShot(spawnPosition);
            }
        }

        private BossController ResolveBoss()
        {
            if (clip.useExistingBoss &&
                !string.IsNullOrWhiteSpace(clip.bossTag))
            {
                GameObject tagged =
                    GameObject.FindGameObjectWithTag(clip.bossTag);
                if (tagged != null)
                {
                    BossController existing =
                        tagged.GetComponent<BossController>();
                    if (existing != null)
                    {
                        return existing;
                    }
                }
            }

            if (clip.bossPrefab == null)
            {
                return null;
            }

            BossController instance = Object.Instantiate(
                clip.bossPrefab,
                GetSpawnPosition(),
                clip.bossPrefab.transform.rotation);
            instance.name = clip.bossPrefab.name;
            return instance;
        }

        private Vector3 GetSpawnPosition()
        {
            if (clip.usePlayerPositionAsSpawn)
            {
                PlayerController player =
                    ProjectDiscovery.FindFirst<PlayerController>();
                if (player == null &&
                    !string.IsNullOrWhiteSpace(clip.playerTag))
                {
                    GameObject tagged =
                        GameObject.FindGameObjectWithTag(clip.playerTag);
                    if (tagged != null)
                    {
                        player =
                            tagged.GetComponentInParent<PlayerController>();
                    }
                }

                if (player != null)
                {
                    return player.transform.position + clip.spawnOffset;
                }
            }

            Transform anchor =
                clip.useOwnerAsSpawnAnchor && owner != null
                    ? owner.transform
                    : null;
            return anchor != null
                ? anchor.TransformPoint(clip.spawnOffset)
                : clip.spawnOffset;
        }

        private void BuildCameraShot(Vector3 focusPosition)
        {
            Vector3 lookPoint =
                focusPosition + Vector3.up * clip.cameraLookHeight;
            cameraShotPosition =
                focusPosition +
                Vector3.up * clip.cameraHeight +
                Vector3.back * clip.cameraDistance;
            Vector3 lookDirection =
                lookPoint - cameraShotPosition;
            cameraShotRotation = lookDirection.sqrMagnitude > .0001f
                ? Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up)
                : cameraStartRotation;
        }

        private void TickCamera(float time)
        {
            if (!clip.takeCameraControl ||
                cameraRig == null)
            {
                return;
            }

            float duration = Mathf.Max(.01f, clip.cameraMoveSeconds);
            float progress = Mathf.Clamp01(time / duration);
            float smooth = progress * progress * (3f - 2f * progress);
            Vector3 position = Vector3.Lerp(
                cameraStartPosition,
                cameraShotPosition,
                smooth);
            Quaternion rotation = Quaternion.Slerp(
                cameraStartRotation,
                cameraShotRotation,
                smooth);
            cameraRig.SetShotTransform(position, rotation);
        }

        private void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            if (clip != null &&
                clip.enableTrackingAtEnd &&
                boss != null &&
                !boss.IsDead)
            {
                boss.SetTrackingEnabled(true);
            }

            if (cameraRig != null)
            {
                if (cameraModeHandle.IsValid)
                {
                    cameraRig.ReleaseViewMode(
                        cameraModeHandle);
                    cameraModeHandle = default;
                }

                cameraRig.Release(true);
                cameraRig = null;
            }
        }
    }
}
