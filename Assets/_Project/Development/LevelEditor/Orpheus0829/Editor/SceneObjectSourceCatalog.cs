using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    internal enum SceneSourceMatchKind
    {
        Claimed,
        ClaimedMissing,
        ClaimedPrefabMismatch,
        ExactPrefab,
        ExactName,
        Ambiguous,
        Unmatched,
    }

    internal sealed class ScenePaletteSource
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
        internal string AssetPath => prefab != null ? AssetDatabase.GetAssetPath(prefab) : string.Empty;
        internal string SearchText => $"{KindLabel} {displayName} {id} {AssetPath}";
    }

    internal readonly struct SceneSourceResolution
    {
        internal readonly SceneSourceMatchKind kind;
        internal readonly ScenePaletteSource source;
        internal readonly int candidateCount;

        internal SceneSourceResolution(SceneSourceMatchKind kind, ScenePaletteSource source, int candidateCount = 0)
        {
            this.kind = kind;
            this.source = source;
            this.candidateCount = candidateCount;
        }

        internal bool CanClaim => kind == SceneSourceMatchKind.ExactPrefab || kind == SceneSourceMatchKind.ExactName;
        internal bool IsResolved => source != null &&
            kind != SceneSourceMatchKind.ClaimedMissing && kind != SceneSourceMatchKind.Ambiguous;

        internal string Describe(LevelEditorPlacedBlock block)
        {
            string name = block != null ? block.EntryName : "<无对象>";
            return kind switch
            {
                SceneSourceMatchKind.Claimed => $"已认领：{source.KindLabel} · {source.displayName}",
                SceneSourceMatchKind.ClaimedMissing => $"来源 ID 已失效：{block?.AuthoringPaletteEntryId}",
                SceneSourceMatchKind.ClaimedPrefabMismatch => $"已认领 {source.displayName}，但实例 Prefab 与模板不一致，请核对后重新认领。",
                SceneSourceMatchKind.ExactPrefab => $"可精确认领：{source.KindLabel} · {source.displayName}（Prefab 匹配）",
                SceneSourceMatchKind.ExactName => $"可认领：{source.KindLabel} · {source.displayName}（唯一名称匹配）",
                SceneSourceMatchKind.Ambiguous => $"“{name}”有 {candidateCount} 个可能来源，请手动选择模板。",
                _ => $"“{name}”没有匹配来源，可手动认领为当前模板。",
            };
        }
    }

    internal readonly struct SceneSourceAudit
    {
        internal readonly int total;
        internal readonly int claimed;
        internal readonly int inferred;
        internal readonly int ambiguous;
        internal readonly int missing;
        internal readonly int broken;

        internal SceneSourceAudit(int total, int claimed, int inferred, int ambiguous, int missing, int broken)
        {
            this.total = total;
            this.claimed = claimed;
            this.inferred = inferred;
            this.ambiguous = ambiguous;
            this.missing = missing;
            this.broken = broken;
        }

        public override string ToString() =>
            $"对象 {total} · 已认领 {claimed} · 可明确认领 {inferred} · 歧义 {ambiguous} · 无匹配 {missing} · 来源失效/不一致 {broken}";
    }

    /// <summary>
    /// Read-only palette/source resolver plus explicit Undo-backed claiming.
    /// Building and resolving never mutate the Scene or palette.
    /// </summary>
    internal static class SceneObjectSourceCatalog
    {
        internal static List<ScenePaletteSource> Build(LevelEditorPalette palette)
        {
            var result = new List<ScenePaletteSource>();
            if (palette == null) return result;
            for (int i = 0; i < palette.Entries.Count; i++)
            {
                LevelEditorBlockEntry entry = palette.Entries[i];
                if (entry == null) continue;
                result.Add(new ScenePaletteSource
                {
                    palette = palette,
                    isProp = false,
                    index = i,
                    id = entry.EntryId ?? string.Empty,
                    displayName = entry.DisplayName ?? "<未命名方块>",
                    prefab = entry.SourcePrefab,
                    color = entry.Color,
                    overrideCount = entry.ComponentValueOverrides?.Count ?? 0,
                    channel = SceneObjectChannel.TerrainSolid,
                });
            }
            for (int i = 0; i < palette.PropEntries.Count; i++)
            {
                LevelEditorPropEntry entry = palette.PropEntries[i];
                if (entry == null) continue;
                result.Add(new ScenePaletteSource
                {
                    palette = palette,
                    isProp = true,
                    index = i,
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

        internal static SceneSourceResolution Resolve(LevelEditorPlacedBlock block, IReadOnlyList<ScenePaletteSource> sources)
        {
            if (block == null || sources == null) return new SceneSourceResolution(SceneSourceMatchKind.Unmatched, null);
            if (!string.IsNullOrWhiteSpace(block.AuthoringPaletteEntryId))
            {
                ScenePaletteSource claimed = sources.FirstOrDefault(item =>
                    item.isProp == block.AuthoringPaletteEntryIsProp && item.id == block.AuthoringPaletteEntryId);
                if (claimed == null) return new SceneSourceResolution(SceneSourceMatchKind.ClaimedMissing, null);
                GameObject actual = PrefabSource(block.gameObject);
                if (actual != null && claimed.prefab != null && !SameAsset(actual, claimed.prefab))
                    return new SceneSourceResolution(SceneSourceMatchKind.ClaimedPrefabMismatch, claimed);
                return new SceneSourceResolution(SceneSourceMatchKind.Claimed, claimed);
            }

            List<ScenePaletteSource> compatible = sources.Where(item => item.isProp == block.IsProp).ToList();
            GameObject prefab = PrefabSource(block.gameObject);
            if (prefab != null)
            {
                List<ScenePaletteSource> prefabMatches = compatible.Where(item => item.prefab != null && SameAsset(prefab, item.prefab)).ToList();
                if (prefabMatches.Count == 1) return new SceneSourceResolution(SceneSourceMatchKind.ExactPrefab, prefabMatches[0], 1);
                if (prefabMatches.Count > 1)
                {
                    List<ScenePaletteSource> named = prefabMatches.Where(item => item.displayName == block.EntryName).ToList();
                    if (named.Count == 1) return new SceneSourceResolution(SceneSourceMatchKind.ExactPrefab, named[0], 1);
                    return new SceneSourceResolution(SceneSourceMatchKind.Ambiguous, null, prefabMatches.Count);
                }
            }

            List<ScenePaletteSource> nameMatches = compatible.Where(item => item.displayName == block.EntryName).ToList();
            if (nameMatches.Count == 1) return new SceneSourceResolution(SceneSourceMatchKind.ExactName, nameMatches[0], 1);
            if (nameMatches.Count > 1) return new SceneSourceResolution(SceneSourceMatchKind.Ambiguous, null, nameMatches.Count);
            return new SceneSourceResolution(SceneSourceMatchKind.Unmatched, null);
        }

        internal static SceneSourceAudit Audit(Scene scene, LevelEditorPalette palette)
        {
            List<ScenePaletteSource> sources = Build(palette);
            SceneObjectIndex index = SceneObjectAuthoring.Read(scene);
            int claimed = 0, inferred = 0, ambiguous = 0, missing = 0, broken = 0;
            foreach (SceneObjectRecord record in index.All)
            {
                SceneSourceResolution resolution = Resolve(record.block, sources);
                switch (resolution.kind)
                {
                    case SceneSourceMatchKind.Claimed: claimed++; break;
                    case SceneSourceMatchKind.ExactPrefab:
                    case SceneSourceMatchKind.ExactName: inferred++; break;
                    case SceneSourceMatchKind.Ambiguous: ambiguous++; break;
                    case SceneSourceMatchKind.ClaimedMissing:
                    case SceneSourceMatchKind.ClaimedPrefabMismatch: broken++; break;
                    default: missing++; break;
                }
            }
            return new SceneSourceAudit(index.All.Count, claimed, inferred, ambiguous, missing, broken);
        }

        internal static bool ClaimGroup(SceneObjectRecord record, ScenePaletteSource source, out string reason)
        {
            reason = string.Empty;
            if (record?.block == null || source == null || string.IsNullOrWhiteSpace(source.id))
            {
                reason = "没有可认领的场景对象或模板。";
                return false;
            }
            Scene scene = record.block.gameObject.scene;
            IReadOnlyList<SceneObjectRecord> group = SceneObjectAuthoring.Read(scene).Group(record);
            if (group.Any(item => item.block == null || item.block.IsProp != source.isProp))
            {
                reason = source.isProp ? "方块组不能认领为道具模板。" : "道具组不能认领为方块模板。";
                return false;
            }
            LevelEditorPlacedBlock[] changed = group.Select(item => item.block)
                .Where(block => block.AuthoringPaletteEntryId != source.id || block.AuthoringPaletteEntryIsProp != source.isProp)
                .Distinct().ToArray();
            if (changed.Length == 0)
            {
                reason = "所选逻辑组已经认领该来源。";
                return false;
            }

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("认领场景对象数据来源");
            Undo.RecordObjects(changed, "认领场景对象数据来源");
            foreach (LevelEditorPlacedBlock block in changed)
            {
                block.SetAuthoringPaletteSource(source.id, source.isProp);
                EditorUtility.SetDirty(block);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            SceneObjectAuthoring.NotifyChanged();
            reason = $"已将 {changed.Length} 个组成员认领为“{source.displayName}”。";
            return true;
        }

        internal static bool ClaimInferred(SceneObjectRecord record, LevelEditorPalette palette, out string reason)
        {
            reason = string.Empty;
            if (record?.block == null) { reason = "请先选择场景中的关卡对象。"; return false; }
            SceneSourceResolution resolution = Resolve(record.block, Build(palette));
            if (!resolution.CanClaim)
            {
                reason = resolution.Describe(record.block);
                return false;
            }
            return ClaimGroup(record, resolution.source, out reason);
        }

        internal static int ClaimAllUnambiguous(Scene scene, LevelEditorPalette palette, out string reason)
        {
            reason = string.Empty;
            List<ScenePaletteSource> sources = Build(palette);
            SceneObjectIndex index = SceneObjectAuthoring.Read(scene);
            var changed = new Dictionary<LevelEditorPlacedBlock, ScenePaletteSource>();
            foreach (IGrouping<string, SceneObjectRecord> group in index.All.GroupBy(item =>
                !string.IsNullOrEmpty(item.authorId) ? item.authorId : "instance:" + item.block.GetInstanceID()))
            {
                SceneObjectRecord[] members = group.ToArray();
                ScenePaletteSource claimed = null;
                bool conflict = false;
                foreach (SceneObjectRecord member in members)
                {
                    if (string.IsNullOrWhiteSpace(member.block.AuthoringPaletteEntryId)) continue;
                    SceneSourceResolution existing = Resolve(member.block, sources);
                    if (existing.source == null) { conflict = true; break; }
                    if (claimed != null && (claimed.id != existing.source.id || claimed.isProp != existing.source.isProp)) { conflict = true; break; }
                    claimed = existing.source;
                }
                if (conflict) continue;

                ScenePaletteSource inferred = claimed;
                foreach (SceneObjectRecord member in members)
                {
                    if (!string.IsNullOrWhiteSpace(member.block.AuthoringPaletteEntryId)) continue;
                    SceneSourceResolution resolution = Resolve(member.block, sources);
                    if (!resolution.CanClaim || (inferred != null &&
                        (inferred.id != resolution.source.id || inferred.isProp != resolution.source.isProp)))
                    { conflict = true; break; }
                    inferred = resolution.source;
                }
                if (conflict || inferred == null) continue;
                foreach (SceneObjectRecord member in members)
                    if (string.IsNullOrWhiteSpace(member.block.AuthoringPaletteEntryId)) changed[member.block] = inferred;
            }

            if (changed.Count == 0)
            {
                reason = "没有可安全批量认领的对象；歧义项需要手动选择模板。";
                return 0;
            }
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("批量认领明确数据来源");
            Undo.RecordObjects(changed.Keys.Cast<UnityEngine.Object>().ToArray(), "批量认领明确数据来源");
            foreach (KeyValuePair<LevelEditorPlacedBlock, ScenePaletteSource> pair in changed)
            {
                pair.Key.SetAuthoringPaletteSource(pair.Value.id, pair.Value.isProp);
                EditorUtility.SetDirty(pair.Key);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            SceneObjectAuthoring.NotifyChanged();
            reason = $"已批量认领 {changed.Count} 个对象；歧义和无匹配项保持原样。";
            return changed.Count;
        }

        internal static GameObject PrefabSource(GameObject instance)
        {
            if (instance == null) return null;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (source != null) return source;
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            return root != null ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
        }

        private static bool SameAsset(GameObject left, GameObject right)
        {
            if (left == right) return true;
            string leftPath = AssetDatabase.GetAssetPath(left);
            string rightPath = AssetDatabase.GetAssetPath(right);
            return !string.IsNullOrEmpty(leftPath) && leftPath == rightPath;
        }

        internal static SceneObjectChannel ClassifyProp(GameObject prefab)
        {
            if (prefab == null) return SceneObjectChannel.GameplayProp;
            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours.Any(item => item != null &&
                (item.GetType().Name.Contains("Water", StringComparison.OrdinalIgnoreCase) ||
                 item.GetType().Name.Contains("Buoyancy", StringComparison.OrdinalIgnoreCase) ||
                 item.GetType().Name.Contains("Current", StringComparison.OrdinalIgnoreCase))))
                return SceneObjectChannel.WaterVolume;
            if (prefab.GetComponentInChildren<UnityEngine.Playables.PlayableDirector>(true) != null ||
                behaviours.Any(item => item != null &&
                (item.GetType().Name.Contains("Trigger", StringComparison.OrdinalIgnoreCase) ||
                 item.GetType().Name.Contains("Marker", StringComparison.OrdinalIgnoreCase) ||
                 item.GetType().Name.Contains("Respawn", StringComparison.OrdinalIgnoreCase))))
                return SceneObjectChannel.MarkerTrigger;
            return SceneObjectChannel.GameplayProp;
        }
    }
}
