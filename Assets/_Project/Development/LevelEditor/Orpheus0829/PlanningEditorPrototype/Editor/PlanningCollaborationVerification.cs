using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanningEditorPrototype
{
    internal static class PlanningCollaborationVerification
    {
        private static int checks;
        private static void Check(bool value, string name)
        { checks++; if (!value) throw new Exception(name); }

        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("仅在隔离项目运行验证。");
            try
            {
                var doc = new PlanningDocument { name = "Collaboration" };
                var room = new PlanningRoom("Room");
                for (int y = 0; y < 3; y++) for (int x = 0; x < 5; x++) room.cells.Add(new PlanningCell(x, y));
                doc.rooms.Add(room);
                var door = new PlanningBox { id = "door", x = 0, y = 0, width = 1, height = 2, singleInstance = true };
                var button = new PlanningBox { id = "button", x = 2, y = 1, doorOwnerId = door.id };
                var source = new PlanningBox { id = "source", x = 2, y = 2 };
                var platform = new PlanningBox { id = "platform", x = 3, y = 2, mechanismBaseId = source.id, crankOwnerId = source.id };
                room.boxes.AddRange(new[] { door, button, source, platform });
                doc.playerStartRoomId = room.id; doc.playerStartLocal = new Vector2(.5f, .5f);
                string before = doc.ToPersistentJson();
                var fragment = PlanningFragmentUtility.Extract(doc, new[] { room.id }, null, out int detached);
                Check(fragment.rooms.Count == 1 && detached == 0 && doc.ToPersistentJson() == before, "owner export is non-destructive");
                var imported = PlanningFragmentUtility.Import(doc, fragment, new Vector2Int(10, -2), false, out detached);
                Check(doc.ToPersistentJson() == before && imported.rooms.Count == 2, "import is atomic and preserves source");
                var added = imported.rooms[1]; var boxes = added.boxes;
                Check(added.cells[0].x == 10 && added.cells[0].y == -2, "import destination");
                Check(boxes.Select(b => b.id).Distinct().Count() == 4 && !boxes.Any(b => room.boxes.Any(old => old.id == b.id)), "new IDs");
                Check(boxes[1].doorOwnerId == boxes[0].id && boxes[3].mechanismBaseId == boxes[2].id && boxes[3].crankOwnerId == boxes[2].id, "all internal links remap");
                Check(imported.playerStartRoomId == room.id && added.name != room.name, "existing spawn and unique names");
                var repeated = PlanningFragmentUtility.Import(imported, fragment, new Vector2Int(20, 0), true, out _);
                Check(repeated.rooms.Select(r => r.id).Distinct().Count() == 3 && repeated.playerStartRoomId == repeated.rooms[2].id, "repeated import with optional spawn");
                bool rejected = false;
                try { PlanningFragmentUtility.Import(doc, fragment, Vector2Int.zero, false, out _); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && doc.ToPersistentJson() == before, "overlap rejection does not change map");
                var crop = PlanningFragmentUtility.Extract(doc, null, new RectInt(2, 0, 3, 3), out detached);
                Check(crop.rooms[0].boxes.Count == 2 && detached == 1 && crop.playerStartRoomId == null, "partial export drops orphan button and outside spawn");
                Check(crop.rooms[0].boxes[0].x == 0 && crop.rooms[0].cells[0].x == 2, "cropped local coordinates reanchor");
                rejected = false;
                try { PlanningFragmentUtility.Extract(doc, null, new RectInt(0, 1, 1, 1), out _); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "cannot export half an object");
                room.boxes.Add(new PlanningBox { id = "painted", x = 0, y = 0, width = 5, height = 1 });
                var clippedTiles = PlanningFragmentUtility.Extract(doc, null, new RectInt(3, 0, 2, 1), out _);
                Check(clippedTiles.rooms[0].boxes[0].width == 2 && clippedTiles.rooms[0].boxes[0].x == 0, "ordinary tiled brush rectangles clip at export boundary");
                room.boxes.RemoveAll(b => b.id == "painted");
                var merged = new PlanningBox { id = "merged", x = 3, y = 0, width = 2, height = 1, isMerged = true };
                merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox { id = "part1", x = 3, y = 0 }));
                merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox { id = "part2", x = 4, y = 0 }));
                room.boxes.Add(merged);
                var mergedCrop = PlanningFragmentUtility.Extract(doc, null, new RectInt(3, 0, 2, 1), out _);
                var mergedImport = PlanningFragmentUtility.Import(new PlanningDocument(), mergedCrop, new Vector2Int(-7, 4), false, out _);
                var m = mergedImport.rooms[0].boxes[0];
                Check(m.isMerged && m.x == 0 && PlanningBox.FromPersistentJson(m.mergeParts[0]).x == 0, "merged source coordinates survive crop and import");
                Check(PlanningBox.FromPersistentJson(m.mergeParts[0]).id != "part1", "unmerge IDs remap");
                var patch = new PlanningBox { id = "patch", x = 1, y = 1 };
                doc.assemblyPatches.Add(patch);
                Check(PlanningWorldUtility.TryMove(doc, new HashSet<string> { room.id }, new HashSet<string>(), new Vector2Int(6, 5)), "move whole owner");
                Check(room.boxes[0].x == 0 && patch.x == 7 && doc.playerStartLocal == new Vector2(.5f, .5f), "move includes patches, contents and spawn");
                string temporary = Path.GetFullPath("Library/PlanningCollaboration/map.json");
                PlanningSaveStore.Save(temporary, doc);
                Check(PlanningSaveStore.Read(temporary).ToPersistentJson() == doc.ToPersistentJson(), "disk round trip");
                PlanningSaveStore.Save(temporary, doc);
                Check(!File.Exists(temporary + ".writing"), "atomic writer leaves no temporary data");
                VerifyScene();
                File.WriteAllText("Library/PlanningCollaborationVerification.txt", "PASS " + checks);
                Debug.Log("协作地图验证通过：" + checks);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/PlanningCollaborationVerification.txt", "FAIL " + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private static void VerifyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            new GameObject("AudioListener").AddComponent<AudioListener>();
            var palette = LevelEditorPaletteService.GetOrCreate();
            var water = palette.PropEntries.First(p => p.Prefab != null && p.Prefab.GetComponent<WaterVisualFeature>() != null);
            var solid = palette.Entries.First();
            var doc = new PlanningDocument { name = "WaterOverlap" };
            var room = new PlanningRoom("Overlap");
            room.cells.Add(new PlanningCell(0, 0)); room.cells.Add(new PlanningCell(1, 0)); doc.rooms.Add(room);
            var waterBox = new PlanningBox { id = "water", type = PlanningDetailType.Prop, propEntryId = water.EntryId, propEntryName = water.DisplayName, x = 0, y = 0, width = 2, height = 1, isMerged = true };
            var item = new PlanningBox { id = "item", type = PlanningDetailType.Solid, paletteEntryId = solid.EntryId, paletteEntryName = solid.DisplayName, x = 0, y = 0 };
            room.boxes.AddRange(new[] { item, waterBox });
            Check(PlanningWaterLayerUtility.IsWater(waterBox, palette) && !PlanningWaterLayerUtility.IsWater(item, palette), "water classification from real prefab");
            PlanningSceneBuilder.SuppressSceneUndo = true;
            Check(PlanningSceneBuilder.TryApplyDocument(doc, out var message), "scene overlap apply: " + message);
            var root = GameObject.Find("__PlanningMapGenerated");
            var blocks = root.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            Check(blocks.Length == 2 && blocks.Any(b => b.PlanningBoxId == item.id) && blocks.Any(b => b.PlanningBoxId == waterBox.id), "scene retains merged water and item in same cell");
            var old = blocks.ToDictionary(b => b.PlanningBoxId, b => b.GetInstanceID());
            Check(PlanningSceneBuilder.TryApplyDocument(doc, out _), "repeat sync");
            blocks = root.GetComponentsInChildren<LevelEditorPlacedBlock>(true);
            Check(blocks.Length == 2 && blocks.All(b => old[b.PlanningBoxId] == b.GetInstanceID()), "sync preserves both instances, no duplicates");
            var canvas = new PlanningCanvas(); canvas.SetDocument(doc); canvas.SetPalette(palette); canvas.SetMode(PlanningCanvasMode.Assembly);
            var images = (Dictionary<string, UnityEngine.UIElements.Image>)typeof(PlanningCanvas)
                .GetField("detailPreviewImages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(canvas);
            Check(canvas.IndexOf(images.First(p => p.Key.StartsWith("water:")).Value) <
                canvas.IndexOf(images.First(p => p.Key.StartsWith("item:")).Value), "preview draws water below foreground regardless of authoring order");
            var erase = typeof(PlanningCanvas).GetMethod("EraseAssemblyBox", BindingFlags.Instance | BindingFlags.NonPublic);
            erase.Invoke(canvas, new object[] { Vector2Int.zero });
            Check(room.boxes.Count == 1 && room.boxes[0].id == waterBox.id, "default erase removes foreground, not water");
            room.boxes.Insert(0, item);
            canvas.SetWaterEditing(true);
            erase.Invoke(canvas, new object[] { Vector2Int.zero });
            Check(room.boxes.Count == 1 && room.boxes[0] == item, "water-only erase preserves item");
            Check(PlanningSceneBuilder.TryApplyDocument(doc, out _), "remove water only");
            Check(root.GetComponentsInChildren<LevelEditorPlacedBlock>(true).Single().PlanningBoxId == item.id, "scene deletion preserves foreground");
            var position = root.GetComponentsInChildren<LevelEditorPlacedBlock>(true)[0].transform.position;
            PlanningWorldUtility.TryMove(doc, new HashSet<string> { room.id }, new HashSet<string>(), new Vector2Int(4, 3));
            Check(PlanningSceneBuilder.TryApplyDocument(doc, out _), "apply room move");
            Check(root.GetComponentsInChildren<LevelEditorPlacedBlock>(true)[0].transform.position == position + new Vector3(4, -3, 0) * LevelEditorState.CellSize, "scene geometry follows room move");
            string path = "Assets/CollaborationSaveProbe.unity";
            EditorSceneManager.SaveScene(scene, path);
            Check(File.Exists(PlanningSharedSaveUtility.SceneSavePath(path)), "saving scene automatically exports editable map");
            Check(PlanningSaveStore.Read(PlanningSharedSaveUtility.SceneSavePath(path)).rooms[0].cells[0].x == 4, "paired save contains latest move");
            var external = PlanningDocument.FromPersistentJson(doc.ToPersistentJson()); external.name = "Remote update";
            PlanningSaveStore.Save(PlanningSharedSaveUtility.SceneSavePath(path), external);
            bool conflict = false;
            try { PlanningSharedSaveUtility.SaveSceneCopy(scene, doc); } catch (IOException) { conflict = true; }
            Check(conflict && PlanningSaveStore.Read(PlanningSharedSaveUtility.SceneSavePath(path)).name == "Remote update", "stale editor cannot overwrite external save");
            Check(File.Exists(Path.GetFullPath("Library/PlanningConflicts/" + AssetDatabase.AssetPathToGUID(path) + "_local.planning.json")), "conflict preserves local map backup");
            PlanningSharedSaveUtility.ObserveSceneCopy(scene);
            var player = new GameObject("Author Player"); player.transform.position = new Vector3(4.5f, -3.5f, 0) * LevelEditorState.CellSize;
            LevelEditorState.Player = player;
            canvas.SetWaterEditing(false); canvas.SetDocument(doc); canvas.SelectRoom(room.id);
            var playerPosition = player.transform.position;
            Check(canvas.MoveWorldSelection(Vector2Int.right), "move selection with actual scene player");
            Check(player.transform.position == playerPosition + Vector3.right * LevelEditorState.CellSize && doc.playerStartRoomId == room.id, "actual player follows and spawn persists");
            LevelEditorState.Player = null;
            PlanningSceneBuilder.SuppressSceneUndo = false;
        }

        public static void RunWindow()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("仅在隔离项目运行验证。");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/CollaborationWindowProbe.unity");
            var window = EditorWindow.GetWindow<PlanningEditorWindow>();
            window.position = new Rect(80, 80, 1500, 900);
            window.Show();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var type = typeof(PlanningEditorWindow);
                    void Set(string name, object value) => type.GetField(name, flags).SetValue(window, value);
                    object Get(string name) => type.GetField(name, flags).GetValue(window);
                    void Invoke(string name) => type.GetMethod(name, flags).Invoke(window, null);
                    var canvas = (PlanningCanvas)Get("canvas");
                    Check(canvas != null && window.rootVisualElement.childCount > 0, "UI Toolkit window opens");
                    var doc = new PlanningDocument { name = "Window Collaboration" };
                    var room = new PlanningRoom("Small"); room.cells.Add(new PlanningCell(0, 0));
                    room.boxes.Add(new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 1, 1)));
                    doc.rooms.Add(room);
                    Set("document", doc); canvas.SetDocument(doc); Set("autoSync", false);
                    Invoke("ResetPlanningHistory");
                    Set("fragmentSource", doc); Set("fragmentAnchor", new Vector2Int(5, 0));
                    Invoke("RefreshImportPreview");
                    Check(((UnityEngine.UIElements.Button)Get("fragmentApply")).enabledSelf, "valid import button enabled");
                    Invoke("ApplyFragment");
                    Check(((PlanningDocument)Get("document")).rooms.Count == 2, "window import appends");
                    Invoke("UndoPlanning");
                    Check(((PlanningDocument)Get("document")).rooms.Count == 1, "custom editor undo import");
                    Invoke("RedoPlanning");
                    Check(((PlanningDocument)Get("document")).rooms.Count == 2, "custom editor redo import");
                    var paired = PlanningSharedSaveUtility.SceneSavePath(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                    Check(File.Exists(paired) && PlanningSaveStore.Read(paired).rooms.Count == 2, "workspace changes persist paired save");
                    RectInt exported = default;
                    canvas.ExportAreaSelected += rect => exported = rect;
                    canvas.BeginAreaExport();
                    var canvasType = typeof(PlanningCanvas);
                    Vector2 screen = (Vector2)canvasType.GetMethod("WorldToScreen", flags).Invoke(canvas, new object[] { new Vector2(2, 3) });
                    canvasType.GetField("dragStart", flags).SetValue(canvas, screen);
                    canvasType.GetField("dragCurrent", flags).SetValue(canvas, screen);
                    canvasType.GetMethod("CompleteWorldDrag", flags).Invoke(canvas, null);
                    Check(exported == new RectInt(2, 3, 1, 1), "area export gesture invokes rectangle event");
                    window.Close();
                    File.WriteAllText("Library/PlanningCollaborationWindowVerification.txt", "PASS " + checks);
                    EditorApplication.Exit(0);
                }
                catch (Exception error)
                {
                    File.WriteAllText("Library/PlanningCollaborationWindowVerification.txt", "FAIL " + error);
                    Debug.LogException(error); window.Close(); EditorApplication.Exit(1);
                }
            };
        }
    }
}
