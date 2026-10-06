using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameLoadingScreenAdapter :
        MonoBehaviour,
        IGameLoadingScreen
    {
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Text progressLabel;

        public void ShowLoading()
        {
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(true);
            }
        }

        public void SetLoadingProgress(float progress)
        {
            float value = Mathf.Clamp01(progress);
            if (progressSlider != null)
            {
                progressSlider.SetValueWithoutNotify(value);
            }

            if (progressLabel != null)
            {
                progressLabel.text =
                    $"{Mathf.RoundToInt(value * 100f)}%";
            }
        }

        public void HideLoading()
        {
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(false);
            }
        }

        private void Awake()
        {
            if (canvasRoot == null)
            {
                canvasRoot = gameObject;
            }

            GameSceneLoader.Instance.RegisterLoadingScreen(this);
            if (Application.isPlaying)
            {
                HideLoading();
            }
        }

        private void OnDestroy()
        {
            GameSceneLoader.Existing?.ClearLoadingScreen(this);
        }
    }
}
