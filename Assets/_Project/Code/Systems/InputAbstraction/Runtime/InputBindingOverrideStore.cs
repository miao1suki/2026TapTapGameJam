using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction
{
    public static class InputBindingOverrideStore
    {
        private const string PreferenceKey =
            "2026TapTap.InputBindings.Asset.v3";

        public static void Apply(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            string json = PlayerPrefs.GetString(
                PreferenceKey,
                string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    asset.LoadFromJson(json);
                }
                catch
                {
                    Clear();
                }
            }
        }

        public static string Load()
        {
            return PlayerPrefs.GetString(
                PreferenceKey,
                string.Empty);
        }

        public static void Save(string json)
        {
            PlayerPrefs.SetString(
                PreferenceKey,
                json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PreferenceKey);
            PlayerPrefs.Save();
        }
    }
}
