using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.GameFlow
{
    [CreateAssetMenu(
        fileName = "GameSceneCatalog",
        menuName = "2026TapTap/Game Flow/Scene Catalog")]
    public sealed class GameSceneCatalog : ScriptableObject
    {
        [SerializeField] private string bootstrapScenePath;
        [SerializeField] private string[] persistentScenePaths = Array.Empty<string>();
        [SerializeField] private GameFlowSceneEntry[] flowScenes =
            Array.Empty<GameFlowSceneEntry>();

        public string BootstrapScenePath => bootstrapScenePath;
        public IReadOnlyList<string> PersistentScenePaths => persistentScenePaths;
        public IReadOnlyList<GameFlowSceneEntry> FlowScenes => flowScenes;

        public bool TryGet(
            GameFlowSceneId id,
            out GameFlowSceneEntry entry)
        {
            for (int index = 0; index < flowScenes.Length; index++)
            {
                if (flowScenes[index] != null && flowScenes[index].Id == id)
                {
                    entry = flowScenes[index];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public bool TryGetIdForPath(
            string scenePath,
            out GameFlowSceneId id)
        {
            string normalized = Normalize(scenePath);
            for (int index = 0; index < flowScenes.Length; index++)
            {
                GameFlowSceneEntry entry = flowScenes[index];
                if (entry != null && Normalize(entry.ScenePath) == normalized)
                {
                    id = entry.Id;
                    return true;
                }
            }

            id = default;
            return false;
        }

        public bool IsPersistentScene(string scenePath)
        {
            string normalized = Normalize(scenePath);
            for (int index = 0; index < persistentScenePaths.Length; index++)
            {
                if (Normalize(persistentScenePaths[index]) == normalized)
                {
                    return true;
                }
            }

            return false;
        }

        public void Configure(
            string valueBootstrapScenePath,
            IEnumerable<string> valuePersistentScenePaths,
            IEnumerable<GameFlowSceneEntry> valueFlowScenes)
        {
            bootstrapScenePath = Normalize(valueBootstrapScenePath);
            persistentScenePaths = valuePersistentScenePaths != null
                ? new List<string>(valuePersistentScenePaths).ConvertAll(Normalize).ToArray()
                : Array.Empty<string>();
            flowScenes = valueFlowScenes != null
                ? new List<GameFlowSceneEntry>(valueFlowScenes).ToArray()
                : Array.Empty<GameFlowSceneEntry>();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('\\', '/');
        }
    }
}
