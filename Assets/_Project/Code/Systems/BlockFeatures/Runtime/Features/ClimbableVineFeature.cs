using System.Collections;
using System.Collections.Generic;
using Project.Audio;
using Project.Player;
using Project.ColorBlocks;
using Project.GreenHook;
using Project.ObjectArtwork;
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
        IVineSegmentAppearance,
        IObjectContactReceiver,
        IGreenHookTarget
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
        private Collider growthBlocker;
        private bool growthBlocked;
        private VineGrowthLift growthLift;
        private readonly Dictionary<int, PlayerController> climbingPlayers =
            new Dictionary<int, PlayerController>();
        private readonly HashSet<int> foundPlayerIds =
            new HashSet<int>();
        private readonly List<int> exitPlayerIds =
            new List<int>();
        private Coroutine growthRoutine;
        private int growthTargetCount;
        [SerializeField, HideInInspector] private ClimbableVineFeature vineRoot;
        // Retained only for serialization compatibility; runtime uses the baked sequence on every platform.
#pragma warning disable CS0414
        [SerializeField, HideInInspector] private GameObject vineParticlePrefab;
        [SerializeField, HideInInspector, Min(0)] private float initialParticleTime = 30.13f;
        [SerializeField, HideInInspector, Min(0)] private float grownParticleTime = 32.43f;
        [BlockParameter(Label = "生长表现时长", Group = "藤蔓表现", Order = 3)]
        [SerializeField, Min(.1f)] private float visualGrowthDuration = 2.3f;
        [BlockParameter(Label = "藤蔓宽度（格）", Group = "藤蔓表现", Order = 4, Tooltip = "控制整束藤蔓的横向范围，不改变攀爬判定。")]
        [SerializeField, Min(.1f)] private float vineWidth = 1.2f;
        [SerializeField, HideInInspector, Min(.1f)] private float vineThickness = 3f;
