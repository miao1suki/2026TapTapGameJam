using System;
using System.Collections.Generic;
using Project;
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

    public sealed partial class PlanningCanvas : VisualElement
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
        private readonly HashSet<string> mergeSelection = new HashSet<string>();

        private IEnumerable<(PlanningRoom room, List<PlanningBox> boxes, PlanningBox box)> AllBoxes()
        {
            if (document == null) yield break;
            foreach (var room in document.rooms)
                foreach (var box in room.boxes) yield return (room, room.boxes, box);
            foreach (var box in document.assemblyPatches) yield return (null, document.assemblyPatches, box);
        }

        private RectInt DisplayRect(PlanningRoom room, PlanningBox box)
        {
            if (mode != PlanningCanvasMode.Assembly || room == null)
                return new RectInt(box.x, box.y, box.width, box.height);
            GetAssemblyStride(out int sx, out int sy);
            return GetAssemblyBoxRect(room, box, sx, sy);
        }

        private static bool SameKind(PlanningBox a, PlanningBox b) =>
            a.type == b.type && a.paletteEntryId == b.paletteEntryId &&
            a.paletteEntryName == b.paletteEntryName && a.propEntryId == b.propEntryId && a.propEntryName == b.propEntryName;

        private void CompleteMergeSelection()
        {
            var start = CellAt(dragStart);
            var end = CellAt(dragCurrent);
            var area = new RectInt(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Abs(start.x-end.x)+1, Mathf.Abs(start.y-end.y)+1);
            ToggleMergeArea(area);
        }

        internal void ToggleMergeArea(RectInt area)
        {
            var hits = new List<PlanningBox>();
            PlanningBox kind = null;
            foreach (var item in AllBoxes())
                if (mergeSelection.Contains(item.box.id)) { kind = item.box; break; }
            foreach (var item in AllBoxes())
            {
                if (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId) continue;
                if (!DisplayRect(item.room, item.box).Overlaps(area)) continue;
                if (kind == null) kind = item.box;
                if (SameKind(kind, item.box)) hits.Add(item.box);
            }
            foreach (var box in hits)
                if (!mergeSelection.Remove(box.id)) mergeSelection.Add(box.id);
            StatusChanged?.Invoke($"已选择 {mergeSelection.Count} 项");
            MarkDirtyRepaint();
        }

        public void MergeSelectedItems()
        {
            var items = new List<PlanningBox>();
            List<PlanningBox> owner = null;
            foreach (var item in AllBoxes())
            {
                if (!mergeSelection.Contains(item.box.id)) continue;
                if (item.box.singleInstance || PlanningDoorUtility.IsDoor(item.box) || !string.IsNullOrEmpty(item.box.doorOwnerId))
                { ValidationFailed?.Invoke("门与绑定按钮不能合并。"); return; }
                if (owner != null && (owner != item.boxes || !SameKind(items[0], item.box)))
                { ValidationFailed?.Invoke("请选择同一房间中的同种物体。"); return; }
                owner = item.boxes;
                items.Add(item.box);
            }
            if (items.Count == 0 || (items.Count == 1 && (items[0].isMerged || items[0].width*items[0].height == 1)))
            { StatusChanged?.Invoke("请先选择至少两个同种单元格。"); return; }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            var cells = new HashSet<Vector2Int>();
            foreach (var box in items)
            {
                minX = Mathf.Min(minX,box.x); minY = Mathf.Min(minY,box.y);
                maxX = Mathf.Max(maxX,box.x+box.width); maxY = Mathf.Max(maxY,box.y+box.height);
                for (int x=box.x; x<box.x+box.width; x++) for (int y=box.y; y<box.y+box.height; y++)
                    if (!cells.Add(new Vector2Int(x,y))) { ValidationFailed?.Invoke("选区存在重叠物体，请先移除重叠。"); return; }
            }
            if ((long)(maxX-minX)*(maxY-minY) != cells.Count)
            { ValidationFailed?.Invoke("请选满连续矩形，合并不能包含空格。"); return; }
            var merged = items[0].Clone();
            merged.id = PlanningDocument.NewId("box"); merged.x=minX; merged.y=minY; merged.width=maxX-minX; merged.height=maxY-minY;
            merged.isMerged = true; merged.mergeParts = new List<string>();
            foreach (var item in items)
            {
                if (item.isMerged && item.mergeParts != null)
                {
                    var parts=new List<PlanningBox>();
                    int originX=int.MaxValue,originY=int.MaxValue;
                    foreach(var json in item.mergeParts)
                    {
                        var part=JsonUtility.FromJson<PlanningBox>(json);
                        if(part==null) continue;
                        originX=Mathf.Min(originX,part.x); originY=Mathf.Min(originY,part.y); parts.Add(part);
                    }
                    foreach(var part in parts)
                    {
                        part.x+=item.x-originX; part.y+=item.y-originY;
                        merged.mergeParts.Add(JsonUtility.ToJson(part));
                    }
                }
                else merged.mergeParts.Add(JsonUtility.ToJson(item));
                owner.Remove(item);
            }
            owner.Add(merged); mergeSelection.Clear(); mergeSelection.Add(merged.id);
            selectedBoxId = merged.id;
            NotifyDocumentChanged(); SelectionChanged?.Invoke(); MarkDirtyRepaint();
            StatusChanged?.Invoke("已合并为单个物体。");
        }

        public void UnmergeSelectedItems()
        {
            var selected = new List<(List<PlanningBox> owner, PlanningBox box)>();
            foreach (var item in AllBoxes())
                if (mergeSelection.Contains(item.box.id) && item.box.isMerged) selected.Add((item.boxes,item.box));
            if (selected.Count == 0) { StatusChanged?.Invoke("请选择已合并物体。"); return; }
            mergeSelection.Clear();
            foreach (var item in selected)
            {
                int ox=int.MaxValue,oy=int.MaxValue;
                var restored = new List<PlanningBox>();
                foreach (string json in item.box.mergeParts)
                {
                    var part=JsonUtility.FromJson<PlanningBox>(json);
                    if (part == null) continue;
                    ox=Mathf.Min(ox,part.x); oy=Mathf.Min(oy,part.y); restored.Add(part);
                }
                if (restored.Count == 0) continue;
                item.owner.Remove(item.box);
                foreach(var part in restored)
                {
                    part.x += item.box.x-ox; part.y += item.box.y-oy;
                    item.owner.Add(part); mergeSelection.Add(part.id);
                }
            }
            NotifyDocumentChanged(); SelectionChanged?.Invoke(); MarkDirtyRepaint();
        }

        private void EditParametersAt(Vector2 position)
        {
            var cell=CellAt(position);
            var visibleItems=new List<(PlanningRoom room,List<PlanningBox> boxes,PlanningBox box)>(AllBoxes());
            visibleItems.Reverse();
            foreach (var item in visibleItems)
            {
                if (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId) continue;
                if (!DisplayRect(item.room,item.box).Contains(cell)) continue;
                var box=item.box;
                var prop=FindPropEntry(box.propEntryId,box.propEntryName);
                var entry=FindEntry(box.paletteEntryId,box.paletteEntryName);
                var source=box.type == PlanningDetailType.Prop ? prop?.Prefab : entry?.SourcePrefab;
                if (source == null) { StatusChanged?.Invoke("此物体没有可配置的预制体组件。"); return; }
                selectedBoxId=box.id;
                LevelEditorPropComponentEditorWindow.Open(source,
                    box.hasComponentOverrides ? box.componentOverrides : box.type == PlanningDetailType.Prop ? prop?.ComponentValueOverrides : entry?.ComponentValueOverrides,
                    values =>
                    {
                        // A stale parameter window must not edit an item removed or restored by history.
                        foreach (var current in AllBoxes())
                            if (current.box.id == box.id)
                            {
                                current.box.hasComponentOverrides=true;
                                current.box.componentOverrides=values;
                                NotifyDocumentChanged(); return;
                            }
                    }, "实例参数 · " + (source.name),
                    PlanningDoorUtility.IsDoor(box) ? () => AddDoorButton(box.id) : (System.Action)null,
                    "添加门按钮");
                SelectionChanged?.Invoke(); return;
            }
        }

        public void RemoveMissingSceneItems(IReadOnlyList<string> ids, IReadOnlyList<Vector2Int> cells)
        {
            bool changed=false;
            var targets=new List<(List<PlanningBox> owner,PlanningBox box)>();
            foreach(var item in AllBoxes()) targets.Add((item.boxes,item.box));
            foreach(var item in targets)
            {
                var removed=new HashSet<Vector2Int>();
                for(int i=0;i<ids.Count;i++) if(ids[i]==item.box.id) removed.Add(cells[i]);
                if(removed.Count==0) continue;
                item.owner.Remove(item.box); changed=true;
                if(item.box.isMerged || item.box.singleInstance || PlanningDoorUtility.IsDoor(item.box)) continue;
                for(int x=item.box.x;x<item.box.x+item.box.width;x++) for(int y=item.box.y;y<item.box.y+item.box.height;y++)
                {
                    if(removed.Contains(new Vector2Int(x,y))) continue;
                    var part=item.box.Clone(); part.id=PlanningDocument.NewId("box"); part.x=x;part.y=y;part.width=part.height=1;
                    item.owner.Add(part);
                }
            }
            if(changed) { NotifyDocumentChanged(); MarkDirtyRepaint(); }
        }
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
        private bool isPlacingRoom;

        internal int LastUnassignedSceneBlockCount { get; private set; }
        private bool hasHover;
        private Vector2Int hoveredCell;
        private readonly Dictionary<string, Image> detailPreviewImages =
            new Dictionary<string, Image>();
        private VisualElement playerOverlay;
        private Image playerThumbnail;
        private Label playerLabel;
        private Texture2D playerThumbnailTexture;
        private int playerThumbnailSourceId;
        private readonly PlanningPlayerDragGesture playerGesture = new PlanningPlayerDragGesture();
        private bool isDraggingPlayer => playerGesture.IsActive;
        private int playerPointerId => playerGesture.PointerId;
        private GameObject playerDragTarget;
        private float playerDragCellSize;
        private Vector3 playerDragBeforePosition;
        private float playerOverlayWidth = 54f;
        private float playerOverlayHeight = 74f;

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
        public LevelEditorPlacedBlock SelectedSceneBlock
        {
            get
            {
                if (!FindSelectedBox(
                        out PlanningRoom room,
                        out List<PlanningBox> _,
                        out PlanningBox box) ||
                    box == null)
                {
                    return null;
                }

                box.sceneBlock = ResolveSceneBlock(
                    room,
                    box);
                return box.sceneBlock;
            }
        }
        public float Zoom => zoom;
        public Vector2 Pan => pan;

        public void RefreshPreviewImages()
        {
            UpdateDetailPreviewImages();
        }

        public void RestoreView(Vector2 savedPan, float savedZoom)
        {
            if (float.IsNaN(savedPan.x) || float.IsNaN(savedPan.y) ||
                float.IsNaN(savedZoom))
            {
                return;
            }

            CancelPointerInteraction();
            pan = savedPan;
            zoom = Mathf.Clamp(savedZoom, MinimumZoom, MaximumZoom);
            UpdateDetailPreviewImages();
            UpdatePlayerOverlay();
            MarkDirtyRepaint();
        }

        public void RefreshPlayerOverlay()
        {
            if (isDraggingPlayer && (LevelEditorState.Tool != LevelEditorTool.Player ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                playerDragTarget == null || LevelEditorState.Player != playerDragTarget ||
                panel == null || !playerOverlay.HasPointerCapture(playerPointerId)))
                EndPlayerPointerDrag(playerPointerId, true);
            UpdatePlayerOverlay();
        }

        public void CancelPointerInteraction()
        {
            EndPointerAction();
        }

        public bool FocusPlayer()
        {
            CancelPointerInteraction();
            if (!TryGetPlayerPlanPosition(out Vector2 plan))
            {
                return false;
            }

            CenterOn(plan.x, plan.y);
            UpdatePlayerOverlay();
            MarkDirtyRepaint();
            return true;
        }

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
                        sceneBlock = block,
                        label = block.EntryName,
                        paletteEntryName = block.IsProp
                            ? string.Empty
                            : block.EntryName,
                        propEntryName = block.IsProp
                            ? block.EntryName
                            : string.Empty
                    };
                    if (!string.IsNullOrEmpty(block.PlanningBoxId)) box.id = block.PlanningBoxId;
                    if (block.GetComponent<Project.Mechanisms.DoorController>() != null)
                    {
                        box.propEntryId = PlanningDoorUtility.DoorEntryId;
                        box.singleInstance = true;
                        box.height = 2;
                        box.y -= 1;
                        if (!string.IsNullOrEmpty(block.PlanningBoxId)) box.id = block.PlanningBoxId;
                    }
                    if (block.GetComponent<Project.Mechanisms.DoorButton>() is Project.Mechanisms.DoorButton button)
                    {
                        box.propEntryId = PlanningDoorUtility.ButtonEntryId;
                        box.singleInstance = true;
                        if (!string.IsNullOrEmpty(block.PlanningBoxId)) box.id = block.PlanningBoxId;
                        box.doorOwnerId = button.Owner != null ? button.Owner.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId : null;
                    }
                    syncedBoxes.Add(box);
                }

                if (BoxesMatch(room.boxes, syncedBoxes))
                {
                    for (int boxIndex = 0;
                         boxIndex < room.boxes.Count;
                         boxIndex++)
                    {
                        room.boxes[boxIndex].sceneBlock =
                            syncedBoxes[boxIndex].sceneBlock;
                    }
                    continue;
                }

                room.boxes.Clear();
                room.boxes.AddRange(syncedBoxes);
                syncedRooms++;
            }

            IReadOnlyList<LevelEditorPlacedBlock> allBlocks =
                ProjectDiscovery.FindAll<LevelEditorPlacedBlock>(true);
            for (int index = 0; index < allBlocks.Count; index++)
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
            return FindSelectedBox(
                out PlanningRoom _,
                out ownerBoxes,
                out selected);
        }

        private bool FindSelectedBox(
            out PlanningRoom ownerRoom,
            out List<PlanningBox> ownerBoxes,
            out PlanningBox selected)
        {
            ownerRoom = null;
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
                        ownerRoom = room;
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

        private LevelEditorPlacedBlock ResolveSceneBlock(
            PlanningRoom room,
            PlanningBox box)
        {
            if (room == null ||
                box == null)
            {
                return null;
            }

            GameObject parent =
                PlanningSceneBuilder.FindRoomContainer(room);
            if (parent == null)
            {
                return null;
            }

            GetAssemblyStride(out int strideX, out int strideY);
            GetAssemblyRoomOffset(
                room,
                strideX,
                strideY,
                out int offsetX,
                out int offsetY,
                out _);
            int globalPlanX = box.x + offsetX;
            int globalPlanY = box.y + offsetY;
            Vector2Int sceneCell = new Vector2Int(
                globalPlanX,
                -globalPlanY - 1);

            LevelEditorPlacedBlock[] blocks =
                parent.GetComponentsInChildren<
                    LevelEditorPlacedBlock>(true);
            for (int index = 0; index < blocks.Length; index++)
            {
                LevelEditorPlacedBlock block = blocks[index];
                if (block == null || !block.ContainsCell(sceneCell))
                {
                    continue;
                }

                bool expectsProp =
                    box.type == PlanningDetailType.Prop;
                if (block.IsProp != expectsProp)
                {
                    continue;
                }

                string expectedName = expectsProp
                    ? box.propEntryName
                    : box.paletteEntryName;
                if (!string.IsNullOrEmpty(expectedName) &&
                    block.EntryName != expectedName)
                {
                    continue;
                }

                return block;
            }

            return null;
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
            RegisterCallback<DetachFromPanelEvent>(_ => CancelPointerInteraction());
            BuildPlayerOverlay();
            schedule.Execute(RefreshRegionLabels).Every(100);
        }

        private void BuildPlayerOverlay()
        {
            playerOverlay = new VisualElement
            {
                name = "PlanningPlayerOverlay",
                pickingMode = PickingMode.Position
            };
            playerOverlay.style.position = Position.Absolute;
            playerOverlay.style.alignItems = Align.Center;
            playerOverlay.style.justifyContent = Justify.FlexStart;
            playerOverlay.tooltip =
                "使用玩家工具拖动贴纸，位置同步到场景；视口外的玩家可通过聚焦玩家定位。";

            playerThumbnail = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            playerThumbnail.style.flexShrink = 0f;
            playerThumbnail.style.backgroundColor =
                new Color(.05f, .08f, .12f, .82f);
            playerThumbnail.style.borderLeftWidth = 2f;
            playerThumbnail.style.borderRightWidth = 2f;
            playerThumbnail.style.borderTopWidth = 2f;
            playerThumbnail.style.borderBottomWidth = 2f;
            playerThumbnail.style.borderLeftColor =
                playerThumbnail.style.borderRightColor =
                playerThumbnail.style.borderTopColor =
                playerThumbnail.style.borderBottomColor =
                    new Color(.35f, .85f, 1f, .95f);

            playerLabel = new Label("玩家")
            {
                pickingMode = PickingMode.Ignore
            };
            playerLabel.style.whiteSpace = WhiteSpace.NoWrap;
            playerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            playerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            playerLabel.style.color = Color.white;
            playerLabel.style.backgroundColor =
                new Color(.02f, .04f, .06f, .72f);
            playerLabel.style.paddingLeft = 4f;
            playerLabel.style.paddingRight = 4f;
            playerLabel.style.paddingTop = 1f;
            playerLabel.style.paddingBottom = 1f;
            playerLabel.style.marginTop = 2f;

            playerOverlay.Add(playerThumbnail);
            playerOverlay.Add(playerLabel);
            playerOverlay.RegisterCallback<PointerDownEvent>(
                OnPlayerPointerDown);
            playerOverlay.RegisterCallback<PointerMoveEvent>(
                OnPlayerPointerMove);
            playerOverlay.RegisterCallback<PointerUpEvent>(
                OnPlayerPointerUp);
            playerOverlay.RegisterCallback<PointerCancelEvent>(
                OnPlayerPointerCancel);
            playerOverlay.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (isDraggingPlayer && evt.pointerId == playerPointerId)
                    EndPlayerPointerDrag(evt.pointerId, true);
            });
            playerOverlay.style.display = DisplayStyle.None;
            Add(playerOverlay);
        }

        private void UpdatePlayerOverlay()
        {
            if (playerOverlay == null)
            {
                return;
            }

            playerOverlay.pickingMode = LevelEditorState.Tool == LevelEditorTool.Player &&
                !EditorApplication.isPlayingOrWillChangePlaymode
                ? PickingMode.Position : PickingMode.Ignore;

            if (!TryGetPlayerPlanPosition(out Vector2 plan))
            {
                playerOverlay.style.display = DisplayStyle.None;
                return;
            }

            if (contentRect.width <= 0f ||
                contentRect.height <= 0f)
            {
                playerOverlay.style.display = DisplayStyle.None;
                return;
            }

            GameObject player = LevelEditorState.Player;
            UpdatePlayerThumbnail(player);

            float iconSize = Mathf.Clamp(
                GridSize * 1.35f,
                42f,
                78f);
            float labelHeight = Mathf.Clamp(
                iconSize * .28f,
                12f,
                18f);
            playerOverlayWidth = iconSize + 8f;
            playerOverlayHeight = iconSize + labelHeight + 9f;
            playerOverlay.style.width = playerOverlayWidth;
            playerOverlay.style.height = playerOverlayHeight;
            playerThumbnail.style.width = iconSize;
            playerThumbnail.style.height = iconSize;
            playerLabel.style.fontSize = Mathf.Clamp(
                iconSize * .24f,
                10f,
                14f);
            playerLabel.style.height = labelHeight;

            Vector2 center = WorldToScreen(plan);
            float halfWidth = playerOverlayWidth * .5f;
            float halfHeight = playerOverlayHeight * .5f;
            float minX = halfWidth + 2f;
            float maxX = Mathf.Max(
                minX,
                contentRect.width - halfWidth - 2f);
            float minY = halfHeight + 2f;
            float maxY = Mathf.Max(
                minY,
                contentRect.height - halfHeight - 2f);
            float clampedX = Mathf.Clamp(center.x, minX, maxX);
            float clampedY = Mathf.Clamp(center.y, minY, maxY);
            playerOverlay.style.left = clampedX - halfWidth;
            playerOverlay.style.top = clampedY - halfHeight;
            playerOverlay.style.display = DisplayStyle.Flex;
            playerOverlay.BringToFront();
        }

        private void UpdatePlayerThumbnail(GameObject player)
        {
            if (playerThumbnail == null || player == null)
            {
                return;
            }

            int sourceId = player.GetInstanceID();
            if (playerThumbnailTexture != null &&
                playerThumbnailSourceId == sourceId)
            {
                return;
            }

            playerThumbnailSourceId = sourceId;
            GameObject source =
                PrefabUtility.GetCorrespondingObjectFromSource(player) ??
                player;
            Texture2D preview = AssetPreview.GetAssetPreview(source);
            preview ??= AssetPreview.GetMiniThumbnail(source);
            playerThumbnailTexture = preview;
            playerThumbnail.image = preview;

            if (preview == null &&
                AssetPreview.IsLoadingAssetPreview(source.GetInstanceID()))
            {
                playerOverlay.schedule.Execute(() =>
                {
                    playerThumbnailSourceId = 0;
                    UpdatePlayerOverlay();
                }).StartingIn(120);
            }
        }

        private bool TryGetPlayerPlanPosition(out Vector2 plan)
        {
            plan = default;
            if (document == null || mode == PlanningCanvasMode.World)
            {
                return false;
            }

            GameObject player = LevelEditorState.Player;
            if (player == null || !player.scene.IsValid())
            {
                return false;
            }

            Vector3 position = player.transform.position;
            var globalPlan = new Vector2(
                position.x,
                -position.y) / Mathf.Max(.05f, LevelEditorState.CellSize);
            if (mode == PlanningCanvasMode.Assembly)
            {
                plan = globalPlan;
                return true;
            }

            PlanningRoom room = document.FindRoom(selectedRoomId);
            if (room == null)
            {
                return false;
            }

            if (room.isConnector)
            {
                PlanningCell origin = GetConnectorOrigin(room);
                plan = globalPlan - new Vector2(
                    origin.x * WorldBlockCellWidth,
                    origin.y * WorldBlockCellHeight);
                return true;
            }

            GetAssemblyStride(out int strideX, out int strideY);
            GetAssemblyRoomOffset(
                room,
                strideX,
                strideY,
                out int offsetX,
                out int offsetY,
                out _);
            plan = globalPlan - new Vector2(offsetX, offsetY);
            return true;
        }

        private void SetPlayerFromGlobalPlanPosition(
            Vector2 globalPlan,
            bool recordUndo)
        {
            var worldPosition = new Vector3(
                globalPlan.x * playerDragCellSize,
                -globalPlan.y * playerDragCellSize,
                0f);
            if (LevelEditorPlayerService.SetPlayerPosition(
                    worldPosition,
                    recordUndo))
            {
                UpdatePlayerOverlay();
                SceneView.RepaintAll();
            }
        }

        private void OnPlayerPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || LevelEditorState.Tool != LevelEditorTool.Player ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                !playerOverlay.worldBound.Contains(evt.position))
            {
                return;
            }

            Vector2 pointerPosition =
                this.WorldToLocal(evt.position);
            if (!TryGetPlayerPlanPosition(out _))
            {
                return;
            }

            EndPointerAction();
            Focus();
            playerDragTarget = LevelEditorState.Player;
            playerDragBeforePosition =
                playerDragTarget != null
                    ? playerDragTarget.transform.position
                    : Vector3.zero;
            playerDragCellSize = Mathf.Max(.05f, LevelEditorState.CellSize);
            playerGesture.Begin(evt.pointerId, pointerPosition,
                new Vector2(playerDragBeforePosition.x, -playerDragBeforePosition.y) / playerDragCellSize,
                GridSize);
            PointerCaptureHelper.CapturePointer(
                playerOverlay,
                evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPlayerPointerMove(PointerMoveEvent evt)
        {
            if (!isDraggingPlayer ||
                evt.pointerId != playerPointerId)
            {
                return;
            }

            if (LevelEditorState.Tool != LevelEditorTool.Player ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                playerDragTarget == null || LevelEditorState.Player != playerDragTarget ||
                !playerOverlay.HasPointerCapture(evt.pointerId) || (evt.pressedButtons & 1) == 0)
            {
                EndPlayerPointerDrag(evt.pointerId, true);
                return;
            }

            Vector2 pointerPosition =
                this.WorldToLocal(evt.position);
            if (playerGesture.TryMove(evt.pointerId, playerOverlay.HasPointerCapture(evt.pointerId),
                    (evt.pressedButtons & 1) != 0, pointerPosition, out Vector2 pointerPlan))
            {
                SetPlayerFromGlobalPlanPosition(
                    pointerPlan,
                    false);
            }

            evt.StopPropagation();
        }

        private void OnPlayerPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0 || !isDraggingPlayer ||
                evt.pointerId != playerPointerId)
            {
                return;
            }

            EndPlayerPointerDrag(evt.pointerId, true);
            evt.StopPropagation();
        }

        private void OnPlayerPointerCancel(PointerCancelEvent evt)
        {
            if (!isDraggingPlayer ||
                evt.pointerId != playerPointerId)
            {
                return;
            }

            EndPlayerPointerDrag(evt.pointerId, true);
            evt.StopPropagation();
        }

        private void EndPlayerPointerDrag(
            int pointerId,
            bool recordUndo)
        {
            if (!isDraggingPlayer || pointerId != playerPointerId) return;
            GameObject target = playerDragTarget;
            Vector3 beforePosition = playerDragBeforePosition;
            // Clear ownership before releasing capture: capture-out can be synchronous.
            playerGesture.End();
            playerDragTarget = null;
            if (pointerId >= 0 && playerOverlay.HasPointerCapture(pointerId))
            {
                PointerCaptureHelper.ReleasePointer(
                    playerOverlay,
                    pointerId);
            }

            if (recordUndo &&
                !EditorApplication.isPlayingOrWillChangePlaymode && target != null &&
                LevelEditorState.Player == target)
            {
                if (target.transform.position != beforePosition)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.scene);
                    NotifyDocumentChanged();
                }
            }

            UpdatePlayerOverlay();
        }

        public void SetDocument(PlanningDocument value)
        {
            CancelPointerInteraction();
            worldSelection.Clear();
            regionSelection.Clear();
            document = value;
            document?.Normalize();
            selectedRoomId = document != null && document.rooms.Count > 0
                ? document.rooms[0].id
                : null;
            if (selectedRoomId != null) worldSelection.Add(selectedRoomId);
            selectedBoxId = null;
            mergeSelection.Clear();
            pan = new Vector2(-90f, -90f);
            zoom = 1f;
            UpdateDetailPreviewImages();
            UpdatePlayerOverlay();
            MarkDirtyRepaint();
            SelectionChanged?.Invoke();
        }

        public void BeginRoomPlacement()
        {
            SetMapTool(PlanningMapTool.Paint);
            isPlacingRoom = true;
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
                NextRoomName());
            room.cells.Add(new PlanningCell(cell.x, cell.y));
            document.rooms.Add(room);
            selectedRoomId = room.id;
            worldSelection.Clear(); worldSelection.Add(room.id); regionSelection.Clear(); selectedRegionId = null;
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
            EndPointerAction();
            isPlacingRoom = false;
            mode = value;
            selectedBoxId = null;
            mergeSelection.Clear();
            UpdateDetailPreviewImages();
            UpdatePlayerOverlay();
            MarkDirtyRepaint();
            SelectionChanged?.Invoke();
        }

        public void SetMapTool(PlanningMapTool value)
        {
            EndPointerAction();
            isPlacingRoom = false;
            mapTool = value;
            isDragging = false;
            lastPaintCell = new Vector2Int(int.MinValue, int.MinValue);
            MarkDirtyRepaint();
        }

        public void SetDetailTool(PlanningDetailTool value)
        {
            EndPointerAction();
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
                case LevelEditorTool.Merge:
                    return PlanningDetailTool.Merge;
                case LevelEditorTool.Parameters:
                    return PlanningDetailTool.Parameters;
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

        public void SelectRoom(string roomId, bool focus = false, bool additive = false)
        {
            if (isDraggingPlayer) EndPlayerPointerDrag(playerPointerId, true);
            selectedRegionId = null;
            if (!additive) { worldSelection.Clear(); regionSelection.Clear(); }
            bool removed = roomId != null && !worldSelection.Add(roomId) && additive;
            if (removed) worldSelection.Remove(roomId);
            mergeSelection.Clear();
            selectedRoomId = removed ? null : roomId;
            if (removed) foreach (string id in worldSelection) { selectedRoomId = id; break; }
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
            if (isDraggingPlayer) EndPlayerPointerDrag(playerPointerId, true);
            PlanningRoom room = document?.FindRoom(roomId);
            if (room == null)
            {
                return;
            }

            if (mode == PlanningCanvasMode.Assembly)
            {
                RectInt assemblyBounds = PlanningWorldUtility.Bounds(room);
                var fineBounds = new RectInt(assemblyBounds.x * WorldBlockCellWidth, assemblyBounds.y * WorldBlockCellHeight,
                    assemblyBounds.width * WorldBlockCellWidth, assemblyBounds.height * WorldBlockCellHeight);
                foreach (var box in room.boxes)
                {
                    var r = GetAssemblyBoxRect(room, box, WorldBlockCellWidth, WorldBlockCellHeight);
                    fineBounds = new RectInt(Mathf.Min(fineBounds.x, r.x), Mathf.Min(fineBounds.y, r.y),
                        Mathf.Max(fineBounds.xMax, r.xMax) - Mathf.Min(fineBounds.x, r.x),
                        Mathf.Max(fineBounds.yMax, r.yMax) - Mathf.Min(fineBounds.y, r.y));
                }
                ApplyDetailView(fineBounds, fineBounds.center);
                UpdateDetailPreviewImages(); UpdatePlayerOverlay(); MarkDirtyRepaint();
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
            Vector2 focus = room.boxes.Count == 0 ? bounds.center : CalculateDensestCenter(cells, bounds);
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

            RectInt emptyBounds = room.GetLocalAllowedRect(WorldBlockCellWidth, WorldBlockCellHeight);
            cells.Add(emptyBounds.position);
            cells.Add(new Vector2Int(emptyBounds.xMax - 1, emptyBounds.yMax - 1));

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
            if (document == null) return;
            bool hasBounds = false;
            RectInt bounds = default;
            foreach (var room in document.rooms)
            {
                foreach (var cell in room.cells)
                    Encapsulate(new RectInt(cell.x * WorldBlockCellWidth, cell.y * WorldBlockCellHeight, WorldBlockCellWidth, WorldBlockCellHeight), ref bounds, ref hasBounds);
                foreach (var box in room.boxes)
                    Encapsulate(GetAssemblyBoxRect(room, box, WorldBlockCellWidth, WorldBlockCellHeight), ref bounds, ref hasBounds);
            }
            foreach (var box in document.assemblyPatches)
                Encapsulate(new RectInt(box.x, box.y, box.width, box.height), ref bounds, ref hasBounds);
            if (!hasBounds) return;
            ApplyDetailView(bounds, bounds.center);
            UpdateDetailPreviewImages(); UpdatePlayerOverlay(); MarkDirtyRepaint();
        }

        private static void Encapsulate(RectInt rect, ref RectInt bounds, ref bool hasBounds)
        {
            if (!hasBounds) { bounds = rect; hasBounds = true; return; }
            int x = Mathf.Min(bounds.x, rect.x), y = Mathf.Min(bounds.y, rect.y);
            bounds = new RectInt(x, y, Mathf.Max(bounds.xMax, rect.xMax) - x, Mathf.Max(bounds.yMax, rect.yMax) - y);
        }

        private void CenterOn(float centerX, float centerY)
        {
            pan = new Vector2(
                contentRect.width * .5f -
                centerX * GridSize,
                contentRect.height * .5f -
                centerY * GridSize);
            UpdatePlayerOverlay();
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
            if (mode != PlanningCanvasMode.World && GetActiveDetailTool() == PlanningDetailTool.Merge)
            {
                foreach(var item in AllBoxes())
                {
                    if (!mergeSelection.Contains(item.box.id) || (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId)) continue;
                    var r=DisplayRect(item.room,item.box);
                    StrokeRect(context.painter2D, CellRect(r.x,r.y,r.width,r.height),new Color(.3f,.85f,1f),3f);
                }
                DrawRectanglePreview(context.painter2D);
            }
        }

        private void DrawAssembly(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            DrawAssemblyOwnership(painter);
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
            DrawDoorLinks(painter);
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
            if (connector.independentCells)
            {
                PlanningLayoutUtility.GetConnectorOrigin(connector, strideX, strideY, out originX, out originY);
                return;
            }
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

        private RectInt GetConnectorDetailBounds(PlanningRoom connector)
        {
            return connector.GetLocalAllowedRect(WorldBlockCellWidth, WorldBlockCellHeight);
        }

        private void DrawWorld(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            DrawWorldRegions(painter);
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
                bool selected = worldSelection.Contains(room.id) || room.id == selectedRoomId;
                color.a = selected ? .78f : .5f;
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
                        selected
                            ? Color.white
                            : new Color(0f, 0f, 0f, .55f),
                        selected ? 2f : 1f);
                }
            }

            DrawConnectors(painter);
            DrawDoors(painter);
            DrawKeys(painter);
            DrawRectanglePreview(painter);
            DrawHoverCell(painter);
            DrawRoomPlacementPreview(painter);
            DrawWorldMovePreview(painter);
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
            DrawDetailOwnership(painter, room);

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

            DrawDoorLinks(painter);
            DrawRectanglePreview(painter);
            DrawHoverCell(painter);
        }

        private void UpdateDetailPreviewImages()
        {
            if (document == null)
            {
                HideDetailPreviewImages();
                UpdatePlayerOverlay();
                return;
            }

            if (mode == PlanningCanvasMode.Assembly)
            {
                UpdateAssemblyPreviewImages();
                UpdatePlayerOverlay();
                return;
            }

            if (mode != PlanningCanvasMode.Detail)
            {
                HideDetailPreviewImages();
                UpdatePlayerOverlay();
                return;
            }

            PlanningRoom room = document.FindRoom(selectedRoomId);
            if (room == null)
            {
                HideDetailPreviewImages();
                UpdatePlayerOverlay();
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
                     y < box.y + (box.isMerged ? 1 : box.height);
                     y++)
                {
                    for (int x = box.x;
                         x < box.x + (box.isMerged ? 1 : box.width);
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

                        Rect rect = CellRect(x, y, box.isMerged ? box.width : 1, box.isMerged ? box.height : 1);
                        image.image = preview;
                        image.tintColor=mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge ? new Color(.55f,.85f,1f) : Color.white;
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

            UpdatePlayerOverlay();
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

            for (int y = rect.y; y < (box.isMerged ? rect.y+1 : rect.yMax); y++)
            {
                for (int x = rect.x; x < (box.isMerged ? rect.x+1 : rect.xMax); x++)
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

                    Rect cellRect = CellRect(x, y, box.isMerged ? rect.width : 1, box.isMerged ? rect.height : 1);
                    image.image = preview;
                    image.tintColor=mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge ? new Color(.55f,.85f,1f) : Color.white;
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
            if (mode == PlanningCanvasMode.World && worldMoving) return;
            if (!isDragging)
            {
                return;
            }

            Vector2Int start = CellAt(dragStart);
            if (IsPlacingDoor)
            {
                DrawDoorPlacementPreview(painter, start);
                return;
            }
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

            if (IsPlacingDoor)
            {
                if (!isDragging) DrawDoorPlacementPreview(painter, hoveredCell);
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

        private bool IsPlacingDoor => mode != PlanningCanvasMode.World &&
            GetActiveDetailTool() == PlanningDetailTool.Box && detailPropEntryId == PlanningDoorUtility.DoorEntryId;

        private void DrawDoorPlacementPreview(Painter2D painter, Vector2Int cell)
        {
            PlanningRoom room = document?.FindRoom(selectedRoomId);
            Vector2Int local = cell;
            if (mode == PlanningCanvasMode.Assembly)
            {
                GetAssemblyStride(out int sx, out int sy);
                if (!TryFindAssemblyOwner(cell, sx, sy, out room, out local)) room = null;
            }
            bool valid = false;
            if (room != null)
            {
                RectInt allowed = room.isConnector ? GetConnectorDetailBounds(room) :
                    room.GetLocalAllowedRect(WorldBlockCellWidth, WorldBlockCellHeight);
                var door = new PlanningBox(PlanningDetailType.Prop, new RectInt(local.x, local.y, 1, 2))
                    { propEntryId = PlanningDoorUtility.DoorEntryId };
                valid = allowed.Contains(local) && allowed.Contains(local + Vector2Int.up) &&
                    PlanningDoorUtility.CanPlace(room.boxes, new RectInt(local.x, local.y, 1, 2)) &&
                    PlanningDoorUtility.TryFindButtonCell(room.boxes, door, allowed, out _);
            }
            Rect footprint = CellRect(cell.x, cell.y, 1, 2);
            Color color = valid ? new Color(.35f, .9f, .65f, 1f) : new Color(1f, .35f, .3f, 1f);
            FillRect(painter, footprint, new Color(color.r, color.g, color.b, .2f), 1f);
            StrokeRect(painter, footprint, color, 2f);
            DrawLine(painter, new Vector2(footprint.xMin, footprint.center.y),
                new Vector2(footprint.xMax, footprint.center.y), color, 1f);
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

                if (box.isMerged)
                    return LevelEditorDecorationService.GetMergedFrontPreview(null, prop.Prefab,
                        new Vector2Int(box.width, box.height),
                        box.hasComponentOverrides ? box.componentOverrides : prop.ComponentValueOverrides);

                Texture2D assetPreview =
                    AssetPreview.GetAssetPreview(prop.Prefab);
                return assetPreview != null
                    ? assetPreview
                    : AssetPreview.GetMiniThumbnail(prop.Prefab);
            }

            LevelEditorBlockEntry entry = FindEntry(
                box.paletteEntryId,
                box.paletteEntryName);
            if (box.isMerged && entry != null)
                return LevelEditorDecorationService.GetMergedFrontPreview(entry, null,
                    new Vector2Int(box.width, box.height),
                    box.hasComponentOverrides ? box.componentOverrides : entry.ComponentValueOverrides);
            return entry != null
                ? LevelEditorDecorationService
                    .GetEntryFrontPreview(entry, 64)
                : null;
        }

        private LevelEditorPropEntry FindPropEntry(
            string entryId,
            string entryName)
        {
            if (entryId == PlanningDoorUtility.ButtonEntryId) return PlanningDoorUtility.GetButtonEntry();
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

            if (isDraggingPlayer) EndPlayerPointerDrag(playerPointerId, true);
            Vector2 pointer = evt.localMousePosition;
            Vector2 before = ScreenToWorld(pointer);
            zoom = Mathf.Clamp(
                zoom * Mathf.Pow(.92f, evt.delta.y),
                MinimumZoom,
                MaximumZoom);
            Vector2 after = ScreenToWorld(pointer);
            pan += (after - before) * GridSize;
            UpdateDetailPreviewImages();
            UpdatePlayerOverlay();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (isDraggingPlayer) EndPlayerPointerDrag(playerPointerId, true);
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

            if (evt.button == 1 ||
                evt.button == 2 ||
                (mode == PlanningCanvasMode.World && mapTool == PlanningMapTool.Pan) ||
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
                HandleWorldPointerDown(position, evt.ctrlKey || evt.commandKey || evt.shiftKey);
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
                UpdatePlayerOverlay();
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
            if (isDraggingPlayer) EndPlayerPointerDrag(playerPointerId, true);
            if (suppressDocumentChanged) EndDocumentBatch();
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
            worldMoving = false;
            lastPaintCell = new Vector2Int(int.MinValue, int.MinValue);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (mode == PlanningCanvasMode.World && evt.keyCode == KeyCode.Delete)
            {
                DeleteWorldSelection(); evt.StopPropagation(); return;
            }
            if (evt.keyCode == KeyCode.Escape)
            {
                if (isPlacingRoom)
                {
                    CancelRoomPlacement();
                    evt.StopPropagation();
                    return;
                }

                EndPointerAction();
                selectedBoxId = null;
                mergeSelection.Clear();
                worldSelection.Clear(); regionSelection.Clear(); selectedRegionId = null;
                if (mode == PlanningCanvasMode.World) selectedRoomId = null;
                SelectionChanged?.Invoke();
                MarkDirtyRepaint();
            }
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
            if (activeTool == PlanningDetailTool.Parameters) { EditParametersAt(position); return; }
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
            int targetX = Mathf.Clamp(
                movingBoxOrigin.x + cellDeltaX,
                allowed.x,
                allowed.xMax - movingBox.width);
            int targetY = Mathf.Clamp(
                movingBoxOrigin.y + cellDeltaY,
                allowed.y,
                allowed.yMax - movingBox.height);
            if (!CanPlaceLocalRect(room, new RectInt(targetX, targetY, movingBox.width, movingBox.height))) return;
            movingBox.x = targetX; movingBox.y = targetY;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        private void CompleteDetailDrag()
        {
            PlanningDetailTool activeTool = GetActiveDetailTool();
            if (activeTool == PlanningDetailTool.Merge) { CompleteMergeSelection(); return; }
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
                if (IsPlacingDoor)
                {
                    minX = maxX = start.x;
                    minY = start.y;
                    maxY = start.y + 1;
                }
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
                if (!PrepareDoorPlacement(room, box, allowed)) return;
                if (!CanPlaceLocalRect(room, new RectInt(box.x, box.y, box.width, box.height)))
                { StatusChanged?.Invoke("选区超出当前房间或通道占格。"); return; }
                room.boxes.Add(box);
                if (PlanningDoorUtility.IsDoor(box)) PlanningDoorUtility.AddButton(room.boxes, box, allowed, c => CanPlaceLocalRect(room, new RectInt(c, Vector2Int.one)));
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
                        NotifyDocumentChanged();
                    }
                }

                EndDocumentBatch();
                SelectionChanged?.Invoke();
            }
        }

        private void HandleAssemblyPointerDown(Vector2 position)
        {
            if (GetActiveDetailTool() == PlanningDetailTool.Parameters) { EditParametersAt(position); return; }
            Vector2Int cell = CellAt(position);
            PlanningDetailTool activeTool = GetActiveDetailTool();
            if (activeTool == PlanningDetailTool.Select)
            {
                movingBox = null;
                var items = new List<(PlanningRoom room, List<PlanningBox> boxes, PlanningBox box)>(AllBoxes());
                items.Reverse();
                foreach (var item in items)
                {
                    if (!DisplayRect(item.room, item.box).Contains(cell)) continue;
                    selectedRoomId = item.room?.id; selectedBoxId = item.box.id;
                    movingBox = item.box; movingBoxStart = position;
                    movingBoxOrigin = new Vector2Int(item.box.x, item.box.y);
                    break;
                }
                SelectionChanged?.Invoke();
            }
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
            if (activeTool == PlanningDetailTool.Select && movingBox != null)
            {
                var owner = document.FindRoom(selectedRoomId);
                Vector2Int delta = CellAt(position) - CellAt(movingBoxStart);
                Vector2Int target = movingBoxOrigin + delta;
                if (owner != null && !CanPlaceLocalRect(owner, new RectInt(target.x, target.y, movingBox.width, movingBox.height))) return;
                movingBox.x = target.x; movingBox.y = target.y;
                UpdateDetailPreviewImages(); MarkDirtyRepaint(); return;
            }
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
            if (activeTool == PlanningDetailTool.Merge) { CompleteMergeSelection(); return; }
            if (activeTool == PlanningDetailTool.Select)
            {
                if (movingBox != null && movingBoxOrigin != new Vector2Int(movingBox.x, movingBox.y)) NotifyDocumentChanged();
                SelectionChanged?.Invoke(); return;
            }
            if (activeTool != PlanningDetailTool.Box && activeTool != PlanningDetailTool.Erase) return;
            if (IsPlacingDoor)
            {
                AddAssemblyBox(assemblyStrokeStart);
                return;
            }
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
                if (!CanPlaceLocalRect(owner, new RectInt(box.x, box.y, box.width, IsPlacingDoor ? 2 : box.height))) return;
                if (owner.boxes.Exists(b => b.x == box.x && b.y == box.y && SameKind(b, box))) return;
                RectInt allowed = owner.GetLocalAllowedRect(WorldBlockCellWidth, WorldBlockCellHeight);
                if (owner.isConnector) allowed = GetConnectorDetailBounds(owner);
                if (!PrepareDoorPlacement(owner, box, allowed)) return;
                owner.boxes.Add(box);
                if (PlanningDoorUtility.IsDoor(box)) PlanningDoorUtility.AddButton(owner.boxes, box, allowed, c => CanPlaceLocalRect(owner, new RectInt(c, Vector2Int.one)));
            }
            else
            {
                StatusChanged?.Invoke("请在房间或通道的占格内放置物体；在世界图可扩展占格。");
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
                CapturePlacementDefaults(box);
                return;
            }

            box.propEntryId = string.Empty;
            box.propEntryName = string.Empty;
            box.paletteEntryId = detailEntryId;
            box.paletteEntryName = detailEntryName;
            CapturePlacementDefaults(box);
        }

        private bool PrepareDoorPlacement(PlanningRoom room, PlanningBox box, RectInt allowed)
        {
            if (!PlanningDoorUtility.IsDoor(box)) return true;
            box.width = 1;
            box.height = 2;
            box.singleInstance = true;
            if (!allowed.Contains(new Vector2Int(box.x, box.y)) || !allowed.Contains(new Vector2Int(box.x, box.y + 1)) ||
                !PlanningDoorUtility.CanPlace(room.boxes, new RectInt(box.x, box.y, 1, 2)))
            { StatusChanged?.Invoke("门需要连续的 1×2 空格。"); return false; }
            if (!PlanningDoorUtility.TryFindButtonCell(room.boxes, box, allowed, out _, c => CanPlaceLocalRect(room, new RectInt(c, Vector2Int.one))))
            { StatusChanged?.Invoke("门附近没有可放置按钮的空格。"); return false; }
            return true;
        }

        private void AddDoorButton(string doorId)
        {
            foreach (var item in AllBoxes())
            {
                if (item.box.id != doorId || !PlanningDoorUtility.IsDoor(item.box) || item.room == null) continue;
                RectInt allowed = item.room.isConnector ? GetConnectorDetailBounds(item.room) :
                    item.room.GetLocalAllowedRect(WorldBlockCellWidth, WorldBlockCellHeight);
                if (PlanningDoorUtility.AddButton(item.boxes, item.box, allowed, c => CanPlaceLocalRect(item.room, new RectInt(c, Vector2Int.one))) == null)
                { StatusChanged?.Invoke("门附近没有可放置按钮的空格。"); return; }
                NotifyDocumentChanged(); SelectionChanged?.Invoke(); MarkDirtyRepaint();
                StatusChanged?.Invoke("已添加门按钮，可在选择模式拖动位置。");
                return;
            }
            StatusChanged?.Invoke("此门已删除，请重新选择。");
        }

        private void DrawDoorLinks(Painter2D painter)
        {
            var items = new List<(PlanningRoom room, List<PlanningBox> boxes, PlanningBox box)>(AllBoxes());
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.box.doorOwnerId) ||
                    (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId)) continue;
                foreach (var door in items)
                {
                    if (door.box.id != item.box.doorOwnerId) continue;
                    RectInt a = DisplayRect(item.room, item.box), b = DisplayRect(door.room, door.box);
                    Vector2 start = CellRect(a.x, a.y, a.width, a.height).center;
                    Vector2 end = CellRect(b.x, b.y, b.width, b.height).center;
                    float length = Vector2.Distance(start, end);
                    for (float d = 0; d < length; d += 12f)
                        DrawLine(painter, Vector2.Lerp(start,end,d/length), Vector2.Lerp(start,end,Mathf.Min(d+6f,length)/length),
                            new Color(1f,.78f,.3f,.9f), 2f);
                    break;
                }
            }
        }

        private void CapturePlacementDefaults(PlanningBox box)
        {
            var values = box.type == PlanningDetailType.Prop
                ? FindPropEntry(box.propEntryId,box.propEntryName)?.ComponentValueOverrides
                : FindEntry(box.paletteEntryId,box.paletteEntryName)?.ComponentValueOverrides;
            box.hasComponentOverrides=true;
            box.componentOverrides=new List<LevelEditorComponentValueOverride>();
            if(values != null) foreach(var value in values) if(value != null) box.componentOverrides.Add(value.Clone());
        }

        public void FreezeExistingDefaults()
        {
            foreach(var item in AllBoxes()) if(!item.box.hasComponentOverrides) CapturePlacementDefaults(item.box);
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

                    var rect=GetAssemblyBoxRect(owner,box,strideX,strideY);
                    CarveBox(owner.boxes, box, cell-rect.position+new Vector2Int(box.x,box.y));
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
            if (PlanningDoorUtility.IsDoor(box)) boxes.RemoveAll(item => item.doorOwnerId == box.id);
            if (box.isMerged || box.singleInstance || !string.IsNullOrEmpty(box.doorOwnerId)) return;
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
            carved.hasComponentOverrides = source.hasComponentOverrides;
            carved.componentOverrides = source.Clone().componentOverrides;
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

            document?.RefreshConnectorPaths();
            if (document != null) PlanningDoorUtility.RemoveOrphanButtons(document);
            DocumentChanged?.Invoke();
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证规划数据逻辑")]
        private static void ValidatePlanningOperations()
        {
            // Isolated in-memory document: no scene, palette asset or preference is modified.
            var testDocument = new PlanningDocument();
            var room = new PlanningRoom("验证");
            room.cells.Add(new PlanningCell(0,0));
            testDocument.rooms.Add(room);
            var first = new PlanningBox(PlanningDetailType.Prop,new RectInt(0,0,1,1)) { propEntryId="test",propEntryName="验证道具",hasComponentOverrides=true };
            var second = new PlanningBox(PlanningDetailType.Prop,new RectInt(1,0,1,1)) { propEntryId="test",propEntryName="验证道具" };
            room.boxes.Add(first); room.boxes.Add(second);
            var testCanvas=new PlanningCanvas();
            testCanvas.SetDocument(testDocument);
            testCanvas.SetMode(PlanningCanvasMode.Detail);
            int changes=0;
            testCanvas.DocumentChanged+=()=>changes++;
            testCanvas.ToggleMergeArea(new RectInt(0,0,2,1));
            testCanvas.ToggleMergeArea(new RectInt(0,0,1,1));
            Require(testCanvas.mergeSelection.Count==1,"点选取消");
            testCanvas.ToggleMergeArea(new RectInt(0,0,1,1));
            testCanvas.MergeSelectedItems();
            Require(room.boxes.Count==1 && room.boxes[0].isMerged && room.boxes[0].width==2,"道具合并");
            var merged=room.boxes[0];
            Require(merged.mergeParts.Count==2 && merged.hasComponentOverrides,"原始数据与独立参数保存");
            var roundTrip=JsonUtility.FromJson<PlanningDocument>(JsonUtility.ToJson(testDocument));
            Require(roundTrip.rooms[0].boxes[0].mergeParts.Count==2,"存档往返");
            merged.x=4; merged.y=3;
            testCanvas.UnmergeSelectedItems();
            Require(room.boxes.Count==2 && room.boxes[0].x==4 && room.boxes[1].x==5 && room.boxes[0].y==3,"移动后解除合并");
            testCanvas.RemoveMissingSceneItems(new []{ room.boxes[0].id },new []{new Vector2Int(4,3)});
            Require(room.boxes.Count==1,"场景删除回流");
            room.boxes.Clear(); testCanvas.mergeSelection.Clear();
            room.boxes.Add(new PlanningBox(PlanningDetailType.Solid,new RectInt(0,0,1,1)));
            room.boxes.Add(new PlanningBox(PlanningDetailType.Solid,new RectInt(2,0,1,1)));
            testCanvas.ToggleMergeArea(new RectInt(0,0,3,1));
            testCanvas.MergeSelectedItems();
            Require(room.boxes.Count==2 && !room.boxes[0].isMerged,"拒绝空洞选区");
            room.boxes.Clear(); testCanvas.mergeSelection.Clear();
            var rectangle=new PlanningBox(PlanningDetailType.Solid,new RectInt(0,0,2,2)) { hasComponentOverrides=true };
            room.boxes.Add(rectangle);
            testCanvas.RemoveMissingSceneItems(new []{rectangle.id,rectangle.id},new []{new Vector2Int(0,0),new Vector2Int(1,1)});
            Require(room.boxes.Count==2 && room.boxes.TrueForAll(b=>b.hasComponentOverrides),"矩形批量删除保留参数");
            Require(changes==4,"每个数据事务仅通知一次");
            Debug.Log("关卡编辑器规划验证通过：选择、合并/解除、存档、空洞保护、删除回流与事务通知。");
        }

        private static void Require(bool condition,string operation)
        {
            if(!condition) throw new InvalidOperationException("关卡编辑器验证失败："+operation);
        }
    }
}
