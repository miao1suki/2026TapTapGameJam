using System;
using System.Collections;
using System.Collections.Generic;
using Project.Audio;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [DisallowMultipleComponent]
    public sealed class RespawnAnchorFeature :
        MonoBehaviour,
        IPlayerRespawnPoint,
        IPlayerRespawnBindingListener,
        IPlayerRespawnPreRoomResetListener,
        IPlayerRespawnPostRoomResetListener,
        IRespawnAnchorChainProvider,
        IRespawnAnchorStateProvider
    {
        [SerializeField, HideInInspector] private string anchorId;
        [SerializeField, HideInInspector] private string nextAnchorId;
        [SerializeField, HideInInspector] private int chainIndex = -1;
        [SerializeField, HideInInspector] private bool initialAnchor;
        [SerializeField, HideInInspector]
        private List<RespawnAnchorChainEntry> chainSnapshot =
            new List<RespawnAnchorChainEntry>();

        [BlockParameter(
            Label = "是否为 Boss 关卡重生点",
            Group = "关卡类型",
            Order = 0,
            Tooltip = "开启后，玩家从该点复活时同步设置 Boss 血量与位置。")]
        [SerializeField]
        private bool isBossArenaRespawnPoint;
        [BlockParameter(
            Label = "复活后 Boss 血量",
            Group = "Boss 关卡重生",
            Order = 0,
            VisibleWhenField = nameof(isBossArenaRespawnPoint),
            VisibleWhenValue = 1)]
        [SerializeField, Min(0)]
        private int bossRespawnHealth = 9;
        [BlockParameter(
            Label = "Boss 横向位置（格）",
            Group = "Boss 关卡重生",
            Order = 1,
            Tooltip = "相对重生点的 X 偏移；Y 始终与重生点相同。",
            VisibleWhenField = nameof(isBossArenaRespawnPoint),
            VisibleWhenValue = 1)]
        [SerializeField]
        private float bossRespawnLocalX;
        [SerializeField, HideInInspector]
        private BossRespawnMarker bossRespawnMarker;
        private bool bossRespawnRequestPending;
        private int pendingBossRespawnHealth;
        private Vector3 pendingBossRespawnPosition;
        private int lastAppliedBossRespawnHealth = -1;
        private Vector3 lastAppliedBossRespawnPosition;
        private float nextBossRespawnLookupTime;
        private string bossRespawnDebugStatus = "未请求";
        public event Action<int, Vector3>
            BossRespawnStateApplied;
        public event Action<
            RespawnAnchorFeature,
            PlayerController>
            NewlyActivatedByPlayer;

        [BlockParameter(Label = "复活高度（格）", Group = "复活设置", Order = 0)]
        [SerializeField, Min(0f)]
        private float respawnHeightBlocks = 1f;
        [BlockParameter(Label = "安全查找半径（格）", Group = "复活设置", Order = 1)]
        [SerializeField, Min(0f)]
        private float respawnRadiusBlocks;
        [BlockParameter(Label = "激活半径（格）", Group = "激活设置", Order = 0)]
        [SerializeField, Min(0f)]
        private float activationRadiusBlocks = 1.5f;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material inactiveMaterial;
        [SerializeField] private Material activatedMaterial;
        [SerializeField] private AnimationClip activationAnimation;
        [SerializeField, Min(0f)]
        private float activationAnimationDuration;

        private bool activated;
        private bool playerInside;
        private readonly HashSet<PlayerController> boundPlayers = new HashSet<PlayerController>();
        public bool IsCurrent => boundPlayers.Count > 0;
        public event Action<bool> CurrentChanged;

        public void OnRespawnBindingChanged(PlayerController player, bool current)
        {
            bool previous = IsCurrent;
            if (current) boundPlayers.Add(player); else boundPlayers.Remove(player);
            if (previous == IsCurrent) return;
            if (!current)
            {
                ClearPendingBossRespawnState(
                    "已离开当前重生点");
            }

            if (activationRoutine != null) { StopCoroutine(activationRoutine); activationRoutine = null; }
            ApplyVisualState();
            if (IsCurrent) PlayActivationAnimation();
            CurrentChanged?.Invoke(IsCurrent);
        }
        [SerializeField] private string regionId;
        public string RegionId => regionId;
        public void SetRegion(string id) => regionId = id;
        private Coroutine activationRoutine;
        private static readonly Dictionary<string, GameObject>
            PlayerCache = new Dictionary<string, GameObject>(
                StringComparer.Ordinal);
        private static readonly Dictionary<string, float>
            PlayerCacheExpiry = new Dictionary<string, float>(
                StringComparer.Ordinal);

        public string RespawnAnchorId => anchorId;
        public string AnchorId => anchorId;
        public string NextAnchorId => nextAnchorId;
        public int ChainIndex => chainIndex;
        public bool HasChainIndex => chainIndex >= 0;
        public string DisplayLabel => HasChainIndex
            ? $"{chainIndex}号重生点"
            : "未连接";
        public bool IsInitialAnchor => initialAnchor;
        public bool IsActivated => activated;
        public bool IsBossArenaRespawnPoint =>
            isBossArenaRespawnPoint;
        public int BossRespawnHealth =>
            Mathf.Max(0, bossRespawnHealth);
        public float BossRespawnLocalX => bossRespawnLocalX;
        public bool HasPendingBossRespawnState =>
            bossRespawnRequestPending;
        public int LastAppliedBossRespawnHealth =>
            lastAppliedBossRespawnHealth;
        public Vector3 LastAppliedBossRespawnPosition =>
            lastAppliedBossRespawnPosition;
        public string BossRespawnDebugStatus =>
            bossRespawnDebugStatus;
        public BossRespawnMarker BossMarker =>
            EnsureBossRespawnMarker(false);
        public Vector3 BossRespawnPosition
        {
            get
            {
                Vector3 markerPosition = transform.TransformPoint(
                    new Vector3(bossRespawnLocalX, 0f, 0f));
                return new Vector3(
                    markerPosition.x,
                    transform.position.y,
                    transform.position.z);
            }
        }
        public bool IsRespawnPointAvailable =>
            isActiveAndEnabled &&
            activated;
        public Vector3 RespawnPosition =>
            transform.position +
            Vector3.up *
            (respawnHeightBlocks *
             GridCellSizeUtility.Resolve(this));
        public int RespawnPriority => chainIndex;
        public float RespawnRadiusBlocks =>
            Mathf.Max(0f, respawnRadiusBlocks);
        public float ActivationRadiusBlocks =>
            Mathf.Max(0f, activationRadiusBlocks);
        public string PlayerTag => playerTag;
        public Renderer TargetRenderer => targetRenderer;
        public Material InactiveMaterial => inactiveMaterial;
        public Material ActivatedMaterial => activatedMaterial;
        public AnimationClip ActivationAnimation =>
            activationAnimation;
        public float ActivationAnimationDuration =>
            Mathf.Max(0f, activationAnimationDuration);

        public IReadOnlyList<RespawnAnchorChainEntry>
            GetRespawnChainSnapshot()
        {
            IReadOnlyList<RespawnAnchorChainEntry> runtimeSnapshot =
                RespawnAnchorChainRuntime.CreateSnapshot();
            if (runtimeSnapshot.Count > 0)
            {
                return runtimeSnapshot;
            }

            return chainSnapshot ??
                   new List<RespawnAnchorChainEntry>();
        }

        public bool Activate()
        {
            if (activated)
            {
                return false;
            }

            activated = true;
            RespawnAnchorChainRuntime.Invalidate();
            ApplyVisualState();
            return true;
        }

        public bool ActivateForDebug()
        {
            if (TryGetPlayer(playerTag, out GameObject player))
                return ActivateForPlayer(player.GetComponent<PlayerController>());
            return Activate();
        }

        public bool ActivateForPlayer(PlayerController player)
        {
            if (player == null || !isActiveAndEnabled) return false;
            bool wasActivated = activated;
            bool wasCurrent = boundPlayers.Contains(player);
            RespawnAnchorChainRuntime.ActivateThrough(this);
            PlayerRespawnPointRegistry.Bind(player, this);
            bool changed = !wasCurrent && boundPlayers.Contains(player);
            if (changed && Application.isPlaying)
                AudioCuePlayer.Play(wasActivated ? AudioCueIds.CheckpointSwitch : AudioCueIds.CheckpointUnlock);
            if (!wasActivated && IsActivated)
                NewlyActivatedByPlayer?.Invoke(this, player);
            return changed;
        }

        public void EnsureRuntimeAnchorId()
        {
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = Guid.NewGuid().ToString("N");
            }
        }

        public void SetBossArenaRespawnPoint(bool value)
        {
            isBossArenaRespawnPoint = value;
            if (!value)
            {
                ClearPendingBossRespawnState(
                    "Boss 重生点已关闭");
            }

            ApplyBossRespawnMarkerState();
        }

        public void SetBossRespawnLocalX(float value)
        {
            bossRespawnLocalX = value;
            ApplyBossRespawnMarkerState();
        }

        public BossRespawnMarker EnsureBossRespawnMarker(
            bool createIfMissing = true,
            bool recordUndo = true)
        {
            bool changed = false;
            BossRespawnMarker[] markers =
                GetComponentsInChildren<BossRespawnMarker>(true);
            if (bossRespawnMarker == null)
            {
                for (int index = 0; index < markers.Length; index++)
                {
                    BossRespawnMarker candidate = markers[index];
                    if (candidate != null &&
                        candidate.Owner == this)
                    {
                        bossRespawnMarker = candidate;
                        changed = true;
                        break;
                    }
                }
            }

            if (!isBossArenaRespawnPoint)
            {
                if (bossRespawnMarker != null)
                {
                    if (bossRespawnMarker.gameObject
                        .activeSelf)
                    {
                        bossRespawnMarker.gameObject
                            .SetActive(false);
                        changed = true;
                    }
#if UNITY_EDITOR
                    MarkBossMarkerDirty(changed);
#endif
                }

                return bossRespawnMarker;
            }

            if (bossRespawnMarker == null && createIfMissing)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Transform existing =
                        transform.Find(BossRespawnMarker.ObjectName);
                    if (existing != null)
                    {
                        bossRespawnMarker =
                            existing.GetComponent<BossRespawnMarker>();
                        changed = true;
                        if (bossRespawnMarker == null)
                        {
                            bossRespawnMarker = recordUndo
                                ? UnityEditor.Undo
                                    .AddComponent<BossRespawnMarker>(
                                        existing.gameObject)
                                : existing.gameObject
                                    .AddComponent<BossRespawnMarker>();
                        }
                    }
                    else
                    {
                        GameObject markerObject =
                            new GameObject(
                                BossRespawnMarker.ObjectName);
                        markerObject.transform.SetParent(
                            transform,
                            false);
                        if (recordUndo)
                        {
                            UnityEditor.Undo
                                .RegisterCreatedObjectUndo(
                                    markerObject,
                                    "创建 Boss 复活点标记");
                            bossRespawnMarker =
                                UnityEditor.Undo
                                    .AddComponent<BossRespawnMarker>(
                                        markerObject);
                        }
                        else
                        {
                            bossRespawnMarker =
                                markerObject.AddComponent<
                                    BossRespawnMarker>();
                        }

                        changed = true;
                    }
                }
