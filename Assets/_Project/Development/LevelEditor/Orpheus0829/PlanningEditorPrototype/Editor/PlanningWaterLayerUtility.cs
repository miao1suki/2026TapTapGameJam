using Project.BlockFeatures;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal static class PlanningWaterLayerUtility
    {
        internal static bool IsWater(PlanningBox box, LevelEditorPalette palette = null)
        {
            if (box == null) return false;
            if (box.type == PlanningDetailType.Water) return true;
            if (box.type != PlanningDetailType.Prop) return false;
            palette ??= LevelEditorState.Palette;
            if (palette == null) return false;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryId) && entry.EntryId == box.propEntryId)
                    return entry.Prefab != null && entry.Prefab.GetComponent<WaterVisualFeature>() != null;
            foreach (var entry in palette.PropEntries)
                if (entry != null && !string.IsNullOrEmpty(box.propEntryName) && entry.DisplayName == box.propEntryName)
                    return entry.Prefab != null && entry.Prefab.GetComponent<WaterVisualFeature>() != null;
            return false;
        }
    }
}
