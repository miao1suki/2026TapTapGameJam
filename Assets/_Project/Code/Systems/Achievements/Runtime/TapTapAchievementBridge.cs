using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TapSDK.Achievement;
using TapSDK.Core;
using TapSDK.Login;
using UnityEngine;

namespace Project.Achievements
{
    public sealed class TapTapAchievementBridge :
        MonoBehaviour,
        ITapInitCallback,
        ITapAchievementCallback
    {
        private const string SettingsResourcePath =
            "TapTap/TapTapAchievementSettings";
        private const string SecretsResourcePath =
            "TapTap/TapTapAchievementSecrets";
        private const string ReportFileName =
            "TapTapReports.json";

        [Serializable]
        private sealed class LocalSecrets
        {
            public string clientToken = string.Empty;
        }

        [Serializable]
        private sealed class ReportFile
        {
            public int schemaVersion = 1;
            public List<AccountReport> accounts =
                new List<AccountReport>();
        }

        [Serializable]
        private sealed class AccountReport
        {
            public string accountId = string.Empty;
            public List<string> achievementIds =
                new List<string>();
        }

        private static TapTapAchievementBridge instance;

        private readonly HashSet<string> pendingIds =
            new HashSet<string>(
                StringComparer.Ordinal);
        private readonly ReportFile reportFile =
            new ReportFile();

        private TapTapAchievementSettings settings;
        private AchievementManager achievementManager;
        private string reportFilePath;
        private string clientToken = string.Empty;
        private string accountId = string.Empty;
        private bool initialized;
        private bool loggedIn;
        private bool callbacksRegistered;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                return;
            }

            TapTapAchievementSettings settings =
                Resources.Load<TapTapAchievementSettings>(
                    SettingsResourcePath);
            if (settings == null || !settings.CanInitialize)
            {
                return;
            }

            GameObject bridgeObject =
                new GameObject("[TapTapAchievementBridge]");
            bridgeObject.AddComponent<TapTapAchievementBridge>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            settings =
                Resources.Load<TapTapAchievementSettings>(
                    SettingsResourcePath);
            if (settings == null || !settings.CanInitialize)
            {
                Destroy(gameObject);
                return;
            }

            clientToken = ResolveClientToken();
            if (string.IsNullOrWhiteSpace(clientToken))
            {
                Debug.LogWarning(
                    "[TapTapAchievementBridge] Missing local " +
                    "TapTap client token. SDK remains disabled.");
                Destroy(gameObject);
                return;
            }

            reportFilePath = Path.Combine(
                Application.persistentDataPath,
                "Achievements",
                ReportFileName);
            LoadReportFile();

            achievementManager =
                AchievementManager.Instance;
            achievementManager.AchievementUnlocked +=
                OnAchievementUnlocked;

