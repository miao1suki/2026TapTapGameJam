using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

// Repeatable authoring + real player-loop regression. Never runs in the main Unity project.
public static class WaterLayerShapeAudit
{
    private const string ScenePath = "Assets/Scenes/WaterLayerShapeAudit.unity";
    private const string Flag = "WaterLayerShapeAudit.Running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks, phase;
    private static double started;
    private static float deadline;
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("WATER SHAPE: " + label);
        checks++; Debug.Log("WATER SHAPE PASS: " + label);
    }
    private static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
    private static void Guard()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("此验证仅可在隔离工程执行。");
    }
    private static PlanningBox Prop(LevelEditorPalette palette, string name, RectInt rect)
    {
        var entry = palette.PropEntries.First(e => e?.Prefab?.name == name);
        return new PlanningBox(PlanningDetailType.Prop, rect) { propEntryId = entry.EntryId, propEntryName = entry.DisplayName };
    }
    public static void Run()
    {
        Guard(); checks = 0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var palette = LevelEditorPaletteService.GetOrCreate();
        var doc = new PlanningDocument { name = "水层与不规则形状验证", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("验证") { id = "water_shape_room" };
        foreach (Vector2Int cell in new RectInt(0,0,32,14).allPositionsWithin) room.cells.Add(new PlanningCell(cell.x,cell.y));
        doc.rooms.Add(room);
        var ring = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in new RectInt(1,1,4,4).allPositionsWithin)
            if (cell != new Vector2Int(2,2) && cell != new Vector2Int(3,2))
            { ring.Add(cell); room.boxes.Add(Prop(palette,"Blue_WaterSource",new RectInt(cell,Vector2Int.one))); }
        var canvas = new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(doc); canvas.SetMode(PlanningCanvasMode.Detail);
        string error = null; canvas.ValidationFailed += message => error = message;
        canvas.SetWaterEditing(true); canvas.ToggleMergeArea(new RectInt(1,1,4,4)); canvas.MergeSelectedItems();
        Check(error == null && room.boxes.Count == 1 && room.boxes[0].isMerged,"non-rectangular water merge succeeds");
        PlanningBox water = room.boxes[0]; string waterId = water.id;
        Check(PlanningShapeUtility.Cells(water).ToHashSet().SetEquals(ring),"merge preserves every cell and both holes");
        Check(!PlanningShapeUtility.Contains(water,new Vector2Int(2,2)),"hole is not hit-testable water");
        var cached = PlanningShapeUtility.Rectangles(water);
        for (int i = 0; i < 100; i++) Check(ReferenceEquals(cached,PlanningShapeUtility.Rectangles(water)),"pan reuses compressed coverage " + i);
        canvas.UnmergeSelectedItems(); Check(room.boxes.Count == ring.Count,"unmerge restores source cells");
        canvas.ToggleMergeArea(new RectInt(1,1,4,4)); canvas.MergeSelectedItems();
        // Unmerge selects restored items, so toggle once clears and a second time selects them.
        if (room.boxes.Count != 1) { canvas.ToggleMergeArea(new RectInt(1,1,4,4)); canvas.MergeSelectedItems(); }
        Check(room.boxes.Count == 1,"second merge after unmerge");
        water = room.boxes[0]; waterId = water.id;
        var wallEntry = palette.Entries.First(e => e?.DisplayName == "白方块");
        PlanningBox Wall(Vector2Int cell) => new PlanningBox(PlanningDetailType.Solid,new RectInt(cell,Vector2Int.one))
            { paletteEntryId = wallEntry.EntryId, paletteEntryName = wallEntry.DisplayName };
        var overWall = Wall(new Vector2Int(1,1)); overWall.id = "water_shape_wall"; room.boxes.Add(overWall);
        room.boxes.Add(Wall(new Vector2Int(2,2)));
        canvas.SetWaterEditing(false);
        Check(ReferenceEquals(Invoke(canvas,"FindBoxAt",room,1,1),overWall),"foreground selected above water");
        Check(!ReferenceEquals(Invoke(canvas,"FindBoxAt",room,2,2),water),"hole can contain a wall independently");
        canvas.SetWaterEditing(true);
        Check(ReferenceEquals(Invoke(canvas,"FindBoxAt",room,1,1),water),"water-only selection passes through wall");
        Check(Invoke(canvas,"FindBoxAt",room,2,2) == null,"water-only selection ignores hole wall");
        Check(!(bool)Invoke(canvas,"CanEditPlacement",overWall),"water-only rejects foreground placement");
        var foreground = (VisualElement)typeof(PlanningCanvas).GetField("objectPreviewLayer",Hidden).GetValue(canvas);
        Check(Mathf.Approximately(foreground.style.opacity.value,.2f),"foreground opacity dims in water-only mode");
        canvas.SetWaterEditing(false); Check(Mathf.Approximately(foreground.style.opacity.value,1f),"foreground restores on leaving water-only mode");
        // All requested foreground families coexist with water, including uncolored triggers.
        string[] names = { "Green_PlantObstacle","Green_Ladder","Red_Platform","Collectible","Red_Foundation","Red_Crank","Respawn_Block","Spike_上","Red_Button" };
        for (int i = 0; i < names.Length; i++)
        {
            var rect = new RectInt(8+i*3,2,1,1);
            room.boxes.Add(Prop(palette,"Blue_WaterSource",rect));
            var prop = Prop(palette,names[i],rect); prop.id = "water_shape_overlay_" + i;
            if (names[i] == "Red_Crank") prop.initialCrankPlatformCreated = true;
            room.boxes.Add(prop);
        }
        var covered = Prop(palette,"Blue_WaterSource",new RectInt(6,2,1,1)); covered.id="water_shape_covered";
        room.boxes.Add(covered); room.boxes.Add(Wall(new Vector2Int(6,2)));
        // Lava receives exactly the same irregular merge behavior.
        for (int i = 0; i < 3; i++) room.boxes.Add(Prop(palette,"Red_Lava",new RectInt(9+(i==2?0:i),7+(i==2?1:0),1,1)));
        canvas.SetDocument(doc); canvas.SetMode(PlanningCanvasMode.Detail);
        canvas.ToggleMergeArea(new RectInt(9,7,2,2)); canvas.MergeSelectedItems();
        PlanningBox lava = room.boxes.Single(b => b.isMerged && b.propEntryId != water.propEntryId);
        lava.id = "water_shape_lava";
        Check(PlanningShapeUtility.Cells(lava).Count() == 3 && !PlanningShapeUtility.Contains(lava,new Vector2Int(10,8)),"lava retains L shape and hole");
        // Missing top-left corner must still produce an owner, not zero instances.
        var missingCorner = Prop(palette,"Blue_WaterSource",new RectInt(1,8,2,2));
        missingCorner.isMerged = true; missingCorner.id = "water_shape_missing_corner";
        foreach (Vector2Int cell in new[] {new Vector2Int(2,8),new Vector2Int(1,9),new Vector2Int(2,9)})
            missingCorner.mergeParts.Add(JsonUtility.ToJson(Prop(palette,"Blue_WaterSource",new RectInt(cell,Vector2Int.one))));
        room.boxes.Add(missingCorner);
        var roundTrip = JsonUtility.FromJson<PlanningDocument>(JsonUtility.ToJson(doc));
        Check(PlanningShapeUtility.Cells(roundTrip.rooms[0].boxes.Single(b=>b.id==waterId)).ToHashSet().SetEquals(ring),"JSON round trip preserves shape");
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc,out string message),"builder: " + message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Physics.SyncTransforms();
        LevelEditorPlacedBlock ById(string id) => Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Single(b=>b.PlanningBoxId==id);
        var placedWater = ById(waterId); placedWater.name = "Audit Water Ring";
        var shape = placedWater.GetComponent<LevelEditorMergedShape>();
        Check(shape != null && shape.Volumes.Count == PlanningShapeUtility.Rectangles(water).Count,"single owner with compressed compound colliders");
        Check(placedWater.GetComponents<WaterSourceFeature>().Length == 1,"one water gameplay state, not per-cell scripts");
        foreach (Vector2Int cell in new RectInt(1,1,4,4).allPositionsWithin)
        {
            Vector3 point = new Vector3(cell.x+.5f,-cell.y-.5f,0);
            Check(shape.ContainsWorldPoint(point)==ring.Contains(cell),"runtime shape coverage " + cell);
            bool actual = shape.Volumes.Any(c=>c.bounds.Contains(point));
            Check(actual==ring.Contains(cell),"actual colliders preserve holes " + cell);
            Check(placedWater.ContainsCell(new Vector2Int(cell.x,-cell.y-1))==ring.Contains(cell),"placed metadata coverage " + cell);
        }
        Check(ById(missingCorner.id).GetComponent<LevelEditorMergedShape>()!=null,"missing top-left anchor creates one owner");
        for (int i = 0; i < names.Length; i++) Check(ById("water_shape_overlay_"+i)!=null,"water coexists with "+names[i]);
        int instance = placedWater.GetInstanceID();
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc,out _),"incremental apply succeeds"); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Check(ById(waterId).GetInstanceID()==instance,"unchanged merge retains scene instance");
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.orthographic=true;
        camera.orthographicSize=6; camera.transform.position=new Vector3(4,-5,-20); camera.backgroundColor=new Color(.15f,.12f,.2f);
        camera.clearFlags=CameraClearFlags.SolidColor;
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"));
        player.transform.position=new Vector3(6,-6,0); player.name="Audit Player";
        var light = new GameObject("Light").AddComponent<Light>(); light.type=LightType.Directional;
        RenderSettings.ambientLight=Color.white;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
        Debug.Log("WATER SHAPE AUTHOR COMPLETE: "+checks);
        SessionState.SetBool(Flag,true); SessionState.SetInt(Flag+".Checks",checks); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag,false)) return;
        checks=SessionState.GetInt(Flag+".Checks",0); phase=0; deadline=1f; started=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        Application.logMessageReceived-=OnLog; Application.logMessageReceived+=OnLog;
    }
    private static void OnLog(string message,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) Finish(false,message+"\n"+stack); }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup-started>90) { Finish(false,"timeout"); return; }
        if(!EditorApplication.isPlaying||Time.time<deadline) return;
        try
        {
            var player=Object.FindFirstObjectByType<PlayerController>();
            if(phase==0)
            {
                player.GetComponent<PlayerInputDriver>().enabled=false; player.Motor.constraints=RigidbodyConstraints.FreezeAll;
                foreach(string color in new[]{"red","green","blue"}) ColorRuntimeService.Instance.Unlock(color);
                phase=1; deadline=Time.time+1; return;
            }
            var water=GameObject.Find("Audit Water Ring").GetComponent<WaterSourceFeature>();
            void Move(Vector3 p) { player.transform.position=p; player.Motor.position=p; Physics.SyncTransforms(); }
            if(phase==2)
            {
                Check(player.IsSwimming,"real physics enters compound water across a seam");
                Move(new Vector3(2.5f,-3.75f,0)); phase=3; deadline=Time.time+.2f; return;
            }
            if(phase==3)
            {
                Check(player.IsSwimming,"exiting one compound collider does not exit remaining water");
                var capsule=player.GetComponent<CapsuleCollider>();
                capsule.radius=.4f; capsule.height=.8f;
                Move(new Vector3(3.5f,-2.5f,0)); phase=4; deadline=Time.time+.2f; return;
            }
            if(phase==4)
            {
                Check(!player.IsSwimming,"player in a real hole is not swimming");
                Project.Rooms.RoomLifecycleManager.ForScene(water.gameObject.scene).ResetRegion("water_shape_room");
                Move(new Vector3(1.5f,-3,0)); phase=5; deadline=Time.time+.3f; return;
            }
            if(phase==5)
            {
                Check(player.IsSwimming,"water contacts recover after room reset");
                Check(ColorRuntimeService.Instance.IsUnlocked("blue")&&!ColorRuntimeService.Instance.IsAbilityUnlocked("blue"),"room reset preserves separate scene/ability unlocks");
                Move(new Vector3(2.5f,-3.75f,0)); phase=50; deadline=Time.time+.2f; return;
            }
            if(phase==50)
            {
                Check(player.IsSwimming,"room reset retains contact when exiting only one compound volume");
                Move(new Vector3(3.5f,-2.5f,0)); phase=51; deadline=Time.time+.2f; return;
            }
            if(phase==51)
            {
                Check(!player.IsSwimming,"room reset does not leave stale compound contacts in a hole");
                var lava=Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Single(b=>b.PlanningBoxId=="water_shape_lava").GetComponent<LavaHazardFeature>();
                Check(lava.ApplyColor("blue",player.gameObject),"irregular lava accepts existing cooling reaction");
                phase=6; deadline=Time.time+.2f; return;
            }
            if(phase==6)
            {
                var cooled=Object.FindObjectsByType<LevelEditorMergedShape>(FindObjectsSortMode.None).Single(s=>s.GetComponent<LevelEditorPlacedBlock>()==null&&s.Dimensions==new Vector2Int(2,2));
                Check(cooled.Rectangles.Sum(r=>r.width*r.height)==3&&!cooled.ContainsWorldPoint(new Vector3(10.5f,-8.5f,0)),"cooled lava retains size, L shape and empty hole");
                Check(!cooled.Volumes.Any(c=>c.bounds.Contains(new Vector3(10.5f,-8.5f,0))),"cooled lava collision also preserves hole");
                Project.Rooms.RoomLifecycleManager.ForScene(water.gameObject.scene).ResetRegion("water_shape_room");
                phase=7; deadline=Time.time+.3f; return;
            }
            if(phase==7)
            {
                Check(Object.FindObjectsByType<LavaHazardFeature>(FindObjectsSortMode.None).Any(l=>l.GetComponent<LevelEditorMergedShape>()!=null),"room reset restores irregular lava");
                phase=8;
            }
            if(phase==8)
            {
                if(!ColorAppearanceManager.Instance.IsComplete("blue")) { deadline=Time.time+.2f; return; }
                var camera=Camera.main; var target=new RenderTexture(1200,900,24); var previous=camera.targetTexture;
                camera.targetTexture=target; camera.Render(); var old=RenderTexture.active; RenderTexture.active=target;
                var image=new Texture2D(1200,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1200,900),0,0); image.Apply();
                File.WriteAllBytes("Library/WaterLayerShapeOverview.png",image.EncodeToPNG());
                camera.targetTexture=previous; RenderTexture.active=old; Object.Destroy(target); Object.Destroy(image);
                Finish(true,"PASS "+checks+" author/runtime assertions"); return;
            }
            var presentation=water.GetComponentInChildren<GameplayWaterPresentation>();
            Check(presentation!=null,"runtime pixel water loaded");
            Check(presentation.SurfaceCellCount+presentation.BodyCellCount+presentation.InteriorCellCount==13,"water visible cells exclude two holes and overlapping wall");
            var front=presentation.transform.Find("Water Refraction").GetComponent<MeshFilter>().sharedMesh;
            Check(front.vertexCount==13*4,"refraction has no bounding-box fill in holes or walls");
            var shape=water.GetComponent<LevelEditorMergedShape>();
            Check(shape.Rectangles.Sum(r=>r.width*r.height)==14,"play-mode reload retains serialized exact shape");
            Check(!shape.ContainsWorldPoint(new Vector3(2.5f,-2.5f,0)),"hole still empty after reload");
            var sensor=player.GetComponent<PlayerInteractionSensor>();
            Check(sensor.TryGetScreenHit(Camera.main,Camera.main.WorldToScreenPoint(new Vector3(1.5f,-1.5f,0)),out var hit)&&
                hit.collider.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId=="water_shape_wall","screen click on wall never falls through to water");
            var volume=shape.Volumes.First(c=>c.bounds.Contains(new Vector3(1.5f,-1.5f,0)));
            Check(WaterCellVisibility.TryVisiblePoint(water,volume,new Vector3(1.5f,-1.5f,0),5,out var visible)&&
                !WaterCellVisibility.Covered(water,visible,1),"water link endpoint finds visible portion, not wall");
            var coveredWater=Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Single(b=>b.PlanningBoxId=="water_shape_covered").GetComponent<WaterSourceFeature>();
            Check(!WaterCellVisibility.TryVisiblePoint(coveredWater,coveredWater.GetComponent<Collider>(),new Vector3(6.5f,-2.5f,-1),5,out _),"fully wall-covered water has no link endpoint");
            Check(sensor.TryGetScreenHit(Camera.main,Camera.main.WorldToScreenPoint(new Vector3(8.5f,-2.5f,0)),out hit)&&
                hit.collider.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId=="water_shape_overlay_0","color click prioritizes plant above water");
            Move(new Vector3(1.5f,-3,0)); phase=2; deadline=Time.time+.2f;
        }
        catch(Exception error) { Finish(false,error.ToString()); }
    }
    private static void Finish(bool passed,string message)
    {
        SessionState.SetBool(Flag,false); EditorApplication.update-=Tick; Application.logMessageReceived-=OnLog;
        File.WriteAllText("Library/WaterLayerShapeResult.txt",message); Debug.Log(message);
        if(EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1); }
        else EditorApplication.Exit(passed?0:1);
    }
}
