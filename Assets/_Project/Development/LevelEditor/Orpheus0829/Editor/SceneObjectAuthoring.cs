using System;
using System.Collections.Generic;
using System.Linq;
using Project.BlockFeatures;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    internal enum SceneObjectChannel
    {
        TerrainSolid,
        WaterVolume,
        GameplayProp,
        MarkerTrigger,
        DecorationObject,
    }

    internal sealed class SceneObjectRecord
    {
        internal LevelEditorPlacedBlock block;
        internal SceneObjectChannel channel;
        internal string authorId;
        internal string regionId;
        internal readonly List<Vector2Int> cells = new List<Vector2Int>();
    }

    /// <summary>Read-only, revisioned Scene index. Rebuilding never writes author data.</summary>
    internal sealed class SceneObjectIndex
    {
        private readonly Dictionary<Vector2Int, List<SceneObjectRecord>> byCell = new();
        private readonly Dictionary<(Vector2Int, SceneObjectChannel), List<SceneObjectRecord>> byChannel = new();
        private readonly Dictionary<LevelEditorPlacedBlock, SceneObjectRecord> byBlock = new();
        private readonly Dictionary<string, List<SceneObjectRecord>> byAuthor = new(StringComparer.Ordinal);
        private readonly List<SceneObjectRecord> all = new();
        internal IReadOnlyList<SceneObjectRecord> All => all;

        internal static SceneObjectIndex Build(Scene scene)
        {
            var result = new SceneObjectIndex();
            if (!scene.IsValid() || !scene.isLoaded) return result;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LevelEditorPlacedBlock block in root.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
            {
                var record = new SceneObjectRecord
                {
                    block = block,
                    authorId = block.AuthoringId ?? string.Empty,
                    channel = Classify(block),
                    regionId = block.GetComponent<RoomResetObject>()?.RegionId ?? string.Empty,
                };
                Vector2Int size = block.SizeCells;
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    Vector2Int cell = block.Cell + new Vector2Int(x, y);
                    if (!block.ContainsCell(cell)) continue;
                    record.cells.Add(cell);
                    if (!result.byCell.TryGetValue(cell, out List<SceneObjectRecord> list))
                        result.byCell[cell] = list = new List<SceneObjectRecord>();
                    list.Add(record);
                    var key = (cell, record.channel);
                    if (!result.byChannel.TryGetValue(key, out List<SceneObjectRecord> channelList))
                        result.byChannel[key] = channelList = new List<SceneObjectRecord>();
                    channelList.Add(record);
                }
                result.all.Add(record);
                result.byBlock[block] = record;
                if (!string.IsNullOrEmpty(record.authorId))
                {
                    if (!result.byAuthor.TryGetValue(record.authorId, out List<SceneObjectRecord> group))
                        result.byAuthor[record.authorId] = group = new List<SceneObjectRecord>();
                    group.Add(record);
                }
            }
            return result;
        }

        internal IReadOnlyList<SceneObjectRecord> At(Vector2Int cell, SceneObjectChannel? channel = null) =>
            channel.HasValue
                ? byChannel.TryGetValue((cell, channel.Value), out List<SceneObjectRecord> channelList) ? channelList : Array.Empty<SceneObjectRecord>()
                : byCell.TryGetValue(cell, out List<SceneObjectRecord> list) ? list : Array.Empty<SceneObjectRecord>();
        internal SceneObjectRecord For(LevelEditorPlacedBlock block) => block != null && byBlock.TryGetValue(block, out SceneObjectRecord record) ? record : null;

        internal IReadOnlyList<SceneObjectRecord> Group(SceneObjectRecord record) =>
            record != null && !string.IsNullOrEmpty(record.authorId) && byAuthor.TryGetValue(record.authorId, out List<SceneObjectRecord> group)
                ? group : record != null ? new[] { record } : Array.Empty<SceneObjectRecord>();

        private static SceneObjectChannel Classify(LevelEditorPlacedBlock block)
        {
            if (!block.IsProp) return SceneObjectChannel.TerrainSolid;
            if ((block.EntryName ?? string.Empty).Contains("水", StringComparison.OrdinalIgnoreCase) ||
                (block.EntryName ?? string.Empty).Contains("Water", StringComparison.OrdinalIgnoreCase)) return SceneObjectChannel.WaterVolume;
            MonoBehaviour[] behaviours = block.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;
                string name = behaviour.GetType().Name;
                if (name.Contains("Water", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Buoyancy", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Current", StringComparison.OrdinalIgnoreCase)) return SceneObjectChannel.WaterVolume;
            }
            if (block.GetComponentInChildren<UnityEngine.Playables.PlayableDirector>(true) != null ||
                behaviours.Any(item => item != null && (item.GetType().Name.Contains("Trigger", StringComparison.OrdinalIgnoreCase) ||
                    item.GetType().Name.Contains("Marker", StringComparison.OrdinalIgnoreCase) ||
                    item.GetType().Name.Contains("Respawn", StringComparison.OrdinalIgnoreCase))))
                return SceneObjectChannel.MarkerTrigger;
            if (block.GetComponent<RoomResetObject>() == null && behaviours.Length <= 1)
                return SceneObjectChannel.DecorationObject;
            return SceneObjectChannel.GameplayProp;
        }
    }

    internal readonly struct SceneDeleteReference
    {
        internal readonly Component owner;
        internal readonly string propertyPath;
        internal SceneDeleteReference(Component owner, string propertyPath) { this.owner = owner; this.propertyPath = propertyPath; }
        public override string ToString() => owner != null ? $"{owner.name}.{propertyPath}" : propertyPath;
    }

    [InitializeOnLoad]
    internal static class SceneObjectAuthoring
    {
        private static readonly Dictionary<int, SceneObjectIndex> Cache = new();
        internal static event Action Changed;
        internal static bool RebindAcrossRegions { get; set; }
        internal static SceneObjectChannel? ActiveChannel { get; set; }
        static SceneObjectAuthoring()
        {
            EditorApplication.hierarchyChanged += Invalidate;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorSceneManager.sceneOpened += (_, _) => Invalidate();
            EditorSceneManager.sceneClosed += _ => Invalidate();
            ObjectChangeEvents.changesPublished += OnObjectChanges;
        }
        internal static void NotifyChanged()
        {
            Invalidate();
            SceneMoveToolSync.ResetTracking();
            Changed?.Invoke();
        }
        private static void Invalidate() => Cache.Clear();
        private static void OnObjectChanges(ref ObjectChangeEventStream stream) => Invalidate();
        private static void OnUndoRedo()
        {
            Invalidate();
            Changed?.Invoke();
        }

        internal static SceneObjectIndex Read(Scene scene)
        {
            if (!scene.IsValid()) return SceneObjectIndex.Build(scene);
            if (!Cache.TryGetValue(scene.handle, out SceneObjectIndex index)) Cache[scene.handle] = index = SceneObjectIndex.Build(scene);
            return index;
        }

        internal static Transform ResolveParent(Scene scene, string regionId)
        {
            if (!scene.IsValid()) return null;
            if (!string.IsNullOrEmpty(regionId))
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                    if (candidate.name.EndsWith("[" + regionId + "]", StringComparison.Ordinal)) return candidate;
            }
            GameObject requested = LevelEditorState.GenerationParent;
            if (requested != null && requested.scene == scene) return requested.transform;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "__PlanningMapGenerated" || root.name == "__LevelEditorContent") return root.transform;
            return null;
        }

        internal static bool MoveGroup(SceneObjectRecord record, Vector2Int delta, bool rebind, out string reason)
        {
            reason = string.Empty;
            if (record?.block == null || delta == Vector2Int.zero) return false;
            Scene scene = record.block.gameObject.scene;
            RoomLifecycleManager manager = SceneRegionAuthoring.FindManager(scene);
            SceneObjectIndex index = Read(scene);
            IReadOnlyList<SceneObjectRecord> group = index.Group(record);
            var groupBlocks = new HashSet<LevelEditorPlacedBlock>(group.Select(item => item.block));
            foreach (SceneObjectRecord member in group)
            foreach (Vector2Int source in member.cells)
            foreach (SceneObjectRecord occupied in index.At(source + delta, member.channel))
                if (!groupBlocks.Contains(occupied.block)) { reason = $"目标格 {source + delta} 的 {member.channel} 通道已有对象。"; return false; }

            SceneGridSpace grid = manager != null ? SceneGridSpace.For(manager) : SceneGridSpace.World(record.block.CellWorldSize);
            foreach (SceneObjectRecord member in group)
            {
                string target = manager != null ? manager.FindRegion(grid.SceneCellCenter(member.block.Cell + delta)) : string.Empty;
                RoomResetObject reset = member.block.GetComponent<RoomResetObject>();
                if (reset != null && !string.IsNullOrEmpty(reset.RegionId) && target != reset.RegionId && !rebind)
                { reason = "移动会跨越房间/通道；请显式启用“跨区重新归属”。"; return false; }
            }

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("移动场景关卡对象组");
            foreach (SceneObjectRecord member in group)
            {
                LevelEditorPlacedBlock block = member.block;
                RoomResetObject reset = block.GetComponent<RoomResetObject>();
                Undo.RecordObjects(reset != null ? new UnityEngine.Object[] { block.transform, block, reset } : new UnityEngine.Object[] { block.transform, block }, "移动场景关卡对象组");
                Vector3 position = block.transform.position;
                Vector3 next = grid.SceneCellCenter(block.Cell + delta, grid.LocalDepth(position));
                block.transform.position = next;
                block.SetAuthoredCell(block.Cell + delta);
                if (reset != null && manager != null && rebind)
                {
                    string region = manager.FindRegion(next);
                    reset.Bind(manager, region ?? string.Empty);
                    Transform parent = ResolveParent(scene, region);
                    if (parent != null && block.transform.parent != parent) block.transform.SetParent(parent, true);
                }
                EditorUtility.SetDirty(block);
                if (reset != null) EditorUtility.SetDirty(reset);
            }
            manager?.RebuildOwnership();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            NotifyChanged();
            return true;
        }

        internal static IReadOnlyList<SceneDeleteReference> FindReverseReferences(IReadOnlyCollection<LevelEditorPlacedBlock> targets)
        {
            var result = new List<SceneDeleteReference>();
            if (targets == null || targets.Count == 0) return result;
            var objects = new HashSet<UnityEngine.Object>();
            foreach (LevelEditorPlacedBlock target in targets)
                if (target != null)
                {
                    objects.Add(target.gameObject); objects.Add(target.transform);
                    foreach (Component component in target.GetComponentsInChildren<Component>(true)) if (component != null) objects.Add(component);
                }
            Scene scene = targets.First(item => item != null).gameObject.scene;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (DoorController door in root.GetComponentsInChildren<DoorController>(true))
                    if (!objects.Contains(door) && door.Buttons.Any(button => button != null && objects.Contains(button)))
                        result.Add(new SceneDeleteReference(door, "buttons"));
                foreach (DoorButton button in root.GetComponentsInChildren<DoorButton>(true))
                    if (!objects.Contains(button) && button.Owner != null && objects.Contains(button.Owner))
                        result.Add(new SceneDeleteReference(button, "owner"));
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || objects.Contains(component) || component is Transform) continue;
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool enter = true;
                while (property.NextVisible(enter))
                {
                    enter = false;
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && objects.Contains(property.objectReferenceValue))
                        if (!result.Any(item => item.owner == component && item.propertyPath == property.propertyPath))
                            result.Add(new SceneDeleteReference(component, property.propertyPath));
                }
            }
            return result;
        }

        internal static bool DeleteGroup(SceneObjectRecord record, bool unlink, out string reason)
        {
            reason = string.Empty;
            if (record?.block == null) return false;
            SceneObjectIndex index = Read(record.block.gameObject.scene);
            LevelEditorPlacedBlock[] blocks = index.Group(record).Select(item => item.block).Where(item => item != null).Distinct().ToArray();
            IReadOnlyList<SceneDeleteReference> references = FindReverseReferences(blocks);
            if (references.Count > 0 && !unlink)
            {
                reason = $"该逻辑组仍被 {references.Count} 个字段引用；请使用“解除引用并删除”。";
                return false;
            }
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(unlink ? "解除引用并删除对象组" : "删除场景关卡对象组");
            if (unlink)
            {
                UnityEngine.Object[] targetState = blocks.SelectMany(block => block.GetComponentsInChildren<Component>(true).Cast<UnityEngine.Object>())
                    .Concat(blocks.Select(block => (UnityEngine.Object)block.gameObject))
                    .Concat(references.Select(reference => (UnityEngine.Object)reference.owner))
                    .Where(value => value != null).Distinct().ToArray();
                Undo.RegisterCompleteObjectUndo(targetState, "解除引用并删除对象组");
            }
            if (unlink) UnlinkReferences(references, blocks);
            Scene scene = record.block.gameObject.scene;
            foreach (LevelEditorPlacedBlock block in blocks) Undo.DestroyObjectImmediate(block.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            NotifyChanged();
            return true;
        }

        private static void UnlinkReferences(IReadOnlyList<SceneDeleteReference> references, IReadOnlyCollection<LevelEditorPlacedBlock> targets)
        {
            foreach (SceneDeleteReference reference in references)
            {
                if (reference.owner == null) continue;
                Undo.RecordObject(reference.owner, "解除场景对象引用");
                if (reference.owner is DoorController door)
                {
                    door.SetButtons(door.Buttons.Where(button => button != null && !targets.Any(target => button.transform.IsChildOf(target.transform))).ToArray());
                    continue;
                }
                if (reference.owner is DoorButton button && targets.Any(target => button.Owner != null && button.Owner.transform.IsChildOf(target.transform)))
                { button.SetOwner(null); continue; }
                if (reference.owner is MovingPlatformFeature platform)
                {
                    if (platform.CrankController != null && targets.Any(target => platform.CrankController.transform.IsChildOf(target.transform))) platform.BindCrank(null);
                    else platform.BindSignalSources(platform.LinkedControls.Where(source => source != null && !targets.Any(target => source.transform.IsChildOf(target.transform))).ToArray());
                    continue;
                }
                if (reference.owner is PlatformMount mount && mount.Platform != null && targets.Any(target => mount.Platform.transform.IsChildOf(target.transform)))
                { mount.Bind(null); continue; }
                var serialized = new SerializedObject(reference.owner);
                SerializedProperty property = serialized.FindProperty(reference.propertyPath);
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference) property.objectReferenceValue = null;
                serialized.ApplyModifiedProperties();
            }
        }

        internal static bool BindDoor(DoorController door, DoorButton button, out string reason)
        {
            reason = string.Empty;
            if (!SameEditableScene(door, button)) { reason = "请选择同一 Scene 中的门和按钮。"; return false; }
            if (button.Owner == door && door.Buttons.Contains(button)) return true;
            DoorController previous = button.Owner;
            if (previous != null && previous.gameObject.scene != door.gameObject.scene) { reason = "按钮的原门位于其他 Scene，请先检查引用。"; return false; }
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("绑定门与按钮");
            var changed = new List<UnityEngine.Object> { door, button };
            if (previous != null && previous != door) changed.Add(previous);
            Undo.RecordObjects(changed.ToArray(), "绑定门与按钮");
            door.SetButtons(door.Buttons.Concat(new[] { button }).Distinct().ToArray());
            foreach (var value in changed)
            {
                EditorUtility.SetDirty(value);
                PrefabUtility.RecordPrefabInstancePropertyModifications(value);
            }
            EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
            Undo.CollapseUndoOperations(undo); NotifyChanged(); return true;
        }

        private static bool SameEditableScene(Component consumer, Component source) =>
            consumer != null && source != null && consumer != source &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorUtility.IsPersistent(consumer) && !EditorUtility.IsPersistent(source) &&
            consumer.gameObject.scene.IsValid() && consumer.gameObject.scene == source.gameObject.scene;

        private static bool IsMechanismPair(Component consumer, Component source) =>
            consumer is MovingPlatformFeature && (source is CrankPlatformFeature || source is MonoBehaviour and IMechanismSignalSource) ||
            consumer is CrankPlatformFeature && source is MechanismBaseFeature ||
            consumer is PlatformMount && source is MovingPlatformFeature;

        private static bool HasMechanismBinding(Component consumer, Component source) =>
            consumer is MovingPlatformFeature platform && (source is CrankPlatformFeature crank
                ? platform.IsCrankControlled && platform.CrankController == crank
                : new SerializedObject(platform).FindProperty("driveMode").intValue == (int)MovingPlatformDriveMode.SignalControlled && platform.LinkedControls.Contains(source as MonoBehaviour)) ||
            consumer is CrankPlatformFeature handle && handle.BrakeBase == source ||
            consumer is PlatformMount mount && mount.Platform == source;

        internal static bool TryResolveBinding(GameObject target, GameObject input, out Component consumer, out Component source, out string reason)
        {
            consumer = source = null;
            reason = "先在场景中点选目标，再设置来源物体。";
            if (target == null || input == null) return false;
            if (target == input) { reason = "目标和来源不能是同一个物体。"; return false; }
            if (!target.scene.IsValid() || target.scene != input.scene || EditorUtility.IsPersistent(target) || EditorUtility.IsPersistent(input))
            { reason = "目标和来源必须是同一 Scene 中的实例。"; return false; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { reason = "请退出运行模式后连接机关。"; return false; }
            var matches = (from a in target.GetComponents<Component>()
                           from b in input.GetComponents<Component>()
                           where a != null && b != null && (a is DoorController && b is DoorButton || IsMechanismPair(a,b))
                           select (a,b)).ToArray();
            if (matches.Length != 1)
            {
                reason = matches.Length == 0 ? "类型不匹配：目标用门、平台、曲柄或附着物；来源用相应按钮、信号源、基座或平台。"
                    : "这两个物体有多组可连接组件，请在实例 Inspector 中明确设置。";
                return false;
            }
            (consumer,source) = matches[0];
            if (consumer is DoorController door && source is DoorButton button ? button.Owner == door && door.Buttons.Contains(button) : HasMechanismBinding(consumer,source))
            { reason = "这两个物体已经连接。"; return false; }
            reason = target.name + " ← " + input.name;
            return true;
        }

        internal static bool BindMechanism(Component consumer, Component source, out string reason)
        {
            reason = string.Empty;
            if (!SameEditableScene(consumer, source)) { reason = "请选择同一 Scene 中的消费者与信号源。"; return false; }
            if (!IsMechanismPair(consumer,source)) { reason = "当前消费者与来源类型不匹配。"; return false; }
            if (HasMechanismBinding(consumer,source)) return true;
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("绑定机关关系");
            Undo.RecordObjects(new UnityEngine.Object[] { consumer, source }, "绑定机关关系");
            if (consumer is MovingPlatformFeature platform && source is CrankPlatformFeature crank) platform.BindCrank(crank);
            else if (consumer is MovingPlatformFeature signalPlatform && source is MonoBehaviour signal && source is IMechanismSignalSource)
                signalPlatform.BindSignalSources(signalPlatform.LinkedControls.Concat(new[] { signal }).Distinct().ToArray());
            else if (consumer is CrankPlatformFeature crankFeature && source is MechanismBaseFeature mechanismBase) crankFeature.BindBase(mechanismBase);
            else if (consumer is PlatformMount mount && source is MovingPlatformFeature mountedPlatform) mount.Bind(mountedPlatform);
            EditorUtility.SetDirty(consumer); PrefabUtility.RecordPrefabInstancePropertyModifications(consumer);
            EditorSceneManager.MarkSceneDirty(consumer.gameObject.scene);
            Undo.CollapseUndoOperations(undo); NotifyChanged(); return true;
        }
    }

    [InitializeOnLoad]
    internal static class SceneMoveToolSync
    {
        private static readonly Dictionary<int, Vector3> LastPositions = new();
        private static bool syncing;
        static SceneMoveToolSync()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Undo.undoRedoPerformed += () => LastPositions.Clear();
        }

        internal static void ResetTracking() => LastPositions.Clear();

        private static void Poll()
        {
            if (syncing || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (GameObject selected in Selection.gameObjects)
            {
                LevelEditorPlacedBlock block = selected != null ? selected.GetComponent<LevelEditorPlacedBlock>() : null;
                if (block == null) continue;
                int id = block.GetInstanceID();
                Vector3 current = block.transform.position;
                if (!LastPositions.TryGetValue(id, out Vector3 previous)) { LastPositions[id] = current; continue; }
                if ((current - previous).sqrMagnitude < .0000001f) continue;
                LastPositions[id] = current;
                Sync(block, previous);
            }
        }

        private static void Sync(LevelEditorPlacedBlock block, Vector3 previous)
        {
            syncing = true;
            try
            {
                RoomLifecycleManager manager = SceneRegionAuthoring.FindManager(block.gameObject.scene);
                SceneGridSpace grid = manager != null ? SceneGridSpace.For(manager) : SceneGridSpace.World(block.CellWorldSize);
                Vector2Int nextCell = grid.WorldToSceneCell(block.transform.position);
                Vector2Int delta = nextCell - block.Cell;
                block.transform.position = previous;
                if (delta == Vector2Int.zero) { LastPositions[block.GetInstanceID()] = previous; return; }
                SceneObjectRecord record = SceneObjectAuthoring.Read(block.gameObject.scene).For(block);
                if (!SceneObjectAuthoring.MoveGroup(record, delta, SceneObjectAuthoring.RebindAcrossRegions, out string reason))
                {
                    LastPositions[block.GetInstanceID()] = previous;
                    SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(string.IsNullOrEmpty(reason) ? "移动已回滚。" : reason));
                    return;
                }
                foreach (SceneObjectRecord member in SceneObjectAuthoring.Read(block.gameObject.scene).Group(record))
                    if (member.block != null) LastPositions[member.block.GetInstanceID()] = member.block.transform.position;
            }
            finally { syncing = false; }
        }
    }
}