            TapTapSDK.AddInitCallback(this);
            TapTapAchievement.RegisterCallBack(this);
            callbacksRegistered = true;
            TryInitializeSdk();
        }

        private void TryInitializeSdk()
        {
            try
            {
                TapTapSdkOptions coreOptions =
                    new TapTapSdkOptions
                    {
                        clientId = settings.ClientId.Trim(),
                        clientToken = clientToken,
                        region = TapTapRegionType.CN,
                        enableLog = settings.EnableSdkLog
                    };
                TapTapSdkBaseOptions[] otherOptions =
                {
                    new TapTapAchievementOptions(
                        settings.EnableSdkToast)
                };
                TapTapSDK.Init(
                    coreOptions,
                    otherOptions);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[TapTapAchievementBridge] TapSDK " +
                    "initialization threw. Local achievements " +
                    "remain available.\n" +
                    exception);
            }
        }

        public void OnInitSuccess()
        {
            initialized = true;
            Debug.Log(
                "[TapTapAchievementBridge] TapSDK initialized.");
            ReconcileAfterLoginAsync();
        }

        public void OnInitFail(
            int errorCode,
            string errorMsg)
        {
            initialized = false;
            Debug.LogWarning(
                "[TapTapAchievementBridge] TapSDK " +
                $"initialization failed ({errorCode}): " +
                errorMsg);
        }

        private async void ReconcileAfterLoginAsync()
        {
            try
            {
                TapTapAccount account =
                    await TapTapLogin.Instance
                        .GetCurrentTapAccount();
                if (account == null &&
                    settings.AutoLoginWithBasicInfo)
                {
                    account =
                        await TapTapLogin.Instance
                            .LoginWithScopes(
                                new[]
                                {
                                    TapTapLogin
                                        .TAP_LOGIN_SCOPE_BASIC_INFO
                                });
                }

                if (account == null)
                {
                    Debug.LogWarning(
                        "[TapTapAchievementBridge] No TapTap " +
                        "account is signed in. Local achievements " +
                        "will be retried after login.");
                    return;
                }

                accountId = ResolveAccountId(account);
                loggedIn = !string.IsNullOrWhiteSpace(
                    accountId);
                if (loggedIn)
                {
                    ReportAllLocallyUnlocked();
                }
            }
            catch (TaskCanceledException)
            {
                Debug.Log(
                    "[TapTapAchievementBridge] TapTap login " +
                    "was cancelled.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[TapTapAchievementBridge] TapTap login " +
                    "failed. Local achievements remain available.\n" +
                    exception);
            }
        }

        private void OnAchievementUnlocked(
            AchievementSO achievement)
        {
            TryReport(achievement);
        }

        private void ReportAllLocallyUnlocked()
        {
            if (!CanReport())
            {
                return;
            }

            IReadOnlyList<AchievementSO> achievements =
                achievementManager.GetAllAchievements();
            for (int index = 0;
                 index < achievements.Count;
                 index++)
            {
                AchievementSO achievement =
                    achievements[index];
                if (achievement != null &&
                    achievementManager.IsUnlocked(
                        achievement.AchievementId))
                {
                    TryReport(achievement);
                }
            }
        }

        private void TryReport(
            AchievementSO achievement)
        {
            if (achievement == null ||
                !CanReport())
            {
                return;
            }

            string platformId =
                achievement.PlatformAchievementId?.Trim();
            if (string.IsNullOrWhiteSpace(platformId))
            {
                Debug.LogWarning(
                    "[TapTapAchievementBridge] Achievement " +
                    $"{achievement.AchievementId} has no platform ID.");
                return;
            }

            if (WasReported(platformId) ||
                !pendingIds.Add(platformId))
            {
                return;
            }

            TapTapAchievement.Unlock(platformId);
        }

        private bool CanReport()
        {
            return initialized &&
                   loggedIn &&
                   settings.CanReport;
        }

        public void OnAchievementSuccess(
            int code,
            TapAchievementResult result)
        {
            string achievementId =
                result?.AchievementId?.Trim();
            if (string.IsNullOrWhiteSpace(achievementId))
            {
                return;
            }

            pendingIds.Remove(achievementId);
            if (code ==
                TapTapAchievementConstants.UNLOCK_SUCCESS)
            {
                MarkReported(achievementId);
            }
        }

        public void OnAchievementFailure(
            string achievementId,
            int errorCode,
            string errorMsg)
        {
            string id = achievementId?.Trim();
            if (!string.IsNullOrWhiteSpace(id))
            {
                pendingIds.Remove(id);
            }

            if (errorCode ==
                    TapTapAchievementConstants.NOT_LOGGED ||
                errorCode ==
                    TapTapAchievementConstants.ACCESS_DENIED)
            {
                loggedIn = false;
            }

            Debug.LogWarning(
                "[TapTapAchievementBridge] Platform " +
                $"achievement report failed ({errorCode}) " +
                $"for '{achievementId}': {errorMsg}");
        }

        private static string ResolveAccountId(
            TapTapAccount account)
        {
            if (!string.IsNullOrWhiteSpace(account.unionId))
            {
                return account.unionId.Trim();
            }

            return account.openId?.Trim() ??
                   string.Empty;
        }

        private string ResolveClientToken()
        {
            TextAsset secretAsset =
                Resources.Load<TextAsset>(
                    SecretsResourcePath);
            if (secretAsset != null &&
                !string.IsNullOrWhiteSpace(secretAsset.text))
            {
                try
                {
                    LocalSecrets secrets =
                        JsonUtility.FromJson<LocalSecrets>(
                            secretAsset.text);
                    if (!string.IsNullOrWhiteSpace(
                            secrets?.clientToken))
                    {
                        return secrets.clientToken.Trim();
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[TapTapAchievementBridge] Local TapTap " +
                        "secret file is invalid.\n" +
                        exception);
                }
            }

            return settings.ClientToken?.Trim() ??
                   string.Empty;
        }

        private AccountReport FindAccountReport(
            string id)
        {
            for (int index = 0;
                 index < reportFile.accounts.Count;
                 index++)
            {
                AccountReport report =
                    reportFile.accounts[index];
                if (report != null &&
                    report.accountId == id)
                {
                    return report;
                }
            }

            AccountReport created =
                new AccountReport
                {
                    accountId = id
                };
            reportFile.accounts.Add(created);
            return created;
        }

        private bool WasReported(string platformId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return false;
            }

            AccountReport report =
                FindAccountReport(accountId);
            return report.achievementIds.Contains(
                platformId);
        }

        private void MarkReported(string platformId)
        {
            if (string.IsNullOrWhiteSpace(accountId) ||
                WasReported(platformId))
            {
                return;
            }

            FindAccountReport(accountId)
                .achievementIds.Add(platformId);
            SaveReportFile();
        }

        private void LoadReportFile()
        {
            if (!File.Exists(reportFilePath))
            {
                return;
            }

            try
            {
                ReportFile loaded =
                    JsonUtility.FromJson<ReportFile>(
                        File.ReadAllText(reportFilePath));
                if (loaded == null ||
                    loaded.schemaVersion != 1 ||
                    loaded.accounts == null)
                {
                    return;
                }

                reportFile.accounts =
                    loaded.accounts;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[TapTapAchievementBridge] Could not read " +
                    "TapTap report ledger. Reports will be reconciled.\n" +
                    exception);
            }
        }

        private void SaveReportFile()
        {
            try
            {
                string directory =
                    Path.GetDirectoryName(reportFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string tempPath = reportFilePath + ".tmp";
                File.WriteAllText(
                    tempPath,
                    JsonUtility.ToJson(reportFile, true));
                if (File.Exists(reportFilePath))
                {
                    File.Delete(reportFilePath);
                }

                File.Move(tempPath, reportFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[TapTapAchievementBridge] Could not save " +
                    "TapTap report ledger.\n" +
                    exception);
            }
        }

        private void OnDestroy()
        {
            if (achievementManager != null)
            {
                achievementManager.AchievementUnlocked -=
                    OnAchievementUnlocked;
            }

            if (callbacksRegistered)
            {
                TapTapAchievement.UnRegisterCallBack(this);
                TapTapSDK.RemoveInitCallback(this);
                callbacksRegistered = false;
            }
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
