namespace Project.Settings
{
    public static class RuntimeSettingsPolicy
    {
        // Hidden code switch. Keep false while the runtime settings UI is
        // still in preview. Set to true when ESC and menu changes should
        // affect real input, audio, and save data.
        public static bool ApplyChangesToRuntime { get; set; } = false;

        public static bool IsPreviewOnly => !ApplyChangesToRuntime;
    }
}
