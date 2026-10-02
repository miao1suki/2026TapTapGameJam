using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.SurfaceTiles
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SurfaceTileBlock : MonoBehaviour
    {
        public const float MaximumOffsetCells = 2f;

        [SerializeField] private SurfaceTilePalette palette;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField] private List<SurfaceTilePlacement> placements =
            new List<SurfaceTilePlacement>();
        [SerializeField, HideInInspector] private string blockId;
        [SerializeField, HideInInspector] private MeshFilter outputFilter;
        [SerializeField, HideInInspector] private MeshRenderer outputRenderer;
        [SerializeField] private bool transparentBase;
        [SerializeField, HideInInspector] private Renderer sourceRenderer;
        [SerializeField, HideInInspector] private bool sourceRendererStateCaptured;
        [SerializeField, HideInInspector] private bool sourceRendererWasEnabled = true;
        [SerializeField, HideInInspector] private Mesh bakedMesh;
        [SerializeField, HideInInspector] private Material bakedMaterial;
        [SerializeField, HideInInspector] private bool bakeUpToDate;
        [SerializeField, HideInInspector] private Vector3 bakedColliderCenter;
        [SerializeField, HideInInspector] private Vector3 bakedColliderSize;
        [SerializeField, HideInInspector] private Vector3 bakedLossyScale;
        [SerializeField, HideInInspector] private float bakedCellSize;

        public SurfaceTilePalette Palette => palette;
        public float CellSize => cellSize;
        public IReadOnlyList<SurfaceTilePlacement> Placements => placements;
        public string BlockId => blockId;
        public MeshFilter OutputFilter => outputFilter;
        public MeshRenderer OutputRenderer => outputRenderer;
        public bool TransparentBase => transparentBase;
        public Renderer SourceRenderer => FindSourceRenderer();
        public Mesh BakedMesh => bakedMesh;
        public Material BakedMaterial => bakedMaterial;
        public bool BakeUpToDate =>
            bakeUpToDate &&
            bakedMesh != null &&
            bakedMaterial != null &&
            GeometryMatchesBake();
        public BoxCollider SurfaceCollider => GetComponent<BoxCollider>();

        public Vector2Int GetGridSize(SurfaceTileFace face)
        {
            return SurfaceTileGeometry.GetGridSize(
                transform,
                SurfaceCollider,
                face,
                cellSize);
        }

        public bool TryGetPlacement(
            SurfaceTileFace face,
            Vector2Int cell,
            out SurfaceTilePlacement placement)
        {
            placement = null;
            int bestLayer = int.MinValue;
            for (int index = 0; index < placements.Count; index++)
            {
                SurfaceTilePlacement candidate = placements[index];
                if (candidate.Face != face ||
                    candidate.Layer < bestLayer ||
                    !TryGetPlacementRect(candidate, out Rect rect) ||
                    !SurfaceTileGeometry.PlacementCoversCell(rect, cell))
                {
                    continue;
                }

                placement = candidate;
                bestLayer = candidate.Layer;
            }

            return placement != null;
        }

        public void SetTile(
            SurfaceTileFace face,
            Vector2Int cell,
            string tileId,
            int quarterTurns,
            bool flipX,
            bool flipY)
        {
            if (string.IsNullOrWhiteSpace(tileId))
            {
                RemoveTile(face, cell);
                return;
            }

            if (TryGetOriginPlacement(face, cell, out SurfaceTilePlacement placement))
            {
                placement.SetTile(tileId, quarterTurns, flipX, flipY);
            }
            else
            {
                placements.Add(new SurfaceTilePlacement(
                    face,
                    cell,
                    tileId,
                    quarterTurns,
                    flipX,
                    flipY));
            }

            bakeUpToDate = false;
        }

        public bool AddTile(
            SurfaceTileFace face,
            Vector2Int cell,
            string tileId,
            int quarterTurns,
            bool flipX,
            bool flipY,
            SurfaceTileAnchor anchor,
            bool stack)
        {
            return AddTile(
                face,
                cell,
                tileId,
                quarterTurns,
                flipX,
                flipY,
                anchor,
                stack,
                Vector2.zero);
        }

        public bool AddTile(
            SurfaceTileFace face,
            Vector2Int cell,
            string tileId,
            int quarterTurns,
            bool flipX,
            bool flipY,
            SurfaceTileAnchor anchor,
            bool stack,
            Vector2 offsetCells)
        {
            if (palette == null ||
                string.IsNullOrWhiteSpace(tileId) ||
                !palette.TryGet(tileId, out SurfaceTilePalette.Entry entry) ||
                !IsValidOffset(offsetCells))
            {
                return false;
            }

            Rect baseRect = SurfaceTileGeometry.GetPlacementRect(
                entry,
                cell,
                quarterTurns,
                anchor);
            if (!SurfaceTileGeometry.PlacementFitsGrid(
                    baseRect,
                    GetGridSize(face)))
            {
                return false;
            }

            Rect targetRect = SurfaceTileGeometry.GetPlacementRect(
                entry,
                cell,
                quarterTurns,
                anchor,
                offsetCells);

            int nextLayer = 0;
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                SurfaceTilePlacement candidate = placements[index];
                if (candidate.Face != face ||
                    !TryGetPlacementRect(candidate, out Rect candidateRect) ||
                    !RectsOverlap(candidateRect, targetRect))
                {
                    continue;
                }

                if (stack &&
                    candidate.Cell == cell &&
                    candidate.TileId == tileId &&
                    candidate.QuarterTurns == Mathf.Abs(quarterTurns) % 4 &&
                    candidate.FlipX == flipX &&
                    candidate.FlipY == flipY &&
                    candidate.Anchor == anchor &&
                    candidate.OffsetCells == offsetCells)
                {
                    return true;
                }

                if (stack)
                {
                    nextLayer = Mathf.Max(nextLayer, candidate.Layer + 1);
                }
                else
                {
                    placements.RemoveAt(index);
                }
            }

            placements.Add(new SurfaceTilePlacement(
                face,
                cell,
                tileId,
                quarterTurns,
                flipX,
                flipY,
                anchor,
                nextLayer,
                offsetCells));
            bakeUpToDate = false;
            return true;
        }

        public bool RemoveTile(SurfaceTileFace face, Vector2Int cell)
        {
            int removeIndex = -1;
            int bestLayer = int.MinValue;
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                SurfaceTilePlacement placement = placements[index];
                if (placement.Face == face &&
                    placement.Layer > bestLayer &&
                    TryGetPlacementRect(placement, out Rect rect) &&
                    SurfaceTileGeometry.PlacementCoversCell(rect, cell))
                {
                    removeIndex = index;
                    bestLayer = placement.Layer;
                }
            }

            if (removeIndex < 0)
            {
                return false;
            }

            placements.RemoveAt(removeIndex);
            bakeUpToDate = false;
            return true;
        }

        public int ClearFace(SurfaceTileFace face)
        {
            int removed = 0;
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                if (placements[index].Face == face)
                {
                    placements.RemoveAt(index);
                    removed++;
                }
            }

            if (removed > 0)
            {
                bakeUpToDate = false;
            }

            return removed;
        }

        public int RemoveOutOfBoundsTiles()
        {
            int removed = 0;
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                SurfaceTilePlacement placement = placements[index];
                Vector2Int size = GetGridSize(placement.Face);
                if (palette == null ||
                    !palette.TryGet(
                        placement.TileId,
                        out SurfaceTilePalette.Entry entry) ||
                    !IsValidOffset(placement.OffsetCells) ||
                    !SurfaceTileGeometry.PlacementFitsGrid(
                        SurfaceTileGeometry.GetPlacementRect(
                            entry,
                            placement.Cell,
                            placement.QuarterTurns,
                            placement.Anchor),
                        size))
                {
                    placements.RemoveAt(index);
                    removed++;
                }
            }

            if (removed > 0)
            {
                bakeUpToDate = false;
            }

            return removed;
        }

        public void Configure(SurfaceTilePalette valuePalette, float valueCellSize)
        {
            palette = valuePalette;
            cellSize = Mathf.Max(0.01f, valueCellSize);
            bakeUpToDate = false;
        }

        public void BindOutput(MeshFilter filter, MeshRenderer renderer)
        {
            outputFilter = filter;
            outputRenderer = renderer;
            EnsureBaseVisibility();
        }

        public void SetTransparentBase(bool value)
        {
            CaptureSourceRenderer();
            transparentBase = value;
            ApplyBaseVisibility();
        }

        public void EnsureBaseVisibility()
        {
            CaptureSourceRenderer();
            ApplyBaseVisibility();
        }

        public void BindBake(Mesh mesh, Material material)
        {
            bakedMesh = mesh;
            bakedMaterial = material;
            bakeUpToDate = mesh != null && material != null;
            BoxCollider collider = SurfaceCollider;
            bakedColliderCenter = collider != null ? collider.center : Vector3.zero;
            bakedColliderSize = collider != null ? collider.size : Vector3.one;
            bakedLossyScale = transform.lossyScale;
            bakedCellSize = cellSize;
            if (outputFilter != null)
            {
                outputFilter.sharedMesh = mesh;
            }

            if (outputRenderer != null)
            {
                outputRenderer.sharedMaterial = material;
            }
        }

        public void InvalidateBake()
        {
            bakeUpToDate = false;
        }

        public void EnsureBlockId()
        {
            if (string.IsNullOrWhiteSpace(blockId))
            {
                blockId = Guid.NewGuid().ToString("N");
            }
        }

        public void RegenerateBlockId()
        {
            blockId = Guid.NewGuid().ToString("N");
            bakeUpToDate = false;
        }

        private void Reset()
        {
            EnsureBlockId();
            EnsureBaseVisibility();
        }

        private void OnEnable()
        {
            EnsureBaseVisibility();
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(0.01f, cellSize);
            EnsureBlockId();
            EnsureBaseVisibility();
        }

        private bool GeometryMatchesBake()
        {
            BoxCollider collider = SurfaceCollider;
            return collider != null &&
                   Approximately(collider.center, bakedColliderCenter) &&
                   Approximately(collider.size, bakedColliderSize) &&
                   Approximately(transform.lossyScale, bakedLossyScale) &&
                   Mathf.Approximately(cellSize, bakedCellSize);
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude <= 0.000001f;
        }

        private bool TryGetOriginPlacement(
            SurfaceTileFace face,
            Vector2Int cell,
            out SurfaceTilePlacement placement)
        {
            placement = null;
            int bestLayer = int.MinValue;
            for (int index = 0; index < placements.Count; index++)
            {
                SurfaceTilePlacement candidate = placements[index];
                if (candidate.Face == face && candidate.Cell == cell &&
                    candidate.Layer >= bestLayer)
                {
                    placement = candidate;
                    bestLayer = candidate.Layer;
                }
            }

            return placement != null;
        }

        private bool TryGetPlacementRect(
            SurfaceTilePlacement placement,
            out Rect rect)
        {
            if (placement != null && palette != null &&
                palette.TryGet(
                    placement.TileId,
                    out SurfaceTilePalette.Entry entry))
            {
                rect = SurfaceTileGeometry.GetPlacementRect(entry, placement);
                return true;
            }

            rect = default;
            return false;
        }

        private static bool RectsOverlap(Rect left, Rect right)
        {
            return left.xMin < right.xMax - 0.0001f &&
                   left.xMax > right.xMin + 0.0001f &&
                   left.yMin < right.yMax - 0.0001f &&
                   left.yMax > right.yMin + 0.0001f;
        }

        private static bool IsValidOffset(Vector2 offsetCells)
        {
            return !float.IsNaN(offsetCells.x) &&
                   !float.IsInfinity(offsetCells.x) &&
                   !float.IsNaN(offsetCells.y) &&
                   !float.IsInfinity(offsetCells.y) &&
                   Mathf.Abs(offsetCells.x) <= MaximumOffsetCells &&
                   Mathf.Abs(offsetCells.y) <= MaximumOffsetCells;
        }

        private Renderer FindSourceRenderer()
        {
            if (sourceRenderer != null && sourceRenderer != outputRenderer)
            {
                return sourceRenderer;
            }

            Renderer candidate = GetComponent<Renderer>();
            if (candidate != null && candidate != outputRenderer)
            {
                return candidate;
            }

            Renderer[] children = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < children.Length; index++)
            {
                if (children[index] != null && children[index] != outputRenderer)
                {
                    return children[index];
                }
            }

            return null;
        }

        private void CaptureSourceRenderer()
        {
            Renderer candidate = FindSourceRenderer();
            if (candidate == null)
            {
                return;
            }

            if (sourceRenderer != candidate)
            {
                sourceRenderer = candidate;
                sourceRendererStateCaptured = false;
            }

            if (!sourceRendererStateCaptured)
            {
                sourceRendererWasEnabled = candidate.enabled;
                sourceRendererStateCaptured = true;
            }
        }

        private void ApplyBaseVisibility()
        {
            if (sourceRenderer == null || !sourceRendererStateCaptured)
            {
                return;
            }

            sourceRenderer.enabled = transparentBase
                ? false
                : sourceRendererWasEnabled;
        }
    }
}
