using System;
using System.Collections.Generic;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.ObjectArtwork;
using Project.Subtitles;
using Project.WorldItems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.BlockFeatures.Editor
{
    /// <summary>
    /// Read-only edit-time artwork. Cached meshes belong to this Editor service,
    /// never to a Scene/Prefab; neither gameplay Awake nor the runtime installers
    /// are needed. Object-change events catch planner replacement and Undo, not
    /// just the original scene-open event.
    /// </summary>
    [InitializeOnLoad]
    public static class ArtworkScenePreview
    {
        private sealed class Part
        {
            internal Mesh mesh;
            internal Material material;
        }
        private sealed class Entry
        {
            internal GameObject owner;
            internal Bounds bounds;
            internal readonly List<Part> parts = new();
            internal readonly List<Renderer> hidden = new();
            internal readonly List<Vector3[]> pickQuads = new();
            internal bool water;
            internal int parentArtwork;
        }
        private readonly struct WaterCell : IEquatable<WaterCell>
        {
            internal readonly int scene, x, y;
            internal WaterCell(int scene, int x, int y) { this.scene = scene; this.x = x; this.y = y; }
            public override int GetHashCode() => (scene * 397 ^ x) * 397 ^ y;
            public bool Equals(WaterCell other) => scene == other.scene && x == other.x && y == other.y;
            public override bool Equals(object obj) => obj is WaterCell other && Equals(other);
        }

        private static readonly Dictionary<int, Entry> entries = new();
        private static readonly Dictionary<Texture, Material> materials = new();
        private static readonly HashSet<int> pending = new();
        private static readonly HashSet<WaterCell> waterCells = new();
        private static readonly Dictionary<WaterCell, int> waterCellOwners = new();
        private static readonly List<int> expired = new();
        private static readonly Plane[] planes = new Plane[6];
        private static bool queued, processing, discoverHierarchy;
        public static int CachedOwnerCount => entries.Count;
        public static int RebuildCount { get; private set; }
        public static int HierarchyDiscoveryCount { get; private set; }

        static ArtworkScenePreview()
        {
            ObjectChangeEvents.changesPublished += Changed;
            EditorSceneManager.sceneOpened += (_, _) => QueueAll();
            EditorSceneManager.newSceneCreated += (_, _, _) => QueueAll();
            EditorSceneManager.sceneClosing += (scene, _) => RemoveScene(scene);
            EditorApplication.projectChanged += QueueAll;
            // ObjectChangeEvents only promises Undo-aware author changes. Planner
            // reconstruction/PrefabUtility.InstantiatePrefab can create native
            // objects without publishing that stream. Hierarchy notifications are
            // coalesced structural events; discover only new owners and retain all
            // existing meshes instead of rebuilding the map on each notification.
            EditorApplication.hierarchyChanged += QueueDiscovery;
            Undo.undoRedoPerformed += QueueAll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Clear();
                else if (state == PlayModeStateChange.EnteredEditMode) QueueAll();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
            SceneView.duringSceneGui += DrawScene;
            EditorApplication.delayCall += QueueAll;
        }

        private static bool IsEditing => !EditorApplication.isPlayingOrWillChangePlaymode;
        private static bool IsAuthorObject(GameObject owner) => owner != null && owner.scene.IsValid() &&
            owner.scene.isLoaded && !EditorUtility.IsPersistent(owner) &&
            !EditorSceneManager.IsPreviewScene(owner.scene) && (owner.hideFlags & HideFlags.DontSaveInEditor) == 0;

        private static bool IsArtworkOwner(GameObject owner) => owner != null && owner.GetComponent<SubtitleTrigger>() == null &&
            (MechanismArtworkPresentation.Supports(owner) || owner.GetComponent<WorldObjectSpritePresentation>() != null ||
             owner.GetComponent<RespawnAnchorFeature>() != null || owner.GetComponent<BouncePlantFeature>() != null ||
             owner.GetComponent<ClimbableVineFeature>() != null || owner.GetComponent<WaterVisualFeature>() != null ||
             owner.GetComponent<SubtitleTrigger>() != null);

        private static void QueueAll()
        {
            if (!IsEditing) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
                foreach (var root in scene.GetRootGameObjects()) pending.Add(root.GetInstanceID());
            }
            Schedule();
        }
        private static void QueueDiscovery()
        {
            if (!IsEditing) return;
            discoverHierarchy = true; Schedule();
        }
        public static void QueueObject(GameObject owner)
        {
            if (!IsEditing || !IsAuthorObject(owner)) return;
            pending.Add(owner.GetInstanceID()); Schedule();
        }
        private static void DiscoverNewOwners()
        {
            if (!discoverHierarchy) return;
            discoverHierarchy = false; HierarchyDiscoveryCount++;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        GameObject owner = transform.gameObject;
                        if (!IsAuthorObject(owner) || !IsArtworkOwner(owner) || entries.ContainsKey(owner.GetInstanceID())) continue;
                        pending.Add(owner.GetInstanceID());
                        if (owner.TryGetComponent<LadderSonFeature>(out var segment) && segment.ArtworkRoot != segment)
                            pending.Add(segment.ArtworkRoot.gameObject.GetInstanceID());
                    }
            }
        }
        private static void Schedule()
        {
            if (queued || !IsEditing) return;
            queued = true;
            EditorApplication.delayCall += Flush;
        }
        private static void Changed(ref ObjectChangeEventStream stream)
        {
            if (!IsEditing || processing) return;
            bool deleted = false;
            for (int i = 0; i < stream.length; i++)
            {
                int id;
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                        stream.GetCreateGameObjectHierarchyEvent(i, out var created); id = created.instanceId; break;
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var changed); id = changed.instanceId; break;
                    case ObjectChangeKind.ChangeGameObjectParent:
                        stream.GetChangeGameObjectParentEvent(i, out var moved); id = moved.instanceId; break;
                    case ObjectChangeKind.ChangeGameObjectStructure:
                        stream.GetChangeGameObjectStructureEvent(i, out var structure); id = structure.instanceId; break;
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        stream.GetChangeGameObjectStructureHierarchyEvent(i, out var hierarchy); id = hierarchy.instanceId; break;
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        deleted = true; continue;
                    case ObjectChangeKind.UpdatePrefabInstances:
                        stream.GetUpdatePrefabInstancesEvent(i, out var prefabs);
                        foreach (int instance in prefabs.instanceIds) pending.Add(instance);
                        continue;
                    default: continue;
                }
                var value = EditorUtility.InstanceIDToObject(id);
                GameObject go = value as GameObject;
                if (go == null && value is Component component) go = component.gameObject;
                if (!IsAuthorObject(go)) continue;
                // forceRenderingOff is this service's transient visibility lease.
                // Renderer notifications from that lease must not recursively
                // tear down/recreate the same preview on the next Editor tick.
                if (value is Renderer) continue;
                // A changed direction child, collider, or generated visual belongs
                // to its nearest artwork owner; don't rescan the entire map.
                Transform nearest = go.transform;
                while (nearest != null && !IsArtworkOwner(nearest.gameObject)) nearest = nearest.parent;
                pending.Add(nearest != null ? nearest.gameObject.GetInstanceID() : go.GetInstanceID());
            }
            if (pending.Count > 0 || deleted) Schedule();
        }

        public static void RefreshOpenScenes()
        {
            QueueAll();
            Flush();
        }
        public static void RefreshObject(GameObject owner)
        {
            if (!IsEditing || !IsAuthorObject(owner)) return;
            pending.Add(owner.GetInstanceID()); Flush();
        }

        private static void Flush()
        {
            EditorApplication.delayCall -= Flush; queued = false;
            if (!IsEditing || processing) return;
            processing = true;
            try
            {
                DiscoverNewOwners();
                expired.Clear();
                foreach (var pair in entries)
                    if (!IsAuthorObject(pair.Value.owner) || !IsArtworkOwner(pair.Value.owner)) expired.Add(pair.Key);
                foreach (int id in expired)
                {
                    if (entries.TryGetValue(id, out var previous) && previous.parentArtwork != 0) pending.Add(previous.parentArtwork);
                    Remove(id);
                }
                var ids = new List<int>(pending); pending.Clear();
                var owners = new HashSet<GameObject>();
                foreach (int id in ids)
                {
                    var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                    if (!IsAuthorObject(go)) continue;
                    foreach (var transform in go.GetComponentsInChildren<Transform>(true))
                        if (IsAuthorObject(transform.gameObject) && IsArtworkOwner(transform.gameObject)) owners.Add(transform.gameObject);
                }
                // Changing any water cell can change its neighbours' surface/bottom
                // choice. Only cached water owners are revisited, never all Scene
                // objects on every repaint or drag event.
                bool waterChanged = expired.Count > 0;
                foreach (var owner in owners) if (owner.GetComponent<WaterVisualFeature>() != null) waterChanged = true;
                if (waterChanged)
                    foreach (var value in entries.Values) if (value.water && value.owner != null) owners.Add(value.owner);
                foreach (var owner in owners)
                {
                    int id = owner.GetInstanceID(); Remove(id);
                    var entry = new Entry { owner = owner, water = owner.GetComponent<WaterVisualFeature>() != null };
                    entries[id] = entry;
                }
                RebuildWaterCells();
                foreach (var owner in owners) if (owner != null && entries.TryGetValue(owner.GetInstanceID(), out var entry)) Build(entry);
            }
            finally { processing = false; }
            SceneView.RepaintAll();
        }

        private static float Cell(GameObject owner) => Mathf.Max(.01f, GridCellSizeUtility.Resolve(owner.transform));
        private static Bounds Footprint(GameObject owner, float cell)
        {
            // A button's thin pressure sensor is not its complete artwork cell.
            // Keep the same centre/pixel scale as runtime for all four facings.
            if (owner.GetComponent<Project.Mechanisms.DoorButton>() != null || owner.GetComponent<ButtonLockFeature>() != null)
                return new Bounds(owner.transform.position, Vector3.one * cell);
            if (owner.TryGetComponent<LevelEditorMergedShape>(out var shape)) return shape.WorldBounds;
            var box = owner.GetComponent<BoxCollider>();
            if (owner.TryGetComponent<ContactCube>(out _))
                foreach (var candidate in owner.GetComponentsInChildren<BoxCollider>(true))
                    if (!candidate.isTrigger) { box = candidate; break; }
            Bounds bounds = box != null ? box.bounds : new Bounds(owner.transform.position,
                owner.TryGetComponent<LevelEditorPlacedBlock>(out var placed) ? new Vector3(placed.SizeCells.x * cell, placed.SizeCells.y * cell, cell) : Vector3.one * cell);
            if (owner.TryGetComponent<NeutralLadder>(out _) && owner.TryGetComponent<LevelEditorPlacedBlock>(out var ladderPlaced))
            {
                bounds.size = new Vector3(cell, ladderPlaced.SizeCells.y * cell, Mathf.Max(cell, bounds.size.z));
                // Old ladders have a bottom-cell pivot rather than a scaled root.
                if (box != null && Mathf.Approximately(box.size.y, 1f) && Mathf.Approximately(Mathf.Abs(owner.transform.lossyScale.y), cell))
                    bounds.center = new Vector3(bounds.center.x, owner.transform.position.y + (ladderPlaced.SizeCells.y - 1) * .5f * cell, bounds.center.z);
            }
            return bounds;
        }
        private static IEnumerable<Bounds> Coverage(GameObject owner, Bounds fallback)
        {
            if (!owner.TryGetComponent<LevelEditorMergedShape>(out var shape)) { yield return fallback; yield break; }
            foreach (var rect in shape.Rectangles)
            {
                Bounds local = shape.RectBounds(rect);
                Vector3 center = owner.transform.TransformPoint(local.center), scale = owner.transform.lossyScale;
                yield return new Bounds(center, Vector3.Scale(local.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            }
        }
        private static WaterCell Key(GameObject owner, Vector3 center, float cell) =>
            new WaterCell(owner.scene.handle, Mathf.FloorToInt(center.x / cell), Mathf.FloorToInt(center.y / cell));
        private static void RebuildWaterCells()
        {
            waterCells.Clear(); waterCellOwners.Clear();
            foreach (var entry in entries.Values)
            {
                if (!entry.water || entry.owner == null || !entry.owner.activeInHierarchy) continue;
                float cell = Cell(entry.owner);
                foreach (var bounds in Coverage(entry.owner, Footprint(entry.owner, cell)))
                    for (int y = 0; y < Mathf.RoundToInt(bounds.size.y / cell); y++)
                        for (int x = 0; x < Mathf.RoundToInt(bounds.size.x / cell); x++)
                        {
                            var key = Key(entry.owner, bounds.min + new Vector3((x + .5f) * cell, (y + .5f) * cell, 0), cell);
                            waterCells.Add(key);
                            int id = entry.owner.GetInstanceID();
                            if (!waterCellOwners.TryGetValue(key, out int previous) || id < previous) waterCellOwners[key] = id;
                        }
            }
        }

        private static void Build(Entry entry)
        {
            GameObject owner = entry.owner;
            float cell = Cell(owner);
            Bounds bounds = entry.bounds = Footprint(owner, cell);
            var cells = new Vector2(bounds.size.x / cell, bounds.size.y / cell);
            var vine = owner.GetComponent<ClimbableVineFeature>();
            PlanningArtworkPreview art;
            if (vine != null)
            {
                // Pure authored ownership/height: never RestoreInitialGrowthLinks
                // here, since that is a gameplay mutation, not a preview query.
                if (vine.ArtworkRoot != vine)
                { entry.parentArtwork = vine.ArtworkRoot.gameObject.GetInstanceID(); HideRenderers(entry); return; }
                cells = vine.AuthoredArtworkCells;
                bounds = vine.AuthoredArtworkRootBounds;
                if (!ObjectArtworkPlanningPreview.TryGetVine(vine, cells, out art)) return;
            }
            else if (!ObjectArtworkPlanningPreview.TryGet(owner, cells, null, out art)) return;
            if (art.EditorOverlay) return; // Timeline owns an editor-only topmost gizmo.
            RebuildCount++;
            if (art.Hidden) { HideRenderers(entry); return; }
            if (art.Texture == null) return;
            if (entry.water)
            {
                foreach (var area in Coverage(owner, bounds))
                    for (int y = 0; y < Mathf.RoundToInt(area.size.y / cell); y++)
                        for (int x = 0; x < Mathf.RoundToInt(area.size.x / cell); x++)
                        {
                            Vector3 center = area.min + new Vector3((x + .5f) * cell, (y + .5f) * cell, 0);
                            var key = Key(owner, center, cell);
                            if (waterCellOwners.TryGetValue(key, out int waterOwner) && waterOwner != owner.GetInstanceID()) continue;
                            int kind = !waterCells.Contains(new WaterCell(key.scene, key.x, key.y + 1)) ? 0 :
                                !waterCells.Contains(new WaterCell(key.scene, key.x, key.y - 1)) ? 2 : 1;
                            if (ObjectArtworkPlanningPreview.TryGetWaterTile(kind, out var tile))
                                AddQuad(entry, tile, center.x - cell * .5f, center.y - cell * .5f, cell, cell, bounds.center.z + .02f, tile.UV);
                        }
            }
            else if (vine != null)
                AddQuad(entry, art, bounds.center.x - art.Cells.x * cell * .5f, bounds.min.y,
                    art.Cells.x * cell, art.Cells.y * cell, bounds.min.z - .015f * cell, art.UV);
            else
            {
                bool platform = owner.GetComponent<MovingPlatformFeature>() != null || owner.GetComponent<OneWayPlatform>() != null;
                foreach (var area in Coverage(owner, bounds))
                {
                    if (platform) AddPlatform(entry, art, area, cell);
                    else if (art.Tile) AddTiles(entry, art, area, cell);
                    else
                    {
                        float width = art.Cells.x * cell, height = art.Cells.y * cell;
                        float bottom = owner.GetComponent<ColorKeyPickup>() != null ? bounds.center.y - height * .5f : bounds.min.y;
                        var door = owner.GetComponent<DoorController>();
                        if (door != null && door.IsHorizontal)
                        {
                            Vector2 pivot = new Vector2(area.max.x, area.center.y);
                            AddQuad(entry, art, pivot.x - width * .5f, pivot.y, width, height,
                                area.min.z - .015f * cell, art.UV, pivot, 90);
                        }
                        else AddQuad(entry, art, area.center.x - width * .5f, bottom, width, height, area.min.z - .015f * cell, art.UV,
                                new Vector2(area.center.x, area.center.y));
                    }
                }
                AddDoorIndicators(entry, bounds, cell);
            }
            if (entry.parts.Count > 0)
            {
                CombineParts(entry);
                Bounds visible = new Bounds(owner.transform.TransformPoint(entry.parts[0].mesh.bounds.center), Vector3.zero);
                foreach (var part in entry.parts)
                {
                    Bounds local = part.mesh.bounds;
                    for (int i = 0; i < 8; i++) visible.Encapsulate(owner.transform.TransformPoint(new Vector3(
                        (i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z)));
                }
                entry.bounds = visible;
                HideRenderers(entry);
            }
            else if (entry.water)
            {
                // Fully overlapped water is represented by its owning water tile;
                // it must not fall back to the old cube renderer underneath it.
                HideRenderers(entry);
            }
        }
        private static void AddTiles(Entry entry, PlanningArtworkPreview art, Bounds bounds, float cell)
        {
            float width = Mathf.Max(.001f, art.Cells.x * cell), height = Mathf.Max(.001f, art.Cells.y * cell);
            for (float y = bounds.min.y; y < bounds.max.y - .0001f; y += height)
                for (float x = bounds.min.x; x < bounds.max.x - .0001f; x += width)
                {
                    float w = Mathf.Min(width, bounds.max.x - x), h = Mathf.Min(height, bounds.max.y - y);
                    Rect uv = art.UV; uv.width *= w / width; uv.height *= h / height;
                    AddQuad(entry, art, x, y, w, h, bounds.min.z - .015f * cell, uv, new Vector2(x + w * .5f, y + h * .5f));
                }
        }
        private static void AddPlatform(Entry entry, PlanningArtworkPreview art, Bounds bounds, float cell)
        {
            var platform = entry.owner.GetComponent<MovingPlatformFeature>();
            Vector2 dimensions = new Vector2(bounds.size.x, bounds.size.y);
            bool vertical = platform != null ? platform.IsArtworkVertical(dimensions)
                : ObjectArtworkLibrary.PlatformVisualIsVertical(dimensions);
            float length = vertical ? bounds.size.y : bounds.size.x;
            float natural = art.Cells.x * cell;
            var oneWay = entry.owner.GetComponent<OneWayPlatform>();
            var kind = oneWay != null ? oneWay.ArtworkKind : platform != null && platform.IsCrankControlled
                ? MechanismArtworkKind.CrankPlatform : MechanismArtworkKind.MovingPlatform;
            float thickness = PlatformArtworkLayout.VisibleThicknessCells(kind, art.Sprite, ObjectArtworkLibrary.Load()) * cell;
            float height = art.Cells.y * cell;
            for (float start = 0; start < length - .0001f; start += natural)
            {
                float width = Mathf.Min(natural, length - start);
                Rect uv = art.Sprite != null ? PlatformArtworkLayout.SliceUV(art.Sprite, width / cell) : art.UV;
                var rotated = new PlanningArtworkPreview(art, uv, art.Cells);
                if (!vertical)
                    AddQuad(entry, rotated, bounds.min.x + start, bounds.max.y - thickness, width, height, bounds.min.z - .015f * cell, uv);
                else
                {
                    Vector2 pivot = new Vector2(bounds.min.x + thickness, bounds.min.y + start);
                    AddQuad(entry, rotated, pivot.x, pivot.y, width, height, bounds.min.z - .015f * cell, uv, pivot, 90f);
                }
            }
        }
        private static void CombineParts(Entry entry)
        {
            if (entry.parts.Count < 2) return;
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var part in entry.parts)
            {
                if (!groups.TryGetValue(part.material, out var group)) groups[part.material] = group = new List<CombineInstance>();
                group.Add(new CombineInstance { mesh = part.mesh, transform = Matrix4x4.identity });
            }
            var combined = new List<Part>();
            foreach (var group in groups)
            {
                var mesh = new Mesh { name = "Editor artwork batch", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, false);
                combined.Add(new Part { mesh = mesh, material = group.Key });
            }
            foreach (var part in entry.parts) if (part.mesh != null) Object.DestroyImmediate(part.mesh);
            entry.parts.Clear(); entry.parts.AddRange(combined);
        }
        private static void AddDoorIndicators(Entry entry, Bounds bounds, float cell)
        {
            var door = entry.owner.GetComponent<DoorController>();
            var library = ObjectArtworkLibrary.Load();
            if (door == null || library == null || library.doorWhiteIndicators == null) return;
            for (int i = 0; i < Mathf.Min(door.Buttons.Count, library.doorWhiteIndicators.Length); i++)
            {
                if (door.Buttons[i] == null || library.doorWhiteIndicators[i] == null) continue;
                var art = new PlanningArtworkPreview(library.doorWhiteIndicators[i]);
                Vector3 offset = ObjectArtworkLibrary.DoorIndicatorOffset(i, door.Buttons.Count) * cell;
                if (door.IsHorizontal)
                {
                    Vector2 pivot = new Vector2(bounds.max.x, bounds.center.y);
                    AddQuad(entry, art, pivot.x - art.Cells.x * cell * .5f + offset.x, pivot.y + offset.y,
                        art.Cells.x * cell, art.Cells.y * cell, bounds.min.z - .016f * cell, art.UV, pivot, 90);
                }
                else AddQuad(entry, art, bounds.center.x - art.Cells.x * cell * .5f + offset.x, bounds.min.y + offset.y,
                    art.Cells.x * cell, art.Cells.y * cell, bounds.min.z - .016f * cell, art.UV);
            }
        }

        private static Material Material(Texture texture)
        {
            if (materials.TryGetValue(texture, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Hidden/Project/Editor/Artwork Preview");
            if (shader == null) return null;
            var material = new Material(shader) { name = "Editor artwork " + texture.name, hideFlags = HideFlags.HideAndDontSave };
            material.mainTexture = texture; materials[texture] = material; return material;
        }
        private static void AddQuad(Entry entry, PlanningArtworkPreview art, float x, float y, float width, float height,
            float z, Rect uv, Vector2? pivot = null, float? rotation = null)
        {
            Material material = Material(art.Texture);
            if (material == null) return;
            Vector3[] vertices = { new(x, y, z), new(x + width, y, z), new(x + width, y + height, z), new(x, y + height, z) };
            float angle = rotation ?? art.RotationDegrees;
            Vector3 center = pivot.HasValue ? new Vector3(pivot.Value.x, pivot.Value.y, z) : new Vector3(x + width * .5f, y + height * .5f, z);
            Quaternion turn = Quaternion.Euler(0, 0, angle);
            for (int i = 0; i < 4; i++) vertices[i] = entry.owner.transform.InverseTransformPoint(center + turn * (vertices[i] - center));
            entry.pickQuads.Add(vertices);
            Vector2[] coordinates = { new(uv.xMin, uv.yMin), new(uv.xMax, uv.yMin), new(uv.xMax, uv.yMax), new(uv.xMin, uv.yMax) };
            if (art.FlipX) for (int i = 0; i < coordinates.Length; i++) coordinates[i].x = uv.xMax + uv.xMin - coordinates[i].x;
            var mesh = new Mesh { name = "Editor artwork quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices; mesh.uv = coordinates; mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
            entry.parts.Add(new Part { mesh = mesh, material = material });
        }
        private static void HideRenderers(Entry entry)
        {
            foreach (var renderer in entry.owner.GetComponentsInChildren<Renderer>(true))
            {
                // An independently authored child owner has its own preview and
                // must not be muted by its parent (notably vine chain segments).
                Transform nearest = renderer.transform;
                while (nearest != null && !IsArtworkOwner(nearest.gameObject)) nearest = nearest.parent;
                if (nearest != entry.owner.transform || renderer.forceRenderingOff) continue;
                renderer.forceRenderingOff = true; entry.hidden.Add(renderer);
            }
        }
        private static void Remove(int id)
        {
            if (!entries.TryGetValue(id, out var entry)) return;
            foreach (var renderer in entry.hidden) if (renderer != null) renderer.forceRenderingOff = false;
            foreach (var part in entry.parts) if (part.mesh != null) Object.DestroyImmediate(part.mesh);
            entries.Remove(id);
        }
        private static void RemoveScene(Scene scene)
        {
            expired.Clear();
            foreach (var pair in entries) if (pair.Value.owner == null || pair.Value.owner.scene == scene) expired.Add(pair.Key);
            foreach (int id in expired) Remove(id);
        }
        public static void Clear()
        {
            EditorApplication.delayCall -= Flush; queued = false; discoverHierarchy = false; pending.Clear();
            foreach (var entry in entries.Values)
            {
                foreach (var renderer in entry.hidden) if (renderer != null) renderer.forceRenderingOff = false;
                foreach (var part in entry.parts) if (part.mesh != null) Object.DestroyImmediate(part.mesh);
            }
            entries.Clear(); waterCells.Clear(); waterCellOwners.Clear();
            foreach (var material in materials.Values) if (material != null) Object.DestroyImmediate(material);
            materials.Clear();
        }
        public static bool TryGetFirstMesh(GameObject owner, out Mesh mesh, out Material material)
        {
            mesh = null; material = null;
            if (owner == null || !entries.TryGetValue(owner.GetInstanceID(), out var entry) || entry.parts.Count == 0) return false;
            mesh = entry.parts[0].mesh; material = entry.parts[0].material; return true;
        }
        public static int PartCount(GameObject owner) => owner != null && entries.TryGetValue(owner.GetInstanceID(), out var entry) ? entry.parts.Count : 0;
        private static void DrawScene(SceneView view)
        {
            if (!IsEditing || view.camera == null) return;
            GeometryUtility.CalculateFrustumPlanes(view.camera, planes);
            Event current = Event.current;
            int control = GUIUtility.GetControlID(0x41525450, FocusType.Passive);
            if (!current.alt && !Tools.viewToolActive && GUIUtility.hotControl == 0 &&
                (current.type == EventType.Layout || current.type == EventType.MouseDown))
            {
                var owner = PickOwner(view, current.mousePosition);
                if (owner != null)
                {
                    // A tiny positive distance lets native transform handles win
                    // their exact-zero hit. No pointer capture, drag or navigation
                    // is owned by this preview service.
                    if (current.type == EventType.Layout) HandleUtility.AddControl(control, .01f);
                    else if (current.button == 0 && HandleUtility.nearestControl == control)
                    {
                        if (current.shift || current.control || current.command)
                        {
                            var selection = new List<Object>(Selection.objects);
                            if (!selection.Remove(owner)) selection.Add(owner);
                            Selection.objects = selection.ToArray();
                        }
                        else Selection.activeGameObject = owner;
                        current.Use();
                    }
                }
            }
            if (current.type == EventType.Repaint) { Draw(true, view.camera, true); Draw(false, view.camera, true); }
        }
        private static GameObject PickOwner(SceneView view, Vector2 point)
        {
            GameObject result = null; float closest = float.PositiveInfinity;
            var stage = StageUtility.GetCurrentStageHandle();
            foreach (var entry in entries.Values)
            {
                if (entry.owner == null || !entry.owner.activeInHierarchy || entry.pickQuads.Count == 0 ||
                    SceneVisibilityManager.instance.IsHidden(entry.owner) || !stage.Contains(entry.owner) ||
                    !GeometryUtility.TestPlanesAABB(planes, entry.bounds)) continue;
                Matrix4x4 matrix = entry.owner.transform.localToWorldMatrix;
                foreach (var quad in entry.pickQuads)
                {
                    Vector3 center = matrix.MultiplyPoint3x4((quad[0] + quad[2]) * .5f);
                    float depth = view.camera.WorldToViewportPoint(center).z;
                    if (depth < 0 || depth >= closest) continue;
                    Vector2 a = HandleUtility.WorldToGUIPoint(matrix.MultiplyPoint3x4(quad[0]));
                    Vector2 b = HandleUtility.WorldToGUIPoint(matrix.MultiplyPoint3x4(quad[1]));
                    Vector2 c = HandleUtility.WorldToGUIPoint(matrix.MultiplyPoint3x4(quad[2]));
                    Vector2 d = HandleUtility.WorldToGUIPoint(matrix.MultiplyPoint3x4(quad[3]));
                    if (Mathf.Abs(Cross(b - a, d - a)) < .001f) continue;
                    float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b);
                    float cd = Cross(d - c, point - c), da = Cross(a - d, point - d);
                    if ((ab >= 0 && bc >= 0 && cd >= 0 && da >= 0) || (ab <= 0 && bc <= 0 && cd <= 0 && da <= 0))
                    { closest = depth; result = entry.owner; }
                }
            }
            return result;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        /// <summary>Editor camera audit/render entry; does not add GameObjects.</summary>
        public static void DrawForCamera(Camera camera)
        {
            if (!IsEditing || camera == null) return;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            Draw(true, camera, false); Draw(false, camera, false);
        }
        private static void Draw(bool water, Camera camera, bool immediate)
        {
            foreach (var entry in entries.Values)
            {
                if (entry.water != water || entry.owner == null || !entry.owner.activeInHierarchy ||
                    SceneVisibilityManager.instance.IsHidden(entry.owner) || !StageUtility.GetCurrentStageHandle().Contains(entry.owner) ||
                    !GeometryUtility.TestPlanesAABB(planes, entry.bounds)) continue;
                foreach (var part in entry.parts)
                {
                    if (part.mesh == null || part.material == null) continue;
                    if (immediate) { if (part.material.SetPass(0)) Graphics.DrawMeshNow(part.mesh, entry.owner.transform.localToWorldMatrix); }
                    else Graphics.DrawMesh(part.mesh, entry.owner.transform.localToWorldMatrix, part.material, 0, camera, 0, null, ShadowCastingMode.Off, false);
                }
            }
        }
    }
}
