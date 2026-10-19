using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Levels.ILorR;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Explicit import; never runs on import and never overwrites an authored scene.</summary>
public static class R3LevelBuilder
{
    public const string Folder = "Assets/Scenes/R3";
    public const string ScenePath = Folder + "/R3.unity";
    public const string SavePath = Folder + "/R3.planning.json";
    public const string PlayerPath = "Assets/_Project/Content/Player/Player.prefab";
    private static int checks;
    [Serializable] public sealed class Cell { public string column; public int row; }
    [Serializable] public sealed class WallRun { public int row, firstColumn, lastColumn; }
    [Serializable] public sealed class GrowingCube
    {
        public string id, column, targetColumn;
        public int row, targetRow;
        public bool pinned;
    }
    [Serializable] public sealed class Lift
    {
        public string column, crankColumn, redColumn, blueColumn, attachmentRule;
        public int row, width, height, topRow, crankRow, redRow, blueRow;
    }
    [Serializable] public sealed class Source
    {
        public bool designConfirmed;
        public int originalWallCount, wallCount;
        public List<WallRun> wallRuns;
        public List<Cell> waterCells, lavaCells, vineCells, buttons, collectibles;
        public List<GrowingCube> growingCubes;
        public Cell spawn;
        public Lift lift;
    }
    public static Source ReadSource() => JsonUtility.FromJson<Source>(File.ReadAllText(Folder + "/R3.design.json"));
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new InvalidOperationException("R3: " + message);
    }
    public static RectInt ExcelRect(string column, int row, int width = 1, int height = 1)
    {
        int col = 0; foreach (char letter in column) col = col * 26 + letter - 'A' + 1;
        return new RectInt(col - 24, row - 75, width, height);
    }
    private static List<PlanningCell> Terrain(Source source)
    {
        var cells = new List<PlanningCell>();
        foreach (var run in source.wallRuns)
            for (int col = run.firstColumn; col <= run.lastColumn; col++)
                cells.Add(new PlanningCell(col - 24, run.row - 75));
        Check(cells.Count == 551 && source.originalWallCount == 552 && source.wallCount == 551, "exact source walls; only approved CZ78 removed");
        return cells;
    }
    private static IEnumerable<Vector2Int> BoxCells(PlanningBox box)
    {
        foreach (var cell in new RectInt(box.x,box.y,box.width,box.height).allPositionsWithin) yield return cell;
    }
    public static PlanningDocument CreateDocument()
    {
        var source = ReadSource();
        Check(source.designConfirmed, "designer confirmation required");
        var doc = new PlanningDocument { name = "R3", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("R3") { id = "ilorr_r3", sourceAreaId = "excel_r3_X75_CZ101",
            sourceNotes = "关卡设计.xlsx / Sheet1；原点X75；下方R2不导入，仅CZ78开洞；轨迹和生长极限非实体。" };
        foreach (var p in new RectInt(0, 0, 81, 27).allPositionsWithin)
            if (p.y <= 22 || p.x >= 39) room.cells.Add(new PlanningCell(p.x, p.y));
        doc.rooms.Add(room);
        var palette = LevelEditorPaletteService.GetOrCreate();
        int index = 0;
        foreach (var rect in PlanningMVMapObjectImporter.Partition(Terrain(source)))
            Add(palette, room, "wall_" + index++, "实体地形", null, rect, false);
        PlanningBox Box(string id, string label, string prefab, string col, int row, int w = 1, int h = 1, bool single = true)
            => Add(palette, room, id, label, prefab, ExcelRect(col, row, w, h), single);
        void ColoredArea(string prefix, string label, string prefab, List<Cell> sourceCells)
        {
            int part = 0;
            var cells = sourceCells.Select(c => { var rect = ExcelRect(c.column,c.row); return new PlanningCell(rect.x,rect.y); }).ToList();
            foreach (var rect in PlanningMVMapObjectImporter.Partition(cells))
                Add(palette, room, prefix + part++, label, prefab, rect, false);
        }
        ColoredArea("water_", "普通水域（按原形状）", "Blue_WaterSource", source.waterCells);
        ColoredArea("lava_", "岩浆（按原形状）", "Red_Lava", source.lavaCells);
        foreach (var cell in source.vineCells)
        {
            var vine = Box("vine_" + cell.column + cell.row, "开局完整藤蔓 " + cell.column + cell.row, "Green_Ladder", cell.column, cell.row);
            Parameter(vine, typeof(ClimbableVineFeature), "maxGrowthBlocks", "Integer", "0");
        }
        foreach (var spec in source.growingCubes)
        {
            var a = ExcelRect(spec.column,spec.row); var b = ExcelRect(spec.targetColumn,spec.targetRow);
            Check(a.x == b.x || a.y == b.y, "cardinal growth " + spec.id);
            var cube = Box(spec.id, "固定绿块 " + spec.column + spec.row + " → " + spec.targetColumn + spec.targetRow, "Green_ContactCube", spec.column,spec.row);
            Parameter(cube, typeof(ContactCube), "maximumLength", "Integer", (Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)+1).ToString(CultureInfo.InvariantCulture));
            Parameter(cube, typeof(Rigidbody), "m_Constraints", "Integer", ((int)RigidbodyConstraints.FreezeAll).ToString());
            Parameter(cube, typeof(CarryableFeature), "m_Enabled", "Boolean", "0");
        }
        var s = source.lift;
        var crank = Box("crank", "随平台移动的曲柄", "Red_Crank",s.crankColumn,s.crankRow);
        crank.initialCrankPlatformCreated = true;
        var lift = Box("lift", "曲柄平台（第" + s.row + "行至第" + s.topRow + "行）", "Red_CrankLift", s.column,s.row,s.width,s.height,false);
        lift.crankOwnerId = crank.id;
        Parameter(lift, typeof(MovingPlatformFeature), "pathMode", "Enum", "3");
        Parameter(lift, typeof(MovingPlatformFeature), "upDistance", "Float", (s.row-s.topRow).ToString(CultureInfo.InvariantCulture));
        crank.platformMountId = lift.id;
        var red = Box("cube_red", "平台红块（搬起后脱离）", "Red_ContactCube",s.redColumn,s.redRow);
        var blue = Box("cube_blue", "平台蓝块（搬起后脱离）", "Blue_ContactCube",s.blueColumn,s.blueRow);
        red.platformMountId = blue.platformMountId = lift.id;
        foreach (var cell in source.collectibles)
            Box("collectible_"+cell.column+cell.row,"收集品 "+cell.column+cell.row,"Collectible",cell.column,cell.row);
        var door = Box("exit", "出口竖门 CZ78:CZ79", "Door", "CZ",78,1,2);
        int order = 0;
        foreach (var cell in source.buttons)
        {
            var button = Box("button_"+cell.column+cell.row,"门按钮 "+cell.column+cell.row,"DoorButton",cell.column,cell.row);
            button.doorOwnerId = door.id; button.doorButtonOrder = ++order;
            Parameter(button,typeof(DoorButton),"mode","Enum","0");
        }
        foreach (var box in room.boxes.Where(b => b.isMerged))
            foreach (var cell in new RectInt(box.x,box.y,box.width,box.height).allPositionsWithin)
            {
                var unit = box.Clone(); unit.mergeParts.Clear(); unit.id += "_cell_"+cell.x+"_"+cell.y;
                unit.x=cell.x; unit.y=cell.y; unit.width=unit.height=1; unit.isMerged=false;
                box.mergeParts.Add(JsonUtility.ToJson(unit));
            }
        var path = ExcelRect("CH",s.topRow,1,s.row-s.topRow+1);
        doc.regions.Add(new PlanningRegion { id="r3_note_platform_path", annotation="平台轨迹（非实体）", x=path.x,y=path.y,width=path.width,height=path.height,colorHex="#999999" });
        foreach (var spec in source.growingCubes)
        {
            var target = ExcelRect(spec.targetColumn,spec.targetRow);
            doc.regions.Add(new PlanningRegion { id="r3_note_"+spec.id, annotation=spec.column+spec.row+" 生长极限", x=target.x,y=target.y,width=1,height=1,colorHex="#A8D08D" });
        }
        var spawn = ExcelRect(source.spawn.column,source.spawn.row);
        doc.playerStartRoomId=room.id; doc.playerStartLocal=new Vector2(spawn.x+.5f,spawn.y+.5f);
        doc.Normalize(); return doc;
    }
    private static PlanningBox Add(LevelEditorPalette palette, PlanningRoom room,string id,string label,string prefab,RectInt rect,bool single)
    {
        var box = new PlanningBox(prefab == null ? PlanningDetailType.Solid : PlanningDetailType.Prop,rect)
        { id="r3_"+id,label=label,singleInstance=single,isMerged=!single&&rect.width*rect.height>1 };
        if (prefab == null)
        {
            var entry=palette.Entries.Single(e=>e?.DisplayName=="白方块");
            box.paletteEntryId=entry.EntryId; box.paletteEntryName=entry.DisplayName;
        }
        else if (prefab=="DoorButton") { box.propEntryId=PlanningDoorUtility.ButtonEntryId; box.propEntryName="门按钮"; }
        else
        {
            var entries=palette.PropEntries.Where(e=>e?.Prefab?.name==prefab).ToArray();
            Check(entries.Length>0&&entries.Select(e=>AssetDatabase.GetAssetPath(e.Prefab)).Distinct().Count()==1,"unambiguous prefab "+prefab);
            box.propEntryId=entries[0].EntryId; box.propEntryName=entries[0].DisplayName;
        }
        room.boxes.Add(box); return box;
    }
    private static void Parameter(PlanningBox box,Type type,string path,string kind,string value,string relativePath="")
    {
        var p=new LevelEditorComponentValueOverride(); p.Configure(relativePath,type.FullName,0,path,kind,value,null);
        box.hasComponentOverrides=true; box.componentOverrides.Add(p);
    }
    private static void Apply(PlanningDocument doc)
    {
        PlanningSceneBuilder.SuppressSceneUndo=true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc,out var message),message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo=false; }
    }
    public static void Build()
    {
        Check(Application.dataPath.Replace('\\','/').EndsWith("/2026TapTapGameJam-R3/Assets"),"isolated R3 worktree only");
        Check(!File.Exists(ScenePath)&&!File.Exists(SavePath),"new scene only; never overwrite saved designer work");
        Preflight(); var doc=CreateDocument();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); Apply(doc);
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn),"player start");
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        player.name="Player"; player.transform.position=spawn;
        var go=new GameObject("Main Camera") { tag="MainCamera" };
        var camera=go.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.15f,.19f);
        go.AddComponent<AudioListener>(); camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var manager=go.AddComponent<CameraControlManager>(); manager.ConfigureOutput(camera);
        var mode=go.AddComponent<CameraModeController>(); mode.Configure(camera,player.transform);
        var follow=go.AddComponent<CameraFollowController>(); follow.Configure(manager,mode,player.transform); follow.SetRequestedMode(CameraViewMode.Side2D,0,true);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab"));
        new GameObject("R3 Starting Progress").AddComponent<R3SceneSetup>();
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=Color.white;
        var light=new GameObject("Directional Light").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=.8f;
        light.transform.rotation=Quaternion.Euler(50,-30,0);
        Check(EditorSceneManager.SaveScene(scene,ScenePath),"scene saved");
        PlanningSaveStore.Save(SavePath,doc); AssetDatabase.Refresh(); AssetDatabase.SaveAssets(); Verify();
    }
    public static void Preflight()
    {
        checks=0; var source=ReadSource(); var doc=CreateDocument(); var room=doc.rooms.Single();
        Check(PlanningDocument.FromPersistentJson(doc.ToPersistentJson()).ToPersistentJson()==doc.ToPersistentJson(),"persistent roundtrip");
        var walls=room.boxes.Where(b=>b.type==PlanningDetailType.Solid).SelectMany(BoxCells).ToList();
        Check(walls.Count==551&&walls.Distinct().Count()==551&&walls.ToHashSet().SetEquals(Terrain(source).Select(c=>new Vector2Int(c.x,c.y))),"partition retains exact terrain");
        Check(!walls.Contains(ExcelRect("CZ",78).position)&&!walls.Contains(ExcelRect("CZ",79).position),"approved two-cell doorway");
        Check(room.boxes.Count(b=>b.platformMountId=="r3_lift")==3,"three persistent platform mounts");
        Check(source.spawn.column=="Z"&&source.spawn.row==96,"final safe Z96 spawn");
        Check(source.lift.row==97&&source.lift.topRow==88&&source.lift.crankRow==96&&source.lift.redRow==96&&source.lift.blueRow==96,"final above-lava row97-88 route and row96 attachments");
        Check(source.waterCells.Count==59&&source.lavaCells.Count==135&&source.vineCells.Count==8,"exact source liquid and vine counts");
        var lava=source.lavaCells.Select(c=>ExcelRect(c.column,c.row).position).ToHashSet();
        var terrain=Terrain(source).Select(c=>new Vector2Int(c.x,c.y)).ToHashSet();
        for(int row=source.lift.topRow;row<=source.lift.row;row++)
        {
            foreach(var p in ExcelRect(source.lift.column,row,4).allPositionsWithin)
                Check(!lava.Contains(p)&&!terrain.Contains(p),"platform entire route avoids lava/walls");
            foreach(var column in new[]{source.lift.redColumn,source.lift.blueColumn,source.lift.crankColumn})
            {var p=ExcelRect(column,row-1).position;Check(!lava.Contains(p)&&!terrain.Contains(p),"attachments entire route avoids lava/walls");}
        }
        var imported=PlanningFragmentUtility.Import(new PlanningDocument(),doc,new Vector2Int(100,100),true,out int detached);
        Check(detached==0,"complete fragment retains all links");
        var boxes=imported.rooms.Single().boxes; var lift=boxes.Single(b=>b.id!="r3_lift"&&b.label.StartsWith("曲柄平台",StringComparison.Ordinal));
        Check(boxes.Count(b=>b.platformMountId==lift.id)==3,"fragment remaps all three mount IDs");
        var orphan=PlanningDocument.FromPersistentJson(doc.ToPersistentJson()); orphan.rooms[0].boxes.RemoveAll(b=>b.id=="r3_lift");
        PlanningMechanismUtility.RemoveMissingLinks(orphan.rooms[0].boxes);
        Check(orphan.rooms[0].boxes.All(b=>string.IsNullOrEmpty(b.platformMountId)),"source deletion clears mounts");
        File.WriteAllText("Library/R3Preflight.txt",$"PASS {checks} source assertions; {room.boxes.Count} author objects; 551 walls,59 water,135 lava,8 vine cells. Fragment mount remapping and source deletion checked.");
        Debug.Log(File.ReadAllText("Library/R3Preflight.txt"));
    }
    public static void Verify()
    {
        checks=0; var doc=PlanningSaveStore.Read(SavePath);
        Check(doc.ToPersistentJson()==CreateDocument().ToPersistentJson(),"paired save equals confirmed source");
        var author=EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc);
        var scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        foreach (var root in author.GetRootGameObjects()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scratch);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scratch); EditorSceneManager.CloseScene(author,true);
        var before=Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p=>p.GetInstanceID()).OrderBy(i=>i).ToArray();
        Apply(doc); VerifyScene(doc);
        Check(before.SequenceEqual(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p=>p.GetInstanceID()).OrderBy(i=>i)),"no-op apply retains all instances and mounts");
        EditorSceneManager.OpenScene(ScenePath); VerifyScene(doc);
        File.WriteAllText("Library/R3Verification.txt",$"PASS {checks} assertions; paired/embedded/source planning, exact collider geometry, no-op apply/reload, standard player, two-button door, fixed green roots and persistent mounts. Not a full route completion.");
        Debug.Log(File.ReadAllText("Library/R3Verification.txt"));
    }
    private static void VerifyScene(PlanningDocument doc)
    {
        Physics.SyncTransforms(); var room=doc.rooms.Single();
        var lifecycle=Object.FindFirstObjectByType<RoomLifecycleManager>();
        Check(lifecycle!=null&&PlanningDocument.FromPersistentJson(lifecycle.PlanningJson).ToPersistentJson()==doc.ToPersistentJson(),"embedded equals paired planning");
        var placed=Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(placed.Length==room.boxes.Count,"one instance per author object: expected "+room.boxes.Count+", actual "+placed.Length+"; missing "+string.Join(",",room.boxes.Select(b=>b.id).Except(placed.Select(p=>p.PlanningBoxId))));
        foreach(var item in placed)
        {
            var b=room.boxes.Single(v=>v.id==item.PlanningBoxId);
            Check(item.SizeCells==new Vector2Int(b.width,b.height),"footprint "+b.id);
            Check(item.Cell==new Vector2Int(b.x,-b.y-b.height),"exact planning cell "+b.id);
            Check(item.GetComponent<RoomResetObject>()!=null,"region membership "+b.id);
            if(b.type==PlanningDetailType.Solid)
            {
                var shape=item.GetComponent<BoxCollider>();
                Check(shape!=null&&Vector3.Distance(shape.bounds.center,new Vector3(b.x+b.width*.5f,-b.y-b.height*.5f,0))<.001f&&
                    Vector3.Distance(shape.bounds.size,new Vector3(b.width,b.height,1))<.001f,"exact solid collider "+b.id);
            }
            if(!string.IsNullOrEmpty(b.platformMountId))
            {
                var mount=item.GetComponent<PlatformMount>();
                Check(mount!=null&&mount.Platform!=null&&mount.Platform.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId==b.platformMountId,"persistent mount "+b.id);
                Check(Vector3.Distance(mount.TargetPosition,item.transform.position)<.001f,"mount anchor "+b.id);
            }
        }
        var door=Object.FindFirstObjectByType<DoorController>();
        Check(door!=null&&door.Buttons.Count==2&&door.Buttons.All(b=>b.Owner==door&&b.Persistent),"two persistent buttons, one AND door");
        var lift=Object.FindObjectsByType<MovingPlatformFeature>(FindObjectsSortMode.None).Single();
        Check(lift.CrankController!=null&&lift.IsCrankControlled&&Mathf.Approximately(lift.Path.FirstEnd,ReadSource().lift.row-88),"crank route");
        foreach(var spec in ReadSource().growingCubes)
        {
            var item=placed.Single(p=>p.PlanningBoxId=="r3_"+spec.id);
            Check(item.GetComponent<Rigidbody>().constraints==RigidbodyConstraints.FreezeAll&&!item.GetComponent<CarryableFeature>().enabled,"fixed non-carryable green root");
        }
        var players=GameObject.FindGameObjectsWithTag("Player");
        Check(players.Length==1&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(players[0])==PlayerPath,"one standard player");
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn)&&Vector3.Distance(players[0].transform.position,spawn)<.001f,"confirmed spawn");
        Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1,"one camera");
        Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1,"one listener");
        Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"one event system");
        foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0&&!PrefabUtility.IsPrefabAssetMissing(t.gameObject),"no missing dependencies");
    }
    public static void FinalAudit() { Preflight(); Verify(); }
}
