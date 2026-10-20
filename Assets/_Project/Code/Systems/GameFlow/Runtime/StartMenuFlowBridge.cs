using System;
using System.Collections;
using Project.StartMenu;
using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StartMenuController))]
    public sealed class StartMenuFlowBridge : MonoBehaviour
    {
        public static event Func<IEnumerator>
            BeforeStartTransition;

        private StartMenuController menu;
        private Coroutine startTransitionRoutine;

        private void Awake()
        {
            menu = GetComponent<StartMenuController>();
        }

        private void OnEnable()
        {
            if (menu != null)
            {
                menu.StartGameRequested += OnStartGameRequested;
                menu.ExitGameRequested += OnExitGameRequested;
            }
        }

        private void OnDisable()
        {
            if (menu != null)
            {
                menu.StartGameRequested -= OnStartGameRequested;
                menu.ExitGameRequested -= OnExitGameRequested;
            }
        }

        private void OnStartGameRequested()
        {
            if (startTransitionRoutine != null)
            {
                return;
            }

            startTransitionRoutine =
                StartCoroutine(
                    StartTransitionWhenReady());
        }

        private IEnumerator StartTransitionWhenReady()
        {
            Delegate[] preparation =
                BeforeStartTransition?
                    .GetInvocationList();
            if (preparation != null)
            {
                for (int index = 0;
                     index < preparation.Length;
                     index++)
                {
                    IEnumerator routine = null;
                    try
                    {
                        routine =
                            preparation[index]
                                .DynamicInvoke() as
                                IEnumerator;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }

                    if (routine != null)
                    {
                        yield return routine;
                    }
                }
            }

            GameFlowController flow =
                GameFlowController.Instance;
            if (flow != null)
            {
                flow.RequestTransition(
                    GameFlowSceneId.Gaming);
            }
            startTransitionRoutine = null;
        }

        private void OnExitGameRequested()
        {
            GameSceneLoader.Instance.RequestApplicationQuit();
        }
    }
}
