using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures.Editor;
using Project.LevelEditor.Editor;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Attached UI Toolkit input and geometry checks. Writes only a report in Library.
public static class EditorInteractionAudit
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static EditorInteractionAuditWindow window;
    private static PlanningCanvas canvas;
    private static PlanningDocument document;
    private static PlanningRoom room;
    private static LevelEditorTool previousTool;
    private static int checks, changes;
    private static double start;
    private static object Field(object owner, string name) => owner.GetType().GetField(name, Hidden).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("EDITOR INTERACTION: " + label);
        checks++; Debug.Log("EDITOR INTERACTION PASS: " + label);
    }
    private static Vector2 At(int x, int y) => ((Rect)Call(canvas, "CellRect", x, y, 1, 1)).center;
    private static void Down(Vector2 local)
    {
        using var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = canvas.LocalToWorld(local) });
        evt.target = canvas; canvas.SendEvent(evt);
    }
    private static void Move(Vector2 local, int buttons = 1)
    {
        using var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = canvas.LocalToWorld(local) });
        typeof(PointerMoveEvent).GetProperty("pressedButtons").SetValue(evt, buttons);
        evt.target = canvas; canvas.SendEvent(evt);
    }
    private static void Up(Vector2 local)
    {
        using var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = canvas.LocalToWorld(local) });
        evt.target = canvas; canvas.SendEvent(evt);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("此验证仅可在 Library/FlowRetirementProbe 隔离工程执行。");
        checks = changes = 0; start = EditorApplication.timeSinceStartup;
        previousTool = LevelEditorState.Tool;
        document = new PlanningDocument { name = "编辑输入验证", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        room = new PlanningRoom("验证");
        foreach (var cell in new RectInt(0, 0, 80, 80).allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        room.boxes.Add(new PlanningBox(PlanningDetailType.Solid, new RectInt(4, 4, 1, 1)));
        document.rooms.Add(room);
        canvas = new PlanningCanvas(); canvas.SetDocument(document); canvas.SetMode(PlanningCanvasMode.Detail);
        canvas.DocumentChanged += () => changes++;
        window = ScriptableObject.CreateInstance<EditorInteractionAuditWindow>();
        window.titleContent = new GUIContent("编辑输入验证");
        window.position = new Rect(70, 70, 1100, 780);
        window.rootVisualElement.Add(canvas); window.Show();
        EditorApplication.update += WaitForLayout;
    }
    private static void WaitForLayout()
    {
        if (EditorApplication.timeSinceStartup - start > 35) { Finish(new Exception("Timed out waiting for UI Toolkit layout")); return; }
        if (canvas.panel == null || canvas.contentRect.width < 500 || canvas.contentRect.height < 400) return;
        EditorApplication.update -= WaitForLayout;
        try { Execute(); Finish(null); }
        catch (Exception exception) { Finish(exception); }
    }
    private static void Execute()
    {
        canvas.RestoreView(new Vector2(10, 10), 1);
        var rect = new Rect(0, 0, 800, 600);
        Check(PlanningCanvas.EdgePanVelocity(rect, new Vector2(400, 300)) == Vector2.zero, "center does not pan");
        Check(PlanningCanvas.EdgePanVelocity(rect, new Vector2(3, 300)).x > 0, "left edge pans view right");
        Check(PlanningCanvas.EdgePanVelocity(rect, new Vector2(797, 300)).x < 0, "right edge pans view left");
        Check(PlanningCanvas.EdgePanVelocity(rect, new Vector2(400, 3)).y > 0, "top edge pans down");
        Check(PlanningCanvas.EdgePanVelocity(rect, new Vector2(400, 597)).y < 0, "bottom edge pans up");
        Check(PlanningCanvas.EdgePanVelocity(rect, new Vector2(-1, 20)) == Vector2.zero, "outside viewport stops edge pan");
        Check(PlanningCanvas.EdgePanVelocity(rect, new Vector2(1, 300)).magnitude > PlanningCanvas.EdgePanVelocity(rect, new Vector2(40, 300)).magnitude, "deeper edge contact accelerates");

        LevelEditorState.Tool = LevelEditorTool.Select;
        var box = room.boxes[0];
        Down(At(4, 4));
        Check((bool)Field(canvas, "isDragging") && canvas.HasPointerCapture(0), "pointer down captures selection");
        Move(At(5, 4)); Up(At(5, 4));
        Check(box.x == 5 && box.y == 4, "release applies final selection position");
        Check(changes == 1, "selection move commits once");
        Check(!(bool)Field(canvas, "isDragging") && !canvas.HasPointerCapture(0), "release clears and releases capture");
        Move(At(12, 10), 0);
        Check(box.x == 5 && box.y == 4 && changes == 1, "unclicked hover cannot keep moving selected object");

        Down(At(5, 4)); Move(At(6, 4));
        Move(At(7, 4), 0);
        Check(!(bool)Field(canvas, "isDragging") && !canvas.HasPointerCapture(0), "lost mouse-up is recovered from pressed buttons");
        Check(box.x == 6 && changes == 2, "lost mouse-up commits last held position once");
        Down(At(6, 4)); Move(At(7, 4));
        canvas.ReleasePointer(0); Move(At(8, 4));
        Check(!(bool)Field(canvas, "isDragging") && !canvas.HasPointerCapture(0), "capture loss ends drag");
        Check(box.x == 7 && changes == 3, "capture loss preserves only actual held move");
        Down(At(7, 4)); Up(At(7, 4));
        Check(changes == 3, "plain selection click creates no history transaction");

        LevelEditorState.Tool = LevelEditorTool.Paint;
        int before = room.boxes.Count;
        Down(At(2, 2));
        Vector2 initial = (Vector2)Call(canvas, "ScreenToWorld", Field(canvas, "dragStart"));
        Vector2 edge = new Vector2(canvas.contentRect.xMax - 2, canvas.contentRect.center.y);
        Move(edge);
        Vector2 pan = canvas.Pan;
        Set(canvas, "edgePanLastTime", 10d);
        canvas.TickEdgePan(10.05d);
        Check(canvas.Pan.x < pan.x, "held edge pans without additional mouse movement");
        Check(((Vector2)Call(canvas, "ScreenToWorld", Field(canvas, "dragStart")) - initial).sqrMagnitude < .0001f, "edge pan retains world marquee origin");
        Check(room.boxes.Count == before && changes == 3, "edge pan does not mutate document or enqueue saves");
        canvas.CancelPointerInteraction();
        pan = canvas.Pan; canvas.TickEdgePan(11);
        Check(canvas.Pan == pan && room.boxes.Count == before, "cancel stops edge pan and discards unfinished paint");
        canvas.RestoreView(new Vector2(10, 10), 1);
        Down(At(10, 10)); Move(At(11, 11)); Up(At(11, 11));
        Check(room.boxes.Count == before + 1 && changes == 4, "rectangle painting remains a single transaction");

        var library = ObjectArtworkLibrary.Load();
        Check(library != null && library.FirstFrame(MechanismArtworkKind.MovingPlatform) != null, "platform uses source sprite rather than square prefab thumbnail");
        var vertical = new PlanningBox(PlanningDetailType.Prop, new RectInt(20, 1, 1, 5)) { propEntryId = PlanningDoorUtility.MovingPlatformEntryId, isMerged = true };
        room.boxes.Add(vertical);
        canvas.RefreshPreviewImages(); Call(canvas, "UpdateDetailPreviewImages");
        var images = (Dictionary<string, Image>)Field(canvas, "detailPreviewImages");
        var parts = images.Where(pair => pair.Key.StartsWith(vertical.id + ":platform:")).Select(pair => pair.Value).ToArray();
        Check(parts.Length > 0, "vertical merged platform has actual sprite previews");
        float grid = (float)canvas.GetType().GetProperty("GridSize", Hidden).GetValue(canvas);
        Check(Mathf.Abs(parts.Sum(image => image.style.width.value.value) - 5 * grid) < .01f, "vertical preview covers five cells along rotated long axis");
        float nativeHeight = library.FirstFrame(MechanismArtworkKind.MovingPlatform).rect.height / GridArtworkMetrics.PixelsPerCell * grid;
        Check(parts.All(image => Mathf.Abs(image.style.rotate.value.angle.value - 90f) < .01f && Mathf.Abs(image.style.height.value.value - nativeHeight) < .01f), "vertical preview rotates ninety degrees without flattening");

        // The standalone audit does not open the placement panel that normally
        // initializes LevelEditorState.Palette. Load the real shared asset explicitly.
        var sourcePalette=AssetDatabase.LoadAssetAtPath<Project.LevelEditor.LevelEditorPalette>(
            "Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
        Check(sourcePalette != null,"shared prop palette exists for artwork integration");
        canvas.SetPalette(sourcePalette);
        var baseEntry=sourcePalette.PropEntries.FirstOrDefault(e=>e?.Prefab!=null && e.Prefab.GetComponent<Project.BlockFeatures.MechanismBaseFeature>()!=null);
        Check(baseEntry!=null,"mechanism base has a palette entry");
        var baseBox=new PlanningBox(PlanningDetailType.Prop,new RectInt(12,8,1,1)) { propEntryId=baseEntry.EntryId,propEntryName=baseEntry.DisplayName };
        room.boxes.Add(baseBox); Call(canvas,"InvalidatePreviewData"); Call(canvas,"UpdateDetailPreviewImages");
        Image baseImage=images.First(pair=>pair.Key==baseBox.id+":art").Value;
        Check(baseImage.image==library.FirstFrame(MechanismArtworkKind.MechanismBase).texture && Mathf.Abs(baseImage.style.width.value.value-grid)<.01f,
            "canvas uses native base artwork instead of a 3D prefab thumbnail");
        var waterEntry=sourcePalette.PropEntries.FirstOrDefault(e=>e?.Prefab!=null && e.Prefab.GetComponent<Project.BlockFeatures.WaterVisualFeature>()!=null);
        Check(waterEntry!=null,"water has a palette entry");
        var waterBox=new PlanningBox(PlanningDetailType.Prop,new RectInt(14,8,2,3)) { propEntryId=waterEntry.EntryId,propEntryName=waterEntry.DisplayName,isMerged=true };
        room.boxes.Add(waterBox); Call(canvas,"InvalidatePreviewData"); Call(canvas,"UpdateDetailPreviewImages");
        var waterImages=images.Where(pair=>pair.Key.StartsWith(waterBox.id+":art:")).Select(pair=>pair.Value).ToArray();
        Check(waterImages.Length==6,"merged water repeats six native cell tiles rather than stretching one picture");
        for(int kind=0;kind<3;kind++)
        {
            Check(ObjectArtworkPlanningPreview.TryGetWaterTile(kind,out var waterArt) && waterImages.Any(i=>i.image==waterArt.Texture && i.uv==waterArt.UV),
                "water preview includes TA surface/middle/bottom kind "+kind);
        }
        ModeAndTransferChecks();
    }

    private static void ModeAndTransferChecks()
    {
        var editor = ScriptableObject.CreateInstance<PlanningEditorWindow>();
        string preference = "EditorInteractionAudit." + Guid.NewGuid().ToString("N");
        try
        {
            Set(editor, "canvas", canvas); Set(editor, "document", document); Set(editor, "scenePreferenceScope", preference);
            var buttons = (Dictionary<PlanningCanvasMode, Button>)Field(editor, "modeButtons");
            foreach (PlanningCanvasMode mode in Enum.GetValues(typeof(PlanningCanvasMode))) buttons[mode] = new Button();
            foreach (PlanningCanvasMode mode in Enum.GetValues(typeof(PlanningCanvasMode)))
            {
                Call(editor, "SetMode", mode);
                Check(canvas.Mode == mode, "canvas changes to " + mode);
                foreach (var pair in buttons) Check(pair.Value.style.borderBottomWidth.value == (pair.Key == mode ? 3f : 0f), "active mode visual " + mode + "/" + pair.Key);
            }
            var overlay = (VisualElement)Call(editor, "BuildCollaborationOverlay");
            editor.rootVisualElement.Add(overlay);
            Call(editor, "ShowCollaborationOverlay", true);
            Check(overlay.style.display.value == DisplayStyle.Flex && ((VisualElement)Field(editor, "fragmentImportSection")).style.display.value == DisplayStyle.Flex, "import panel is directly visible without foldouts");
            Call(editor, "ShowCollaborationOverlay", false);
            Check(((VisualElement)Field(editor, "fragmentExportSection")).style.display.value == DisplayStyle.Flex, "export section is directly visible");
            Check(overlay.Query<Button>().ToList().Any(button => button.text.Contains("框选区域")), "area export has named visible action");
            Call(editor, "BeginFragmentAreaExport");
            Check(overlay.style.display.value == DisplayStyle.None && canvas.MapTool == PlanningMapTool.ExportArea, "area export closes obstruction and arms marquee");
        }
        finally
        {
            Set(editor, "canvas", null); Set(editor, "document", null);
            UnityEngine.Object.DestroyImmediate(editor);
            EditorPrefs.DeleteKey(preference + ".View.Mode");
        }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= WaitForLayout;
        canvas?.CancelPointerInteraction();
        LevelEditorState.Tool = previousTool;
        if (window != null) window.Close();
        string result = error == null ? "PASS " + checks : "FAIL " + error;
        File.WriteAllText("Library/EditorInteractionResult.txt", result);
        if (error == null) Debug.Log("EDITOR INTERACTION: " + result);
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class EditorInteractionAuditWindow : EditorWindow { }
