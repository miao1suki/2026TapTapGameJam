using UnityEngine;

namespace Project.InputAbstraction
{
    public enum InputPlatformMode
    {
        Automatic = 0,
        Desktop = 1,
        Mobile = 2,
    }

    public static class InputPlatformResolver
    {
        public static InputPlatformMode Current
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return InputPlatformMode.Mobile;
#else
                return InputPlatformMode.Desktop;
#endif
            }
        }

        public static InputPlatformMode Resolve(InputPlatformMode configuredMode)
        {
            return configuredMode == InputPlatformMode.Automatic
                ? Current
                : configuredMode;
        }

        public static InputPlatformMode FromRuntimePlatform(RuntimePlatform platform)
        {
            return platform == RuntimePlatform.Android ||
                   platform == RuntimePlatform.IPhonePlayer
                ? InputPlatformMode.Mobile
                : InputPlatformMode.Desktop;
        }
    }
}
