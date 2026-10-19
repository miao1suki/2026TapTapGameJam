using Project.CameraModes;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public sealed class BossDeathCameraRestorePlayable :
        PlayableAsset
    {
        [SerializeField] private string bossTag = "Boss";

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            BossDeathCameraRestoreBehaviour behaviour =
                new BossDeathCameraRestoreBehaviour
                {
                    bossTag = bossTag
                };
            return ScriptPlayable<
                    BossDeathCameraRestoreBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class BossDeathCameraRestoreBehaviour :
        PlayableBehaviour
    {
        public string bossTag = "Boss";

        private bool restored;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            restored = false;
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (restored ||
                string.IsNullOrWhiteSpace(bossTag))
            {
                return;
            }

            GameObject bossObject;
            try
            {
                bossObject =
                    GameObject.FindGameObjectWithTag(
                        bossTag);
            }
            catch (UnityException)
            {
                return;
            }

            BossController boss = bossObject != null
                ? bossObject.GetComponent<BossController>()
                : null;
            if (boss == null || !boss.IsDead)
            {
                return;
            }

            BossCameraSize cameraSize =
                boss.GetComponent<BossCameraSize>();
            cameraSize?.RequestDisable();
            CameraControlManager manager =
                ProjectDiscovery
                    .FindFirst<CameraControlManager>();
            if (manager != null)
            {
                TimelineCamRig rig =
                    manager.GetComponent<TimelineCamRig>() ??
                    manager.gameObject.AddComponent<
                        TimelineCamRig>();
                if (rig.IsTimelineControlling)
                {
                    rig.ReleaseTimelineControl(true);
                }
            }
            restored = true;
        }
    }
}
