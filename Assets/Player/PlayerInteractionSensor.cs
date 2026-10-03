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
        public IInteractionTarget CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;

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

                    float distance = Vector3.Distance(
                        transform.position,
                        collider.bounds.center);
                    if (distance >= nearestDistance)
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
