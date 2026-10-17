using UnityEngine;

namespace Project.InputAbstraction
{
    /// <summary>One cursor policy for desktop menus, pause and directly opened levels.</summary>
    public static class DesktopCursorPolicy
    {
        public static CursorLockMode ResolveLockMode(bool standaloneDesktop, bool focused) =>
            standaloneDesktop && focused ? CursorLockMode.Confined : CursorLockMode.None;

        public static void Apply(bool visible) => Apply(visible, Application.isFocused);

        public static void Apply(bool visible, bool focused)
        {
            // Do not trap the author's mouse inside the Unity Game view, or
            // interfere with mobile touches. Confined preserves absolute aiming.
            bool desktop = !Application.isEditor &&
                (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.LinuxPlayer);
            CursorLockMode mode = ResolveLockMode(desktop, focused);
            if (Cursor.visible != visible) Cursor.visible = visible;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
        }
    }
}
