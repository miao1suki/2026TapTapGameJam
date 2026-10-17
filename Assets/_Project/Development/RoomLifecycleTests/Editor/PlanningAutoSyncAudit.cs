using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Opens the real attached window and lets its update loop run. Isolated project only.
public static class PlanningAutoSyncAudit
{
    [Serializable] private sealed class Record { public string id; public Vector2Int cell; }
    [Serializable] private sealed class Memory
    {
        public string documentHash;
        public List<Record> records = new List<Record>();
    }
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static PlanningEditorWindow window;
    private static PlanningDocument expected;
    private static string originalScene, originalSave;
    private static double next, deadline;
    private static int step, checks;
    private static string removedId;
    private static object Field(string name) => typeof(PlanningEditorWindow).GetField(name, Hidden).GetValue(window);
    private static void Set(string name, object value) => typeof(PlanningEditorWindow).GetField(name, Hidden).SetValue(window, value);
    private static string Key(string name) => (string)typeof(PlanningEditorWindow).GetProperty(name, Hidden).GetValue(window);
    private static void Call(string name) => typeof(PlanningEditorWindow).GetMethod(name, Hidden, null, Type.EmptyTypes, null).Invoke(window, null);
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("AUTOSYNC: " + label);
        checks++; Debug.Log("AUTOSYNC PASS: " + label);
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("仅在隔离验证工程执行。");
        originalScene = File.ReadAllText(MRoom2MapImport.ScenePath);
        originalSave = File.ReadAllText(MRoom2MapImport.SavePath);
        expected = PlanningDocument.FromPersistentJson(originalSave);
        EditorSceneManager.OpenScene(MRoom2MapImport.ScenePath);
        foreach (var old in Resources.FindObjectsOfTypeAll<PlanningEditorWindow>()) Object.DestroyImmediate(old);
        window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
        window.position = new Rect(40, 40, 1700, 950);
        window.Show();
        next = EditorApplication.timeSinceStartup + 1;
        deadline = next + 70;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("AUTOSYNC window test timed out");
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            switch (step++)
            {
                case 0:
                    Check(window.rootVisualElement.panel != null, "real UI Toolkit window attached");
                    AssertMap("before stale session");
                    string memoryKey = Key("BindingMemoryKey"), autoKey = Key("AutoSyncPreferenceKey");
                    // An actual previous import reproduces the original 188 -> 104
                    // corruption. A synthetic revision keeps this audit repeatable
                    // on clones without that local forensic backup.
                    string previousPath = "Library/PlanningAutoSyncPrevious.json";
                    var previous = PlanningDocument.FromPersistentJson(File.Exists(previousPath)
                        ? File.ReadAllText(previousPath) : originalSave);
                    if (!File.Exists(previousPath))
                    {
                        previous.name += "_previous_import";
                        foreach (var box in previous.rooms.SelectMany(r => r.boxes)) box.x++;
                    }
                    var memory = new Memory { documentHash = Hash128.Compute(previous.ToPersistentJson()).ToString() };
                    foreach (var box in previous.rooms.SelectMany(r => r.boxes))
                        memory.records.Add(new Record { id = box.id, cell = new Vector2Int(box.x, box.y) });
                    Set("autoSync", false);
                    window.Close();
                    SessionState.SetString(memoryKey, JsonUtility.ToJson(memory));
                    EditorPrefs.SetBool(autoKey, true);
                    window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
                    window.position = new Rect(40, 40, 1700, 950);
                    window.Show();
                    break;
                case 1:
                    AssertMap("opening new import with old session bindings");
                    Call("ApplyMapToScene");
                    break;
                case 2:
                    AssertMap("after real autosync ticks");
                    Call("RememberBindings");
                    window.Close();
                    window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
                    window.position = new Rect(40, 40, 1700, 950); window.Show();
                    break;
                case 3:
                    AssertMap("after close/reopen");
                    Call("ApplyMapToScene");
                    break;
                case 4:
                    AssertMap("after second apply and delayed updates");
                    // An old cache with no version must be discarded too.
                    SessionState.SetString(Key("BindingMemoryKey"), "{\"records\":[{\"id\":\"" + expected.rooms[0].boxes[0].id + "\",\"cell\":{\"x\":999,\"y\":999}}]}");
                    Call("RestoreAutoSync");
                    AssertMap("legacy unversioned memory");
                    // Replacing geometry from another generator must not look like
                    // deleting every old Unity instance.
                    PlanningSceneBuilder.SuppressSceneUndo = true;
                    Check(PlanningSceneBuilder.TryBuildDocument(expected, out var rebuilt), "external geometry rebuild: " + rebuilt);
                    PlanningSceneBuilder.SuppressSceneUndo = false;
                    break;
                case 5:
                    AssertMap("replacement instances are reacquired, not removed");
                    removedId = "mroom2_submerged_platform_water";
                    Object.DestroyImmediate(GameObject.Find("__PlanningMapGenerated").GetComponentsInChildren<LevelEditorPlacedBlock>(true)
                        .Single(b => b.PlanningBoxId == removedId).gameObject);
                    break;
                case 6:
                    AssertOneDeletion("actual manual deletion in open window");
                    Call("UndoPlanning");
                    break;
                case 7:
                    AssertMap("planning undo restores real manual deletion");
                    Call("ApplyMapToScene");
                    Call("RememberBindings");
                    window.Close();
                    removedId = expected.rooms[0].boxes.First(b => b.label == "墙" && b.isMerged).id;
                    Object.DestroyImmediate(GameObject.Find("__PlanningMapGenerated").GetComponentsInChildren<LevelEditorPlacedBlock>(true)
                        .Single(b => b.PlanningBoxId == removedId).gameObject);
                    window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
                    window.position = new Rect(40, 40, 1700, 950); window.Show();
                    break;
                case 8:
                    AssertOneDeletion("same-revision deletion while window closed");
                    Call("UndoPlanning");
                    break;
                case 9:
                    AssertMap("undo restores closed-window deletion");
                    Finish(null);
                    break;
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void AssertOneDeletion(string stage)
    {
        var actual = (PlanningDocument)Field("document");
        var boxes = actual.rooms.SelectMany(r => r.boxes).ToArray();
        Check(boxes.Length == 187 && boxes.All(b => b.id != removedId), stage + ": exactly the deleted object removed");
        var root = GameObject.Find("__PlanningMapGenerated");
        var ids = root.GetComponentsInChildren<LevelEditorPlacedBlock>(true).Select(b => b.PlanningBoxId).ToHashSet();
        Check(boxes.All(b => ids.Contains(b.id)), stage + ": remaining geometry intact");
        Check(ids.Contains("mroom2_room_d777v35wuy_4"), stage + ": foreground platform over water survives");
    }

    private static void AssertMap(string stage)
    {
        var actual = (PlanningDocument)Field("document");
        Check(actual.rooms.SelectMany(r => r.boxes).Count() == expected.rooms.SelectMany(r => r.boxes).Count(),
            stage + ": all 188 authoring objects retained (actual " + actual.rooms.SelectMany(r => r.boxes).Count() + ")");
        foreach (var room in expected.rooms)
        foreach (var box in room.boxes)
        {
            var found = actual.FindRoom(room.id).boxes.SingleOrDefault(b => b.id == box.id);
            Check(found != null && JsonUtility.ToJson(found) == JsonUtility.ToJson(box), stage + ": stable object " + box.id);
        }
        var root = GameObject.Find("__PlanningMapGenerated");
        Check(root != null, stage + ": scene root exists");
        var sceneIds = root.GetComponentsInChildren<LevelEditorPlacedBlock>(true).Select(b => b.PlanningBoxId).ToHashSet();
        Check(expected.rooms.SelectMany(r => r.boxes).All(b => sceneIds.Contains(b.id)), stage + ": scene contains every source object");
        Check((bool)Field("autoSync") || step == 1, stage + ": automatic sync remains enabled");
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        if (window != null) { Set("autoSync", false); window.Close(); }
        // Test failures may have autosaved; never deliver their destructive state.
        File.WriteAllText(MRoom2MapImport.ScenePath, originalScene);
        File.WriteAllText(MRoom2MapImport.SavePath, originalSave);
        File.WriteAllText("Library/PlanningAutoSyncAudit.txt", error == null ? "PASS " + checks : "FAIL " + error);
        if (error != null) Debug.LogException(error);
        else Debug.Log("AUTOSYNC VERIFIED " + checks);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
