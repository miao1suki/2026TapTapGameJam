using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    public sealed partial class GridArtWindow
    {
        private bool roomOverview;
        private Vector2 drawingPan;
        private float drawingZoom;

        private void SetRoomOverview(bool value)
        {
            if (canvas == null || IsStroking || value == roomOverview) return;
            if (value)
            {
                StopScenePainting(); EffectCancelGesture(); EndPreview();
                drawingPan = canvas.ViewPan; drawingZoom = canvas.ViewZoom;
            }
            roomOverview = value;
            if (value) canvas.FrameLevel();
            else { canvas.ViewPan = drawingPan; canvas.ViewZoom = drawingZoom; }
            canvas.RepaintView();
            RefreshOverviewControls();
        }

        private void OpenReferenceRoom()
        {
            if (IsStroking) return;
            SetRoomOverview(false);
            canvas?.FrameRoom(selectedReferenceRoom);
        }

        private void RefreshOverviewControls()
        {
            var button = rootVisualElement.Q<Button>("art-room-overview");
            if (button != null)
            {
                button.text = roomOverview ? "返回美术" : "装配图";
                button.SetEnabled(levelSource != null && levelSource.rooms.Count > 0);
            }
            rootVisualElement.Q<Button>("art-enter-room")?.SetEnabled(levelSource != null && levelSource.rooms.Count > 0);
            var hint = rootVisualElement.Q<Label>("art-canvas-hint");
            if (hint != null) hint.text = roomOverview
                ? "房间装配图 · 单击选择 · 双击进入绘制 · 中键平移 · 滚轮缩放（只读，不移动房间）"
                : "左键绘制 · 右键擦除 · 中键平移 · 滚轮缩放 · N 开关工作台";
        }

        private sealed partial class ArtCanvas
        {
            internal Vector2 ViewPan { get => pan; set => pan = value; }
            internal float ViewZoom { get => zoom; set => zoom = value; }
            private readonly Dictionary<string, Label> roomLabels = new Dictionary<string, Label>();
            private float ReferenceCellSize => Mathf.Max(.01f, owner.map != null ? owner.map.cellSize : owner.levelSource?.cellSize ?? 1f);
            private Rect RoomScreen(Rect world) => Screen(new Rect(world.position / ReferenceCellSize, world.size / ReferenceCellSize));
            private Vector2 RoomPoint(Vector2 world) => new Vector2(pan.x + world.x / ReferenceCellSize * zoom,
                pan.y - world.y / ReferenceCellSize * zoom);

            private void SelectOverviewRoom(Vector2 screen, bool enter)
            {
                if (owner.levelSource == null) return;
                Vector2 world = new Vector2((screen.x - pan.x) / zoom, (pan.y - screen.y) / zoom) * ReferenceCellSize;
                GridArtLevelRoom picked = null;
                foreach (var room in owner.levelSource.rooms)
                    if (room.Contains(world) && (picked == null || room.boundsWorld.width * room.boundsWorld.height <
                        picked.boundsWorld.width * picked.boundsWorld.height)) picked = room;
                if (picked == null) return;
                owner.selectedReferenceRoom = picked.id;
                EditorPrefs.SetString(owner.SceneKey + ".ReferenceRoom", picked.id);
                owner.RefreshNavigation(); RepaintView();
                if (enter) owner.OpenReferenceRoom();
            }

            private void RefreshRoomLabels()
            {
                // Retained labels, not one text element per tile or a scene search.
                foreach (var label in roomLabels.Values) label.style.display = DisplayStyle.None;
                if (!owner.roomOverview || owner.levelSource == null) return;
                foreach (var room in owner.levelSource.rooms)
                {
                    Rect rect = RoomScreen(room.boundsWorld);
                    if (!rect.Overlaps(contentRect)) continue;
                    if (!roomLabels.TryGetValue(room.id, out var label))
                    {
                        label = new Label { pickingMode = PickingMode.Ignore };
                        label.style.position = Position.Absolute;
                        label.style.fontSize = 12; label.style.color = Color.white;
                        label.style.backgroundColor = new Color(.08f, .1f, .13f, .85f);
                        label.style.paddingLeft = label.style.paddingRight = 4;
                        roomLabels.Add(room.id, label); Add(label);
                    }
                    label.text = (room.id == owner.selectedReferenceRoom ? "● " : "") + room.label;
                    label.tooltip = room.id;
                    var center = room.boundsWorld.center;
                    if (!room.Contains(center) && room.areasWorld.Count > 0) center = room.areasWorld[0].center;
                    Vector2 anchor = RoomPoint(center);
                    label.style.left = Mathf.Clamp(anchor.x - 45, 0, Mathf.Max(0, contentRect.width - 100));
                    label.style.top = Mathf.Clamp(anchor.y - 10, 0, Mathf.Max(0, contentRect.height - 20));
                    label.style.display = DisplayStyle.Flex;
                }
            }

            private void DrawRoomOverview(Painter2D painter)
            {
                if (owner.levelSource == null) return;
                foreach (var room in owner.levelSource.rooms)
                {
                    if (!RoomScreen(room.boundsWorld).Overlaps(contentRect)) continue;
                    bool selected = room.id == owner.selectedReferenceRoom;
                    Color tint = Color.HSVToRGB(((uint)room.id.GetHashCode() % 1000) / 1000f, .35f, .65f);
                    tint.a = selected ? .65f : .28f; painter.fillColor = tint;
                    painter.BeginPath();
                    if (room.areasWorld.Count == 0) Rectangle(room.boundsWorld);
                    else foreach (var area in room.areasWorld) Rectangle(area);
                    painter.Fill();
                    painter.strokeColor = selected ? new Color(.3f, .85f, 1) : new Color(.6f, .7f, .8f, .8f);
                    painter.lineWidth = selected ? 3 : 1;
                    if (room.edgesWorld.Count > 0)
                    {
                        painter.BeginPath();
                        foreach (var edge in room.edgesWorld) { painter.MoveTo(RoomPoint(edge.from)); painter.LineTo(RoomPoint(edge.to)); }
                    }
                    painter.Stroke();
                    void Rectangle(Rect area)
                    {
                        Rect r = RoomScreen(area);
                        painter.MoveTo(r.min); painter.LineTo(new Vector2(r.xMax, r.yMin));
                        painter.LineTo(r.max); painter.LineTo(new Vector2(r.xMin, r.yMax)); painter.ClosePath();
                    }
                }
            }
        }
    }
}
