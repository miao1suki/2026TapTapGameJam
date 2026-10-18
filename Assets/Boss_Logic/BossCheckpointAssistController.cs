using System.Collections.Generic;
using Project.BlockFeatures;
using Project.Player;
using UnityEngine;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController))]
    [RequireComponent(typeof(BossRightMoveExecutor))]
    [AddComponentMenu("2026TapTap/Boss/出生点弱补助")]
    public sealed class BossCheckpointAssistController :
        MonoBehaviour
    {
        [Header("弱补助")]
        [SerializeField, Min(1f)]
        private float horizontalSpeedMultiplier = 1.35f;
        [SerializeField, Min(.01f)]
        private float maximumDurationSeconds = 15f;
        [SerializeField, Min(0f)]
        private float horizontalToleranceBlocks = .25f;
        [SerializeField, Min(0f)]
        private float verticalAssistSpeedBlocksPerSecond = .75f;

        private readonly List<RespawnAnchorFeature> anchors =
            new List<RespawnAnchorFeature>();
        private BossRightMoveExecutor mover;
        private RespawnAnchorFeature activeAnchor;
        private PlayerController activePlayer;

        public BossRightMoveExecutor Mover => mover;
        public RespawnAnchorFeature ActiveAnchor =>
            activeAnchor;

        private void OnEnable()
        {
            mover = GetComponent<BossRightMoveExecutor>();
            BindAnchors();
        }

        private void OnDisable()
        {
            UnbindAnchors();
            activeAnchor = null;
            activePlayer = null;
        }

        private void Update()
        {
            if (activeAnchor != null &&
                mover != null &&
                !mover.IsCheckpointAssistActive)
            {
                activeAnchor = null;
                activePlayer = null;
            }
        }

        private void BindAnchors()
        {
            UnbindAnchors();
            IReadOnlyList<RespawnAnchorFeature> found =
                ProjectDiscovery.FindAll<
                    RespawnAnchorFeature>(true);
            for (int index = 0; index < found.Count; index++)
            {
                RespawnAnchorFeature anchor = found[index];
                if (anchor == null)
                {
                    continue;
                }

                anchors.Add(anchor);
                anchor.NewlyActivatedByPlayer -=
                    HandleAnchorActivated;
                anchor.NewlyActivatedByPlayer +=
                    HandleAnchorActivated;
            }
        }

        private void UnbindAnchors()
        {
            for (int index = 0; index < anchors.Count; index++)
            {
                RespawnAnchorFeature anchor = anchors[index];
                if (anchor != null)
                {
                    anchor.NewlyActivatedByPlayer -=
                        HandleAnchorActivated;
                }
            }

            anchors.Clear();
        }

        private void HandleAnchorActivated(
            RespawnAnchorFeature anchor,
            PlayerController player)
        {
            if (anchor == null ||
                player == null ||
                player.IsDead ||
                player.IsRespawning ||
                mover == null)
            {
                return;
            }

            activeAnchor = anchor;
            activePlayer = player;
            mover.BeginCheckpointAssist(
                anchor.BossRespawnPosition,
                horizontalSpeedMultiplier,
                maximumDurationSeconds,
                verticalAssistSpeedBlocksPerSecond,
                horizontalToleranceBlocks);
        }
    }
}
