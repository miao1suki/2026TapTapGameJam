using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public sealed class BossDeathAnimationTriggerPlayable :
        PlayableAsset
    {
        [SerializeField] private string bossTag = "Boss";

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            BossDeathAnimationTriggerBehaviour behaviour =
                new BossDeathAnimationTriggerBehaviour
                {
                    bossTag = bossTag
                };
            return ScriptPlayable<
                    BossDeathAnimationTriggerBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class BossDeathAnimationTriggerBehaviour :
        PlayableBehaviour
    {
        public string bossTag = "Boss";

        private bool triggered;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            triggered = false;
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (triggered ||
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

            BossAnimationDriver animation =
                boss.GetComponent<BossAnimationDriver>();
            animation?.PlayDeathAnimationNow();
            triggered = true;
        }
    }
}
