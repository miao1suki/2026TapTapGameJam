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
        private void Awake()
        {
            cube = GetComponent<ContactCube>(); motion = GetComponent<ContactCubeMotion>();
            shape = GetComponentInChildren<BoxCollider>();
        }
        private void FixedUpdate()
        {
            current.Clear();
            if (cube.Color == CubeColor.Green && cube.Length > 1 && motion.IsSettled &&
                Mathf.Abs(cube.GrowthDirection.x) > .98f)
            {
                float cell = GridCellSizeUtility.Resolve(this);
                Bounds bounds = shape.bounds;
                int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.up * cell * contactMargin,
                    hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    PlayerController player = hits[i].GetComponentInParent<PlayerController>();
                    if (player == null || current.Contains(player)) continue;
                    var capsule = player.GetComponent<CapsuleCollider>();
                    if (capsule == null) continue;
                    Bounds p = capsule.bounds;
                    float end = transform.position.x + Mathf.Sign(cube.GrowthDirection.x) * cell * .5f;
                    bool grown = cube.GrowthDirection.x > 0 ? p.center.x > end && p.center.x < bounds.max.x : p.center.x < end && p.center.x > bounds.min.x;
                    if (!grown || p.max.z < bounds.min.z || p.min.z > bounds.max.z) continue;
                    bool top = Mathf.Abs(p.min.y - bounds.max.y) <= cell * contactMargin && player.Motor.linearVelocity.y <= .1f;
                    bool bottom = Mathf.Abs(p.max.y - bounds.min.y) <= cell * contactMargin;
                    if (!top && !bottom) continue;
                    float target = player.Motor.position.y + (top ? bounds.max.y + .015f - p.min.y : bounds.min.y - .015f - p.max.y);
                    player.SetHorizontalVineSurface(this, target, traverseSpeed); current.Add(player);
                }
            }
            foreach (var player in previous) if (player != null && !current.Contains(player)) player.ClearHorizontalVineSurface(this);
            previous.Clear(); foreach (var player in current) previous.Add(player);
        }
        public void ResetForRoom()
        {
            foreach (var player in previous) if (player != null) player.ClearHorizontalVineSurface(this);
            previous.Clear(); current.Clear();
        }
        private void OnDisable() => ResetForRoom();
    }
}
