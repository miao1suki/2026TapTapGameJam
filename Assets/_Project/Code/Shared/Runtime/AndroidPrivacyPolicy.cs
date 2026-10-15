using UnityEngine;

namespace Project.Privacy
{
    public static class AndroidPrivacyPolicy
    {
        public const string PolicyUrl = "https://www.taptap.cn/app/976206/common-privacy";

        public static void ShowPolicy()
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
            Application.OpenURL(PolicyUrl);
#endif
        }
    }
}
