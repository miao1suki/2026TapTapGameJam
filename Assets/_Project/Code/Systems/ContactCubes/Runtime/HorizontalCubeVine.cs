using System.Collections.Generic;
using Project.ColorBlocks;
using Project.BlockFeatures;
using Project.Player;
using UnityEngine;

namespace Project.ContactCubes
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-50), RequireComponent(typeof(ContactCubeMotion))]
    public sealed class HorizontalCubeVine : MonoBehaviour, IRoomColorResettable
    {
        [SerializeField, Min(.1f), InspectorName("横向攀附速度")] private float traverseSpeed = 2f;
        [SerializeField, Range(.05f,.4f), InspectorName("上下接触容差（格）")] private float contactMargin = .2f;
        private ContactCube cube;
        private ContactCubeMotion motion;
        private BoxCollider shape;
        private readonly Collider[] hits = new Collider[64];
        private readonly HashSet<PlayerController> previous = new HashSet<PlayerController>();
        private readonly HashSet<PlayerController> current = new HashSet<PlayerController>();
        private readonly HashSet<PlayerController> previousSides = new HashSet<PlayerController>();
        private readonly HashSet<PlayerController> currentSides = new HashSet<PlayerController>();
        private void Awake()
        {
            cube = GetComponent<ContactCube>(); motion = GetComponent<ContactCubeMotion>();
            shape = GetComponentInChildren<BoxCollider>();
        }
        private void FixedUpdate()
        {
            current.Clear();
            currentSides.Clear();
            if (cube.IsUnlocked && cube.Color == CubeColor.Green && motion.IsSettled && shape.enabled)
            {
                float cell = GridCellSizeUtility.Resolve(this);
                Bounds bounds = shape.bounds;
                int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * cell * .85f,
                    hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    PlayerController player = hits[i].GetComponentInParent<PlayerController>();
                    if (player == null || current.Contains(player)) continue;
                    var capsule = player.GetComponent<CapsuleCollider>();
                    if (capsule == null) continue;
                    Bounds p = capsule.bounds;
                    if (cube.Length > 1 && Mathf.Abs(cube.GrowthDirection.x) > .98f)
                        player.ProtectClimbCorner(this, bounds, traverseSpeed);
                    if (player.IsClimbCornerProtected) current.Add(player);
                    bool side = p.max.y > bounds.min.y+.02f && p.min.y < bounds.max.y-.02f &&
                        p.max.z >= bounds.min.z && p.min.z <= bounds.max.z &&
                        (Mathf.Abs(p.max.x-bounds.min.x) <= cell*contactMargin ||
                         Mathf.Abs(p.min.x-bounds.max.x) <= cell*contactMargin);
                    if (side)
                    {
                        player.EnterClimb(this);
                        player.SetClimbTopHeight(this,bounds.max.y);
                        currentSides.Add(player);
                    }
                    if (cube.Length <= 1 || Mathf.Abs(cube.GrowthDirection.x) <= .98f) continue;
                    bool grown = p.center.x >= bounds.min.x - contactMargin * cell && p.center.x <= bounds.max.x + contactMargin * cell;
                    if (!grown || p.max.z < bounds.min.z || p.min.z > bounds.max.z) continue;
                    bool top = Mathf.Abs(p.min.y - bounds.max.y) <= cell * contactMargin &&
                        (player.Motor.linearVelocity.y <= .1f || player.IsHorizontalClimbing);
                    bool bottom = Mathf.Abs(p.max.y - bounds.min.y) <= cell * contactMargin;
                    if (!top && !bottom) continue;
                    float target = player.Motor.position.y + (top ? bounds.max.y + .015f - p.min.y : bounds.min.y - .015f - p.max.y);
                    player.SetHorizontalVineSurface(this, target, traverseSpeed); current.Add(player);
                }
            }
            foreach (var player in previous) if (player != null && !current.Contains(player))
            { player.CancelClimbCorner(this); player.ClearHorizontalVineSurface(this); }
            previous.Clear(); foreach (var player in current) previous.Add(player);
            foreach (var player in previousSides) if (player != null && !currentSides.Contains(player)) player.ExitClimb(this);
            previousSides.Clear(); foreach (var player in currentSides) previousSides.Add(player);
        }
        public void ResetForRoom()
        {
            foreach (var player in previous) if (player != null) { player.CancelClimbCorner(this); player.ClearHorizontalVineSurface(this); }
            previous.Clear(); current.Clear();
            foreach (var player in previousSides) if (player != null) player.ExitClimb(this);
            previousSides.Clear(); currentSides.Clear();
        }
        private void OnDisable() => ResetForRoom();
    }
}
