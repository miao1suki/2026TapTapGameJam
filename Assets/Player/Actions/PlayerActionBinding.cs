using System;
using Project.InputAbstraction;

namespace Project.Player
{
    [Serializable]
    public struct PlayerActionBinding
    {
        public InputActionId inputAction;
        public ActSO action;
    }
}
