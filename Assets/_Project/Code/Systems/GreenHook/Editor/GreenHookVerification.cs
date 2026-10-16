using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.Items;
using Project.Player;
using Project.WorldItems;
using Project.WorldItems.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using PlanningEditorPrototype;
using Project.LevelEditor;
using Project.LevelEditor.Editor;

namespace Project.GreenHook.Editor
{
    /// <summary>Isolated, real rigidbody regression; never opens or rewrites an author's scene.</summary>
    public static class GreenHookVerification
    {
        private static int checks;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Check(bool success, string label)
        {
            checks++; if (!success) throw new Exception(label); Debug.Log("HOOK PASS " + label);
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            WorldItemSetup.CreateMissing();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool("GreenHookVerification", true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("GreenHookVerification", false)) return;
            EditorApplication.update -= Wait; EditorApplication.update += Wait;
        }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            EditorApplication.update -= Wait;
            try { RunCases(); Finish(true, "PASS " + checks); }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
        }
        private static GameObject Instantiate(string path, Vector3 position) =>
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
        private static GameObject Solid(string name, Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.position = position; obj.transform.localScale = size;
            return obj;
        }
        private static PlayerController Player(Vector3 position)
        {
            var player = Instantiate("Assets/_Project/Content/Player/Player.prefab", position).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.interpolation = RigidbodyInterpolation.None;
            return player;
        }
        private static GreenHookAbility Attach(PlayerController player, IGreenHookTarget target)
        {
            var hook = player.GetComponent<GreenHookAbility>();
            Set(hook, "hookDirection", Vector2.right); Set(hook, "hookLength", 6f);
            Invoke(hook, "BeginHook", target, target.GreenHookTransform.position);
            Invoke(hook, "BeginPulling"); Physics.SyncTransforms(); return hook;
        }
        private static void Step(GreenHookAbility hook, PlayerController player, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Invoke(hook, "FixedUpdate"); Invoke(player, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime);
            }
        }
        private static void RunCases()
        {
            var oldSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            try
            {
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                for (int variant = 0; variant < 3; variant++)
                {
                    Vector3 origin = new Vector3(variant * 30, 20, 0);
                    var player = Player(origin);
                    var cube = Instantiate("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab", origin + Vector3.right * 5).GetComponent<ContactCubeMotion>();
                    cube.GetComponent<Rigidbody>().interpolation = variant == 1 ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                    var floor = Solid("Floor", origin + new Vector3(2,-1.1f,0), new Vector3(20,1,2));
                    Physics.SyncTransforms();
                    var hook = Attach(player, cube); Vector3 start = cube.GreenHookPosition;
                    Step(hook, player, 7);
                    Check(cube.GreenHookPosition.x < start.x - .5f && !hook.IsPullingPlayer, "target first " + variant);
                    Check(Mathf.Abs(player.Motor.position.x - origin.x) < .03f, "player held during target motion");
                    var wall = Solid("Wall", new Vector3(cube.GreenHookPosition.x-.7f,origin.y,0), new Vector3(.4f,4,2));
                    Physics.SyncTransforms(); Step(hook,player,7);
                    Check(hook.IsPullingPlayer && player.Motor.position.x > origin.x + .25f, "blocked target switches to player");
                    Object.DestroyImmediate(wall); Physics.SyncTransforms();
                    start = cube.GreenHookPosition; Step(hook,player,4);
                    Check(!hook.IsPullingPlayer && cube.GreenHookPosition.x < start.x-.2f, "target priority re-evaluated");
                    Step(hook,player,650);
                    Check(hook.IsHookActive && player.IsGreenHookActive, "contact and long duration do not detach");
                    hook.DriveInput(Vector2.zero,true);
                    Check(!hook.IsHookActive && !player.IsGreenHookActive && !player.IsControlLocked, "manual release restores control");
                    Object.DestroyImmediate(cube.gameObject); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(floor);
                }
                {
                    var player = Player(new Vector3(100,20,0));
                    var cube = Instantiate("Assets/_Project/Content/ContactCubes/Green_ContactCube.prefab", new Vector3(103,22,0)).GetComponent<ContactCubeMotion>();
                    cube.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                    var hook = Attach(player,cube); Vector3 anchorStart = cube.GreenHookPosition;
                    Check(hook.IsSwinging, "green cube is swing anchor");
                    hook.DriveInput(Vector2.right,false); Step(hook,player,650);
                    Check(hook.IsSwinging && player.IsGreenHookActive, "swing never times out");
                    Check(Vector3.Distance(anchorStart,cube.GreenHookPosition)<.001f, "swing does not move anchor");
                    Check(Vector2.Distance(player.Motor.position,anchorStart) <= 3.7f, "rope length constrained");
                    var externalLock=player.AcquireControlLock(new object()); Step(hook,player,1);
                    Check(player.Motor.linearVelocity.sqrMagnitude < .001f && !player.CanDriveGreenHook, "additional control lock pauses hook motion");
                    externalLock.Dispose(); player.UpdateGreenHookInput(Vector2.zero,true);
                    Check(!hook.IsHookActive && !player.IsControlLocked,"player API releases swing");
                    Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(cube.gameObject);
                }
                VerifyItems();
                VerifyBaseAndPush();
                VerifyRoomBoundary();
            }
            finally { Physics.simulationMode = oldSimulation; }
        }
        private static void VerifyBaseAndPush()
        {
            var player = Player(new Vector3(300,20,0));
            var foundation = Instantiate("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab",new Vector3(305,20,0));
            var feature=foundation.GetComponent<MechanismBaseFeature>();
            Check(feature != null && feature.GreenHookMotionMode == GreenHookMotionMode.PullTarget,"base contract target-first");
            var motion=foundation.GetComponent<GreenHookBodyMotion>();
            Check(motion != null && foundation.GetComponent<Rigidbody>().isKinematic,"existing base gains hook motion and remains immovable at rest");
            var hook=Attach(player,feature); Step(hook,player,5);
            Check(foundation.GetComponent<Rigidbody>().position.x < 304.5f && motion.IsHookMoving,"base actually pulled");
            player.UpdateGreenHookInput(Vector2.zero,true);
            Check(foundation.GetComponent<Rigidbody>().isKinematic && !motion.IsHookMoving,"base releases physics ownership");
            Object.DestroyImmediate(foundation); Object.DestroyImmediate(player.gameObject);
            for(int index=0;index<4;index++)
            {
                Vector3 start=new Vector3(330+index*15,20,0);
                var floor=Solid("PushFloor",start+new Vector3(2,-1.0f,0),new Vector3(20,1,2));
                GameObject mover=index==3 ? Instantiate("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab",start) :
                    Instantiate("Assets/_Project/Content/ContactCubes/"+(CubeColor)index+"_ContactCube.prefab",start);
                var item=Instantiate(WorldItemSetup.Folder+"/Collectible.prefab",start+Vector3.right*.9f);
                var move=mover.GetComponent<IGreenHookMotion>(); move.BeginGreenHookPull(); Physics.SyncTransforms();
                for(int step=0;step<30;step++) { move.SetGreenHookVelocity(Vector3.right*3); Physics.Simulate(.02f); }
                Check(item.GetComponent<Rigidbody>().position.x>start.x+1.5f,"physical push pickup "+index);
                move.EndGreenHookPull(false,Vector3.zero,0);
                Object.DestroyImmediate(mover); Object.DestroyImmediate(item); Object.DestroyImmediate(floor);
            }
            var palette=AssetDatabase.LoadAssetAtPath<Project.LevelEditor.LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            var props=palette.PropEntries.Where(p=>p.Prefab!=null && AssetDatabase.GetAssetPath(p.Prefab).StartsWith(WorldItemSetup.Folder)).ToArray();
            Check(props.Length==5 && props.Select(p=>p.EntryId).Distinct().Count()==5,"five editor props with persistent unique IDs");
        }
        private static void VerifyRoomBoundary()
        {
            var player=Player(new Vector3(400.5f,20.5f,0));
            var cube=Instantiate("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab",new Vector3(405.5f,20.5f,0)).GetComponent<ContactCubeMotion>();
            var obj=new GameObject("RoomManager"); var manager=obj.AddComponent<Project.Rooms.RoomLifecycleManager>();
            var cells=new List<Vector2Int>();
            for(int x=399;x<410;x++) for(int y=-24;y<-18;y++) cells.Add(new Vector2Int(x,y));
            manager.Configure(new List<Project.Rooms.RoomRegion>{new Project.Rooms.RoomRegion{id="room",cells=cells}},1f,"");
            var hook=Attach(player,cube); Step(hook,player,2); Check(hook.IsHookActive,"hook allowed within room");
            player.Motor.position=new Vector3(430,20,0); Physics.SyncTransforms(); Step(hook,player,1);
            Check(!hook.IsHookActive && !player.IsGreenHookActive,"room exit cancels and releases ownership");
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(cube.gameObject); Object.DestroyImmediate(obj);
        }
        private static void VerifyItems()
        {
            var player = Player(new Vector3(200,20,0));
            var obj = Instantiate(WorldItemSetup.Folder + "/Collectible.prefab", new Vector3(205,20,0));
            var item = obj.GetComponent<CollectiblePickup>(); var body = obj.GetComponent<Rigidbody>();
            Check(item is DropItemBase && item.CanBePushed,"pushable shared pickup lifecycle");
            body.position += Vector3.right; item.GetComponent<Project.Rooms.RoomResetObject>().RestorePose(); item.ResetForRoom();
            Check(Mathf.Abs(body.position.x-205)<.001f && item.CanCollect,"collectible reset returns to author position");
            int collected=0; Action<CollectiblePickup,GameObject> listener=(pickup,actor)=>collected++;
            CollectiblePickup.Collected += listener;
            Invoke(item,"OnTriggerEnter",player.GetComponent<Collider>());
            Check(item.CollectionRequested && !item.CollectionFinished && obj.activeSelf,"collection waits for flight instead of early hide");
            item.ResetForRoom();
            Check(item.CanCollect && collected==0 && !obj.GetComponent<Project.Pickups.PickupFlightAnimator>().IsPlaying,"reset cancels pending pickup and reward");
            CollectiblePickup.Collected -= listener;
            Object.DestroyImmediate(obj);
            foreach (string direction in new[]{"上","下","左","右"})
            {
                var spike=Instantiate(WorldItemSetup.Folder+"/Spike_"+direction+".prefab",new Vector3(210,20,0));
                Check(spike.GetComponentInChildren<MeshCollider>().isTrigger && spike.GetComponent<ColorObject>()==null,"neutral spike trigger "+direction);
                Check(spike.GetComponentInChildren<MeshCollider>().bounds.size.x > 0,"spike oriented geometry "+direction);
                Object.DestroyImmediate(spike);
            }
            var hazard = new GameObject("hazard").AddComponent<NeutralSpike>();
            Set(player,"invulnerableUntil",Time.time+100);
            Invoke(hazard,"OnTriggerEnter",player.GetComponent<Collider>());
            Check(!player.IsDead,"spikes respect revival protection");
            Set(player,"invulnerableUntil",0f);
            Invoke(hazard,"OnTriggerEnter",player.GetComponent<Collider>());
            Check(player.IsDead,"spike touch enters normal death flow");
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(hazard.gameObject);
        }
        private static void Finish(bool success,string result)
        {
            File.WriteAllText(Path.Combine(Application.dataPath,"../Library/GreenHookVerification.txt"),result);
            SessionState.SetBool("GreenHookVerification",false); EditorApplication.Exit(success?0:1);
        }

        public static void RunEditor()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                WorldItemSetup.CreateMissing();
                var palette=LevelEditorPaletteService.GetOrCreate();
                var ids=palette.PropEntries.Select(entry=>entry.EntryId).ToArray();
                WorldItemSetup.CreateMissing();
                Check(ids.SequenceEqual(palette.PropEntries.Select(entry=>entry.EntryId)),"setup is idempotent and preserves existing IDs");
                var props=palette.PropEntries.Where(entry=>entry.Prefab!=null && AssetDatabase.GetAssetPath(entry.Prefab).StartsWith(WorldItemSetup.Folder)).ToArray();
                var doc=new PlanningDocument {name="WorldItemsProbe"}; var room=new PlanningRoom("Items"); doc.rooms.Add(room);
                for(int i=0;i<5;i++)
                {
                    room.cells.Add(new PlanningCell(i,0));
                    room.boxes.Add(new PlanningBox {id="prop"+i,type=PlanningDetailType.Prop,propEntryId=props[i].EntryId,propEntryName=props[i].DisplayName,x=i,y=0});
                    var editor=UnityEditor.Editor.CreateEditor(props[i].Prefab.GetComponent<CollectiblePickup>() as Component ?? props[i].Prefab.GetComponent<NeutralSpike>());
                    Check(editor.CreateInspectorGUI()!=null,"UI Toolkit inspector "+props[i].DisplayName); Object.DestroyImmediate(editor);
                }
                var water=palette.PropEntries.First(entry=>entry.Prefab!=null && entry.Prefab.GetComponent<WaterVisualFeature>()!=null);
                room.boxes.Add(new PlanningBox{id="water",type=PlanningDetailType.Prop,propEntryId=water.EntryId,propEntryName=water.DisplayName,x=0,y=0,width=5,height=1,isMerged=true});
                PlanningSceneBuilder.SuppressSceneUndo=true;
                Check(PlanningSceneBuilder.TryApplyDocument(doc,out var message),"new props apply in water: "+message);
                var root=GameObject.Find("__PlanningMapGenerated");
                Check(root.GetComponentsInChildren<CollectiblePickup>(true).Length==1 && root.GetComponentsInChildren<NeutralSpike>(true).Length==4,"all five props instantiated");
                var old=root.GetComponentsInChildren<LevelEditorPlacedBlock>(true).ToDictionary(item=>item.PlanningBoxId,item=>item.GetInstanceID());
                Check(PlanningSceneBuilder.TryApplyDocument(doc,out _) && root.GetComponentsInChildren<LevelEditorPlacedBlock>(true).All(item=>old[item.PlanningBoxId]==item.GetInstanceID()),"incremental sync retains prop identities");
                PlanningSaveStore.Save("Library/WorldItemsProbe.planning.json",doc);
                var saved=PlanningSaveStore.Read("Library/WorldItemsProbe.planning.json");
                Check(saved.ToPersistentJson()==doc.ToPersistentJson(),"new props save and reload");
                var fragment=PlanningFragmentUtility.Extract(doc,new[]{room.id},null,out _);
                var imported=PlanningFragmentUtility.Import(new PlanningDocument(),fragment,new Vector2Int(10,0),false,out _);
                Check(imported.rooms[0].boxes.Count==6 && imported.rooms[0].boxes.Any(box=>box.propEntryId==props[0].EntryId),"fragment import retains all props and water");
                PlanningSceneBuilder.SuppressSceneUndo=false;
                Finish(true,"EDITOR PASS "+checks);
            }
            catch(Exception error) { Debug.LogException(error); Finish(false,error.ToString()); }
        }
    }
}
