using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameFlowSceneRoot : MonoBehaviour
    {
        [SerializeField] private GameFlowSceneId sceneId;

        public GameFlowSceneId SceneId => sceneId;

        public void Configure(GameFlowSceneId valueSceneId)
        {
            sceneId = valueSceneId;
        }
    }
}