#endif
            }

            if (bossRespawnMarker != null)
            {
                if (!bossRespawnMarker.gameObject.activeSelf)
                {
                    bossRespawnMarker.gameObject.SetActive(true);
                    changed = true;
                }

                changed |= bossRespawnMarker.Bind(this);
#if UNITY_EDITOR
                MarkBossMarkerDirty(changed);
#endif
                for (int index = 0;
                     index < markers.Length;
                     index++)
                {
                    BossRespawnMarker duplicate = markers[index];
                    if (duplicate != null &&
                        duplicate != bossRespawnMarker &&
                        duplicate.Owner == this)
                    {
                        if (duplicate.gameObject.activeSelf)
                        {
                            duplicate.gameObject.SetActive(false);
                            changed = true;
                        }
                    }
                }
            }

#if UNITY_EDITOR
            MarkBossMarkerDirty(changed);
#endif
            return bossRespawnMarker;
        }

#if UNITY_EDITOR
        private void MarkBossMarkerDirty(bool changed)
        {
            if (!changed)
            {
                return;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            if (bossRespawnMarker != null)
            {
                UnityEditor.EditorUtility.SetDirty(
                    bossRespawnMarker);
            }

            if (!UnityEditor.PrefabUtility
                .IsPartOfPrefabInstance(this))
            {
                return;
            }

            UnityEditor.PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    this);
            if (bossRespawnMarker != null)
            {
                UnityEditor.PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        bossRespawnMarker);
            }
        }
