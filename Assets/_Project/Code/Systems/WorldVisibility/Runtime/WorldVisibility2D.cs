using System;
using System.Collections.Generic;
using Project.LevelEditor;
using Project.Mechanisms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.WorldVisibility
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WorldVisibility2D : MonoBehaviour
    {
        static readonly List<WorldVisibility2D> active = new List<WorldVisibility2D>();
        [SerializeField] public Camera targetCamera;
        [SerializeField] public Transform viewer;
        [SerializeField] public bool findPlayer = true;
        [SerializeField] public bool previewInEditor;
        [SerializeField] public bool connectedSpaceVisibility = true;
        [SerializeField, Min(.01f)] public float cellWorldSize = 1;
        [SerializeField, Min(.5f)] public float sightRadiusCells = 24;
        [SerializeField, Min(.5f)] public float playerLightRadiusCells = 12;
        [SerializeField, Range(0,1)] public float ambient = .04f;
        [SerializeField, Range(0,1)] public float lightCore = .45f;
        [SerializeField, Range(0,4)] public float shadowSoftnessCells = 1.5f;
        [SerializeField, Range(.25f,1)] public float maskResolution = .5f;
        [SerializeField, Range(1,8)] public int maxExtraLights = 4;
        [SerializeField, Min(0f)] public float defaultOcclusionFadeDuration = .35f;
        [SerializeField, Min(0f)] public float connectedTransitionDuration = .25f;
        [SerializeField] public bool respectPlayerSight = true;
        readonly VisibilityGeometry.Polygon sight = new VisibilityGeometry.Polygon();
        ConnectedSpaceGeometry connected = new ConnectedSpaceGeometry();
        ConnectedSpaceGeometry connectedPrevious = new ConnectedSpaceGeometry();
        readonly VisibilityGeometry.Polygon playerLight = new VisibilityGeometry.Polygon();
        readonly List<VisibilityGeometry.Segment> walls = new List<VisibilityGeometry.Segment>(4096);
        readonly List<VisibilityGeometry.Segment> dynamicEdges = new List<VisibilityGeometry.Segment>(128);
        readonly List<VisibilityGeometry.Segment> nearby = new List<VisibilityGeometry.Segment>(512);
        readonly List<Vector3> wallVertices = new List<Vector3>(4096);
        readonly List<int> wallIndices = new List<int>(8192);
        readonly Dictionary<object,int> suppressionRequests = new Dictionary<object,int>();
        readonly Dictionary<Vector2Int,List<int>> buckets = new Dictionary<Vector2Int,List<int>>();
        readonly HashSet<int> queried = new HashSet<int>();
        readonly HashSet<BoxCollider> seenBoxes = new HashSet<BoxCollider>();
        readonly List<BoxCollider> boxBuffer = new List<BoxCollider>();
        readonly List<VisibilityLight2D> visibleLights = new List<VisibilityLight2D>(8);
        DoorController[] doors;
        int solidRevision = -1, revision, dynamicHash, configurationHash;
        Vector2 lastViewer = new Vector2(float.PositiveInfinity,0);
        float nextPlayerSearch;
        float nextConnectedBuild;
        int connectedBuildCount;
        Mesh wallMesh;
        float occlusionStrength = 1;
        float fadeFrom = 1;
        float fadeTarget = 1;
        float fadeElapsed;
        float fadeDuration;
        float connectedBlend = 1;
        float connectedBlendStartTime;
        float connectedBlendDuration;
        int connectedRevision = -1;
        Vector2Int connectedCell = new Vector2Int(int.MinValue, int.MinValue);
        float connectedRadius, connectedStep;
        const float BucketSize = 8;
        public int CachedSegments => walls.Count;
        public int GeometryRevision => revision;
        public Mesh WallMesh => wallMesh;
        public float OcclusionStrength => occlusionStrength;
        public Mesh SightMesh => connectedSpaceVisibility ? connected.Mesh : sight.Mesh;
        public Mesh PreviousSightMesh => connectedSpaceVisibility ? connectedPrevious.Mesh : null;
        public float ConnectedBlend => connectedBlend;
        public int ConnectedBuildCount => connectedBuildCount;
        public int ConnectedCellCount => connected.ReachableCells;
        internal Mesh PlayerMesh => playerLight.Mesh;
        internal IReadOnlyList<VisibilityLight2D> Lights => visibleLights;
        public static void InvalidateAll() { foreach (var item in active) if (item) item.solidRevision = -1; }
        public static WorldVisibility2D FindForScene(Scene scene)
        {
            foreach (var item in active)
                if (item && item.isActiveAndEnabled && item.gameObject.scene == scene) return item;
            return null;
        }
        public IDisposable SuppressOcclusion(object owner = null,float fadeOutDuration = -1,float fadeInDuration = -1)
        {
            owner ??= this;
            suppressionRequests.TryGetValue(owner,out int count);
            suppressionRequests[owner] = count + 1;
            float duration = fadeOutDuration >= 0 ? fadeOutDuration : defaultOcclusionFadeDuration;
            SetOcclusionTarget(0,duration);
            return new SuppressionLease(this,owner,fadeInDuration);
        }
        public void SetOcclusionEnabled(bool enabled,float fadeDurationOverride = -1)
        {
            float duration = fadeDurationOverride >= 0 ? fadeDurationOverride : defaultOcclusionFadeDuration;
            SetOcclusionTarget(enabled ? 1 : 0,duration);
        }
        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            SceneManager.sceneLoaded += SceneLoaded; solidRevision = -1;
            if (!targetCamera) targetCamera = GetComponent<Camera>();
        }
        void OnDisable()
        {
            active.Remove(this); SceneManager.sceneLoaded -= SceneLoaded;
            sight.Dispose(); connected.Dispose(); connectedPrevious.Dispose(); playerLight.Dispose(); walls.Clear(); buckets.Clear(); visibleLights.Clear();
            if (wallMesh) { if (Application.isPlaying) Destroy(wallMesh); else DestroyImmediate(wallMesh); wallMesh = null; }
            suppressionRequests.Clear(); occlusionStrength = 1; fadeElapsed = fadeDuration = 0; fadeFrom = fadeTarget = 1;
            connectedRevision = -1;
            connectedBuildCount = 0; connectedBlend = 1; connectedBlendDuration = 0;
        }
        void OnValidate() { solidRevision = -1; cellWorldSize = Mathf.Max(.01f,cellWorldSize); }
        void SceneLoaded(Scene scene, LoadSceneMode mode) => solidRevision = -1;
        public static WorldVisibility2D ForCamera(Camera camera)
        {
            foreach (var item in active)
                if (item && item.isActiveAndEnabled && item.targetCamera == camera && (Application.isPlaying || item.previewInEditor)) return item;
            return null;
        }
        void Update()
        {
            AdvanceConnectedTransition();
            if (fadeDuration <= 0) { occlusionStrength = fadeTarget; return; }
            if (fadeElapsed >= fadeDuration) { occlusionStrength = fadeTarget; return; }
            fadeElapsed = Mathf.Min(fadeDuration,fadeElapsed+Time.unscaledDeltaTime);
            occlusionStrength = Mathf.SmoothStep(fadeFrom,fadeTarget,fadeElapsed/fadeDuration);
        }
        public bool Prepare(Camera camera)
        {
            if (!camera || !camera.orthographic) return false;
            if (!viewer && findPlayer && Time.realtimeSinceStartup >= nextPlayerSearch)
            { var player = GameObject.FindGameObjectWithTag("Player"); if (player) viewer = player.transform; nextPlayerSearch = Time.realtimeSinceStartup + 1; }
            if (!viewer) return false; // Missing references must never black out an unrelated camera.
            if (solidRevision != LevelEditorPlacedBlock.SolidRevision) RebuildWalls();
            RebuildDynamicEdges();
            if (connectedSpaceVisibility)
            {
                AdvanceConnectedTransition();
                float step = Mathf.Max(.01f, cellWorldSize) * .5f;
                float radius = Mathf.Clamp(sightRadiusCells, 2, 48) * cellWorldSize;
                Vector2 p = viewer.position;
                var cell = new Vector2Int(Mathf.FloorToInt(p.x / step), Mathf.FloorToInt(p.y / step));
                bool changed = connectedRevision != revision || radius != connectedRadius || step != connectedStep;
                bool teleported = ((Vector2)cell - connectedCell).sqrMagnitude > 16f;
                if (changed || cell != connectedCell && (teleported || Time.realtimeSinceStartup >= nextConnectedBuild))
                {
                    Query(p, radius + step * 2);
                    ConnectedSpaceGeometry previous = connected;
                    connected = connectedPrevious;
                    connectedPrevious = previous;
                    connected.Build(p, radius, step, nearby);
                    connectedBuildCount++;
                    connectedBlend = connectedPrevious.Mesh ? 0f : 1f;
                    connectedBlendStartTime = Time.unscaledTime;
                    connectedBlendDuration = Mathf.Max(0f,connectedTransitionDuration);
                    connectedCell = cell; connectedRadius = radius; connectedStep = step; connectedRevision = revision;
                    nextConnectedBuild = Time.realtimeSinceStartup + .1f;
                }
                // No ray-cast light polygons or per-lamp work in connected-space mode.
                visibleLights.Clear();
                return true;
            }
            int config;
            unchecked { config = (sightRadiusCells.GetHashCode() * 397 ^ playerLightRadiusCells.GetHashCode()) * 397 ^ cellWorldSize.GetHashCode(); }
            Vector2 position = viewer.position;
            if ((position-lastViewer).sqrMagnitude > .000001f || configurationHash != config)
            {
                Query(position,Mathf.Max(sightRadiusCells,playerLightRadiusCells) * cellWorldSize);
                sight.Build(position,sightRadiusCells * cellWorldSize,nearby,Color.white);
                playerLight.Build(position,playerLightRadiusCells * cellWorldSize,nearby,Color.white);
                lastViewer = position; configurationHash = config;
            }
            visibleLights.Clear();
            foreach (var light in VisibilityLight2D.Active)
            {
                if (!light || !light.isActiveAndEnabled || light.gameObject.scene != gameObject.scene) continue;
                if (visibleLights.Count >= maxExtraLights) break;
                float radius = Mathf.Max(.1f,light.radiusCells) * cellWorldSize;
                if (((Vector2)light.transform.position-position).sqrMagnitude > Mathf.Pow(radius+sightRadiusCells*cellWorldSize,2)) continue;
                if (light.lastRevision != revision || light.lastPosition != light.transform.position ||
                    light.lastRadius != radius || light.lastTint != light.tint || light.lastIntensity != light.intensity)
                {
                    Query(light.transform.position,radius);
                    light.polygon.Build(light.transform.position,radius,nearby,light.tint * light.intensity);
                    light.lastPosition = light.transform.position; light.lastRevision = revision;
                    light.lastRadius = radius; light.lastTint = light.tint; light.lastIntensity = light.intensity;
                }
                visibleLights.Add(light);
            }
            return true;
        }
        void RebuildWalls()
        {
            walls.Clear(); buckets.Clear(); seenBoxes.Clear();
            wallVertices.Clear(); wallIndices.Clear();
            IReadOnlyList<LevelEditorPlacedBlock> blocks = Application.isPlaying ? LevelEditorPlacedBlock.ActiveInstances
                : FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            foreach (var block in blocks)
            {
                if (!block || block.IsProp || block.gameObject.scene != gameObject.scene) continue;
                boxBuffer.Clear(); block.GetComponentsInChildren(false,boxBuffer);
                foreach (var box in boxBuffer) if (seenBoxes.Add(box))
                {
                    VisibilityGeometry.AddBox(box,walls);
                    AddWallFill(box);
                }
            }
            // Each actual collider is retained: nonrectangular unions and holes are not filled.
            VisibilityGeometry.Compact(walls);
            for (int i = 0; i < walls.Count; i++)
            {
                var edge = walls[i]; Vector2 min = Vector2.Min(edge.a,edge.b), max = Vector2.Max(edge.a,edge.b);
                for (int x = Mathf.FloorToInt(min.x/BucketSize); x <= Mathf.FloorToInt(max.x/BucketSize); x++)
                    for (int y = Mathf.FloorToInt(min.y/BucketSize); y <= Mathf.FloorToInt(max.y/BucketSize); y++)
                    {
                        var key = new Vector2Int(x,y);
                        if (!buckets.TryGetValue(key,out var list)) buckets.Add(key,list = new List<int>());
                        list.Add(i);
                    }
            }
            doors = FindObjectsByType<DoorController>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            wallMesh ??= new Mesh { name = "墙体保留遮罩", hideFlags = HideFlags.HideAndDontSave };
            wallMesh.Clear(); wallMesh.SetVertices(wallVertices); wallMesh.SetTriangles(wallIndices,0);
            wallMesh.RecalculateBounds();
            solidRevision = LevelEditorPlacedBlock.SolidRevision; revision++; lastViewer.x = float.PositiveInfinity;
        }
        void AddWallFill(BoxCollider box)
        {
            if (!box || !box.enabled || box.isTrigger || !box.gameObject.activeInHierarchy) return;
            Vector3 half = box.size * .5f, center = box.center;
            Vector3 p = box.transform.TransformPoint(center + new Vector3(-half.x,-half.y,0));
            Vector3 q = box.transform.TransformPoint(center + new Vector3(half.x,-half.y,0));
            Vector3 r = box.transform.TransformPoint(center + new Vector3(half.x,half.y,0));
            Vector3 s = box.transform.TransformPoint(center + new Vector3(-half.x,half.y,0));
            int start = wallVertices.Count;
            wallVertices.Add(p); wallVertices.Add(q); wallVertices.Add(r); wallVertices.Add(s);
            wallIndices.Add(start); wallIndices.Add(start+1); wallIndices.Add(start+2);
            wallIndices.Add(start); wallIndices.Add(start+2); wallIndices.Add(start+3);
        }
        void RebuildDynamicEdges()
        {
            dynamicEdges.Clear(); seenBoxes.Clear();
            if (doors != null) foreach (var door in doors)
            {
                if (!door || !door.isActiveAndEnabled || door.gameObject.scene != gameObject.scene || door.State != DoorState.Locked) continue;
                boxBuffer.Clear(); door.GetComponentsInChildren(false,boxBuffer);
                foreach (var box in boxBuffer) if (seenBoxes.Add(box)) VisibilityGeometry.AddBox(box,dynamicEdges);
            }
            foreach (var occluder in VisibilityOccluder2D.Active)
                if (occluder && occluder.gameObject.scene == gameObject.scene && occluder.shapes != null)
                    foreach (var box in occluder.shapes) if (box && seenBoxes.Add(box)) VisibilityGeometry.AddBox(box,dynamicEdges);
            int hash = 17;
            unchecked { foreach (var edge in dynamicEdges) hash = (hash*31+edge.a.GetHashCode())*31+edge.b.GetHashCode(); }
            if (hash != dynamicHash) { dynamicHash = hash; revision++; lastViewer.x = float.PositiveInfinity; }
        }
        void Query(Vector2 position, float radius)
        {
            nearby.Clear(); queried.Clear();
            for (int x = Mathf.FloorToInt((position.x-radius)/BucketSize); x <= Mathf.FloorToInt((position.x+radius)/BucketSize); x++)
                for (int y = Mathf.FloorToInt((position.y-radius)/BucketSize); y <= Mathf.FloorToInt((position.y+radius)/BucketSize); y++)
                    if (buckets.TryGetValue(new Vector2Int(x,y),out var indices)) foreach (int index in indices)
                        if (queried.Add(index) && InRange(walls[index],position,radius)) nearby.Add(walls[index]);
            foreach (var edge in dynamicEdges) if (InRange(edge,position,radius)) nearby.Add(edge);
        }
        static bool InRange(VisibilityGeometry.Segment edge, Vector2 point, float radius)
        {
            Vector2 delta = edge.b-edge.a;
            float t = delta.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(point-edge.a,delta)/delta.sqrMagnitude);
            return (point-edge.a-t*delta).sqrMagnitude <= radius*radius;
        }
        void SetOcclusionTarget(float value,float duration)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value-fadeTarget) < .0001f && fadeDuration > 0) return;
            fadeFrom = occlusionStrength; fadeTarget = value;
            fadeElapsed = 0; fadeDuration = Mathf.Max(0,duration);
            if (fadeDuration <= 0) occlusionStrength = fadeTarget;
        }
        void AdvanceConnectedTransition()
        {
            if (connectedBlendDuration <= 0 || connectedBlend >= 1) { connectedBlend = 1; return; }
            connectedBlend = Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-connectedBlendStartTime)/connectedBlendDuration));
        }
        void ReleaseSuppression(object owner,float fadeInDuration)
        {
            if (!suppressionRequests.TryGetValue(owner,out int count)) return;
            if (count > 1) { suppressionRequests[owner] = count - 1; return; }
            suppressionRequests.Remove(owner);
            if (suppressionRequests.Count == 0)
                SetOcclusionTarget(1,fadeInDuration >= 0 ? fadeInDuration : defaultOcclusionFadeDuration);
        }
        sealed class SuppressionLease : IDisposable
        {
            WorldVisibility2D system; object owner; float fadeInDuration;
            public SuppressionLease(WorldVisibility2D system,object owner,float fadeInDuration)
            { this.system = system; this.owner = owner; this.fadeInDuration = fadeInDuration; }
            public void Dispose()
            {
                if (!system) return;
                system.ReleaseSuppression(owner,fadeInDuration); system = null; owner = null;
            }
        }
    }
}
