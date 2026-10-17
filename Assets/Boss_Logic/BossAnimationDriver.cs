using System;
using UnityEngine;
using UnityEngine.Events;

namespace Project.BossLogic
{
    [Serializable]
    public sealed class BossStateEvent : UnityEvent<BossStateId>
    {
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController))]
    public sealed class BossAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private bool driveAnimator = true;
        [SerializeField] private string chaseBoolParameter = "Chasing";
        [SerializeField] private string hitTriggerParameter = "Hit";
        [SerializeField] private string deathTriggerParameter = "Death";
        [SerializeField] private string jumpTriggerParameter = "Jump";
        [SerializeField] private BossStateEvent stateChanged =
            new BossStateEvent();
        [SerializeField] private UnityEvent hitStarted =
            new UnityEvent();
        [SerializeField] private UnityEvent deathStarted =
            new UnityEvent();
        [SerializeField] private UnityEvent jumpStarted =
            new UnityEvent();

        private BossController controller;

        public event Action<BossStateId, BossStateId> StateForwarded;

        private void OnEnable()
        {
            controller = GetComponent<BossController>();
            if (controller != null)
            {
                controller.StateChanged += OnStateChanged;
                ApplyState(controller.State);
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.StateChanged -= OnStateChanged;
            }
        }

        public void SetAnimator(Animator value)
        {
            animator = value;
            if (isActiveAndEnabled && controller != null)
            {
                ApplyState(controller.State);
            }
        }

        private void OnStateChanged(
            BossStateId previous,
            BossStateId next)
        {
            ApplyState(next);
            StateForwarded?.Invoke(previous, next);
            stateChanged?.Invoke(next);
            if (next == BossStateId.Hit)
            {
                hitStarted?.Invoke();
            }
            else if (next == BossStateId.Death)
            {
                deathStarted?.Invoke();
            }
            else if (next == BossStateId.Jump)
            {
                jumpStarted?.Invoke();
            }
        }

        private void ApplyState(BossStateId state)
        {
            if (!driveAnimator || animator == null)
            {
                return;
            }

            SetBool(chaseBoolParameter, state == BossStateId.Chase);
            if (state == BossStateId.Hit)
            {
                SetTrigger(hitTriggerParameter);
            }
            else if (state == BossStateId.Death)
            {
                SetTrigger(deathTriggerParameter);
            }
            else if (state == BossStateId.Jump)
            {
                SetTrigger(jumpTriggerParameter);
            }
        }

        private void SetBool(string parameter, bool value)
        {
            if (!string.IsNullOrWhiteSpace(parameter) &&
                HasParameter(parameter, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(parameter, value);
            }
        }

        private void SetTrigger(string parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter) &&
                HasParameter(parameter, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(parameter);
            }
        }

        private bool HasParameter(
            string parameter,
            AnimatorControllerParameterType type)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters =
                animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter candidate = parameters[index];
                if (candidate.type == type &&
                    string.Equals(
                        candidate.name,
                        parameter,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
