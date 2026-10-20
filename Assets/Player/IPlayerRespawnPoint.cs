using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Player
{
    public interface IPlayerRespawnPoint
    {
        bool IsRespawnPointAvailable { get; }
        Vector3 RespawnPosition { get; }
        int RespawnPriority { get; }
        float RespawnRadiusBlocks { get; }
    }

    public static class PlayerRespawnPointRegistry
    {
        private static readonly List<IPlayerRespawnPoint> Points =
            new List<IPlayerRespawnPoint>();
        private static readonly Dictionary<PlayerController, IPlayerRespawnPoint> Bound =
            new Dictionary<PlayerController, IPlayerRespawnPoint>();
        public static event Action<
            PlayerController,
            IPlayerRespawnPoint,
            bool> BindingChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Points.Clear();
            Bound.Clear();
            BindingChanged = null;
        }

        public static void Bind(PlayerController player, IPlayerRespawnPoint point)
        {
            if (player == null || point == null || !point.IsRespawnPointAvailable) return;
            Bound.TryGetValue(player, out IPlayerRespawnPoint previous);
            if (ReferenceEquals(previous, point)) return;
            Bound[player] = point;
            NotifyBinding(previous, player, false);
            NotifyBinding(point, player, true);
            BindingChanged?.Invoke(
                player,
                previous,
                false);
            BindingChanged?.Invoke(
                player,
                point,
                true);
        }

        public static bool TryGetBound(
            PlayerController player,
            out IPlayerRespawnPoint point)
        {
            point = null;
            return player != null &&
                   Bound.TryGetValue(player, out point) &&
                   point != null &&
                   (!(point is UnityEngine.Object unityObject) ||
                    unityObject != null) &&
                   point.IsRespawnPointAvailable;
        }

        public static void ClearBinding(
            PlayerController player)
        {
            if (player == null ||
                !Bound.TryGetValue(
                    player,
                    out IPlayerRespawnPoint point))
            {
                return;
            }

            Bound.Remove(player);
            NotifyBinding(point, player, false);
            BindingChanged?.Invoke(
                player,
                point,
                false);
        }

        private static void NotifyBinding(IPlayerRespawnPoint point, PlayerController player, bool current)
        {
            if (point is UnityEngine.Object obj && obj == null) return;
            if (point is IPlayerRespawnBindingListener listener) listener.OnRespawnBindingChanged(player, current);
        }

        public static bool TryFindPreferred(PlayerController player, Vector3 origin, out IPlayerRespawnPoint point)
        {
            if (player != null && Bound.TryGetValue(player, out point) &&
                (!(point is UnityEngine.Object obj) || obj != null) && point.IsRespawnPointAvailable) return true;
            return TryFindPreferred(origin, out point);
        }

        public static void Register(IPlayerRespawnPoint point)
        {
            if (point != null && !Points.Contains(point))
            {
                Points.Add(point);
            }
        }

        public static void Unregister(IPlayerRespawnPoint point)
        {
            if (point != null)
            {
                Points.Remove(point);
                var players = new List<PlayerController>();
                foreach (var pair in Bound)
                    if (ReferenceEquals(pair.Value, point)) players.Add(pair.Key);
                foreach (var player in players)
                {
                    Bound.Remove(player);
                    NotifyBinding(point, player, false);
                    BindingChanged?.Invoke(
                        player,
                        point,
                        false);
                }
            }
        }

        public static bool TryFindNearest(
            Vector3 origin,
            out Vector3 position)
        {
            position = default;
            bool found = false;
            float nearest = float.MaxValue;
            for (int index = Points.Count - 1;
                 index >= 0;
                 index--)
            {
                IPlayerRespawnPoint point = Points[index];
                if (point == null)
                {
                    Points.RemoveAt(index);
                    continue;
                }

                if (!point.IsRespawnPointAvailable)
                {
                    continue;
                }

                float distance =
                    (point.RespawnPosition - origin).sqrMagnitude;
                if (distance >= nearest)
                {
                    continue;
                }

                nearest = distance;
                position = point.RespawnPosition;
                found = true;
            }

            return found;
        }

        public static bool TryFindHighestPriority(
            out IPlayerRespawnPoint point)
        {
            point = null;
            int highestPriority = int.MinValue;
            int bestInstanceId = int.MinValue;
            for (int index = Points.Count - 1;
                 index >= 0;
                 index--)
            {
                IPlayerRespawnPoint candidate = Points[index];
                if (candidate == null)
                {
                    Points.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsRespawnPointAvailable)
                {
                    continue;
                }

                int priority = candidate.RespawnPriority;
                if (priority < 0)
                {
                    continue;
                }

                int instanceId = candidate is UnityEngine.Object unityObject
                    ? unityObject.GetInstanceID()
                    : 0;
                if (priority < highestPriority ||
                    priority == highestPriority &&
                    instanceId <= bestInstanceId)
                {
                    continue;
                }

                highestPriority = priority;
                bestInstanceId = instanceId;
                point = candidate;
            }

            return point != null;
        }

        public static bool TryFindPreferred(
            Vector3 origin,
            out IPlayerRespawnPoint point)
        {
            if (TryFindHighestPriority(out point))
            {
                return true;
            }

            bool hasOrderedPoint = false;
            for (int index = 0; index < Points.Count; index++)
            {
                IPlayerRespawnPoint candidate = Points[index];
                if (candidate != null &&
                    candidate.RespawnPriority >= 0)
                {
                    hasOrderedPoint = true;
                    break;
                }
            }

            if (hasOrderedPoint)
            {
                point = null;
                return false;
            }

            return TryFindNearestAvailable(origin, out point);
        }

        public static bool TryFindNearestAvailable(
            Vector3 origin,
            out IPlayerRespawnPoint point)
        {
            point = null;
            float nearest = float.MaxValue;
            for (int index = Points.Count - 1;
                 index >= 0;
                 index--)
            {
                IPlayerRespawnPoint candidate = Points[index];
                if (candidate == null)
                {
                    Points.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsRespawnPointAvailable)
                {
                    continue;
                }

                float distance =
                    (candidate.RespawnPosition - origin).sqrMagnitude;
                if (distance >= nearest)
                {
                    continue;
                }

                nearest = distance;
                point = candidate;
            }

            return point != null;
        }
    }

    public interface IPlayerRespawnBindingListener
    {
        void OnRespawnBindingChanged(PlayerController player, bool current);
    }

    public interface IPlayerRespawnPreRoomResetListener
    {
        void OnPlayerRespawnBeforeRoomReset(
            PlayerController player);
    }

    public interface IPlayerRespawnPostRoomResetListener
    {
        void OnPlayerRespawnAfterRoomReset(
            PlayerController player);
    }
}
