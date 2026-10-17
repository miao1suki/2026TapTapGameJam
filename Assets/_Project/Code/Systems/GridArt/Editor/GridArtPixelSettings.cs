using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    internal static class GridArtPixelSettings
    {
        internal static VisualElement Build(GridArtBrush brush, Action<Action> edit, Action refresh)
        {
            var root = new VisualElement();
            root.Add(GridArtColorField.Build(brush.colorId, false, value => edit(() => brush.colorId = value)));
            var current = new Vector2Field("当前显示（格）") { value = brush.DisplaySize };
            current.SetEnabled(false); root.Add(current);
            if (!brush.autoTerrain && brush.sprite != null)
            {
                Vector2 pixels = GridArtPixelLayout.SourcePixels(brush.sprite, brush.contentRect);
                int sourceScale = GridArtPixelLayout.SourceScale(brush.sprite, brush.sourcePixelScale);
                root.Add(new Label($"源选区：{pixels.x:0.##}×{pixels.y:0.##} 像素 · 整数导出倍率 {sourceScale}×"));
                var natural = new Vector2Field("原像素尺寸（格）") { value = GridArtPixelLayout.NativeSize(brush.sprite, brush.contentRect, sourceScale) };
                natural.SetEnabled(false); root.Add(natural);
            }
            var scale = new IntegerField("原图整数导出倍率") { value = Mathf.Max(1, brush.sourcePixelScale), isDelayed = true };
            scale.tooltip = "只用于美术整倍最近邻导出的图片；不是任意缩放。tail的4倍导出会自动识别。";
            scale.RegisterValueChangedCallback(e => { edit(() => { brush.sourcePixelScale = Mathf.Clamp(e.newValue,1,16); GridArtPixelLayout.RestoreNative(brush,out _); }); refresh(); }); root.Add(scale);
            if (!GridArtPixelLayout.IsNative(brush))
            {
                root.Add(new HelpBox(brush.autoTerrain ? GridArtPixelLayout.TerrainIssue(brush) :
                    "此素材保留旧版显示比例。恢复原像素会影响共享此素材的美术图，请确认后操作。", HelpBoxMessageType.Warning));
                var feedback = new Label(); feedback.style.whiteSpace = WhiteSpace.Normal;
                var restore = new Button(() =>
                {
                    string error = null;
                    edit(() => GridArtPixelLayout.RestoreNative(brush, out error));
                    if (error != null) feedback.text = error;
                    else refresh();
                }) { text = "恢复原像素比例" };
                restore.SetEnabled(!brush.autoTerrain || GridArtPixelLayout.TerrainIssue(brush) == null);
                root.Add(restore); root.Add(feedback);
            }
            else root.Add(new Label("显示比例已遵循原像素；占格仅控制排布。"));
            return root;
        }
    }
}
