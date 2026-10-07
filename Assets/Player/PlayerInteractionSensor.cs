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

        private readonly Collider[] overlapBuffer =
            new Collider[32];
        private readonly RaycastHit[] screenRaycastBuffer =
            new RaycastHit[32];
        public IInteractionTarget CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;
        public float ScanRadius => scanRadius;

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

        public bool TryGetScreenComponent<T>(
            Camera camera,
            Vector2 screenPosition,
            out T target)
            where T : class
        {
            target = null;
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
                QueryTriggerInteraction.Collide);
            Collider nearestCollider = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = screenRaycastBuffer[index];
                if (hit.collider == null ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestCollider = hit.collider;
            }

            if (nearestCollider == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours =
                nearestCollider.GetComponentsInParent<MonoBehaviour>();
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
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                scanRadius,
                overlapBuffer,
                interactionMask.value,
                QueryTriggerInteraction.Collide);
            IInteractionTarget nearest = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                Collider collider = overlapBuffer[index];
                if (collider == null)
                {
                    continue;
                }

                MonoBehaviour[] behaviours =
                    collider.GetComponentsInParent<MonoBehaviour>();
                for (int behaviourIndex = 0;
                     behaviourIndex < behaviours.Length;
                     behaviourIndex++)
                {
                    if (!(behaviours[behaviourIndex] is
                            IInteractionTarget target) ||
                        !target.CanInteract(gameObject))
                    {
                        continue;
                    }

                    Vector3 closest = collider.ClosestPoint(transform.position);
                    float distance = Vector3.Distance(
                        transform.position,
                        closest);
                    if (distance > scanRadius || distance >= nearestDistance)
                    {
                        continue;
                    }

                    nearestDistance = distance;
                    nearest = target;
                }
            }

            return nearest;
        }
    }
}