#pragma warning restore CS0414
        [BlockParameter(Label = "逐节燃烧时间", Group = "燃烧设置", Order = 0)]
        [SerializeField, Min(.1f)] private float burnSegmentDuration = .45f;
        private Coroutine burnRoutine;
        private bool burning, burned;
        private GameObject vineVisual;
        private Renderer vineRenderer;
        private Project.BurnEffects.PlantBurnEffectPlayer burnEffect;
        private float visualProgress, visualTarget, visibleCells = 1f, targetCells = 1f;
        private int lastPose = -1;
        [BlockParameter(Label = "序列帧美术配置", Group = "藤蔓表现", Order = 0, Tooltip = "留空使用 Resources/ObjectArtwork/Objects；整条母藤蔓共用一套序列帧。")]
        [SerializeField] private ObjectArtworkLibrary sequenceArtwork;
        private SpriteRenderer sequenceRenderer;
        private MaterialPropertyBlock sequenceMask;
        private float sequenceIdleTime;
        private bool sequenceMissingReported;
        private float nextWaterContactCheck;
        private float artworkBaseCells = 1f;
        public ClimbableVineFeature Root => vineRoot != null ? vineRoot : this;
        // Pure authoring reads: preview code must not call RestoreInitialGrowthLinks,
        // which intentionally repairs serialized ownership and growth parameters.
        public ClimbableVineFeature ArtworkRoot
        {
            get
            {
                if (this is LadderSonFeature && transform.parent != null &&
                    transform.parent.TryGetComponent<ClimbableVineFeature>(out var parent) && !(parent is LadderSonFeature)) return parent;
                return Root;
            }
        }
        public ObjectArtworkLibrary SequenceArtwork => sequenceArtwork;
        public float ArtworkWidthCells => vineWidth;
        private float ArtworkCellWorldSize => IsAttached ? GridCellWorldSize : GridCellSizeUtility.Resolve(this);
        public Bounds AuthoredArtworkRootBounds => VineArtworkLayout.RootBounds(transform,
            GetComponent<BoxCollider>(), ArtworkCellWorldSize);
        public Vector2 AuthoredArtworkCells
        {
            get
            {
                var root = ArtworkRoot;
                float cell = GridCellSizeUtility.Resolve(root);
                Bounds bounds = root.AuthoredArtworkRootBounds;
                float top = bounds.max.y;
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    var child = root.transform.GetChild(i);
                    if (child.GetComponent<LadderSonFeature>() != null)
                        top = Mathf.Max(top, VineArtworkLayout.RootBounds(child, child.GetComponent<BoxCollider>(), cell).max.y);
                }
                return new Vector2(root.vineWidth, Mathf.Max(.001f, (top - bounds.min.y) / cell));
            }
        }
        public float AuthoredArtworkGrowthProgress
        {
            get
            {
                var root = ArtworkRoot;
                int count = 0;
                for (int i = 0; i < root.transform.childCount; i++)
                    if (root.transform.GetChild(i).GetComponent<LadderSonFeature>() != null) count++;
                // OnAttach restores author children as an already-grown chain and
                // sets maxGrowthBlocks to that count. Mirror that outcome without
                // repairing fields merely because SceneView asked for an image.
                return count > 0 ? 1f : 0f;
            }
        }
        public bool IsBurning => burning;
        public bool IsBurned => burned;
        public GreenHookMotionMode GreenHookMotionMode =>
            GreenHookMotionMode.Swing;
        public Transform GreenHookTransform => Root.transform;
        public bool CanBeGreenHooked(GameObject actor) =>
            CanRunFeature && !Root.burning && !Root.burned;

        protected override void OnAttach()
        {
            RestoreInitialGrowthLinks();
            artworkBaseCells = Mathf.Max(.001f, AuthoredArtworkRootBounds.size.y / GridCellWorldSize);
            burned = false;
            visualProgress = visualTarget = maxGrowthBlocks > 0 ? Mathf.Clamp01((float)CurrentGrowthBlocks / maxGrowthBlocks) : 0f;
            visibleCells = targetCells = CurrentGrowthBlocks + artworkBaseCells;
        }

        public override bool CanApplyColor(string colorId, GameObject actor) =>
            !Root.burning && !Root.burned && base.CanApplyColor(colorId, actor);

        private int PlayerCount => climbingPlayers.Count;
        public int CurrentGrowthBlocks =>
            initialSegments.Count + spawnedSegments.Count;
        public int MaxGrowthBlocks => maxGrowthBlocks;
        public GameObject GrowthSegmentPrefab => growthSegmentPrefab;
        public float ClimbSpeedMultiplier => climbSpeedMultiplier;
        public bool IsGrowthBlocked => growthBlocked;

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
            if (Root == this && vineRenderer != null)
                ColorAppearanceManager.Instance.RegisterVine(vineRenderer);
        }

        protected override void OnDetach()
        {
            DisconnectAllPlayers();
            ResetGrowth();
            StopBurn();
            ColorAppearanceManager.Existing?.UnregisterVine(vineRenderer);
            if (vineVisual != null)
            {
                vineVisual.SetActive(false);
                Destroy(vineVisual);
            }
            vineVisual = null;
            vineRenderer = null;
            sequenceRenderer = null;
            sequenceMask = null;
            sequenceIdleTime = 0f;
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
            Collider segmentCollider = Root == this ? rootCollider : GetComponent<Collider>();
            float topHeight = (rootCollider != null ? rootCollider.bounds.max.y : Root.transform.position.y + GridCellWorldSize * .5f)
                + Root.CurrentGrowthBlocks * GridCellWorldSize;
            foreach (PlayerController climbingPlayer in climbingPlayers.Values)
                if (climbingPlayer != null) climbingPlayer.SetClimbTopHeight(this, topHeight);
            float range =
                Mathf.Max(0f, climbEnterDistanceGrid) *
                GridCellWorldSize;
            foundPlayerIds.Clear();
            bool hasCandidates = PlayerController.HasAutomaticClimbCandidate;
            if (range > 0f && hasCandidates)
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
                        !player.isActiveAndEnabled || player.IsDead ||
                        !player.gameObject.CompareTag(playerTag) ||
                        !player.CanAutomaticallyClimb ||
                        !IsSideApproach(player))
                    {
                        continue;
                    }

                    int id = player.GetInstanceID();
                    if (!foundPlayerIds.Add(id))
                    {
                        continue;
                    }
                    if (segmentCollider != null)
                        player.ProtectClimbCorner(this, segmentCollider.bounds,
                            player.ClimbSpeed * climbSpeedMultiplier, false);
                    if (climbingPlayers.ContainsKey(id)) continue;

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
                if (!foundPlayerIds.Contains(pair.Key) &&
                    (!hasCandidates || pair.Value == null || !pair.Value.isActiveAndEnabled || pair.Value.IsDead ||
                     !pair.Value.CanAutomaticallyClimb || !pair.Value.IsClimbCornerProtectedBy(this)))
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

            player.CancelClimbCorner(this);
            player.ClearHorizontalVineSurface(this);
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
                if (climbingPlayers.TryGetValue(ids[index], out var player) && player != null)
                { player.CancelClimbCorner(this); player.ClearHorizontalVineSurface(this); }
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

        // The serialized hierarchy is the authoritative identity of prebuilt segments.
        // Rebind once on attachment, not by per-frame proximity/world searches.
        public void RestoreInitialGrowthLinks()
        {
            if (this is LadderSonFeature)
            {
                ClimbableVineFeature parent = transform.parent != null
                    ? transform.parent.GetComponent<ClimbableVineFeature>() : null;
                if (parent != null && !(parent is LadderSonFeature)) vineRoot = parent;
                return;
            }

            var children = new List<GameObject>();
            for (int index = 0; index < transform.childCount; index++)
            {
                GameObject child = transform.GetChild(index).gameObject;
                if (child.GetComponent<LadderSonFeature>() != null) children.Add(child);
            }
            if (children.Count > 0)
            {
                children.Sort((left, right) => left.transform.position.y.CompareTo(right.transform.position.y));
                ConfigureInitialGrowth(children);
            }
            else if (initialSegments.Count > 0) ConfigureInitialGrowth(null);
            else BindOwnedSegments();
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

            if (!CanSpawnSegment(CurrentGrowthBlocks)) return;

            AudioCuePlayer.Play(AudioCueIds.PlantGrow, .8f);
            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
                growthRoutine = null;
            }
            growthTargetCount = targetCount;

            growthLift ??= new VineGrowthLift(this);
            if (growInstantly && !growthLift.HasBodies)
            {
                while (CurrentGrowthBlocks < targetCount)
                {
                    if (!CanSpawnSegment(CurrentGrowthBlocks)) break;
                    if (growthLift.HasBodies)
                    {
                        growthRoutine = StartCoroutine(GrowRoutine(targetCount));
                        return;
                    }
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
            growthLift?.End();
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
            growthBlocker = null;
            growthBlocked = false;
            visualTarget = maxGrowthBlocks > 0 ? Mathf.Clamp01((float)initialSegments.Count / maxGrowthBlocks) : 0f;
            targetCells = initialSegments.Count + artworkBaseCells;
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
            values.Add(new BlockDebugValue("生长阻挡", growthBlocker != null ? growthBlocker.name : growthBlocked ? "检测区域拥挤" : "无"));
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
                if (!CanSpawnSegment(CurrentGrowthBlocks)) break;
                if (growthLift.HasBodies)
                {
                    growthLift.Begin();
                    while (growthLift.Active && growthLift.Progress < .99999f)
                    {
                        yield return new WaitForFixedUpdate();
                        if (!growthLift.Advance(GridCellWorldSize * Time.fixedDeltaTime / Mathf.Max(.1f, growthInterval))) break;
                        visibleCells = targetCells = CurrentGrowthBlocks + artworkBaseCells + growthLift.Progress;
                        visualProgress = visualTarget = maxGrowthBlocks > 0 ? (visibleCells - artworkBaseCells) / maxGrowthBlocks : 0;
                    }
                    yield return new WaitForFixedUpdate();
                    bool completed = growthLift.Active && growthLift.Progress >= .99999f;
                    growthLift.End();
                    if (!completed)
                    {
                        growthBlocked = true; growthBlocker = growthLift.Blocker;
                        targetCells = CurrentGrowthBlocks + artworkBaseCells;
                        visualTarget = maxGrowthBlocks > 0 ? (float)CurrentGrowthBlocks / maxGrowthBlocks : 0;
                        break;
                    }
                }
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
            if (!CanSpawnSegment(index)) return null;
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
            targetCells = CurrentGrowthBlocks + artworkBaseCells;
            segment.SetActive(true);

            if (playAppearance)
            {
                PlayAppearance(segment);
            }

            return segment;
        }

        private bool CanSpawnSegment(int index)
        {
            growthBlocker = null;
            growthBlocked = false;
            GameObject source = growthSegmentPrefab != null ? growthSegmentPrefab : gameObject;
            BoxCollider shape = source.GetComponent<BoxCollider>();
            if (shape == null) shape = GetComponent<BoxCollider>();
            Vector3 position = transform.position + Vector3.up * (GridCellWorldSize * (index + 1));
            Vector3 scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 center = position + transform.rotation * Vector3.Scale(shape != null ? shape.center : Vector3.zero, scale);
            Vector3 half = shape != null ? Vector3.Scale(shape.size * .5f, scale) : Vector3.one * (GridCellWorldSize * .5f);
            half = Vector3.Max(half - Vector3.one * (GridCellWorldSize * .02f), Vector3.one * .001f);
            growthLift ??= new VineGrowthLift(this);
            if (!growthLift.Prepare(center, half, transform.rotation, GridCellWorldSize))
            {
                growthBlocked = true; growthBlocker = growthLift.Blocker; return false;
            }
            return true;
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
            if (UpdateVineSequence(deltaTime)) return;
            if (sequenceMissingReported) return;
            sequenceMissingReported = true;
            Debug.LogError("藤蔓序列帧配置缺失，请检查ObjectArtwork/Objects；不启用旧实时粒子后备。", this);
        }

        private bool UpdateVineSequence(float deltaTime)
        {
            if (sequenceArtwork == null) sequenceArtwork = ObjectArtworkLibrary.Load();
            if (sequenceArtwork == null || sequenceArtwork.vineGrowth == null || sequenceArtwork.vineGrowth.Length == 0 ||
                sequenceArtwork.colorSpriteMaterial == null) return false;
            if (vineVisual == null)
            {
                vineVisual = new GameObject("Vine Visual");
                vineVisual.transform.SetParent(transform, false);
                sequenceRenderer = vineVisual.AddComponent<SpriteRenderer>();
                sequenceRenderer.enabled = false;
                sequenceRenderer.sharedMaterial = sequenceArtwork.colorSpriteMaterial;
                vineVisual.layer = ColorRuntimeService.Instance.Catalog?.Find("green")?.unityLayer ?? gameObject.layer;
                // Frames are baked; shared shader sampling keeps stretched growth on the same 16-pixel grid.
                sequenceRenderer.renderingLayerMask &= ~128u;
                vineRenderer = sequenceRenderer;
                sequenceMask = new MaterialPropertyBlock();
                ColorAppearanceManager.Instance.RegisterVine(sequenceRenderer);
                lastPose = -1;
            }
            if (sequenceRenderer == null) return false;
            visualProgress = Mathf.MoveTowards(visualProgress, visualTarget, deltaTime / Mathf.Max(.1f, visualGrowthDuration));
            visibleCells = Mathf.MoveTowards(visibleCells, targetCells,
                deltaTime * Mathf.Max(1, maxGrowthBlocks) / Mathf.Max(.1f, visualGrowthDuration));
            bool idle = !burning && Mathf.Approximately(visualProgress, visualTarget);
            sequenceIdleTime = idle ? sequenceIdleTime + deltaTime : 0f;
            int index = sequenceArtwork.VineFrame(visualProgress, sequenceIdleTime, idle);
            Sprite frame = sequenceArtwork.vineGrowth[index];
            if (frame == null) return true;
            if (index != lastPose)
            {
                sequenceRenderer.sprite = frame;
                sequenceMask.Clear();
                sequenceMask.SetFloat("_ColorMaskGroup", 1f);
                sequenceMask.SetFloat("_ColorMaskReal", 1f);
                sequenceMask.SetFloat("_ColorMaskUseVertexAlpha", 1f);
                sequenceMask.SetTexture("_ColorMaskAlphaTexture", frame.texture);
                GridArtworkMetrics.ConfigurePixelSampling(sequenceMask, frame, GridCellWorldSize);
                sequenceRenderer.SetPropertyBlock(sequenceMask);
                lastPose = index;
            }
            // Atlas sprites are trimmed to their visible height and keep one common X range.
            // Fit the whole root once, not one independent animation per gameplay segment.
            VineArtworkLayout.Apply(vineVisual.transform, transform, frame, vineWidth, visibleCells,
                GridCellWorldSize, AuthoredArtworkRootBounds);
            return true;
        }

        public void BeginBurn()
        {
            if (Root != this) { Root.BeginBurn(); return; }
            if (burning || burned || !CanRunFeature) return;
            growthLift?.End();
            burning = true;
            AudioCuePlayer.Play(AudioCueIds.PlantBurn);
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
            UpdateVineVisual(0f);
            burnEffect = Project.BurnEffects.PlantBurnEffectPlayer.GetOrCreate(gameObject);
            burnEffect.Play(new[] { vineRenderer }, null, owned.Count * burnSegmentDuration, null,
                fromTop: true, exactDuration: true, cellWorldSize: GridCellWorldSize);
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
            burnEffect?.Cancel();
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
            burnEffect?.Cancel();
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
