using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Project.LevelEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    /// <summary>Explicit, provisional object conversion; layout-only import remains unchanged.</summary>
    internal static class PlanningMVMapObjectImporter
    {
        internal static PlanningDocument Read(string json, LevelEditorPalette palette, out string report,
            IReadOnlyDictionary<string, string> prefabBySourceId = null)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            var source = PlanningMVMapImporter.Read(json);
            var all = source.rooms.SelectMany(r => r.cells).ToList();
            int minX = all.Min(c => c.x), minY = all.Min(c => c.y);
            int width = all.Max(c => c.x) - minX + 1, height = all.Max(c => c.y) - minY + 1;
            // Filling the playable area does not fill its object geometry. Keep conversion bounded.
            if ((long)width * height > 1000000)
                throw new FormatException("地图范围超过物体试导入上限，请先拆分源图。");
            var doc = new PlanningDocument { name = "MVMap 物体试导入", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
            var room = new PlanningRoom("导入测试区域") { id = "mvmap_objects_test_area" };
            for (int y = minY - 2; y < minY + height; y++)
                for (int x = minX; x < minX + width; x++) room.cells.Add(new PlanningCell(x, y));
            doc.rooms.Add(room);
            // Two rows of headroom keep the test spawn and upper surfaces in the same reset region.
            int originY = minY - 2;
            var white = palette.Entries.FirstOrDefault(e => e != null && e.DisplayName == "白方块");
            if (white == null) throw new InvalidOperationException("方块栏目缺少白方块。");
            var notes = new StringBuilder("# MVMap 物体映射报告\n\n");
            notes.AppendLine($"源图：{source.name}；{source.rooms.Count} 个物体区域，{all.Count} 格。\n");
            notes.AppendLine("所有颜色/形状映射均为试导入假设，不是策划定稿。源 ID 和名称保留在物体标签/稳定 ID 中。\n");
            notes.AppendLine("外部房间代表物体，不作为真实房间边界。测试暂用一个整体重置区域；没有推测锁规则、移动路线、水流方向或正式关卡连通。\n");
            notes.AppendLine("| 源 ID | 名称 | 原色 | 格数 | 映射 | 依据 |\n|---|---|---|---:|---|---|");
            foreach (var item in source.rooms)
            {
                string sourceColor = string.IsNullOrEmpty(item.sourceColorHex) ? "未指定" : item.sourceColorHex;
                string prefab = Resolve(item, prefabBySourceId, out string reason);
                LevelEditorPropEntry prop = prefab == null ? null : palette.PropEntries.FirstOrDefault(e => e?.Prefab != null && e.Prefab.name == prefab);
                if (prefab == "DoorButton") prop = PlanningDoorUtility.GetButtonEntry();
                if (prefab != null && prop?.Prefab == null) throw new InvalidOperationException("道具栏目缺少预制体：" + prefab);
                var rects = Partition(item.cells);
                if (prefab == "Green_Ladder")
                    rects = item.cells.OrderBy(c => c.y).ThenBy(c => c.x).Select(c => new RectInt(c.x, c.y, 1, 1)).ToList();
                int part = 0;
                foreach (var rect in rects)
                {
                    var box = new PlanningBox(prop == null ? PlanningDetailType.Solid : PlanningDetailType.Prop,
                        new RectInt(rect.x - minX, rect.y - originY, rect.width, rect.height))
                    {
                        id = "mvmap_" + item.id + "_" + part++,
                        label = item.name + " [" + item.id + "; " + sourceColor + "]",
                        isMerged = rect.width * rect.height > 1 && prefab != "Door",
                        singleInstance = prefab != "Green_Ladder"
                    };
                    if (prop == null) { box.paletteEntryId = white.EntryId; box.paletteEntryName = white.DisplayName; }
                    else
                    {
                        box.propEntryId = prefab == "DoorButton" ? PlanningDoorUtility.ButtonEntryId : prop.EntryId;
                        box.propEntryName = prop.DisplayName;
                    }
                    if (prefab == "Door" && (rect.width != 1 || rect.height != 2))
                        throw new FormatException(item.name + " 的门占格须为1×2。");
                    room.boxes.Add(box);
                }
                notes.AppendLine($"| {item.id} | {item.name} | {sourceColor} | {item.cells.Count} | {prefab ?? "白方块地形"} | {reason} |");
            }
            // Bind guessed buttons explicitly and persist the relationship; never use an instance ID.
            var doors = room.boxes.Where(PlanningDoorUtility.IsDoor).ToList();
            foreach (var button in room.boxes.Where(b => b.propEntryId == PlanningDoorUtility.ButtonEntryId).ToList())
            {
                var door = doors.Where(d => room.boxes.Count(b => b.doorOwnerId == d.id) < 3)
                    .OrderBy(d => (new Vector2(d.x, d.y) - new Vector2(button.x, button.y)).sqrMagnitude).ThenBy(d => d.id).FirstOrDefault();
                if (door == null) throw new FormatException("按钮没有可绑定的门，请提供明确映射。");
                button.doorOwnerId = door.id;
                notes.AppendLine($"\n临时绑定：{button.label} → {door.label}（最近门，待确认）。");
            }
            foreach (var door in doors)
                if (!room.boxes.Any(b => b.doorOwnerId == door.id))
                {
                    var button = PlanningDoorUtility.AddButton(room.boxes, door, new RectInt(0, 0, width, height + 2));
                    if (button == null) throw new FormatException("门附近没有空格生成按钮。");
                    button.id = door.id + "_test_button";
                    notes.AppendLine($"\n辅助按钮：{door.label}，并非来自源图。");
                }
            var first = source.rooms[0].cells.OrderBy(c => c.y).ThenBy(c => c.x).First();
            doc.playerStartRoomId = room.id;
            doc.playerStartLocal = new Vector2(first.x - minX + 1.5f, first.y - originY - .5f);
            doc.Normalize();
            report = notes.ToString();
            return doc;
        }

        private static string Resolve(PlanningRoom room, IReadOnlyDictionary<string, string> overrides, out string reason)
        {
            if (overrides != null && overrides.TryGetValue(room.id, out var chosen)) { reason = "指定 ID 映射"; return chosen; }
            string name = room.name ?? "";
            reason = "明确名称";
            if (name.Contains("重生")) return "Respawn_Block";
            if (name.Contains("按钮")) return "DoorButton";
            if (name.Contains("门")) return "Door";
            if (name.Contains("藤蔓")) return "Green_Ladder";
            if (name.Contains("植物障碍")) return "Green_PlantObstacle";
            if (name.Contains("弹跳")) return "Green_JumpPlant";
            if (name.Contains("岩浆")) return "Red_Lava";
            if (name.Contains("水")) return "Blue_WaterSource";
            var bounds = Bounds(room.cells);
            reason = "临时颜色/形状推测";
            switch ((room.sourceColorHex ?? "").ToLowerInvariant())
            {
                case "#4c8dd8": case "#4cc9d8": return "Blue_WaterSource";
                case "#3fb27f": return bounds.height > 1 ? "Green_Ladder" : "Green_JumpPlant";
                case "#d8734c": return "Red_ContactCube";
                case "#d84c7a": return "Red_Lava";
                case "#d8b64c": return bounds.width == 1 && bounds.height == 2 ? "Door" : "DoorButton";
                case "#a56fd0": reason = "紫色长条/边界按墙体暂存，不猜平台路线"; return null;
                default: reason = "灰色/未指定按地面暂存"; return null;
            }
        }

        private static RectInt Bounds(List<PlanningCell> cells)
        {
            int x = cells.Min(c => c.x), y = cells.Min(c => c.y);
            return new RectInt(x, y, cells.Max(c => c.x) - x + 1, cells.Max(c => c.y) - y + 1);
        }

        // Exact rectangles: preserve holes, L shapes and disconnected islands instead of filling their bounds.
        internal static List<RectInt> Partition(List<PlanningCell> cells)
        {
            var remaining = new HashSet<Vector2Int>(cells.Select(c => new Vector2Int(c.x, c.y)));
            var result = new List<RectInt>();
            while (remaining.Count > 0)
            {
                var start = remaining.OrderBy(c => c.y).ThenBy(c => c.x).First();
                int width = 1, height = 1;
                while (remaining.Contains(start + new Vector2Int(width, 0))) width++;
                while (Enumerable.Range(0, width).All(x => remaining.Contains(start + new Vector2Int(x, height)))) height++;
                var rect = new RectInt(start.x, start.y, width, height);
                foreach (var cell in rect.allPositionsWithin) remaining.Remove(cell);
                result.Add(rect);
            }
            return result;
        }
    }
}
