using System.Collections.Generic;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures.Vine
{
    public interface IBlockClimbSource
    {
        bool IsClimbEnabled { get; }
        Bounds ClimbBounds { get; }
        bool Contains(Vector3 worldPosition);
        void EnterActor(GameObject actor);
        void ExitActor(GameObject actor);
    }

    [BlockFeature(
        DisplayName = "藤蔓攀爬组件",
        DefaultColorId = "green",
        Phase = BlockFeaturePhase.Simulation,
        Order = 20,
        MaxPerBlock = 1,
        Provides = new[] { typeof(IBlockClimbSource) },
        Writes = new[] { BlockChannel.Growth })]
    public sealed class BlockVineFeature : BlockFeature,
        IBlockClimbSource
    {
        [BlockParameter(
            Label = "启用时允许攀爬",
            Group = "攀爬",
            Order = 0,
            Tooltip = "绿色功能生效时，允许玩家接触该方块后进行攀爬。")]
        [SerializeField] private bool startsEnabled = true;

        [BlockParameter(
            Label = "调试日志",
            Group = "调试",
            Order = 0)]
        [SerializeField] private bool debugLog;

        private readonly HashSet<GameObject> actors =
            new HashSet<GameObject>();
        private BoxCollider volume;
        private bool climbEnabled;

        public bool IsClimbEnabled => climbEnabled;
        public Bounds ClimbBounds => ResolveBounds();

        protected override void OnAttach()
        {
            volume = GetComponent<BoxCollider>();
            SetClimbEnabled(startsEnabled);
        }

        protected override void OnDetach()
        {
            SetClimbEnabled(false);
        }

        private void OnDisable()
        {
            SetClimbEnabled(false);
        }

        private void OnDestroy()
        {
            SetClimbEnabled(false);
        }

        public void SetClimbEnabled(bool value)
        {
            if (climbEnabled == value)
            {
                return;
            }

            climbEnabled = value;
            if (!climbEnabled)
            {
                ExitAllActors();
            }

            GetComponent<ColorBlock>()?.RefreshClimbContacts();
        }

        public bool Contains(Vector3 worldPosition)
        {
            return climbEnabled &&
                   ClimbBounds.Contains(worldPosition);
        }

        public void EnterActor(GameObject actor)
        {
            if (!climbEnabled || actor == null || !actors.Add(actor))
            {
                return;
            }

            PlayerController player =
                actor.GetComponentInParent<PlayerController>();
            player?.EnterClimb(this);
            if (debugLog)
            {
                Debug.Log($"[BlockVine] Enter {actor.name}", this);
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
            player?.ExitClimb(this);
            if (debugLog)
            {
                Debug.Log($"[BlockVine] Exit {actor.name}", this);
            }
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "攀爬状态",
                climbEnabled ? "开启" : "关闭"));
            values.Add(new BlockDebugValue(
                "攀爬范围",
                FormatBounds(ClimbBounds)));
            values.Add(new BlockDebugValue(
                "当前攀爬者",
                actors.Count));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                climbEnabled ? "关闭攀爬" : "开启攀爬",
                () => SetClimbEnabled(!climbEnabled)));
            actions.Add(new BlockDebugAction(
                "开启攀爬",
                () => SetClimbEnabled(true)));
            actions.Add(new BlockDebugAction(
                "关闭攀爬",
                () => SetClimbEnabled(false)));
            actions.Add(new BlockDebugAction(
                "模拟玩家进入",
                SimulatePlayerEnter,
                FindPlayer() != null));
            actions.Add(new BlockDebugAction(
                "模拟玩家离开",
                SimulatePlayerExit,
                FindPlayer() != null));
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

        private static string FormatBounds(Bounds bounds)
        {
            return $"C {bounds.center} / S {bounds.size}";
        }

        private void OnDrawGizmosSelected()
        {
            Bounds bounds = ResolveBounds();
            Gizmos.color = new Color(.25f, 1f, .25f, .65f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        // TODO: 绿色解锁后的运行时基础外观、正式藤蔓视觉、生长方向、
        // 枝条延伸、叶片落脚点、根系区域连接和攀爬音效尚未实现。
    }
}