#endif

        public void ApplyBossRespawnMarkerState()
        {
            EnsureBossRespawnMarker(false);
        }

        public void OnPlayerRespawnBeforeRoomReset(
            PlayerController player)
        {
            if (!isBossArenaRespawnPoint || player == null)
            {
                return;
            }

            pendingBossRespawnHealth =
                BossRespawnHealth;
            pendingBossRespawnPosition =
                BossRespawnPosition;
            bossRespawnRequestPending = true;
            nextBossRespawnLookupTime = 0f;
            bossRespawnDebugStatus =
                "等待 Boss 生成";
        }

        public void OnPlayerRespawnAfterRoomReset(
            PlayerController player)
        {
            TryApplyPendingBossRespawnState();
        }

        public void RequestBossRespawnStateForDebug()
        {
            if (!isBossArenaRespawnPoint)
            {
                bossRespawnDebugStatus =
                    "未启用 Boss 重生";
                return;
            }

            if (!Application.isPlaying)
            {
                bossRespawnDebugStatus =
                    "仅可在运行时应用";
                return;
            }

            pendingBossRespawnHealth =
                BossRespawnHealth;
            pendingBossRespawnPosition =
                BossRespawnPosition;
            bossRespawnRequestPending = true;
            nextBossRespawnLookupTime = 0f;
            bossRespawnDebugStatus =
                "调试请求：等待 Boss 生成";
            TryApplyPendingBossRespawnState();
        }

        private void TryApplyPendingBossRespawnState()
        {
            if (!bossRespawnRequestPending ||
                !isBossArenaRespawnPoint ||
                !isActiveAndEnabled ||
                !Application.isPlaying)
            {
                return;
            }

            if (Time.unscaledTime <
                nextBossRespawnLookupTime)
            {
                return;
            }

            nextBossRespawnLookupTime =
                Time.unscaledTime + .1f;
            IBossRespawnStateReceiver receiver =
                FindBossRespawnReceiver();
            if (receiver == null ||
                !receiver.IsBossRespawnStateAvailable)
            {
                bossRespawnDebugStatus =
                    "等待 Boss 生成";
                return;
            }

            receiver.PrepareForBossRespawn();
            receiver.ApplyBossRespawnState(
                pendingBossRespawnHealth,
                pendingBossRespawnPosition);
            float observedHealth =
                receiver.CurrentHealth;
            Vector3 observedPosition =
                receiver.WorldPosition;
            lastAppliedBossRespawnHealth =
                Mathf.RoundToInt(observedHealth);
            lastAppliedBossRespawnPosition =
                observedPosition;
            bossRespawnRequestPending = false;
            bool healthMatches = Mathf.Approximately(
                observedHealth,
                pendingBossRespawnHealth);
            bool positionMatches =
                (observedPosition -
                 pendingBossRespawnPosition)
                .sqrMagnitude < .0001f;
            bossRespawnDebugStatus =
                $"已应用并检测：{observedHealth:0.##} 血，" +
                $"X {observedPosition.x:0.###}，" +
                $"Y {observedPosition.y:0.###} " +
                (healthMatches && positionMatches
                    ? "（一致）"
                    : "（不一致）");
            BossRespawnStateApplied?.Invoke(
                lastAppliedBossRespawnHealth,
                lastAppliedBossRespawnPosition);
        }

        private void ClearPendingBossRespawnState(
            string status)
        {
            bossRespawnRequestPending = false;
            bossRespawnDebugStatus = status;
        }

        public void SetChainData(
            string valueNextAnchorId,
            int valueChainIndex,
            bool valueInitialAnchor,
            IReadOnlyList<RespawnAnchorChainEntry>
                valueChainSnapshot)
        {
            nextAnchorId = valueNextAnchorId ?? string.Empty;
            chainIndex = Mathf.Max(-1, valueChainIndex);
            initialAnchor = valueInitialAnchor;
            chainSnapshot = new List<RespawnAnchorChainEntry>();
            if (valueChainSnapshot != null)
            {
                for (int index = 0;
                     index < valueChainSnapshot.Count;
                     index++)
                {
                    RespawnAnchorChainEntry entry =
                        valueChainSnapshot[index];
                    if (entry == null)
                    {
                        continue;
                    }

                    chainSnapshot.Add(
                        new RespawnAnchorChainEntry(
                            entry.anchorId,
                            entry.nextAnchorId,
                            entry.chainIndex,
                            entry.worldPosition,
                            entry.initialAnchor,
                            entry.activated));
                }
            }

            RespawnAnchorChainRuntime.Invalidate();
        }

        public void RestoreSavedState(
            string valueNextAnchorId,
            int valueChainIndex,
            bool valueInitialAnchor,
            bool valueActivated)
        {
            nextAnchorId = valueNextAnchorId ?? string.Empty;
            chainIndex = Mathf.Max(-1, valueChainIndex);
            initialAnchor = valueInitialAnchor;
            activated = valueActivated;
            RespawnAnchorChainRuntime.Invalidate();
            ApplyVisualState();
        }

        public void ResetSavedState()
        {
            nextAnchorId = string.Empty;
            chainIndex = -1;
            initialAnchor = false;
            activated = false;
            RespawnAnchorChainRuntime.Invalidate();
            ApplyVisualState();
        }

        public void SetNextAnchorId(string value)
        {
            nextAnchorId = value ?? string.Empty;
            RespawnAnchorChainRuntime.Invalidate();
        }

        private void Awake()
        {
            WorldObjectSpritePresentation.Ensure(gameObject);
            EnsureRuntimeAnchorId();
            ResolveTargetRenderer();
            ApplyBossRespawnMarkerState();
            ApplyVisualState();
        }

        private void OnEnable()
        {
            playerInside = false;
            boundPlayers.Clear();
            EnsureRuntimeAnchorId();
            ResolveTargetRenderer();
            ApplyBossRespawnMarkerState();
            RespawnAnchorChainRuntime.Register(this);
            PlayerRespawnPointRegistry.Register(this);
            ApplyVisualState();
        }

        private void Start()
        {
            var manager = Project.Rooms.RoomLifecycleManager.ForScene(gameObject.scene);
            if (manager != null && string.IsNullOrEmpty(regionId)) regionId = manager.FindRegion(transform.position);
        }

        private void OnDisable()
        {
            ClearPendingBossRespawnState(
                "重生点已禁用");
            RespawnAnchorChainRuntime.Unregister(this);
            PlayerRespawnPointRegistry.Unregister(this);
            if (activationRoutine != null)
            {
                StopCoroutine(activationRoutine);
                activationRoutine = null;
            }
        }

        private void OnValidate()
        {
            respawnHeightBlocks = Mathf.Max(0f, respawnHeightBlocks);
            respawnRadiusBlocks = Mathf.Max(0f, respawnRadiusBlocks);
            activationRadiusBlocks = Mathf.Max(
                0f,
                activationRadiusBlocks);
            activationAnimationDuration = Mathf.Max(
                0f,
                activationAnimationDuration);
            bossRespawnHealth = Mathf.Max(
                0,
                bossRespawnHealth);
            ResolveTargetRenderer();
            ApplyBossRespawnMarkerState();
        }

        private void Update()
        {
            TryApplyPendingBossRespawnState();
            bool hasPlayer = TryGetPlayer(
                playerTag,
                out GameObject player);
            if (hasPlayer)
            {
                ApplySortingOrder(player);
            }

            if (!hasPlayer)
            {
                return;
            }

            float radius =
                activationRadiusBlocks *
                GridCellSizeUtility.Resolve(this);
            Vector2 anchorPosition = new Vector2(
                transform.position.x,
                transform.position.y);
            Vector2 playerPosition = new Vector2(
                player.transform.position.x,
                player.transform.position.y);
            bool inside = (playerPosition - anchorPosition).sqrMagnitude <= radius * radius;
            if (inside && !playerInside)
            {
                ActivateForPlayer(player.GetComponent<PlayerController>());
            }
            playerInside = inside;
        }

        private Renderer ResolveTargetRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            return targetRenderer;
        }

        private void ApplyVisualState()
        {
            Renderer renderer = ResolveTargetRenderer();
            if (renderer == null)
            {
                return;
            }

            Material material = IsCurrent
                ? activatedMaterial
                : inactiveMaterial;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (TryGetPlayer(playerTag, out GameObject player))
            {
                ApplySortingOrder(player);
            }
        }

        private void ApplySortingOrder(GameObject player)
        {
            if (targetRenderer == null || player == null)
            {
                return;
            }

            Renderer playerRenderer =
                player.GetComponentInChildren<Renderer>(true);
            if (playerRenderer == null)
            {
                return;
            }

            targetRenderer.sortingLayerID =
                playerRenderer.sortingLayerID;
            targetRenderer.sortingOrder =
                playerRenderer.sortingOrder - 1;
        }

        private void PlayActivationAnimation()
        {
            AnimationClip clip = activationAnimation;
            if (clip == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                clip.SampleAnimation(gameObject, clip.length);
                return;
            }

            if (activationRoutine != null)
            {
                StopCoroutine(activationRoutine);
            }

            activationRoutine =
                StartCoroutine(PlayActivationAnimationRoutine(clip));
        }

        private IEnumerator PlayActivationAnimationRoutine(
            AnimationClip clip)
        {
            float duration = activationAnimationDuration > 0f
                ? activationAnimationDuration
                : clip.length;
            if (duration <= 0f)
            {
                clip.SampleAnimation(gameObject, clip.length);
                activationRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float time = Mathf.Lerp(
                    0f,
                    clip.length,
                    Mathf.Clamp01(elapsed / duration));
                clip.SampleAnimation(gameObject, time);
                elapsed += Time.deltaTime;
                yield return null;
            }

            clip.SampleAnimation(gameObject, clip.length);
            activationRoutine = null;
            ApplyVisualState();
        }

        private static IBossRespawnStateReceiver
            FindBossRespawnReceiver()
        {
            IBossRespawnStateReceiver registered =
                BossRespawnStateRegistry.FindAvailable();
            if (registered != null)
            {
                return registered;
            }

            IReadOnlyList<MonoBehaviour> behaviours =
                ProjectDiscovery.FindAll<MonoBehaviour>(true);
            for (int index = 0;
                 index < behaviours.Count;
                 index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null ||
                    !behaviour.CompareTag("Boss") ||
                    behaviour is not IBossRespawnStateReceiver receiver ||
                    !receiver.IsBossRespawnStateAvailable)
                {
                    continue;
                }

                return receiver;
            }

            return null;
        }

        private static bool TryGetPlayer(
            string tag,
            out GameObject player)
        {
            player = null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            if (PlayerCache.TryGetValue(tag, out player) &&
                player != null &&
                player.activeInHierarchy &&
                player.tag == tag)
            {
                return true;
            }

            if (PlayerCacheExpiry.TryGetValue(
                    tag,
                    out float nextSearch) &&
                Time.unscaledTime < nextSearch)
            {
                player = null;
                return false;
            }

            PlayerCacheExpiry[tag] = Time.unscaledTime + .25f;
            player = GameObject.FindGameObjectWithTag(tag);
            PlayerCache[tag] = player;
            return player != null;
        }
    }
}
