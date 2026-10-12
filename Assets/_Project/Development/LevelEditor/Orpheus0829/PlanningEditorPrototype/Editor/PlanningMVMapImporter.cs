using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PlanningEditorPrototype
{
    /// <summary>MVMap v1 world layout. Import never invents gameplay from planning markers.</summary>
    internal static class PlanningMVMapImporter
    {
        [Serializable] private sealed class Source
        {
            public int version;
            public string name;
            public SourceRoom[] rooms;
            public SourceArea[] areas;
            public Marker[] doors, keys, locks;
        }
        [Serializable] private sealed class Marker { public string id; }
        [Serializable] private sealed class SourceArea { public string id, name, color; }
        [Serializable] private sealed class SourceRoom { public string id, name, areaId, color, notes; public string[] cells; }

        internal static bool IsMVMap(string json) => json != null &&
            json.Contains("\"version\"") && json.Contains("\"areaId\"") && json.Contains("\"cells\"");

        internal static PlanningDocument Read(string json)
        {
            Source source = JsonUtility.FromJson<Source>(json);
            if (source == null || source.version != 1 || source.rooms == null)
                throw new FormatException("需要 MVMap v1 格式的地图文件。");
            if ((source.doors?.Length ?? 0) + (source.keys?.Length ?? 0) + (source.locks?.Length ?? 0) > 0)
                throw new FormatException("此版本仅导入区域布局；文件中的门、钥匙与锁规则尚不兼容，请先导出仅含布局的副本。");
            var doc = new PlanningDocument { name = source.name, worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
            var ids = new HashSet<string>();
            var ownership = new HashSet<Vector2Int>();
            foreach (var item in source.rooms)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id))
                    throw new FormatException("MVMap 的房间 ID 缺失或重复。");
                var room = new PlanningRoom(item.name)
                {
                    id = item.id, sourceColorHex = item.color, sourceAreaId = item.areaId, sourceNotes = item.notes,
                    allowDisconnectedCells = true
                };
                var unique = new HashSet<Vector2Int>();
                foreach (string text in item.cells ?? Array.Empty<string>())
                {
                    string[] parts = text.Split(',');
                    if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                        !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) || Math.Abs((long)x) > 100000 || Math.Abs((long)y) > 100000)
                        throw new FormatException($"无效的地图格坐标：{text}");
                    var cell = new Vector2Int(x, y);
                    if (!unique.Add(cell)) continue;
                    if (!ownership.Add(cell)) throw new FormatException($"不同房间的区域重叠：{text}");
                    room.cells.Add(new PlanningCell(x, y));
                }
                if (room.cells.Count > 0) doc.rooms.Add(room);
            }
            if (doc.rooms.Count == 0) throw new FormatException("地图没有房间格。");
            // Disconnected islands stay under the same external ID; no geometry is discarded.
            doc.Normalize();
            return doc;
        }
    }
}
