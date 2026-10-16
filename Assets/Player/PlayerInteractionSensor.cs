using System.Collections.Generic;
using UnityEngine;
using Project.InputAbstraction;

namespace Project.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractionSensor : MonoBehaviour
    {
        [SerializeField, Min(0.5f)]
        private float scanRadius = 5f;

        [SerializeField]
        private LayerMask interactionMask = ~0;

        private Collider[] overlapBuffer = new Collider[128];
        private readonly RaycastHit[] screenRaycastBuffer =
            new RaycastHit[32];
        public IInteractionTarget CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;
        public float ScanRadius => scanRadius;
        private PlayerController player;
        private readonly List<MonoBehaviour> targetBehaviours = new List<MonoBehaviour>();

        // Keep close ties stable, but immediately discard objects behind the player.
        public static float FacingScore(PlayerController player, Vector3 position)
        {
            Vector3 delta = position - player.transform.position;
            float forward = delta.x * player.FacingDirection;
            if (forward < -.05f * player.GridCellWorldSize) return float.PositiveInfinity;
            return delta.sqrMagnitude;
        }

        public bool IsWithinRange(IInteractionTarget target)
        {
            Component component = target as Component;
            return IsWithinRange(component);
        }

        public bool IsWithinRange(Component component)
        {
            Behaviour behaviour = component as Behaviour;
            if (component == null || behaviour == null ||
                !behaviour.isActiveAndEnabled)
            {
                return false;
            }

            float rangeSqr = scanRadius * scanRadius;
            Collider[] colliders = component.GetComponentsInChildren<Collider>(true);
            bool foundCollider = false;
            float nearestSqr = float.MaxValue;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                foundCollider = true;
                Vector3 closest = collider.ClosestPoint(transform.position);
                nearestSqr = Mathf.Min(
                    nearestSqr,
                    (closest - transform.position).sqrMagnitude);
            }

            if (!foundCollider)
            {
                nearestSqr = (component.transform.position - transform.position)
                    .sqrMagnitude;
            }

            return nearestSqr <= rangeSqr;
        }

        public bool TryGetScreenTarget(
            Camera camera,
            Vector2 screenPosition,
            out IInteractionTarget target)
        {
            if (!TryGetScreenComponent(
                    camera,
                    screenPosition,
                    out IInteractionTarget candidate) ||
                !candidate.CanInteract(gameObject))
            {
                target = null;
                return false;
            }

            target = candidate;
            return true;
        }

        public bool TryGetScreenHit(
            Camera camera,
            Vector2 screenPosition,
            out RaycastHit hit)
        {
            return TryGetScreenHit(
                camera,
                screenPosition,
                out hit,
                QueryTriggerInteraction.Collide);
        }

        public bool TryGetScreenHit(
            Camera camera,
            Vector2 screenPosition,
            out RaycastHit hit,
            QueryTriggerInteraction triggerInteraction)
        {
            hit = default;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(
                ray,
                screenRaycastBuffer,
                Mathf.Infinity,
                interactionMask.value,
                triggerInteraction);
            float nearestDistance = float.MaxValue;
            bool found = false;
            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = screenRaycastBuffer[index];
                if (candidate.collider == null ||
                    candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidate.distance;
                hit = candidate;
                found = true;
            }

            return found;
        }

        public bool TryGetScreenComponent<T>(
            Camera camera,
            Vector2 screenPosition,
            out T target)
            where T : class
        {
            target = null;
            if (!TryGetScreenHit(
                    camera,
                    screenPosition,
                    out RaycastHit hit) ||
                hit.collider == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours =
                hit.collider.GetComponentsInParent<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is T candidate)
                {
                    target = candidate;
                    return true;
                }
            }

            return false;
        }

        public void RefreshTarget()
        {
            CurrentTarget = FindNearestTarget();
        }

        public bool TryInteract()
        {
            return CurrentTarget != null &&
                   CurrentTarget.TryInteract(gameObject);
        }

        private IInteractionTarget FindNearestTarget()
        {
            if (player == null) player = GetComponent<PlayerController>();
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                scanRadius,
                overlapBuffer,
                interactionMask.value,
                QueryTriggerInteraction.Collide);
            while (count == overlapBuffer.Length && overlapBuffer.Length < 4096)
            {
                System.Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
                count = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, overlapBuffer,
                    interactionMask.value, QueryTriggerInteraction.Collide);
            }
            IInteractionTarget nearest = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                Collider collider = overlapBuffer[index];
                if (collider == null)
                {
                    continue;
                }

                targetBehaviours.Clear();
                collider.GetComponentsInParent(false, targetBehaviours);
                for (int behaviourIndex = 0;
                     behaviourIndex < targetBehaviours.Count;
                     behaviourIndex++)
                {
                    if (!(targetBehaviours[behaviourIndex] is
                            IInteractionTarget target) ||
                        !target.CanInteract(gameObject))
                    {
                        continue;
                    }

                    Vector3 closest = collider.ClosestPoint(transform.position);
                    float distance = FacingScore(player, collider.bounds.center);
                    if (float.IsPositiveInfinity(distance) ||
                        (closest - transform.position).sqrMagnitude > scanRadius * scanRadius)
                    {
                        continue;
                    }

                    if (ReferenceEquals(target, CurrentTarget)) distance -= .05f * scanRadius;
                    if (distance > nearestDistance || (Mathf.Approximately(distance, nearestDistance) &&
                        nearest is Component previous && targetBehaviours[behaviourIndex].GetInstanceID() >= previous.GetInstanceID())) continue;

                    nearestDistance = distance;
                    nearest = target;
                }
            }

            return nearest;
        }
    }
}
