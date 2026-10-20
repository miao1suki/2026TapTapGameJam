using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.Editor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class IncomingEditorProgressAudit
{
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("Use isolated FlowRetirementProbe.");
        int checks = 0, exit = 0;
        var previous = LevelEditorState.Tool;
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
        try
        {
            var canvas = new PlanningCanvas();
            var doc = new PlanningDocument();
            var room = new PlanningRoom("Temporary tool audit"); room.cells.Add(new PlanningCell(0, 0)); doc.rooms.Add(room);
            canvas.SetDocument(doc); canvas.SelectRoom(room.id); canvas.SetMode(PlanningCanvasMode.Assembly);
            string original = JsonUtility.ToJson(doc);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            object Call(string method, params object[] args) => typeof(PlanningCanvas).GetMethod(method, flags).Invoke(canvas, args);
            LevelEditorState.Tool = LevelEditorTool.Parameters;
            Check(Call("GetActiveDetailTool").ToString() == "Parameters", "original parameter tool");
            Check((bool)Call("HandleTemporaryToolKey", KeyCode.LeftShift, true, false, true), "Shift key accepted");
            Check(Call("GetActiveDetailTool").ToString() == "Box", "Shift temporarily paints");
            canvas.SetDetailProp("existing-prop", "Previously selected prop");
            var box = new PlanningBox(PlanningDetailType.Prop, new RectInt(0, 0, 1, 1));
            Call("ApplyDetailSelection", box);
            Check(string.IsNullOrEmpty(box.propEntryId) && box.paletteEntryName == "白方块", "Shift chooses white, not previous prop");
            Call("HandleTemporaryToolKey", KeyCode.LeftAlt, true, true, true);
            Check(Call("GetActiveDetailTool").ToString() == "Erase", "Alt takes precedence over Shift");
            Call("HandleTemporaryToolKey", KeyCode.LeftAlt, false, false, true);
            Check(Call("GetActiveDetailTool").ToString() == "Box", "release Alt retains held Shift");
            Call("HandleTemporaryToolKey", KeyCode.LeftShift, false, false, false);
            Check(Call("GetActiveDetailTool").ToString() == "Parameters", "release returns original tool");
            Call("HandleTemporaryToolKey", KeyCode.RightShift, true, false, true);
            canvas.ResetTemporaryAssemblyTools();
            Check(Call("GetActiveDetailTool").ToString() == "Parameters", "lost-focus reset clears held key");
            Call("HandleTemporaryToolKey", KeyCode.LeftAlt, true, true, false);
            canvas.SetMode(PlanningCanvasMode.Detail);
            Check(Call("GetActiveDetailTool").ToString() == "Parameters", "mode switch restores tool");
            Check(!(bool)Call("HandleTemporaryToolKey", KeyCode.LeftShift, true, false, true), "Detail mode not hijacked");
            Check(JsonUtility.ToJson(doc) == original && LevelEditorState.Tool == LevelEditorTool.Parameters, "modifiers do not alter document or global tool");
            var owner = new GameObject("Read-only owner");
            UnityEngine.Object.DestroyImmediate(owner);
            bool readDestroyed = false;
            var row = ProjectInspectorUtility.CreateReadOnlyRow("Status", () => { readDestroyed = true; return owner.name; }, owner: owner);
            Check(!readDestroyed && row.Query<Label>().ToList().Any(l => l.text == "对象已移除"), "destroyed Unity owner not confused with omitted owner");
            var normal = ProjectInspectorUtility.CreateReadOnlyRow("Normal", () => "Available");
            Check(normal.Query<Label>().ToList().Any(l => l.text == "Available"), "omitted owner remains usable");
            PlanningCanvas.VerifyRetainedBlockPreviews();
            File.WriteAllText("Library/IncomingEditorProgressAudit.txt", "PASS " + checks + " editor integration assertions; retained-batch regression also passed.");
        }
        catch (Exception e) { exit = 1; File.WriteAllText("Library/IncomingEditorProgressAudit.txt", e.ToString()); Debug.LogException(e); }
        finally { LevelEditorState.Tool = previous; EditorApplication.Exit(exit); }
    }
}
