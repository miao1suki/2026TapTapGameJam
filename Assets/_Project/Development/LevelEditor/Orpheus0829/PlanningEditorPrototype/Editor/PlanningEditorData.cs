using System;
using System.Collections.Generic;
using Project.LevelEditor;
using UnityEditor;
using UnityEngine;

namespace PlanningEditorPrototype
{
    public enum PlanningMapTool
    {
        Select,
        Paint,
        Rectangle,
        Door,
        Key,
        Connector,
        Erase,
        Pan,
        Region
    }

    public enum PlanningDetailTool
    {
        Select,
        Box,
        Erase,
        Pan,
        Merge,
        Parameters
    }

    public enum PlanningDetailType
    {
        Solid,
        Platform,
        Hazard,
        Water,
        Ladder,
        Exit,
        Item,
        Enemy,
        Note,
        Prop
    }

    public enum PlanningPortSide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    [Serializable]
    public sealed class PlanningCell
    {
        public int x;
        public int y;

        public PlanningCell()
        {
        }

        public PlanningCell(int valueX, int valueY)
        {
            x = valueX;
            y = valueY;
        }
    }

    [Serializable]
    public sealed class PlanningBox
    {
        public string id;
        public PlanningDetailType type;
        public string label;
        public string paletteEntryId;
        public string paletteEntryName;
        public string propEntryId;
        public string propEntryName;
        public int x;
        public int y;
        public int width = 1;
        public int height = 1;
        public bool isMerged;
        public bool singleInstance;
        public string doorOwnerId;
        public List<string> mergeParts = new List<string>();
        public bool hasComponentOverrides;
        public List<LevelEditorComponentValueOverride> componentOverrides = new List<LevelEditorComponentValueOverride>();
        public PlanningBox Clone() => JsonUtility.FromJson<PlanningBox>(JsonUtility.ToJson(this));
        [NonSerialized] public LevelEditorPlacedBlock sceneBlock;

        public PlanningBox()
        {
        }

        public PlanningBox(
            PlanningDetailType valueType,
            RectInt rect)
        {
            id = PlanningDocument.NewId("box");
            type = valueType;
            x = rect.x;
            y = rect.y;
            width = Mathf.Max(1, rect.width);
            height = Mathf.Max(1, rect.height);
        }
    }

    [Serializable]
    public sealed class PlanningDoor
    {
        public string id;
        public string roomId;
        public string lockId;
        public int x;
        public int y;
        public int direction;

        public PlanningDoor()
        {
        }

        public PlanningDoor(
            string valueRoomId,
            int valueX,
            int valueY,
            int valueDirection)
        {
            id = PlanningDocument.NewId("door");
            roomId = valueRoomId;
            x = valueX;
            y = valueY;
            direction = valueDirection;
        }
    }

    [Serializable]
    public sealed class PlanningKey
    {
        public string id;
        public string name;
        public string colorHex = "#F5D35A";
        public string roomId;
        public int x;
        public int y;

        public PlanningKey()
        {
        }

        public PlanningKey(
            string valueName,
            string valueRoomId,
            int valueX,
            int valueY)
        {
            id = PlanningDocument.NewId("key");
            name = valueName;
            roomId = valueRoomId;
            x = valueX;
            y = valueY;
        }
    }

    [Serializable]
    public sealed class PlanningLock
    {
        public string id;
        public string name;
        public string colorHex = "#F5D35A";
        public List<string> keyIds = new List<string>();

        public PlanningLock()
        {
        }

        public PlanningLock(string valueName, string valueColorHex)
        {
            id = PlanningDocument.NewId("lock");
            name = valueName;
            colorHex = valueColorHex;
        }
    }

    [Serializable]
    public sealed class PlanningRoom
    {
        public string id;
        public string name;
        public bool isConnector;
        public bool independentCells;
        public List<string> connectedRoomIds = new List<string>();
        public string fromRoomId;
        public string toRoomId;
        public int connectorWidth = 4;
        public PlanningPortSide fromSide = PlanningPortSide.Right;
        public float fromOffset = .5f;
        public PlanningPortSide toSide = PlanningPortSide.Left;
        public float toOffset = .5f;
        public int detailBoundsX;
        public int detailBoundsY;
        public int detailBoundsWidth;
        public int detailBoundsHeight;
        public List<PlanningCell> cells = new List<PlanningCell>();
        public List<PlanningBox> boxes = new List<PlanningBox>();

        public PlanningRoom()
        {
        }

        public PlanningRoom(string valueName)
        {
            id = PlanningDocument.NewId("room");
            name = valueName;
        }

        public static PlanningRoom CreateConnector(
            string valueName,
            string valueFromRoomId,
            string valueToRoomId)
        {
            return new PlanningRoom(valueName)
            {
                isConnector = true,
                fromRoomId = valueFromRoomId,
                toRoomId = valueToRoomId,
                connectorWidth = 4
            };
        }

        public RectInt GetLocalAllowedRect(
            int cellWidth,
            int cellHeight)
        {
            if (cells.Count == 0)
            {
                return new RectInt(0, 0, cellWidth, cellHeight);
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int index = 0; index < cells.Count; index++)
            {
                PlanningCell cell = cells[index];
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new RectInt(
                0,
                0,
                Mathf.Max(1, maxX - minX + 1) * cellWidth,
                Mathf.Max(1, maxY - minY + 1) * cellHeight);
        }
    }

    [Serializable]
    public sealed class PlanningRegion
    {
        public string id = PlanningDocument.NewId("region");
        public string annotation = "新区域";
        public int x, y, width = 1, height = 1;
        public string colorHex = "#799BCC";
        public RectInt Bounds => new RectInt(x, y, width, height);
    }

    [Serializable]
    public sealed class PlanningDocument
    {
        private const string DefaultMapAssetPath =
            "Assets/_Project/Development/LevelEditor/Orpheus0829/" +
            "PlanningEditorPrototype/Editor/DefaultPlanningMap.json";

        public string name = "未命名地图";
        public int worldBlockCellWidth = 16;
        public int worldBlockCellHeight = 16;
        public List<PlanningRoom> rooms = new List<PlanningRoom>();
        public List<PlanningRegion> regions = new List<PlanningRegion>();
        public List<PlanningDoor> doors = new List<PlanningDoor>();
        public List<PlanningKey> keys = new List<PlanningKey>();
        public List<PlanningLock> locks = new List<PlanningLock>();
        public List<PlanningBox> assemblyPatches =
            new List<PlanningBox>();
        public string playerStartRoomId;
        public Vector2 playerStartLocal = new Vector2(2.5f, 12f);

