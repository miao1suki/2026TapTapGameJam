using System.Collections;
using System.Collections.Generic;
using Project.Player;
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
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Growth })]
    public class ClimbableVineFeature : BlockFeature,
        IVineSegmentAppearance
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

        private int PlayerCount => climbingPlayers.Count;
        public int CurrentGrowthBlocks => spawnedSegments.Count;
        public int MaxGrowthBlocks => maxGrowthBlocks;
        public float ClimbSpeedMultiplier => climbSpeedMultiplier;

        protected override void OnTick(float deltaTime)
        {
            RefreshClimbingPlayers();
        }

        protected override void OnResetForRoom()
        {
            DisconnectAllPlayers();
            ResetGrowth();
        }

        protected override void OnDetach()
        {
            DisconnectAllPlayers();
            ResetGrowth();
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
                Destroy(gameObject);
                return true;
            }

            if (!string.Equals(
                    colorId,
                    "blue",
                    System.StringComparison.OrdinalIgnoreCase) ||
                !CanGrowFromColor ||
                spawnedSegments.Count >= maxGrowthBlocks)
            {
                return false;
            }

            GrowTo(growInstantly
                ? maxGrowthBlocks
                : spawnedSegments.Count + 1);
            return true;
        }

        protected virtual bool CanGrowFromColor =>
            allowGrowthFromColor;

        private void RefreshClimbingPlayers()
        {
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
            GrowTo(spawnedSegments.Count + 1);
        }

        public void GrowTo(int targetCount)
        {
            targetCount = Mathf.Clamp(
                targetCount,
                0,
                maxGrowthBlocks);
            if (targetCount <= spawnedSegments.Count)
            {
                return;
            }

            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
                growthRoutine = null;
            }

            if (growInstantly)
            {
                while (spawnedSegments.Count < targetCount)
                {
                    if (SpawnSegment(
                        spawnedSegments.Count,
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
                $"{spawnedSegments.Count} / {maxGrowthBlocks}"));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                "向上生长一块",
                GrowOne,
                Application.isPlaying &&
                spawnedSegments.Count < maxGrowthBlocks));
            actions.Add(new BlockDebugAction(
                "清除生长",
                ResetGrowth,
                Application.isPlaying &&
                spawnedSegments.Count > 0));
        }

        private IEnumerator GrowRoutine(int targetCount)
        {
            while (spawnedSegments.Count < targetCount)
            {
                if (SpawnSegment(
                    spawnedSegments.Count,
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
}
