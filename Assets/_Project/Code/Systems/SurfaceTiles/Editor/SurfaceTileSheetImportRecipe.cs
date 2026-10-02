using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.SurfaceTiles.Editor
{
    internal enum SurfaceTileOutputAnchor
    {
        Center = 0,
        BottomCenter = 1
    }

    [Serializable]
    internal sealed class SurfaceTileSourceRegion
    {
        [SerializeField] private string displayName = "Tile";
        [SerializeField] private RectInt rect;
        [SerializeField] private bool enabled = true;

        internal string DisplayName
        {
            get => displayName;
            set => displayName = string.IsNullOrWhiteSpace(value)
                ? "Tile"
                : value.Trim();
        }

        internal RectInt Rect
        {
            get => rect;
            set => rect = value;
        }

        internal bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        internal SurfaceTileSourceRegion(string name, RectInt valueRect)
        {
            displayName = name;
            rect = valueRect;
        }
    }

    internal sealed class SurfaceTileSheetImportRecipe : ScriptableObject
    {
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private string outputFolder =
            "Assets/_Project/Art/Generated/TilePalettes";
        [SerializeField] private string outputName = "SurfaceTiles";
        [SerializeField, Min(8)] private int outputWidth = 128;
        [SerializeField, Min(8)] private int outputHeight = 128;
        [SerializeField, Min(1)] private int sourcePixelsPerCell = 64;
        [SerializeField, Min(0)] private int transparentPadding = 4;
        [SerializeField] private bool trimTransparentPixels = true;
        [SerializeField] private bool allowUpscale;
        [SerializeField] private SurfaceTileOutputAnchor anchor =
            SurfaceTileOutputAnchor.Center;
        [SerializeField] private List<SurfaceTileSourceRegion> regions =
            new List<SurfaceTileSourceRegion>();

        internal Texture2D SourceTexture => sourceTexture;
        internal string OutputFolder => outputFolder;
        internal string OutputName => outputName;
        internal int OutputWidth => outputWidth;
        internal int OutputHeight => outputHeight;
        internal int SourcePixelsPerCell => sourcePixelsPerCell > 0
            ? sourcePixelsPerCell
            : 64;
        internal int TransparentPadding => transparentPadding;
        internal bool TrimTransparentPixels => trimTransparentPixels;
        internal bool AllowUpscale => allowUpscale;
        internal SurfaceTileOutputAnchor Anchor => anchor;
        internal List<SurfaceTileSourceRegion> Regions => regions;

        internal void ConfigureSource(Texture2D value)
        {
            sourceTexture = value;
            if (value != null &&
                (string.IsNullOrWhiteSpace(outputName) || outputName == "SurfaceTiles"))
            {
                outputName = value.name + "_SurfaceTiles";
            }
        }

        internal void ConfigureOutput(
            string folder,
            string name,
            int width,
            int height,
            int sourceCellPixels,
            int padding,
            bool trim,
            bool upscale,
            SurfaceTileOutputAnchor valueAnchor)
        {
            outputFolder = string.IsNullOrWhiteSpace(folder)
                ? "Assets/_Project/Art/Generated/TilePalettes"
                : folder.Replace('\\', '/').TrimEnd('/');
            outputName = string.IsNullOrWhiteSpace(name)
                ? "SurfaceTiles"
                : name.Trim();
            outputWidth = Mathf.Max(8, width);
            outputHeight = Mathf.Max(8, height);
            sourcePixelsPerCell = Mathf.Max(1, sourceCellPixels);
            transparentPadding = Mathf.Clamp(
                padding,
                0,
                Mathf.Min(outputWidth, outputHeight) / 2 - 1);
            trimTransparentPixels = trim;
            allowUpscale = upscale;
            anchor = valueAnchor;
        }
    }
}
