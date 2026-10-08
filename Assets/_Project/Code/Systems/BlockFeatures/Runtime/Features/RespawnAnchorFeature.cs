using System;
using System.Collections;
using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [DisallowMultipleComponent]
    public sealed class RespawnAnchorFeature :
        MonoBehaviour,
        IPlayerRespawnPoint,
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

        [SerializeField, Min(0f)]
        private float respawnHeightBlocks = 1f;
        [SerializeField, Min(0f)]
        private float respawnRadiusBlocks;
        [SerializeField, Min(0f)]
        private float activationRadiusBlocks = .5f;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material inactiveMaterial;
        [SerializeField] private Material activatedMaterial;
        [SerializeField] private AnimationClip activationAnimation;
        [SerializeField, Min(0f)]
        private float activationAnimationDuration;

        private bool activated;
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
            PlayActivationAnimation();
            return true;
        }

        public bool ActivateForDebug()
        {
            return Activate();
        }

        public void EnsureRuntimeAnchorId()
        {
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = Guid.NewGuid().ToString("N");
            }
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
            EnsureRuntimeAnchorId();
            ResolveTargetRenderer();
            ApplyVisualState();
        }

        private void OnEnable()
        {
            EnsureRuntimeAnchorId();
            ResolveTargetRenderer();
            RespawnAnchorChainRuntime.Register(this);
            PlayerRespawnPointRegistry.Register(this);
        }

        private void OnDisable()
        {
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
            ResolveTargetRenderer();
        }

        private void Update()
        {
            bool hasPlayer = TryGetPlayer(
                playerTag,
                out GameObject player);
            if (hasPlayer)
            {
                ApplySortingOrder(player);
            }

            if (activated || !hasPlayer)
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
            if ((playerPosition - anchorPosition).sqrMagnitude <=
                radius * radius)
            {
                RespawnAnchorChainRuntime.ActivateThrough(this);
            }
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

            Material material = activated
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
