using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.Player;
using UnityEngine;
using Unity.Profiling;

namespace Project.Rooms
{
    [Serializable]
    public sealed class RoomRegion
    {
        public string id;
        public string label;
        public bool corridor;
        public List<Vector2Int> cells = new List<Vector2Int>();
    }

    /// <summary>World-map ownership, not a bounding rectangle or a physics trigger.</summary>
    [DefaultExecutionOrder(-1000), DisallowMultipleComponent]
    public sealed class RoomLifecycleManager : MonoBehaviour, IGridCellSizeProvider
    {
        [SerializeField] private List<RoomRegion> regions = new List<RoomRegion>();
        [SerializeField, Min(.01f)] private float cellSize = 1f;
        [SerializeField] private PlayerController player;
        [SerializeField] private bool autoRespawnOnDeath = true;
        [SerializeField, TextArea, HideInInspector] private string planningJson;
        private readonly Dictionary<Vector2Int, string> owners = new Dictionary<Vector2Int, string>();
        private readonly Dictionary<string, List<RoomResetObject>> members = new Dictionary<string, List<RoomResetObject>>();
        private readonly List<RoomResetObject> confinedItems = new List<RoomResetObject>();
        private readonly List<RoomResetObject> resetSnapshot = new List<RoomResetObject>();
        private static readonly ProfilerMarker ResetRegionMarker = new ProfilerMarker("Gameplay.Room.ResetRegion");
        private string currentRegionId;
        private bool resetting;
        public string CurrentRegionId => currentRegionId;
        public string PlanningJson => planningJson;
        public IReadOnlyList<RoomRegion> Regions => regions;
        public float CellSize => cellSize;
        public float CellWorldSize => cellSize;
        public int Revision { get; private set; }
        public event Action<string> RegionReset;
        public event Action<string, string> RegionChanged;
        private static readonly List<RoomLifecycleManager> Live = new List<RoomLifecycleManager>();

        public void Configure(List<RoomRegion> values, float size, string json)
        {
            regions = values ?? new List<RoomRegion>();
            cellSize = Mathf.Max(.01f, size);
            planningJson = json ?? string.Empty;
            RebuildOwnership();
        }

        private void Awake() => RebuildOwnership();
        private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        private void OnDisable() => Live.Remove(this);
        private void Start()
        {
            if (player == null) player = ProjectDiscovery.FindFirst<PlayerController>();
            // One startup pass. Inactive authored objects are registered too.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (LevelEditorPlacedBlock block in root.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
                    RegisterObject(block.gameObject);
                foreach (BlockRuntime block in root.GetComponentsInChildren<BlockRuntime>(true))
                    RegisterObject(block.gameObject);
                foreach (Project.ColorBlocks.UniversalColorBlock block in root.GetComponentsInChildren<Project.ColorBlocks.UniversalColorBlock>(true))
                    RegisterObject(block.gameObject);
                foreach (Project.Items.DropItemBase item in root.GetComponentsInChildren<Project.Items.DropItemBase>(true))
                    RegisterObject(item.gameObject);
                foreach (CarryableFeature item in root.GetComponentsInChildren<CarryableFeature>(true))
                    RegisterObject(item.gameObject);
                foreach (MovingPlatformFeature platform in root.GetComponentsInChildren<MovingPlatformFeature>(true))
                    RegisterObject(platform.gameObject);
                foreach (Project.Mechanisms.OneWayPlatform platform in root.GetComponentsInChildren<Project.Mechanisms.OneWayPlatform>(true))
                    RegisterObject(platform.gameObject);
                foreach (Project.Mechanisms.DoorButton button in root.GetComponentsInChildren<Project.Mechanisms.DoorButton>(true))
                    RegisterObject(button.gameObject);
                foreach (RoomResetObject member in root.GetComponentsInChildren<RoomResetObject>(true))
                    Register(member);
            }
            currentRegionId = player != null ? FindRegion(player.transform.position) : null;
        }

        private void RegisterObject(GameObject target)
        {
            if (FindRegion(target.transform.position) == null) return;
            var member = target.GetComponent<RoomResetObject>() ?? target.AddComponent<RoomResetObject>();
            Register(member);
        }

        public void RebuildOwnership()
        {
            Revision++;
            owners.Clear();
            foreach (RoomRegion region in regions)
            {
                if (region == null || string.IsNullOrEmpty(region.id)) continue;
                foreach (Vector2Int cell in region.cells)
                {
                    if (owners.TryGetValue(cell, out string other) && other != region.id)
                        Debug.LogError($"房间区域重叠：{other} / {region.id}，格 {cell}", this);
                    else owners[cell] = region.id;
                }
            }
        }

        public string FindRegion(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            var cell = new Vector2Int(Mathf.FloorToInt(local.x / cellSize), -Mathf.FloorToInt(local.y / cellSize) - 1);
            return owners.TryGetValue(cell, out string id) ? id : null;
        }

