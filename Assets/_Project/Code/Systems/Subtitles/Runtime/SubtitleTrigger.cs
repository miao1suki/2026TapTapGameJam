using Project.Player;
using UnityEngine;

namespace Project.Subtitles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SubtitleTrigger : MonoBehaviour
    {
        [SerializeField] private SubtitleCue cue = new SubtitleCue();
        [SerializeField] private bool playOnce = true;
        [SerializeField] private bool hideVisualOnAwake = true;
        [SerializeField] private bool destroyAfterPlay;

        private bool played;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            if (hideVisualOnAwake && Application.isPlaying)
            {
                SetVisualsVisible(false);
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                SetVisualsVisible(true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (played && playOnce)
            {
                return;
            }

            PlayerController player =
                other != null
                    ? other.GetComponentInParent<PlayerController>()
                    : null;
            if (player == null)
            {
                return;
            }

            played = true;
            SubtitleServiceRegistry.Current?.Show(cue);
            if (destroyAfterPlay)
            {
                Destroy(gameObject);
            }
        }

        [ContextMenu("调试显示字幕")]
        public void ShowDebug()
        {
            SubtitleServiceRegistry.Current?.Show(cue);
        }

        private void SetVisualsVisible(bool visible)
        {
            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                renderers[index].enabled = visible;
            }
        }
    }
}
