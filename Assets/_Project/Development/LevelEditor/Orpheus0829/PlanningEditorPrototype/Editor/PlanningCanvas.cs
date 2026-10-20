using System;
using System.Collections.Generic;
using System.Linq;
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
        private const float MinimumZoom = .005f;
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
        private string bindingConsumerId;
        private bool pickingSignalSource;
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
            if (!UsesAssemblyPreviewCoordinates || room == null)
                return new RectInt(box.x, box.y, box.width, box.height);
            GetAssemblyStride(out int sx, out int sy);
            if (room.isConnector) return GetAssemblyBoxRect(room, box, sx, sy);
            var origin = OwnerPreview(room).Bounds.position;
            return new RectInt(origin.x * sx + box.x, origin.y * sy + box.y, box.width, box.height);
        }

        private static bool SameKind(PlanningBox a, PlanningBox b) =>
            a.type == b.type && (a.paletteEntryId ?? "") == (b.paletteEntryId ?? "") &&
            (a.paletteEntryName ?? "") == (b.paletteEntryName ?? "") && (a.propEntryId ?? "") == (b.propEntryId ?? "") && (a.propEntryName ?? "") == (b.propEntryName ?? "");

        private static bool IsBossRespawnMarker(PlanningBox box) =>
            box != null &&
            box.propEntryId ==
            PlanningBox.BossRespawnMarkerEntryId;

        private void CompleteMergeSelection()
        {
            var start = CellAt(dragStart);
            var end = CellAt(dragCurrent);
            var area = new RectInt(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Abs(start.x-end.x)+1, Mathf.Abs(start.y-end.y)+1);
            ToggleMergeArea(area);
        }

        private bool ShapeContains(PlanningRoom room, PlanningBox box, Vector2Int displayCell)
        {
            Vector2Int local = displayCell - DisplayRect(room, box).position + new Vector2Int(box.x, box.y);
            return PlanningShapeUtility.Contains(box, local);
        }

        internal void ToggleMergeArea(RectInt area)
        {
            var hits = new List<PlanningBox>();
            PlanningBox kind = null;
            foreach (var item in AllBoxes())
                if (mergeSelection.Contains(item.box.id)) { kind = item.box; break; }
            foreach (var item in HitOrder())
            {
                if (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId) continue;
                var localArea = area; localArea.position += new Vector2Int(item.box.x, item.box.y) - DisplayRect(item.room, item.box).position;
                if (!PlanningShapeUtility.Overlaps(item.box, localArea)) continue;
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
                if ((item.box.singleInstance && !PlanningPlatformShapeUtility.SupportsEntry(item.box.propEntryId)) ||
                    PlanningDoorUtility.IsDoor(item.box) || !string.IsNullOrEmpty(item.box.doorOwnerId))
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
                foreach (Vector2Int cell in PlanningShapeUtility.Cells(box))
                    if (!cells.Add(cell)) { ValidationFailed?.Invoke("选区存在重叠物体，请先移除重叠。"); return; }
            }
            if (!PlanningMechanismUtility.CanMerge(items))
            { ValidationFailed?.Invoke("控制器绑定不同，请先绑定同一曲柄或基座再合并。"); return; }
            bool platform = PlanningPlatformShapeUtility.SupportsEntry(items[0].propEntryId);
            if (platform && !PlanningPlatformShapeUtility.CanMerge(items,
                FindPropEntry(items[0].propEntryId, items[0].propEntryName), new RectInt(minX, minY, maxX-minX, maxY-minY), cells.Count, out string platformReason))
            { ValidationFailed?.Invoke(platformReason ?? "平台素材入口不可用，未修改原平台。"); return; }
            var merged = items[0].Clone();
            merged.id = PlanningDocument.NewId("box"); merged.x=minX; merged.y=minY; merged.width=maxX-minX; merged.height=maxY-minY;
            merged.isMerged = true; merged.mergeParts = new List<string>();
            if (platform)
            {
                if (!merged.hasComponentOverrides) CapturePlacementDefaults(merged);
                merged.singleInstance = true;
                PlanningPlatformShapeUtility.SetOrientation(merged, merged.height > merged.width);
            }
            foreach (var item in items)
            {
                if (item.isMerged && item.mergeParts != null && item.mergeParts.Count > 0)
                {
                    var parts=new List<PlanningBox>();
                    int originX=int.MaxValue,originY=int.MaxValue;
                    foreach(var json in item.mergeParts)
                    {
                        var part=PlanningBox.FromPersistentJson(json);
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
            owner.Add(merged);
            PlanningMechanismUtility.RemapMergedSources(items, merged, AllBoxes().Select(item => item.box));
            mergeSelection.Clear(); mergeSelection.Add(merged.id);
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
                item.box.NormalizeMechanismButtonLinks();
                int ox=int.MaxValue,oy=int.MaxValue;
                var restored = new List<PlanningBox>();
                foreach (string json in item.box.mergeParts)
                {
                    var part=PlanningBox.FromPersistentJson(json);
                    if (part == null) continue;
                    ox=Mathf.Min(ox,part.x); oy=Mathf.Min(oy,part.y); restored.Add(part);
                }
                if (restored.Count == 0) continue;
                item.owner.Remove(item.box);
                foreach(var part in restored)
                {
                    part.x += item.box.x-ox; part.y += item.box.y-oy;
                    part.crankOwnerId = item.box.crankOwnerId;
                    part.platformMountId = item.box.platformMountId;
                    part.mechanismBaseId = item.box.mechanismBaseId;
                    part.mechanismBaseCellOffset = item.box.mechanismBaseCellOffset;
                    part.mechanismButtonIds = new List<string>(item.box.mechanismButtonIds);
                    item.owner.Add(part); mergeSelection.Add(part.id);
                }
                PlanningMechanismUtility.RemapCarvedSource(item.box, restored, AllBoxes().Select(value => value.box));
            }
            NotifyDocumentChanged(); SelectionChanged?.Invoke(); MarkDirtyRepaint();
        }

        private void EditParametersAt(Vector2 position)
        {
            var cell=CellAt(position);
            var visibleItems = HitOrder();
            foreach (var item in visibleItems)
            {
                if (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId) continue;
                if (!ShapeContains(item.room,item.box,cell)) continue;
                var box=item.box;
                var prop=FindPropEntry(box.propEntryId,box.propEntryName);
                var entry=FindEntry(box.paletteEntryId,box.paletteEntryName);
                var source=box.type == PlanningDetailType.Prop ? prop?.Prefab : entry?.SourcePrefab;
                if (source == null) { StatusChanged?.Invoke("此物体没有可配置的预制体组件。"); return; }
                selectedBoxId=box.id;
                Action primaryAction = null;
                string primaryLabel = null;
                Action secondaryAction = null;
                string secondaryLabel = null;
                Action thirdAction = null;
                string thirdLabel = null;
                Action fourthAction = null;
                string fourthLabel = null;
                if (source.GetComponent<
                        Project.BlockFeatures
                            .RespawnAnchorFeature>() != null)
                {
                    primaryAction =
                        () => AddBossRespawnMarker(box.id);
                    primaryLabel = "生成 Boss 复活标记";
                    secondaryAction =
                        () => RemoveBossRespawnMarker(box.id);
                    secondaryLabel = "删除 Boss 复活标记";
                }
                else if (PlanningDoorUtility.IsDoor(box))
                {
                    primaryAction = () => AddDoorButton(box.id);
                    primaryLabel = "添加门按钮";
                }
                else if (IsCrankBox(box))
                {
                    primaryAction = () => AddCrankPlatform(box.id);
                    primaryLabel = "添加升降平台";
                    secondaryAction = () => BeginBaseBinding(box.id);
                    secondaryLabel = "绑定制动基座";
                    thirdAction = () => ClearBaseBinding(box.id);
                    thirdLabel = "解除制动基座绑定";
                    fourthAction = () => { if (string.IsNullOrEmpty(box.crankOwnerId)) BeginSignalBinding(box.id); else ClearSignalBinding(box.id); };
                    fourthLabel = string.IsNullOrEmpty(box.crankOwnerId) ? "绑定共享曲柄" : "解除共享曲柄";
                }
                else if (PlanningMechanismUtility.IsLift(source))
                {
                    primaryAction = () => BeginSignalBinding(box.id);
                    primaryLabel = "绑定信号源";
                    secondaryAction = () => ClearSignalBinding(box.id);
                    secondaryLabel = "清空信号源绑定";
                }
                else if (source.GetComponent<Project.BlockFeatures.MovingPlatformFeature>() != null)
                {
                    primaryAction = () => BeginBaseBinding(box.id);
                    primaryLabel = "绑定基座";
                    secondaryAction = () => BeginSignalBinding(box.id);
                    secondaryLabel = "绑定信号源";
                    thirdAction = () => ClearBaseBinding(box.id);
                    thirdLabel = "解除基座绑定";
                    fourthAction = () => ClearSignalBinding(box.id);
                    fourthLabel = "清空信号源绑定";
                }
                if (!LevelEditorPropComponentEditorWindow.SaveOpenSession()) return;
                LevelEditorPropComponentEditorWindow.Open(source,
                    box.hasComponentOverrides ? box.componentOverrides : box.type == PlanningDetailType.Prop ? prop?.ComponentValueOverrides : entry?.ComponentValueOverrides,
                    values =>
                    {
                        // A stale parameter window must not edit an item removed or restored by history.
                        foreach (var current in AllBoxes())
                            if (current.box.id == box.id)
                            {
                                PlanningPlatformShapeUtility.PreserveOrientation(current.box, values, prop?.ComponentValueOverrides);
                                bool hadMarker =
                                    current.boxes.Exists(
                                        value =>
                                            IsBossRespawnMarker(
                                                value) &&
                                            value
                                                .bossRespawnOwnerId ==
                                            current.box.id);
                                current.box.hasComponentOverrides=true;
                                current.box.componentOverrides=values;
                                if (hadMarker &&
                                    !HasBossOwnerOverride(
                                        values,
                                        "isBossArenaRespawnPoint"))
                                {
                                    SetBossOwnerEnabled(
                                        current.box,
                                        true);
                                }

                                SyncBossMarkerBoxForOwner(
                                    current.box,
                                    current.boxes);
                                NotifyDocumentChanged(); return;
                            }
                    }, "实例参数 · " + (source.name),
                    primaryAction,
                    primaryLabel,
                    secondaryAction,
                    secondaryLabel,
                    thirdAction,
                    thirdLabel,
                    fourthAction,
                    fourthLabel,
                    enabled =>
                    {
                        if (enabled)
                        {
                            AddBossRespawnMarker(box.id);
                        }
                        else
                        {
                            RemoveBossRespawnMarker(box.id);
                        }
                    },
                    source.GetComponent<Project.BlockFeatures.MovingPlatformFeature>() != null ? box.width : 0,
                    source.GetComponent<Project.BlockFeatures.MovingPlatformFeature>() != null
                        ? width => ResizePlatform(box.id, width) : (Func<int, string>)null);
                SelectionChanged?.Invoke(); return;
            }
        }

        internal string ResizePlatform(string id, int width)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "播放模式下不能修改平台宽度。";
            foreach (var current in AllBoxes())
            {
                var box = current.box;
                if (box.id != id) continue;
                if (width == box.width) return null;
                if (width < 1 || width > 4096) return "平台宽度须为1～4096格。";
                var source = FindPropEntry(box.propEntryId, box.propEntryName)?.Prefab;
                if (source == null || source.GetComponent<Project.BlockFeatures.MovingPlatformFeature>() == null)
                    return "当前物体不是移动平台。";
                if (box.isMerged) return "请先解除平台合并，再修改宽度。";
                var area = new RectInt(box.x, box.y, width, box.height);
                if (current.room != null && !CanPlaceLocalRect(current.room, area))
                    return "平台宽度超出当前房间或通道的有效范围。";
                foreach (var other in current.boxes)
                    if (other != box && !PlanningWaterLayerUtility.IsWater(other, palette) && PlanningShapeUtility.Overlaps(other, area))
                        return "平台加宽后会覆盖其他物体，请先移开相邻物体。";
                box.width = width;
                box.singleInstance = true;
                // The normal override save callback emits one document transaction.
                return null;
            }
            return "此平台已被删除或替换，请关闭窗口后重新选择。";
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
        private int panButton = -1;
        private Vector2 panLastPosition;
        private Vector2 panStartPosition;
        private bool rightClickParameters;
        private bool isDragging;
        private int dragPointerId = -1;
        private Vector2 dragStart;
        private Vector2 dragCurrent;
        private PlanningBox movingBox;
        private Vector2 movingBoxStart;
        private Vector2Int movingBoxOrigin;
        private PlanningRoom bossMarkerDragRoom;
        private PlanningBox bossMarkerDragBox;
        private bool bossMarkerDragMoved;
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
            if (pendingBoxPreviews.Count == 0) return;
            foreach (string id in pendingBoxPreviews) boxPreviewCache.Remove(id);
            pendingBoxPreviews.Clear();
            retainedImagesValid = false;
            blockBatchesValid = false;
            UpdateDetailPreviewImages();
        }

        public void RestoreView(Vector2 savedPan, float savedZoom)
        {
            if (float.IsNaN(savedPan.x) || float.IsNaN(savedPan.y) ||
                float.IsNaN(savedZoom) || float.IsInfinity(savedPan.x) || float.IsInfinity(savedPan.y) || float.IsInfinity(savedZoom))
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
            document != null ? document.worldBlockCellWidth : 1;

        private int WorldBlockCellHeight =>
            document != null ? document.worldBlockCellHeight : 1;

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

            var replacement = box.Clone();
            ApplyDetailSelection(replacement);
            if (!CanEditPlacement(replacement)) return false;
            if (!CanPlaceWaterLayer(document.rooms.Find(r => ReferenceEquals(r.boxes, boxes)), replacement,
                    new Vector2Int(replacement.x, replacement.y), box)) return false;
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
                var authoredBossMarkers =
                    room.boxes.FindAll(IsBossRespawnMarker);
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
                    var authored = room.boxes.Find(value => value.id == block.PlanningBoxId);
                    if (authored != null && (authored.isMerged || authored.singleInstance || authored.width * authored.height == 1))
                    {
                        box = authored.Clone(); box.sceneBlock = block;
                        box.x = globalPlanX - offsetX; box.y = globalPlanY - offsetY - (box.height - 1);
                    }
                    if (authored == null && block.GetComponent<LevelEditorMergedShape>() is LevelEditorMergedShape importedShape)
                    {
                        box.width = importedShape.Dimensions.x; box.height = importedShape.Dimensions.y;
                        box.y -= box.height - 1; box.isMerged = true;
                        foreach (RectInt part in importedShape.Rectangles)
                        {
                            var snapshot = box.PersistentClone();
                            snapshot.isMerged = false; snapshot.mergeParts.Clear();
                            snapshot.id = PlanningDocument.NewId("box");
                            snapshot.x = box.x + part.x; snapshot.y = box.y + part.y;
                            snapshot.width = part.width; snapshot.height = part.height;
                            box.mergeParts.Add(JsonUtility.ToJson(snapshot));
                        }
                    }
                    if (block.GetComponent<Project.BlockFeatures.PlatformMount>() is Project.BlockFeatures.PlatformMount importedMount)
                        box.platformMountId = importedMount.Platform != null
                            ? importedMount.Platform.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId : null;
                    if (block.GetComponent<Project.BlockFeatures.CrankPlatformFeature>() is Project.BlockFeatures.CrankPlatformFeature importedCrank)
                    {
                        box.singleInstance = true; box.initialCrankPlatformCreated = importedCrank.InitialPlatformCreated;
                        box.crankOwnerId = importedCrank.SharedController != null
                            ? importedCrank.SharedController.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId : null;
                    }
                    if (block.GetComponent<Project.BlockFeatures.MovingPlatformFeature>() is Project.BlockFeatures.MovingPlatformFeature importedPlatform)
                    {
                        if (importedPlatform.IsCrankControlled)
                        {
                            box.crankOwnerId = importedPlatform.CrankController != null ? importedPlatform.CrankController.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId : null;
                            bool authoredLift =
                                block.EntryName == "曲柄升降平台" ||
                                box.propEntryName == "曲柄升降平台";
                            if (authoredLift)
                            {
                                box.propEntryId = PlanningMechanismUtility.LiftEntryId;
                                box.width = block.SizeCells.x; box.height = block.SizeCells.y;
                                box.y = globalPlanY - offsetY - (box.height - 1);
                                box.isMerged = block.SizeCells != Vector2Int.one;
                            }
                        }
                        else
                        {
                            foreach (var control in importedPlatform.LinkedControls)
                            {
                                var controlBlock = control != null ? control.GetComponent<LevelEditorPlacedBlock>() : null;
                                if (controlBlock == null || string.IsNullOrEmpty(controlBlock.PlanningBoxId)) continue;
                                if (control is Project.BlockFeatures.MechanismBaseFeature)
                                {
                                    box.mechanismBaseId = controlBlock.PlanningBoxId;
                                    box.mechanismBaseCellOffset = Vector2Int.zero;
                                }
                                else if (control is Project.Mechanisms.DoorButton ||
                                         control is Project.BlockFeatures.ButtonLockFeature)
                                {
                                    box.NormalizeMechanismButtonLinks();
                                    if (!box.mechanismButtonIds.Contains(controlBlock.PlanningBoxId))
                                        box.mechanismButtonIds.Add(controlBlock.PlanningBoxId);
                                }
                            }
                        }
                    }
                    if (authored == null && block.GetComponent<Project.Mechanisms.DoorController>() is Project.Mechanisms.DoorController importedDoor)
                    {
                        box.propEntryId = importedDoor.IsHorizontal ? PlanningDoorUtility.HorizontalDoorEntryId : PlanningDoorUtility.DoorEntryId;
                        box.singleInstance = true;
                        var footprint = PlanningDoorUtility.Footprint(box.propEntryId);
                        box.width = footprint.x;
                        box.height = footprint.y;
                        box.y -= footprint.y - 1;
                        if (!string.IsNullOrEmpty(block.PlanningBoxId)) box.id = block.PlanningBoxId;
                    }
                    if (authored == null && block.GetComponent<Project.Mechanisms.NeutralLadder>() is Project.Mechanisms.NeutralLadder ladder)
                    {
                        box.propEntryId = ladder.SideFacing
                            ? ladder.WallSide == Project.Mechanisms.LadderWallSide.Left ? PlanningDoorUtility.LeftSideLadderEntryId : PlanningDoorUtility.SideLadderEntryId
                            : PlanningDoorUtility.FrontLadderEntryId;
                        box.singleInstance = true;
                        box.width = block.SizeCells.x; box.height = block.SizeCells.y;
                        box.y -= box.height - 1;
                    }
                    if (block.GetComponent<Project.Mechanisms.DoorButton>() is Project.Mechanisms.DoorButton button)
                    {
                        box.propEntryId = PlanningDoorUtility.ButtonEntryId;
                        box.singleInstance = true;
                        if (!string.IsNullOrEmpty(block.PlanningBoxId)) box.id = block.PlanningBoxId;
                        box.doorOwnerId = button.Owner != null ? button.Owner.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId : null;
                        if (box.doorButtonOrder <= 0) box.doorButtonOrder = button.BindingIndex + 1;
                    }
                    syncedBoxes.Add(box);
                }

                for (int index = 0;
                     index < authoredBossMarkers.Count;
                     index++)
                {
                    PlanningBox marker =
                        authoredBossMarkers[index];
                    if (string.IsNullOrEmpty(
                            marker.bossRespawnOwnerId) ||
                        !syncedBoxes.Exists(
                            value => value.id ==
                                     marker
                                         .bossRespawnOwnerId))
                    {
                        continue;
                    }

                    syncedBoxes.Add(marker.Clone());
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
                a.NormalizeMechanismButtonLinks();
                b.NormalizeMechanismButtonLinks();
                if (a.x != b.x ||
                    a.y != b.y ||
                    a.width != b.width ||
                    a.height != b.height ||
                    a.paletteEntryName != b.paletteEntryName || a.propEntryName != b.propEntryName ||
                    a.crankOwnerId != b.crankOwnerId || a.mechanismBaseId != b.mechanismBaseId ||
                    a.platformMountId != b.platformMountId ||
                    !a.mechanismButtonIds.SequenceEqual(b.mechanismButtonIds) ||
                    a.doorOwnerId != b.doorOwnerId ||
                    a.doorButtonOrder != b.doorButtonOrder ||
                    a.bossRespawnOwnerId !=
                    b.bossRespawnOwnerId)
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
            RegisterCallback<PointerCaptureOutEvent>(OnCanvasCaptureOut);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnTemporaryToolKeyUp);
            RegisterCallback<FocusOutEvent>(_ => ResetTemporaryAssemblyTools());
            RegisterCallback<GeometryChangedEvent>(
                _ => UpdateDetailPreviewImages());
            RegisterCallback<AttachToPanelEvent>(_ => { EditorApplication.projectChanged -= RefreshArtworkPreviews; EditorApplication.projectChanged += RefreshArtworkPreviews; });
            RegisterCallback<DetachFromPanelEvent>(_ => { EditorApplication.projectChanged -= RefreshArtworkPreviews; ResetTemporaryAssemblyTools(); CancelPointerInteraction(); });
            BuildPreviewLayers();
            BuildPlayerOverlay();
            schedule.Execute(RefreshRegionLabels).Every(100);
            InitializeEdgePan();
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

            playerOverlay.pickingMode = !HasTemporaryAssemblyTool && !editWaterLayer && LevelEditorState.Tool == LevelEditorTool.Player &&
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
            triggerPreviewLayer?.BringToFront();
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
            if (mode == PlanningCanvasMode.Assembly && (evt.altKey || evt.shiftKey)) return;
            if (editWaterLayer || evt.button != 0 || LevelEditorState.Tool != LevelEditorTool.Player ||
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
            InvalidatePreviewData();
            document?.Normalize();
            EnsureInitialCrankPlatforms();
            NormalizeBossRespawnMarkers();
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

        internal void NormalizeBossRespawnMarkers()
        {
            if (document == null)
            {
                return;
            }

            for (int roomIndex = 0;
                 roomIndex < document.rooms.Count;
                 roomIndex++)
            {
                PlanningRoom room =
                    document.rooms[roomIndex];
                var owners =
                    new List<PlanningBox>();
                for (int boxIndex = 0;
                     boxIndex < room.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = room.boxes[boxIndex];
                    if (!IsBossRespawnMarker(box))
                    {
                        owners.Add(box);
                    }
                }

                room.boxes.RemoveAll(
                    value => IsBossRespawnMarker(value) &&
                             !owners.Exists(
                                 owner =>
                                     owner.id ==
                                     value
                                         .bossRespawnOwnerId));
                for (int index = 0;
                     index < owners.Count;
                     index++)
                {
                    PlanningBox owner = owners[index];
                    if (FindPropEntry(
                            owner.propEntryId,
                            owner.propEntryName)?.Prefab?
                        .GetComponent<
                            Project.BlockFeatures
                                .RespawnAnchorFeature>() ==
                        null)
                    {
                        continue;
                    }

                    SyncBossMarkerBoxForOwner(
                        owner,
                        room.boxes);
                }
            }
        }

        public void BeginRoomPlacement()
        {
            SetMapTool(PlanningMapTool.Paint);
            isPlacingRoom = true;
            MarkDirtyRepaint();
            StatusChanged?.Invoke(
                "单击或拖拽框选空白格创建房间，右键或 Esc 取消。");
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
            ResetTemporaryAssemblyTools();
            isPlacingRoom = false;
            mode = value;
            waterPreviewCells.Clear();
            waterPreviewOwners.Clear();
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

        private PlanningDetailTool GetActiveDetailTool()
        {
            if (mode == PlanningCanvasMode.Assembly)
            {
                if (assemblyAltHeld) return PlanningDetailTool.Erase;
                if (assemblyShiftHeld) return PlanningDetailTool.Box;
            }
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
            if (detailEntryId == entryId && detailEntryName == entryName && string.IsNullOrEmpty(detailPropEntryId)) return;
            detailEntryId = entryId;
            detailEntryName = entryName;
            detailPropEntryId = string.Empty;
            detailPropEntryName = string.Empty;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        public void SetDetailProp(string entryId, string entryName)
        {
            if (detailPropEntryId == entryId && detailPropEntryName == entryName && string.IsNullOrEmpty(detailEntryId)) return;
            detailPropEntryId = entryId;
            detailPropEntryName = entryName;
            detailEntryId = string.Empty;
            detailEntryName = string.Empty;
            UpdateDetailPreviewImages();
            MarkDirtyRepaint();
        }

        public void SetPalette(LevelEditorPalette value)
        {
            if (palette == value && previewPaletteRevision == LevelEditorState.PaletteRevision) return;
            palette = value;
            previewPaletteRevision = LevelEditorState.PaletteRevision;
            InvalidatePreviewData();
            EnsureInitialCrankPlatforms();
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
                    Vector2Int offset = r.position - new Vector2Int(item.box.x, item.box.y);
                    foreach (RectInt part in PlanningShapeUtility.Rectangles(item.box))
                        StrokeRect(context.painter2D, CellRect(part.x + offset.x,part.y + offset.y,part.width,part.height),new Color(.3f,.85f,1f),3f);
                }
                DrawRectanglePreview(context.painter2D);
            }
        }

        private void DrawAssembly(MeshGenerationContext context)
        {
            using var profile = DrawAssemblyMarker.Auto();
            Painter2D painter = context.painter2D;
            DrawAssemblyOwnership(painter);
            DrawGrid(context);
            GetAssemblyStride(out int strideX, out int strideY);

            for (int index = 0; index < document.rooms.Count; index++)
            {
                PlanningRoom owner = document.rooms[index];
                if (owner.isConnector)
                {
                    continue;
                }

                Vector2Int origin = OwnerPreview(owner).Bounds.position;
                int offsetX = origin.x * strideX, offsetY = origin.y * strideY;
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
                    if (!rect.Overlaps(contentRect)) continue;
                    DrawShapeFallback(painter, box, new Vector2Int(offsetX,offsetY));
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
                    if (!rect.Overlaps(contentRect)) continue;
                    DrawShapeFallback(painter, box, new Vector2Int(originX,originY));
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
                if (!rect.Overlaps(contentRect)) continue;
                DrawShapeFallback(painter, box, Vector2Int.zero);
                StrokeRect(
                    painter,
                    rect,
                    new Color(1f, .85f, .35f, .8f),
                    1f);
            }

            DrawBossRespawnMarkers(painter);
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

        private void GetAssemblyRoomOffset(PlanningRoom room, int strideX, int strideY,
            out int offsetX, out int offsetY, out int maxPlanY)
        {
            Vector2Int origin = OwnerPreview(room).Bounds.position;
            offsetX = origin.x * strideX; offsetY = origin.y * strideY;
            maxPlanY = 1;
            foreach (var box in room.boxes) maxPlanY = Mathf.Max(maxPlanY, box.y + box.height);
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
                if (!string.IsNullOrEmpty(room.sourceColorHex) && ColorUtility.TryParseHtmlString(room.sourceColorHex, out Color importedColor))
                    color = importedColor;
                bool selected = worldSelection.Contains(room.id) || room.id == selectedRoomId;
                color.a = showWorldRoomContents ? (selected ? .2f : .08f) : (selected ? .78f : .5f);
                DrawWorldOwner(painter, room, color, selected);
            }

            DrawConnectors(painter);
            if (showWorldRoomContents) DrawWorldRoomContents(painter);
            DrawGrid(context);
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
            using var profile = DrawDetailMarker.Auto();
            PlanningRoom room = document?.FindRoom(selectedRoomId);
            if (room == null)
            {
                DrawEmptyHint(context);
                return;
            }

            Painter2D painter = context.painter2D;
            DrawGrid(context);
            DrawDetailOwnership(painter, room);

            foreach (var box in room.boxes.OrderBy(b => IsPreviewWater(b) ? 0 : 1))
            {
                Rect rect = CellRect(box.x, box.y, box.width, box.height);
                if (!rect.Overlaps(contentRect)) continue;
                Texture2D preview = IsBatchedBlock(box) ? Texture2D.whiteTexture : GetBoxPreview(box);
                if (preview == null)
                {
                    DrawShapeFallback(painter, box, Vector2Int.zero);
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

            DrawBossRespawnMarkers(painter);
            DrawDoorLinks(painter);
            DrawRectanglePreview(painter);
            DrawHoverCell(painter);
        }

        private void DrawShapeFallback(Painter2D painter, PlanningBox box, Vector2Int offset)
        {
            if (IsBatchedBlock(box)) return;
            if (GetBoxPreview(box) != null) return;
            Color color = ColorForBox(box);
            color.a *= editWaterLayer && !PlanningWaterLayerUtility.IsWater(box, palette) ? .2f : 1f;
            foreach (RectInt part in PlanningShapeUtility.Rectangles(box))
            {
                Rect rect = CellRect(part.x + offset.x, part.y + offset.y, part.width, part.height);
                if (rect.Overlaps(contentRect)) FillRect(painter, rect, color, 0f);
            }
        }

        private void UpdateDetailPreviewImages()
        {
            UpdateBlockBatches();
            UpdateTriggerPlacementPreview();
            if (UpdateRetainedImageView())
            {
                UpdatePlayerOverlay();
                return;
            }
            buildingRetainedImages = true;
            Vector2 currentPan = pan; float currentZoom = zoom;
            pan = Vector2.zero; zoom = 1;
            try { RebuildDetailPreviewImages(); retainedImagesValid = true; PreviewGeometryRevision++; }
            finally { pan = currentPan; zoom = currentZoom; buildingRetainedImages = false; }
            UpdateRetainedImageView();
            UpdatePlayerOverlay();
        }

        private void RebuildDetailPreviewImages()
        {
            if (objectPreviewLayer != null) objectPreviewLayer.style.opacity = editWaterLayer ? .2f : 1f;
            if (document == null)
            {
                HideDetailPreviewImages();
                UpdatePlayerOverlay();
                return;
            }

            if (UsesAssemblyPreviewCoordinates)
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

            var active = activePreviewKeys;
            active.Clear();
            foreach (PlanningBox box in room.boxes)
            {
                AddPreviewImage(box, new RectInt(box.x, box.y, box.width, box.height), active);
            }
            RemoveUnusedPreviewImages(active);
            UpdatePlayerOverlay();
        }

        private void UpdateAssemblyPreviewImages()
        {
            var active = activePreviewKeys;
            active.Clear();
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

                // Resolve the room layout once, not once for every object in this room.
                var origin = OwnerPreview(room).Bounds.position;
                for (int boxIndex = 0;
                     boxIndex < room.boxes.Count;
                     boxIndex++)
                {
                    PlanningBox box = room.boxes[boxIndex];
                    AddPreviewImage(
                        box,
                        new RectInt(origin.x * strideX + box.x, origin.y * strideY + box.y, box.width, box.height),
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

            RemoveUnusedPreviewImages(active);
        }

        private void AddPreviewImage(
            PlanningBox box,
            RectInt rect,
            HashSet<string> active)
        {
            if (IsBatchedBlock(box)) return;
            if (!PreviewVisible(rect)) return;
            Vector2Int offset = rect.position - new Vector2Int(box.x, box.y);
            foreach (RectInt part in PlanningShapeUtility.Rectangles(box))
            {
                var display = part; display.position += offset;
                AddPreviewRectangle(box, display, active, offset);
            }
        }

        private void AddPreviewRectangle(PlanningBox box, RectInt rect, HashSet<string> active, Vector2Int offset)
        {
            if (!PreviewVisible(rect)) return;
            if (TryAddPlatformPreview(box, rect, active)) return;
            if (TryAddArtworkPreview(box, rect, active, offset)) return;
            Texture2D preview = GetBoxPreview(box);
            bool fallback = preview == null;
            preview ??= Texture2D.whiteTexture;
            bool water = PlanningWaterLayerUtility.IsWater(box, palette);
            VisualElement layer = water ? waterPreviewLayer : objectPreviewLayer;
            int startX = rect.x, startY = rect.y, endX = rect.xMax, endY = rect.yMax;
            if (!buildingRetainedImages && !box.isMerged && contentRect.width > 0f && contentRect.height > 0f)
            {
                Vector2 topLeft = ScreenToWorld(contentRect.min), bottomRight = ScreenToWorld(contentRect.max);
                startX = Mathf.Max(startX, Mathf.FloorToInt(topLeft.x)); startY = Mathf.Max(startY, Mathf.FloorToInt(topLeft.y));
                endX = Mathf.Min(endX, Mathf.CeilToInt(bottomRight.x)); endY = Mathf.Min(endY, Mathf.CeilToInt(bottomRight.y));
            }
            for (int y = startY; y < (box.isMerged ? startY+1 : endY); y++)
            {
                for (int x = startX; x < (box.isMerged ? startX+1 : endX); x++)
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
                        layer.Add(image);
                        detailPreviewImages[key] = image;
                    }
                    else if (image.parent != layer) layer.Add(image);

                    Rect cellRect = CellRect(x, y, box.isMerged ? rect.width : 1, box.isMerged ? rect.height : 1);
                    image.image = preview;
                    image.uv = new Rect(0, 0, 1, 1);
                    image.style.rotate = new Rotate(new Angle(0));
                    image.tintColor = fallback ? ColorForBox(box) :
                        mergeSelection.Contains(box.id) && LevelEditorState.Tool == LevelEditorTool.Merge ? new Color(.55f,.85f,1f) : Color.white;
                    image.style.left = cellRect.x;
                    image.style.top = cellRect.y;
                    image.style.width = cellRect.width;
                    image.style.height = cellRect.height;
                    image.style.display = DisplayStyle.Flex;
                    if (water) image.tintColor = new Color(.65f, .85f, 1f, editWaterLayer ? 1f : .72f);
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
            MeshGenerationContext context,
            float opacity = 1f)
        {
            float luminance =
                backgroundColor.r * .2126f +
                backgroundColor.g * .7152f +
                backgroundColor.b * .0722f;
            Color gridColor = luminance < .35f
                ? new Color(.42f, .5f, .64f, .68f)
                : new Color(.18f, .22f, .28f, .58f);
            gridColor.a *= opacity;
            Project.GridArt.Editor.GridViewportDrawing.Draw(context, contentRect, pan, GridSize, gridColor);
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

        private bool IsPlacingDoor => !IsTemporaryWhiteBrush && mode != PlanningCanvasMode.World &&
            GetActiveDetailTool() == PlanningDetailTool.Box && PlanningDoorUtility.HasFixedFootprint(detailPropEntryId);

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
            Vector2Int size = PlacementFootprint(detailPropEntryId);
            if (room != null)
            {
                RectInt allowed = room.isConnector ? GetConnectorDetailBounds(room) :
                    room.GetLocalAllowedRect(WorldBlockCellWidth, WorldBlockCellHeight);
                var door = new PlanningBox(PlanningDetailType.Prop, new RectInt(local, size))
                    { propEntryId = detailPropEntryId };
                valid = CanPlaceLocalRect(room, new RectInt(local, size)) &&
                    PlanningDoorUtility.CanPlace(room.boxes, new RectInt(local, size), palette) &&
                    (!PlanningDoorUtility.IsDoor(door) || PlanningDoorUtility.TryFindButtonCell(room.boxes, door, allowed, out _, c => CanPlaceLocalRect(room, new RectInt(c, Vector2Int.one))));
            }
            Rect footprint = CellRect(cell.x, cell.y, size.x, size.y);
            Color color = valid ? new Color(.35f, .9f, .65f, 1f) : new Color(1f, .35f, .3f, 1f);
            FillRect(painter, footprint, new Color(color.r, color.g, color.b, .2f), 1f);
            StrokeRect(painter, footprint, color, 2f);
            if (size.x > 1)
                DrawLine(painter, new Vector2(footprint.center.x, footprint.yMin), new Vector2(footprint.center.x, footprint.yMax), color, 1f);
            else
                DrawLine(painter, new Vector2(footprint.xMin, footprint.center.y), new Vector2(footprint.xMax, footprint.center.y), color, 1f);
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
            if (boxPreviewCache.TryGetValue(box.id, out var cached)) return cached;
            Texture2D preview = ResolveBoxPreview(box);
            boxPreviewCache[box.id] = preview;
            return preview;
        }

        private Texture2D ResolveBoxPreview(PlanningBox box)
        {
            if (ResolveArtworkPreview(box,out var artwork)) return artwork.Texture != null ? artwork.Texture : Texture2D.whiteTexture;
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
                if (assetPreview == null) pendingBoxPreviews.Add(box.id);
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
            var key = (entryId, entryName);
            if (!previewPropEntries.TryGetValue(key, out var entry))
                previewPropEntries[key] = entry = ResolvePropEntry(entryId, entryName);
            return entry;
        }

        private LevelEditorPropEntry ResolvePropEntry(string entryId, string entryName)
        {
            // Solid blocks have no prop entry. Avoid scanning the whole prop palette
            // for every wall in each overlay pass.
            if (string.IsNullOrEmpty(entryId) && string.IsNullOrEmpty(entryName)) return null;
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
            if (isDragging || isPanning) CancelPointerInteraction();
            if (isDraggingPlayer) EndPlayerPointerDrag(playerPointerId, true);
            Focus();
            UpdateTemporaryAssemblyTools(evt.altKey, evt.shiftKey);
            Vector2 position = evt.localPosition;
            UpdateHover(position);
            if (!string.IsNullOrEmpty(bindingConsumerId))
            {
                if (evt.button == 1) { bindingConsumerId = null; pickingSignalSource = false; StatusChanged?.Invoke("已取消绑定。"); evt.StopPropagation(); return; }
                if (evt.button == 0)
                {
                    BindMechanismAt(CellAt(position)); evt.StopPropagation(); return;
                }
            }
            if (isPlacingRoom && (evt.button == 1 || evt.button == 2))
            {
                CancelRoomPlacement();
                evt.StopPropagation();
                return;
            }

            if (evt.button == 1 ||
                evt.button == 2 ||
                (importPreview == null && mode == PlanningCanvasMode.World && mapTool == PlanningMapTool.Pan) ||
                (mode == PlanningCanvasMode.Detail &&
                 GetActiveDetailTool() == PlanningDetailTool.Pan) ||
                (mode == PlanningCanvasMode.Assembly &&
                 GetActiveDetailTool() == PlanningDetailTool.Pan))
            {
                isPanning = true;
                panPointerId = evt.pointerId;
                panButton = evt.button;
                panLastPosition = position;
                panStartPosition = position;
                rightClickParameters = evt.button == 1 && mode != PlanningCanvasMode.World;
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
            BeginEdgePan(position);
            if (TryBeginImportDrag(position))
            {
                MarkDirtyRepaint(); evt.StopPropagation(); return;
            }
            if (!HasTemporaryAssemblyTool && TryBeginBossMarkerDrag(position))
            {
                MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }
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
            UpdateTemporaryAssemblyTools(evt.altKey, evt.shiftKey);
            Vector2 position = evt.localPosition;
            UpdateHover(position);
            if (isPanning && evt.pointerId == panPointerId)
            {
                if ((evt.pressedButtons & (1 << panButton)) == 0 || !this.HasPointerCapture(panPointerId))
                { EndPointerAction(); return; }
                if (rightClickParameters)
                {
                    if ((position - panStartPosition).sqrMagnitude <= 16f) return;
                    rightClickParameters = false;
                }
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

            if ((evt.pressedButtons & 1) == 0 || !this.HasPointerCapture(dragPointerId))
            {
                CompletePointerDrag(dragCurrent);
                return;
            }

            dragCurrent = position;
            ContinuePointerDrag(position);
            UpdateEdgePanPointer(position);

            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (evt.target != this) return;
            if (isDragging && !isPanning && this.HasPointerCapture(dragPointerId))
                UpdateEdgePanPointer(this.WorldToLocal(evt.position));
            else
                PauseEdgePan();
            hasHover = false;
            UpdateTriggerPlacementPreview();
            MarkDirtyRepaint();
        }

        private void UpdateHover(Vector2 position)
        {
            hoveredCell = CellAt(position);
            hasHover = true;
            UpdateTriggerPlacementPreview();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            UpdateTemporaryAssemblyTools(evt.altKey, evt.shiftKey);
            if (evt.pointerId != panPointerId &&
                evt.pointerId != dragPointerId)
            {
                return;
            }

            if (isDragging && evt.pointerId == dragPointerId && evt.button == 0)
            {
                CompletePointerDrag(evt.localPosition);
            }
            else if (isPanning && evt.pointerId == panPointerId && evt.button == panButton)
            {
                bool openParameters = rightClickParameters &&
                    (evt.localPosition - (Vector3)panStartPosition).sqrMagnitude <= 16f;
                EndPointerAction();
                if (openParameters) EditParametersAt(evt.localPosition);
            }
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void EndPointerAction()
        {
            // Clear before releasing: capture-out is synchronous on some panels.
            int previousPan = panPointerId, previousDrag = dragPointerId;
            bool moved = movingBox != null && movingBoxOrigin != new Vector2Int(movingBox.x, movingBox.y);
            bool bossMarkerMoved = bossMarkerDragMoved;
            isPanning = false;
            isDragging = false;
            draggingImport = false;
            panPointerId = -1;
            dragPointerId = -1;
            panButton = -1;
            rightClickParameters = false;
            movingBox = null;
            bossMarkerDragRoom = null;
            bossMarkerDragBox = null;
            bossMarkerDragMoved = false;
            worldMoving = false;
            StopEdgePan();
            if (isDraggingPlayer) EndPlayerPointerDrag(playerPointerId, true);
            if (suppressDocumentChanged) EndDocumentBatch();
            if (previousPan >= 0 && this.HasPointerCapture(previousPan)) this.ReleasePointer(previousPan);
            if (previousDrag >= 0 && this.HasPointerCapture(previousDrag)) this.ReleasePointer(previousDrag);
            // Interrupted drags have already changed coordinates. Persist that one move,
            // never an unfinished paint/erase marquee or a preview-only world move.
            if (moved || bossMarkerMoved) { NotifyDocumentChanged(); SelectionChanged?.Invoke(); }
            lastPaintCell = new Vector2Int(int.MinValue, int.MinValue);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (HandleTemporaryToolKey(evt.keyCode, true, evt.altKey, evt.shiftKey))
            {
                evt.StopPropagation();
                return;
            }
            if (importPreview != null && mode == PlanningCanvasMode.World)
            {
                if (evt.keyCode == KeyCode.Escape) ImportPreviewCancelled?.Invoke();
                evt.StopPropagation(); return;
            }
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
            if (!CanPlaceWaterLayer(room, movingBox, new Vector2Int(targetX, targetY))) return;
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
                    Vector2Int size = PlacementFootprint(detailPropEntryId);
                    minX = start.x; maxX = start.x + size.x - 1;
                    minY = start.y;
                    maxY = start.y + size.y - 1;
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
                if (!CanEditPlacement(box)) return;
                if (!PrepareDoorPlacement(room, box, allowed)) return;
                if (!CanPlaceLocalRect(room, new RectInt(box.x, box.y, box.width, box.height)))
                { StatusChanged?.Invoke("选区超出当前房间或通道占格。"); return; }
                if (!CanPlaceWaterLayer(room, box, new Vector2Int(box.x, box.y))) return;
                room.boxes.Add(box);
                if (PlanningDoorUtility.IsDoor(box)) PlanningDoorUtility.AddButton(room.boxes, box, allowed, c => CanPlaceLocalRect(room, new RectInt(c, Vector2Int.one)));
                selectedBoxId = box.id;
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
            }
            else if (activeTool == PlanningDetailTool.Select &&
                     movingBox != null)
            {
                if (movingBoxOrigin != new Vector2Int(movingBox.x, movingBox.y)) NotifyDocumentChanged();
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
                var items = HitOrder();
                foreach (var item in items)
                {
                    if (!ShapeContains(item.room, item.box, cell)) continue;
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
                if (!CanPlaceWaterLayer(owner, movingBox, target)) return;
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
                if (!CanEditPlacement(box)) return;
                Vector2Int size = IsPlacingDoor ? PlacementFootprint(detailPropEntryId) : new Vector2Int(box.width, box.height);
                if (!CanPlaceLocalRect(owner, new RectInt(new Vector2Int(box.x, box.y), size))) return;
                if (owner.boxes.Exists(b => SameKind(b, box) && PlanningShapeUtility.Contains(b, new Vector2Int(box.x, box.y)))) return;
                RectInt allowed = owner.GetLocalAllowedRect(WorldBlockCellWidth, WorldBlockCellHeight);
                if (owner.isConnector) allowed = GetConnectorDetailBounds(owner);
                if (!PrepareDoorPlacement(owner, box, allowed)) return;
                if (!CanPlaceWaterLayer(owner, box, new Vector2Int(box.x, box.y))) return;
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
            if (IsTemporaryWhiteBrush)
            {
                var white = palette?.Entries.FirstOrDefault(entry =>
                    entry.EntryId == "b425a019bb3c476da08cd4065bd77862") ??
                    palette?.Entries.FirstOrDefault(entry => entry.DisplayName == "白方块");
                box.type = PlanningDetailType.Platform;
                box.propEntryId = box.propEntryName = string.Empty;
                box.paletteEntryId = white?.EntryId ?? string.Empty;
                box.paletteEntryName = white?.DisplayName ?? "白方块";
                CapturePlacementDefaults(box);
                return;
            }
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
            if (IsCrankBox(box))
            {
                box.width = box.height = 1; box.singleInstance = true;
                if (!CanPlaceLocalRect(room, new RectInt(box.x,box.y,1,1)) ||
                    !PlanningDoorUtility.CanPlace(room.boxes,new RectInt(box.x,box.y,1,1), palette) || !TryFindCrankPlatformCell(room,box,out _))
                { StatusChanged?.Invoke("曲柄及附近升降平台需要空格。"); return false; }
                return true;
            }
            if (!PlanningDoorUtility.HasFixedFootprint(box.propEntryId)) return true;
            Vector2Int size = PlacementFootprint(box.propEntryId);
            box.width = size.x;
            box.height = size.y;
            box.singleInstance = true;
            ApplyPlatformPlacementShape(box);
            if (!CanPlaceLocalRect(room, new RectInt(box.x, box.y, size.x, size.y)) ||
                !PlanningDoorUtility.CanPlace(room.boxes, new RectInt(box.x, box.y, size.x, size.y), palette))
            { StatusChanged?.Invoke($"物体需要连续的 {size.x}×{size.y} 空格。"); return false; }
            if (!PlanningDoorUtility.IsDoor(box)) return true;
            if (!PlanningDoorUtility.TryFindButtonCell(room.boxes, box, allowed, out _, c => CanPlaceLocalRect(room, new RectInt(c, Vector2Int.one))))
            { StatusChanged?.Invoke("门附近没有可放置按钮的空格。"); return false; }
            return true;
        }

        private void AddDoorButton(string doorId)
        {
            foreach (var item in AllBoxes())
            {
                if (item.box.id != doorId || !PlanningDoorUtility.IsDoor(item.box) || item.room == null) continue;
                int buttonCount = 0;
                foreach (var candidate in item.boxes) if (candidate.doorOwnerId == doorId) buttonCount++;
                if (buttonCount >= Project.Mechanisms.DoorController.MaximumButtons)
                { StatusChanged?.Invoke("每扇门最多绑定 3 个按钮。"); return; }
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

        private void AddBossRespawnMarker(string ownerId)
        {
            foreach (var item in AllBoxes())
            {
                if (item.box.id != ownerId ||
                    item.room == null ||
                    FindPropEntry(
                        item.box.propEntryId,
                        item.box.propEntryName)?.Prefab?
                        .GetComponent<
                            Project.BlockFeatures
                                .RespawnAnchorFeature>() ==
                    null)
                {
                    continue;
                }

                SetBossOwnerEnabled(item.box, true);
                SyncBossMarkerBoxForOwner(
                    item.box,
                    item.boxes);
                PlanningBox marker = item.boxes.Find(
                    value => IsBossRespawnMarker(value) &&
                             value.bossRespawnOwnerId ==
                             item.box.id);
                selectedRoomId = item.room.id;
                selectedBoxId = marker?.id;
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
                MarkDirtyRepaint();
                StatusChanged?.Invoke(
                    "已生成 Boss 复活标记，只允许沿 X 拖动。");
                return;
            }
        }

        private void RemoveBossRespawnMarker(string ownerId)
        {
            foreach (var item in AllBoxes())
            {
                if (item.box.id != ownerId)
                {
                    continue;
                }

                SetBossOwnerEnabled(item.box, false);
                item.boxes.RemoveAll(
                    value => IsBossRespawnMarker(value) &&
                             value.bossRespawnOwnerId ==
                             ownerId);
                NotifyDocumentChanged();
                SelectionChanged?.Invoke();
                MarkDirtyRepaint();
                StatusChanged?.Invoke(
                    "已删除 Boss 复活标记。");
                return;
            }
        }

        private void SyncBossMarkerBoxForOwner(
            PlanningBox owner,
            List<PlanningBox> boxes)
        {
            if (owner == null || boxes == null)
            {
                return;
            }

            bool enabled = ReadBoolOverride(
                GetBossOwnerOverrides(owner),
                "isBossArenaRespawnPoint",
                false);
            var markers = boxes.FindAll(
                value => IsBossRespawnMarker(value) &&
                         value.bossRespawnOwnerId ==
                         owner.id);
            if (!enabled)
            {
                for (int index = 0;
                     index < markers.Count;
                     index++)
                {
                    boxes.Remove(markers[index]);
                }

                return;
            }

            float localX = ReadFloatOverride(
                GetBossOwnerOverrides(owner),
                "bossRespawnLocalX",
                0f);
            PlanningBox marker = markers.Count > 0
                ? markers[0]
                : null;
            if (marker == null)
            {
                marker = new PlanningBox(
                    PlanningDetailType.Prop,
                    new RectInt(
                        owner.x + Mathf.RoundToInt(localX),
                        owner.y,
                        1,
                        1))
                {
                    propEntryId =
                        PlanningBox.BossRespawnMarkerEntryId,
                    propEntryName =
                        PlanningBox.BossRespawnMarkerEntryName,
                    singleInstance = true,
                    bossRespawnOwnerId = owner.id
                };
                boxes.Add(marker);
            }
            else
            {
                marker.x = owner.x +
                           Mathf.RoundToInt(localX);
                marker.y = owner.y;
                marker.width = marker.height = 1;
                marker.singleInstance = true;
                marker.bossRespawnOwnerId = owner.id;
                for (int index = 1;
                     index < markers.Count;
                     index++)
                {
                    boxes.Remove(markers[index]);
                }
            }
        }

        private IReadOnlyList<LevelEditorComponentValueOverride>
            GetBossOwnerOverrides(PlanningBox owner)
        {
            LevelEditorPropEntry prop = FindPropEntry(
                owner.propEntryId,
                owner.propEntryName);
            return owner.hasComponentOverrides
                ? owner.componentOverrides
                : prop?.ComponentValueOverrides;
        }

        private void SetBossOwnerEnabled(
            PlanningBox owner,
            bool enabled)
        {
            SetBossOwnerOverride(
                owner,
                "isBossArenaRespawnPoint",
                "Boolean",
                enabled ? "1" : "0");
        }

        private void SetBossOwnerOverride(
            PlanningBox owner,
            string propertyPath,
            string valueType,
            string text)
        {
            if (owner == null)
            {
                return;
            }

            LevelEditorPropEntry prop = FindPropEntry(
                owner.propEntryId,
                owner.propEntryName);
            if (!owner.hasComponentOverrides)
            {
                owner.hasComponentOverrides = true;
                owner.componentOverrides =
                    new List<LevelEditorComponentValueOverride>();
                if (prop?.ComponentValueOverrides != null)
                {
                    for (int index = 0;
                         index < prop.ComponentValueOverrides.Count;
                         index++)
                    {
                        LevelEditorComponentValueOverride source =
                            prop.ComponentValueOverrides[index];
                        if (source != null)
                        {
                            owner.componentOverrides.Add(
                                source.Clone());
                        }
                    }
                }
            }

            owner.componentOverrides ??=
                new List<LevelEditorComponentValueOverride>();
            string typeName =
                typeof(Project.BlockFeatures.RespawnAnchorFeature)
                    .FullName;
            for (int index =
                     owner.componentOverrides.Count - 1;
                 index >= 0;
                 index--)
            {
                LevelEditorComponentValueOverride value =
                    owner.componentOverrides[index];
                if (value != null &&
                    string.IsNullOrEmpty(value.ComponentPath) &&
                    value.ComponentTypeName == typeName &&
                    value.PropertyPath == propertyPath)
                {
                    owner.componentOverrides.RemoveAt(index);
                }
            }

            var overrideValue =
                new LevelEditorComponentValueOverride();
            overrideValue.Configure(
                string.Empty,
                typeName,
                0,
                propertyPath,
                valueType,
                text,
                null);
            owner.componentOverrides.Add(overrideValue);
        }

        private void DrawBossRespawnMarkers(Painter2D painter)
        {
            foreach (var item in AllBoxes())
            {
                if (mode == PlanningCanvasMode.Detail &&
                    item.room?.id != selectedRoomId)
                {
                    continue;
                }

                if (!TryGetBossMarkerLayout(
                        item.room,
                        item.box,
                        out Vector2 anchor,
                        out Vector2 marker,
                        out _))
                {
                    continue;
                }

                Color color =
                    new Color(1f, .62f, .25f, .95f);
                for (float distance = 0f;
                     distance < Vector2.Distance(anchor, marker);
                     distance += 10f)
                {
                    Vector2 start = Vector2.Lerp(
                        anchor,
                        marker,
                        distance /
                        Mathf.Max(
                            .001f,
                            Vector2.Distance(anchor, marker)));
                    Vector2 end = Vector2.Lerp(
                        anchor,
                        marker,
                        Mathf.Min(
                            distance + 5f,
                            Vector2.Distance(anchor, marker)) /
                        Mathf.Max(
                            .001f,
                            Vector2.Distance(anchor, marker)));
                    DrawLine(painter, start, end, color, 1.5f);
                }

                Rect markerRect = new Rect(
                    marker.x - 6f,
                    marker.y - 6f,
                    12f,
                    12f);
                StrokeRect(painter, markerRect, color, 2f);
                DrawLine(
                    painter,
                    marker - Vector2.right * 4f,
                    marker + Vector2.right * 4f,
                    color,
                    1.5f);
                DrawLine(
                    painter,
                    marker - Vector2.up * 4f,
                    marker + Vector2.up * 4f,
                    color,
                    1.5f);
            }
        }

        private bool TryGetBossMarkerLayout(
            PlanningRoom room,
            PlanningBox box,
            out Vector2 anchor,
            out Vector2 marker,
            out float localX)
        {
            anchor = default;
            marker = default;
            localX = 0f;
            if (!IsBossRespawnMarker(box) ||
                room == null ||
                string.IsNullOrEmpty(
                    box.bossRespawnOwnerId))
            {
                return false;
            }

            PlanningBox owner = room.boxes.Find(
                value => value.id ==
                         box.bossRespawnOwnerId);
            if (owner == null)
            {
                return false;
            }

            RectInt ownerRect = DisplayRect(room, owner);
            RectInt markerRect = DisplayRect(room, box);
            anchor = CellRect(
                ownerRect.x,
                ownerRect.y,
                ownerRect.width,
                ownerRect.height).center;
            marker = CellRect(
                markerRect.x,
                markerRect.y,
                1,
                1).center;
            localX = markerRect.x - ownerRect.x;
            return true;
        }

        private static bool ReadBoolOverride(
            IReadOnlyList<LevelEditorComponentValueOverride> values,
            string propertyPath,
            bool fallback)
        {
            if (values == null)
            {
                return fallback;
            }

            string typeName =
                typeof(Project.BlockFeatures.RespawnAnchorFeature)
                    .FullName;
            for (int index = 0; index < values.Count; index++)
            {
                LevelEditorComponentValueOverride value =
                    values[index];
                if (value != null &&
                    string.IsNullOrEmpty(value.ComponentPath) &&
                    value.ComponentTypeName == typeName &&
                    value.PropertyPath == propertyPath)
                {
                    return value.Value == "1";
                }
            }

            return fallback;
        }

        private static bool HasBossOwnerOverride(
            IReadOnlyList<LevelEditorComponentValueOverride> values,
            string propertyPath)
        {
            if (values == null)
            {
                return false;
            }

            string typeName =
                typeof(Project.BlockFeatures.RespawnAnchorFeature)
                    .FullName;
            for (int index = 0; index < values.Count; index++)
            {
                LevelEditorComponentValueOverride value =
                    values[index];
                if (value != null &&
                    string.IsNullOrEmpty(value.ComponentPath) &&
                    value.ComponentTypeName == typeName &&
                    value.PropertyPath == propertyPath)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ReadFloatOverride(
            IReadOnlyList<LevelEditorComponentValueOverride> values,
            string propertyPath,
            float fallback)
        {
            if (values == null)
            {
                return fallback;
            }

            string typeName =
                typeof(Project.BlockFeatures.RespawnAnchorFeature)
                    .FullName;
            for (int index = 0; index < values.Count; index++)
            {
                LevelEditorComponentValueOverride value =
                    values[index];
                if (value == null ||
                    !string.IsNullOrEmpty(value.ComponentPath) ||
                    value.ComponentTypeName != typeName ||
                    value.PropertyPath != propertyPath)
                {
                    continue;
                }

                return float.TryParse(
                    value.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo
                        .InvariantCulture,
                    out float parsed)
                    ? parsed
                    : fallback;
            }

            return fallback;
        }

        private void SetBossMarkerLocalX(
            PlanningBox owner,
            float localX)
        {
            LevelEditorPropEntry prop = FindPropEntry(
                owner.propEntryId,
                owner.propEntryName);
            Project.BlockFeatures.RespawnAnchorFeature anchorFeature =
                prop?.Prefab != null
                    ? prop.Prefab.GetComponent<
                        Project.BlockFeatures
                            .RespawnAnchorFeature>()
                    : null;
            if (anchorFeature == null)
            {
                return;
            }

            SetBossOwnerOverride(
                owner,
                "bossRespawnLocalX",
                "Float",
                localX.ToString(
                    "R",
                    System.Globalization.CultureInfo
                        .InvariantCulture));
        }

        private void DrawDoorLinks(Painter2D painter)
        {
            using var profile = DrawLinksMarker.Auto();
            if (editWaterLayer) return;
            EnsurePreviewLinks();
            var byId = previewLinksById;
            DrawMechanismLinks(painter, previewMechanisms, byId);
            DrawContactTimelineRanges(painter);
            foreach (var item in previewDoorButtons)
            {
                if (string.IsNullOrEmpty(item.box.doorOwnerId) ||
                    (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId)) continue;
                if (byId.TryGetValue(item.box.doorOwnerId, out var door))
                {
                    RectInt a = DisplayRect(item.room, item.box), b = DisplayRect(door.room, door.box);
                    Vector2 start = CellRect(a.x, a.y, a.width, a.height).center;
                    Vector2 end = CellRect(b.x, b.y, b.width, b.height).center;
                    Dashed(painter, start, end, new Color(1f,.78f,.3f,.9f));
                }
            }
        }

        private void BeginBaseBinding(string id)
        {
            bindingConsumerId = id;
            pickingSignalSource = false;
            StatusChanged?.Invoke("点击基座完成绑定，右键取消。");
        }
        private void BeginSignalBinding(string id)
        {
            bindingConsumerId = id;
            pickingSignalSource = true;
            StatusChanged?.Invoke("点击按钮或曲柄绑定信号源；按钮可连续添加，右键结束。");
        }
        private void ClearBaseBinding(string id)
        {
            foreach (var item in AllBoxes()) if (item.box.id == id)
            {
                item.box.mechanismBaseId = null;
                item.box.mechanismBaseCellOffset = default;
                NotifyDocumentChanged(); MarkDirtyRepaint(); StatusChanged?.Invoke("已解除绑定。"); return;
            }
        }
        private void ClearSignalBinding(string id)
        {
            foreach (var item in AllBoxes()) if (item.box.id == id)
            {
                item.box.NormalizeMechanismButtonLinks();
                item.box.mechanismButtonIds.Clear();
                item.box.crankOwnerId = null;
                NotifyDocumentChanged(); MarkDirtyRepaint(); StatusChanged?.Invoke("已清空信号源绑定。"); return;
            }
        }
        private void BindMechanismAt(Vector2Int cell)
        {
            PlanningBox consumer = null, selected = null;
            GameObject selectedPrefab = null;
            Vector2Int selectedOffset = default;
            foreach (var item in AllBoxes())
            {
                if (item.box.id == bindingConsumerId) consumer = item.box;
                if (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId) continue;
                GameObject targetPrefab = FindPropEntry(item.box.propEntryId, item.box.propEntryName)?.Prefab;
                bool targetMatches = pickingSignalSource
                    ? targetPrefab != null &&
                      (targetPrefab.GetComponent<Project.Mechanisms.DoorButton>() != null ||
                       targetPrefab.GetComponent<Project.BlockFeatures.ButtonLockFeature>() != null ||
                       targetPrefab.GetComponent<Project.BlockFeatures.CrankPlatformFeature>() != null)
                    : targetPrefab != null &&
                      targetPrefab.GetComponent<Project.BlockFeatures.MechanismBaseFeature>() != null;
                if (ShapeContains(item.room, item.box, cell) && targetMatches)
                {
                    selected = item.box;
                    selectedPrefab = targetPrefab;
                    selectedOffset = cell - DisplayRect(item.room,item.box).position;
                }
            }
            if (consumer == null) { bindingConsumerId = null; pickingSignalSource = false; return; }
            if (selected == null)
            {
                StatusChanged?.Invoke(pickingSignalSource
                    ? "请选择门按钮、红色按钮或曲柄。"
                    : "请选择基座。跨房间绑定可在装配图操作。");
                return;
            }
            if (pickingSignalSource)
            {
                if (IsCrankBox(consumer) && (selected.id == consumer.id || !IsCrankBox(selected) ||
                    !string.IsNullOrEmpty(selected.crankOwnerId) ||
                    AllBoxes().Any(item => IsCrankBox(item.box) && item.box.crankOwnerId == consumer.id)))
                {
                    StatusChanged?.Invoke("请选择另一独立曲柄；共享曲柄不能串联或循环绑定。");
                    return;
                }
                if (selectedPrefab.GetComponent<Project.BlockFeatures.CrankPlatformFeature>() != null)
                {
                    consumer.crankOwnerId = selected.id;
                    bindingConsumerId = null;
                    pickingSignalSource = false;
                    NotifyDocumentChanged();
                    MarkDirtyRepaint();
                    StatusChanged?.Invoke(IsCrankBox(consumer) ? "已共享曲柄进度；两个位置控制同一组平台。" : "已绑定曲柄；平台进入曲柄控制。");
                    return;
                }

                consumer.NormalizeMechanismButtonLinks();
                if (!consumer.mechanismButtonIds.Contains(selected.id))
                    consumer.mechanismButtonIds.Add(selected.id);
                NotifyDocumentChanged();
                MarkDirtyRepaint();
                StatusChanged?.Invoke("已添加按钮信号源；可继续点击按钮或曲柄，右键结束。");
                return;
            }
            consumer.mechanismBaseId = selected.id;
            consumer.mechanismBaseCellOffset = selectedOffset;
            bindingConsumerId = null; pickingSignalSource = false;
            NotifyDocumentChanged(); MarkDirtyRepaint(); StatusChanged?.Invoke("已完成绑定。");
        }
        private void DrawMechanismLinks(Painter2D painter,
            List<(PlanningRoom room, List<PlanningBox> boxes, PlanningBox box)> items,
            Dictionary<string, (PlanningRoom room, PlanningBox box)> byId)
        {
            using var profile = DrawMechanismLinksMarker.Auto();
            foreach (var item in items)
            {
                if (mode == PlanningCanvasMode.Detail && item.room?.id != selectedRoomId) continue;
                var prefab = FindPropEntry(item.box.propEntryId, item.box.propEntryName)?.Prefab;
                var feature = PreviewComponent<Project.BlockFeatures.MovingPlatformFeature>(prefab);
                if (feature == null && PreviewComponent<Project.BlockFeatures.CrankPlatformFeature>(prefab) == null) continue;
                RectInt rect = DisplayRect(item.room, item.box);
                Vector2 start = CellRect(rect.x, rect.y, rect.width, rect.height).center;
                var values = item.box.hasComponentOverrides ? item.box.componentOverrides : FindPropEntry(item.box.propEntryId, item.box.propEntryName)?.ComponentValueOverrides;
                if (feature != null)
                {
                    if (!previewPlatformPaths.TryGetValue(item.box, out var path))
                        previewPlatformPaths[item.box] = path = PlanningPlatformPathUtility.Resolve(feature, values);
                    Vector2 ScreenOffset(Vector2 cells) => new Vector2(cells.x, -cells.y) * GridSize;
                    Color pathColor = new Color(.3f,.9f,1f,.85f);
                    Dashed(painter, start + ScreenOffset(path.PreviewFrom), start + ScreenOffset(path.PreviewTo), pathColor);
                    Vector2 first = ScreenOffset(path.Axis * path.FirstEnd);
                    if (first.sqrMagnitude > 16f)
                    {
                        Vector2 tip = start + first.normalized * Mathf.Min(18f, first.magnitude);
                        Vector2 side = new Vector2(-first.y, first.x).normalized * 3f;
                        DrawLine(painter, tip, tip - first.normalized * 6f + side, pathColor, 2f);
                        DrawLine(painter, tip, tip - first.normalized * 6f - side, pathColor, 2f);
                    }
                    if (path.IsRange)
                    {
                        Vector2 center = start + ScreenOffset(path.Axis * path.Center);
                        DrawLine(painter, center - Vector2.right * 4f, center + Vector2.right * 4f, pathColor, 2f);
                        DrawLine(painter, center - Vector2.up * 4f, center + Vector2.up * 4f, pathColor, 2f);
                    }
                }
                if (!string.IsNullOrEmpty(item.box.crankOwnerId) && byId.TryGetValue(item.box.crankOwnerId, out var controller))
                {
                    RectInt target = DisplayRect(controller.room,controller.box);
                    Dashed(painter,start,CellRect(target.x,target.y,target.width,target.height).center,new Color(1f,.78f,.3f,.9f));
                }
                if (!string.IsNullOrEmpty(item.box.mechanismBaseId) && byId.TryGetValue(item.box.mechanismBaseId, out var source))
                {
                    RectInt b = DisplayRect(source.room, source.box);
                    Dashed(painter, start, CellRect(b.x+item.box.mechanismBaseCellOffset.x,b.y+item.box.mechanismBaseCellOffset.y,1,1).center, new Color(1f,.45f,.8f,.9f));
                }
                item.box.NormalizeMechanismButtonLinks();
                foreach (string buttonId in item.box.mechanismButtonIds)
                {
                    if (!string.IsNullOrEmpty(buttonId) && byId.TryGetValue(buttonId, out var button))
                    {
                        RectInt b = DisplayRect(button.room, button.box);
                        Dashed(painter, start, CellRect(b.x,b.y,b.width,b.height).center, new Color(1f,.35f,.25f,.9f));
                    }
                }
            }
        }

        private void DrawContactTimelineRanges(
            Painter2D painter)
        {
            using var profile = DrawTimelineRangesMarker.Auto();
            Color color =
                new Color(1f, .78f, .3f, .9f);
            EnsurePreviewLinks();
            foreach (var item in previewTimelines)
            {
                if (mode == PlanningCanvasMode.Detail &&
                    item.room?.id != selectedRoomId)
                {
                    continue;
                }

                LevelEditorPropEntry entry = FindPropEntry(
                    item.box.propEntryId,
                    item.box.propEntryName);
                GameObject prefab = entry?.Prefab;
                if (prefab == null ||
                    PreviewComponent<
                        Project.BlockFeatures
                            .ContactTimelineTrigger>(prefab) ==
                    null)
                {
                    continue;
                }

                BoxCollider collider =
                    PreviewComponent<BoxCollider>(prefab);
                Vector3 size = collider != null
                    ? collider.size
                    : Vector3.one;
                Vector3 center = collider != null
                    ? collider.center
                    : Vector3.zero;
                IReadOnlyList<
                    LevelEditorComponentValueOverride>
                    overrides = item.box
                        .hasComponentOverrides
                        ? item.box.componentOverrides
                        : entry.ComponentValueOverrides;
                if (overrides != null)
                {
                    foreach (LevelEditorComponentValueOverride
                             value in overrides)
                    {
                        if (value == null)
                        {
                            continue;
                        }

                        if (value.PropertyPath == "m_Size")
                        {
                            size = ParseVector3(
                                value.Value,
                                size);
                        }
                        else if (
                            value.PropertyPath ==
                            "m_Center")
                        {
                            center = ParseVector3(
                                value.Value,
                                center);
                        }
                    }
                }

                RectInt rect = DisplayRect(
                    item.room,
                    item.box);
                Rect footprint = CellRect(
                    rect.x,
                    rect.y,
                    rect.width,
                    rect.height);
                float width = Mathf.Max(
                    GridSize,
                    Mathf.Abs(size.x) * GridSize);
                float height = Mathf.Max(
                    GridSize,
                    Mathf.Abs(size.y) * GridSize);
                Vector2 rangeCenter =
                    footprint.center +
                    new Vector2(
                        center.x,
                        -center.y) * GridSize;
                Rect range = Rect.MinMaxRect(
                    rangeCenter.x - width * .5f,
                    rangeCenter.y - height * .5f,
                    rangeCenter.x + width * .5f,
                    rangeCenter.y + height * .5f);
                Dashed(
                    painter,
                    new Vector2(range.xMin, range.yMin),
                    new Vector2(range.xMax, range.yMin),
                    color);
                Dashed(
                    painter,
                    new Vector2(range.xMax, range.yMin),
                    new Vector2(range.xMax, range.yMax),
                    color);
                Dashed(
                    painter,
                    new Vector2(range.xMax, range.yMax),
                    new Vector2(range.xMin, range.yMax),
                    color);
                Dashed(
                    painter,
                    new Vector2(range.xMin, range.yMax),
                    new Vector2(range.xMin, range.yMin),
                    color);
            }
        }

        private static Vector3 ParseVector3(
            string value,
            Vector3 fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            try
            {
                return JsonUtility.FromJson<Vector3>(
                    value);
            }
            catch
            {
                return fallback;
            }
        }

        private void Dashed(Painter2D painter, Vector2 start, Vector2 end, Color color)
        {
            if (!PlanningPlatformPathUtility.ClipLine(contentRect, ref start, ref end)) return;
            float length = Vector2.Distance(start,end);
            for (float d = 0; d < length; d += 12f) DrawLine(painter, Vector2.Lerp(start,end,d/length), Vector2.Lerp(start,end,Mathf.Min(d+6,length)/length), color, 2f);
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
            foreach (var item in HitOrder())
            {
                var rect = DisplayRect(item.room, item.box);
                if (!ShapeContains(item.room, item.box, cell)) continue;
                CarveBox(item.boxes, item.box, cell - rect.position + new Vector2Int(item.box.x, item.box.y));
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

        private void CarveBox(
            List<PlanningBox> boxes,
            PlanningBox box,
            Vector2Int cell)
        {
            boxes.Remove(box);
            if (PlanningDoorUtility.IsDoor(box)) boxes.RemoveAll(item => item.doorOwnerId == box.id);
            if (box.isMerged || box.singleInstance || !string.IsNullOrEmpty(box.doorOwnerId))
            {
                PlanningMechanismUtility.RemapCarvedSource(box, Array.Empty<PlanningBox>(), AllBoxes().Select(item => item.box));
                return;
            }
            int firstPartIndex = boxes.Count;
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
            PlanningMechanismUtility.RemapCarvedSource(box, boxes.GetRange(firstPartIndex, boxes.Count-firstPartIndex), AllBoxes().Select(item => item.box));
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

            source.NormalizeMechanismButtonLinks();
            var carved = new PlanningBox(
                source.type,
                new RectInt(x, y, width, height))
            {
                label = source.label,
                paletteEntryId = source.paletteEntryId,
                paletteEntryName = source.paletteEntryName,
                propEntryId = source.propEntryId,
                propEntryName = source.propEntryName,
                mechanismBaseId = source.mechanismBaseId,
                mechanismBaseCellOffset = source.mechanismBaseCellOffset,
                mechanismButtonIds = new List<string>(source.mechanismButtonIds),
                crankOwnerId = source.crankOwnerId,
                platformMountId = source.platformMountId,
                initialCrankPlatformCreated = source.initialCrankPlatformCreated
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

        private PlanningBox FindBoxAt(
            PlanningRoom room,
            int x,
            int y)
        {
            var ordered = room.boxes.OrderBy(b => PlanningWaterLayerUtility.IsWater(b, palette) ? 0 : 1).ToList();
            for (int index = ordered.Count - 1; index >= 0; index--)
            {
                PlanningBox box = ordered[index];
                if (editWaterLayer && !PlanningWaterLayerUtility.IsWater(box, palette)) continue;
                if (PlanningShapeUtility.Contains(box, new Vector2Int(x, y)))
                {
                    return box;
                }
            }

            return null;
        }

        private void NotifyDocumentChanged()
        {
            PlanningShapeUtility.Invalidate();
            if (suppressDocumentChanged)
            {
                pendingDocumentChanged = true;
                return;
            }

            document?.RefreshConnectorPaths();
            InvalidatePreviewData();
            EnsureInitialCrankPlatforms();
            if (document != null) PlanningDoorUtility.RemoveOrphanButtons(document);
            PlanningMechanismUtility.RemoveMissingLinks(AllBoxes().Select(item => item.box));
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
            Require(room.boxes.Count==1 && room.boxes[0].isMerged &&
                PlanningShapeUtility.Contains(room.boxes[0],new Vector2Int(0,0)) &&
                !PlanningShapeUtility.Contains(room.boxes[0],new Vector2Int(1,0)) &&
                PlanningShapeUtility.Contains(room.boxes[0],new Vector2Int(2,0)),"不规则合并保留孔洞");
            room.boxes.Clear(); testCanvas.mergeSelection.Clear();
            var rectangle=new PlanningBox(PlanningDetailType.Solid,new RectInt(0,0,2,2)) { hasComponentOverrides=true };
            room.boxes.Add(rectangle);
            testCanvas.RemoveMissingSceneItems(new []{rectangle.id,rectangle.id},new []{new Vector2Int(0,0),new Vector2Int(1,1)});
            Require(room.boxes.Count==2 && room.boxes.TrueForAll(b=>b.hasComponentOverrides),"矩形批量删除保留参数");
            Require(changes==5,"每个数据事务仅通知一次");
            Debug.Log("关卡编辑器规划验证通过：选择、合并/解除、存档、孔洞保留、删除回流与事务通知。");
        }

        private static void Require(bool condition,string operation)
        {
            if(!condition) throw new InvalidOperationException("关卡编辑器验证失败："+operation);
        }
    }
}