        public void Register(RoomResetObject member)
        {
            if (member == null) return;
            string owner = string.IsNullOrEmpty(member.RegionId) ? FindRegion(member.transform.position) : member.RegionId;
            if (string.IsNullOrEmpty(owner)) return;
            member.Bind(this, owner);
            if (!members.TryGetValue(owner, out var list)) members[owner] = list = new List<RoomResetObject>();
            if (!list.Contains(member)) list.Add(member);
            if ((member.GetComponent<Project.ContactCubes.ContactCube>() != null || member.GetComponent<CarryableFeature>() != null) && !confinedItems.Contains(member))
                confinedItems.Add(member);
        }

        public bool CanPlaceInAuthoredRegion(RoomResetObject member, Vector3 position) =>
            member == null || member.Manager != this || string.IsNullOrEmpty(member.RegionId) || FindRegion(position) == member.RegionId;

        private void LateUpdate()
        {
            if (Time.timeScale <= 0f) return;
            if (player != null)
            {
                if (player.IsDead)
                {
                    if (autoRespawnOnDeath && player.CanAutomaticallyRespawn) player.RequestRespawn();
                }
                else UpdatePlayerRegion(player);
            }
            EnforceItemBoundaries();
        }

        private void EnforceItemBoundaries()
        {
            if (resetting) return;
            // Only registered movable items; never scan the scene or wall geometry per frame.
            for (int i = confinedItems.Count - 1; i >= 0; i--)
            {
                var member = confinedItems[i];
                if (member == null) { confinedItems.RemoveAt(i); continue; }
                if (!member.gameObject.activeInHierarchy || CanPlaceInAuthoredRegion(member,member.transform.position)) continue;
                var carry = member.GetComponent<CarryableFeature>();
                var motion = member.GetComponent<Project.ContactCubes.ContactCubeMotion>();
                if ((carry != null && carry.IsCarryActive) || (motion != null && motion.IsSuspended)) player?.PrepareForRoomReset();
                member.RestorePose(); member.RestoreState(); Physics.SyncTransforms();
            }
        }

        public void UpdatePlayerRegion(PlayerController target)
        {
            if (resetting || target == null || target != player || target.IsDead) return;
            string next = FindRegion(target.transform.position);
            if (next == currentRegionId) return;
            string previous = currentRegionId;
            currentRegionId = next;
            if (!string.IsNullOrEmpty(previous))
            {
                target.PrepareForRoomReset();
                ResetRegion(previous);
            }
            RegionChanged?.Invoke(previous, next);
        }

        public void ResetRegion(string id)
        {
            if (resetting || string.IsNullOrEmpty(id) || !members.TryGetValue(id, out var list)) return;
            using var marker = ResetRegionMarker.Auto();
            resetting = true;
            try
            {
                // Restore all poses first so cross-object links evaluate against authored positions.
                resetSnapshot.Clear();
                resetSnapshot.AddRange(list);
                foreach (var member in resetSnapshot) if (member != null) member.RestorePose();
                foreach (var member in resetSnapshot) if (member != null) member.RestoreState();
                Physics.SyncTransforms();
                RegionReset?.Invoke(id);
            }
            finally { resetSnapshot.Clear(); resetting = false; }
        }

        public static RoomLifecycleManager ForScene(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var manager in Live) if (manager != null && manager.gameObject.scene == scene) return manager;
            return null;
        }

        public static void BeforeRespawn(PlayerController target, Vector3 destination)
        {
            var manager = ForScene(target.gameObject.scene);
            if (manager == null) return;
            manager.player = target;
            string origin = manager.currentRegionId ?? manager.FindRegion(target.transform.position);
            string next = manager.FindRegion(destination);
            manager.ResetRegion(origin);
            if (next != origin) manager.ResetRegion(next);
            manager.currentRegionId = next;
        }

        public void PrepareCheckpointDestination(PlayerController target, Vector3 destination)
        {
            if (target == null || target.gameObject.scene != gameObject.scene) return;
            string origin = FindRegion(target.transform.position);
            string next = FindRegion(destination);
            if (!string.IsNullOrEmpty(next) && next != origin) ResetRegion(next);
        }

        public void CommitCheckpointTravel(PlayerController target, Vector3 destination)
        {
            if (target == null || target.gameObject.scene != gameObject.scene) return;
            player = target;
            string origin = FindRegion(target.transform.position);
            string next = FindRegion(destination);
            if (origin != next) ResetRegion(origin);
            currentRegionId = next;
            if (origin != next) RegionChanged?.Invoke(origin, next);
        }

        public static void ResetCurrentRooms()
        {
            foreach (var manager in Live.ToArray())
                if (manager != null) { manager.player?.PrepareForRoomReset(); manager.ResetRegion(manager.currentRegionId); }
        }
    }
}
