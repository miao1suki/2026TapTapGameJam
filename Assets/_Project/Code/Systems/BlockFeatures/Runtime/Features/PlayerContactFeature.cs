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

        protected int PlayerCount => contacts.Count;
        protected IEnumerable<PlayerController> ActivePlayers =>
            contacts.Values;

        protected override void OnAttach()
        {
            contacts.Clear();
        }

        protected override void OnDetach()
        {
            DisconnectAllPlayers();
        }

        protected void DisconnectAllPlayers()
        {
            var players = new List<PlayerController>(contacts.Values);
            contacts.Clear();
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
            OnPlayerEnter(other != null ? other.gameObject : null);
        }

        private void OnTriggerExit(Collider other)
        {
            OnPlayerExit(other != null ? other.gameObject : null);
        }

        private void OnTriggerStay(Collider other)
        {
            OnPlayerStay(other != null ? other.gameObject : null);
        }

        public void OnPlayerEnter(GameObject actor)
        {
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
