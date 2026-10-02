using System;
using UnityEngine;

namespace Project.SurfaceTiles
{
    public enum SurfaceTileFace
    {
        Front = 0,
        Right = 1,
        Back = 2,
        Left = 3,
        Top = 4,
        Bottom = 5
    }

    public enum SurfaceTileAnchor
    {
        BottomLeft = 0,
        Center = 1,
        BottomEdge = 2,
        TopEdge = 3,
        LeftEdge = 4,
        RightEdge = 5
    }

    [Serializable]
    public sealed class SurfaceTilePlacement
    {
        [SerializeField] private SurfaceTileFace face;
        [SerializeField] private Vector2Int cell;
        [SerializeField] private string tileId;
        [SerializeField, Range(0, 3)] private int quarterTurns;
        [SerializeField] private bool flipX;
        [SerializeField] private bool flipY;
        [SerializeField] private SurfaceTileAnchor anchor;
        [SerializeField] private int layer;
        [SerializeField] private Vector2 offsetCells;

        public SurfaceTileFace Face => face;
        public Vector2Int Cell => cell;
        public string TileId => tileId;
        public int QuarterTurns => quarterTurns;
        public bool FlipX => flipX;
        public bool FlipY => flipY;
        public SurfaceTileAnchor Anchor => anchor;
        public int Layer => layer;
        public Vector2 OffsetCells => offsetCells;

        public SurfaceTilePlacement(
            SurfaceTileFace valueFace,
            Vector2Int valueCell,
            string valueTileId,
            int valueQuarterTurns,
            bool valueFlipX,
            bool valueFlipY)
            : this(
                valueFace,
                valueCell,
                valueTileId,
                valueQuarterTurns,
                valueFlipX,
                valueFlipY,
                SurfaceTileAnchor.BottomLeft,
                0,
                Vector2.zero)
        {
        }

        public SurfaceTilePlacement(
            SurfaceTileFace valueFace,
            Vector2Int valueCell,
            string valueTileId,
            int valueQuarterTurns,
            bool valueFlipX,
            bool valueFlipY,
            SurfaceTileAnchor valueAnchor,
            int valueLayer)
            : this(
                valueFace,
                valueCell,
                valueTileId,
                valueQuarterTurns,
                valueFlipX,
                valueFlipY,
                valueAnchor,
                valueLayer,
                Vector2.zero)
        {
        }

        public SurfaceTilePlacement(
            SurfaceTileFace valueFace,
            Vector2Int valueCell,
            string valueTileId,
            int valueQuarterTurns,
            bool valueFlipX,
            bool valueFlipY,
            SurfaceTileAnchor valueAnchor,
            int valueLayer,
            Vector2 valueOffsetCells)
        {
            face = valueFace;
            cell = valueCell;
            tileId = valueTileId;
            quarterTurns = Mathf.Abs(valueQuarterTurns) % 4;
            flipX = valueFlipX;
            flipY = valueFlipY;
            anchor = valueAnchor;
            layer = Mathf.Max(0, valueLayer);
            offsetCells = valueOffsetCells;
        }

        public void SetTile(
            string valueTileId,
            int valueQuarterTurns,
            bool valueFlipX,
            bool valueFlipY)
        {
            tileId = valueTileId;
            quarterTurns = Mathf.Abs(valueQuarterTurns) % 4;
            flipX = valueFlipX;
            flipY = valueFlipY;
        }

        public void SetPlacement(
            string valueTileId,
            int valueQuarterTurns,
            bool valueFlipX,
            bool valueFlipY,
            SurfaceTileAnchor valueAnchor,
            int valueLayer)
        {
            SetPlacement(
                valueTileId,
                valueQuarterTurns,
                valueFlipX,
                valueFlipY,
                valueAnchor,
                valueLayer,
                Vector2.zero);
        }

        public void SetPlacement(
            string valueTileId,
            int valueQuarterTurns,
            bool valueFlipX,
            bool valueFlipY,
            SurfaceTileAnchor valueAnchor,
            int valueLayer,
            Vector2 valueOffsetCells)
        {
            SetTile(
                valueTileId,
                valueQuarterTurns,
                valueFlipX,
                valueFlipY);
            anchor = valueAnchor;
            layer = Mathf.Max(0, valueLayer);
            offsetCells = valueOffsetCells;
        }
    }

