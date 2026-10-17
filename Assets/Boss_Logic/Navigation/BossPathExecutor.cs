using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController))]
    [RequireComponent(typeof(BossPathfinder))]
    [AddComponentMenu("2026TapTap/Boss/路径执行器")]
    public sealed class BossPathExecutor : MonoBehaviour
    {
        [Header("执行")]
        [SerializeField] private bool execute = true;
        [SerializeField, Min(.05f)] private float arriveDistance = .25f;

        [Header("自动越障")]
        [SerializeField, Min(0f)] private float autoJumpHeight = .35f;
        [SerializeField, Min(0f)] private float autoDropHeight = .35f;
        [SerializeField] private bool autoJumpOnObstacle = true;
        [SerializeField, Min(.05f)] private float obstacleProbePadding = .12f;
        [SerializeField, Min(0f)] private float noAutoJumpNearTarget = 2f;

        [Header("跳跃")]
        [SerializeField, Min(.05f)] private float jumpRequestInterval = .25f;

        private BossController controller;
        private BossPathfinder pathfinder;
        private int appliedPathVersion = -1;
        private int stepIndex;
        private float nextJumpRequestTime;
        private bool climbing;

        public bool IsExecuting => execute;
        public int StepIndex => stepIndex;

        private void Awake()
        {
            controller = GetComponent<BossController>();
            pathfinder = GetComponent<BossPathfinder>();
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            RestoreClimb();
            controller?.ClearExternalMotion();
        }

        private void FixedUpdate()
        {
            if (!execute ||
                controller == null ||
                pathfinder == null ||
                controller.IsDead)
            {
                controller?.ClearExternalMotion();
                return;
            }

            if (controller.IsJumping)
            {
                return;
            }

            if (appliedPathVersion != pathfinder.PathVersion)
            {
                appliedPathVersion = pathfinder.PathVersion;
                stepIndex = FindFirstUnreachedStep();
                RestoreClimb();
            }

            IReadOnlyList<BossPathStep> steps =
                pathfinder.CurrentSteps;
            if (stepIndex < 0 || stepIndex >= steps.Count)
            {
                controller.ClearExternalMotion();
                return;
            }

            BossPathStep step = steps[stepIndex];
            ExecuteStep(step);
            if (HasReached(step))
            {
                RestoreClimb();
                stepIndex++;
            }
        }

        public void SetExecutionEnabled(bool value)
        {
            execute = value;
            if (!value)
            {
                RestoreClimb();
                controller.ClearExternalMotion();
            }
        }

        private void ExecuteStep(BossPathStep step)
        {
            BossPathNode target = step.To;
            if (target == null)
            {
                StopHorizontal();
                return;
            }

            switch (step.Kind)
            {
                case BossPathLinkKind.Jump:
                    TickJump(target.transform.position);
                    break;
                case BossPathLinkKind.Climb:
                    TickClimb(target.transform.position);
                    break;
                case BossPathLinkKind.Drop:
                    TickDrop(target.transform.position);
                    break;
                case BossPathLinkKind.Ride:
                    TickRide(target.transform.position);
                    break;
                default:
                    TickRun(target.transform.position);
                    break;
            }
        }

        private void TickRun(Vector3 target)
        {
            TickRun(target, true);
        }

        private void TickRun(
            Vector3 target,
            bool allowAutoJump)
        {
            RestoreClimb();
            float verticalDelta =
                target.y - transform.position.y;
            if (verticalDelta > autoJumpHeight &&
                controller.IsGrounded)
            {
                TickJump(target, false);
                return;
            }

            if (verticalDelta < -autoDropHeight)
            {
                TickDrop(target);
                return;
            }

            if (allowAutoJump &&
                autoJumpOnObstacle &&
                controller.IsGrounded &&
                !IsNearTarget() &&
                HasObstacleAhead(target))
            {
                TickJump(target, false);
                return;
            }

            float direction = target.x - transform.position.x;
            float horizontal = Mathf.Abs(direction) <= arriveDistance
                ? 0f
                : Mathf.Sign(direction);
            controller.SetExternalMove(
                horizontal);
        }

        private void TickClimb(Vector3 target)
        {
            SetClimbing();
            float direction = target.y - transform.position.y;
            float vertical = Mathf.Abs(direction) <= arriveDistance
                ? 0f
                : Mathf.Sign(direction);
            controller.SetExternalClimb(vertical);
        }

        private void TickDrop(Vector3 target)
        {
            RestoreClimb();
            float direction = target.x - transform.position.x;
            float horizontal = Mathf.Abs(direction) <= arriveDistance
                ? 0f
                : Mathf.Sign(direction);
            controller.SetExternalMove(
                horizontal);
        }

        private void TickRide(Vector3 target)
        {
            if (Mathf.Abs(
                    target.y - transform.position.y) > .5f)
            {
                TickClimb(target);
            }
            else
            {
                TickRun(target);
            }
        }

        private void TickJump(Vector3 target)
        {
            TickJump(target, true);
        }

        private void TickJump(
            Vector3 target,
            bool fallbackToRun)
        {
            RestoreClimb();
            if (Time.time < nextJumpRequestTime)
            {
                if (fallbackToRun)
                {
                    TickRun(target, false);
                }

                return;
            }

            if (!controller.IsGrounded)
            {
                if (fallbackToRun)
                {
                    TickRun(target, false);
                }

                return;
            }

            if (controller.RequestJumpTo(target))
            {
                nextJumpRequestTime =
                    Time.time +
                    Mathf.Max(0f, jumpRequestInterval);
                return;
            }

            if (fallbackToRun)
            {
                TickRun(target, false);
            }
        }

        private bool HasObstacleAhead(Vector3 target)
        {
            Collider bodyCollider = controller.BodyCollider;
            if (bodyCollider == null)
            {
                return false;
            }

            float horizontal =
                Mathf.Abs(target.x - transform.position.x);
            if (horizontal <= arriveDistance)
            {
                return false;
            }

            float direction = Mathf.Sign(
                target.x - transform.position.x);
            Vector3 rayDirection = new Vector3(direction, 0f, 0f);
            float distance = Mathf.Max(
                arriveDistance,
                horizontal + obstacleProbePadding);
            Bounds bounds = bodyCollider.bounds;
            Vector3[] origins =
            {
                bounds.center,
                new Vector3(
                    bounds.center.x,
                    bounds.min.y + .08f,
                    bounds.center.z)
            };
            for (int index = 0; index < origins.Length; index++)
            {
                if (Physics.Raycast(
                        origins[index],
                        rayDirection,
                        out RaycastHit hit,
                        distance,
                        ~0,
                        QueryTriggerInteraction.Ignore) &&
                    hit.collider != null &&
                    hit.collider != bodyCollider &&
                    !hit.collider.transform.IsChildOf(transform) &&
                    !IsTargetCollider(hit.collider) &&
                    hit.normal.y < .8f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsNearTarget()
        {
            Transform target = controller.Target;
            if (target == null)
            {
                return false;
            }

            return Mathf.Abs(
                target.position.x - transform.position.x) <=
                noAutoJumpNearTarget;
        }

        private bool IsTargetCollider(Collider collider)
        {
            Transform target = controller.Target;
            return target != null &&
                   (collider.transform == target ||
                    collider.transform.IsChildOf(target) ||
                    collider.GetComponentInParent<PlayerController>() != null);
        }

        private bool HasReached(BossPathStep step)
        {
            if (step.To == null)
            {
                return true;
            }

            Vector3 delta =
                step.To.transform.position - transform.position;
            delta.z = 0f;
            return delta.sqrMagnitude <=
                   arriveDistance * arriveDistance;
        }

        private int FindFirstUnreachedStep()
        {
            IReadOnlyList<BossPathStep> steps =
                pathfinder.CurrentSteps;
            for (int index = 0; index < steps.Count; index++)
            {
                if (!HasReached(steps[index]))
                {
                    return index;
                }
            }

            return steps.Count;
        }

        private void SetClimbing()
        {
            if (climbing)
            {
                return;
            }

            climbing = true;
        }

        private void RestoreClimb()
        {
            if (!climbing)
            {
                return;
            }

            climbing = false;
        }

        private void StopHorizontal()
        {
            if (controller == null ||
                controller.IsJumping)
            {
                return;
            }

            controller.SetExternalMove(
                0f);
        }
    }
}
