using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.BossLogic.Editor
{
    /// <summary>One-shot, isolated authoring patch. Never rebuilds a world or moves an existing object.</summary>
    public static class BossStartAuthoringPatch
    {
        private const string Gaming = "Assets/_Project/Scenes/Playing/Gaming.unity";
        private const string Boss = "Assets/_Project/Scenes/Boss.unity";
        private const string Formal = "Assets/_Project/Content/LevelMaps/正式关卡.json";
        private const string EntryId = "box_aa7a24dc";
        private const string StartId = "box_gameplay_start_anchor";
        private const string Folder = "Assets/Boss_Logic/Authoring";
        private const string StartPrefab = Folder + "/GamingStartAnchor.prefab";
        private const string GatePrefab = Folder + "/BossCollectibleEntryTrigger.prefab";
        private const string StandardPlayer = "Assets/_Project/Content/Player/Player.prefab";
        private static readonly StringBuilder log = new StringBuilder();

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0 || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Only run in the silent, non-playing isolated FlowRetirementProbe.");
            log.Clear();
            try { Patch(); File.WriteAllText("Library/BossStartAuthoringPatch.txt", "PASS\n" + log); Debug.Log(log); EditorApplication.Exit(0); }
            catch (Exception error)
            {
                File.WriteAllText("Library/BossStartAuthoringPatch.txt", "FAIL\n" + log + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private static void Patch()
        {
            for (int i=0;i<SceneManager.sceneCount;i++)
                Require(!SceneManager.GetSceneAt(i).isDirty,"no dirty scene may be overwritten");
            Require(PlanningEditorWindow.DocumentForScene(SceneManager.GetActiveScene().handle)==null,
                "no open planning editor document may overwrite this patch");
            PlanningDocument gaming=Read(Path.ChangeExtension(Gaming,"planning.json"));
            PlanningDocument formal=Read(Formal);
            Require(gaming.ToPersistentJson()==formal.ToPersistentJson(),"Gaming and formal save match before patch");
            PlanningDocument boss=Read(Path.ChangeExtension(Boss,"planning.json"));
            var originalGaming=SnapshotBoxes(gaming); var originalBoss=SnapshotBoxes(boss);
            string gamingLayout=NonBoxFingerprint(gaming),bossLayout=NonBoxFingerprint(boss);
            PlanningBox entry=FindBox(boss,EntryId,out var bossRoom);
            Require(entry!=null && entry.hasComponentOverrides,"Boss entry has explicit Timeline configuration");
            string originalEntry=JsonUtility.ToJson(entry);
            LevelEditorPalette palette=FindPalette(entry.propEntryId,entry.propEntryName);
            LevelEditorPropEntry originalProp=FindProp(palette,entry.propEntryId);
            Require(originalProp?.Prefab!=null,"entry source prefab is present");
            // Opening a Scene in Single mode unloads unused assets. Keep stable paths,
            // not Unity object handles (or nested entry references), across that boundary.
            string palettePath=AssetDatabase.GetAssetPath(palette);
            string sourcePrefabPath=AssetDatabase.GetAssetPath(originalProp.Prefab);

            Scene gamingScene=EditorSceneManager.OpenScene(Gaming,OpenSceneMode.Single);
            Scene bossScene=EditorSceneManager.OpenScene(Boss,OpenSceneMode.Additive);
            Require(PlanningEditorWindow.DocumentForScene(gamingScene.handle)==null &&
                PlanningEditorWindow.DocumentForScene(bossScene.handle)==null,"neither target has an open planning document");
            palette=LoadPalette(palettePath);
            originalProp=FindProp(palette,entry.propEntryId);
            Require(originalProp!=null && AssetDatabase.GetAssetPath(originalProp.Prefab)==sourcePrefabPath,
                "entry source is reacquired after opening target scenes");
            RoomLifecycleManager gm=Manager(gamingScene),bm=Manager(bossScene);
            Require(gm.transform.position==Vector3.zero && gm.transform.rotation==Quaternion.identity && gm.transform.lossyScale==Vector3.one,
                "generated Gaming coordinate root is identity");
            PlayerController[] players=All<PlayerController>(gamingScene);
            Require(players.Length==1,"Gaming contains one standard player");
            PlayerController player=players[0];
            Require(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(player.gameObject))==StandardPlayer,
                "Start uses the real standard Player instance");
            Vector3 spawn=player.transform.position;
            Require(Vector3.Distance(spawn,gaming.playerStartWorld)<.002f,"actual player pose agrees with authored start");
            PlanningRoom room=gaming.rooms.Single(r=>r.id==gaming.playerStartRoomId);
            Transform roomParent=gm.transform.Cast<Transform>().SingleOrDefault(t=>t.name.Contains("["+room.id+"]"));
            Require(roomParent!=null,"existing player room container found without creating/rebuilding rooms");
            float cell=gm.CellSize;
            Vector2Int global=new Vector2Int(Mathf.FloorToInt(spawn.x/cell),-Mathf.FloorToInt(spawn.y/cell)-1);
            Vector2Int origin=new Vector2Int(room.cells.Min(c=>c.x),room.cells.Min(c=>c.y));
            Vector2Int local=global-origin;
            Require(room.cells.Any(c=>c.x==global.x && c.y==global.y),"Start grid anchor is inside the player room");
            Vector3 center=new Vector3((global.x+.5f)*cell,(-global.y-.5f)*cell,0);
            Vector3 offset=spawn-center;
            LevelEditorPlacedBlock entryScene=All<LevelEditorPlacedBlock>(bossScene).Single(b=>b.PlanningBoxId==EntryId);
            Require(entryScene.GetComponent<ContactTimelineTrigger>()!=null,"existing Boss entry is a Timeline trigger");
            var gamingPoses=Capture(gamingScene); var bossPoses=Capture(bossScene);
            int gamingPlaced=All<LevelEditorPlacedBlock>(gamingScene).Length,bossPlaced=All<LevelEditorPlacedBlock>(bossScene).Length;
            bool hadStart=FindBox(gaming,StartId,out _)!=null;
            Require(!All<TeleportAnchorFeature>(gamingScene).Any(a=>a.TeleportId=="Start" &&
                a.GetComponentInParent<LevelEditorPlacedBlock>()?.PlanningBoxId!=StartId),"no unrelated Start ID will be overwritten");
            log.AppendLine($"Preflight: Gaming {gamingPlaced} placed objects; Boss {bossPlaced}; exact player start {spawn:R}; offset {offset:R}");
            if (entryScene.TryGetComponent<BoxCollider>(out var triggerShape))
                log.AppendLine($"Boss trigger actual bounds {triggerShape.bounds}; return ID=Start (no fake Boss Start or cross-scene load).");

            EnsureFolder();
            SceneManager.SetActiveScene(gamingScene);
            CreateStartPrefab(offset);
            CreateGatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath));
            // Saving/importing a new Prefab may also unload assets. Reacquire each
            // palette/prefab from disk, and persist each addition so a partial run can
            // be retried without duplicate entries or dangling managed wrappers.
            EnsureProp(palettePath,"正式游戏起点（Start）",StartPrefab);
            EnsureProp(palettePath,"Boss入场（收集30%）",GatePrefab);
            palette=LoadPalette(palettePath);
            LevelEditorPropEntry startProp=palette.PropEntries.Single(e=>AssetDatabase.GetAssetPath(e.Prefab)==StartPrefab);
            LevelEditorPropEntry gateProp=palette.PropEntries.Single(e=>AssetDatabase.GetAssetPath(e.Prefab)==GatePrefab);
            GameObject startPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(StartPrefab);
            Require(startPrefab!=null,"Start template is reacquired after all imports");
            PlanningBox start=FindBox(gaming,StartId,out _);
            if (start==null)
            {
                start=new PlanningBox(PlanningDetailType.Prop,new RectInt(local,Vector2Int.one)) { id=StartId,
                    label="正式游戏出生点 Start",singleInstance=true,propEntryId=startProp.EntryId,propEntryName=startProp.DisplayName };
                room.boxes.Add(start);
            }
            else Require(start.x==local.x && start.y==local.y && start.propEntryId==startProp.EntryId,
                "existing tool-owned Start retains the same grid origin");
            PatchGateBox(entry,gateProp);
            var expectedEntry=JsonUtility.FromJson<PlanningBox>(originalEntry); PatchGateBox(expectedEntry,gateProp);
            Require(JsonUtility.ToJson(entry)==JsonUtility.ToJson(expectedEntry),"entry delta is only its template and Gate parameters");
            originalBoss[EntryId]=JsonUtility.ToJson(expectedEntry);
            VerifyBoxes(gaming,originalGaming,hadStart ? null : StartId);
            VerifyBoxes(boss,originalBoss,null);
            Require(NonBoxFingerprint(gaming)==gamingLayout && NonBoxFingerprint(boss)==bossLayout,
                "all room/cell/player/link/layout metadata unchanged");

            LevelEditorPlacedBlock startScene=All<LevelEditorPlacedBlock>(gamingScene).SingleOrDefault(b=>b.PlanningBoxId==StartId);
            if (startScene==null)
            {
                GameObject go=(GameObject)PrefabUtility.InstantiatePrefab(startPrefab,gamingScene);
                go.transform.SetParent(roomParent,true); go.transform.position=center;
                startScene=go.AddComponent<LevelEditorPlacedBlock>();
                startScene.Configure(new Vector2Int(global.x,-global.y-1),startProp.DisplayName,Color.white,false,true,cell);
                startScene.gameObject.AddComponent<RoomResetObject>().SetRegion(room.id);
            }
            SetSignature(startScene,start);
            BossCollectibleEntryGate gate=entryScene.GetComponent<BossCollectibleEntryGate>() ?? entryScene.gameObject.AddComponent<BossCollectibleEntryGate>();
            ConfigureGate(gate); PrefabUtility.RecordPrefabInstancePropertyModifications(gate); SetSignature(entryScene,entry);
            var triggerSettings=new SerializedObject(entryScene.GetComponent<ContactTimelineTrigger>());
            triggerSettings.FindProperty("targetCollider").objectReferenceValue=null;
            triggerSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(entryScene.GetComponent<ContactTimelineTrigger>());
            Require(Vector3.Distance(startScene.GetComponentInChildren<TeleportAnchorFeature>(true).transform.position,spawn)<.00001f,
                "Start destination exactly matches Player, not the grid center");
            float previousCellSize=LevelEditorState.CellSize;
            try
            {
                LevelEditorState.CellSize=cell;
                VerifyGeneratedBox(palette,start,global,spawn);
                Vector2Int bossOrigin=new Vector2Int(bossRoom.cells.Min(c=>c.x),bossRoom.cells.Min(c=>c.y));
                LevelEditorState.CellSize=bm.CellSize;
                VerifyGeneratedBox(palette,entry,bossOrigin+new Vector2Int(entry.x,entry.y),null);
                VerifyFragmentImport(palette,start,entry);
            }
            finally {LevelEditorState.CellSize=previousCellSize;}
            VerifyPoses(gamingPoses); VerifyPoses(bossPoses);
            Require(All<LevelEditorPlacedBlock>(gamingScene).Length==gamingPlaced+(hadStart?0:1) &&
                All<LevelEditorPlacedBlock>(bossScene).Length==bossPlaced,"original placed object counts unchanged except one new Gaming anchor");
            string gateEntryId=gateProp.EntryId;
            Save(gamingScene,gm,gaming,Formal); Save(bossScene,bm,boss,null);
            palette=LoadPalette(palettePath);EditorUtility.SetDirty(palette);AssetDatabase.SaveAssets();
            Require(Read(Path.ChangeExtension(Gaming,"planning.json")).ToPersistentJson()==Read(Formal).ToPersistentJson(),
                "Gaming paired and formal JSON remain identical");
            Require(FindBox(Read(Path.ChangeExtension(Boss,"planning.json")),EntryId,out _).propEntryId==gateEntryId,
                "Boss paired JSON persists the Gate template");
            log.AppendLine("Exact delta: Gaming +one Start box and invisible root/Destination prefab instance; Boss original entry template ID/name +Gate(0.3,Start,null,true), and only its invalid targetCollider reference:local -> reference:null. All other existing box parameters retained.");
            log.AppendLine("PASS: every original transform/active state/box ID retained; no full-map builder was called.");
            log.AppendLine("Deliver Gaming.unity + Gaming.planning.json + 正式关卡.json + Boss.unity + Boss.planning.json + palette + both Authoring prefabs, with their existing/new meta files.");
        }

        private static GameObject CreateStartPrefab(Vector3 offset)
        {
            GameObject existing=AssetDatabase.LoadAssetAtPath<GameObject>(StartPrefab);
            if(existing!=null)
            {
                var a=existing.GetComponentInChildren<TeleportAnchorFeature>(true);
                Require(a!=null && a.TeleportId=="Start" && Vector3.Distance(a.transform.localPosition,offset)<.00001f,
                    "existing Start template has the same exact offset"); return existing;
            }
            var root=new GameObject("正式游戏起点 Start");
            try
            {
                var target=new GameObject("Destination"); target.transform.SetParent(root.transform,false); target.transform.localPosition=offset;
                target.AddComponent<TeleportAnchorFeature>().SetTeleportId("Start");
                return PrefabUtility.SaveAsPrefabAsset(root,StartPrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject CreateGatePrefab(GameObject source)
        {
            GameObject existing=AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefab);
            if(existing!=null) { Require(existing.GetComponent<BossCollectibleEntryGate>()!=null,"existing Gate template has the Gate"); return existing; }
            Require(source!=null,"live entry source prefab is available for a new template");
            GameObject root=(GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                root.name="Boss入场（收集30%）";
                ConfigureGate(root.GetComponent<BossCollectibleEntryGate>() ?? root.AddComponent<BossCollectibleEntryGate>());
                return PrefabUtility.SaveAsPrefabAsset(root,GatePrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void ConfigureGate(BossCollectibleEntryGate gate)
        {
            var so=new SerializedObject(gate); so.FindProperty("minimumRatio").floatValue=.3f;
            so.FindProperty("returnDestinationId").stringValue="Start"; so.FindProperty("returnPoint").objectReferenceValue=null;
            so.FindProperty("showHint").boolValue=true; so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void PatchGateBox(PlanningBox box,LevelEditorPropEntry prop)
        {
            box.propEntryId=prop.EntryId; box.propEntryName=prop.DisplayName; box.hasComponentOverrides=true;
            box.componentOverrides ??= new List<LevelEditorComponentValueOverride>();
            // A scene-local numeric collider reference is not portable. The trigger's
            // ManagedCollider resolves null to its own existing Collider, so this
            // replaces only an invalid token, not its geometry or contact behaviour.
            var localCollider=box.componentOverrides.FirstOrDefault(v=>v.ComponentPath=="" &&
                v.ComponentTypeName==typeof(ContactTimelineTrigger).FullName && v.PropertyPath=="targetCollider" && v.Value=="reference:local");
            localCollider?.Configure("",typeof(ContactTimelineTrigger).FullName,0,"targetCollider","ObjectReference","reference:null",null);
            foreach(var pair in new[] { ("minimumRatio","Float",.3f.ToString("R",CultureInfo.InvariantCulture)),
                ("returnDestinationId","String","Start"),("returnPoint","ObjectReference","reference:null"),("showHint","Boolean","1") })
            {
                var value=box.componentOverrides.FirstOrDefault(v=>v.ComponentPath=="" && v.ComponentTypeName==typeof(BossCollectibleEntryGate).FullName && v.PropertyPath==pair.Item1);
                if(value==null) {value=new LevelEditorComponentValueOverride();box.componentOverrides.Add(value);}
                value.Configure("",typeof(BossCollectibleEntryGate).FullName,0,pair.Item1,pair.Item2,pair.Item3,null);
            }
        }
        private static LevelEditorPalette FindPalette(string id,string name)
        {
            var found=AssetDatabase.FindAssets("t:LevelEditorPalette").Select(g=>AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p=>p.PropEntries.Any(e=>e.EntryId==id || e.DisplayName==name)).ToArray();
            Require(found.Length==1,"exactly one shared palette owns the original entry"); return found[0];
        }
        private static LevelEditorPropEntry FindProp(LevelEditorPalette p,string id)=>p.PropEntries.SingleOrDefault(e=>e.EntryId==id);
        private static LevelEditorPalette LoadPalette(string path)
        {
            var palette=AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(path);
            Require(palette!=null,"reacquired live palette "+path);return palette;
        }
        private static void EnsureProp(string palettePath,string label,string prefabPath)
        {
            LevelEditorPalette palette=LoadPalette(palettePath);
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(prefab!=null,"reacquired authoring template "+prefabPath);
            var entry=palette.PropEntries.SingleOrDefault(e=>AssetDatabase.GetAssetPath(e.Prefab)==prefabPath);
            if(entry!=null) {log.AppendLine("Reusing existing authoring entry "+entry.EntryId);return;}
            entry=new LevelEditorPropEntry();entry.Configure(label,prefab);palette.AddProp(entry);
            EditorUtility.SetDirty(palette);AssetDatabase.SaveAssetIfDirty(palette);
        }
        [Serializable] private sealed class Signature { public List<LevelEditorComponentValueOverride> values; }
        private static void SetSignature(LevelEditorPlacedBlock placed,PlanningBox box)
        {
            placed.SetPlanningSource(box.id,JsonUtility.ToJson(box.PersistentClone())+JsonUtility.ToJson(new Signature {
                values=LevelEditorComponentOverrideUtility.PersistentValues(box.hasComponentOverrides?box.componentOverrides:null)}),new Vector2Int(box.x,box.y));
            EditorUtility.SetDirty(placed);
        }
        private static void VerifyGeneratedBox(LevelEditorPalette palette,PlanningBox box,Vector2Int global,Vector3? destination)
        {
            var root=new GameObject("Authoring regeneration verification");
            try
            {
                var place=typeof(PlanningSceneBuilder).GetMethod("PlacePlanningBox",BindingFlags.NonPublic|BindingFlags.Static);
                Require(place!=null,"real single-box builder is available");
                var result=(LevelEditorPlacedBlock)place.Invoke(null,new object[] {palette,box,global,root.transform,new Vector2Int(box.x,box.y)});
                Require(result!=null,"single-box regeneration succeeds "+box.id);
                if(destination.HasValue)
                    Require(Vector3.Distance(result.GetComponentInChildren<TeleportAnchorFeature>(true).transform.position,destination.Value)<.00001f,
                        "real builder preserves exact Start offset");
                else
                {
                    var gate=result.GetComponent<BossCollectibleEntryGate>();
                    Require(gate!=null,"real builder retains the added Gate component");
                    var settings=new SerializedObject(gate);
                    Require(Mathf.Approximately(gate.MinimumRatio,.3f) && settings.FindProperty("returnDestinationId").stringValue=="Start",
                        "real builder retains Gate type/30%/Start after planning reconstruction");
                    ContactTimelineTrigger trigger=result.GetComponent<ContactTimelineTrigger>();
                    Require(trigger.ManagedCollider==result.GetComponent<BoxCollider>(),"null targetCollider safely resolves the trigger's own BoxCollider");
                    var timelineValue=box.componentOverrides.First(v=>v.ComponentTypeName==typeof(ContactTimelineTrigger).FullName && v.PropertyPath=="timeline");
                    Require(GlobalObjectId.GetGlobalObjectIdSlow(trigger.Timeline).ToString()==timelineValue.Value,
                        "real builder preserves the original Timeline resource");
                }
            }
            finally {Object.DestroyImmediate(root);}
        }
        private static void VerifyFragmentImport(LevelEditorPalette palette,PlanningBox start,PlanningBox gate)
        {
            Require(LevelEditorPaletteService.GetOrCreate()==palette,"fragment import uses the same shared palette");
            var source=new PlanningDocument {name="Start and Gate portable fragment"};
            var room=new PlanningRoom("portable fixture");room.cells.Add(new PlanningCell(0,0));room.cells.Add(new PlanningCell(1,0));
            var a=start.PersistentClone();a.x=0;a.y=0;
            var b=gate.PersistentClone();b.x=1;b.y=0;
            room.boxes.Add(a);room.boxes.Add(b);source.rooms.Add(room);
            PlanningFragmentUtility.ValidateDependencies(source);
            log.AppendLine("ValidateDependencies accepts both new template records (including original Timeline asset and built-in mesh).");
            var imported=PlanningFragmentUtility.Import(new PlanningDocument {name="destination fixture"},source,new Vector2Int(1000,1000),false,out _);
            PlanningBox importedStart=imported.rooms.SelectMany(r=>r.boxes).Single(v=>v.propEntryId==start.propEntryId);
            PlanningBox importedGate=imported.rooms.SelectMany(r=>r.boxes).Single(v=>v.propEntryId==gate.propEntryId);
            Require(importedStart.id!=start.id && importedGate.id!=gate.id,"real fragment import remaps IDs without losing either template");
            Require(importedGate.componentOverrides.Any(v=>v.ComponentTypeName==typeof(BossCollectibleEntryGate).FullName &&
                v.PropertyPath=="returnDestinationId" && v.Value=="Start"),"fragment import preserves same-world Start destination");
            VerifyGeneratedBox(palette,importedGate,new Vector2Int(1001,1000),null);
        }
        private static void Save(Scene scene,RoomLifecycleManager manager,PlanningDocument document,string named)
        {
            string json=document.ToPersistentJson(true);
            manager.Configure(new List<RoomRegion>(manager.Regions),manager.CellSize,json);EditorUtility.SetDirty(manager);
            PlanningSharedSaveUtility.ObserveSceneCopy(scene);EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene),"saved only patched scene "+scene.path);
            PlanningSharedSaveUtility.SaveSceneCopy(scene,document,json);
            if(named!=null)PlanningSaveStore.Save(named,document,json);
        }
        private static void EnsureFolder(){if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Boss_Logic","Authoring");}
        private static PlanningDocument Read(string p)=>PlanningDocument.FromPersistentJson(File.ReadAllText(p));
        private static T[] All<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private static RoomLifecycleManager Manager(Scene s){var a=All<RoomLifecycleManager>(s);Require(a.Length==1,"one planning manager in "+s.path);return a[0];}
        private static PlanningBox FindBox(PlanningDocument d,string id,out PlanningRoom owner)
        {foreach(var r in d.rooms){var b=r.boxes.SingleOrDefault(v=>v.id==id);if(b!=null){owner=r;return b;}}owner=null;return d.assemblyPatches.SingleOrDefault(b=>b.id==id);}
        private static Dictionary<string,string> SnapshotBoxes(PlanningDocument d)=>d.rooms.SelectMany(r=>r.boxes).Concat(d.assemblyPatches).ToDictionary(b=>b.id,b=>JsonUtility.ToJson(b));
        private static void VerifyBoxes(PlanningDocument d,Dictionary<string,string> old,string extra)
        {
            var current=SnapshotBoxes(d);Require(current.Count==old.Count+(extra==null?0:1),"only permitted planning box count delta");
            foreach(var pair in old)Require(current.TryGetValue(pair.Key,out string json)&&json==pair.Value,"original box retained "+pair.Key,false);
            log.AppendLine($"Verified {old.Count} original box records/IDs; only the permitted new template/fields differ.");
            if(extra!=null)Require(current.ContainsKey(extra),"only new planning record is "+extra);
        }
        private static string NonBoxFingerprint(PlanningDocument d)
        {var c=JsonUtility.FromJson<PlanningDocument>(JsonUtility.ToJson(d));foreach(var r in c.rooms)r.boxes.Clear();c.assemblyPatches.Clear();return JsonUtility.ToJson(c);}
        private sealed class Pose
        {public Transform t,parent;public Vector3 local,scale;public Quaternion rotation;public bool active;public string id;}
        private static Pose[] Capture(Scene s)=>All<Transform>(s).Select(t=>new Pose {t=t,parent=t.parent,local=t.localPosition,
            scale=t.localScale,rotation=t.localRotation,active=t.gameObject.activeSelf,id=t.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId}).ToArray();
        private static void VerifyPoses(Pose[] all)
        {foreach(var p in all)Require(p.t!=null&&p.t.parent==p.parent&&p.t.localPosition==p.local&&p.t.localRotation==p.rotation&&p.t.localScale==p.scale&&
            p.t.gameObject.activeSelf==p.active&&p.t.GetComponent<LevelEditorPlacedBlock>()?.PlanningBoxId==p.id,"original pose/ID unchanged "+(p.t!=null?p.t.name:"destroyed"),false);
            log.AppendLine($"Verified {all.Length} original transforms, parents, active flags, and placement IDs.");}
        private static void Require(bool valid,string message,bool logSuccess=true)
        {if(!valid)throw new InvalidOperationException("Authoring patch: "+message);if(logSuccess)log.AppendLine(message);}
    }
}
