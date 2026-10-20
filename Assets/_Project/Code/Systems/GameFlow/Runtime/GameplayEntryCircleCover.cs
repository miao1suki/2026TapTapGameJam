using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameplayEntryCircleCover :
        MonoBehaviour
    {
        private const string ShaderName =
            "Hidden/2026TapTap/StoryStartCircleCurtain";
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");
        private static readonly int CenterId =
            Shader.PropertyToID("_Center");
        private static readonly int RadiusId =
            Shader.PropertyToID("_Radius");
        private static readonly int AspectId =
            Shader.PropertyToID("_Aspect");

        private CanvasGroup group;
        private Material material;
        private float maxRadius;
        private float progress;

        public static GameplayEntryCircleCover Create(
            Transform parent)
        {
            GameObject root = new GameObject(
                "Gameplay Entry Circle Cover",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            GameplayEntryCircleCover cover =
                root.AddComponent<
                    GameplayEntryCircleCover>();
            cover.Build();
            return cover;
        }

        public IEnumerator CoverAsync(float duration)
        {
            float start = progress;
            Tween tween = DOTween.To(
                    () => start,
                    value =>
                    {
                        progress = value;
                        SetProgress(value);
                    },
                    1f,
                    Mathf.Max(0f, duration))
                .SetEase(Ease.InOutCubic)
                .SetUpdate(true)
                .SetTarget(this);
            yield return tween.WaitForCompletion();
            progress = 1f;
            SetProgress(1f);
        }

        public void Dispose()
        {
            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        private void Build()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50000;

            CanvasScaler scaler =
                GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.Expand;

            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = false;

            GameObject panel = new GameObject(
                "Circle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform rect =
                panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = panel.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;

            Shader shader = Shader.Find(ShaderName);
            if (shader != null)
            {
                material = new Material(shader)
                {
                    name =
                        "Gameplay Entry Circle Cover (Runtime)"
                };
                material.SetColor(
                    ColorId,
                    Color.black);
                material.SetVector(
                    CenterId,
                    new Vector2(.5f, .5f));
                image.material = material;
            }
            else
            {
                image.color = Color.black;
            }

            Canvas.ForceUpdateCanvases();
            RectTransform root =
                GetComponent<RectTransform>();
            float width = root.rect.width > 1f
                ? root.rect.width
                : Mathf.Max(1f, Screen.width);
            float height = root.rect.height > 1f
                ? root.rect.height
                : Mathf.Max(1f, Screen.height);
            float aspect = width / height;
            if (material != null)
            {
                material.SetFloat(AspectId, aspect);
                maxRadius =
                    .5f *
                    Mathf.Sqrt(
                        aspect * aspect + 1f) *
                    1.02f;
            }

            progress = 0f;
            SetProgress(0f);
        }

        private void SetProgress(float value)
        {
            float clamped =
                Mathf.Clamp01(value);
            if (material != null)
            {
                group.alpha = 1f;
                material.SetFloat(
                    RadiusId,
                    Mathf.Lerp(
                        maxRadius,
                        0f,
                        clamped));
            }
            else
            {
                group.alpha = clamped;
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }
    }
}
