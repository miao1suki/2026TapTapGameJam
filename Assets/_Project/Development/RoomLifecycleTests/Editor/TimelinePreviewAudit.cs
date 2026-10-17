using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class TimelinePreviewAudit
{
    private static PlanningCanvas canvas;
    private static EditorInteractionAuditWindow window;
    private static double started;
    private static int checks;
    private static PlanningBox triggerBox;
    private static LevelEditorPalette palette;
    private static GameObject player;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object owner, string name) => owner.GetType().GetField(name, Hidden).GetValue(owner);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("TIMELINE PREVIEW: " + label);
        checks++; Debug.Log("TIMELINE PREVIEW PASS: " + label);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0) throw new InvalidOperationException("Only use the silent isolated probe.");
        started = EditorApplication.timeSinceStartup;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        player = new GameObject("Preview-only player"); player.transform.position = new Vector3(5.5f,-5.5f,0);
        LevelEditorState.Player = player;
        palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
        var entry = palette.PropEntries.First(e => e.Prefab != null && e.Prefab.GetComponent<ContactTimelineTrigger>() != null);
        var document = new PlanningDocument { name = "Timeline preview audit", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("test");
        foreach (var cell in new RectInt(0, 0, 30, 30).allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        triggerBox = new PlanningBox(PlanningDetailType.Prop, new RectInt(5, 5, 1, 1)) { propEntryId = entry.EntryId, propEntryName = entry.DisplayName };
        room.boxes.Add(triggerBox); room.boxes.Add(new PlanningBox(PlanningDetailType.Solid, new RectInt(5, 5, 1, 1)));
        document.rooms.Add(room);
        canvas = new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(document); canvas.SetMode(PlanningCanvasMode.Detail);
        canvas.SetDetailProp(entry.EntryId, entry.DisplayName); LevelEditorState.Tool = LevelEditorTool.Paint;
        window = ScriptableObject.CreateInstance<EditorInteractionAuditWindow>(); window.position = new Rect(70, 70, 1100, 780);
        window.rootVisualElement.Add(canvas); window.Show(); EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 40) { Finish(new Exception("Layout timed out")); return; }
        if (canvas.panel == null || canvas.contentRect.width < 500) return;
        EditorApplication.update -= Tick;
        try
        {
            canvas.RestoreView(new Vector2(10, 10), 1);
            var entry = palette.PropEntries.First(e => e.EntryId == triggerBox.propEntryId);
            Texture2D texture = ContactTimelineEditorPreview.Texture;
            Check(texture != null && Mathf.Max(texture.width, texture.height) <= 512, "new photo imported at bounded editor-marker size");
            var importer = (TextureImporter)AssetImporter.GetAtPath(ContactTimelineEditorPreview.TexturePath);
            Check(importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled, "single image without blank multi-sprite slicing or mipmaps");
            Check(ObjectArtworkPlanningPreview.TryGet(entry.Prefab, out var art) && art.EditorOverlay && art.Texture == texture && !art.Hidden, "shared palette/canvas uses supplied photo instead of 3D cube");
            Check(Mathf.Max(art.Cells.x, art.Cells.y) <= 1.001f && Mathf.Abs(art.Cells.x / art.Cells.y - (float)texture.width / texture.height) < .001f, "photo fits cell without stretching its aspect ratio");
            Call(canvas, "UpdateDetailPreviewImages");
            var images = (Dictionary<string, Image>)Get(canvas, "detailPreviewImages");
            var image = images[triggerBox.id + ":art"];
            var overlay = (VisualElement)Get(canvas, "triggerPreviewLayer");
            var ghost = (Image)Get(canvas, "triggerPlacementPreview");
            Check(image.parent == overlay && image.image == texture && image.tintColor.a < 1, "placed trigger is translucent in independent overlay");
            Check(((VisualElement)Get(canvas,"playerOverlay")).style.display.value == DisplayStyle.Flex, "real scene-player overlay is present for ordering test");
            Check(canvas.hierarchy[canvas.hierarchy.childCount-1] == overlay && overlay.pickingMode == PickingMode.Ignore && image.pickingMode == PickingMode.Ignore, "trigger overlay is topmost without intercepting editor input");
            Rect rect = (Rect)Call(canvas, "CellRect", 5, 5, 1, 1);
            Call(canvas, "UpdateHover", rect.center);
            Check(ghost.style.display.value == DisplayStyle.Flex && ghost.image == texture && ghost.style.opacity.value == .5f, "painting shows translucent supplied-image ghost");
            Check(overlay.hierarchy[overlay.hierarchy.childCount-1] == ghost && ghost.pickingMode == PickingMode.Ignore, "placement image sits above existing markers");
            int count = images.Count;
            for (int i=0;i<100;i++) { canvas.RestoreView(new Vector2(10+i*.001f,10), 1); Call(canvas,"UpdateDetailPreviewImages"); }
            Check(images.Count == count && images[triggerBox.id+":art"] == image && Get(canvas,"triggerPlacementPreview") == ghost, "repeated pan refresh reuses image elements");
            LevelEditorState.Tool = LevelEditorTool.Select; Call(canvas,"UpdateTriggerPlacementPreview");
            Check(ghost.style.display.value == DisplayStyle.None && image.parent == overlay, "selection hides placement ghost but retains placed marker");
            canvas.SetMode(PlanningCanvasMode.World); Call(canvas,"UpdateDetailPreviewImages");
            Check(!images.ContainsKey(triggerBox.id+":art") || image.style.display.value == DisplayStyle.None || image.parent == null, "world view has no leftover detail marker");
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; if (window != null) window.Close();
        LevelEditorState.Player = null; if (player != null) UnityEngine.Object.DestroyImmediate(player);
        string result = error == null ? "PASS " + checks + " attached preview assertions" : "FAIL " + error;
        File.WriteAllText("Library/TimelinePreview.txt",result);
        if (error == null) Debug.Log(result); else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
