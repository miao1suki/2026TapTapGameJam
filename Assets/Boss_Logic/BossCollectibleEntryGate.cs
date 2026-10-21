using System;
using Project.BlockFeatures;
using Project.InputAbstraction;
using Project.Items;
using Project.Player;
using Project.Rooms;
using Project.WordNote;
using UnityEngine;

namespace Project.BossLogic
{
    /// <summary>Author on the Boss-entry ContactTimelineTrigger, not on every Timeline trigger.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ContactTimelineTrigger))]
    [AddComponentMenu("2026TapTap/Boss/收集进度入场门槛")]
    public sealed class BossCollectibleEntryGate : MonoBehaviour, IContactTimelineCondition
    {
        [SerializeField, InspectorName("是否启用该组件")] private bool gateEnabled = true;
        [SerializeField, Range(0f, 1f), InspectorName("入场所需收集比例")] private float minimumRatio = .3f;
        [SerializeField, InspectorName("未达标返回传送ID")] private string returnDestinationId = "Start";
        [SerializeField, InspectorName("门外返回位置（优先）")] private Transform returnPoint;
        [SerializeField, InspectorName("显示未达标提示")] private bool showHint = true;
        private float nextRejection;
        public bool GateEnabled => gateEnabled;
        public float MinimumRatio => minimumRatio;
        public CollectibleProgressSnapshot Progress => CollectibleProgressService.GetWorldProgress();
        public bool CanEnter => !gateEnabled || Progress.MeetsRatio(minimumRatio);

        public bool CanPlayTimeline(GameObject actor)
        {
            if (!gateEnabled) return true;
            PlayerController player = actor != null ? actor.GetComponentInParent<PlayerController>() : null;
            if (player == null) return false;
            if (CanEnter) return true;
            // Multiple player colliders can enter during the same physics step.
            if (Time.unscaledTime < nextRejection) return false;
            nextRejection = Time.unscaledTime + .5f;
            if (showHint)
                WordNoteUI.Instance?.ShowTimed($"收集进度不足{minimumRatio:P0}，暂时无法进入", 2f, WordNotePosition.Bottom);
            Transform destination = returnPoint;
            TeleportAnchorFeature destinationAnchor = null;
            if (destination == null && TeleportAnchorFeature.TryGetAnchor(returnDestinationId, out var anchor))
            {
                destinationAnchor = anchor;
                destination = anchor.transform;
            }
            if (destination != null && destination.gameObject.scene == player.gameObject.scene)
            {
                if (destinationAnchor != null)
                {
                    if (!player.TryTeleportToStoryAnchor(destinationAnchor))
                        Debug.LogWarning("[BossEntry] 同场景返回锚点暂不可用：" + returnDestinationId, this);
                    else if (IsStartReturn())
                        EmitReturnedHome(player);
                    return false;
                }
                // Explicit author Transform overrides retain their legacy path;
                // the default Start anchor always uses the controller contract.
                player.PrepareForRoomReset();
                RoomLifecycleManager.BeforeRespawn(player, destination.position);
                if (player.Motor != null)
                {
                    player.Motor.position = destination.position;
                    if (!player.Motor.isKinematic)
                    {
                        player.Motor.linearVelocity = Vector3.zero;
                        player.Motor.angularVelocity = Vector3.zero;
                    }
                    player.Motor.WakeUp();
                }
                player.transform.position = destination.position;
                Physics.SyncTransforms();
                if (IsStartReturn())
                    EmitReturnedHome(player);
            }
            else
            {
                Debug.LogWarning("[BossEntry] 收集进度不足，已拒绝入场但缺少当前玩家同场景的返回目标 " +
                    returnDestinationId + "。正式Boss与关卡统一合入Gaming并使用Start；独立Boss测试请显式配置本场景锚点，不会自动切换场景。", this);
            }
            return false;
        }

        private bool IsStartReturn()
        {
            return string.Equals(
                returnDestinationId,
                "Start",
                StringComparison.Ordinal);
        }

        private static void EmitReturnedHome(
            PlayerController player)
        {
            GameplaySignalHub.Emit(
                AchievementSignalIds
                    .LowCollectibleReturnHome,
                player.gameObject);
        }
    }
}