    public readonly struct SurfaceTileFaceBasis
    {
        public SurfaceTileFaceBasis(
            Vector3 center,
            Vector3 normal,
            Vector3 axisU,
            Vector3 axisV,
            float width,
            float height)
        {
            Center = center;
            Normal = normal;
            AxisU = axisU;
            AxisV = axisV;
            Width = width;
            Height = height;
        }

        public Vector3 Center { get; }
        public Vector3 Normal { get; }
        public Vector3 AxisU { get; }
        public Vector3 AxisV { get; }
        public float Width { get; }
        public float Height { get; }

        public Vector3 Point(float normalizedU, float normalizedV, float bias = 0f)
        {
            return Center +
                   AxisU * ((normalizedU - 0.5f) * Width) +
                   AxisV * ((normalizedV - 0.5f) * Height) +
                   Normal * bias;
        }
    }

    public static class SurfaceTileGeometry
    {
        public static Vector2 GetRotatedSize(
            SurfaceTilePalette.Entry entry,
            int quarterTurns)
        {
            Vector2 size = entry != null ? entry.SizeInCells : Vector2.one;
            return Mathf.Abs(quarterTurns) % 2 == 0
                ? size
                : new Vector2(size.y, size.x);
        }

        public static Rect GetPlacementRect(
            SurfaceTilePalette.Entry entry,
            Vector2Int cell,
            int quarterTurns,
            SurfaceTileAnchor anchor)
        {
            return GetPlacementRect(
                entry,
                cell,
                quarterTurns,
                anchor,
                Vector2.zero);
        }

        public static Rect GetPlacementRect(
            SurfaceTilePalette.Entry entry,
            Vector2Int cell,
            int quarterTurns,
            SurfaceTileAnchor anchor,
            Vector2 offsetCells)
        {
            Vector2 size = GetRotatedSize(entry, quarterTurns);
            Vector2 origin;
            switch (anchor)
            {
                case SurfaceTileAnchor.Center:
                    origin = new Vector2(
                        cell.x + 0.5f - size.x * 0.5f,
                        cell.y + 0.5f - size.y * 0.5f);
                    break;
                case SurfaceTileAnchor.BottomEdge:
                    origin = new Vector2(
                        cell.x,
                        cell.y);
                    break;
                case SurfaceTileAnchor.TopEdge:
                    origin = new Vector2(
                        cell.x,
                        cell.y + 1f - size.y);
                    break;
                case SurfaceTileAnchor.LeftEdge:
                    origin = new Vector2(
                        cell.x,
                        cell.y);
                    break;
                case SurfaceTileAnchor.RightEdge:
                    origin = new Vector2(
                        cell.x + 1f - size.x,
                        cell.y);
                    break;
                default:
                    origin = cell;
                    break;
            }

            return new Rect(origin + offsetCells, size);
        }

        public static Rect GetPlacementRect(
            SurfaceTilePalette.Entry entry,
            SurfaceTilePlacement placement)
        {
            return placement == null
                ? default
                : GetPlacementRect(
                    entry,
                    placement.Cell,
                    placement.QuarterTurns,
                    placement.Anchor,
                    placement.OffsetCells);
        }

        public static bool PlacementCoversCell(Rect placementRect, Vector2Int cell)
        {
            Rect cellRect = new Rect(cell.x, cell.y, 1f, 1f);
            return placementRect.xMin < cellRect.xMax - 0.0001f &&
                   placementRect.xMax > cellRect.xMin + 0.0001f &&
                   placementRect.yMin < cellRect.yMax - 0.0001f &&
                   placementRect.yMax > cellRect.yMin + 0.0001f;
        }

        public static bool PlacementFitsGrid(Rect placementRect, Vector2Int grid)
        {
            return placementRect.xMin >= -0.0001f &&
                   placementRect.yMin >= -0.0001f &&
                   placementRect.xMax <= grid.x + 0.0001f &&
                   placementRect.yMax <= grid.y + 0.0001f;
        }