        public static string NewId(string prefix)
        {
            return prefix + "_" + Guid.NewGuid()
                .ToString("N")
                .Substring(0, 8);
        }

        public static PlanningDocument CreateDefault()
        {
            PlanningDocument embedded = LoadEmbeddedDefault();
            if (embedded != null)
            {
                embedded.name = "示例地图";
                embedded.Normalize();
                return embedded;
            }

            var document = new PlanningDocument();
            PlanningRoom start = AddRoom(
                document,
                "R1 道具大厅",
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0),
                new Vector2Int(4, 0),
                new Vector2Int(5, 0),
                new Vector2Int(6, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(3, 1),
                new Vector2Int(4, 1),
                new Vector2Int(5, 1),
                new Vector2Int(6, 1));
            PlanningRoom redRoom = AddRoom(
                document,
                "R2 红色机关房",
                new Vector2Int(8, 0));
            PlanningRoom blueRoom = AddRoom(
                document,
                "R3 蓝色水流房",
                new Vector2Int(9, 0));
            PlanningRoom greenRoom = AddRoom(
                document,
                "R4 绿色攀爬房",
                new Vector2Int(10, 0));
            PlanningRoom finalRoom = AddRoom(
                document,
                "R5 终点房",
                new Vector2Int(11, 0));

            BuildShowcaseRoomDetails(
                start,
                redRoom,
                blueRoom,
                greenRoom,
                finalRoom);
            BuildShowcaseConnectors(
                document,
                start,
                redRoom,
                blueRoom,
                greenRoom,
                finalRoom);
            document.playerStartRoomId = start.id;
            document.playerStartLocal = new Vector2(2.5f, 28f);
            return document;
        }

