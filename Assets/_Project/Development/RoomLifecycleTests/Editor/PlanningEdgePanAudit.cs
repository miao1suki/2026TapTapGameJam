using System;
using System.IO;
using System.Reflection;
using PlanningEditorPrototype;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// Exercises the scheduled callbacks, not a direct call to TickEdgePan.
public static class PlanningEdgePanAudit
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static PlanningCanvas canvas;
    private static EditorInteractionAuditWindow window;
    private static PlanningRoom room;
    private static PlanningBox box;
    private static LevelEditorTool previousTool;
    private static int phase, index, checks, changes;
    private static double due, deadline;
    private static Vector2 beforePan, afterPan, worldOrigin, pointer;
    private static Vector2Int afterBox;
    private static readonly Vector2[] Directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

    private static object Field(string name) => typeof(PlanningCanvas).GetField(name, Hidden).GetValue(canvas);
    private static object Call(string name, params object[] args) => typeof(PlanningCanvas).GetMethod(name, Hidden).Invoke(canvas, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("EDGE PAN: " + message);
        checks++; Debug.Log("EDGE PAN PASS: " + message);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("此验证仅可在隔离工程执行。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        previousTool = LevelEditorState.Tool;
        phase = index = checks = 0; deadline = EditorApplication.timeSinceStartup + 60;
        canvas = new PlanningCanvas();
        canvas.DocumentChanged += () => changes++;
        window = ScriptableObject.CreateInstance<EditorInteractionAuditWindow>();
        window.position = new Rect(60, 60, 1100, 780);
        window.rootVisualElement.Add(canvas); window.Show();
        EditorApplication.update += Tick;
    }
    private static void Prepare()
    {
        canvas.CancelPointerInteraction();
        var document = new PlanningDocument { name = "边缘拖拽验证", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        room = new PlanningRoom("验证");
        foreach (var cell in new RectInt(0, 0, 80, 80).allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        int boxCell = index == 16 || index == 17 ? 20 : 40;
        box = new PlanningBox(PlanningDetailType.Solid, new RectInt(boxCell, boxCell, 1, 1));
        room.boxes.Add(box); document.rooms.Add(room);
        canvas.SetDocument(document);
        canvas.SetMode(index < 8 || index == 16 ? PlanningCanvasMode.Detail :
            index < 12 || index == 17 ? PlanningCanvasMode.Assembly : PlanningCanvasMode.World);
        LevelEditorState.Tool = index >= 16 ? LevelEditorTool.Paint : LevelEditorTool.Select;
        if (canvas.Mode == PlanningCanvasMode.World)
            canvas.SetMapTool(index == 18 ? PlanningMapTool.Region : PlanningMapTool.Select);
        canvas.RestoreView(Vector2.zero, 1f);
        var center = ((Rect)Call("CellRect", 40, 40, 1, 1)).center;
        canvas.RestoreView(canvas.contentRect.center - center, 1f);
        changes = 0;
        SendDown(((Rect)Call("CellRect", 40, 40, 1, 1)).center);
        Check(canvas.HasPointerCapture(0), "gesture owns pointer " + index);
        worldOrigin = (Vector2)Call("ScreenToWorld", Field("dragStart"));
        var direction = Directions[index % 4];
        // First four cover a stationary cursor just inside; subsequent cases
        // deliberately overshoot into the surrounding UI and send a leave event.
        float outside = index < 4 ? -3f : 30f;
        var r = canvas.contentRect;
        pointer = r.center + Vector2.Scale(direction, r.size * .5f + Vector2.one * outside);
        SendMove(pointer);
        if (index >= 4) SendLeave(pointer);
        beforePan = canvas.Pan;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Edge pan audit timed out at " + index + "/" + phase);
            if (canvas.panel == null || canvas.contentRect.width < 500 || EditorApplication.timeSinceStartup < due) return;
            if (phase == 0)
            {
                Prepare(); phase = 1; due = EditorApplication.timeSinceStartup + .22; return;
            }
            if (phase == 1)
            {
                var direction = Directions[index % 4];
                Check(Vector2.Dot(canvas.Pan - beforePan, direction) < -1f,
                    canvas.Mode + " scheduled stationary " + (index < 4 ? "inside" : "outside") + " edge " + direction);
                Check(((Vector2)Call("ScreenToWorld", Field("dragStart")) - worldOrigin).sqrMagnitude < .001f, "world grab origin does not drift " + index);
                Check(changes == 0, "panning does not commit intermediate history " + index);
                if (index >= 16)
                    Check(room.boxes.Count == 1 && canvas.Document.regions.Count == 0, "marquee does not create items before release " + index);
                else if (canvas.Mode != PlanningCanvasMode.World)
                {
                    Vector2 center = ((Rect)Call("CellRect", box.x, box.y, 1, 1)).center;
                    Check((center - pointer).magnitude < (float)typeof(PlanningCanvas).GetProperty("GridSize", Hidden).GetValue(canvas), "object follows cursor rather than viewport " + index);
                }
                SendMove(canvas.contentRect.center);
                afterPan = canvas.Pan; phase = 2; due = EditorApplication.timeSinceStartup + .12; return;
            }
            if (phase == 2)
            {
                Check(canvas.Pan == afterPan, "return to center stops scrolling " + index);
                SendMove(pointer);
                var startCell = (Vector2Int)Call("CellAt", Field("dragStart"));
                var endCell = (Vector2Int)Call("CellAt", pointer);
                var area = new RectInt(Mathf.Min(startCell.x, endCell.x), Mathf.Min(startCell.y, endCell.y),
                    Mathf.Abs(endCell.x - startCell.x) + 1, Mathf.Abs(endCell.y - startCell.y) + 1);
                SendUp(pointer);
                Check(!canvas.HasPointerCapture(0) && !(bool)Field("isDragging"), "outside release clears capture " + index);
                Check(changes == 1, "one drag creates one transaction " + index);
                if (index >= 16)
                    Check(index == 18 ? canvas.Document.regions.Count == 1 :
                        room.boxes.Count == 1 + (index == 17 ? area.width * area.height : 1),
                        "scrolled marquee commits expected coverage in one transaction " + index);
                afterPan = canvas.Pan; afterBox = new Vector2Int(box.x, box.y);
                phase = 3; due = EditorApplication.timeSinceStartup + .12; return;
            }
            Check(canvas.Pan == afterPan && new Vector2Int(box.x, box.y) == afterBox, "release stops timer and object " + index);
            if (++index < 19) { phase = 0; return; }
            // Capture loss and cancellation must also stop an overshot drag.
            index = 5; Prepare(); canvas.ReleasePointer(0); afterPan = canvas.Pan;
            canvas.TickEdgePan(EditorApplication.timeSinceStartup + .1);
            Check(!(bool)Field("isDragging"), "capture loss clears drag on next timer tick");
            Check(canvas.Pan == afterPan, "capture loss stops edge motion");
            Prepare(); canvas.CancelPointerInteraction(); afterPan = canvas.Pan;
            canvas.TickEdgePan(EditorApplication.timeSinceStartup + .1);
            Check(canvas.Pan == afterPan && !canvas.HasPointerCapture(0), "cancel stops overshot drag");
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void SendDown(Vector2 local)
    {
        using var e = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = canvas.LocalToWorld(local) });
        e.target = canvas; canvas.SendEvent(e);
    }
    private static void SendMove(Vector2 local)
    {
        using var e = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = canvas.LocalToWorld(local) });
        typeof(PointerMoveEvent).GetProperty("pressedButtons").SetValue(e, 1);
        e.target = canvas; canvas.SendEvent(e);
    }
    private static void SendUp(Vector2 local)
    {
        using var e = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = canvas.LocalToWorld(local) });
        e.target = canvas; canvas.SendEvent(e);
    }
    private static void SendLeave(Vector2 local)
    {
        using var e = PointerLeaveEvent.GetPooled(new Event { type = EventType.MouseLeaveWindow, mousePosition = canvas.LocalToWorld(local) });
        e.target = canvas; canvas.SendEvent(e);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        canvas?.CancelPointerInteraction();
        if (window != null) window.Close();
        LevelEditorState.Tool = previousTool;
        File.WriteAllText("Library/PlanningEdgePanResult.txt", error == null ? "PASS " + checks : "FAIL " + error);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