        public static SurfaceTileFace FaceFromLocalNormal(Vector3 normal)
        {
            Vector3 absolute = new Vector3(
                Mathf.Abs(normal.x),
                Mathf.Abs(normal.y),
                Mathf.Abs(normal.z));
            if (absolute.y >= absolute.x && absolute.y >= absolute.z)
            {
                return normal.y >= 0f
                    ? SurfaceTileFace.Top
                    : SurfaceTileFace.Bottom;
            }

            if (absolute.x >= absolute.z)
            {
                return normal.x >= 0f
                    ? SurfaceTileFace.Left
                    : SurfaceTileFace.Right;
            }

            return normal.z >= 0f
                ? SurfaceTileFace.Back
                : SurfaceTileFace.Front;
        }

        public static SurfaceTileFaceBasis GetLocalBasis(
            BoxCollider collider,
            SurfaceTileFace face)
        {
            Vector3 center = collider != null ? collider.center : Vector3.zero;
            Vector3 size = collider != null ? collider.size : Vector3.one;
            switch (face)
            {
                case SurfaceTileFace.Right:
                    return new SurfaceTileFaceBasis(
                        center + Vector3.left * size.x * 0.5f,
                        Vector3.left,
                        Vector3.back,
                        Vector3.up,
                        size.z,
                        size.y);
                case SurfaceTileFace.Back:
                    return new SurfaceTileFaceBasis(
                        center + Vector3.forward * size.z * 0.5f,
                        Vector3.forward,
                        Vector3.left,
                        Vector3.up,
                        size.x,
                        size.y);
                case SurfaceTileFace.Left:
                    return new SurfaceTileFaceBasis(
                        center + Vector3.right * size.x * 0.5f,
                        Vector3.right,
                        Vector3.forward,
                        Vector3.up,
                        size.z,
                        size.y);
                case SurfaceTileFace.Top:
                    return new SurfaceTileFaceBasis(
                        center + Vector3.up * size.y * 0.5f,
                        Vector3.up,
                        Vector3.right,
                        Vector3.forward,
                        size.x,
                        size.z);
                case SurfaceTileFace.Bottom:
                    return new SurfaceTileFaceBasis(
                        center + Vector3.down * size.y * 0.5f,
                        Vector3.down,
                        Vector3.right,
                        Vector3.back,
                        size.x,
                        size.z);
                default:
                    return new SurfaceTileFaceBasis(
                        center + Vector3.back * size.z * 0.5f,
                        Vector3.back,
                        Vector3.right,
                        Vector3.up,
                        size.x,
                        size.y);
            }
        }

        public static Vector2Int GetGridSize(
            Transform transform,
            BoxCollider collider,
            SurfaceTileFace face,
            float worldCellSize)
        {
            SurfaceTileFaceBasis basis = GetLocalBasis(collider, face);
            Vector3 scale = transform != null
                ? transform.lossyScale
                : Vector3.one;
            float scaleU = AxisScale(basis.AxisU, scale);
            float scaleV = AxisScale(basis.AxisV, scale);
            float safeCellSize = Mathf.Max(0.01f, worldCellSize);
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(basis.Width * scaleU / safeCellSize)),
                Mathf.Max(1, Mathf.RoundToInt(basis.Height * scaleV / safeCellSize)));
        }

        public static bool TryGetCell(
            BoxCollider collider,
            SurfaceTileFace face,
            Vector3 localPoint,
            Vector2Int gridSize,
            out Vector2Int cell)
        {
            SurfaceTileFaceBasis basis = GetLocalBasis(collider, face);
            Vector3 origin = basis.Point(0f, 0f);
            float normalizedU = Vector3.Dot(localPoint - origin, basis.AxisU) /
                                Mathf.Max(0.0001f, basis.Width);
            float normalizedV = Vector3.Dot(localPoint - origin, basis.AxisV) /
                                Mathf.Max(0.0001f, basis.Height);
            if (normalizedU < -0.001f || normalizedU > 1.001f ||
                normalizedV < -0.001f || normalizedV > 1.001f)
            {
                cell = default;
                return false;
            }

            cell = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(normalizedU * gridSize.x), 0, gridSize.x - 1),
                Mathf.Clamp(Mathf.FloorToInt(normalizedV * gridSize.y), 0, gridSize.y - 1));
            return true;
        }

        private static float AxisScale(Vector3 axis, Vector3 scale)
        {
            return Mathf.Abs(axis.x) * Mathf.Abs(scale.x) +
                   Mathf.Abs(axis.y) * Mathf.Abs(scale.y) +
                   Mathf.Abs(axis.z) * Mathf.Abs(scale.z);
        }
    }
}
