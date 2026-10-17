using Project.BlockFeatures;
using Project.Player;
using UnityEngine;

namespace Project.Subtitles
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SubtitleTrigger : MonoBehaviour
    {
        [SerializeField] private SubtitleCue cue = new SubtitleCue();
        [SerializeField] private bool playOnce = true;
        [SerializeField] private bool hideVisualOnAwake = true;
        [SerializeField] private bool destroyAfterPlay;
        [SerializeField, BlockParameter(Label = "显示告示牌", Group = "告示牌外观", Order = 0,
            Tooltip = "显示场景中的告示牌图样，不改变字幕触发范围与字幕内容。")]
        private bool showSign = true;
        [SerializeField, BlockParameter(Label = "告示牌图样", Group = "告示牌外观", Order = 1,
            Tooltip = "选择告示牌精灵图；留空使用默认告示牌。按16源像素对应一格显示，保持原图比例。")]
        private Sprite signSprite;

        private bool played;
        private SubtitleSignVisual signVisual;
        private bool refreshSign;
        private Vector3 signParentScale;

        public bool ShowSign => showSign;
        public Sprite SignSprite => signSprite;

        private void OnEnable()
        {
            RefreshSignVisual();
        }

        private void OnDisable()
        {
            signVisual?.Dispose();
            signVisual = null;
        }

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
            refreshSign = true;
            if (!Application.isPlaying)
            {
                SetVisualsVisible(true);
            }
        }

        private void Update()
        {
            if (refreshSign || transform.lossyScale != signParentScale)
                RefreshSignVisual();
        }

        public void RefreshSignVisual()
        {
#if UNITY_EDITOR
            // Asset import/validation must not generate children in a prefab asset.
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
            signVisual ??= new SubtitleSignVisual(transform);
            signVisual.Refresh(showSign, signSprite);
            signParentScale = transform.lossyScale;
            refreshSign = false;
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
                if (renderers[index] != signVisual?.Renderer)
                    renderers[index].enabled = visible;
            }
        }
    }
}
