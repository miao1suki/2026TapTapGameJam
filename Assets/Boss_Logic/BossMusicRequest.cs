using System.Collections.Generic;
using Project;
using Project.Audio;
using Project.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    public sealed class BossMusicRequest : MonoBehaviour
    {
        [SerializeField]
        private string musicCueId = AudioCueIds.MusicBoss;
        [SerializeField, Range(0f, 1f)]
        private float volumeScale = 1f;

        private BossController boss;
        private MusicRequestHandle request;

        private void Awake()
        {
            boss = GetComponent<BossController>();
        }

        private void OnEnable()
        {
            BindBoss();
            if (boss != null &&
                boss.IsHealthSystemActive &&
                !boss.IsDead)
            {
                TryRequestMusic();
            }
        }

        private void OnDisable()
        {
            UnbindBoss();
            ReleaseMusic();
        }

        private void OnDestroy()
        {
            ReleaseMusic();
        }

        private void BindBoss()
        {
            if (boss == null)
            {
                boss = GetComponent<BossController>();
            }
            if (boss == null)
            {
                return;
            }

            boss.HealthSystemActivated -=
                HandleHealthSystemActivated;
            boss.HealthSystemActivated +=
                HandleHealthSystemActivated;
            boss.Died -= HandleBossDied;
            boss.Died += HandleBossDied;
            boss.RespawnStateApplied -=
                HandleRespawnStateApplied;
            boss.RespawnStateApplied +=
                HandleRespawnStateApplied;
        }

        private void UnbindBoss()
        {
            if (boss == null)
            {
                return;
            }

            boss.HealthSystemActivated -=
                HandleHealthSystemActivated;
            boss.Died -= HandleBossDied;
            boss.RespawnStateApplied -=
                HandleRespawnStateApplied;
        }

        private void HandleHealthSystemActivated()
        {
            TryRequestMusic();
        }

        private void HandleBossDied()
        {
            ReleaseMusic();
        }

        private void HandleRespawnStateApplied(
            float health,
            Vector3 worldPosition)
        {
            if (health > 0f)
            {
                TryRequestMusic();
            }
        }

        private void TryRequestMusic()
        {
            if (request.IsValid ||
                boss == null ||
                boss.IsDead ||
                !boss.IsHealthSystemActive ||
                string.IsNullOrWhiteSpace(musicCueId) ||
                !TryResolveSharedBossScene(out _))
            {
                return;
            }

            request = AudioCuePlayer.RequestMusic(
                musicCueId,
                this,
                volumeScale);
        }

        private bool TryResolveSharedBossScene(
            out Scene scene)
        {
            scene = default;
            if (boss == null)
            {
                return false;
            }

            Scene bossScene = boss.gameObject.scene;
            if (!bossScene.IsValid() ||
                !bossScene.isLoaded)
            {
                return false;
            }

            IReadOnlyList<PlayerController> players =
                ProjectDiscovery.FindAll<PlayerController>(
                    true);
            bool hasPlayer = false;
            for (int index = 0;
                 index < players.Count;
                 index++)
            {
                PlayerController player = players[index];
                if (player != null &&
                    player.isActiveAndEnabled &&
                    player.gameObject.scene == bossScene)
                {
                    hasPlayer = true;
                    break;
                }
            }
            if (!hasPlayer)
            {
                return false;
            }

            IReadOnlyList<BossController> bosses =
                ProjectDiscovery.FindAll<BossController>(
                    true);
            for (int index = 0;
                 index < bosses.Count;
                 index++)
            {
                BossController candidate = bosses[index];
                if (candidate != null &&
                    candidate.isActiveAndEnabled &&
                    !candidate.IsDead &&
                    candidate.gameObject.scene == bossScene)
                {
                    scene = bossScene;
                    return true;
                }
            }

            return false;
        }

        private void ReleaseMusic()
        {
            if (!request.IsValid)
            {
                request = default;
                return;
            }

            request.Release();
            request = default;
        }
    }
}
