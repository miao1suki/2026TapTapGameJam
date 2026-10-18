using System.Collections.Generic;
using Project.GreenHook;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    public enum BounceTriggerMode { FallingOnly = 0, JumpOnly = 1, Both = 2 }

    [BlockFeature(DisplayName = "弹性植物", DefaultColorId = "green", Category = BlockFeatureCategory.Plant,
        Interactions = BlockFeatureInteraction.AppliedColor | BlockFeatureInteraction.RoomReset, Writes = new[] { BlockChannel.Motion })]
    public sealed class BouncePlantFeature : BlockFeature,
        IPlayerBounceSurface,
        IGreenHookTarget
    {
        [BlockParameter(Label = "玩家标签", Group = "弹跳", Order = 0)]
        [SerializeField] private string playerTag = "Player";
        [BlockParameter(Label = "普通回弹高度（格）", Group = "弹跳", Order = 1, Tooltip = "只影响本实例的普通回弹高度；关卡编辑器参数模式可在放置前或选中植物后调整。")]
        [SerializeField, Min(.1f)] private float normalRiseCells = 8f;
        [BlockParameter(Label = "高跳落差门槛（格）", Group = "弹跳", Order = 2, Tooltip = "落差超过门槛，且落地时按住跳跃，触发高跳。")]
        [SerializeField, Min(0f)] private float highFallThresholdCells = 4f;
        [BlockParameter(Label = "高跳高度（格）", Group = "弹跳", Order = 3)]
        [SerializeField, Min(.1f)] private float highRiseCells = 8f;
        [BlockParameter(Label = "搬运跳跃高度（格）", Group = "搬运", Order = 0, Tooltip = "站到植物顶面后恢复一次携带跳跃，离开植物仍可使用。")]
        [SerializeField, Min(.1f)] private float carryRiseCells = 4f;
        [BlockParameter(Label = "触发间隔（秒）", Group = "弹跳", Order = 4)]
        [SerializeField, Min(.05f)] private float minTriggerInterval = .15f;
        private bool burning, burned;
        private Collider[] burnColliders;
        private bool[] colliderStates;
        private Project.BurnEffects.PlantBurnEffectPlayer burnEffect;
        public bool ShowsArtwork => !burned;
        public bool IsBounceSurfaceAvailable => CanRunFeature && !burning && !burned;
        public float NormalRiseCells => normalRiseCells;
        public float HighRiseCells => highRiseCells;
        public float CarryRiseCells => carryRiseCells;
        public GreenHookMotionMode GreenHookMotionMode =>
            GreenHookMotionMode.Swing;
        public Transform GreenHookTransform => transform;
        public bool CanBeGreenHooked(GameObject actor) =>
            IsBounceSurfaceAvailable;
        public event System.Action BounceTriggered;
        public event System.Action AppearanceReset;

        protected override void OnAttach() => WorldObjectSpritePresentation.Ensure(gameObject);
        protected override void OnResetForRoom() { RestoreBurn(); AppearanceReset?.Invoke(); }

        public float GetRiseCells(float fallCells, bool jumpHeld) => fallCells > highFallThresholdCells && jumpHeld ? highRiseCells : normalRiseCells;
        public bool TryGetBounceSpeed(PlayerController player, float fallDistance, float downwardSpeed, out float verticalSpeed)
        {
            verticalSpeed = 0f;
            if (!IsBounceSurfaceAvailable || player == null || player.IsCarrying || fallDistance <= .2f || player.MoveInput.y < -.1f || !player.CanBounce(minTriggerInterval)) return false;
            float cells = GetRiseCells(fallDistance / GridCellWorldSize, player.JumpHeld);
            verticalSpeed = player.CalculateGridRiseSpeed(cells);
            player.RegisterBounce(1f, minTriggerInterval);
            BounceTriggered?.Invoke();
            return true;
        }

        private void OnCollisionEnter(Collision collision) => TrackTop(collision);
        private void OnCollisionStay(Collision collision) => TrackTop(collision);
        private void TrackTop(Collision collision)
        {
            if (!IsBounceSurfaceAvailable || collision == null) return;
            var player = collision.collider.GetComponentInParent<PlayerController>();
            if (player == null || !player.CompareTag(playerTag)) return;
            Collider plant = collision.contactCount > 0 ? collision.GetContact(0).thisCollider : GetComponent<Collider>();
            Collider body = player.GetComponent<Collider>();
            if (plant == null || body == null) return;
            Bounds p = plant.bounds, b = body.bounds;
            bool top = b.min.y >= p.max.y - GridCellWorldSize * .12f && b.center.x >= p.min.x && b.center.x <= p.max.x && player.VerticalVelocity <= .1f;
            if (!top) { player.ExitBounceSurface(this); return; }
            player.EnterBounceSurface(this);
            player.GrantCarryPlantJump(carryRiseCells);
        }
        private void OnCollisionExit(Collision collision)
        {
            var player = collision?.collider.GetComponentInParent<PlayerController>();
            if (player != null) player.ExitBounceSurface(this);
        }
        protected override void OnDetach()
        {
            RestoreBurn();
            AppearanceReset?.Invoke();
            foreach (var player in PlayerController.ActivePlayers) if (player != null) player.ExitBounceSurface(this);
        }
        protected override bool OnColorApplied(string colorId, GameObject actor)
        {
            if (colorId != "red" || burning || burned) return false;
            burning = true;
            foreach (var player in PlayerController.ActivePlayers) if (player != null) player.ExitBounceSurface(this);
            burnColliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[burnColliders.Length];
            for (int i = 0; i < burnColliders.Length; i++) { colliderStates[i] = burnColliders[i].enabled; burnColliders[i].enabled = false; }
            WorldObjectSpritePresentation.Ensure(gameObject);
            Project.Audio.AudioCuePlayer.Play(Project.Audio.AudioCueIds.PlantBurn);
            burnEffect = Project.BurnEffects.PlantBurnEffectPlayer.GetOrCreate(gameObject);
            if (!burnEffect.Play(GetComponentsInChildren<Renderer>(true), null, 0, FinishBurn,
                cellWorldSize: GridCellWorldSize)) FinishBurn();
            return true;
        }
        private void FinishBurn()
        {
            burning = false; burned = true;
            if (!Project.Rooms.RoomResetObject.HideForReaction(gameObject)) Destroy(gameObject);
        }
        private void RestoreBurn()
        {
            burnEffect?.Cancel();
            burning = burned = false;
            if (burnColliders != null) for (int i = 0; i < burnColliders.Length; i++)
                if (burnColliders[i] != null) burnColliders[i].enabled = colliderStates[i];
            burnColliders = null; colliderStates = null;
        }
        public override void CollectDebugValues(List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue("普通回弹", $"{normalRiseCells} 格"));
            values.Add(new BlockDebugValue("高跳", $"{highRiseCells} 格"));
            values.Add(new BlockDebugValue("搬运跳跃", $"{carryRiseCells} 格"));
        }
    }
}
