using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    internal sealed class BossPostDeathDirectorLifetime :
        MonoBehaviour
    {
        private PlayableDirector director;

        public void Bind(PlayableDirector value)
        {
            if (director != null)
            {
                director.stopped -= HandleStopped;
            }
            director = value;
            if (director != null)
            {
                director.stopped += HandleStopped;
            }
        }

        private void OnDestroy()
        {
            if (director != null)
            {
                director.stopped -= HandleStopped;
            }
        }

        private void HandleStopped(
            PlayableDirector stoppedDirector)
        {
            if (stoppedDirector != director ||
                !Application.isPlaying)
            {
                return;
            }
            Destroy(gameObject, .1f);
        }
    }
}
