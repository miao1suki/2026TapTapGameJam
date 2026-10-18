using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public sealed class BossHealthBarActivationPlayable : PlayableAsset
    {
        [SerializeField] private string bossTag = "Boss";
        [SerializeField] private bool enableCameraSize = true;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            BossHealthBarActivationBehaviour behaviour =
                new BossHealthBarActivationBehaviour
                {
                    bossTag = bossTag,
                    enableCameraSize = enableCameraSize
                };
            return ScriptPlayable<BossHealthBarActivationBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class BossHealthBarActivationBehaviour :
        PlayableBehaviour
    {
        public string bossTag = "Boss";
        public bool enableCameraSize = true;

        private bool activated;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            activated = false;
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (activated || string.IsNullOrWhiteSpace(bossTag))
            {
                return;
            }

            GameObject bossObject = null;
            try
            {
                bossObject =
                    GameObject.FindGameObjectWithTag(bossTag);
            }
            catch (UnityException)
            {
                return;
            }

            BossController boss = bossObject != null
                ? bossObject.GetComponent<BossController>()
                : null;
            if (boss == null)
            {
                return;
            }

            boss.ActivateHealthSystem();
            if (enableCameraSize)
            {
                BossCameraSize cameraSize =
                    boss.GetComponent<BossCameraSize>();
                cameraSize?.RequestEnable();
            }
            activated = true;
        }
    }
}
