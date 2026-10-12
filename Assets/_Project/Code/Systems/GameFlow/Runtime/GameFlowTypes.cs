using System;
using UnityEngine;

namespace Project.GameFlow
{
    public enum GameFlowSceneId
    {
        MainMenu = 0,
        Level01 = 10,
        Level02 = 20,
        Level03 = 30,
        Ending = 100
    }

    public enum GameSystemSceneKind
    {
        Core = 0,
        Input = 10,
        Audio = 20,
        Camera = 30,
        UI = 40
    }

    [Serializable]
    public sealed class GameFlowSceneEntry
    {
        [SerializeField] private GameFlowSceneId id;
        [SerializeField] private string scenePath;
        [SerializeField] private bool gameplay;

        public GameFlowSceneId Id => id;
        public string ScenePath => scenePath;
        public bool IsGameplay => gameplay;

        public GameFlowSceneEntry(
            GameFlowSceneId valueId,
            string valueScenePath,
            bool valueGameplay)
        {
            id = valueId;
            scenePath = valueScenePath;
            gameplay = valueGameplay;
        }
    }

}
