using Project.BlockFeatures;
using Project.SurfaceTiles;
using UnityEngine;

namespace Project.LevelEditor
{
    [DisallowMultipleComponent]
    public sealed class LevelEditorPlacedBlock : MonoBehaviour,
        IGridCellSizeProvider
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private Vector2Int sizeCells = Vector2Int.one;
        [SerializeField, Min(.01f)] private float cellWorldSize = 1f;
        [SerializeField] private string entryName;
        [SerializeField] private Color entryColor = Color.white;
        [SerializeField] private bool useEntryColor;
        [SerializeField] private bool hasColorData;
        [SerializeField] private bool isProp;
        [SerializeField] private string planningBoxId;
        [SerializeField] private string planningSignature;
        [SerializeField] private Vector2Int planningLocalCell;
        public string PlanningBoxId => planningBoxId;
        public string PlanningSignature => planningSignature;
        public Vector2Int PlanningLocalCell => planningLocalCell;
        public void SetPlanningSource(string id, string signature, Vector2Int localCell)
        {
            planningBoxId = id;
            planningSignature = signature;
            planningLocalCell = localCell;
        }

        public Vector2Int Cell => cell;
        public Vector2Int SizeCells => new Vector2Int(Mathf.Max(1, sizeCells.x), Mathf.Max(1, sizeCells.y));
        public float CellWorldSize => Mathf.Max(.01f, cellWorldSize);
        public bool ContainsCell(Vector2Int value) =>
            value.x >= cell.x && value.y >= cell.y &&
            value.x < cell.x + SizeCells.x && value.y < cell.y + SizeCells.y;
        public string EntryName => entryName;
        public Color EntryColor => entryColor;
        public bool HasColorData => hasColorData;
        public bool IsProp => isProp;

        public void Configure(
            Vector2Int valueCell,
            string valueEntryName,
            Color valueColor,
            bool valueUseEntryColor,
            bool valueIsProp = false,
            float valueCellWorldSize = 1f)
        {
            cell = valueCell;
            entryName = valueEntryName;
            entryColor = valueColor;
            useEntryColor = valueUseEntryColor;
            hasColorData = true;
            isProp = valueIsProp;
            sizeCells = Vector2Int.one;
            SetCellWorldSize(valueCellWorldSize);
            ApplyEntryColor();
        }

        public void SetCellWorldSize(float value)
        {
            cellWorldSize = Mathf.Max(.01f, value);
        }

        public void SetSizeCells(Vector2Int value)
        {
            sizeCells = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
        }

        public void ApplyEntryColor()
        {
            if (!useEntryColor)
            {
                return;
            }

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            SurfaceTileBlock surfaceBlock =
                GetComponent<SurfaceTileBlock>();
            Renderer sourceRenderer = surfaceBlock != null
                ? surfaceBlock.SourceRenderer
                : null;
            if (sourceRenderer != null)
            {
                sourceRenderer.GetPropertyBlock(properties);
                properties.SetColor("_Color", entryColor);
                properties.SetColor("_BaseColor", entryColor);
                sourceRenderer.SetPropertyBlock(properties);
                return;
            }

            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer target = renderers[index];
                target.GetPropertyBlock(properties);
                properties.SetColor("_Color", entryColor);
                properties.SetColor("_BaseColor", entryColor);
                target.SetPropertyBlock(properties);
            }
        }

        private void OnEnable()
        {
            ApplyEntryColor();
        }
    }
}
