using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.Interactions
{
    [DisallowMultipleComponent]
    public sealed class GreenBouncePad : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minimumFallDistance = 2f;
        [SerializeField, Min(0f)] private float maximumFallDistance = 8f;
        [SerializeField, Min(0f)] private float minimumBounceSpeed = 10f;
        [SerializeField, Min(0f)] private float maximumBounceSpeed = 20f;
        [SerializeField, Min(0.01f)] private float nextJumpMultiplier = 1.5f;
        private readonly HashSet<PlayerFallDamage> actors =
            new HashSet<PlayerFallDamage>();

        public void RegisterBounceActor(GameObject actor)
        {
            PlayerFallDamage fall = actor != null
                ? actor.GetComponentInParent<PlayerFallDamage>()
                : null;
            if (fall == null || !actors.Add(fall)) return;
            fall.Landed += OnActorLanded;
        }

        public void UnregisterBounceActor(GameObject actor)
        {
            PlayerFallDamage fall = actor != null
                ? actor.GetComponentInParent<PlayerFallDamage>()
                : null;
            if (fall == null || !actors.Remove(fall)) return;
            fall.Landed -= OnActorLanded;
        }

        public void ArmNextJump(GameObject unusedContext)
        {
            NextJumpBounceService.Arm(nextJumpMultiplier);
        }

        private void OnActorLanded(PlayerFallDamage actor, float fallDistance)
        {
            if (fallDistance < minimumFallDistance) return;
            float normalized = Mathf.InverseLerp(
                minimumFallDistance,
                Mathf.Max(minimumFallDistance, maximumFallDistance),
                fallDistance);
            float curved = Mathf.Pow(Mathf.Clamp01(normalized), 1.8f);
            if (actor == null || !actors.Contains(actor) ||
                actor.GetComponent<PlayerController>()?.IsGrounded != true ||
                !IsActorOnTop(actor.gameObject))
            {
                return;
            }
            actor.ApplyBounce(Mathf.Lerp(
                minimumBounceSpeed,
                maximumBounceSpeed,
                curved));
        }

        private bool IsActorOnTop(GameObject actor)
        {
            Collider pad = GetComponent<Collider>();
            Collider body = actor != null
                ? actor.GetComponentInParent<Collider>()
                : null;
            if (pad == null || body == null || pad.isTrigger || body.isTrigger)
            {
                return false;
            }

            Bounds padBounds = pad.bounds;
            Bounds bodyBounds = body.bounds;
            return bodyBounds.min.y >= padBounds.max.y - .2f &&
                   bodyBounds.center.x >= padBounds.min.x &&
                   bodyBounds.center.x <= padBounds.max.x &&
                   bodyBounds.center.z >= padBounds.min.z &&
                   bodyBounds.center.z <= padBounds.max.z;
        }

        private void OnDisable()
        {
            foreach (PlayerFallDamage actor in actors)
            {
                if (actor != null) actor.Landed -= OnActorLanded;
            }
            actors.Clear();
        }
    }
}
