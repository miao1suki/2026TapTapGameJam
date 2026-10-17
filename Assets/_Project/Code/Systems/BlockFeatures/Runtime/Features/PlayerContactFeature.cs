using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>
    /// Shared trigger relay for features that react to the player.
    /// </summary>
    public abstract class PlayerContactFeature : BlockFeature,
        IPlayerContactReceiver
    {
        [BlockParameter(
            Label = "玩家标签",
            Group = "接触设置",
            Order = 100,
            Tooltip = "只有带此 Unity Tag 的对象会被当作玩家。")]
        [SerializeField] protected string playerTag = "Player";

        private readonly Dictionary<int, PlayerController> contacts =
            new Dictionary<int, PlayerController>();
        private readonly Dictionary<int, int> triggerCounts = new Dictionary<int, int>();
        private Project.LevelEditor.LevelEditorMergedShape mergedShape;

        protected int PlayerCount => contacts.Count;
        protected IEnumerable<PlayerController> ActivePlayers =>
            contacts.Values;

        protected override void OnAttach()
        {
            contacts.Clear();
            triggerCounts.Clear();
            mergedShape = GetComponent<Project.LevelEditor.LevelEditorMergedShape>();
        }

        protected override void OnDetach()
        {
            DisconnectAllPlayers();
        }

        protected void DisconnectAllPlayers()
        {
            var players = new List<PlayerController>(contacts.Values);
            contacts.Clear();
            triggerCounts.Clear();
            for (int index = 0; index < players.Count; index++)
            {
                if (players[index] != null)
                {
                    OnPlayerExited(players[index]);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = ResolvePlayer(other != null ? other.gameObject : null);
            if (player != null)
            {
                int id = player.GetInstanceID(); triggerCounts.TryGetValue(id, out int count); triggerCounts[id] = count + 1;
            }
            OnPlayerEnter(other != null ? other.gameObject : null);
        }

        private void OnTriggerExit(Collider other)
        {
            var player = ResolvePlayer(other != null ? other.gameObject : null);
            if (player != null && mergedShape != null)
            {
                // A room reset can reattach a feature while the player is already inside.
                // At an exit, resolve actual overlap rather than trusting pre-reset enter counts.
                triggerCounts.Remove(player.GetInstanceID());
                foreach (var volume in mergedShape.Volumes)
                    if (volume != null && volume.enabled && other != null &&
                        Physics.ComputePenetration(volume, volume.transform.position, volume.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out _, out _)) return;
            }
            else if (player != null && triggerCounts.TryGetValue(player.GetInstanceID(), out int count))
            {
                if (count > 1) { triggerCounts[player.GetInstanceID()] = count - 1; return; }
                triggerCounts.Remove(player.GetInstanceID());
            }
            OnPlayerExit(other != null ? other.gameObject : null);
        }

        private void OnTriggerStay(Collider other)
        {
            OnPlayerEnter(other != null ? other.gameObject : null);
            OnPlayerStay(other != null ? other.gameObject : null);
        }

        public void OnPlayerEnter(GameObject actor)
        {
            if (!CanRunFeature)
            {
                return;
            }

            PlayerController player = ResolvePlayer(actor);
            if (player == null)
            {
                return;
            }

            int id = player.GetInstanceID();
            if (contacts.ContainsKey(id))
            {
                return;
            }

            contacts.Add(id, player);
            OnPlayerEntered(player);
        }

        public void OnPlayerExit(GameObject actor)
        {
            PlayerController player = ResolvePlayer(actor);
            if (player == null)
            {
                return;
            }

            int id = player.GetInstanceID();
            if (!contacts.Remove(id))
            {
                return;
            }

            OnPlayerExited(player);
        }

        public void OnPlayerStay(GameObject actor)
        {
            if (!CanRunFeature)
            {
                return;
            }

            PlayerController player = ResolvePlayer(actor);
            if (player == null ||
                !contacts.ContainsKey(player.GetInstanceID()))
            {
                return;
            }

            OnPlayerStayed(player);
        }

        protected virtual void OnPlayerEntered(PlayerController player)
        {
        }

        protected virtual void OnPlayerExited(PlayerController player)
        {
        }

        protected virtual void OnPlayerStayed(PlayerController player)
        {
        }

        private PlayerController ResolvePlayer(GameObject actor)
        {
            PlayerController player = actor != null
                ? actor.GetComponentInParent<PlayerController>()
                : null;
            return player != null &&
                   player.gameObject.CompareTag(playerTag)
                ? player
                : null;
        }
    }
}
