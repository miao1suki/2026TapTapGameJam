using Project.SurfaceTiles;
using UnityEngine;

namespace Project.LevelEditor
{
    [DisallowMultipleComponent]
    public sealed class LevelEditorPlacedBlock : MonoBehaviour
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private string entryName;
        [SerializeField] private Color entryColor = Color.white;
        [SerializeField] private bool useEntryColor;
        [SerializeField] private bool hasColorData;
        [SerializeField] private bool isProp;

        public Vector2Int Cell => cell;
        public string EntryName => entryName;
        public Color EntryColor => entryColor;
        public bool HasColorData => hasColorData;
        public bool IsProp => isProp;

        public void Configure(
            Vector2Int valueCell,
            string valueEntryName,
            Color valueColor,
            bool valueUseEntryColor,
            bool valueIsProp = false)
        {
            cell = valueCell;
            entryName = valueEntryName;
            entryColor = valueColor;
            useEntryColor = valueUseEntryColor;
            hasColorData = true;
            isProp = valueIsProp;
            ApplyEntryColor();
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
