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

            Transform playerTransform = player.transform;
            Vector3 position = playerTransform.position;
            if (Mathf.Approximately(position.z, 0f))
            {
                return false;
            }

            Undo.RecordObject(
                playerTransform,
                "将玩家对齐到关卡平面");
            position.z = 0f;
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

            Undo.RecordObject(palette, "移除关卡方块栏目");
            palette.RemoveAt(index);
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
            AddManagedColorEntry(palette, "red", "红方块", new Color(1f, .25f, .25f));
            AddManagedColorEntry(palette, "blue", "蓝方块", new Color(.22f, .52f, 1f));
            AddManagedColorEntry(palette, "green", "绿方块", new Color(.25f, .8f, .38f));
            AddEntry(palette, "黑方块", new Color(.08f, .08f, .08f), null);
            AddEntry(palette, "白方块", new Color(.95f, .95f, .95f), null);
        }

        private static void AddManagedColorEntry(
            LevelEditorPalette palette,
            string typeId,
            string displayName,
            Color previewColor)
        {
            string path = "Assets/_Project/Content/ColorBlocks/Prefabs/ColorBlock_" +
                          typeId + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int index = AddEntry(
                palette,
                displayName,
                previewColor,
                prefab,
                LevelEditorBlockMode.Prefab);
            if (index < 0)
            {
                return;
            }

            palette.Entries[index].SetManagedColorType(typeId);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            if (!palette.Entries[index].HasValidManagedColorPrefab)
            {
                Debug.LogError($"关卡栏目“{displayName}”未找到有效的 {typeId} 颜色方块预制体：{path}");
            }
        }

        internal static bool TryValidateManagedColorEntry(
            LevelEditorBlockEntry entry,
            out string message)
        {
            message = string.Empty;
            if (entry == null || string.IsNullOrEmpty(entry.ManagedColorTypeId))
            {
                return true;
            }

            if (!entry.HasValidManagedColorPrefab)
            {
                message = $"栏目“{entry.DisplayName}”必须使用基础色为 {entry.ManagedColorTypeId} 的 ColorBlock 预制体。";
                return false;
            }

            ColorCatalog catalog = AssetDatabase.LoadAssetAtPath<ColorCatalog>(
                "Assets/_Project/Resources/ColorBlocks/ColorCatalog.asset");
            if (catalog == null || catalog.Find(entry.ManagedColorTypeId) == null)
            {
                message = $"颜色目录中没有栏目“{entry.DisplayName}”对应的 {entry.ManagedColorTypeId} 类型。";
                return false;
            }

            return true;
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
            LevelEditorPlacedBlock[] blocks =
                Object.FindObjectsByType<LevelEditorPlacedBlock>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < blocks.Length; index++)
            {
                if (blocks[index].Cell == cell)
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

            if (!LevelEditorPaletteService.TryValidateManagedColorEntry(
                    entry,
                    out string validationMessage))
            {
                Debug.LogError(validationMessage);
                return null;
            }

            LevelEditorPlacedBlock existing = FindAt(cell);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return existing;
            }

            GameObject instance;
            GameObject source = entry.UsesPrefabDirectly
                ? entry.Prefab
                : entry.SourcePrefab;
            if (source != null)
            {
                instance = PrefabUtility.InstantiatePrefab(source)
                    as GameObject;
            }
            else if (entry.UsesPrefabDirectly)
            {
                return null;
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
                !entry.UsesPrefabDirectly);
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
                PrefabUtility.InstantiatePrefab(entry.Prefab)
                    as GameObject;
            if (instance == null)
            {
                return null;
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
                Color.white,
                false,
                true);
            Undo.RegisterCreatedObjectUndo(instance, "放置关卡道具");
            MarkDirty(instance.scene);
            Selection.activeGameObject = instance;
            return placed;
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

        internal static void MigratePlacedBlocks()
        {
            LevelEditorPlacedBlock[] blocks =
                Object.FindObjectsByType<LevelEditorPlacedBlock>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            float scale = Mathf.Max(.05f, LevelEditorState.CellSize);
            for (int index = 0; index < blocks.Length; index++)
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
                        : isPlainPrimitive);
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
            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                return root;
            }

            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "创建关卡编辑内容根节点");
            return root;
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
