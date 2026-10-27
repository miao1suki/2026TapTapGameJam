using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.LevelEditor.Editor
{
    /// <summary>
    /// A read-only view over LevelEditorPalette. Palette items are spawn templates;
    /// placed Scene instances never retain a link back to an item.
    /// </summary>
    internal sealed class ScenePaletteItem
    {
        internal LevelEditorPalette palette;
        internal bool isProp;
        internal int index;
        internal string id;
        internal string displayName;
        internal GameObject prefab;
        internal Color color;
        internal int overrideCount;
        internal SceneObjectChannel channel;

        internal string KindLabel => isProp ? "道具" : "方块";
        internal string AssetPath => prefab != null
            ? AssetDatabase.GetAssetPath(prefab)
            : string.Empty;
        internal string SearchText =>
            $"{KindLabel} {displayName} {id} {AssetPath}";
    }

    internal static class ScenePlacementPaletteCatalog
    {
        internal static event Action SelectionChanged;

        internal const string PalettePath =
            "Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset";

        internal static LevelEditorPalette LoadPalette()
        {
            LevelEditorPalette palette =
                AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(PalettePath);
            LevelEditorState.Palette = palette;
            return palette;
        }

        internal static List<ScenePaletteItem> Build(
            LevelEditorPalette palette)
        {
            var result = new List<ScenePaletteItem>();
            if (palette == null)
            {
                return result;
            }

            for (int index = 0; index < palette.Entries.Count; index++)
            {
                LevelEditorBlockEntry entry = palette.Entries[index];
                if (entry == null)
                {
                    continue;
                }

                result.Add(new ScenePaletteItem
                {
                    palette = palette,
                    isProp = false,
                    index = index,
                    id = entry.EntryId ?? string.Empty,
                    displayName = entry.DisplayName ?? "<未命名方块>",
                    prefab = entry.SourcePrefab,
                    color = entry.Color,
                    overrideCount = entry.ComponentValueOverrides?.Count ?? 0,
                    channel = SceneObjectChannel.TerrainSolid,
                });
            }

            for (int index = 0; index < palette.PropEntries.Count; index++)
            {
                LevelEditorPropEntry entry = palette.PropEntries[index];
                if (entry == null)
                {
                    continue;
                }

                result.Add(new ScenePaletteItem
                {
                    palette = palette,
                    isProp = true,
                    index = index,
                    id = entry.EntryId ?? string.Empty,
                    displayName = entry.DisplayName ?? "<未命名道具>",
                    prefab = entry.Prefab,
                    color = Color.white,
                    overrideCount = entry.ComponentValueOverrides?.Count ?? 0,
                    channel = ClassifyProp(entry.Prefab),
                });
            }

            return result;
        }

        internal static ScenePaletteItem Current(
            IReadOnlyList<ScenePaletteItem> items)
        {
            if (items == null)
            {
                return null;
            }

            return items.FirstOrDefault(item =>
                item.isProp
                    ? item.index == LevelEditorState.SelectedPropIndex &&
                      LevelEditorState.SelectedEntryIndex < 0
                    : item.index == LevelEditorState.SelectedEntryIndex &&
                      LevelEditorState.SelectedPropIndex < 0);
        }

        internal static void Select(ScenePaletteItem item)
        {
            if (item == null)
            {
                return;
            }

            int previousEntry = LevelEditorState.SelectedEntryIndex;
            int previousProp = LevelEditorState.SelectedPropIndex;
            SceneObjectChannel? previousChannel =
                SceneObjectAuthoring.ActiveChannel;
            LevelEditorState.Palette = item.palette;
            if (item.isProp)
            {
                LevelEditorState.SelectedPropIndex = item.index;
                LevelEditorState.SelectedEntryIndex = -1;
            }
            else
            {
                LevelEditorState.SelectedEntryIndex = item.index;
                LevelEditorState.SelectedPropIndex = -1;
            }

            SceneObjectAuthoring.ActiveChannel = item.channel;
            SceneView.RepaintAll();
            if (previousEntry != LevelEditorState.SelectedEntryIndex ||
                previousProp != LevelEditorState.SelectedPropIndex ||
                previousChannel != SceneObjectAuthoring.ActiveChannel)
            {
                SelectionChanged?.Invoke();
            }
        }

        internal static SceneObjectChannel ClassifyProp(GameObject prefab)
        {
            if (prefab == null)
            {
                return SceneObjectChannel.GameplayProp;
            }

            MonoBehaviour[] behaviours =
                prefab.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours.Any(item => item != null &&
                (item.GetType().Name.Contains(
                     "Water", StringComparison.OrdinalIgnoreCase) ||
                 item.GetType().Name.Contains(
                     "Buoyancy", StringComparison.OrdinalIgnoreCase) ||
                 item.GetType().Name.Contains(
                     "Current", StringComparison.OrdinalIgnoreCase))))
            {
                return SceneObjectChannel.WaterVolume;
            }

            if (prefab.GetComponentInChildren<
                    UnityEngine.Playables.PlayableDirector>(true) != null ||
                behaviours.Any(item => item != null &&
                    (item.GetType().Name.Contains(
                         "Trigger", StringComparison.OrdinalIgnoreCase) ||
                     item.GetType().Name.Contains(
                         "Marker", StringComparison.OrdinalIgnoreCase) ||
                     item.GetType().Name.Contains(
                         "Respawn", StringComparison.OrdinalIgnoreCase))))
            {
                return SceneObjectChannel.MarkerTrigger;
            }

            return SceneObjectChannel.GameplayProp;
        }
    }
}
