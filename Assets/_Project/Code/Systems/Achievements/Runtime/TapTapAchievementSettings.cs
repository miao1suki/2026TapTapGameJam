using UnityEngine;

namespace Project.Achievements
{
    [CreateAssetMenu(
        menuName = "2026TapTap/Achievements/TapTap SDK Settings",
        fileName = "TapTapAchievementSettings")]
    public sealed class TapTapAchievementSettings :
        ScriptableObject
    {
        [SerializeField]
        private bool enabled;

        [SerializeField]
        private string clientId = string.Empty;

        [SerializeField, HideInInspector]
        private string clientToken = string.Empty;

        [SerializeField]
        private bool backendAchievementIdsConfirmed;

        [SerializeField]
        private bool autoLoginWithBasicInfo = true;

        [SerializeField]
        private bool enableSdkToast;

        [SerializeField]
        private bool enableSdkLog;

        public bool Enabled => enabled;
        public string ClientId => clientId;
        public string ClientToken => clientToken;
        public bool BackendAchievementIdsConfirmed =>
            backendAchievementIdsConfirmed;
        public bool AutoLoginWithBasicInfo =>
            autoLoginWithBasicInfo;
        public bool EnableSdkToast => enableSdkToast;
        public bool EnableSdkLog => enableSdkLog;

        public bool HasCredentials =>
            !string.IsNullOrWhiteSpace(clientId);

        public bool CanInitialize =>
            enabled && HasCredentials;

        public bool CanReport =>
            CanInitialize &&
            backendAchievementIdsConfirmed;
    }
}
