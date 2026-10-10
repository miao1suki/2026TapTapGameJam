using System.Collections;
using System.Collections.Generic;
using Project.Player;
using Project.ColorBlocks;
using UnityEngine;

namespace Project.BlockFeatures
{
    public interface IVineSegmentAppearance
    {
        void PlayAppearAnimation();
    }

    [BlockFeature(
        DisplayName = "可攀爬藤蔓",
        DefaultColorId = "green",
        Category = BlockFeatureCategory.Plant,
        Interactions =
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.ObjectContact |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Growth })]
    public class ClimbableVineFeature : BlockFeature,
        IVineSegmentAppearance, IObjectContactReceiver
    {
        private static readonly int AppearTriggerHash =
            Animator.StringToHash("Appear");

        [BlockParameter(
            Label = "玩家标签",
            Group = "攀爬设置",
            Order = 0)]
        [SerializeField] private string playerTag = "Player";

        [BlockParameter(
            Label = "攀爬倍率",
            Group = "攀爬设置",
            Order = 1,
            Tooltip = "额外乘以玩家自身的攀爬速度，初始为 1。")]
        [SerializeField, Min(.01f)] private float climbSpeedMultiplier = 1f;

        [BlockParameter(
            Label = "进入攀爬距离（格）",
            Group = "攀爬设置",
            Order = 2,
            Tooltip = "1 表示一格，0.5 表示半格；玩家进入该距离后进入攀爬状态。")]
        [SerializeField, Min(0f)] private float climbEnterDistanceGrid = 1f;

        [BlockParameter(
            Label = "自然下滑速度",
            Group = "攀爬设置",
            Order = 3,
            Tooltip = "玩家不按上下时沿藤蔓自然下滑的速度。")]
        [SerializeField, Min(0f)] private float naturalSlideSpeed = 1.2f;

        [BlockParameter(
            Label = "加速下滑速度",
            Group = "攀爬设置",
            Order = 4,
            Tooltip = "玩家按住 S 时沿藤蔓加速下滑的速度。")]
        [SerializeField, Min(0f)] private float fastSlideSpeed = 4.5f;

        [SerializeField] protected bool allowGrowthFromColor = true;

        [BlockParameter(
            Label = "生长物预制体",
            Group = "生长设置",
            Order = 1,
            Tooltip = "留空时使用自身预制体；藤蔓推荐绑定 LadderSon 预制体。",
            VisibleWhenField = nameof(allowGrowthFromColor),
            VisibleWhenValue = 1)]
        [SerializeField] private GameObject growthSegmentPrefab;

        [BlockParameter(
            Label = "最大生长块数",
            Group = "生长设置",
            Order = 2,
            Tooltip = "以当前藤蔓块为起点，最多向上再生成多少个藤蔓块。",
            VisibleWhenField = nameof(allowGrowthFromColor),
            VisibleWhenValue = 1)]
        [SerializeField, Min(0)] private int maxGrowthBlocks = 4;

        [BlockParameter(
            Label = "瞬间长完",
            Group = "生长设置",
            Order = 3,
            Tooltip = "开启后一次生长请求直接生成全部块；关闭时按生长间隔逐块出现。",
            VisibleWhenField = nameof(allowGrowthFromColor),
            VisibleWhenValue = 1)]
        [SerializeField] private bool growInstantly;

        [BlockParameter(
            Label = "生长间隔",
            Group = "生长设置",
            Order = 4,
            VisibleWhenField = "growInstantly",
            VisibleWhenValue = 0,
            Tooltip = "逐块生长时，每生成一块后等待的时间。")]
        [SerializeField, Min(0f)] private float growthInterval = .18f;

        [SerializeField] private List<GameObject> initialSegments =
            new List<GameObject>();

        private readonly List<GameObject> spawnedSegments =
            new List<GameObject>();
        private readonly Collider[] proximityBuffer =
            new Collider[32];
        private readonly Dictionary<int, PlayerController> climbingPlayers =
            new Dictionary<int, PlayerController>();
        private readonly HashSet<int> foundPlayerIds =
            new HashSet<int>();
        private readonly List<int> exitPlayerIds =
            new List<int>();
        private Coroutine growthRoutine;
        private int growthTargetCount;
        [SerializeField, HideInInspector] private ClimbableVineFeature vineRoot;
        [BlockParameter(Label = "藤蔓粒子预制体", Group = "藤蔓表现", Order = 0, Tooltip = "留空使用颜色目录中的 TA 藤蔓粒子。")]
        [SerializeField] private GameObject vineParticlePrefab;
        [BlockParameter(Label = "初始粒子时间", Group = "藤蔓表现", Order = 1)]
        [SerializeField, Min(0)] private float initialParticleTime = 30.13f;
        [BlockParameter(Label = "长成粒子时间", Group = "藤蔓表现", Order = 2)]
        [SerializeField, Min(0)] private float grownParticleTime = 32.43f;
        [BlockParameter(Label = "生长表现时长", Group = "藤蔓表现", Order = 3)]
        [SerializeField, Min(.1f)] private float visualGrowthDuration = 2.3f;
        [BlockParameter(Label = "藤蔓宽度（格）", Group = "藤蔓表现", Order = 4, Tooltip = "控制整束藤蔓的横向范围，不改变攀爬判定。")]
        [SerializeField, Min(.1f)] private float vineWidth = 1.2f;
        [BlockParameter(Label = "藤蔓枝条粗细倍率", Group = "藤蔓表现", Order = 5, Tooltip = "放大粒子拖尾宽度，默认为原素材的三倍。")]
        [SerializeField, Min(.1f)] private float vineThickness = 3f;
        [BlockParameter(Label = "逐节燃烧时间", Group = "燃烧设置", Order = 0)]
        [SerializeField, Min(.1f)] private float burnSegmentDuration = .45f;
        private Coroutine burnRoutine;
        private bool burning, burned;
        private GameObject vineVisual;
        private ParticleSystem vineParticles;
        private Renderer vineRenderer;
        private VinePoseCache poseCache;
        private float visualProgress, visualTarget, visibleCells = 1f, targetCells = 1f;
        private int lastPose = -1;
        private float nextWaterContactCheck;
        public ClimbableVineFeature Root => vineRoot != null ? vineRoot : this;
        public bool IsBurning => burning;
        public bool IsBurned => burned;

        protected override void OnAttach()
        {
            BindOwnedSegments();
            burned = false;
            visualProgress = visualTarget = maxGrowthBlocks > 0 ? Mathf.Clamp01((float)CurrentGrowthBlocks / maxGrowthBlocks) : 0f;
            visibleCells = targetCells = CurrentGrowthBlocks + 1;
        }

        public override bool CanApplyColor(string colorId, GameObject actor) =>
            !Root.burning && !Root.burned && base.CanApplyColor(colorId, actor);

        private int PlayerCount => climbingPlayers.Count;
        public int CurrentGrowthBlocks =>
            initialSegments.Count + spawnedSegments.Count;
        public int MaxGrowthBlocks => maxGrowthBlocks;
        public GameObject GrowthSegmentPrefab => growthSegmentPrefab;
        public float ClimbSpeedMultiplier => climbSpeedMultiplier;

        protected override void OnTick(float deltaTime)
        {
            if (burned) return;
            if (Root == this) UpdateVineVisual(deltaTime);
            if (burning) return;
            CheckWaterContact();
            RefreshClimbingPlayers();
        }

        protected override void OnResetForRoom()
        {
            DisconnectAllPlayers();
            ResetGrowth();
            RestoreBurnedSegments();
        }

        protected override void OnDetach()
        {
            DisconnectAllPlayers();
            ResetGrowth();
            StopBurn();
            ColorAppearanceManager.Existing?.UnregisterVine(vineRenderer);
            if (vineVisual != null) Destroy(vineVisual);
            vineVisual = null;
            vineParticles = null;
            vineRenderer = null;
            lastPose = -1;
        }

        protected override bool OnColorApplied(
            string colorId,
            GameObject actor)
        {
            if (string.Equals(
                    colorId,
                    "red",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                Root.BeginBurn();
                return true;
            }

            if (!string.Equals(
                    colorId,
                    "blue",
                    System.StringComparison.OrdinalIgnoreCase) ||
                !CanGrowFromColor ||
                CurrentGrowthBlocks >= maxGrowthBlocks)
            {
                return false;
            }

            GrowTo(growInstantly
                ? maxGrowthBlocks
                : CurrentGrowthBlocks + 1);
            return true;
        }

        protected virtual bool CanGrowFromColor =>
            allowGrowthFromColor;

        private void RefreshClimbingPlayers()
        {
            Collider rootCollider = Root.GetComponent<Collider>();
            float topHeight = (rootCollider != null ? rootCollider.bounds.max.y : Root.transform.position.y + GridCellWorldSize * .5f)
                + Root.CurrentGrowthBlocks * GridCellWorldSize;
            foreach (PlayerController climbingPlayer in climbingPlayers.Values)
                if (climbingPlayer != null) climbingPlayer.SetClimbTopHeight(this, topHeight);
            float range =
                Mathf.Max(0f, climbEnterDistanceGrid) *
                GridCellWorldSize;
            foundPlayerIds.Clear();
            if (range > 0f)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    range,
                    proximityBuffer,
                    ~0,
                    QueryTriggerInteraction.Collide);
                for (int index = 0; index < count; index++)
                {
                    Collider collider = proximityBuffer[index];
                    PlayerController player = collider != null
                        ? collider.GetComponentInParent<PlayerController>()
                        : null;
                    if (player == null ||
                        !player.gameObject.CompareTag(playerTag) ||
                        !IsSideApproach(player))
                    {
                        continue;
                    }

                    int id = player.GetInstanceID();
                    if (!foundPlayerIds.Add(id) ||
                        climbingPlayers.ContainsKey(id))
                    {
                        continue;
                    }

                    climbingPlayers.Add(id, player);
                    player.EnterClimb(this);
                    player.SetClimbTopHeight(this, topHeight);
                    player.SetClimbSpeedMultiplier(
                        this,
                        climbSpeedMultiplier);
                    player.SetClimbSlideSpeed(
                        this,
                        naturalSlideSpeed);
                    player.SetClimbFastSlideSpeed(
                        this,
                        fastSlideSpeed);
                }
            }

            exitPlayerIds.Clear();
            foreach (KeyValuePair<int, PlayerController> pair in
                     climbingPlayers)
            {
                if (!foundPlayerIds.Contains(pair.Key))
                {
                    exitPlayerIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < exitPlayerIds.Count; index++)
            {
                ExitClimb(exitPlayerIds[index]);
            }
        }

        private bool IsSideApproach(PlayerController player)
        {
            Vector3 offset =
                player.transform.position - transform.position;
            float halfCell = GridCellWorldSize * .5f;
            if (player.IsGrounded && offset.y > halfCell)
            {
                return false;
            }

            Vector2 horizontalOffset =
                new Vector2(offset.x, offset.z);
            float minimumSideOffset = halfCell * .5f;
            return horizontalOffset.sqrMagnitude >=
                   minimumSideOffset * minimumSideOffset;
        }

        private void ExitClimb(int playerId)
        {
            if (!climbingPlayers.TryGetValue(
                    playerId,
                    out PlayerController player))
            {
                return;
            }

            climbingPlayers.Remove(playerId);
            if (player == null)
            {
                return;
            }

            player.ExitClimb(this);
            player.SetClimbSpeedMultiplier(this, 0f);
            player.ClearClimbSlideSpeed(this);
            player.ClearClimbFastSlideSpeed(this);
        }

        private void DisconnectAllPlayers()
        {
            var ids = new List<int>(climbingPlayers.Keys);
            for (int index = 0; index < ids.Count; index++)
            {
                ExitClimb(ids[index]);
            }
        }

        public void GrowOne()
        {
            GrowTo(CurrentGrowthBlocks + 1);
        }

        // Scene generation uses this only for already placed, contiguous vines.
        // Initial segments are scene objects and survive room resets; only
        // segments created during play belong to ResetGrowth().
        public void ConfigureInitialGrowth(
            IReadOnlyList<GameObject> segments)
        {
            initialSegments.Clear();
            if (segments != null)
            {
                for (int index = 0; index < segments.Count; index++)
                {
                    if (segments[index] != null)
                    {
                        initialSegments.Add(segments[index]);
                    }
                }
            }

            maxGrowthBlocks = initialSegments.Count;
            BindOwnedSegments();
        }

        public void GrowTo(int targetCount)
        {
            if (!CanRunFeature || !CanGrowFromColor || Root.burning || Root.burned)
            {
                return;
            }

            targetCount = Mathf.Clamp(
                targetCount,
                0,
                maxGrowthBlocks);
            if (growthRoutine != null && targetCount == growthTargetCount) return;
            if (targetCount <= CurrentGrowthBlocks)
            {
                return;
            }

            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
                growthRoutine = null;
            }
            growthTargetCount = targetCount;

            if (growInstantly)
            {
                while (CurrentGrowthBlocks < targetCount)
                {
                    if (SpawnSegment(
                        CurrentGrowthBlocks,
                        false) == null)
                    {
                        break;
                    }
                }

                PlayAppearanceOnTopSegment();
                return;
            }

            growthRoutine = StartCoroutine(
                GrowRoutine(targetCount));
        }

        public void ResetGrowth()
        {
            StopBurn();
            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
                growthRoutine = null;
            }

            for (int index = 0;
                 index < spawnedSegments.Count;
                 index++)
            {
                if (spawnedSegments[index] != null)
                {
                    Destroy(spawnedSegments[index]);
                }
            }

            spawnedSegments.Clear();
            visualTarget = maxGrowthBlocks > 0 ? Mathf.Clamp01((float)initialSegments.Count / maxGrowthBlocks) : 0f;
            targetCells = initialSegments.Count + 1;
        }

        public void PlayAppearAnimation()
        {
            Animation[] animations =
                GetComponentsInChildren<Animation>(true);
            for (int index = 0;
                 index < animations.Length;
                 index++)
            {
                Animation animation = animations[index];
                if (animation == null ||
                    animation.clip == null)
                {
                    continue;
                }

                animation.Stop();
                animation.Play();
                return;
            }

            Animator[] animators =
                GetComponentsInChildren<Animator>(true);
            for (int index = 0;
                 index < animators.Length;
                 index++)
            {
                Animator animator = animators[index];
                if (animator == null ||
                    animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                AnimatorControllerParameter[] parameters =
                    animator.parameters;
                for (int parameterIndex = 0;
                     parameterIndex < parameters.Length;
                     parameterIndex++)
                {
                    if (parameters[parameterIndex].type ==
                            AnimatorControllerParameterType.Trigger &&
                        parameters[parameterIndex].nameHash ==
                            AppearTriggerHash)
                    {
                        animator.Rebind();
                        animator.SetTrigger(
                            parameters[parameterIndex].nameHash);
                        return;
                    }
                }

                animator.Rebind();
                animator.Play(0, 0, 0f);
                return;
            }
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "接触玩家",
                PlayerCount));
            values.Add(new BlockDebugValue("根系状态", Root.burned ? "已烧毁" : Root.burning ? "燃烧中" : "正常"));
            values.Add(new BlockDebugValue("所属母根", Root.name));
            values.Add(new BlockDebugValue(
                "攀爬倍率",
                climbSpeedMultiplier));
            values.Add(new BlockDebugValue(
                "进入攀爬距离",
                $"{climbEnterDistanceGrid:0.###} 格"));
            values.Add(new BlockDebugValue(
                "自然下滑速度",
                naturalSlideSpeed));
            values.Add(new BlockDebugValue(
                "加速下滑速度",
                fastSlideSpeed));
            values.Add(new BlockDebugValue(
                "生长块数",
                $"{CurrentGrowthBlocks} / {maxGrowthBlocks}"));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                "向上生长一块",
                GrowOne,
                Application.isPlaying &&
                CurrentGrowthBlocks < maxGrowthBlocks));
            actions.Add(new BlockDebugAction(
                "清除生长",
                ResetGrowth,
                Application.isPlaying &&
                spawnedSegments.Count > 0));
        }

        private IEnumerator GrowRoutine(int targetCount)
        {
            while (CurrentGrowthBlocks < targetCount)
            {
                if (SpawnSegment(
                    CurrentGrowthBlocks,
                    true) == null)
                {
                    growthRoutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(
                    Mathf.Max(0f, growthInterval));
            }

            growthRoutine = null;
        }

        private GameObject SpawnSegment(
            int index,
            bool playAppearance)
        {
            GameObject segmentPrefab = growthSegmentPrefab != null
                ? growthSegmentPrefab
                : gameObject;
            GameObject segment = Instantiate(
                segmentPrefab,
                transform.parent);
            if (segment == null)
            {
                return null;
            }

            segment.name = $"{name} +{index + 1}";
            segment.transform.position =
                transform.position +
                Vector3.up *
                (GridCellWorldSize * (index + 1));
            segment.transform.rotation = transform.rotation;
            segment.transform.localScale = transform.localScale;
            spawnedSegments.Add(segment);
            ClimbableVineFeature child = segment.GetComponent<ClimbableVineFeature>();
            if (child != null) child.vineRoot = this;
            visualTarget = maxGrowthBlocks > 0 ? Mathf.Clamp01((float)CurrentGrowthBlocks / maxGrowthBlocks) : 0f;
            targetCells = CurrentGrowthBlocks + 1;
            segment.SetActive(true);

            if (playAppearance)
            {
                PlayAppearance(segment);
            }

            return segment;
        }

        private void PlayAppearanceOnTopSegment()
        {
            if (spawnedSegments.Count == 0)
            {
                return;
            }

            PlayAppearance(
                spawnedSegments[spawnedSegments.Count - 1]);
        }

        private void BindOwnedSegments()
        {
            foreach (GameObject segment in initialSegments)
            {
                if (segment == null || segment == gameObject) continue;
                var child = segment.GetComponent<ClimbableVineFeature>();
                if (child != null) child.vineRoot = this;
            }
        }

        public void OnObjectEnter(GameObject other)
        {
            WaterVisualFeature water = other != null ? other.GetComponentInParent<WaterVisualFeature>() : null;
            if (water != null && water.IsAttached && water.enabled && Root.CanRunFeature && Root.CanGrowFromColor && !Root.burning && !Root.burned)
                Root.GrowTo(Root.maxGrowthBlocks);
        }
        public void OnObjectExit(GameObject other) { }

        private void CheckWaterContact()
        {
            if (Time.time < nextWaterContactCheck || Root.CurrentGrowthBlocks >= Root.maxGrowthBlocks) return;
            nextWaterContactCheck = Time.time + .2f;
            // Static authored volumes also need contact without requiring a Rigidbody on every map item.
            int count = Physics.OverlapBoxNonAlloc(transform.position, Vector3.one * GridCellWorldSize * .51f,
                proximityBuffer, transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
                if (proximityBuffer[index] != null) OnObjectEnter(proximityBuffer[index].gameObject);
        }

        private void UpdateVineVisual(float deltaTime)
        {
            if (vineVisual == null)
            {
                GameObject prefab = vineParticlePrefab != null ? vineParticlePrefab : ColorRuntimeService.Instance.Catalog?.GreenVinePrefab;
                if (prefab == null) return;
                vineVisual = new GameObject("Vine Visual");
                vineVisual.transform.SetParent(transform, false);
                GameObject particles = Instantiate(prefab, vineVisual.transform);
                particles.transform.localPosition = Vector3.zero;
                particles.transform.localScale = Vector3.one;
                vineParticles = particles.GetComponent<ParticleSystem>();
                vineRenderer = particles.GetComponent<Renderer>();
                if (vineRenderer != null) vineRenderer.enabled = false;
                if (vineParticles == null || vineRenderer == null)
                {
                    Destroy(vineVisual);
                    vineVisual = null;
                    return;
                }
                poseCache = VinePoseCache.Get(prefab, vineParticles, initialParticleTime, grownParticleTime);
                var trails = vineParticles.trails;
                var width = trails.widthOverTrail;
                if (width.mode == ParticleSystemCurveMode.Constant) width.constant *= vineThickness;
                else if (width.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    width.constantMin *= vineThickness;
                    width.constantMax *= vineThickness;
                }
                else width.curveMultiplier *= vineThickness;
                trails.widthOverTrail = width;
                int layer = ColorRuntimeService.Instance.Catalog.Find("green")?.unityLayer ?? gameObject.layer;
                particles.layer = layer;
                vineRenderer.renderingLayerMask |= 128u;
                var properties = new MaterialPropertyBlock();
                vineRenderer.GetPropertyBlock(properties);
                properties.SetFloat("_ColorMaskUseVertexAlpha", 1f);
                Material trailMaterial = (vineRenderer as ParticleSystemRenderer)?.trailMaterial;
                Texture alphaTexture = trailMaterial != null ? trailMaterial.mainTexture : null;
                properties.SetTexture("_ColorMaskAlphaTexture", alphaTexture != null ? alphaTexture : Texture2D.whiteTexture);
                vineRenderer.SetPropertyBlock(properties);
                ColorAppearanceManager.Instance.RegisterVine(vineRenderer);
            }
            visualProgress = Mathf.MoveTowards(visualProgress, visualTarget, deltaTime / Mathf.Max(.1f, visualGrowthDuration));
            visibleCells = Mathf.MoveTowards(visibleCells, targetCells, deltaTime * Mathf.Max(1, maxGrowthBlocks) / Mathf.Max(.1f, visualGrowthDuration));
            int pose = Mathf.RoundToInt(visualProgress * (poseCache.Poses.Length - 1));
            if (pose != lastPose)
            {
                poseCache.Apply(vineParticles, pose);
                lastPose = pose;
            }
            Bounds bounds = poseCache.Poses[pose].Bounds;
            Vector3 scale = new Vector3(vineWidth / Mathf.Max(.05f, poseCache.FullBounds.size.x),
                Mathf.Max(.001f, visibleCells) / Mathf.Max(.05f, bounds.size.y),
                vineWidth / Mathf.Max(.05f, poseCache.FullBounds.size.z));
            vineVisual.transform.localScale = scale;
            vineVisual.transform.localPosition = new Vector3(-bounds.center.x * scale.x,
                -.5f - bounds.min.y * scale.y, -bounds.center.z * scale.z);
        }

        public void BeginBurn()
        {
            if (Root != this) { Root.BeginBurn(); return; }
            if (burning || burned || !CanRunFeature) return;
            burning = true;
            if (growthRoutine != null) StopCoroutine(growthRoutine);
            growthRoutine = null;
            burnRoutine = StartCoroutine(BurnRootRoutine());
        }

        private IEnumerator BurnRootRoutine()
        {
            var owned = new List<ClimbableVineFeature>();
            foreach (GameObject item in initialSegments) AddOwned(owned, item);
            foreach (GameObject item in spawnedSegments) AddOwned(owned, item);
            owned.Add(this);
            owned.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            float startProgress = visualProgress;
            float startCells = visibleCells;
            for (int index = 0; index < owned.Count; index++)
            {
                ClimbableVineFeature segment = owned[index];
                if (segment == null) continue;
                segment.burning = true;
                segment.DisconnectAllPlayers();
                float elapsed = 0;
                while (elapsed < burnSegmentDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = (index + Mathf.Clamp01(elapsed / burnSegmentDuration)) / owned.Count;
                    visualProgress = visualTarget = Mathf.Lerp(startProgress, 0f, progress);
                    visibleCells = targetCells = Mathf.Lerp(startCells, .001f, progress);
                    yield return null;
                }
                segment.burned = true;
                segment.burning = false;
                foreach (Collider collider in segment.GetComponents<Collider>()) collider.enabled = false;
                if (segment != this) segment.gameObject.SetActive(false);
            }
            burned = true;
            burning = false;
            ColorAppearanceManager.Existing?.UnregisterVine(vineRenderer);
            if (vineRenderer != null) vineRenderer.enabled = false;
            burnRoutine = null;
        }

        private void AddOwned(List<ClimbableVineFeature> owned, GameObject item)
        {
            if (item == null) return;
            var segment = item.GetComponent<ClimbableVineFeature>();
            if (segment != null && segment.Root == this && !owned.Contains(segment)) owned.Add(segment);
        }

        private void StopBurn()
        {
            if (burnRoutine != null) StopCoroutine(burnRoutine);
            burnRoutine = null;
            burning = false;
        }

        private void RestoreBurnedSegments()
        {
            burned = false;
            foreach (Collider collider in GetComponents<Collider>()) collider.enabled = true;
            foreach (GameObject item in initialSegments)
            {
                if (item == null) continue;
                var segment = item.GetComponent<ClimbableVineFeature>();
                if (segment != null) { segment.burned = false; segment.burning = false; }
                item.SetActive(true);
            }
        }

        private static void PlayAppearance(GameObject segment)
        {
            if (segment == null)
            {
                return;
            }

            MonoBehaviour[] behaviours =
                segment.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0;
                 index < behaviours.Length;
                 index++)
            {
                if (behaviours[index] is IVineSegmentAppearance appearance)
                {
                    appearance.PlayAppearAnimation();
                    return;
                }
            }
        }
    }

    // One deterministic simulation per source/time pair; subsequent instances and reverse playback restore snapshots.
    internal sealed class VinePoseCache
    {
        internal sealed class Pose
        {
            public ParticleSystem.Particle[] Particles;
            public ParticleSystem.Trails Trails;
            public ParticleSystem.PlaybackState Playback;
            public Bounds Bounds;
        }
        private static readonly Dictionary<string, VinePoseCache> caches = new Dictionary<string, VinePoseCache>();
        public readonly Pose[] Poses = new Pose[72];
        public Bounds FullBounds;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear() => caches.Clear();
        public static VinePoseCache Get(GameObject prefab, ParticleSystem system, float start, float end)
        {
            string key = prefab.GetInstanceID() + ":" + start.ToString("R") + ":" + end.ToString("R");
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed = 1f;
            var trails = system.trails;
            trails.worldSpace = false;
            system.useAutoRandomSeed = false;
            system.randomSeed = 74123;
            if (caches.TryGetValue(key, out var existing)) { system.Pause(true); return existing; }
            var cache = new VinePoseCache();
            system.Simulate(Mathf.Max(0, start), false, true, true);
            for (int index = 0; index < cache.Poses.Length; index++)
            {
                if (index > 0) system.Simulate(Mathf.Max(0, end - start) / (cache.Poses.Length - 1), false, false, true);
                var pose = new Pose { Particles = new ParticleSystem.Particle[system.particleCount],
                    Playback = system.GetPlaybackState(), Bounds = new Bounds(Vector3.zero, Vector3.one * .01f) };
                system.GetParticles(pose.Particles);
                system.GetTrails(ref pose.Trails);
                Quaternion rotation = system.transform.localRotation;
                foreach (var particle in pose.Particles)
                {
                    Vector3 position = rotation * particle.position;
                    pose.Bounds.Encapsulate(position + Vector3.one * particle.GetCurrentSize(system) * .5f);
                    pose.Bounds.Encapsulate(position - Vector3.one * particle.GetCurrentSize(system) * .5f);
                }
                cache.Poses[index] = pose;
            }
            system.Pause(true);
            cache.FullBounds = cache.Poses[cache.Poses.Length - 1].Bounds;
            caches.Add(key, cache);
            return cache;
        }
        public void Apply(ParticleSystem system, int index)
        {
            Pose pose = Poses[index];
            system.SetPlaybackState(pose.Playback);
            system.SetParticlesAndTrails(pose.Particles, pose.Trails, pose.Particles.Length);
            system.Pause(true);
        }
    }
}
