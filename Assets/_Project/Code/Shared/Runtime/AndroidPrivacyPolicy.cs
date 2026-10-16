using System.Net;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Privacy
{
    public static class AndroidPrivacyPolicy
    {
        private static GameObject viewer;
        public static bool IsShowing => viewer != null;

        public static void ShowPolicy(TMP_FontAsset font)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                using var uiPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var uiActivity = uiPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var privacy = new AndroidJavaClass("com.miao1suki.shapeofcolor.privacy.PrivacyConsent");
                privacy.CallStatic("showPolicy", uiActivity);
            }));
#else
            if (viewer != null) return;
            var policy = Resources.Load<TextAsset>("Privacy/privacy-policy");
            if (policy == null || font == null) { Debug.LogError("缺少内置隐私政策或项目字体。"); return; }
            viewer = new GameObject("隐私政策", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = viewer.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
            var scaler = viewer.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Rect("背景", viewer.transform); Stretch(background);
            background.gameObject.AddComponent<Image>().color = new Color(.035f, .055f, .075f, .99f);
            var title = Text("标题", viewer.transform, font, "隐私政策", 30);
            title.alignment = TextAlignmentOptions.Center; title.rectTransform.anchorMin = new Vector2(.05f, .9f);
            title.rectTransform.anchorMax = new Vector2(.95f, .98f); title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            var scroll = Rect("正文", viewer.transform); scroll.anchorMin = new Vector2(.06f, .15f); scroll.anchorMax = new Vector2(.94f, .88f);
            scroll.offsetMin = scroll.offsetMax = Vector2.zero;
            var viewport = Rect("视口", scroll); Stretch(viewport); viewport.gameObject.AddComponent<Image>().color = new Color(.07f, .1f, .13f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var body = Text("全文", viewport, font, PlainText(policy.text), 22);
            body.richText = false; body.alignment = TextAlignmentOptions.TopLeft; body.margin = new Vector4(18, 14, 18, 14);
            body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.pivot = new Vector2(.5f, 1); body.rectTransform.sizeDelta = Vector2.zero;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrolling = scroll.gameObject.AddComponent<ScrollRect>(); scrolling.horizontal = false;
            scrolling.viewport = viewport; scrolling.content = body.rectTransform; scrolling.movementType = ScrollRect.MovementType.Clamped;
            scrolling.scrollSensitivity = 36;
            var close = Rect("返回", viewer.transform); close.anchorMin = close.anchorMax = new Vector2(.5f, .07f); close.sizeDelta = new Vector2(200, 58);
            var image = close.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .3f, .42f);
            var button = close.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => Object.Destroy(viewer));
            var label = Text("文字", close, font, "返回", 26); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
#endif
        }

        public static string PlainText(string html)
        {
            string text = Regex.Replace(html, @"</?(?:h[1-6]|p|li|br)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            return WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]*>", "")).Trim();
        }
        private static RectTransform Rect(string name, Transform parent)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string value, int size)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
            label.fontSize = size; label.text = value; label.color = Color.white; label.raycastTarget = false; return label;
        }
    }
}
