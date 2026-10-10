using System.Collections.Generic;
using System.IO;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.SurfaceTiles;
using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.LevelEditor.Editor
{
    internal enum LevelEditorTool
    {
        Select = 0,
        Paint = 1,
        Erase = 2,
        Player = 3,
        Merge = 4,
        Parameters = 5,
    }

    internal static class LevelEditorState
    {
        private const string PaletteScalePreferenceKey =
            "2026TapTap.LevelEditor.PaletteScale";
        private const string PlayerReferencePreferenceKey =
            "2026TapTap.LevelEditor.PlayerReference";
        private const string GenerationParentPreferenceKey =
            "2026TapTap.LevelEditor.GenerationParent";
        private const string LockPlayerToPlanePreferenceKey =
            "2026TapTap.LevelEditor.LockPlayerToPlane";
        private static GameObject player;
        private static GameObject generationParent;

        internal static bool EditMode { get; set; }
        internal static LevelEditorTool Tool { get; set; }
        internal static float CellSize { get; set; } = 1f;
        internal static LevelEditorPalette Palette { get; set; }
        internal static int SelectedEntryIndex { get; set; }
        internal static int SelectedPropIndex { get; set; }
        internal static int PaletteRevision { get; private set; }

        internal static void MarkPaletteChanged()
        {
            PaletteRevision++;
        }

        internal static float PaletteScale
        {
            get
            {
                return Mathf.Clamp(
                    EditorPrefs.GetFloat(PaletteScalePreferenceKey, 1f),
                    .5f,
                    1.5f);
            }
            set
            {
                EditorPrefs.SetFloat(
                    PaletteScalePreferenceKey,
                    Mathf.Clamp(value, .5f, 1.5f));
            }
        }

        internal static GameObject Player
        {
            get
            {
                if (player != null)
                {
                    return player;
                }

                string reference = EditorPrefs.GetString(
                    PlayerReferencePreferenceKey,
                    string.Empty);
                if (!string.IsNullOrEmpty(reference) &&
                    GlobalObjectId.TryParse(
                        reference,
                        out GlobalObjectId globalId))
                {
                    player =
                        GlobalObjectId.GlobalObjectIdentifierToObjectSlow(
                            globalId) as GameObject;
                }

                return player;
            }
            set
            {
                player = value;
                if (value == null)
                {
                    EditorPrefs.DeleteKey(PlayerReferencePreferenceKey);
                    return;
                }

                EditorPrefs.SetString(
                    PlayerReferencePreferenceKey,
                    GlobalObjectId.GetGlobalObjectIdSlow(value).ToString());
            }
        }

        internal static GameObject GenerationParent
        {
            get
            {
                if (generationParent != null)
                {
                    return generationParent;
                }

                string reference = EditorPrefs.GetString(
                    GenerationParentPreferenceKey,
                    string.Empty);
                if (!string.IsNullOrEmpty(reference) &&
                    GlobalObjectId.TryParse(
                        reference,
                        out GlobalObjectId globalId))
                {
                    generationParent =
                        GlobalObjectId.GlobalObjectIdentifierToObjectSlow(
                            globalId) as GameObject;
                }

                return generationParent;
            }
            set
            {
                if (value != null && !value.scene.IsValid())
                {
                    value = null;
                }

                generationParent = value;
                if (value == null)
                {
                    EditorPrefs.DeleteKey(
                        GenerationParentPreferenceKey);
                    return;
                }

                EditorPrefs.SetString(
                    GenerationParentPreferenceKey,
                    GlobalObjectId.GetGlobalObjectIdSlow(value).ToString());
            }
        }

        internal static bool LockPlayerToPlane
        {
            get
            {
                return EditorPrefs.GetBool(
                    LockPlayerToPlanePreferenceKey,
                    true);
            }
            set
            {
                EditorPrefs.SetBool(
                    LockPlayerToPlanePreferenceKey,
                    value);
            }
        }
    }

    internal static class LevelEditorPlayerService
    {
        internal static bool SnapPlayerToPlane()
        {
            GameObject player = LevelEditorState.Player;
            if (player == null)
            {
                return false;
            }

            return SetPlayerPosition(player.transform.position, true);
        }

        internal static bool SetPlayerPosition(
            Vector3 position,
            bool recordUndo)
        {
            GameObject player = LevelEditorState.Player;
            if (EditorApplication.isPlayingOrWillChangePlaymode || player == null || !player.scene.IsValid())
            {
                return false;
            }

            Transform playerTransform = player.transform;
            position.z = 0f;
            if ((playerTransform.position - position).sqrMagnitude <
                .000001f)
            {
                return false;
            }

            if (recordUndo)
            {
                Undo.RecordObject(
                    playerTransform,
                    "移动玩家参考");
            }

            playerTransform.position = position;
            EditorUtility.SetDirty(playerTransform);
            if (player.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(player.scene);
            }

            return true;
        }

        internal static bool EnforceEditPlane()
        {
            if (!LevelEditorState.EditMode ||
                !LevelEditorState.LockPlayerToPlane)
            {
                return false;
            }

            return SnapPlayerToPlane();
        }
    }

    internal static class PlanningPlayerUndoUtility
    {
        private static readonly List<PlanningPlayerUndoRecord> Records =
            new List<PlanningPlayerUndoRecord>();

        static PlanningPlayerUndoUtility()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        internal static void RecordDrag(
            GameObject player,
            Vector3 beforePosition,
            Vector3 afterPosition)
        {
            if (player == null ||
                !player.scene.IsValid() ||
                (beforePosition - afterPosition).sqrMagnitude <
                .000001f)
            {
                return;
            }

            PlanningPlayerUndoRecord record =
                ScriptableObject.CreateInstance<
                    PlanningPlayerUndoRecord>();
            record.hideFlags = HideFlags.HideAndDontSave;
            record.Configure(
                player.GetInstanceID(),
                beforePosition,
                afterPosition);
            Undo.RecordObject(record, "移动玩家参考");
            record.MarkAfter();
            Records.Add(record);
        }

        private static void OnUndoRedoPerformed()
        {
            for (int index = Records.Count - 1;
                 index >= 0;
                 index--)
            {
                PlanningPlayerUndoRecord record = Records[index];
                if (record == null)
                {
                    Records.RemoveAt(index);
                    continue;
                }

                if (record.LastHandledVersion == record.Version)
                {
                    continue;
                }

                record.LastHandledVersion = record.Version;
                Apply(record);
            }
        }

        private static void Apply(PlanningPlayerUndoRecord record)
        {
            GameObject player = LevelEditorState.Player;
            if (player == null ||
                !player.scene.IsValid() ||
                player.GetInstanceID() != record.PlayerInstanceId)
            {
                return;
            }

            Vector3 target = record.Version == 1
                ? record.AfterPosition
                : record.BeforePosition;
            target.z = 0f;
            Transform targetTransform = player.transform;
            if ((targetTransform.position - target).sqrMagnitude <
                .000001f)
            {
                return;
            }

            targetTransform.position = target;
            EditorUtility.SetDirty(targetTransform);
            EditorSceneManager.MarkSceneDirty(player.scene);
        }
    }

    internal sealed class PlanningPlayerUndoRecord : ScriptableObject
    {
        [SerializeField] private int playerInstanceId;
        [SerializeField] private Vector3 beforePosition;
        [SerializeField] private Vector3 afterPosition;
        [SerializeField] private int version;

        internal int PlayerInstanceId => playerInstanceId;
        internal Vector3 BeforePosition => beforePosition;
        internal Vector3 AfterPosition => afterPosition;
        internal int Version => version;
        internal int LastHandledVersion { get; set; }

        internal void Configure(
            int valuePlayerInstanceId,
            Vector3 valueBeforePosition,
            Vector3 valueAfterPosition)
        {
            playerInstanceId = valuePlayerInstanceId;
            beforePosition = valueBeforePosition;
            afterPosition = valueAfterPosition;
            version = 0;
            LastHandledVersion = 0;
        }

        internal void MarkAfter()
        {
            version = 1;
            LastHandledVersion = 1;
        }
    }

    internal static class LevelEditorPaletteService
    {
        private const string PalettePath =
            "Assets/_Project/Development/LevelEditor/Orpheus0829/" +
            "LevelEditorPalette.asset";

        internal static LevelEditorPalette GetOrCreate()
        {
            LevelEditorPalette palette =
                AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(
                    PalettePath);
            if (palette != null)
            {
                EnsureEntryIds(palette);
                EnsurePropEntryIds(palette);
                return palette;
            }

            string folder = Path.GetDirectoryName(PalettePath)
                ?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) &&
                !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            palette = ScriptableObject.CreateInstance<LevelEditorPalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
            AddDefaultEntries(palette);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            return palette;
        }

        internal static int AddEntry(
            LevelEditorPalette palette,
            string displayName,
            Color color,
            GameObject prefab,
            LevelEditorBlockMode mode = LevelEditorBlockMode.Custom)
        {
            if (palette == null)
            {
                return -1;
            }

            Undo.RecordObject(palette, "新增关卡方块栏目");
            LevelEditorBlockEntry entry = new LevelEditorBlockEntry();
            entry.Configure(displayName, color, prefab, mode);
            palette.Add(entry);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
            return palette.Entries.Count - 1;
        }

        internal static bool UpdateEntry(
            LevelEditorPalette palette,
            int index,
            string displayName,
            Color color,
            GameObject prefab,
            LevelEditorBlockMode mode)
        {
            if (palette == null || index < 0 ||
                index >= palette.Entries.Count)
            {
                return false;
            }

            Undo.RecordObject(palette, "编辑关卡方块栏目");
            palette.Entries[index].Configure(
                displayName,
                color,
                prefab,
                mode);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
            return true;
        }

        internal static void RemoveEntry(
            LevelEditorPalette palette,
            int index)
        {
            if (palette == null)
            {
                return;
            }

            LevelEditorBlockEntry removedEntry =
                index >= 0 && index < palette.Entries.Count
                    ? palette.Entries[index]
                    : null;
            Undo.RecordObject(palette, "移除关卡方块栏目");
            palette.RemoveAt(index);
            LevelEditorBlockFactory.RemovePlacedBlocksForEntry(
                removedEntry);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
        }

        internal static int AddProp(
            LevelEditorPalette palette,
            string displayName,
            GameObject prefab)
        {
            if (palette == null)
            {
                return -1;
            }

            Undo.RecordObject(palette, "新增道具栏目");
            var entry = new LevelEditorPropEntry();
            entry.Configure(displayName, prefab);
            palette.AddProp(entry);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
            return palette.PropEntries.Count - 1;
        }

        internal static bool UpdateProp(
            LevelEditorPalette palette,
            int index,
            string displayName,
            GameObject prefab)
        {
            if (palette == null ||
                index < 0 ||
                index >= palette.PropEntries.Count)
            {
                return false;
            }

            Undo.RecordObject(palette, "编辑道具栏目");
            palette.PropEntries[index].Configure(
                displayName,
                prefab);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
            return true;
        }

        internal static void RemoveProp(
            LevelEditorPalette palette,
            int index)
        {
            if (palette == null)
            {
                return;
            }

            Undo.RecordObject(palette, "移除道具栏目");
            palette.RemovePropAt(index);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            LevelEditorState.MarkPaletteChanged();
        }

        private static void AddDefaultEntries(LevelEditorPalette palette)
        {
            AddEntry(palette, "黑方块", new Color(.08f, .08f, .08f), null);
            AddEntry(palette, "白方块", new Color(.95f, .95f, .95f), null);
            AddDefaultColorKey(palette, "red", "红色钥匙");
            AddDefaultColorKey(palette, "green", "绿色钥匙");
            AddDefaultColorKey(palette, "blue", "蓝色钥匙");
        }

        private static void AddDefaultColorKey(
            LevelEditorPalette palette, string typeId, string displayName)
        {
            string path = "Assets/_Project/Content/ColorBlocks/Prefabs/ColorKey_" +
                          typeId + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) AddProp(palette, displayName, prefab);
            else Debug.LogError($"关卡道具栏目缺少颜色钥匙预制体：{path}");
        }

        private static void EnsureEntryIds(LevelEditorPalette palette)
        {
            bool changed = false;
            for (int index = 0; index < palette.Entries.Count; index++)
            {
                LevelEditorBlockEntry entry = palette.Entries[index];
                if (entry == null || !string.IsNullOrEmpty(entry.EntryId))
                {
                    continue;
                }

                entry.EnsureEntryId();
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssetIfDirty(palette);
            }
        }

        private static void EnsurePropEntryIds(LevelEditorPalette palette)
        {
            bool changed = false;
            for (int index = 0; index < palette.PropEntries.Count; index++)
            {
                LevelEditorPropEntry entry = palette.PropEntries[index];
                if (entry == null || !string.IsNullOrEmpty(entry.EntryId))
                {
                    continue;
                }

                entry.EnsureEntryId();
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssetIfDirty(palette);
            }
        }
    }

    internal static class LevelEditorViewLock
    {
        private static SceneView sceneView;
        private static bool active;
        private static bool previousOrthographic;
        private static Quaternion previousRotation;
        private static Vector3 previousPivot;
        private static float previousSize;

        internal static void Enter(SceneView view)
        {
            if (active || view == null)
            {
                return;
            }

            sceneView = view;
            previousOrthographic = view.orthographic;
            previousRotation = view.rotation;
            previousPivot = view.pivot;
            previousSize = view.size;
            active = true;
            view.orthographic = true;
            view.rotation = Quaternion.identity;
            view.pivot = new Vector3(view.pivot.x, view.pivot.y, 0f);
            view.Repaint();
        }

        internal static void Tick(SceneView view)
        {
            if (!active || view == null || view != sceneView)
            {
                return;
            }

            view.orthographic = true;
            view.rotation = Quaternion.identity;
            Vector3 pivot = view.pivot;
            pivot.z = 0f;
            view.pivot = pivot;
        }

        internal static void Exit()
        {
            if (!active)
            {
                return;
            }

            if (sceneView != null)
            {
                sceneView.orthographic = false;
                sceneView.rotation = previousOrthographic
                    ? Quaternion.Euler(30f, -45f, 0f)
                    : previousRotation;
                sceneView.pivot = previousPivot;
                sceneView.size = previousSize;
                sceneView.Repaint();
            }

            sceneView = null;
            active = false;
        }
    }

    internal static class LevelEditorBlockFactory
    {
        private const string RootName = "__LevelEditorContent";

        internal static LevelEditorPlacedBlock FindAt(Vector2Int cell)
        {
            UnityEngine.SceneManagement.Scene targetScene = TargetScene();
            IReadOnlyList<LevelEditorPlacedBlock> blocks =
                ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true);
            for (int index = 0; index < blocks.Count; index++)
            {
                if (blocks[index].gameObject.scene == targetScene &&
                    blocks[index].ContainsCell(cell))
                {
                    return blocks[index];
                }
            }

            return null;
        }

        internal static LevelEditorPlacedBlock Place(
            LevelEditorBlockEntry entry,
            Vector2Int cell)
        {
            if (entry == null || cell.x < -512 || cell.y < -512 ||
                cell.x > 512 || cell.y > 512)
            {
                return null;
            }

            LevelEditorPlacedBlock existing = FindAt(cell);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return existing;
            }

            GameObject instance;
            UnityEngine.SceneManagement.Scene targetScene = TargetScene();
            GameObject source = entry.UsesPrefabDirectly
                ? entry.Prefab
                : entry.SourcePrefab;
            if (source != null)
            {
                instance = PrefabUtility.InstantiatePrefab(source, targetScene)
                    as GameObject;
            }
            else if (entry.UsesPrefabDirectly)
            {
                return null;
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (instance.scene != targetScene)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, targetScene);
                instance.transform.localScale =
                    Vector3.one * LevelEditorState.CellSize;
            }

            if (instance == null)
            {
                return null;
            }

            if (!entry.UsesPrefabDirectly)
            {
                instance.name = entry.DisplayName;
                SurfaceTileBlock block =
                    instance.GetComponent<SurfaceTileBlock>();
                if (block != null)
                {
                    block.RegenerateBlockId();
                    EditorUtility.SetDirty(block);
                }
            }

            GameObject requestedParent =
                LevelEditorState.GenerationParent;
            Transform root = requestedParent != null &&
                             requestedParent.scene == instance.scene
                ? requestedParent.transform
                : GetOrCreateRoot().transform;
            instance.transform.SetParent(root, true);
            instance.transform.position = CellToWorld(cell);

            LevelEditorPlacedBlock placed =
                instance.GetComponent<LevelEditorPlacedBlock>() ??
                Undo.AddComponent<LevelEditorPlacedBlock>(instance);
            placed.Configure(
                cell,
                entry.DisplayName,
                entry.Color,
                !entry.UsesPrefabDirectly,
                false,
                LevelEditorState.CellSize);
            if (!entry.UsesPrefabDirectly)
            {
                LevelEditorDecorationService.ApplyToPlacedBlock(
                    instance,
                    entry);
            }

            Undo.RegisterCreatedObjectUndo(instance, "放置关卡方块");
            MarkDirty(instance.scene);
            Selection.activeGameObject = instance;
            return placed;
        }

        internal static LevelEditorPlacedBlock PlaceProp(
            LevelEditorPropEntry entry,
            Vector2Int cell)
        {
            if (entry == null ||
                entry.Prefab == null ||
                cell.x < -512 ||
                cell.y < -512 ||
                cell.x > 512 ||
                cell.y > 512)
            {
                return null;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(entry.Prefab, TargetScene())
                    as GameObject;
            if (instance == null)
            {
                return null;
            }

            LevelEditorComponentOverrideUtility.ApplyOverrides(
                instance,
                entry.ComponentValueOverrides);
            GameObject requestedParent =
                LevelEditorState.GenerationParent;
            Transform root = requestedParent != null &&
                             requestedParent.scene == instance.scene
                ? requestedParent.transform
                : GetOrCreateRoot().transform;
            instance.transform.SetParent(root, true);
            instance.transform.position = CellToWorld(cell);

            LevelEditorPlacedBlock placed =
                instance.GetComponent<LevelEditorPlacedBlock>() ??
                Undo.AddComponent<LevelEditorPlacedBlock>(instance);
            placed.Configure(
                cell,
                entry.DisplayName,
                Color.white,
                false,
                true,
                LevelEditorState.CellSize);
            if (instance.GetComponent<Project.Mechanisms.DoorController>() != null)
            {
                instance.transform.localScale *= Mathf.Max(.05f, LevelEditorState.CellSize);
                placed.SetSizeCells(new Vector2Int(1, 2));
            }
            Undo.RegisterCreatedObjectUndo(instance, "放置关卡道具");
            MarkDirty(instance.scene);
            Selection.activeGameObject = instance;
            return placed;
        }

        internal static void ApplyPropOverridesToPlacedBlocks(
            LevelEditorPropEntry entry)
        {
            if (entry == null ||
                string.IsNullOrEmpty(entry.DisplayName))
            {
                return;
            }

            UnityEngine.SceneManagement.Scene targetScene =
                TargetScene();
            IReadOnlyList<LevelEditorPlacedBlock> blocks =
                ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true);
            int changed = 0;
            for (int index = 0; index < blocks.Count; index++)
            {
                LevelEditorPlacedBlock block = blocks[index];
                if (!block.IsProp ||
                    block.gameObject.scene != targetScene ||
                    block.EntryName != entry.DisplayName)
                {
                    continue;
                }

                Undo.RecordObject(block.gameObject, "应用道具组件数值");
                LevelEditorComponentOverrideUtility
                    .RestorePrefabDefaults(
                        block.gameObject,
                        entry.Prefab);
                LevelEditorComponentOverrideUtility.ApplyOverrides(
                    block.gameObject,
                    entry.ComponentValueOverrides);
                EditorUtility.SetDirty(block.gameObject);
                changed++;
            }

            if (changed > 0)
            {
                MarkDirty(targetScene);
            }
        }

        internal static bool EraseAt(Vector2Int cell)
        {
            LevelEditorPlacedBlock block = FindAt(cell);
            if (block == null)
            {
                return false;
            }

            MarkDirty(block.gameObject.scene);
            Undo.DestroyObjectImmediate(block.gameObject);
            return true;
        }

        internal static int RemovePlacedBlocksForEntry(
            LevelEditorBlockEntry entry)
        {
            if (entry == null ||
                string.IsNullOrEmpty(entry.DisplayName))
            {
                return 0;
            }

            UnityEngine.SceneManagement.Scene targetScene =
                TargetScene();
            IReadOnlyList<LevelEditorPlacedBlock> blocks =
                ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true);
            var targets = new List<LevelEditorPlacedBlock>();
            for (int index = 0; index < blocks.Count; index++)
            {
                LevelEditorPlacedBlock block = blocks[index];
                if (!block.IsProp &&
                    block.gameObject.scene == targetScene &&
                    block.EntryName == entry.DisplayName)
                {
                    targets.Add(block);
                }
            }

            if (targets.Count == 0)
            {
                return 0;
            }

            Undo.SetCurrentGroupName("删除栏目并清理已放置方块");
            for (int index = 0; index < targets.Count; index++)
            {
                Undo.DestroyObjectImmediate(
                    targets[index].gameObject);
            }

            MarkDirty(targetScene);
            return targets.Count;
        }

        internal static int SelectedBlockCount
        {
            get
            {
                int count = 0;
                foreach (GameObject gameObject in Selection.gameObjects)
                    if (gameObject != null && gameObject.GetComponent<LevelEditorPlacedBlock>() != null)
                        count++;
                return count;
            }
        }

        internal static bool MergeSelected(out string message)
        {
            var blocks = new List<LevelEditorPlacedBlock>();
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected == null) continue;
                LevelEditorPlacedBlock block = selected.GetComponent<LevelEditorPlacedBlock>();
                if (block != null) blocks.Add(block);
            }
            if (blocks.Count < 2)
            {
                message = "至少选择两个单元方块。";
                return false;
            }

            LevelEditorPlacedBlock first = blocks[0];
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(first.gameObject);
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            var occupied = new HashSet<Vector2Int>();
            foreach (LevelEditorPlacedBlock block in blocks)
            {
                if (block.IsProp || block.SizeCells != Vector2Int.one ||
                    block.EntryName != first.EntryName ||
                    block.transform.parent != first.transform.parent ||
                    block.gameObject.scene != first.gameObject.scene ||
                    block.transform.childCount != 0 ||
                    block.GetComponent<SurfaceTileBlock>() != null ||
                    block.transform.rotation != first.transform.rotation ||
                    block.transform.localScale != first.transform.localScale ||
                    !Mathf.Approximately(block.transform.position.z, first.transform.position.z) ||
                    PrefabUtility.GetCorrespondingObjectFromSource(block.gameObject) != prefab ||
                    !CanMergeBlock(block) ||
                    block.GetComponent<MeshRenderer>().sharedMaterial != first.GetComponent<MeshRenderer>().sharedMaterial ||
                    block.GetComponent<BoxCollider>().center != first.GetComponent<BoxCollider>().center ||
                    block.GetComponent<BoxCollider>().size != first.GetComponent<BoxCollider>().size ||
                    Vector2.Distance(block.transform.position, CellToWorld(block.Cell)) > .002f)
                {
                    message = "仅能合并同种、同尺寸、同层级且没有附加内容的单元方块。";
                    return false;
                }
                occupied.Add(block.Cell);
                minX = Mathf.Min(minX, block.Cell.x);
                minY = Mathf.Min(minY, block.Cell.y);
                maxX = Mathf.Max(maxX, block.Cell.x);
                maxY = Mathf.Max(maxY, block.Cell.y);
            }
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            if (occupied.Count != blocks.Count || occupied.Count != width * height)
            {
                message = "选区必须是无空格的矩形。";
                return false;
            }
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    if (!occupied.Contains(new Vector2Int(x, y)))
                    {
                        message = "选区必须是无空格的矩形。";
                        return false;
                    }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("合并方块");
            GameObject merged = prefab != null
                ? PrefabUtility.InstantiatePrefab(prefab, first.gameObject.scene) as GameObject
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (merged == null)
            {
                message = "无法创建合并方块。";
                return false;
            }
            Undo.RegisterCreatedObjectUndo(merged, "合并方块");
            merged.transform.SetParent(first.transform.parent, true);
            merged.name = first.gameObject.name;
            merged.transform.rotation = first.transform.rotation;
            merged.transform.localScale = Vector3.Scale(first.transform.localScale, new Vector3(width, height, 1f));
            merged.transform.position = CellToWorld(new Vector2Int(minX, minY)) +
                new Vector3((width - 1) * LevelEditorState.CellSize * .5f,
                    (height - 1) * LevelEditorState.CellSize * .5f,
                    first.transform.position.z - CellToWorld(first.Cell).z);
            LevelEditorPlacedBlock placed = merged.GetComponent<LevelEditorPlacedBlock>() ??
                Undo.AddComponent<LevelEditorPlacedBlock>(merged);
            placed.Configure(new Vector2Int(minX, minY), first.EntryName,
                first.EntryColor, prefab == null, false,
                LevelEditorState.CellSize);
            placed.SetSizeCells(new Vector2Int(width, height));
            if (prefab == null)
                merged.GetComponent<MeshRenderer>().sharedMaterial = first.GetComponent<MeshRenderer>().sharedMaterial;
            foreach (LevelEditorPlacedBlock block in blocks)
                Undo.DestroyObjectImmediate(block.gameObject);
            Selection.activeGameObject = merged;
            MarkDirty(merged.scene);
            Undo.CollapseUndoOperations(undoGroup);
            message = $"已合并为 {width} × {height} 方块。";
            return true;
        }

        private static bool CanMergeBlock(LevelEditorPlacedBlock block)
        {
            if (block.GetComponent<MeshRenderer>() == null ||
                block.GetComponent<BoxCollider>() == null ||
                block.GetComponent<MeshFilter>() == null) return false;
            foreach (MonoBehaviour behaviour in block.GetComponents<MonoBehaviour>())
                if (behaviour != null &&
                    !(behaviour is LevelEditorPlacedBlock) &&
                    !(behaviour is IColorObject)) return false;
            return true;
        }

        internal static void MigratePlacedBlocks()
        {
            IReadOnlyList<LevelEditorPlacedBlock> blocks =
                ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true);
            float scale = Mathf.Max(.05f, LevelEditorState.CellSize);
            for (int index = 0; index < blocks.Count; index++)
            {
                LevelEditorPlacedBlock block = blocks[index];
                bool isPlainPrimitive =
                    PrefabUtility.GetPrefabInstanceStatus(
                        block.gameObject) == PrefabInstanceStatus.NotAPrefab &&
                    block.transform.childCount == 0 &&
                    block.GetComponent<MeshFilter>() != null;
                if (isPlainPrimitive)
                {
                    Vector3 localScale = block.transform.localScale;
                    if (Mathf.Approximately(localScale.x, .9f) &&
                        Mathf.Approximately(localScale.y, .9f) &&
                        Mathf.Approximately(localScale.z, .9f))
                    {
                        Undo.RecordObject(
                            block.transform,
                            "贴合关卡白模到格子");
                        block.transform.localScale = Vector3.one * scale;
                        EditorUtility.SetDirty(block.transform);
                    }
                }

                if (block.HasColorData)
                {
                    if (!Mathf.Approximately(
                            block.CellWorldSize,
                            scale))
                    {
                        Undo.RecordObject(
                            block,
                            "同步关卡格子尺寸");
                        block.SetCellWorldSize(scale);
                        EditorUtility.SetDirty(block);
                    }

                    block.ApplyEntryColor();
                    continue;
                }

                LevelEditorBlockEntry entry =
                    FindEntry(block.EntryName);
                Undo.RecordObject(block, "迁移关卡方块外观");
                block.Configure(
                    block.Cell,
                    block.EntryName,
                    entry != null ? entry.Color : Color.white,
                    entry != null
                        ? !entry.UsesPrefabDirectly
                        : isPlainPrimitive,
                    false,
                    scale);
                EditorUtility.SetDirty(block);
                if (block.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(
                        block.gameObject.scene);
                }
            }
        }

        private static LevelEditorBlockEntry FindEntry(string entryName)
        {
            LevelEditorPalette palette = LevelEditorState.Palette;
            if (palette == null || string.IsNullOrEmpty(entryName))
            {
                return null;
            }

            for (int index = 0; index < palette.Entries.Count; index++)
            {
                LevelEditorBlockEntry entry = palette.Entries[index];
                if (entry != null &&
                    entry.DisplayName == entryName)
                {
                    return entry;
                }
            }

            return null;
        }

        internal static LevelEditorPlacedBlock SelectedPlacedBlock()
        {
            return Selection.activeGameObject != null
                ? Selection.activeGameObject
                    .GetComponentInParent<LevelEditorPlacedBlock>()
                : null;
        }

        internal static void RemoveSelected()
        {
            LevelEditorPlacedBlock selected = SelectedPlacedBlock();
            if (selected != null)
            {
                MarkDirty(selected.gameObject.scene);
                Undo.DestroyObjectImmediate(selected.gameObject);
            }
        }

        internal static void ReplaceSelected(
            LevelEditorBlockEntry entry)
        {
            LevelEditorPlacedBlock selected = SelectedPlacedBlock();
            if (selected == null || entry == null)
            {
                return;
            }

            Vector2Int cell = selected.Cell;
            EraseAt(cell);
            Place(entry, cell);
        }

        internal static Vector3 CellToWorld(Vector2Int cell)
        {
            float size = LevelEditorState.CellSize;
            return new Vector3(
                (cell.x + .5f) * size,
                (cell.y + .5f) * size,
                0f);
        }

        internal static Vector2Int WorldToCell(Vector3 position)
        {
            float size = Mathf.Max(.0001f, LevelEditorState.CellSize);
            return new Vector2Int(
                Mathf.FloorToInt(position.x / size),
                Mathf.FloorToInt(position.y / size));
        }

        private static GameObject GetOrCreateRoot()
        {
            UnityEngine.SceneManagement.Scene targetScene = TargetScene();
            foreach (GameObject root in targetScene.GetRootGameObjects())
            {
                if (root.name == RootName) return root;
            }

            GameObject created = new GameObject(RootName);
            if (created.scene != targetScene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, targetScene);
            Undo.RegisterCreatedObjectUndo(created, "创建关卡编辑内容根节点");
            return created;
        }

        private static UnityEngine.SceneManagement.Scene TargetScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        }

        private static void MarkDirty(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            SceneView.RepaintAll();
        }
    }
}
