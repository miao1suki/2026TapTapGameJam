using System.Collections.Generic;
using Project.ColorBlocks;
using Project.GameFlow;
using Project.Player;
using Project.BlockFeatures.Power;
using UnityEngine;

namespace Project.BlockFeatures
{
    public enum BlockWaterVisualMode
    {
        [InspectorName("功能组件自己管理水体")]
        FeatureOwned = 0,
        [InspectorName("沿用颜色管理器水体")]
        ExternalManager = 1
    }

    public interface IBlockWaterSource
    {
        bool IsWaterEnabled { get; }
        bool UsesOwnedVisual { get; }
        Bounds WaterBounds { get; }
        bool Contains(Vector3 worldPosition);
        void SetReveal(float progress);
        void EnterActor(GameObject actor);
        void ExitActor(GameObject actor);
    }

    public interface IBlockWaterSwitch
    {
        bool IsWaterEnabled { get; }
        void SetWaterEnabled(bool value);
    }

    [BlockFeature(
        DisplayName = "水体功能组件",
        DefaultColorId = "blue",
        Phase = BlockFeaturePhase.Simulation,
        Order = 10,
        MaxPerBlock = 1,
        Provides = new[]
        {
            typeof(IBlockWaterSource),
            typeof(IBlockWaterSwitch)
        },
        Conflicts = new[] { typeof(BlockPowerFeature) },
        Writes = new[] { BlockChannel.Water })]
    public sealed class BlockWaterFeature : BlockFeature,
        IBlockWaterSource,
        IBlockWaterSwitch
    {
        [BlockParameter(
            Label = "水体管理方式",
            Group = "水体",
            Order = 0,
            Tooltip = "功能组件自己创建 TA 水体，或沿用颜色管理器的蓝色水体。")]
        [SerializeField] private BlockWaterVisualMode visualMode =
            BlockWaterVisualMode.FeatureOwned;

        [BlockParameter(
            Label = "水体预制体",
            Group = "水体",
            Order = 1,
            Tooltip = "留空时回退到颜色目录中的蓝色水体预制体。")]
        [SerializeField] private GameObject waterVisualPrefab;

        [BlockParameter(
            Label = "启用时自动开启",
            Group = "水体",
            Order = 2)]
        [SerializeField] private bool startsEnabled = true;

        [BlockParameter(
            Label = "允许玩家游泳",
            Group = "玩家",
            Order = 0)]
        [SerializeField] private bool allowSwimming = true;

        [BlockParameter(
            Label = "进水速度倍率",
            Group = "玩家",
            Order = 1,
            Tooltip = "蓝色水体内的水平游泳速度倍率。")]
        [SerializeField, Min(0.05f)] private float swimSpeedMultiplier = .55f;

        [BlockParameter(
            Label = "进水恢复生命",
            Group = "玩家",
            Order = 2,
            Tooltip = "玩家每次进入此水体恢复的生命值。")]
        [SerializeField, Min(0f)] private float restoreHealthOnEnter = 1f;

        [BlockParameter(
            Label = "免疫摔落伤害",
            Group = "玩家",
            Order = 3)]
        [SerializeField] private bool ignoreFallDamage = true;

        [BlockParameter(
            Label = "碰撞体切换为 Trigger",
            Group = "玩家",
            Order = 1,
            Tooltip = "水体模式启用时，将根 BoxCollider 临时改为 Trigger。")]
        [SerializeField] private bool configureColliderAsTrigger = true;

        [BlockParameter(
            Label = "初始显示进度",
            Group = "表现",
            Order = 0)]
        [SerializeField, Range(0f, 1f)] private float initialReveal = 1f;

        [BlockParameter(
            Label = "入水音效",
            Group = "音频",
            Order = 0)]
        [SerializeField] private AudioClip enterWaterClip;

        [BlockParameter(
            Label = "入水水花音效",
            Group = "音频",
            Order = 1)]
        [SerializeField] private AudioClip splashClip;

        [BlockParameter(
            Label = "离开水音效",
            Group = "音频",
            Order = 2)]
        [SerializeField] private AudioClip exitWaterClip;

        [BlockParameter(
            Label = "入水音量",
            Group = "音频",
            Order = 3)]
        [SerializeField, Range(0f, 1f)] private float enterWaterVolume = .9f;

        [BlockParameter(
            Label = "水花音量",
            Group = "音频",
            Order = 4)]
        [SerializeField, Range(0f, 1f)] private float splashVolume = .85f;

        [BlockParameter(
            Label = "离开音量",
            Group = "音频",
            Order = 5)]
        [SerializeField, Range(0f, 1f)] private float exitWaterVolume = .65f;

        [BlockParameter(
            Label = "生成入水波纹",
            Group = "表现",
            Order = 1)]
        [SerializeField] private bool createRippleOnEnter = true;

        [BlockParameter(
            Label = "调试日志",
            Group = "调试",
            Order = 0)]
        [SerializeField] private bool debugLog;

        private readonly HashSet<GameObject> actors =
            new HashSet<GameObject>();
        private GameObject visual;
        private InteractiveWater.InteractiveWater water;
        private BoxCollider volume;
        private bool originalTrigger;
        private bool triggerChanged;
        private bool waterEnabled;
        private float reveal;

        public bool IsWaterEnabled => waterEnabled;
        public bool UsesOwnedVisual =>
            visualMode == BlockWaterVisualMode.FeatureOwned;
        public Bounds WaterBounds => ResolveBounds();

        protected override void OnAttach()
        {
            volume = GetComponent<BoxCollider>();
            reveal = Mathf.Clamp01(initialReveal);
            SetWaterEnabled(startsEnabled);
        }

        protected override void OnTick(float deltaTime)
        {
            if (!isActiveAndEnabled || !waterEnabled)
            {
                return;
            }

            if (UsesOwnedVisual && visual == null)
            {
                EnsureVisual();
            }
        }

        protected override void OnDetach()
        {
            SetWaterEnabled(false);
            DestroyVisual();
        }

        private void OnDisable()
        {
            SetWaterEnabled(false);
        }

        private void OnDestroy()
        {
            SetWaterEnabled(false);
            DestroyVisual();
        }

        public void SetWaterEnabled(bool value)
        {
            if (waterEnabled == value)
            {
                if (value)
                {
                    ApplyVisualState();
                }
                return;
            }

            waterEnabled = value;
            if (waterEnabled)
            {
                ConfigureTrigger();
                if (UsesOwnedVisual)
                {
                    EnsureVisual();
                }
                ApplyVisualState();
                GetComponent<ColorBlock>()?.RefreshWaterContacts();
                return;
            }

            ExitAllActors();
            if (visual != null)
            {
                visual.SetActive(false);
            }
            RestoreTrigger();
            GetComponent<ColorBlock>()?.RefreshWaterContacts();
        }

        public void SetReveal(float progress)
        {
            reveal = Mathf.Clamp01(progress);
            ApplyVisualState();
        }

        public bool Contains(Vector3 worldPosition)
        {
            return waterEnabled &&
                   WaterBounds.Contains(worldPosition);
        }

        public void EnterActor(GameObject actor)
        {
            if (!waterEnabled || actor == null || !actors.Add(actor))
            {
                return;
            }

            PlayerController player =
                actor.GetComponentInParent<PlayerController>();
            if (allowSwimming && player != null)
            {
                player.EnterWater(this);
                player.SetSwimSpeedMultiplier(this, swimSpeedMultiplier);
                if (ignoreFallDamage)
                {
                    player.SetFallDamageImmune(this, true);
                    player.GetComponent<PlayerFallDamage>()?.SetFallDamageImmune(true);
                }
                player.GetComponent<PlayerHealth>()?.Heal(restoreHealthOnEnter);
            }

            PlayClip(enterWaterClip, enterWaterVolume);
            PlayClip(splashClip, splashVolume);
            if (createRippleOnEnter)
            {
                CreateRipple();
            }

            if (debugLog)
            {
                Debug.Log($"[BlockWater] Enter {actor.name}", this);
            }
        }

        public void ExitActor(GameObject actor)
        {
            if (actor == null || !actors.Remove(actor))
            {
                return;
            }

            PlayerController player =
                actor.GetComponentInParent<PlayerController>();
            if (allowSwimming && player != null)
            {
                player.ExitWater(this);
                player.SetSwimSpeedMultiplier(this, 0f);
                if (ignoreFallDamage)
                {
                    player.SetFallDamageImmune(this, false);
                    player.GetComponent<PlayerFallDamage>()?.SetFallDamageImmune(false);
                }
            }

            PlayClip(exitWaterClip, exitWaterVolume);
            if (debugLog)
            {
                Debug.Log($"[BlockWater] Exit {actor.name}", this);
            }
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "水体状态",
                waterEnabled ? "开启" : "关闭"));
            values.Add(new BlockDebugValue(
                "管理方式",
                visualMode == BlockWaterVisualMode.FeatureOwned
                    ? "功能组件"
                    : "颜色管理器"));
            values.Add(new BlockDebugValue(
                "显示进度",
                reveal.ToString("0.00")));
            values.Add(new BlockDebugValue(
                "水体积",
                FormatBounds(WaterBounds)));
            values.Add(new BlockDebugValue(
                "当前接触者",
                actors.Count));
            values.Add(new BlockDebugValue(
                "音效",
                BuildAudioSummary()));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                waterEnabled ? "关闭水体" : "开启水体",
                () => SetWaterEnabled(!waterEnabled)));
            actions.Add(new BlockDebugAction(
                "显示完整水体",
                () => SetWaterEnabled(true)));
            actions.Add(new BlockDebugAction(
                "隐藏水体",
                () => SetWaterEnabled(false)));
            actions.Add(new BlockDebugAction(
                "Reveal 0",
                () => SetReveal(0f)));
            actions.Add(new BlockDebugAction(
                "Reveal 1",
                () => SetReveal(1f)));
            actions.Add(new BlockDebugAction(
                "模拟玩家进入",
                SimulatePlayerEnter,
                FindPlayer() != null));
            actions.Add(new BlockDebugAction(
                "模拟玩家离开",
                SimulatePlayerExit,
                FindPlayer() != null));
            actions.Add(new BlockDebugAction(
                "播放入水音效",
                () => PlayClip(enterWaterClip, enterWaterVolume),
                enterWaterClip != null));
            actions.Add(new BlockDebugAction(
                "播放水花音效",
                () => PlayClip(splashClip, splashVolume),
                splashClip != null));
            actions.Add(new BlockDebugAction(
                "播放离开音效",
                () => PlayClip(exitWaterClip, exitWaterVolume),
                exitWaterClip != null));
            actions.Add(new BlockDebugAction(
                "生成水波纹",
                CreateRipple,
                water != null));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || GetComponent<ColorBlock>() != null)
            {
                return;
            }

            EnterActor(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || GetComponent<ColorBlock>() != null)
            {
                return;
            }

            ExitActor(other.gameObject);
        }

        private void ConfigureTrigger()
        {
            if (!configureColliderAsTrigger || volume == null)
            {
                return;
            }

            if (!triggerChanged)
            {
                originalTrigger = volume.isTrigger;
                triggerChanged = true;
            }

            volume.isTrigger = true;
        }

        private void RestoreTrigger()
        {
            if (!triggerChanged || volume == null)
            {
                return;
            }

            volume.isTrigger = originalTrigger;
            triggerChanged = false;
        }

        private void EnsureVisual()
        {
            if (!UsesOwnedVisual || visual != null)
            {
                return;
            }

            GameObject prefab = waterVisualPrefab;
            if (prefab == null)
            {
                ColorRuntimeService manager = ColorRuntimeService.Existing;
                prefab = manager?.Catalog?.BlueWaterPrefab;
            }

            if (prefab == null)
            {
                Context?.Debug.Record(
                    BlockDebugEventKind.Error,
                    "水体功能组件缺少水体预制体。");
                return;
            }

            Bounds bounds = ResolveBounds();
            visual = Instantiate(prefab, transform);
            visual.name = "Block Water";
            visual.transform.position = new Vector3(
                bounds.min.x,
                bounds.max.y + .01f,
                bounds.min.z - .02f);
            water = visual.GetComponent<
                InteractiveWater.InteractiveWater>();
            if (water != null)
            {
                water.ConfigureBlockVolume(
                    new Vector2(
                        bounds.size.x,
                        bounds.size.z + .04f),
                    bounds.size.y + .01f);
            }
        }

        private void ApplyVisualState()
        {
            if (visual == null || water == null)
            {
                return;
            }

            bool visible = waterEnabled && reveal > .001f;
            if (visual.activeSelf != visible)
            {
                visual.SetActive(visible);
            }

            water.SetReveal(reveal);
        }

        private void CreateRipple()
        {
            if (water == null)
            {
                return;
            }

            water.CreateContactRippleAt(
                transform.position,
                .45f,
                true);
        }

        private void ExitAllActors()
        {
            if (actors.Count == 0)
            {
                return;
            }

            var snapshot = new List<GameObject>(actors);
            for (int index = 0; index < snapshot.Count; index++)
            {
                ExitActor(snapshot[index]);
            }
            actors.Clear();
        }

        private void SimulatePlayerEnter()
        {
            PlayerController player = FindPlayer();
            if (player != null)
            {
                EnterActor(player.gameObject);
            }
        }

        private void SimulatePlayerExit()
        {
            PlayerController player = FindPlayer();
            if (player != null)
            {
                ExitActor(player.gameObject);
            }
        }

        private static PlayerController FindPlayer()
        {
            return ProjectDiscovery.FindFirst<PlayerController>();
        }

        private static void PlayClip(AudioClip clip, float volume)
        {
            if (clip == null || GameAudioService.Instance == null)
            {
                return;
            }

            GameAudioService.Instance.PlaySound(clip, volume);
        }

        private Bounds ResolveBounds()
        {
            BoxCollider box = volume != null
                ? volume
                : GetComponent<BoxCollider>();
            if (box != null)
            {
                return box.bounds;
            }

            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(
                transform.position,
                Vector3.one);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderers[index].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            return bounds;
        }

        private void DestroyVisual()
        {
            if (visual == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(visual);
            }
            else
            {
                DestroyImmediate(visual);
            }

            visual = null;
            water = null;
        }

        private static string FormatBounds(Bounds bounds)
        {
            return $"C {bounds.center} / S {bounds.size}";
        }

        private string BuildAudioSummary()
        {
            return
                $"入水 {(enterWaterClip != null ? "有" : "无")} · " +
                $"水花 {(splashClip != null ? "有" : "无")} · " +
                $"离开 {(exitWaterClip != null ? "有" : "无")}";
        }

        private void OnDrawGizmosSelected()
        {
            Bounds bounds = ResolveBounds();
            Gizmos.color = new Color(.2f, .7f, 1f, .8f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
