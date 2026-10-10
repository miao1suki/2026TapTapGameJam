using System.Collections.Generic;
using UnityEngine;

namespace PlanningEditorPrototype
{
    internal static class PlanningLayoutUtility
    {
        internal static void GetStride(
            IReadOnlyList<PlanningRoom> rooms,
            int worldBlockCellWidth,
            int worldBlockCellHeight,
            out int strideX,
            out int strideY)
        {
            // One world cell always maps to one fixed-size detail block.
            // Contents must not change the position of other rooms.
            strideX = Mathf.Max(1, worldBlockCellWidth);
            strideY = Mathf.Max(1, worldBlockCellHeight);
        }

        internal static RoomLayoutInfo GetRoomLayout(
            PlanningRoom room,
            int strideX,
            int strideY)
        {
            int minCellX = int.MaxValue;
            int minCellY = int.MaxValue;
            for (int index = 0; index < room.cells.Count; index++)
            {
                PlanningCell cell = room.cells[index];
                minCellX = Mathf.Min(minCellX, cell.x);
                minCellY = Mathf.Min(minCellY, cell.y);
            }

            if (minCellX == int.MaxValue)
            {
                minCellX = 0;
                minCellY = 0;
            }

            int maxPlanY = 1;
            for (int index = 0; index < room.boxes.Count; index++)
            {
                PlanningBox box = room.boxes[index];
                maxPlanY = Mathf.Max(
                    maxPlanY,
                    box.y + box.height);
            }

            return new RoomLayoutInfo(
                room,
                minCellX * strideX,
                minCellY * strideY,
                maxPlanY);
        }

        internal static RectInt GetAssemblyBoxRect(
            PlanningRoom owner,
            PlanningBox box,
            int strideX,
            int strideY)
        {
            if (owner.isConnector)
            {
                GetConnectorOrigin(
                    owner,
                    strideX,
                    strideY,
                    out int originX,
                    out int originY);
                return new RectInt(
                    originX + box.x,
                    originY + box.y,
                    box.width,
                    box.height);
            }

            RoomLayoutInfo layout = GetRoomLayout(
                owner,
                strideX,
                strideY);
            return new RectInt(
                layout.OffsetX + box.x,
                layout.OffsetY + box.y,
                box.width,
                box.height);
        }

        internal static void GetConnectorOrigin(
            PlanningRoom connector,
            int strideX,
            int strideY,
            out int originX,
            out int originY)
        {
            PlanningCell origin = GetConnectorLocalOrigin(connector);
            originX = origin.x * strideX;
            originY = origin.y * strideY;
        }

        internal static PlanningCell GetConnectorLocalOrigin(
            PlanningRoom connector)
        {
            if (connector != null && connector.independentCells)
            {
                RectInt bounds = PlanningWorldUtility.Bounds(connector);
                return new PlanningCell(bounds.x, bounds.y);
            }
            return connector != null &&
                   connector.cells.Count > 0
                ? connector.cells[0]
                : new PlanningCell(0, 0);
        }

        internal static List<Vector2Int> GetConnectorAssemblyPath(
            IReadOnlyList<PlanningRoom> rooms,
            PlanningRoom connector,
            int worldBlockCellWidth,
            int worldBlockCellHeight)
        {
            var path = new List<Vector2Int>();
            if (connector.independentCells)
            {
                GetConnectorOrigin(connector, worldBlockCellWidth, worldBlockCellHeight, out int x, out int y);
                // Compatibility anchor for scene generation; no implicit floor is generated.
                if (connector.cells.Count > 0) path.Add(new Vector2Int(x, y));
                return path;
            }
            PlanningRoom from = FindRoom(rooms, connector.fromRoomId);
            PlanningRoom to = FindRoom(rooms, connector.toRoomId);
            if (from == null || to == null)
            {
                return path;
            }

            GetStride(
                rooms,
                worldBlockCellWidth,
                worldBlockCellHeight,
                out int strideX,
                out int strideY);
            RoomLayoutInfo fromLayout = GetRoomLayout(
                from,
                strideX,
                strideY);
            RoomLayoutInfo toLayout = GetRoomLayout(
                to,
                strideX,
                strideY);
            RectInt fromAllowed = from.GetLocalAllowedRect(
                worldBlockCellWidth,
                worldBlockCellHeight);
            RectInt toAllowed = to.GetLocalAllowedRect(
                worldBlockCellWidth,
                worldBlockCellHeight);
            Vector2Int start = GetAssemblyPort(
                fromLayout,
                fromAllowed,
                connector.fromSide,
                connector.fromOffset);
            Vector2Int end = GetAssemblyPort(
                toLayout,
                toAllowed,
                connector.toSide,
                connector.toOffset);
            AppendPath(path, start, end);
            return path;
        }

        private static Vector2Int GetAssemblyPort(
            RoomLayoutInfo layout,
            RectInt allowed,
            PlanningPortSide side,
            float offset)
        {
            float safeOffset = Mathf.Clamp01(offset);
            switch (side)
            {
                case PlanningPortSide.Left:
                    return new Vector2Int(
                        layout.OffsetX - 1,
                        layout.OffsetY + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, allowed.height - 1)),
                            0,
                            Mathf.Max(0, allowed.height - 1)));
                case PlanningPortSide.Right:
                    return new Vector2Int(
                        layout.OffsetX + allowed.width,
                        layout.OffsetY + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, allowed.height - 1)),
                            0,
                            Mathf.Max(0, allowed.height - 1)));
                case PlanningPortSide.Top:
                    return new Vector2Int(
                        layout.OffsetX + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, allowed.width - 1)),
                            0,
                            Mathf.Max(0, allowed.width - 1)),
                        layout.OffsetY - 1);
                default:
                    return new Vector2Int(
                        layout.OffsetX + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, allowed.width - 1)),
                            0,
                            Mathf.Max(0, allowed.width - 1)),
                        layout.OffsetY + allowed.height);
            }
        }

        private static PlanningRoom FindRoom(
            IReadOnlyList<PlanningRoom> rooms,
            string roomId)
        {
            for (int index = 0; index < rooms.Count; index++)
            {
                if (rooms[index].id == roomId)
                {
                    return rooms[index];
                }
            }

            return null;
        }

        private static void AppendPath(
            List<Vector2Int> path,
            Vector2Int start,
            Vector2Int end)
        {
            int x = start.x;
            int y = start.y;
            path.Add(new Vector2Int(x, y));
            while (x != end.x)
            {
                x += x < end.x ? 1 : -1;
                path.Add(new Vector2Int(x, y));
            }

            while (y != end.y)
            {
                y += y < end.y ? 1 : -1;
                path.Add(new Vector2Int(x, y));
            }
        }

        internal sealed class RoomLayoutInfo
        {
            internal readonly PlanningRoom Room;
            internal readonly int OffsetX;
            internal readonly int OffsetY;
            internal readonly int MaxPlanY;

            internal RoomLayoutInfo(
                PlanningRoom room,
                int offsetX,
                int offsetY,
                int maxPlanY)
            {
                Room = room;
                OffsetX = offsetX;
                OffsetY = offsetY;
                MaxPlanY = maxPlanY;
            }
        }
    }
}
