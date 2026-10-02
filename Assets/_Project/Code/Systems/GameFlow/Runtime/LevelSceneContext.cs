using Project.CameraModes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelSceneContext : MonoBehaviour
    {
        [SerializeField] private GameFlowSceneId sceneId = GameFlowSceneId.Level01;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private GameObject playerPrefab;

        private GameObject spawnedPlayer;

        public GameFlowSceneId SceneId => sceneId;
        public Transform PlayerSpawn => playerSpawn;
        public GameObject PlayerPrefab => playerPrefab;
        public GameObject SpawnedPlayer => spawnedPlayer;

        public void Configure(
            GameFlowSceneId valueSceneId,
            Transform valuePlayerSpawn,
            GameObject valuePlayerPrefab = null)
        {
            sceneId = valueSceneId;
            playerSpawn = valuePlayerSpawn;
            if (valuePlayerPrefab != null)
            {
                playerPrefab = valuePlayerPrefab;
            }
        }

        public void SetPlayerSpawn(Transform value)
        {
            playerSpawn = value;
        }

        public void SetPlayerPrefab(GameObject value)
        {
            playerPrefab = value;
        }

        public void Activate()
        {
            if (spawnedPlayer == null)
            {
                spawnedPlayer = FindPlayerInOwnScene();
            }

            if (spawnedPlayer == null && playerPrefab != null)
            {
                Vector3 position = playerSpawn != null
                    ? playerSpawn.position
                    : transform.position;
                Quaternion rotation = playerSpawn != null
                    ? playerSpawn.rotation
                    : Quaternion.identity;
                spawnedPlayer = Instantiate(playerPrefab, position, rotation);
                spawnedPlayer.name = playerPrefab.name;
                SceneManager.MoveGameObjectToScene(spawnedPlayer, gameObject.scene);
            }

            if (spawnedPlayer == null)
            {
                Debug.LogError(
                    $"关卡 {sceneId} 没有玩家，也没有配置玩家 Prefab。",
                    this);
                return;
            }

            CameraFollowController follow =
                FindFirstObjectByType<CameraFollowController>();
            if (follow != null)
            {
                follow.SetTarget(spawnedPlayer.transform, true);
                follow.SetRequestedMode(CameraViewMode.Side2D, 0f, true);
            }

            CameraModeController mode =
                FindFirstObjectByType<CameraModeController>();
            mode?.SetFollowTarget(spawnedPlayer.transform, true);
        }

        private GameObject FindPlayerInOwnScene()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            for (int index = 0; index < players.Length; index++)
            {
                if (players[index].scene == gameObject.scene)
                {
                    return players[index];
                }
            }

            return null;
        }
    }
}
