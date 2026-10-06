using Project.StartMenu;
using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StartMenuController))]
    public sealed class StartMenuFlowBridge : MonoBehaviour
    {
        private StartMenuController menu;

        private void Awake()
        {
            menu = GetComponent<StartMenuController>();
        }

        private void OnEnable()
        {
            if (menu != null)
            {
                menu.StartGameRequested += OnStartGameRequested;
            }
        }

        private void OnDisable()
        {
            if (menu != null)
            {
                menu.StartGameRequested -= OnStartGameRequested;
            }
        }

        private void OnStartGameRequested()
        {
            GameFlowController flow = GameFlowController.Instance;
            if (flow == null)
            {
                return;
            }

            flow.RequestTransition(GameFlowSceneId.Level01);
        }
    }
}
