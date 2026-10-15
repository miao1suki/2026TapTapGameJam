using UnityEngine;

namespace Project.StartMenu
{
    public sealed class GameUiArtwork : ScriptableObject
    {
        [InspectorName("主菜单背景")] public Texture2D menuBackground;
        [InspectorName("设置页背景")] public Sprite panelBackground;
        [InspectorName("开始按钮")] public Sprite startButton;
        [InspectorName("设置按钮")] public Sprite settingsButton;
        [InspectorName("退出按钮")] public Sprite quitButton;
        [InspectorName("制作名单按钮")] public Sprite creditsButton;
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