        private static PlanningDocument LoadEmbeddedDefault()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                DefaultMapAssetPath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<PlanningDocument>(asset.text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PlanningDocument CreateDefaultLegacy()
        {
            var document = new PlanningDocument();

            PlanningRoom start = AddRoom(
                document,
                "R1 起点",
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

            PlanningRoom blueKeyRoom = AddRoom(
                document,
                "R2 蓝色钥匙房",
                new Vector2Int(3, 0),
                new Vector2Int(4, 0));
            PlanningRoom blueGate = AddRoom(
                document,
                "R3 蓝色门厅",
                new Vector2Int(6, 0),
                new Vector2Int(7, 0));

            PlanningRoom returnPath = AddRoom(
                document,
                "R4 回访通路",
                new Vector2Int(9, 0),
                new Vector2Int(10, 0));

            PlanningRoom greenKeyRoom = AddRoom(
                document,
                "R5 绿色钥匙房",
                new Vector2Int(12, 0),
                new Vector2Int(13, 0));
            PlanningRoom greenGate = AddRoom(
                document,
                "R6 绿色门厅",
                new Vector2Int(15, 0),
                new Vector2Int(16, 0));

            PlanningRoom redKeyRoom = AddRoom(
                document,
                "R7 红色钥匙房",
                new Vector2Int(18, 0),
                new Vector2Int(19, 0));

            PlanningRoom redGate = AddRoom(
                document,
                "R8 红色门厅",
                new Vector2Int(21, 0),
                new Vector2Int(22, 0));

            PlanningRoom finalRoom = AddRoom(
                document,
                "R9 终点核心",
                new Vector2Int(24, 0),
                new Vector2Int(25, 0));

            BuildSampleRoomDetails(
                start,
                blueKeyRoom,
                blueGate,
                returnPath,
                greenKeyRoom,
                greenGate,
                redKeyRoom,
                redGate,
                finalRoom);
            BuildSampleConnectors(
                document,
                start,
                blueKeyRoom,
                blueGate,
                returnPath,
                greenKeyRoom,
                greenGate,
                redKeyRoom,
                redGate,
                finalRoom);

            PlanningKey blueKey = AddKey(
                document,
                "蓝钥匙",
                "#3FA9D8",
                3,
                0);
            PlanningKey greenKey = AddKey(
                document,
                "绿钥匙",
                "#62B56C",
                12,
                0);
            PlanningKey redKey = AddKey(
                document,
                "红钥匙",
                "#D45A47",
                18,
                0);

            PlanningLock blueLock = AddLock(
                document,
                "蓝门",
                "#3FA9D8",
                blueKey.id);
            PlanningLock greenLock = AddLock(
                document,
                "绿门",
                "#62B56C",
                greenKey.id);
            PlanningLock redLock = AddLock(
                document,
                "红门",
                "#D45A47",
                redKey.id);

            AddDoor(document, blueGate, 6, 0, blueLock.id);
            AddDoor(document, greenGate, 15, 0, greenLock.id);
            AddDoor(document, redGate, 21, 0, redLock.id);
            return document;
        }

        private static PlanningRoom AddRoom(
            PlanningDocument document,
            string name,
            params Vector2Int[] cells)
        {
            var room = new PlanningRoom(name);
            for (int index = 0; index < cells.Length; index++)
            {
                room.cells.Add(new PlanningCell(
                    cells[index].x,
                    cells[index].y));
            }

            document.rooms.Add(room);
            return room;
        }

        private static PlanningKey AddKey(
            PlanningDocument document,
            string name,
            string colorHex,
            int x,
            int y)
        {
            var key = new PlanningKey(
                name,
                document.FindRoomAt(x, y)?.id,
                x,
                y);
            key.colorHex = colorHex;
            document.keys.Add(key);
            return key;
        }

        private static PlanningLock AddLock(
            PlanningDocument document,
            string name,
            string colorHex,
            string keyId)
        {
            var lockValue = new PlanningLock(name, colorHex);
            lockValue.keyIds.Add(keyId);
            document.locks.Add(lockValue);
            return lockValue;
        }

        private static void AddDoor(
            PlanningDocument document,
            PlanningRoom room,
            int x,
            int y,
            string lockId)
        {
            var door = new PlanningDoor(room.id, x, y, 0)
            {
                lockId = lockId
            };
            document.doors.Add(door);
        }

        private static void BuildShowcaseRoomDetails(
            PlanningRoom start,
            PlanningRoom redRoom,
            PlanningRoom blueRoom,
            PlanningRoom greenRoom,
            PlanningRoom finalRoom)
        {
            AddBox(start, PlanningDetailType.Solid, 0, 29, 112, 2, "大厅地基");
            AddBox(start, PlanningDetailType.Solid, 0, 3, 2, 26, "大厅左墙");
            AddBox(start, PlanningDetailType.Solid, 110, 3, 2, 26, "大厅右墙");
            AddBox(start, PlanningDetailType.Solid, 2, 3, 108, 1, "大厅顶板");
            AddBox(start, PlanningDetailType.Note, 4, 4, 104, 1, "测试区：钥匙 / 红 / 蓝 / 绿 / 通用展示", "白方块");

            AddProp(start, "3c635d52f11f48f18d4d82a53c65e601", "红色钥匙", "红色钥匙", 4, 28);
            AddProp(start, "2986677913284d2f85e9c64011ca22d0", "绿色钥匙", "绿色钥匙", 6, 28);
            AddProp(start, "04b7a2d6735b4d0cbce343ab1293d6e0", "蓝色钥匙", "蓝色钥匙", 8, 28);

            AddProp(start, "06e690b71b634be2909b6fecd7ecb627", "字幕激发(测试)", "字幕激发测试", 14, 28);
            AddProp(start, "7a1b2c3d4e5f60718293a4b5c6d7e8f9", "锚点", "锚点", 18, 28);
            AddProp(start, "8b88592971284aea9d1616569675b69b", "字幕展示道具", "Display_Block", 22, 28);
            AddProp(start, "0fa659ac46b54c40942bb0566aacf593", "万能方块", "万能方块", 26, 28);

            AddProp(start, "b3197af4d69b44778f4f6d43fb2634e2", "岩浆", "岩浆", 32, 28);
            AddProp(start, "1b9aae04512d422c827d95dd1f66ba45", "能源快", "能源快", 36, 28);
            AddProp(start, "ce4252f60fa240bfafb4d340ece01941", "按钮", "按钮", 40, 28);
            AddProp(start, "d13739bd929949c88ceb41a1a74ea6ea", "曲柄", "曲柄", 44, 28);
            AddProp(start, "16c56a2b8cfc43ae9ab69270626c8de7", "基座", "基座", 48, 28);
            AddProp(start, "212cd7a2ff304a6288b4d850132de143", "平台", "平台", 52, 28);

            AddProp(start, "234025e375b248bfbb8ac4dca60021fa", "水源", "水源 A", 58, 28);
            AddProp(start, "6a317712f4c344038e7eff8345de01a0", "水源", "水源 B", 62, 28);
            AddProp(start, "4ba0994908e1477fb5f7dae6a0e0cb40", "气泡柱", "气泡柱", 66, 28);

            AddProp(start, "b1789be1339f4f5b897ef3177788da37", "藤蔓", "藤蔓", 72, 28);
            AddProp(start, "eb7a961c3edb4b2db919a5c209562fed", "弹性植物", "弹性植物", 76, 28);
            AddProp(start, "2b19e161603c4ea8b7e74919e1656f1b", "植物障碍", "植物障碍", 80, 28);

            BuildReservedRoom(redRoom, "红色机关");
            BuildReservedRoom(blueRoom, "蓝色水流");
            BuildReservedRoom(greenRoom, "绿色攀爬");
            BuildReservedRoom(finalRoom, "终点汇合");
        }

        private static void BuildReservedRoom(
            PlanningRoom room,
            string title)
        {
            AddBox(room, PlanningDetailType.Solid, 0, 13, 16, 2, $"{title}房地基");
            AddBox(room, PlanningDetailType.Solid, 0, 3, 2, 10, "左墙");
            AddBox(room, PlanningDetailType.Solid, 14, 3, 2, 10, "右墙");
            AddBox(room, PlanningDetailType.Platform, 3, 11, 4, 1, "入口平台");
            AddBox(room, PlanningDetailType.Platform, 9, 8, 4, 1, "中段平台");
            AddBox(room, PlanningDetailType.Note, 3, 4, 10, 1, $"{title}预留区", "白方块");
        }

        private static void BuildShowcaseConnectors(
            PlanningDocument document,
            PlanningRoom start,
            PlanningRoom redRoom,
            PlanningRoom blueRoom,
            PlanningRoom greenRoom,
            PlanningRoom finalRoom)
        {
            var rooms = new List<PlanningRoom>
            {
                start,
                redRoom,
                blueRoom,
                greenRoom,
                finalRoom
            };
            GetSampleStride(rooms, out int strideX, out int strideY);
            AddSampleConnector(
                document,
                start,
                redRoom,
                new Vector2Int(6, 0),
                new Vector2Int(8, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                redRoom,
                blueRoom,
                new Vector2Int(8, 0),
                new Vector2Int(9, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                blueRoom,
                greenRoom,
                new Vector2Int(9, 0),
                new Vector2Int(10, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                greenRoom,
                finalRoom,
                new Vector2Int(10, 0),
                new Vector2Int(11, 0),
                strideX,
                strideY);
            document.RefreshConnectorPaths();
        }

        private static void BuildSimpleSampleRoomDetails(
            PlanningRoom start,
            PlanningRoom redRoom,
            PlanningRoom blueRoom,
            PlanningRoom greenRoom,
            PlanningRoom finalRoom)
        {
            AddBox(start, PlanningDetailType.Solid, 0, 13, 16, 2, "黑色地基");
            AddBox(start, PlanningDetailType.Solid, 0, 3, 2, 10, "左墙");
            AddBox(start, PlanningDetailType.Solid, 14, 3, 2, 10, "右墙");
            AddBox(start, PlanningDetailType.Platform, 3, 11, 4, 1, "白色踏板 1");
            AddBox(start, PlanningDetailType.Platform, 8, 9, 4, 1, "白色踏板 2");
            AddBox(start, PlanningDetailType.Platform, 12, 7, 2, 1, "白色踏板 3");
            AddBox(start, PlanningDetailType.Item, 3, 12, 1, 1, "起点");
            AddBox(start, PlanningDetailType.Note, 5, 4, 7, 1, "黑块承重", "白方块");

            AddBox(redRoom, PlanningDetailType.Solid, 0, 13, 16, 2, "红色房地基");
            AddBox(redRoom, PlanningDetailType.Solid, 0, 3, 2, 10, "左墙");
            AddBox(redRoom, PlanningDetailType.Solid, 14, 3, 2, 10, "右墙");
            AddBox(redRoom, PlanningDetailType.Hazard, 3, 11, 4, 1, "红色交互块 A");
            AddBox(redRoom, PlanningDetailType.Hazard, 8, 9, 4, 1, "红色交互块 B");
            AddBox(redRoom, PlanningDetailType.Hazard, 12, 7, 2, 1, "红色交互块 C");
            AddBox(redRoom, PlanningDetailType.Platform, 4, 5, 5, 1, "红色回看台");
            AddBox(redRoom, PlanningDetailType.Note, 5, 4, 7, 1, "红色触发连锁", "白方块");

            AddBox(blueRoom, PlanningDetailType.Solid, 0, 13, 16, 2, "蓝色房地基");
            AddBox(blueRoom, PlanningDetailType.Solid, 0, 3, 2, 10, "左墙");
            AddBox(blueRoom, PlanningDetailType.Solid, 14, 3, 2, 10, "右墙");
            AddBox(blueRoom, PlanningDetailType.Water, 3, 11, 4, 1, "蓝色桥 A");
            AddBox(blueRoom, PlanningDetailType.Water, 8, 9, 4, 1, "蓝色桥 B");
            AddBox(blueRoom, PlanningDetailType.Water, 12, 7, 2, 1, "蓝色桥 C");
            AddBox(blueRoom, PlanningDetailType.Platform, 12, 5, 2, 1, "蓝色高台");
            AddBox(blueRoom, PlanningDetailType.Note, 5, 4, 7, 1, "蓝色改变路径", "白方块");

            AddBox(greenRoom, PlanningDetailType.Solid, 0, 13, 16, 2, "绿色房地基");
            AddBox(greenRoom, PlanningDetailType.Solid, 0, 3, 2, 10, "左墙");
            AddBox(greenRoom, PlanningDetailType.Solid, 14, 3, 2, 10, "右墙");
            AddBox(greenRoom, PlanningDetailType.Ladder, 3, 11, 4, 1, "绿色平台 A");
            AddBox(greenRoom, PlanningDetailType.Ladder, 8, 9, 4, 1, "绿色平台 B");
            AddBox(greenRoom, PlanningDetailType.Ladder, 12, 7, 2, 1, "绿色平台 C");
            AddBox(greenRoom, PlanningDetailType.Water, 5, 5, 4, 1, "蓝绿连接台");
            AddBox(greenRoom, PlanningDetailType.Note, 5, 4, 7, 1, "绿块连接蓝块", "白方块");

            AddBox(finalRoom, PlanningDetailType.Solid, 0, 13, 16, 2, "终点地基");
            AddBox(finalRoom, PlanningDetailType.Solid, 0, 3, 2, 10, "左墙");
            AddBox(finalRoom, PlanningDetailType.Solid, 14, 3, 2, 10, "右墙");
            AddBox(finalRoom, PlanningDetailType.Water, 3, 11, 4, 1, "蓝色终点段");
            AddBox(finalRoom, PlanningDetailType.Ladder, 8, 9, 4, 1, "绿色终点段");
            AddBox(finalRoom, PlanningDetailType.Hazard, 12, 7, 2, 1, "红色终点段");
            AddBox(finalRoom, PlanningDetailType.Item, 6, 5, 3, 1, "核心");
            AddBox(finalRoom, PlanningDetailType.Note, 4, 4, 9, 1, "三色交互汇合", "白方块");
        }

        private static void BuildSimpleSampleConnectors(
            PlanningDocument document,
            PlanningRoom start,
            PlanningRoom redRoom,
            PlanningRoom blueRoom,
            PlanningRoom greenRoom,
            PlanningRoom finalRoom)
        {
            AddSimpleConnector(
                document,
                start,
                redRoom,
                new Vector2Int(0, 0),
                new Vector2Int(2, 0));
            AddSimpleConnector(
                document,
                redRoom,
                blueRoom,
                new Vector2Int(2, 0),
                new Vector2Int(4, 0));
            AddSimpleConnector(
                document,
                blueRoom,
                greenRoom,
                new Vector2Int(4, 0),
                new Vector2Int(6, 0));
            AddSimpleConnector(
                document,
                greenRoom,
                finalRoom,
                new Vector2Int(6, 0),
                new Vector2Int(8, 0));
        }

        private static void AddSimpleConnector(
            PlanningDocument document,
            PlanningRoom from,
            PlanningRoom to,
            Vector2Int startHint,
            Vector2Int endHint)
        {
            PlanningRoom connector = PlanningRoom.CreateConnector(
                $"通道 {from.name} → {to.name}",
                from.id,
                to.id);
            connector.connectorWidth = 4;
            PlanningDocument.SetConnectorPorts(
                connector,
                from,
                to,
                new PlanningCell(startHint.x, startHint.y),
                new PlanningCell(endHint.x, endHint.y));
            document.rooms.Add(connector);
            document.RefreshConnectorPaths();
        }

        private static void BuildSampleRoomDetails(
            PlanningRoom start,
            PlanningRoom blueKeyRoom,
            PlanningRoom blueGate,
            PlanningRoom returnPath,
            PlanningRoom greenKeyRoom,
            PlanningRoom greenGate,
            PlanningRoom redKeyRoom,
            PlanningRoom redGate,
            PlanningRoom finalRoom)
        {
            AddBox(start, PlanningDetailType.Solid, 0, 12, 24, 2, "黑色承重地面");
            AddBox(start, PlanningDetailType.Solid, 0, 3, 2, 9, "左侧黑墙");
            AddBox(start, PlanningDetailType.Solid, 23, 3, 1, 9, "右侧黑墙");
            AddBox(start, PlanningDetailType.Platform, 4, 10, 4, 1, "白色踏板 1");
            AddBox(start, PlanningDetailType.Platform, 9, 8, 4, 1, "白色踏板 2");
            AddBox(start, PlanningDetailType.Platform, 14, 6, 4, 1, "白色回看踏板");
            AddBox(start, PlanningDetailType.Exit, 20, 10, 3, 2, "开放出口 · 通往 R2", "黑方块");
            AddBox(start, PlanningDetailType.Item, 3, 11, 1, 1, "移动提示", "白方块");
            AddBox(start, PlanningDetailType.Enemy, 15, 11, 1, 1, "基础敌人");
            AddBox(start, PlanningDetailType.Note, 8, 11, 3, 1, "黑块承重，白块可踩", "白方块");

            AddBox(blueKeyRoom, PlanningDetailType.Solid, 0, 13, 26, 2, "蓝色钥匙房黑底");
            AddBox(blueKeyRoom, PlanningDetailType.Solid, 0, 3, 2, 10, "左墙");
            AddBox(blueKeyRoom, PlanningDetailType.Solid, 24, 3, 2, 10, "右墙");
            AddBox(blueKeyRoom, PlanningDetailType.Water, 5, 11, 5, 1, "蓝色交互块 A");
            AddBox(blueKeyRoom, PlanningDetailType.Water, 10, 9, 4, 1, "蓝色交互块 B");
            AddBox(blueKeyRoom, PlanningDetailType.Water, 15, 7, 5, 1, "蓝色交互块 C");
            AddBox(blueKeyRoom, PlanningDetailType.Platform, 20, 5, 4, 1, "钥匙白色高台");
            AddBox(blueKeyRoom, PlanningDetailType.Item, 21, 3, 2, 2, "蓝钥匙", "蓝方块");
            AddBox(blueKeyRoom, PlanningDetailType.Exit, 2, 12, 2, 2, "返回 R1", "黑方块");
            AddBox(blueKeyRoom, PlanningDetailType.Exit, 22, 11, 3, 2, "通往 R3", "黑方块");
            AddBox(blueKeyRoom, PlanningDetailType.Enemy, 12, 12, 1, 1, "守卫敌人");
            AddBox(blueKeyRoom, PlanningDetailType.Note, 6, 12, 6, 1, "蓝块开始提供交互路径", "白方块");

            AddBox(blueGate, PlanningDetailType.Solid, 0, 13, 28, 2, "蓝色门厅黑底");
            AddBox(blueGate, PlanningDetailType.Solid, 0, 2, 2, 11, "左墙");
            AddBox(blueGate, PlanningDetailType.Solid, 26, 2, 2, 11, "右墙");
            AddBox(blueGate, PlanningDetailType.Water, 4, 11, 5, 1, "蓝块桥 A");
            AddBox(blueGate, PlanningDetailType.Water, 10, 9, 5, 1, "蓝块桥 B");
            AddBox(blueGate, PlanningDetailType.Water, 16, 7, 5, 1, "蓝块桥 C");
            AddBox(blueGate, PlanningDetailType.Platform, 20, 5, 4, 1, "门厅白色落脚点");
            AddBox(blueGate, PlanningDetailType.Exit, 2, 12, 2, 2, "返回 R2", "黑方块");
            AddBox(blueGate, PlanningDetailType.Exit, 23, 4, 3, 2, "蓝色门 · 通往 R4", "蓝方块");
            AddBox(blueGate, PlanningDetailType.Enemy, 13, 12, 1, 1, "门厅敌人");
            AddBox(blueGate, PlanningDetailType.Note, 7, 12, 10, 1, "蓝钥匙开启蓝色门", "白方块");

            AddBox(returnPath, PlanningDetailType.Solid, 0, 11, 26, 2, "回访通路黑底");
            AddBox(returnPath, PlanningDetailType.Solid, 0, 2, 2, 9, "左墙");
            AddBox(returnPath, PlanningDetailType.Solid, 24, 2, 2, 9, "右墙");
            AddBox(returnPath, PlanningDetailType.Platform, 3, 9, 4, 1, "入口白色台阶");
            AddBox(returnPath, PlanningDetailType.Platform, 18, 5, 4, 1, "右侧白色高台");
            AddBox(returnPath, PlanningDetailType.Ladder, 6, 7, 1, 5, "绿色交互块 A");
            AddBox(returnPath, PlanningDetailType.Ladder, 10, 7, 1, 5, "绿色交互块 B");
            AddBox(returnPath, PlanningDetailType.Ladder, 21, 5, 1, 7, "绿色出口链");
            AddBox(returnPath, PlanningDetailType.Exit, 2, 9, 1, 2, "返回 R3", "黑方块");
            AddBox(returnPath, PlanningDetailType.Exit, 23, 9, 1, 2, "通往 R5", "黑方块");
            AddBox(returnPath, PlanningDetailType.Note, 9, 10, 9, 1, "回访后绿色方块激活", "白方块");

            AddBox(greenKeyRoom, PlanningDetailType.Solid, 0, 13, 28, 2, "绿色钥匙房黑底");
            AddBox(greenKeyRoom, PlanningDetailType.Solid, 0, 2, 2, 11, "左墙");
            AddBox(greenKeyRoom, PlanningDetailType.Solid, 26, 2, 2, 11, "右墙");
            AddBox(greenKeyRoom, PlanningDetailType.Water, 4, 11, 4, 1, "蓝块基座");
            AddBox(greenKeyRoom, PlanningDetailType.Water, 8, 9, 4, 1, "蓝块接力平台");
            AddBox(greenKeyRoom, PlanningDetailType.Ladder, 12, 7, 4, 1, "绿色交互块 1");
            AddBox(greenKeyRoom, PlanningDetailType.Ladder, 16, 5, 4, 1, "绿色交互块 2");
            AddBox(greenKeyRoom, PlanningDetailType.Platform, 20, 3, 5, 1, "钥匙白色平台");
            AddBox(greenKeyRoom, PlanningDetailType.Item, 21, 2, 3, 1, "绿钥匙", "绿方块");
            AddBox(greenKeyRoom, PlanningDetailType.Exit, 2, 11, 2, 2, "返回 R4", "黑方块");
            AddBox(greenKeyRoom, PlanningDetailType.Enemy, 11, 12, 1, 1, "钥匙房守卫");
            AddBox(greenKeyRoom, PlanningDetailType.Note, 6, 12, 8, 1, "蓝块连接绿色方块", "白方块");

            AddBox(greenGate, PlanningDetailType.Solid, 0, 12, 28, 2, "绿色门厅黑底");
            AddBox(greenGate, PlanningDetailType.Solid, 0, 3, 2, 9, "左墙");
            AddBox(greenGate, PlanningDetailType.Solid, 26, 3, 2, 9, "右墙");
            AddBox(greenGate, PlanningDetailType.Water, 4, 10, 4, 1, "蓝绿链起点");
            AddBox(greenGate, PlanningDetailType.Ladder, 8, 8, 4, 1, "绿链平台 A");
            AddBox(greenGate, PlanningDetailType.Water, 13, 6, 4, 1, "蓝链平台 B");
            AddBox(greenGate, PlanningDetailType.Ladder, 18, 4, 5, 1, "绿链终点");
            AddBox(greenGate, PlanningDetailType.Exit, 2, 11, 2, 2, "返回 R5", "黑方块");
            AddBox(greenGate, PlanningDetailType.Exit, 23, 3, 3, 2, "绿色门 · 通往 R7", "绿方块");
            AddBox(greenGate, PlanningDetailType.Enemy, 12, 11, 1, 1, "连锁守卫");
            AddBox(greenGate, PlanningDetailType.Note, 8, 11, 11, 1, "蓝块触发绿色方块连锁", "白方块");

            AddBox(redKeyRoom, PlanningDetailType.Solid, 0, 13, 32, 2, "红色钥匙房黑底");
            AddBox(redKeyRoom, PlanningDetailType.Solid, 0, 3, 2, 10, "入口墙");
            AddBox(redKeyRoom, PlanningDetailType.Solid, 30, 3, 2, 10, "出口墙");
            AddBox(redKeyRoom, PlanningDetailType.Hazard, 6, 11, 7, 1, "红色交互链 A");
            AddBox(redKeyRoom, PlanningDetailType.Hazard, 15, 10, 5, 1, "红色交互链 B");
            AddBox(redKeyRoom, PlanningDetailType.Hazard, 22, 8, 5, 1, "红色交互链 C");
            AddBox(redKeyRoom, PlanningDetailType.Water, 4, 9, 3, 1, "蓝块触发点");
            AddBox(redKeyRoom, PlanningDetailType.Ladder, 11, 7, 4, 1, "绿块中转台");
            AddBox(redKeyRoom, PlanningDetailType.Platform, 18, 5, 4, 1, "钥匙白色平台");
            AddBox(redKeyRoom, PlanningDetailType.Item, 20, 4, 3, 1, "红钥匙", "能源方块");
            AddBox(redKeyRoom, PlanningDetailType.Exit, 2, 12, 2, 2, "返回 R6", "黑方块");
            AddBox(redKeyRoom, PlanningDetailType.Exit, 28, 11, 2, 2, "通往 R8", "黑方块");
            AddBox(redKeyRoom, PlanningDetailType.Enemy, 18, 12, 1, 1, "红色连锁守卫");
            AddBox(redKeyRoom, PlanningDetailType.Note, 8, 5, 10, 1, "红块负责触发连续机关", "白方块");

            AddBox(redGate, PlanningDetailType.Solid, 0, 12, 27, 2, "红色门厅黑底");
            AddBox(redGate, PlanningDetailType.Solid, 0, 3, 2, 9, "左墙");
            AddBox(redGate, PlanningDetailType.Solid, 25, 3, 2, 9, "右墙");
            AddBox(redGate, PlanningDetailType.Water, 5, 10, 4, 1, "蓝块支点 A");
            AddBox(redGate, PlanningDetailType.Ladder, 10, 8, 4, 1, "绿块支点 B");
            AddBox(redGate, PlanningDetailType.Hazard, 15, 6, 5, 1, "红块主机关");
            AddBox(redGate, PlanningDetailType.Platform, 20, 4, 4, 1, "红门白色平台");
            AddBox(redGate, PlanningDetailType.Exit, 2, 11, 2, 2, "返回 R7", "黑方块");
            AddBox(redGate, PlanningDetailType.Exit, 22, 3, 3, 2, "红色门 · 通往 R9", "能源方块");
            AddBox(redGate, PlanningDetailType.Enemy, 12, 11, 1, 1, "红色门卫");
            AddBox(redGate, PlanningDetailType.Item, 19, 10, 2, 1, "连锁检查点", "白方块");
            AddBox(redGate, PlanningDetailType.Note, 8, 11, 11, 1, "红钥匙完成三色连锁", "白方块");

            AddBox(finalRoom, PlanningDetailType.Solid, 0, 13, 30, 2, "终点核心黑底");
            AddBox(finalRoom, PlanningDetailType.Solid, 0, 2, 2, 11, "左墙");
            AddBox(finalRoom, PlanningDetailType.Solid, 28, 2, 2, 11, "右墙");
            AddBox(finalRoom, PlanningDetailType.Water, 4, 11, 5, 1, "蓝色汇合段");
            AddBox(finalRoom, PlanningDetailType.Water, 9, 9, 4, 1, "蓝色抬升段");
            AddBox(finalRoom, PlanningDetailType.Ladder, 14, 7, 4, 1, "绿色汇合段");
            AddBox(finalRoom, PlanningDetailType.Ladder, 19, 5, 4, 1, "绿色抬升段");
            AddBox(finalRoom, PlanningDetailType.Hazard, 23, 3, 5, 1, "红色核心段");
            AddBox(finalRoom, PlanningDetailType.Item, 25, 2, 2, 1, "终点核心", "白方块");
            AddBox(finalRoom, PlanningDetailType.Exit, 2, 11, 1, 2, "返回 R8", "黑方块");
            AddBox(finalRoom, PlanningDetailType.Enemy, 11, 12, 1, 1, "核心守卫");
            AddBox(finalRoom, PlanningDetailType.Note, 9, 4, 11, 1, "蓝、绿、红材料最终汇合", "白方块");
        }

        private static void AddBox(
            PlanningRoom room,
            PlanningDetailType type,
            int x,
            int y,
            int width,
            int height,
            string label,
            string paletteEntryName = null)
        {
            var box = new PlanningBox(
                type,
                new RectInt(x, y, width, height))
            {
                label = label,
                paletteEntryName = string.IsNullOrEmpty(paletteEntryName)
                    ? DefaultPaletteEntryName(type)
                    : paletteEntryName
            };
            room.boxes.Add(box);
        }

        private static void AddProp(
            PlanningRoom room,
            string entryId,
            string entryName,
            string label,
            int x,
            int y)
        {
            room.boxes.Add(new PlanningBox(
                PlanningDetailType.Prop,
                new RectInt(x, y, 1, 1))
            {
                label = label,
                paletteEntryName = "白方块",
                propEntryId = entryId,
                propEntryName = entryName
            });
        }

        private static void BuildSampleConnectors(
            PlanningDocument document,
            PlanningRoom start,
            PlanningRoom blueKeyRoom,
            PlanningRoom blueGate,
            PlanningRoom returnPath,
            PlanningRoom greenKeyRoom,
            PlanningRoom greenGate,
            PlanningRoom redKeyRoom,
            PlanningRoom redGate,
            PlanningRoom finalRoom)
        {
            var rooms = new List<PlanningRoom>
            {
                start,
                blueKeyRoom,
                blueGate,
                returnPath,
                greenKeyRoom,
                greenGate,
                redKeyRoom,
                redGate,
                finalRoom
            };
            GetSampleStride(rooms, out int strideX, out int strideY);
            AddSampleConnector(
                document,
                start,
                blueKeyRoom,
                new Vector2Int(1, 0),
                new Vector2Int(3, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                blueKeyRoom,
                blueGate,
                new Vector2Int(4, 0),
                new Vector2Int(6, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                blueGate,
                returnPath,
                new Vector2Int(7, 0),
                new Vector2Int(9, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                returnPath,
                greenKeyRoom,
                new Vector2Int(10, 0),
                new Vector2Int(12, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                greenKeyRoom,
                greenGate,
                new Vector2Int(13, 0),
                new Vector2Int(15, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                greenGate,
                redKeyRoom,
                new Vector2Int(16, 0),
                new Vector2Int(18, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                redKeyRoom,
                redGate,
                new Vector2Int(19, 0),
                new Vector2Int(21, 0),
                strideX,
                strideY);
            AddSampleConnector(
                document,
                redGate,
                finalRoom,
                new Vector2Int(22, 0),
                new Vector2Int(24, 0),
                strideX,
                strideY);
            document.RefreshConnectorPaths();
        }

        private static void AddSampleConnector(
            PlanningDocument document,
            PlanningRoom from,
            PlanningRoom to,
            Vector2Int startCell,
            Vector2Int endCell,
            int strideX,
            int strideY)
        {
            PlanningRoom connector = PlanningRoom.CreateConnector(
                $"通道 {from.name} → {to.name}",
                from.id,
                to.id);
            PlanningDocument.SetConnectorPorts(
                connector,
                from,
                to,
                new PlanningCell(startCell.x, startCell.y),
                new PlanningCell(endCell.x, endCell.y));

            RectInt fromRect = GetSampleRoomRect(from, strideX, strideY);
            RectInt toRect = GetSampleRoomRect(to, strideX, strideY);
            int startEdgeX = endCell.x >= startCell.x
                ? fromRect.xMax
                : fromRect.xMin - 1;
            int endEdgeX = endCell.x >= startCell.x
                ? toRect.xMin - 1
                : toRect.xMax;
            int fromFloorY = Mathf.Max(1, fromRect.yMax - 2);
            int toFloorY = Mathf.Max(1, toRect.yMax - 2);
            int corridorHeight = 5;
            int minX = Mathf.Min(startEdgeX, endEdgeX);
            int maxX = Mathf.Max(startEdgeX, endEdgeX);
            if (fromFloorY == toFloorY)
            {
                AddHorizontalTunnel(
                    connector,
                    minX,
                    maxX,
                    fromFloorY,
                    corridorHeight);
            }
            else
            {
                int middleX = Mathf.RoundToInt(
                    (startEdgeX + endEdgeX) * .5f);
                AddHorizontalTunnel(
                    connector,
                    Mathf.Min(startEdgeX, middleX),
                    Mathf.Max(startEdgeX, middleX),
                    fromFloorY,
                    corridorHeight);
                AddHorizontalTunnel(
                    connector,
                    Mathf.Min(middleX, endEdgeX),
                    Mathf.Max(middleX, endEdgeX),
                    toFloorY,
                    corridorHeight);
                AddVerticalTunnel(
                    connector,
                    middleX,
                    fromFloorY,
                    toFloorY,
                    corridorHeight);
            }

            document.rooms.Add(connector);
        }

        private static void AddHorizontalTunnel(
            PlanningRoom connector,
            int startX,
            int endX,
            int floorY,
            int height)
        {
            int width = Mathf.Max(1, endX - startX + 1);
            AddGlobalPatch(
                connector,
                startX,
                floorY,
                width,
                1);
            AddGlobalPatch(
                connector,
                startX,
                floorY - height,
                width,
                1);
        }

        private static void AddVerticalTunnel(
            PlanningRoom connector,
            int x,
            int firstFloorY,
            int secondFloorY,
            int width)
        {
            int top = Mathf.Min(firstFloorY, secondFloorY) - width;
            int bottom = Mathf.Max(firstFloorY, secondFloorY);
            AddGlobalPatch(
                connector,
                x,
                top,
                1,
                bottom - top + 1);
            AddGlobalPatch(
                connector,
                x + width - 1,
                top,
                1,
                bottom - top + 1);
        }

        private static void AddGlobalPatch(
            PlanningRoom connector,
            int x,
            int y,
            int width,
            int height)
        {
            AddBox(
                connector,
                PlanningDetailType.Solid,
                x,
                y,
                width,
                height,
                "通道桥体",
                "黑方块");
        }

        private static void GetSampleStride(
            IReadOnlyList<PlanningRoom> rooms,
            out int strideX,
            out int strideY)
        {
            int maxWidth = 1;
            int maxHeight = 1;
            for (int index = 0; index < rooms.Count; index++)
            {
                PlanningRoom room = rooms[index];
                RectInt rect = GetSampleRoomRect(room, 1, 1);
                maxWidth = Mathf.Max(maxWidth, rect.width);
                maxHeight = Mathf.Max(maxHeight, rect.height);
            }

            strideX = maxWidth + 6;
            strideY = maxHeight + 6;
        }

        private static RectInt GetSampleRoomRect(
            PlanningRoom room,
            int strideX,
            int strideY)
        {
            int maxX = 1;
            int maxY = 1;
            for (int index = 0; index < room.boxes.Count; index++)
            {
                PlanningBox box = room.boxes[index];
                maxX = Mathf.Max(maxX, box.x + box.width);
                maxY = Mathf.Max(maxY, box.y + box.height);
            }

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

            return new RectInt(
                minCellX * strideX,
                minCellY * strideY,
                maxX,
                maxY);
        }

        private static void AppendConnectorPath(
            List<PlanningCell> path,
            Vector2Int start,
            Vector2Int end)
        {
            int x = start.x;
            int y = start.y;
            path.Add(new PlanningCell(x, y));
            while (x != end.x)
            {
                x += x < end.x ? 1 : -1;
                path.Add(new PlanningCell(x, y));
            }

            while (y != end.y)
            {
                y += y < end.y ? 1 : -1;
                path.Add(new PlanningCell(x, y));
            }
        }

        private static string DefaultPaletteEntryName(
            PlanningDetailType type)
        {
            switch (type)
            {
                case PlanningDetailType.Solid:
                    return "黑方块";
                case PlanningDetailType.Platform:
                    return "白方块";
                case PlanningDetailType.Hazard:
                    return "能源方块";
                case PlanningDetailType.Water:
                    return "蓝方块";
                case PlanningDetailType.Ladder:
                    return "绿方块";
                case PlanningDetailType.Exit:
                    return "黑方块";
                case PlanningDetailType.Item:
                case PlanningDetailType.Enemy:
                    return "能源方块";
                default:
                    return "白方块";
            }
        }

        public PlanningRoom FindRoom(string roomId)
        {
            return rooms.Find(room => room.id == roomId);
        }

        public bool TryGetPlayerStartWorldPosition(
            out Vector3 position)
        {
            position = default;
            PlanningRoom startRoom = FindRoom(playerStartRoomId);
            if (startRoom == null || startRoom.isConnector)
            {
                return false;
            }

            PlanningLayoutUtility.GetStride(
                rooms,
                worldBlockCellWidth,
                worldBlockCellHeight,
                out int strideX,
                out int strideY);
            PlanningLayoutUtility.RoomLayoutInfo layout =
                PlanningLayoutUtility.GetRoomLayout(
                    startRoom,
                    strideX,
                    strideY);
            float globalX = layout.OffsetX + playerStartLocal.x;
            float globalY = layout.OffsetY + playerStartLocal.y;
            position = new Vector3(globalX, -globalY, 0f);
            return true;
        }

        public PlanningKey FindKey(string keyId)
        {
            return keys.Find(key => key.id == keyId);
        }

        public PlanningLock FindLock(string lockId)
        {
            return locks.Find(item => item.id == lockId);
        }

        public PlanningRoom FindRoomAt(int x, int y)
        {
            for (int roomIndex = 0;
                 roomIndex < rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = rooms[roomIndex];
                if (room.isConnector)
                {
                    continue;
                }

                for (int cellIndex = 0;
                     cellIndex < room.cells.Count;
                     cellIndex++)
                {
                    PlanningCell cell = room.cells[cellIndex];
                    if (cell.x == x && cell.y == y)
                    {
                        return room;
                    }
                }
            }

            return null;
        }

        public PlanningRoom FindConnectorAt(int x, int y)
        {
            for (int roomIndex = 0;
                 roomIndex < rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = rooms[roomIndex];
                if (!room.isConnector)
                {
                    continue;
                }

                for (int cellIndex = 0;
                     cellIndex < room.cells.Count;
                     cellIndex++)
                {
                    PlanningCell cell = room.cells[cellIndex];
                    if (cell.x == x && cell.y == y)
                    {
                        return room;
                    }
                }
            }

            return null;
        }

        public void Normalize()
        {
            rooms ??= new List<PlanningRoom>();
            regions ??= new List<PlanningRegion>();
            worldBlockCellWidth = Mathf.Clamp(
                worldBlockCellWidth,
                1,
                64);
            worldBlockCellHeight = Mathf.Clamp(
                worldBlockCellHeight,
                1,
                64);
            for (int index = 0; index < rooms.Count; index++)
            {
                PlanningRoom room = rooms[index];
                room.cells ??= new List<PlanningCell>();
                room.boxes ??= new List<PlanningBox>();
                if (room.isConnector)
                {
                    room.connectorWidth = Mathf.Clamp(
                        room.connectorWidth,
                        1,
                        12);
                }
            }

            doors ??= new List<PlanningDoor>();
            keys ??= new List<PlanningKey>();
            locks ??= new List<PlanningLock>();
            assemblyPatches ??= new List<PlanningBox>();
            RefreshConnectorPaths();
        }

        public void RefreshConnectorPaths()
        {
            PlanningWorldUtility.RefreshConnectors(this);
        }

        public static void SetConnectorPorts(
            PlanningRoom connector,
            PlanningRoom from,
            PlanningRoom to,
            PlanningCell fromHint,
            PlanningCell toHint)
        {
            if (connector == null || from == null || to == null)
            {
                return;
            }

            RectInt fromBounds = GetCellBounds(from);
            RectInt toBounds = GetCellBounds(to);
            int deltaX = Mathf.RoundToInt(
                toBounds.center.x - fromBounds.center.x);
            int deltaY = Mathf.RoundToInt(
                toBounds.center.y - fromBounds.center.y);
            if (Mathf.Abs(deltaX) >= Mathf.Abs(deltaY))
            {
                connector.fromSide = deltaX >= 0
                    ? PlanningPortSide.Right
                    : PlanningPortSide.Left;
                connector.toSide = deltaX >= 0
                    ? PlanningPortSide.Left
                    : PlanningPortSide.Right;
                connector.fromOffset = GetOffset(
                    fromHint.y,
                    fromBounds.y,
                    fromBounds.height);
                connector.toOffset = GetOffset(
                    toHint.y,
                    toBounds.y,
                    toBounds.height);
            }
            else
            {
                connector.fromSide = deltaY >= 0
                    ? PlanningPortSide.Bottom
                    : PlanningPortSide.Top;
                connector.toSide = deltaY >= 0
                    ? PlanningPortSide.Top
                    : PlanningPortSide.Bottom;
                connector.fromOffset = GetOffset(
                    fromHint.x,
                    fromBounds.x,
                    fromBounds.width);
                connector.toOffset = GetOffset(
                    toHint.x,
                    toBounds.x,
                    toBounds.width);
            }
        }

        private static PlanningCell GetPortCell(
            PlanningRoom room,
            PlanningPortSide side,
            float offset)
        {
            RectInt bounds = GetCellBounds(room);
            float safeOffset = Mathf.Clamp01(offset);
            switch (side)
            {
                case PlanningPortSide.Left:
                    return new PlanningCell(
                        bounds.xMin - 1,
                        bounds.yMin + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, bounds.height - 1)),
                            0,
                            Mathf.Max(0, bounds.height - 1)));
                case PlanningPortSide.Right:
                    return new PlanningCell(
                        bounds.xMax,
                        bounds.yMin + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, bounds.height - 1)),
                            0,
                            Mathf.Max(0, bounds.height - 1)));
                case PlanningPortSide.Top:
                    return new PlanningCell(
                        bounds.xMin + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, bounds.width - 1)),
                            0,
                            Mathf.Max(0, bounds.width - 1)),
                        bounds.yMin - 1);
                default:
                    return new PlanningCell(
                        bounds.xMin + Mathf.Clamp(
                            Mathf.RoundToInt(
                                safeOffset * Mathf.Max(0, bounds.width - 1)),
                            0,
                            Mathf.Max(0, bounds.width - 1)),
                        bounds.yMax);
            }
        }

        private static RectInt GetCellBounds(PlanningRoom room)
        {
            if (room.cells.Count == 0)
            {
                return new RectInt(0, 0, 1, 1);
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int index = 0; index < room.cells.Count; index++)
            {
                PlanningCell cell = room.cells[index];
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new RectInt(
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1);
        }

        private static float GetOffset(
            int value,
            int start,
            int length)
        {
            return length <= 1
                ? .5f
                : Mathf.Clamp01(
                    (value - start + .5f) / length);
        }

        private static void AppendPath(
            List<PlanningCell> path,
            PlanningCell start,
            PlanningCell end)
        {
            int x = start.x;
            int y = start.y;
            path.Add(new PlanningCell(x, y));
            while (x != end.x)
            {
                x += x < end.x ? 1 : -1;
                path.Add(new PlanningCell(x, y));
            }

            while (y != end.y)
            {
                y += y < end.y ? 1 : -1;
                path.Add(new PlanningCell(x, y));
            }
        }
    }
}
