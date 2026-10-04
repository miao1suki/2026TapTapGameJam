using System;
using System.Collections.Generic;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public enum PlanningCanvasMode
    {
        World,
        Detail,
        Assembly
    }

    public sealed class PlanningCanvas : VisualElement
    {
        private const float BaseGridSize = 26f;
        private const float MinimumZoom = .08f;
        private const float MaximumZoom = 3f;

        private Color backgroundColor = new Color(.11f, .13f, .17f);
        private PlanningDocument document;
        private PlanningCanvasMode mode;
        private PlanningMapTool mapTool = PlanningMapTool.Paint;
        private PlanningDetailTool detailTool = PlanningDetailTool.Box;
        private PlanningDetailType detailType = PlanningDetailType.Platform;
        private string detailEntryId;
        private string detailEntryName;
        private string detailPropEntryId;
        private string detailPropEntryName;
        private LevelEditorPalette palette;
        private string selectedRoomId;
        private string selectedBoxId;
        private Vector2 pan = new Vector2(-90f, -90f);
        private float zoom = 1f;
        private bool isPanning;
        private int panPointerId = -1;
        private Vector2 panLastPosition;
        private bool isDragging;
        private int dragPointerId = -1;
        private Vector2 dragStart;
        private Vector2 dragCurrent;
        private PlanningBox movingBox;
        private Vector2 movingBoxStart;
        private Vector2Int movingBoxOrigin;
        private Vector2Int lastPaintCell = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2Int assemblyStrokeStart;
        private Vector2Int assemblyStrokeEnd;
        private bool suppressDocumentChanged;
        private bool pendingDocumentChanged;
        private bool hasConnectorStart;
        private Vector2Int connectorStartCell;
        private bool isPlacingRoom;

        internal int LastUnassignedSceneBlockCount { get; private set; }
        private bool hasHover;
        private Vector2Int hoveredCell;
        private readonly Dictionary<string, Image> detailPreviewImages =
            new Dictionary<string, Image>();

        public event Action DocumentChanged;
        public event Action SelectionChanged;
        public event Action<string> ValidationFailed;
        public event Action<string> StatusChanged;

        public PlanningDocument Document => document;
        public PlanningCanvasMode Mode => mode;
        public PlanningMapTool MapTool => mapTool;
        public PlanningDetailTool DetailTool => detailTool;
        public PlanningDetailType DetailType => detailType;
        public string DetailEntryId => detailEntryId;
        public string DetailEntryName => detailEntryName;
        public string DetailPropEntryId => detailPropEntryId;
        public string DetailPropEntryName => detailPropEntryName;
        public string SelectedRoomId => selectedRoomId;
        public string SelectedBoxId => selectedBoxId;
        public float Zoom => zoom;

        public bool IsPlacingRoom => isPlacingRoom;

        private int WorldBlockCellWidth =>
            document != null ? document.worldBlockCellWidth : 16;

        private int WorldBlockCellHeight =>
            document != null ? document.worldBlockCellHeight : 16;

        public bool HasPlanningSelection()
        {
            return FindSelectedBox(out _, out _);
        }

        public bool ReplaceSelectedPlanningBox()
        {
            if (!FindSelectedBox(
                    out List<PlanningBox> boxes,
                    out PlanningBox box))
            {
                return false;
            }

            ApplyDetailSelection(box);
            NotifyDocumentChanged();
            SelectionChanged?.Invoke();
            return true;
        }

        public bool DeleteSelectedPlanningBox()
        {
            if (!FindSelectedBox(
                    out List<PlanningBox> boxes,
                    out PlanningBox box))
            {
                return false;
            }

            boxes.Remove(box);
            selectedBoxId = null;
            NotifyDocumentChanged();
            SelectionChanged?.Invoke();
            return true;
        }

        internal bool SyncAssemblyFromScene()
        {
            LastUnassignedSceneBlockCount = 0;
            if (document == null)
            {
                return false;
            }

            GetAssemblyStride(out int strideX, out int strideY);
            int syncedRooms = 0;
            var assignedBlocks = new HashSet<int>();
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = document.rooms[roomIndex];
                if (room.isConnector)
                {
                    continue;
                }

                GameObject parent =
                    PlanningSceneBuilder.FindRoomContainer(room);
                if (parent == null)
                {
                    continue;
                }

                LevelEditorPlacedBlock[] blocks =
                    parent.GetComponentsInChildren<
                        LevelEditorPlacedBlock>(true);
                if (blocks.Length == 0)
                {
                    continue;
                }

                GetAssemblyRoomOffset(
                    room,
                    strideX,
                    strideY,
                    out int offsetX,
                    out int offsetY,
                    out _);
                var syncedBoxes = new List<PlanningBox>();
                for (int blockIndex = 0;
                     blockIndex < blocks.Length;
                     blockIndex++)
                {
                    LevelEditorPlacedBlock block = blocks[blockIndex];
                    assignedBlocks.Add(block.GetInstanceID());
                    int globalPlanX = block.Cell.x;
                    int globalPlanY = -block.Cell.y - 1;
                    var box = new PlanningBox(
                        block.IsProp
                            ? PlanningDetailType.Prop
                            : PlanningDetailType.Solid,
                        new RectInt(
                            globalPlanX - offsetX,
                            globalPlanY - offsetY,
                            1,
                            1))
                    {
                        label = block.EntryName,
                        paletteEntryName = block.IsProp
                            ? string.Empty
                            : block.EntryName,
                        propEntryName = block.IsProp
                            ? block.EntryName
                            : string.Empty
                    };
                    syncedBoxes.Add(box);
                }

                if (BoxesMatch(room.boxes, syncedBoxes))
                {
                    continue;
                }

                room.boxes.Clear();
                room.boxes.AddRange(syncedBoxes);
                syncedRooms++;
            }

            LevelEditorPlacedBlock[] allBlocks =
                UnityEngine.Object.FindObjectsByType<LevelEditorPlacedBlock>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < allBlocks.Length; index++)
            {
                if (!assignedBlocks.Contains(allBlocks[index].GetInstanceID()))
                {
                    LastUnassignedSceneBlockCount++;
                }
            }

            if (syncedRooms > 0)
            {
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
                return true;
            }

            return false;
        }

        private static bool BoxesMatch(
            IReadOnlyList<PlanningBox> first,
            IReadOnlyList<PlanningBox> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Count; index++)
            {
                PlanningBox a = first[index];
                PlanningBox b = second[index];
                if (a.x != b.x ||
                    a.y != b.y ||
                    a.width != b.width ||
                    a.height != b.height ||
                    a.paletteEntryName != b.paletteEntryName)
                {
                    return false;
                }
            }

            return true;
        }

        private bool FindSelectedBox(
            out List<PlanningBox> ownerBoxes,
            out PlanningBox selected)
        {
            ownerBoxes = null;
            selected = null;
            if (document == null || string.IsNullOrEmpty(selectedBoxId))
            {
                return false;
            }

            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = document.rooms[roomIndex];
                for (int boxIndex = 0;
                     boxIndex < room.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = room.boxes[boxIndex];
                    if (box.id == selectedBoxId)
                    {
                        ownerBoxes = room.boxes;
                        selected = box;
                        return true;
                    }
                }
            }

            for (int index = 0;
                 index < document.assemblyPatches.Count;
                 index++)
            {
                PlanningBox box = document.assemblyPatches[index];
                if (box.id == selectedBoxId)
                {
                    ownerBoxes = document.assemblyPatches;
                    selected = box;
                    return true;
                }
            }

            return false;
        }

        public void SetBackgroundColor(Color value)
        {
            backgroundColor = value;
            style.backgroundColor = value;
            MarkDirtyRepaint();
        }

        public PlanningCanvas()
        {
            focusable = true;
            pickingMode = PickingMode.Position;
            style.flexGrow = 1f;
            style.backgroundColor = backgroundColor;
            generateVisualContent += Draw;
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(_ => EndPointerAction());
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<GeometryChangedEvent>(
                _ => UpdateDetailPreviewImages());
        }

        public void SetDocument(PlanningDocument value)
        {
            document = value;
            document?.Normalize();
            selectedRoomId = document != null && document.rooms.Count > 0
                ? document.rooms[0].id
                : null;
            selectedBoxId = null;
            pan = new Vector2(-90f, -90f);
            zoom = 1f;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
            SelectionChanged?.Invoke();
        }

        public void BeginRoomPlacement()
        {
            isPlacingRoom = true;
            hasConnectorStart = false;
            MarkDirtyRepaint();
            StatusChanged?.Invoke(
                "请点击世界图空白格设置新房间初始点，右键或 Esc 取消。");
        }

        public void CancelRoomPlacement()
        {
            if (!isPlacingRoom)
            {
                return;
            }

            isPlacingRoom = false;
            MarkDirtyRepaint();
            StatusChanged?.Invoke("已取消新增房间。");
        }

        private bool TryPlaceNewRoom(Vector2Int cell)
        {
            if (document.FindRoomAt(cell.x, cell.y) != null ||
                document.FindConnectorAt(cell.x, cell.y) != null)
            {
                StatusChanged?.Invoke(
                    "这个位置已经有房间或通道，请选择空白格。");
                return false;
            }

            var room = new PlanningRoom(
                "房间 " + (document.rooms.Count + 1));
            room.cells.Add(new PlanningCell(cell.x, cell.y));
            document.rooms.Add(room);
            selectedRoomId = room.id;
            selectedBoxId = null;
            isPlacingRoom = false;
            NotifyDocumentChanged();
            SelectionChanged?.Invoke();
            StatusChanged?.Invoke(
                $"已创建房间初始点：{room.name}");
            return true;
        }

        public void SetMode(PlanningCanvasMode value)
        {
            mode = value;
            selectedBoxId = null;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
            SelectionChanged?.Invoke();
        }

        public void SetMapTool(PlanningMapTool value)
        {
            mapTool = value;
            isDragging = false;
            hasConnectorStart = false;
            lastPaintCell = new Vector2Int(int.MinValue, int.MinValue);
            MarkDirtyRepaint();
        }

        public void SetDetailTool(PlanningDetailTool value)
        {
            detailTool = value;
            isDragging = false;
            lastPaintCell = new Vector2Int(int.MinValue, int.MinValue);
            MarkDirtyRepaint();
        }

        private static PlanningDetailTool GetActiveDetailTool()
        {
            switch (LevelEditorState.Tool)
            {
                case LevelEditorTool.Select:
                    return PlanningDetailTool.Select;
                case LevelEditorTool.Erase:
                    return PlanningDetailTool.Erase;
                case LevelEditorTool.Paint:
                    return PlanningDetailTool.Box;
                default:
                    return PlanningDetailTool.Pan;
            }
        }

        public void SetDetailType(PlanningDetailType value)
        {
            detailType = value;
            MarkDirtyRepaint();
        }

        public void SetDetailEntry(string entryId, string entryName)
        {
            detailEntryId = entryId;
            detailEntryName = entryName;
            detailPropEntryId = string.Empty;
            detailPropEntryName = string.Empty;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        public void SetDetailProp(string entryId, string entryName)
        {
            detailPropEntryId = entryId;
            detailPropEntryName = entryName;
            detailEntryId = string.Empty;
            detailEntryName = string.Empty;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        public void SetPalette(LevelEditorPalette value)
        {
            palette = value;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        private void BeginDocumentBatch()
        {
            suppressDocumentChanged = true;
            pendingDocumentChanged = false;
        }

        private void EndDocumentBatch()
        {
            suppressDocumentChanged = false;
            if (pendingDocumentChanged)
            {
                pendingDocumentChanged = false;
                NotifyDocumentChanged();
            }
        }

        public void SelectRoom(string roomId, bool focus = false)
        {
            selectedRoomId = roomId;
            selectedBoxId = null;
            if (focus)
            {
                FocusRoom(roomId);
            }

            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
            SelectionChanged?.Invoke();
        }

        public void FocusRoom(string roomId)
        {
            PlanningRoom room = document?.FindRoom(roomId);
            if (room == null)
            {
                return;
            }

            if (mode != PlanningCanvasMode.Detail)
            {
                RectInt worldBounds = GetWorldCellBounds(room);
                CenterOn(
                    worldBounds.xMin + worldBounds.width * .5f,
                    worldBounds.yMin + worldBounds.height * .5f);
                MarkDirtyRepaint();
                return;
            }

            HashSet<Vector2Int> cells = BuildDetailCells(room);
            if (cells.Count == 0)
            {
                return;
            }

            RectInt bounds = GetCellSetBounds(cells);
            Vector2 focus = CalculateDensestCenter(cells, bounds);
            ApplyDetailView(bounds, focus);
            MarkDirtyRepaint();
        }

        private HashSet<Vector2Int> BuildDetailCells(PlanningRoom room)
        {
            var cells = new HashSet<Vector2Int>();
            if (room.boxes.Count > 0)
            {
                for (int index = 0; index < room.boxes.Count; index++)
                {
                    PlanningBox box = room.boxes[index];
                    for (int y = box.y; y < box.y + box.height; y++)
                    {
                        for (int x = box.x; x < box.x + box.width; x++)
                        {
                            cells.Add(new Vector2Int(x, y));
                        }
                    }
                }

                return cells;
            }

            PlanningCell origin = room.isConnector
                ? GetConnectorOrigin(room)
                : new PlanningCell(0, 0);
            for (int index = 0; index < room.cells.Count; index++)
            {
                PlanningCell cell = room.cells[index];
                cells.Add(new Vector2Int(
                    cell.x - origin.x,
                    cell.y - origin.y));
            }

            return cells;
        }

        private static RectInt GetCellSetBounds(
            HashSet<Vector2Int> cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            foreach (Vector2Int cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x + 1);
                maxY = Mathf.Max(maxY, cell.y + 1);
            }

            return new RectInt(
                minX,
                minY,
                Mathf.Max(1, maxX - minX),
                Mathf.Max(1, maxY - minY));
        }

        private static Vector2 CalculateDensestCenter(
            HashSet<Vector2Int> cells,
            RectInt bounds)
        {
            Vector2 fallback = new Vector2(
                bounds.xMin + bounds.width * .5f,
                bounds.yMin + bounds.height * .5f);
            int radius = Mathf.Clamp(
                Mathf.Max(bounds.width, bounds.height) / 10,
                2,
                10);
            int bestCount = -1;
            float bestDistance = float.MaxValue;
            Vector2 bestCenter = fallback;
            foreach (Vector2Int anchor in cells)
            {
                int count = 0;
                for (int y = anchor.y - radius;
                     y <= anchor.y + radius;
                     y++)
                {
                    for (int x = anchor.x - radius;
                         x <= anchor.x + radius;
                         x++)
                    {
                        if (cells.Contains(new Vector2Int(x, y)))
                        {
                            count++;
                        }
                    }
                }

                Vector2 candidate = new Vector2(
                    anchor.x + .5f,
                    anchor.y + .5f);
                float distance = (candidate - fallback).sqrMagnitude;
                if (count > bestCount ||
                    (count == bestCount &&
                     distance < bestDistance))
                {
                    bestCount = count;
                    bestDistance = distance;
                    bestCenter = candidate;
                }
            }

            return bestCount == cells.Count
                ? fallback
                : bestCenter;
        }

        private void ApplyDetailView(
            RectInt bounds,
            Vector2 focus)
        {
            float viewportWidth = Mathf.Max(
                160f,
                contentRect.width - 36f);
            float viewportHeight = Mathf.Max(
                160f,
                contentRect.height - 36f);
            float fitX = viewportWidth /
                         (Mathf.Max(1, bounds.width) * BaseGridSize);
            float fitY = viewportHeight /
                         (Mathf.Max(1, bounds.height) * BaseGridSize);
            zoom = Mathf.Clamp(
                Mathf.Min(fitX, fitY),
                MinimumZoom,
                Mathf.Min(MaximumZoom, 2f));
            CenterOn(focus.x, focus.y);
        }

        public void FocusWorld()
        {
            if (document == null || document.rooms.Count == 0)
            {
                return;
            }

            float sumX = 0f;
            float sumY = 0f;
            int count = 0;
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = document.rooms[roomIndex];
                for (int cellIndex = 0;
                     cellIndex < room.cells.Count;
                     cellIndex++)
                {
                    PlanningCell cell = room.cells[cellIndex];
                    sumX += cell.x + .5f;
                    sumY += cell.y + .5f;
                    count++;
                }
            }

            for (int index = 0; index < document.keys.Count; index++)
            {
                PlanningKey key = document.keys[index];
                sumX += key.x + .5f;
                sumY += key.y + .5f;
                count++;
            }

            if (count > 0)
            {
                CenterOn(sumX / count, sumY / count);
                MarkDirtyRepaint();
            }
        }

        public void FocusAssembly()
        {
            if (document == null)
            {
                return;
            }

            GetAssemblyStride(out int strideX, out int strideY);
            float sumX = 0f;
            float sumY = 0f;
            int count = 0;
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = document.rooms[roomIndex];
                if (room.isConnector)
                {
                    GetAssemblyConnectorOrigin(
                        room,
                        strideX,
                        strideY,
                        out int originX,
                        out int originY);
                    if (room.boxes.Count == 0)
                    {
                        PlanningCell origin = GetConnectorOrigin(room);
                        for (int cellIndex = 0;
                             cellIndex < room.cells.Count;
                             cellIndex++)
                        {
                            PlanningCell cell = room.cells[cellIndex];
                            sumX += originX + cell.x - origin.x + .5f;
                            sumY += originY + cell.y - origin.y + .5f;
                            count++;
                        }
                    }
                    else
                    {
                        AccumulateBoxCells(
                            room.boxes,
                            originX,
                            originY,
                            ref sumX,
                            ref sumY,
                            ref count);
                    }

                    continue;
                }

                GetAssemblyRoomOffset(
                    room,
                    strideX,
                    strideY,
                    out int offsetX,
                    out int offsetY,
                    out _);
                AccumulateBoxCells(
                    room.boxes,
                    offsetX,
                    offsetY,
                    ref sumX,
                    ref sumY,
                    ref count);
            }

            for (int index = 0;
                 index < document.assemblyPatches.Count;
                 index++)
            {
                PlanningBox box = document.assemblyPatches[index];
                AccumulateBoxCells(
                    new[] { box },
                    0,
                    0,
                    ref sumX,
                    ref sumY,
                    ref count);
            }

            if (count > 0)
            {
                CenterOn(sumX / count, sumY / count);
                MarkDirtyRepaint();
            }
        }

        private static void AccumulateBoxCells(
            IReadOnlyList<PlanningBox> boxes,
            int offsetX,
            int offsetY,
            ref float sumX,
            ref float sumY,
            ref int count)
        {
            for (int boxIndex = 0;
                 boxIndex < boxes.Count;
                 boxIndex++)
            {
                PlanningBox box = boxes[boxIndex];
                for (int y = box.y;
                     y < box.y + box.height;
                     y++)
                {
                    for (int x = box.x;
                         x < box.x + box.width;
                         x++)
                    {
                        sumX += offsetX + x + .5f;
                        sumY += offsetY + y + .5f;
                        count++;
                    }
                }
            }
        }

        private void CenterOn(float centerX, float centerY)
        {
            pan = new Vector2(
                contentRect.width * .5f -
                centerX * GridSize * zoom,
                contentRect.height * .5f -
                centerY * GridSize * zoom);
        }

        private float GridSize => BaseGridSize * zoom;

        private Vector2 ScreenToWorld(Vector2 position)
        {
            return (position - pan) / GridSize;
        }

        private Vector2 WorldToScreen(Vector2 position)
        {
            return position * GridSize + pan;
        }

        private Vector2Int CellAt(Vector2 position)
        {
            Vector2 world = ScreenToWorld(position);
            return new Vector2Int(
                Mathf.FloorToInt(world.x),
                Mathf.FloorToInt(world.y));
        }

        private void Draw(MeshGenerationContext context)
        {
            if (document == null)
            {
                return;
            }

            if (mode == PlanningCanvasMode.World)
            {
                DrawWorld(context);
            }
            else if (mode == PlanningCanvasMode.Assembly)
            {
                DrawAssembly(context);
            }
            else
            {
                DrawDetail(context);
            }
        }

        private void DrawAssembly(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            DrawGrid(painter);
            GetAssemblyStride(out int strideX, out int strideY);

            for (int index = 0; index < document.rooms.Count; index++)
            {
                PlanningRoom owner = document.rooms[index];
                if (owner.isConnector)
                {
                    continue;
                }

                GetAssemblyRoomOffset(
                    owner,
                    strideX,
                    strideY,
                    out int offsetX,
                    out int offsetY,
                    out int maxPlanY);
                for (int boxIndex = 0;
                     boxIndex < owner.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = owner.boxes[boxIndex];
                    Rect rect = CellRect(
                        offsetX + box.x,
                        offsetY + box.y,
                        box.width,
                        box.height);
                    FillRect(painter, rect, ColorForBox(box), .92f);
                }
            }

            for (int index = 0; index < document.rooms.Count; index++)
            {
                PlanningRoom connector = document.rooms[index];
                if (!connector.isConnector)
                {
                    continue;
                }

                GetAssemblyConnectorOrigin(
                    connector,
                    strideX,
                    strideY,
                    out int originX,
                    out int originY);
                List<Vector2Int> assemblyPath =
                    PlanningLayoutUtility.GetConnectorAssemblyPath(
                        document.rooms,
                        connector,
                        WorldBlockCellWidth,
                        WorldBlockCellHeight);
                if (connector.boxes.Count == 0)
                {
                    float width =
                        2f +
                        GridSize *
                        Mathf.Clamp(connector.connectorWidth, 1, 12) *
                        .28f;
                    for (int cellIndex = 1;
                         cellIndex < assemblyPath.Count;
                         cellIndex++)
                    {
                        Vector2Int previous =
                            assemblyPath[cellIndex - 1];
                        Vector2Int current =
                            assemblyPath[cellIndex];
                        DrawLine(
                            painter,
                            CellCenter(previous.x, previous.y),
                            CellCenter(current.x, current.y),
                            new Color(.16f, .64f, .58f, .72f),
                            width);
                    }
                }

                for (int boxIndex = 0;
                     boxIndex < connector.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = connector.boxes[boxIndex];
                    Rect rect = CellRect(
                        originX + box.x,
                        originY + box.y,
                        box.width,
                        box.height);
                    FillRect(painter, rect, ColorForBox(box), .92f);
                }
            }

            for (int index = 0;
                 index < document.assemblyPatches.Count;
                 index++)
            {
                PlanningBox box = document.assemblyPatches[index];
                Rect rect = CellRect(
                    box.x,
                    box.y,
                    box.width,
                    box.height);
                FillRect(painter, rect, ColorForBox(box), .92f);
                StrokeRect(
                    painter,
                    rect,
                    new Color(1f, .85f, .35f, .8f),
                    1f);
            }

            DrawGrid(painter, .16f);
            DrawRectanglePreview(painter);
            DrawHoverCell(painter);
        }

        private void GetAssemblyStride(
            out int strideX,
            out int strideY)
        {
            PlanningLayoutUtility.GetStride(
                document.rooms,
                WorldBlockCellWidth,
                WorldBlockCellHeight,
                out strideX,
                out strideY);
        }

        private static void GetAssemblyRoomOffset(
            PlanningRoom room,
            int strideX,
            int strideY,
            out int offsetX,
            out int offsetY,
            out int maxPlanY)
        {
            PlanningLayoutUtility.RoomLayoutInfo layout =
                PlanningLayoutUtility.GetRoomLayout(
                    room,
                    strideX,
                    strideY);
            offsetX = layout.OffsetX;
            offsetY = layout.OffsetY;
            maxPlanY = layout.MaxPlanY;
        }

        private void GetAssemblyConnectorOrigin(
            PlanningRoom connector,
            int strideX,
            int strideY,
            out int originX,
            out int originY)
        {
            List<Vector2Int> path =
                PlanningLayoutUtility.GetConnectorAssemblyPath(
                    document.rooms,
                    connector,
                    WorldBlockCellWidth,
                    WorldBlockCellHeight);
            if (path.Count > 0)
            {
                originX = path[0].x;
                originY = path[0].y;
                return;
            }

            originX = 0;
            originY = 0;
        }

        private static PlanningCell GetConnectorOrigin(
            PlanningRoom connector)
        {
            return PlanningLayoutUtility.GetConnectorLocalOrigin(
                connector);
        }

        private RectInt GetConnectorDetailBounds(
            PlanningRoom connector)
        {
            if (connector.detailBoundsWidth > 0 &&
                connector.detailBoundsHeight > 0)
            {
                return new RectInt(
                    connector.detailBoundsX,
                    connector.detailBoundsY,
                    connector.detailBoundsWidth,
                    connector.detailBoundsHeight);
            }

            if (connector.boxes.Count > 0)
            {
                int minX = int.MaxValue;
                int minY = int.MaxValue;
                int maxX = int.MinValue;
                int maxY = int.MinValue;
                for (int index = 0;
                     index < connector.boxes.Count;
                     index++)
                {
                    PlanningBox box = connector.boxes[index];
                    minX = Mathf.Min(minX, box.x);
                    minY = Mathf.Min(minY, box.y);
                    maxX = Mathf.Max(maxX, box.x + box.width);
                    maxY = Mathf.Max(maxY, box.y + box.height);
                }

                int boxPadding = Mathf.Max(
                    1,
                    Mathf.CeilToInt(connector.connectorWidth * .5f));
                return new RectInt(
                    minX - boxPadding,
                    minY - boxPadding,
                    maxX - minX + boxPadding * 2,
                    maxY - minY + boxPadding * 2);
            }

            PlanningCell origin = GetConnectorOrigin(connector);
            int minCellX = int.MaxValue;
            int minCellY = int.MaxValue;
            int maxCellX = int.MinValue;
            int maxCellY = int.MinValue;
            for (int index = 0;
                 index < connector.cells.Count;
                 index++)
            {
                PlanningCell cell = connector.cells[index];
                minCellX = Mathf.Min(minCellX, cell.x - origin.x);
                minCellY = Mathf.Min(minCellY, cell.y - origin.y);
                maxCellX = Mathf.Max(
                    maxCellX,
                    cell.x - origin.x + 1);
                maxCellY = Mathf.Max(
                    maxCellY,
                    cell.y - origin.y + 1);
            }

            if (minCellX == int.MaxValue)
            {
                return new RectInt(0, 0, 1, 1);
            }

            int padding = Mathf.Max(
                1,
                Mathf.CeilToInt(connector.connectorWidth * .5f));
            return new RectInt(
                minCellX - padding,
                minCellY - padding,
                maxCellX - minCellX + padding * 2,
                maxCellY - minCellY + padding * 2);
        }

        private void DrawWorld(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            DrawGrid(painter);
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = document.rooms[roomIndex];
                if (room.isConnector)
                {
                    continue;
                }

                Color color = Color.HSVToRGB(
                    (roomIndex * .137f) % 1f,
                    .48f,
                    .78f);
                color.a = room.id == selectedRoomId ? .78f : .5f;
                for (int cellIndex = 0;
                     cellIndex < room.cells.Count;
                     cellIndex++)
                {
                    PlanningCell cell = room.cells[cellIndex];
                    Rect rect = CellRect(cell.x, cell.y);
                    FillRect(painter, rect, color, 1f);
                    StrokeRect(
                        painter,
                        rect,
                        room.id == selectedRoomId
                            ? Color.white
                            : new Color(0f, 0f, 0f, .55f),
                        room.id == selectedRoomId ? 2f : 1f);
                }
            }

            DrawConnectors(painter);
            DrawConnectorStart(painter);
            DrawDoors(painter);
            DrawKeys(painter);
            DrawRectanglePreview(painter);
            DrawHoverCell(painter);
            DrawRoomPlacementPreview(painter);
        }

        private void DrawRoomPlacementPreview(Painter2D painter)
        {
            if (!isPlacingRoom || !hasHover)
            {
                return;
            }

            Rect rect = CellRect(hoveredCell.x, hoveredCell.y);
            FillRect(
                painter,
                rect,
                new Color(.3f, .95f, .78f, .2f),
                1f);
            StrokeRect(
                painter,
                rect,
                new Color(.3f, 1f, .82f, .95f),
                2f);
        }

        private void DrawConnectorStart(Painter2D painter)
        {
            if (!hasConnectorStart)
            {
                return;
            }

            Rect rect = CellRect(
                connectorStartCell.x,
                connectorStartCell.y);
            FillRect(
                painter,
                rect,
                new Color(.25f, .9f, .78f, .28f),
                1f);
            StrokeRect(
                painter,
                rect,
                new Color(.35f, 1f, .88f, .95f),
                2f);
        }

        private void DrawConnectors(Painter2D painter)
        {
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom connector = document.rooms[roomIndex];
                if (!connector.isConnector)
                {
                    continue;
                }

                Color color = connector.id == selectedRoomId
                    ? new Color(.3f, .9f, .82f, .92f)
                    : new Color(.2f, .68f, .62f, .72f);
                List<Vector2> points =
                    GetWorldConnectorVisualPoints(connector);
                float width = Mathf.Clamp(
                    2f + connector.connectorWidth * 1.6f,
                    3f,
                    16f);
                for (int pointIndex = 1;
                     pointIndex < points.Count;
                     pointIndex++)
                {
                    DrawLine(
                        painter,
                        WorldToScreen(points[pointIndex - 1]),
                        WorldToScreen(points[pointIndex]),
                        color,
                        width);
                }

                if (points.Count == 1)
                {
                    DrawLine(
                        painter,
                        WorldToScreen(points[0]) -
                        new Vector2(GridSize * .18f, 0f),
                        WorldToScreen(points[0]) +
                        new Vector2(GridSize * .18f, 0f),
                        color,
                        width);
                }
            }
        }

        private List<Vector2> GetWorldConnectorVisualPoints(
            PlanningRoom connector)
        {
            var points = new List<Vector2>();
            for (int index = 0;
                 index < connector.cells.Count;
                 index++)
            {
                PlanningCell cell = connector.cells[index];
                points.Add(new Vector2(cell.x + .5f, cell.y + .5f));
            }

            return points;
        }

        private static RectInt GetWorldCellBounds(PlanningRoom room)
        {
            if (room.cells.Count == 0)
            {
                return new RectInt(0, 0, 1, 1);
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            for (int index = 0; index < room.cells.Count; index++)
            {
                PlanningCell cell = room.cells[index];
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new RectInt(
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1);
        }

        private void DrawDetail(MeshGenerationContext context)
        {
            PlanningRoom room = document?.FindRoom(selectedRoomId);
            if (room == null)
            {
                DrawEmptyHint(context);
                return;
            }

            Painter2D painter = context.painter2D;
            DrawGrid(painter);
            if (room.isConnector && room.boxes.Count == 0)
            {
                List<Vector2Int> assemblyPath =
                    PlanningLayoutUtility.GetConnectorAssemblyPath(
                        document.rooms,
                        room,
                        WorldBlockCellWidth,
                        WorldBlockCellHeight);
                if (assemblyPath.Count == 0)
                {
                    assemblyPath.Add(Vector2Int.zero);
                }

                Vector2Int origin = assemblyPath[0];
                for (int index = 1;
                     index < assemblyPath.Count;
                     index++)
                {
                    Vector2Int previous = assemblyPath[index - 1];
                    Vector2Int current = assemblyPath[index];
                    DrawLine(
                        painter,
                        CellCenter(
                            previous.x - origin.x,
                            previous.y - origin.y),
                        CellCenter(
                            current.x - origin.x,
                            current.y - origin.y),
                        new Color(.3f, .8f, .72f, .34f),
                        2f);
                }
            }

            if (!room.isConnector)
            {
                RectInt allowed = room.GetLocalAllowedRect(
                    WorldBlockCellWidth,
                    WorldBlockCellHeight);
                StrokeRect(
                    painter,
                    CellRect(
                        allowed.x,
                        allowed.y,
                        allowed.width,
                        allowed.height),
                    new Color(.4f, .78f, 1f, .55f),
                    2f);
            }
            else
            {
                RectInt allowed = GetConnectorDetailBounds(room);
                StrokeRect(
                    painter,
                    CellRect(
                        allowed.x,
                        allowed.y,
                        allowed.width,
                        allowed.height),
                    new Color(1f, .62f, .25f, .82f),
                    2f);
            }

            for (int index = 0; index < room.boxes.Count; index++)
            {
                PlanningBox box = room.boxes[index];
                Rect rect = CellRect(box.x, box.y, box.width, box.height);
                Texture2D preview = GetBoxPreview(box);
                if (preview == null)
                {
                    FillRect(
                        painter,
                        rect,
                        ColorForBox(box),
                        .82f);
                }

                if (box.id == selectedBoxId)
                {
                    StrokeRect(
                        painter,
                        rect,
                        Color.white,
                        2f);
                }
            }

            DrawRectanglePreview(painter);
            DrawHoverCell(painter);
            UpdateDetailPreviewImages();
        }

        private void UpdateDetailPreviewImages()
        {
            if (document == null)
            {
                HideDetailPreviewImages();
                return;
            }

            if (mode == PlanningCanvasMode.Assembly)
            {
                UpdateAssemblyPreviewImages();
                return;
            }

            if (mode != PlanningCanvasMode.Detail)
            {
                HideDetailPreviewImages();
                return;
            }

            PlanningRoom room = document.FindRoom(selectedRoomId);
            if (room == null)
            {
                HideDetailPreviewImages();
                return;
            }

            var active = new HashSet<string>();
            for (int boxIndex = 0;
                 boxIndex < room.boxes.Count;
                 boxIndex++)
            {
                PlanningBox box = room.boxes[boxIndex];
                Texture2D preview = GetBoxPreview(box);
                if (preview == null)
                {
                    continue;
                }

                for (int y = box.y;
                     y < box.y + box.height;
                     y++)
                {
                    for (int x = box.x;
                         x < box.x + box.width;
                         x++)
                    {
                        string key = box.id + ":" + x + ":" + y;
                        if (!detailPreviewImages.TryGetValue(
                                key,
                                out Image image))
                        {
                            image = new Image
                            {
                                pickingMode = PickingMode.Ignore,
                                scaleMode = ScaleMode.StretchToFill
                            };
                            image.style.position = Position.Absolute;
                            Add(image);
                            detailPreviewImages[key] = image;
                        }

                        Rect rect = CellRect(x, y, 1, 1);
                        image.image = preview;
                        image.style.left = rect.x;
                        image.style.top = rect.y;
                        image.style.width = rect.width;
                        image.style.height = rect.height;
                        image.style.display = DisplayStyle.Flex;
                        active.Add(key);
                    }
                }
            }

            foreach (var pair in detailPreviewImages)
            {
                if (!active.Contains(pair.Key))
                {
                    pair.Value.style.display = DisplayStyle.None;
                }
            }
        }

        private void UpdateAssemblyPreviewImages()
        {
            var active = new HashSet<string>();
            GetAssemblyStride(out int strideX, out int strideY);
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = document.rooms[roomIndex];
                if (room.isConnector)
                {
                    if (room.boxes.Count == 0)
                    {
                        continue;
                    }

                    GetAssemblyConnectorOrigin(
                        room,
                        strideX,
                        strideY,
                        out int originX,
                        out int originY);
                    for (int boxIndex = 0;
                         boxIndex < room.boxes.Count;
                         boxIndex++)
                    {
                        PlanningBox box = room.boxes[boxIndex];
                        AddPreviewImage(
                            box,
                            new RectInt(
                                originX + box.x,
                                originY + box.y,
                                box.width,
                                box.height),
                            active);
                    }

                    continue;
                }

                for (int boxIndex = 0;
                     boxIndex < room.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = room.boxes[boxIndex];
                    AddPreviewImage(
                        box,
                        GetAssemblyBoxRect(
                            room,
                            box,
                            strideX,
                            strideY),
                        active);
                }
            }

            for (int index = 0;
                 index < document.assemblyPatches.Count;
                 index++)
            {
                PlanningBox box = document.assemblyPatches[index];
                AddPreviewImage(
                    box,
                    new RectInt(
                        box.x,
                        box.y,
                        box.width,
                        box.height),
                    active);
            }

            foreach (var pair in detailPreviewImages)
            {
                if (!active.Contains(pair.Key))
                {
                    pair.Value.style.display = DisplayStyle.None;
                }
            }
        }

        private void AddPreviewImage(
            PlanningBox box,
            RectInt rect,
            HashSet<string> active)
        {
            Texture2D preview = GetBoxPreview(box);
            if (preview == null)
            {
                return;
            }

            for (int y = rect.y; y < rect.yMax; y++)
            {
                for (int x = rect.x; x < rect.xMax; x++)
                {
                    string key = box.id + ":" + x + ":" + y;
                    if (!detailPreviewImages.TryGetValue(
                            key,
                            out Image image))
                    {
                        image = new Image
                        {
                            pickingMode = PickingMode.Ignore,
                            scaleMode = ScaleMode.StretchToFill
                        };
                        image.style.position = Position.Absolute;
                        Add(image);
                        detailPreviewImages[key] = image;
                    }

                    Rect cellRect = CellRect(x, y, 1, 1);
                    image.image = preview;
                    image.style.left = cellRect.x;
                    image.style.top = cellRect.y;
                    image.style.width = cellRect.width;
                    image.style.height = cellRect.height;
                    image.style.display = DisplayStyle.Flex;
                    active.Add(key);
                }
            }
        }

        private void HideDetailPreviewImages()
        {
            foreach (var pair in detailPreviewImages)
            {
                pair.Value.style.display = DisplayStyle.None;
            }
        }

        private void DrawGrid(
            Painter2D painter,
            float opacity = 1f)
        {
            Rect visible = contentRect;
            float grid = GridSize;
            if (grid <= 2f)
            {
                return;
            }

            Vector2 topLeft = ScreenToWorld(visible.min);
            Vector2 bottomRight = ScreenToWorld(visible.max);
            int startX = Mathf.FloorToInt(topLeft.x);
            int endX = Mathf.CeilToInt(bottomRight.x);
            int startY = Mathf.FloorToInt(topLeft.y);
            int endY = Mathf.CeilToInt(bottomRight.y);
            int lineCount = 0;
            float luminance =
                backgroundColor.r * .2126f +
                backgroundColor.g * .7152f +
                backgroundColor.b * .0722f;
            Color gridColor = luminance < .35f
                ? new Color(.42f, .5f, .64f, .68f)
                : new Color(.18f, .22f, .28f, .58f);
            gridColor.a *= opacity;
            for (int x = startX; x <= endX && lineCount < 500; x++)
            {
                Vector2 start = WorldToScreen(new Vector2(x, topLeft.y));
                Vector2 end = WorldToScreen(new Vector2(x, bottomRight.y));
                DrawLine(painter, start, end, gridColor, 1f);
                lineCount++;
            }

            for (int y = startY; y <= endY && lineCount < 1000; y++)
            {
                Vector2 start = WorldToScreen(new Vector2(topLeft.x, y));
                Vector2 end = WorldToScreen(new Vector2(bottomRight.x, y));
                DrawLine(painter, start, end, gridColor, 1f);
                lineCount++;
            }
        }

        private void DrawDoors(Painter2D painter)
        {
            for (int index = 0; index < document.doors.Count; index++)
            {
                PlanningDoor door = document.doors[index];
                PlanningLock lockValue =
                    document.FindLock(door.lockId);
                Color doorColor = lockValue != null
                    ? ParseColor(
                        lockValue.colorHex,
                        new Color(1f, .72f, .22f))
                    : new Color(1f, .72f, .22f);
                Rect rect = CellRect(door.x, door.y);
                rect = new Rect(
                    rect.x + rect.width * .15f,
                    rect.y + rect.height * .15f,
                    rect.width * .7f,
                    rect.height * .7f);
                FillRect(
                    painter,
                    rect,
                    new Color(
                        doorColor.r,
                        doorColor.g,
                        doorColor.b,
                        .95f),
                    1f);
            }
        }

        private void DrawKeys(Painter2D painter)
        {
            for (int index = 0; index < document.keys.Count; index++)
            {
                PlanningKey key = document.keys[index];
                Rect rect = CellRect(key.x, key.y);
                Vector2 center = new Vector2(
                    rect.center.x,
                    rect.center.y);
                float radius = Mathf.Max(2f, rect.width * .24f);
                FillCircle(
                    painter,
                    center,
                    radius,
                    ParseColor(key.colorHex, new Color(1f, .83f, .35f)));
            }
        }

        private void DrawRectanglePreview(Painter2D painter)
        {
            if (!isDragging)
            {
                return;
            }

            Vector2Int start = CellAt(dragStart);
            Vector2Int end = CellAt(dragCurrent);
            int minX = Mathf.Min(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxX = Mathf.Max(start.x, end.x);
            int maxY = Mathf.Max(start.y, end.y);
            Rect rect = CellRect(
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1);
            FillRect(
                painter,
                rect,
                new Color(.24f, .72f, 1f, .18f),
                1f);
            StrokeRect(
                painter,
                rect,
                new Color(.45f, .85f, 1f, .9f),
                2f);
        }

        private void DrawHoverCell(Painter2D painter)
        {
            if (!hasHover)
            {
                return;
            }

            Rect rect = CellRect(hoveredCell.x, hoveredCell.y);
            FillRect(
                painter,
                rect,
                new Color(1f, 1f, 1f, .14f),
                1f);
            StrokeRect(
                painter,
                rect,
                new Color(1f, 1f, 1f, .95f),
                2f);
        }

        private void DrawEmptyHint(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            FillRect(
                painter,
                new Rect(
                    contentRect.center.x - 110f,
                    contentRect.center.y - 22f,
                    220f,
                    44f),
                new Color(.12f, .15f, .2f, .94f),
                1f);
        }

        private Rect CellRect(int x, int y, int width = 1, int height = 1)
        {
            Vector2 position = WorldToScreen(new Vector2(x, y));
            return new Rect(
                position.x,
                position.y,
                GridSize * width,
                GridSize * height);
        }

        private Vector2 CellCenter(int x, int y)
        {
            Rect rect = CellRect(x, y);
            return rect.center;
        }

        private static void DrawLine(
            Painter2D painter,
            Vector2 start,
            Vector2 end,
            Color color,
            float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.Stroke();
        }

        private static void FillRect(
            Painter2D painter,
            Rect rect,
            Color color,
            float lineWidth)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
            if (lineWidth <= 0f)
            {
                return;
            }
        }

        private static void StrokeRect(
            Painter2D painter,
            Rect rect,
            Color color,
            float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Stroke();
        }

        private static void FillCircle(
            Painter2D painter,
            Vector2 center,
            float radius,
            Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
        }

        private static Color ParseColor(string value, Color fallback)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   ColorUtility.TryParseHtmlString(value, out Color color)
                ? color
                : fallback;
        }

        private static Color ColorForDetail(PlanningDetailType type)
        {
            switch (type)
            {
                case PlanningDetailType.Solid:
                    return new Color(.28f, .31f, .38f, 1f);
                case PlanningDetailType.Platform:
                    return new Color(.3f, .75f, .95f, 1f);
                case PlanningDetailType.Hazard:
                    return new Color(.9f, .28f, .28f, 1f);
                case PlanningDetailType.Water:
                    return new Color(.2f, .48f, .9f, 1f);
                case PlanningDetailType.Ladder:
                    return new Color(.85f, .72f, .3f, 1f);
                case PlanningDetailType.Exit:
                    return new Color(.35f, .85f, .5f, 1f);
                case PlanningDetailType.Item:
                    return new Color(1f, .82f, .3f, 1f);
                case PlanningDetailType.Enemy:
                    return new Color(.85f, .35f, .75f, 1f);
                case PlanningDetailType.Prop:
                    return new Color(1f, .62f, .2f, 1f);
                default:
                    return new Color(.7f, .7f, .75f, 1f);
            }
        }

        private Color ColorForBox(PlanningBox box)
        {
            if (!string.IsNullOrEmpty(box.propEntryId) ||
                !string.IsNullOrEmpty(box.propEntryName))
            {
                return ColorForDetail(PlanningDetailType.Prop);
            }

            LevelEditorBlockEntry entry = FindEntry(
                box.paletteEntryId,
                box.paletteEntryName);
            return entry != null
                ? entry.Color
                : ColorForDetail(box.type);
        }

        private Texture2D GetBoxPreview(PlanningBox box)
        {
            if (!string.IsNullOrEmpty(box.propEntryId) ||
                !string.IsNullOrEmpty(box.propEntryName))
            {
                LevelEditorPropEntry prop = FindPropEntry(
                    box.propEntryId,
                    box.propEntryName);
                if (prop == null || prop.Prefab == null)
                {
                    return null;
                }

                Texture2D assetPreview =
                    AssetPreview.GetAssetPreview(prop.Prefab);
                return assetPreview != null
                    ? assetPreview
                    : AssetPreview.GetMiniThumbnail(prop.Prefab);
            }

            LevelEditorBlockEntry entry = FindEntry(
                box.paletteEntryId,
                box.paletteEntryName);
            return entry != null
                ? LevelEditorDecorationService
                    .GetEntryFrontPreview(entry, 64)
                : null;
        }

        private LevelEditorPropEntry FindPropEntry(
            string entryId,
            string entryName)
        {
            if (palette == null)
            {
                return null;
            }

            for (int index = 0;
                 index < palette.PropEntries.Count;
                 index++)
            {
                LevelEditorPropEntry entry = palette.PropEntries[index];
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entryId) &&
                    entry.EntryId == entryId)
                {
                    return entry;
                }

                if (string.IsNullOrEmpty(entryId) &&
                    !string.IsNullOrEmpty(entryName) &&
                    entry.DisplayName == entryName)
                {
                    return entry;
                }
            }

            return null;
        }

        private LevelEditorBlockEntry FindEntry(
            string entryId,
            string entryName)
        {
            if (palette == null)
            {
                return null;
            }

            for (int index = 0; index < palette.Entries.Count; index++)
            {
                LevelEditorBlockEntry entry = palette.Entries[index];
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entryId) &&
                    entry.EntryId == entryId)
                {
                    return entry;
                }

                if (string.IsNullOrEmpty(entryId) &&
                    !string.IsNullOrEmpty(entryName) &&
                    entry.DisplayName == entryName)
                {
                    return entry;
                }
            }

            return null;
        }

        private void OnWheel(WheelEvent evt)
        {
            if (document == null)
            {
                return;
            }

            Vector2 pointer = evt.localMousePosition;
            Vector2 before = ScreenToWorld(pointer);
            zoom = Mathf.Clamp(
                zoom * Mathf.Pow(.92f, evt.delta.y),
                MinimumZoom,
                MaximumZoom);
            Vector2 after = ScreenToWorld(pointer);
            pan += (after - before) * GridSize;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            Vector2 position = evt.localPosition;
            UpdateHover(position);
            if (isPlacingRoom)
            {
                if (evt.button == 0)
                {
                    TryPlaceNewRoom(CellAt(position));
                }
                else if (evt.button == 1 || evt.button == 2)
                {
                    CancelRoomPlacement();
                }

                evt.StopPropagation();
                return;
            }

            if (mode == PlanningCanvasMode.World &&
                mapTool == PlanningMapTool.Connector &&
                HandleConnectorPointerDown(position))
            {
                evt.StopPropagation();
                return;
            }

            if (evt.button == 1 ||
                evt.button == 2 ||
                mapTool == PlanningMapTool.Pan ||
                (mode == PlanningCanvasMode.Detail &&
                 GetActiveDetailTool() == PlanningDetailTool.Pan) ||
                (mode == PlanningCanvasMode.Assembly &&
                 GetActiveDetailTool() == PlanningDetailTool.Pan))
            {
                isPanning = true;
                panPointerId = evt.pointerId;
                panLastPosition = position;
                PointerCaptureHelper.CapturePointer(
                    this,
                    evt.pointerId);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
            {
                return;
            }

            isDragging = true;
            dragPointerId = evt.pointerId;
            dragStart = position;
            dragCurrent = position;
            PointerCaptureHelper.CapturePointer(
                this,
                evt.pointerId);
            if (mode == PlanningCanvasMode.World)
            {
                HandleWorldPointerDown(position);
            }
            else if (mode == PlanningCanvasMode.Assembly)
            {
                HandleAssemblyPointerDown(position);
            }
            else
            {
                HandleDetailPointerDown(position);
            }

            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 position = evt.localPosition;
            UpdateHover(position);
            if (isPanning && evt.pointerId == panPointerId)
            {
                pan += position - panLastPosition;
                panLastPosition = position;
                UpdateDetailPreviewImages();
                MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }

            if (!isDragging || evt.pointerId != dragPointerId)
            {
                MarkDirtyRepaint();
                return;
            }

            dragCurrent = position;
            if (mode == PlanningCanvasMode.World)
            {
                HandleWorldPointerMove(position);
            }
            else if (mode == PlanningCanvasMode.Assembly)
            {
                HandleAssemblyPointerMove(position);
            }
            else
            {
                HandleDetailPointerMove(position);
            }

            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            hasHover = false;
            MarkDirtyRepaint();
        }

        private void UpdateHover(Vector2 position)
        {
            hoveredCell = CellAt(position);
            hasHover = true;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != panPointerId &&
                evt.pointerId != dragPointerId)
            {
                return;
            }

            if (isDragging && evt.pointerId == dragPointerId)
            {
                dragCurrent = evt.localPosition;
                if (mode == PlanningCanvasMode.World)
                {
                    CompleteWorldDrag();
                }
                else if (mode == PlanningCanvasMode.Assembly)
                {
                    CompleteAssemblyDrag();
                }
                else
                {
                    CompleteDetailDrag();
                }
            }

            EndPointerAction();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void EndPointerAction()
        {
            if (isPanning && panPointerId >= 0)
            {
                PointerCaptureHelper.ReleasePointer(
                    this,
                    panPointerId);
            }

            if (isDragging && dragPointerId >= 0)
            {
                PointerCaptureHelper.ReleasePointer(
                    this,
                    dragPointerId);
            }

            isPanning = false;
            isDragging = false;
            panPointerId = -1;
            dragPointerId = -1;
            movingBox = null;
            lastPaintCell = new Vector2Int(int.MinValue, int.MinValue);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                if (isPlacingRoom)
                {
                    CancelRoomPlacement();
                    evt.StopPropagation();
                    return;
                }

                isDragging = false;
                hasConnectorStart = false;
                selectedBoxId = null;
                SelectionChanged?.Invoke();
                MarkDirtyRepaint();
            }
        }

        private void HandleWorldPointerDown(Vector2 position)
        {
            Vector2Int cell = CellAt(position);
            switch (mapTool)
            {
                case PlanningMapTool.Select:
                    SelectRoomAt(cell);
                    break;
                case PlanningMapTool.Paint:
                    AddCell(cell);
                    lastPaintCell = cell;
                    break;
                case PlanningMapTool.Erase:
                    lastPaintCell = cell;
                    break;
                case PlanningMapTool.Door:
                    AddDoor(cell);
                    break;
                case PlanningMapTool.Key:
                    AddKey(cell);
                    break;
                case PlanningMapTool.Connector:
                    break;
            }
        }

        private void HandleWorldPointerMove(Vector2 position)
        {
            if (mapTool != PlanningMapTool.Paint)
            {
                return;
            }

            Vector2Int cell = CellAt(position);
            if (cell == lastPaintCell)
            {
                return;
            }

            lastPaintCell = cell;
            AddCell(cell);
        }

        private void CompleteWorldDrag()
        {
            Vector2Int start = CellAt(dragStart);
            Vector2Int end = CellAt(dragCurrent);
            if (mapTool == PlanningMapTool.Connector)
            {
                if (start != end)
                {
                    AddConnector(start, end);
                    hasConnectorStart = false;
                }
                else
                {
                    hasConnectorStart = true;
                    connectorStartCell = start;
                }

                MarkDirtyRepaint();
                return;
            }

            if (mapTool == PlanningMapTool.Erase)
            {
                BeginDocumentBatch();
                foreach (Vector2Int cell in BuildAreaCells(start, end))
                {
                    EraseCell(cell);
                }

                EndDocumentBatch();
                return;
            }

            if (mapTool != PlanningMapTool.Rectangle)
            {
                return;
            }

            PlanningRoom room = GetOrCreateSelectedRoom(
                Mathf.Min(start.x, end.x),
                Mathf.Min(start.y, end.y));
            List<PlanningCell> before = CloneCells(room.cells);
            int minX = Mathf.Min(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxX = Mathf.Max(start.x, end.x);
            int maxY = Mathf.Max(start.y, end.y);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    AddCellToRoom(room, x, y);
                }
            }

            if (!ValidateConnectivity(room))
            {
                room.cells = before;
                ValidationFailed?.Invoke(
                    room.isConnector
                        ? "不允许：矩形修改会断开通道两端。"
                        : "不允许：矩形修改会断开房间区块。");
                MarkDirtyRepaint();
                return;
            }

            NotifyDocumentChanged();
        }

        private void AddConnector(Vector2Int start, Vector2Int end)
        {
            PlanningRoom fromRoom = FindRoomNearCell(start);
            PlanningRoom toRoom = FindRoomNearCell(end);
            if (fromRoom == null ||
                toRoom == null ||
                fromRoom == toRoom)
            {
                return;
            }

            PlanningRoom connector = PlanningRoom.CreateConnector(
                $"通道 {fromRoom.name} → {toRoom.name}",
                fromRoom.id,
                toRoom.id);
            PlanningDocument.SetConnectorPorts(
                connector,
                fromRoom,
                toRoom,
                new PlanningCell(start.x, start.y),
                new PlanningCell(end.x, end.y));
            BuildDefaultConnectorLine(connector);
            document.rooms.Add(connector);
            document.RefreshConnectorPaths();
            if (!ValidateConnectivity(connector))
            {
                document.rooms.Remove(connector);
                ValidationFailed?.Invoke(
                    "不允许：通道没有真正连接两个房间。");
                return;
            }

            selectedRoomId = connector.id;
            selectedBoxId = null;
            NotifyDocumentChanged();
            SelectionChanged?.Invoke();
        }

        private void BuildDefaultConnectorLine(
            PlanningRoom connector)
        {
            List<Vector2Int> path =
                PlanningLayoutUtility.GetConnectorAssemblyPath(
                    document.rooms,
                    connector,
                    document.worldBlockCellWidth,
                    document.worldBlockCellHeight);
            if (path.Count == 0)
            {
                return;
            }

            Vector2Int origin = path[0];
            for (int index = 0; index < path.Count; index++)
            {
                Vector2Int cell = path[index];
                var box = new PlanningBox(
                    PlanningDetailType.Solid,
                    new RectInt(
                        cell.x - origin.x,
                        cell.y - origin.y,
                        1,
                        1))
                {
                    label = "默认通道线",
                    paletteEntryName = "黑方块"
                };
                connector.boxes.Add(box);
            }
        }

        private bool HandleConnectorPointerDown(Vector2 position)
        {
            Vector2Int cell = CellAt(position);
            if (!hasConnectorStart)
            {
                connectorStartCell = cell;
                hasConnectorStart = true;
                MarkDirtyRepaint();
                return false;
            }

            AddConnector(connectorStartCell, cell);
            hasConnectorStart = false;
            MarkDirtyRepaint();
            return true;
        }

        private PlanningRoom FindRoomNearCell(Vector2Int cell)
        {
            PlanningRoom exact = document.FindRoomAt(cell.x, cell.y);
            if (exact != null)
            {
                return exact;
            }

            PlanningRoom nearest = null;
            int nearestDistance = 3;
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room = document.rooms[roomIndex];
                if (room.isConnector)
                {
                    continue;
                }

                for (int cellIndex = 0;
                     cellIndex < room.cells.Count;
                     cellIndex++)
                {
                    PlanningCell roomCell = room.cells[cellIndex];
                    int distance =
                        Mathf.Abs(roomCell.x - cell.x) +
                        Mathf.Abs(roomCell.y - cell.y);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = room;
                    }
                }
            }

            return nearest;
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

        private static IEnumerable<Vector2Int> BuildStrokeCells(
            Vector2Int start,
            Vector2Int end)
        {
            int x = start.x;
            int y = start.y;
            int deltaX = Mathf.Abs(end.x - start.x);
            int deltaY = -Mathf.Abs(end.y - start.y);
            int stepX = start.x < end.x ? 1 : -1;
            int stepY = start.y < end.y ? 1 : -1;
            int error = deltaX + deltaY;

            while (true)
            {
                yield return new Vector2Int(x, y);
                if (x == end.x && y == end.y)
                {
                    yield break;
                }

                int doubled = error * 2;
                if (doubled >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }

                if (doubled <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
            }
        }

        private static IEnumerable<Vector2Int> BuildAreaCells(
            Vector2Int start,
            Vector2Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }

        private void HandleDetailPointerDown(Vector2 position)
        {
            PlanningRoom room = document?.FindRoom(selectedRoomId);
            if (room == null)
            {
                return;
            }

            Vector2Int cell = CellAt(position);
            PlanningDetailTool activeTool = GetActiveDetailTool();
            if (activeTool == PlanningDetailTool.Select)
            {
                PlanningBox box = FindBoxAt(room, cell.x, cell.y);
                selectedBoxId = box?.id;
                movingBox = box;
                movingBoxStart = position;
                movingBoxOrigin = box != null
                    ? new Vector2Int(box.x, box.y)
                    : default;
                SelectionChanged?.Invoke();
            }
        }

        private void HandleDetailPointerMove(Vector2 position)
        {
            PlanningDetailTool activeTool = GetActiveDetailTool();
            if (activeTool != PlanningDetailTool.Select ||
                movingBox == null)
            {
                return;
            }

            Vector2 delta = position - movingBoxStart;
            int cellDeltaX = Mathf.RoundToInt(delta.x / GridSize);
            int cellDeltaY = Mathf.RoundToInt(delta.y / GridSize);
            PlanningRoom room = document?.FindRoom(selectedRoomId);
            if (room == null)
            {
                return;
            }

            RectInt allowed = room.GetLocalAllowedRect(
                WorldBlockCellWidth,
                WorldBlockCellHeight);
            if (room.isConnector)
            {
                allowed = GetConnectorDetailBounds(room);
            }
            movingBox.x = Mathf.Clamp(
                movingBoxOrigin.x + cellDeltaX,
                allowed.x,
                allowed.xMax - movingBox.width);
            movingBox.y = Mathf.Clamp(
                movingBoxOrigin.y + cellDeltaY,
                allowed.y,
                allowed.yMax - movingBox.height);
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        private void CompleteDetailDrag()
        {
            PlanningDetailTool activeTool = GetActiveDetailTool();
            if (activeTool == PlanningDetailTool.Box)
            {
                PlanningRoom room = document?.FindRoom(selectedRoomId);
                if (room == null)
                {
                    return;
                }

                Vector2Int start = CellAt(dragStart);
                Vector2Int end = CellAt(dragCurrent);
                int minX = Mathf.Min(start.x, end.x);
                int minY = Mathf.Min(start.y, end.y);
                int maxX = Mathf.Max(start.x, end.x);
                int maxY = Mathf.Max(start.y, end.y);
                RectInt allowed = room.GetLocalAllowedRect(
                    WorldBlockCellWidth,
                    WorldBlockCellHeight);
                if (room.isConnector)
                {
                    allowed = GetConnectorDetailBounds(room);
                }
                if (minX < allowed.x ||
                    minY < allowed.y ||
                    maxX >= allowed.xMax ||
                    maxY >= allowed.yMax)
                {
                    return;
                }

                PlanningBox box = new PlanningBox(
                    detailType,
                    new RectInt(
                        minX,
                        minY,
                        maxX - minX + 1,
                        maxY - minY + 1));
                ApplyDetailSelection(box);
                room.boxes.Add(box);
                selectedBoxId = box.id;
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
            }
            else if (activeTool == PlanningDetailTool.Select &&
                     movingBox != null)
            {
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
            }
            else if (activeTool == PlanningDetailTool.Erase)
            {
                PlanningRoom room = document?.FindRoom(selectedRoomId);
                if (room == null)
                {
                    return;
                }

                BeginDocumentBatch();
                foreach (Vector2Int cell in BuildAreaCells(
                             CellAt(dragStart),
                             CellAt(dragCurrent)))
                {
                    PlanningBox box = FindBoxAt(
                        room,
                        cell.x,
                        cell.y);
                    if (box != null)
                    {
                        CarveBox(room.boxes, box, cell);
                    }
                }

                EndDocumentBatch();
                SelectionChanged?.Invoke();
            }
        }

        private void HandleAssemblyPointerDown(Vector2 position)
        {
            Vector2Int cell = CellAt(position);
            PlanningDetailTool activeTool = GetActiveDetailTool();
            if (activeTool == PlanningDetailTool.Box ||
                activeTool == PlanningDetailTool.Erase)
            {
                assemblyStrokeStart = cell;
                assemblyStrokeEnd = cell;
                lastPaintCell = cell;
            }
        }

        private void HandleAssemblyPointerMove(Vector2 position)
        {
            PlanningDetailTool activeTool = GetActiveDetailTool();
            if (activeTool != PlanningDetailTool.Box &&
                activeTool != PlanningDetailTool.Erase)
            {
                return;
            }

            Vector2Int cell = CellAt(position);
            if (cell == lastPaintCell)
            {
                return;
            }

            lastPaintCell = cell;
            assemblyStrokeEnd = cell;
            MarkDirtyRepaint();
        }

        private void CompleteAssemblyDrag()
        {
            PlanningDetailTool activeTool = GetActiveDetailTool();
            BeginDocumentBatch();
            IEnumerable<Vector2Int> cells = BuildAreaCells(
                assemblyStrokeStart,
                assemblyStrokeEnd);
            foreach (Vector2Int cell in cells)
            {
                if (activeTool == PlanningDetailTool.Erase)
                {
                    EraseAssemblyBox(cell);
                }
                else if (activeTool == PlanningDetailTool.Box)
                {
                    AddAssemblyBox(cell);
                }
            }

            EndDocumentBatch();
        }

        private void AddAssemblyBox(Vector2Int cell)
        {
            GetAssemblyStride(out int strideX, out int strideY);
            PlanningBox box;
            if (TryFindAssemblyOwner(
                    cell,
                    strideX,
                    strideY,
                    out PlanningRoom owner,
                    out Vector2Int local))
            {
                box = new PlanningBox(
                    detailType,
                    new RectInt(local.x, local.y, 1, 1));
                ApplyDetailSelection(box);
                owner.boxes.Add(box);
            }
            else
            {
                return;
            }

            selectedBoxId = box.id;
            NotifyDocumentChanged();
            SelectionChanged?.Invoke();
        }

        private void ApplyDetailSelection(PlanningBox box)
        {
            if (!string.IsNullOrEmpty(detailPropEntryId))
            {
                box.type = PlanningDetailType.Prop;
                box.propEntryId = detailPropEntryId;
                box.propEntryName = detailPropEntryName;
                box.paletteEntryId = string.Empty;
                box.paletteEntryName = string.Empty;
                return;
            }

            box.propEntryId = string.Empty;
            box.propEntryName = string.Empty;
            box.paletteEntryId = detailEntryId;
            box.paletteEntryName = detailEntryName;
        }

        private void EraseAssemblyBox(Vector2Int cell)
        {
            GetAssemblyStride(out int strideX, out int strideY);
            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom owner = document.rooms[roomIndex];
                for (int boxIndex = owner.boxes.Count - 1;
                     boxIndex >= 0;
                     boxIndex--)
                {
                    PlanningBox box = owner.boxes[boxIndex];
                    if (!GetAssemblyBoxRect(
                            owner,
                            box,
                            strideX,
                            strideY).Contains(cell))
                    {
                        continue;
                    }

                    CarveBox(owner.boxes, box, cell);
                    NotifyDocumentChanged();
                    SelectionChanged?.Invoke();
                    return;
                }
            }

            for (int index = document.assemblyPatches.Count - 1;
                 index >= 0;
                 index--)
            {
                PlanningBox box = document.assemblyPatches[index];
                RectInt rect = new RectInt(
                    box.x,
                    box.y,
                    box.width,
                    box.height);
                if (!rect.Contains(cell))
                {
                    continue;
                }

                CarveBox(
                    document.assemblyPatches,
                    box,
                    cell);
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
                return;
            }
        }

        private bool TryFindAssemblyOwner(
            Vector2Int cell,
            int strideX,
            int strideY,
            out PlanningRoom owner,
            out Vector2Int local)
        {
            for (int index = 0; index < document.rooms.Count; index++)
            {
                PlanningRoom candidate = document.rooms[index];
                if (candidate.isConnector)
                {
                    continue;
                }

                GetAssemblyRoomOffset(
                    candidate,
                    strideX,
                    strideY,
                    out int offsetX,
                    out int offsetY,
                    out int maxPlanY);
                RectInt allowed = candidate.GetLocalAllowedRect(
                    WorldBlockCellWidth,
                    WorldBlockCellHeight);
                var bounds = new RectInt(
                    offsetX,
                    offsetY,
                    allowed.width,
                    allowed.height);
                if (!bounds.Contains(cell))
                {
                    continue;
                }

                owner = candidate;
                local = new Vector2Int(
                    cell.x - offsetX,
                    cell.y - offsetY);
                return true;
            }

            for (int index = 0; index < document.rooms.Count; index++)
            {
                PlanningRoom connector = document.rooms[index];
                if (!connector.isConnector)
                {
                    continue;
                }

                GetAssemblyConnectorOrigin(
                    connector,
                    strideX,
                    strideY,
                    out int originX,
                    out int originY);
                if (connector.boxes.Count == 0)
                {
                    PlanningCell pathOrigin = GetConnectorOrigin(connector);
                    Vector2Int localCell = new Vector2Int(
                        cell.x - originX,
                        cell.y - originY);
                    for (int cellIndex = 0;
                         cellIndex < connector.cells.Count;
                         cellIndex++)
                    {
                        PlanningCell pathCell =
                            connector.cells[cellIndex];
                        if (pathCell.x - pathOrigin.x == localCell.x &&
                            pathCell.y - pathOrigin.y == localCell.y)
                        {
                            owner = connector;
                            local = localCell;
                            return true;
                        }
                    }
                }

                for (int boxIndex = 0;
                     boxIndex < connector.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = connector.boxes[boxIndex];
                    var bounds = new RectInt(
                        originX + box.x,
                        originY + box.y,
                        box.width,
                        box.height);
                    if (!bounds.Contains(cell))
                    {
                        continue;
                    }

                    owner = connector;
                    local = new Vector2Int(
                        cell.x - originX,
                        cell.y - originY);
                    return true;
                }
            }

            owner = null;
            local = default;
            return false;
        }

        private static RectInt GetAssemblyBoxRect(
            PlanningRoom owner,
            PlanningBox box,
            int strideX,
            int strideY)
        {
            return PlanningLayoutUtility.GetAssemblyBoxRect(
                owner,
                box,
                strideX,
                strideY);
        }

        private static void CarveBox(
            List<PlanningBox> boxes,
            PlanningBox box,
            Vector2Int cell)
        {
            boxes.Remove(box);
            int leftWidth = cell.x - box.x;
            int rightStart = cell.x + 1;
            int rightWidth = box.x + box.width - rightStart;
            int topHeight = cell.y - box.y;
            int bottomStart = cell.y + 1;
            int bottomHeight = box.y + box.height - bottomStart;

            AddCarvedBox(
                boxes,
                box,
                box.x,
                box.y,
                leftWidth,
                box.height);
            AddCarvedBox(
                boxes,
                box,
                rightStart,
                box.y,
                rightWidth,
                box.height);
            AddCarvedBox(
                boxes,
                box,
                cell.x,
                box.y,
                1,
                topHeight);
            AddCarvedBox(
                boxes,
                box,
                cell.x,
                bottomStart,
                1,
                bottomHeight);
        }

        private static void AddCarvedBox(
            List<PlanningBox> boxes,
            PlanningBox source,
            int x,
            int y,
            int width,
            int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var carved = new PlanningBox(
                source.type,
                new RectInt(x, y, width, height))
            {
                label = source.label,
                paletteEntryId = source.paletteEntryId,
                paletteEntryName = source.paletteEntryName,
                propEntryId = source.propEntryId,
                propEntryName = source.propEntryName
            };
            boxes.Add(carved);
        }

        private void SelectRoomAt(Vector2Int cell)
        {
            PlanningRoom room = document.FindRoomAt(cell.x, cell.y) ??
                                document.FindConnectorAt(cell.x, cell.y);
            selectedRoomId = room?.id;
            selectedBoxId = null;
            SelectionChanged?.Invoke();
        }

        private void AddCell(Vector2Int cell)
        {
            PlanningRoom room = GetOrCreateSelectedRoom(cell.x, cell.y);
            List<PlanningCell> before = CloneCells(room.cells);
            AddCellToRoom(room, cell.x, cell.y);
            if (!ValidateConnectivity(room))
            {
                room.cells = before;
                ValidationFailed?.Invoke(
                    room.isConnector
                        ? "不允许：这次修改会断开通道两端。"
                        : "不允许：房间区块必须保持连通。");
                return;
            }

            NotifyDocumentChanged();
        }

        private void EraseCell(Vector2Int cell)
        {
            PlanningRoom room =
                document.FindRoomAt(cell.x, cell.y) ??
                document.FindConnectorAt(cell.x, cell.y);
            if (room == null)
            {
                return;
            }

            List<PlanningCell> before = CloneCells(room.cells);
            room.cells.RemoveAll(
                item => item.x == cell.x && item.y == cell.y);
            if (!ValidateConnectivity(room))
            {
                room.cells = before;
                ValidationFailed?.Invoke(
                    room.isConnector
                        ? "不允许：不能断开通道两端。"
                        : "不允许：房间区块不能断开。");
                return;
            }

            NotifyDocumentChanged();
        }

        private bool ValidateConnectivity(PlanningRoom room)
        {
            if (room.isConnector)
            {
                return IsConnectorConnected(room);
            }

            return AreCellsConnected(room.cells);
        }

        private bool IsConnectorConnected(PlanningRoom connector)
        {
            PlanningRoom from = document.FindRoom(connector.fromRoomId);
            PlanningRoom to = document.FindRoom(connector.toRoomId);
            if (from == null ||
                to == null ||
                connector.cells.Count == 0)
            {
                return false;
            }

            if (!CellInRoom(connector.cells[0], from) ||
                !CellInRoom(
                    connector.cells[connector.cells.Count - 1],
                    to))
            {
                return false;
            }

            return AreCellsConnected(connector.cells);
        }

        private static bool CellInRoom(
            PlanningCell cell,
            PlanningRoom room)
        {
            for (int index = 0; index < room.cells.Count; index++)
            {
                PlanningCell candidate = room.cells[index];
                if (candidate.x == cell.x && candidate.y == cell.y)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreCellsConnected(
            IReadOnlyList<PlanningCell> cells)
        {
            if (cells.Count == 0)
            {
                return false;
            }

            var all = new HashSet<Vector2Int>();
            for (int index = 0; index < cells.Count; index++)
            {
                all.Add(new Vector2Int(cells[index].x, cells[index].y));
            }

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var first = new Vector2Int(cells[0].x, cells[0].y);
            visited.Add(first);
            queue.Enqueue(first);
            var directions = new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int directionIndex = 0;
                     directionIndex < directions.Length;
                     directionIndex++)
                {
                    Vector2Int next = current + directions[directionIndex];
                    if (!all.Contains(next) || !visited.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            return visited.Count == all.Count;
        }

        private static List<PlanningCell> CloneCells(
            IReadOnlyList<PlanningCell> cells)
        {
            var clone = new List<PlanningCell>();
            for (int index = 0; index < cells.Count; index++)
            {
                clone.Add(new PlanningCell(
                    cells[index].x,
                    cells[index].y));
            }

            return clone;
        }

        private void AddDoor(Vector2Int cell)
        {
            PlanningRoom room = GetOrCreateSelectedRoom(cell.x, cell.y);
            PlanningDoor existing = document.doors.Find(
                door => door.x == cell.x && door.y == cell.y);
            if (existing != null)
            {
                return;
            }

            document.doors.Add(new PlanningDoor(
                room.id,
                cell.x,
                cell.y,
                0));
            NotifyDocumentChanged();
        }

        private void AddKey(Vector2Int cell)
        {
            PlanningRoom room = GetOrCreateSelectedRoom(cell.x, cell.y);
            PlanningKey existing = document.keys.Find(
                key => key.x == cell.x && key.y == cell.y);
            if (existing != null)
            {
                return;
            }

            document.keys.Add(new PlanningKey(
                "颜色钥匙 " + (document.keys.Count + 1),
                room.id,
                cell.x,
                cell.y));
            NotifyDocumentChanged();
        }

        private PlanningRoom GetOrCreateSelectedRoom(int x, int y)
        {
            PlanningRoom room = document.FindRoom(selectedRoomId);
            if (room != null)
            {
                return room;
            }

            room = new PlanningRoom(
                "新房间 " + (document.rooms.Count + 1));
            document.rooms.Add(room);
            selectedRoomId = room.id;
            SelectionChanged?.Invoke();
            return room;
        }

        private static void AddCellToRoom(
            PlanningRoom room,
            int x,
            int y)
        {
            for (int index = 0; index < room.cells.Count; index++)
            {
                PlanningCell cell = room.cells[index];
                if (cell.x == x && cell.y == y)
                {
                    return;
                }
            }

            room.cells.Add(new PlanningCell(x, y));
        }

        private static PlanningBox FindBoxAt(
            PlanningRoom room,
            int x,
            int y)
        {
            for (int index = room.boxes.Count - 1; index >= 0; index--)
            {
                PlanningBox box = room.boxes[index];
                if (x >= box.x && x < box.x + box.width &&
                    y >= box.y && y < box.y + box.height)
                {
                    return box;
                }
            }

            return null;
        }

        private void NotifyDocumentChanged()
        {
            if (suppressDocumentChanged)
            {
                pendingDocumentChanged = true;
                return;
            }

            DocumentChanged?.Invoke();
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }
    }
}
