using System.Collections.Generic;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.Player;
using UnityEngine;

namespace Project.Mechanisms
{
    public enum LadderWallSide
    {
        [InspectorName("右侧连墙（从左侧攀爬）")] Right,
        [InspectorName("左侧连墙（从右侧攀爬）")] Left
    }
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class NeutralLadder : MonoBehaviour
    {
        [BlockParameter(Label = "攀爬倍率", Group = "攀爬", Order = 0)]
        [SerializeField, Min(.01f)] private float climbMultiplier = 1f;
        [BlockParameter(Label = "自然下滑速度", Group = "攀爬", Order = 1)]
        [SerializeField, Min(0)] private float slideSpeed = .5f;
        [BlockParameter(Label = "加速下滑速度", Group = "攀爬", Order = 2)]
        [SerializeField, Min(0)] private float fastSlideSpeed = 3f;
        [SerializeField, InspectorName("侧面梯子")] private bool sideFacing;
        [BlockParameter(Label = "靠墙方向", Group = "攀爬", Order = 3)]
        [SerializeField] private LadderWallSide wallSide;
        private BoxCollider area;
        private readonly Dictionary<Collider, PlayerController> contacts = new();
        private readonly List<Collider> expired = new();
        public bool SideFacing => sideFacing;
        public LadderWallSide WallSide => wallSide;
        public int ContactCount => contacts.Count;

        private void Awake() { area = GetComponent<BoxCollider>(); area.isTrigger = true; PreserveLegacyFootprint(); }

        internal void PreserveLegacyFootprint()
        {
            if (area == null) area = GetComponent<BoxCollider>();
            if (area == null || !Mathf.Approximately(area.size.y, 1f) || !Mathf.Approximately(area.center.y, 0f)) return;
            var placed = GetComponent<LevelEditorPlacedBlock>();
            if (placed == null || placed.SizeCells.y <= 1 ||
                !Mathf.Approximately(Mathf.Abs(transform.lossyScale.y), placed.CellWorldSize)) return;
            // Old scene instances use a bottom-cell pivot and inherit the shared prefab
            // collider. New rectangular ladders already carry their height in root scale.
            var size = area.size; size.y = placed.SizeCells.y; area.size = size;
            var center = area.center; center.y = (placed.SizeCells.y - 1) * .5f; area.center = center;
        }
        private void OnTriggerEnter(Collider other) => Track(other);
        private void OnTriggerStay(Collider other) => Track(other);
        private void Track(Collider other)
        {
            if (!isActiveAndEnabled || other == null || other.isTrigger) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.isActiveAndEnabled) return;
            contacts[other] = player;
            player.EnterClimb(this);
            player.SetClimbTopHeight(this, area.bounds.max.y);
            player.SetClimbSpeedMultiplier(this, climbMultiplier);
            player.SetClimbSlideSpeed(this, slideSpeed);
            player.SetClimbFastSlideSpeed(this, fastSlideSpeed);
        }
        private void OnTriggerExit(Collider other) => Forget(other);
        private void Forget(Collider other)
        {
            if (!contacts.TryGetValue(other, out var player)) return;
            contacts.Remove(other);
            foreach (var remaining in contacts.Values) if (remaining == player) return;
            Release(player);
        }
        private void FixedUpdate()
        {
            expired.Clear();
            foreach (var pair in contacts)
                if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy ||
                    pair.Value == null || !pair.Value.isActiveAndEnabled) expired.Add(pair.Key);
            foreach (var collider in expired) Forget(collider);
        }
        private void Release(PlayerController player)
        {
            if (player == null) return;
            player.ExitClimb(this);
            player.SetClimbSpeedMultiplier(this, 0);
            player.ClearClimbSlideSpeed(this);
            player.ClearClimbFastSlideSpeed(this);
        }
        private void OnDisable()
        {
            foreach (var player in contacts.Values) Release(player);
            contacts.Clear();
        }
    }
}
