using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.Mechanisms
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider)), DefaultExecutionOrder(-200)]
    public sealed class OneWayPlatform : MonoBehaviour, IRoomColorResettable
    {
        [SerializeField, Min(0f), InspectorName("顶面容差（格）")]
        private float topToleranceCells = .05f;
        private BoxCollider surface;
        private readonly Dictionary<Collider, bool> pairs = new Dictionary<Collider, bool>();
        private readonly Dictionary<Collider, float> previousFeet = new Dictionary<Collider, float>();
        private void Awake() => surface = GetComponent<BoxCollider>();
        public bool CanSupport(PlayerController player)
        {
            if (player == null || surface == null || !surface.enabled) return false;
            Collider body = player.GetComponent<Collider>();
            if (body == null || player.VerticalVelocity > .1f) return false;
            float feet = Feet(player, body);
            float threshold = surface.bounds.max.y - topToleranceCells * GridCellSizeUtility.Resolve(this);
            return feet >= threshold || previousFeet.TryGetValue(body, out float previous) && previous >= threshold;
        }
        private void FixedUpdate()
        {
            foreach (PlayerController player in PlayerController.ActivePlayers)
            {
                if (player == null || player.gameObject.scene != gameObject.scene) continue;
                Collider body = player.GetComponent<Collider>();
                if (body == null) continue;
                if (!pairs.ContainsKey(body)) pairs.Add(body, Physics.GetIgnoreCollision(surface, body));
                Physics.IgnoreCollision(surface, body, pairs[body] || !CanSupport(player));
                previousFeet[body] = Feet(player, body);
            }
        }
        public void ResetForRoom() => Restore();
        private void OnDisable() => Restore();
        private void Restore()
        {
            if (surface != null) foreach (var pair in pairs) if (pair.Key != null) Physics.IgnoreCollision(surface, pair.Key, pair.Value);
            pairs.Clear();
            previousFeet.Clear();
        }
        private static float Feet(PlayerController player, Collider body) => body.bounds.min.y + player.Motor.position.y - player.transform.position.y;
    }
}
