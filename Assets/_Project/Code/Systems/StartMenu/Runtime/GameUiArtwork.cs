using UnityEngine;

namespace Project.StartMenu
{
    public sealed class GameUiArtwork : ScriptableObject
    {
        [InspectorName("主菜单背景")] public Texture2D menuBackground;
        [InspectorName("设置页背景")] public Sprite panelBackground;
        [InspectorName("成就块背景")] public Sprite achievementBlock;
        [InspectorName("成就解锁提示块")] public Sprite achievementUnlockBlock;
        [InspectorName("成就解锁勾")] public Sprite achievementUnlockCheck;
        [InspectorName("收集品图标")] public Sprite collectibleIcon;
        [Header("设置界面")]
        [InspectorName("设置按钮")] public Sprite settingButton;
        [InspectorName("滑块背景")] public Sprite settingSliderBackground;
        [InspectorName("滑块填充")] public Sprite settingSliderFill;
        [InspectorName("滑块手柄")] public Sprite settingSliderHandle;
        [InspectorName("设置勾选框")] public Sprite settingToggleBox;
        [InspectorName("设置关闭X")] public Sprite settingCloseButton;
        [InspectorName("设置页背景")] public Sprite settingsPageBackground;
        [InspectorName("存档条目块")] public Sprite saveSlotBlock;
        [InspectorName("开始菜单内页背景")] public Sprite startMenuPanelBackground;
        [InspectorName("开始按钮")] public Sprite startButton;
        [InspectorName("设置按钮")] public Sprite settingsButton;
        [InspectorName("退出按钮")] public Sprite quitButton;
        [InspectorName("制作名单按钮")] public Sprite creditsButton;
        [Header("场景融合开始菜单")]
        [InspectorName("融合菜单按钮"), Tooltip("依次为开始游戏、制作名单、设置、退出。")] public Sprite[] worldMenuButtons;
        [InspectorName("融合菜单选中按钮")] public Sprite[] worldMenuSelectedButtons;
        [InspectorName("按钮左侧透明像素"), Tooltip("编辑器离线计算01/02不透明范围并集；运行时不读像素。")] public float[] worldMenuButtonLeftPadding;
        [InspectorName("标题序列帧")] public Sprite[] worldMenuLogoFrames;
        [Min(.02f), InspectorName("标题每帧秒数")] public float worldMenuLogoFrameSeconds = .08f;
        [InspectorName("暂停扇形"), Tooltip("依次为继续、设置、存档、收藏、返回主菜单、制作名单。")] public Sprite[] pauseIcons;
        [InspectorName("暂停扇形圆心"), Tooltip("相对于切片矩形的共同圆心，可位于矩形之外；不含底部阴影。")] public Vector2 pauseSectorPivot = new Vector2(-7f / 32f, 23f / 38f);
        [InspectorName("暂停扇形夹角")] public float pauseSectorAngle = 60f;
        [InspectorName("暂停像素缩放")] public float pauseSectorPixelScale = 9f;
        [Min(0f), InspectorName("暂停扇形径向间距")] public float pauseSectorSpacing = 64f;
        [InspectorName("能力扇区"), Tooltip("依次为红、绿、蓝。")] public Sprite[] abilitySectors;
        [InspectorName("能力高亮扇区")] public Sprite[] abilityHighlights;
        [InspectorName("能力扇区缩放轴心")] public Vector2[] abilityPivots;
        public static GameUiArtwork Load() => Resources.Load<GameUiArtwork>("UI/GameUiArtwork");
    }
}
