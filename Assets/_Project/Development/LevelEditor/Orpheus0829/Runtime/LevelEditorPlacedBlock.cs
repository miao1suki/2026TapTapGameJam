using System.Collections.Generic;
using Project.BlockFeatures;
using Project.SurfaceTiles;
using UnityEngine;

namespace Project.LevelEditor
{
    [DisallowMultipleComponent]
    public sealed class LevelEditorPlacedBlock : MonoBehaviour,
        IGridCellSizeProvider
    {
        private static readonly List<LevelEditorPlacedBlock> activeInstances = new List<LevelEditorPlacedBlock>();
        private static readonly Dictionary<LevelEditorPlacedBlock, int> activeIndices = new Dictionary<LevelEditorPlacedBlock, int>();
        public static IReadOnlyList<LevelEditorPlacedBlock> ActiveInstances => activeInstances;
        public static int ActiveRevision { get; private set; }
        /// <summary>Wall topology only; ordinary prop/reaction lifetimes do not invalidate water meshes.</summary>
        public static int SolidRevision { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            activeInstances.Clear();
            activeIndices.Clear();
            ActiveRevision++;
            SolidRevision++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SeedRegistry()
        {
            // Also supports Enter Play Mode with both reload options disabled.
            foreach (LevelEditorPlacedBlock block in ProjectDiscovery.FindAll<LevelEditorPlacedBlock>())
                if (block.isActiveAndEnabled) block.RegisterActive();
        }

        private void RegisterActive()
        {
            if (activeIndices.ContainsKey(this)) return;
            activeIndices.Add(this, activeInstances.Count);
            activeInstances.Add(this);
            ActiveRevision++;
            if (!isProp) SolidRevision++;
        }
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
        public string AuthoringId => planningBoxId;
        public Vector2Int AuthoringLocalCell => planningLocalCell;
        public void SetPlanningSource(string id, string signature, Vector2Int localCell)
        {
            planningBoxId = id;
            planningSignature = signature;
            planningLocalCell = localCell;
        }

        public Vector2Int Cell => cell;
        public Vector2Int SizeCells => new Vector2Int(Mathf.Max(1, sizeCells.x), Mathf.Max(1, sizeCells.y));
        public float CellWorldSize => Mathf.Max(.01f, cellWorldSize);
        public bool ContainsCell(Vector2Int value)
        {
            if (value.x < cell.x || value.y < cell.y || value.x >= cell.x + SizeCells.x || value.y >= cell.y + SizeCells.y) return false;
            var shape = GetComponent<LevelEditorMergedShape>();
            return shape == null || shape.ContainsCell(new Vector2Int(value.x - cell.x, SizeCells.y - 1 - (value.y - cell.y)));
        }
        public string EntryName => entryName;
        public Color EntryColor => entryColor;
        public bool HasColorData => hasColorData;
        public bool IsProp => isProp;

        public void SetAuthoredCell(Vector2Int value)
        {
            if (cell == value) return;
            cell = value;
            ActiveRevision++;
            NotifySolidGeometryChanged();
        }

        public void Configure(
            Vector2Int valueCell,
            string valueEntryName,
            Color valueColor,
            bool valueUseEntryColor,
            bool valueIsProp = false,
            float valueCellWorldSize = 1f)
        {
            bool affectedWall = !isProp || !valueIsProp;
            cell = valueCell;
            entryName = valueEntryName;
            entryColor = valueColor;
            useEntryColor = valueUseEntryColor;
            hasColorData = true;
            isProp = valueIsProp;
            sizeCells = Vector2Int.one;
            SetCellWorldSize(valueCellWorldSize);
            ActiveRevision++;
            if (affectedWall) SolidRevision++;
            ApplyEntryColor();
        }

        public void SetCellWorldSize(float value)
        {
            float next = Mathf.Max(.01f, value);
            if (cellWorldSize == next) return;
            cellWorldSize = next;
            NotifySolidGeometryChanged();
        }

        public void SetSizeCells(Vector2Int value)
        {
            sizeCells = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
            ActiveRevision++;
            NotifySolidGeometryChanged();
        }

        /// <summary>Call after changing a wall's actual Transform/collider geometry at runtime.</summary>
        public void NotifySolidGeometryChanged()
        {
            if (!isProp) SolidRevision++;
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
            RegisterActive();
            ApplyEntryColor();
        }

        private void OnDisable()
        {
            if (!activeIndices.TryGetValue(this, out int index)) return;
            int lastIndex = activeInstances.Count - 1;
            LevelEditorPlacedBlock last = activeInstances[lastIndex];
            activeInstances[index] = last;
            activeIndices[last] = index;
            activeInstances.RemoveAt(lastIndex);
            activeIndices.Remove(this);
            ActiveRevision++;
            if (!isProp) SolidRevision++;
        }
    }
}
