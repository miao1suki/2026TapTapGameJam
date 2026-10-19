using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public enum GridArtEffectKind { Rain, Fireflies }

    [Serializable]
    public sealed class GridArtEffectSettings
    {
        public GridArtEffectKind kind;
        public int rainColor = 1;
        public float rainDensity = 18f;
        public int rainSeed = 37;
        public float rainDepth = 1f;
        public int fireflyCount = 22;
        public int layoutSeed = 107, motionSeed = 293, pulseSeed = 521;
        public float fireflyDepthCells = -.25f;
    }

    public readonly struct GridArtEffectRegion
    {
        public readonly Component target;
        public readonly GridArtEffectKind kind;
        public readonly Rect worldBounds;
        public readonly string label;
        public readonly Color tint;
        public GridArtEffectRegion(Component value, GridArtEffectKind type, Rect bounds, string name, Color color)
        { target = value; kind = type; worldBounds = bounds; label = name; tint = color; }
    }

    /// <summary>Assembly-CSharp-Editor installs the TA adapter. The workbench never depends on gameplay assemblies.</summary>
    public static class GridArtEffectsSource
    {
        public static Func<Scene, IReadOnlyList<GridArtEffectRegion>> Read;
        public static Func<GridArtEffectSettings, RectInt, float, Component> Create;
        public static Func<Component, Action, VisualElement> BuildInspector;
        public static event Action Changed;
        public static bool Available => Read != null && Create != null && BuildInspector != null;
        public static void NotifyChanged() => Changed?.Invoke();

        public static Rect CellBounds(RectInt cells, float cellSize) => new Rect(
            cells.x * cellSize, cells.y * cellSize, cells.width * cellSize, cells.height * cellSize);
    }
}
