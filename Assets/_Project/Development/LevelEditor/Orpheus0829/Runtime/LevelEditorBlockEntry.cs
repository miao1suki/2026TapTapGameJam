using System;
using System.Collections.Generic;
using Project.ColorBlocks;
using Project.SurfaceTiles;
using UnityEngine;

namespace Project.LevelEditor
{
    public enum LevelEditorBlockMode
    {
        Custom = 0,
        Prefab = 1
    }

    [Serializable]
    public sealed class LevelEditorBlockEntry
    {
        [SerializeField] private string entryId;
        [SerializeField] private LevelEditorBlockMode mode =
            LevelEditorBlockMode.Custom;
        [SerializeField] private string displayName = "新方块";
        [SerializeField] private Color color = Color.white;
        [SerializeField] private GameObject prefab;
        [SerializeField] private GameObject customTemplate;
        [SerializeField] private string managedColorTypeId;
        [SerializeField] private SurfaceTilePalette surfaceTilePalette;
        [SerializeField, Min(.01f)] private float surfaceTileCellSize = 1f;
        [SerializeField] private bool surfaceTileTransparentBase;
        [SerializeField]
        private List<SurfaceTilePlacement> surfaceTilePlacements =
            new List<SurfaceTilePlacement>();

        public string EntryId => entryId;
        public LevelEditorBlockMode Mode => mode;
        public string DisplayName => displayName;
        public Color Color => color;
        public GameObject Prefab => prefab;
        public GameObject CustomTemplate => customTemplate;
        public string ManagedColorTypeId => managedColorTypeId;
        public bool HasValidManagedColorPrefab
        {
            get
            {
                if (string.IsNullOrEmpty(managedColorTypeId))
                {
                    return true;
                }

                ColorBlock block = prefab != null
                    ? prefab.GetComponent<ColorBlock>()
                    : null;
                return UsesPrefabDirectly && block != null &&
                       block.BaseColorTypeId == managedColorTypeId;
            }
        }
        public GameObject SourcePrefab =>
            customTemplate != null ? customTemplate : prefab;
        public bool UsesPrefabDirectly =>
            mode == LevelEditorBlockMode.Prefab;
        public SurfaceTilePalette SurfaceTilePalette => surfaceTilePalette;
        public float SurfaceTileCellSize => surfaceTileCellSize;
        public bool SurfaceTileTransparentBase => surfaceTileTransparentBase;
        public IReadOnlyList<SurfaceTilePlacement> SurfaceTilePlacements =>
            surfaceTilePlacements;
        public bool HasDecoration => surfaceTilePalette != null;

        public void EnsureEntryId()
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                entryId = Guid.NewGuid().ToString("N");
            }
        }

        public void Configure(
            string valueDisplayName,
            Color valueColor,
            GameObject valuePrefab,
            LevelEditorBlockMode valueMode = LevelEditorBlockMode.Custom)
        {
            EnsureEntryId();
            displayName = string.IsNullOrWhiteSpace(valueDisplayName)
                ? "新方块"
                : valueDisplayName;
            color = valueColor;
            if (prefab != valuePrefab && customTemplate != null)
            {
                customTemplate = null;
            }

            prefab = valuePrefab;
            mode = valueMode;
        }

        public void SetCustomTemplate(GameObject value)
        {
            EnsureEntryId();
            customTemplate = value;
        }

#if UNITY_EDITOR
        public void SetManagedColorType(string typeId)
        {
            managedColorTypeId = typeId;
        }
#endif

        public void CaptureDecoration(SurfaceTileBlock block)
        {
            if (UsesPrefabDirectly)
            {
                return;
            }

            surfaceTilePlacements.Clear();
            surfaceTilePalette = block != null ? block.Palette : null;
            surfaceTileCellSize = block != null
                ? Mathf.Max(.01f, block.CellSize)
                : 1f;
            surfaceTileTransparentBase =
                block != null && block.TransparentBase;
            if (block == null)
            {
                return;
            }

            for (int index = 0; index < block.Placements.Count; index++)
            {
                SurfaceTilePlacement placement = block.Placements[index];
                if (placement == null)
                {
                    continue;
                }

                surfaceTilePlacements.Add(new SurfaceTilePlacement(
                    placement.Face,
                    placement.Cell,
                    placement.TileId,
                    placement.QuarterTurns,
                    placement.FlipX,
                    placement.FlipY,
                    placement.Anchor,
                    placement.Layer,
                    placement.OffsetCells));
            }

        }

        public void ApplyDecoration(SurfaceTileBlock block)
        {
            if (block == null || UsesPrefabDirectly || !HasDecoration)
            {
                return;
            }

            block.Configure(surfaceTilePalette, surfaceTileCellSize);
            block.SetTransparentBase(surfaceTileTransparentBase);

            for (int faceIndex = 0; faceIndex < 6; faceIndex++)
            {
                block.ClearFace((SurfaceTileFace)faceIndex);
            }

            for (int index = 0; index < surfaceTilePlacements.Count; index++)
            {
                SurfaceTilePlacement placement = surfaceTilePlacements[index];
                block.AddTile(
                    placement.Face,
                    placement.Cell,
                    placement.TileId,
                    placement.QuarterTurns,
                    placement.FlipX,
                    placement.FlipY,
                    placement.Anchor,
                    true,
                    placement.OffsetCells);
            }
        }
    }
}
