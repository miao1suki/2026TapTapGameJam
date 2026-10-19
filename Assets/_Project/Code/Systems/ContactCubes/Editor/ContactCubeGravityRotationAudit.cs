using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.GridArt;
using Project.GridArt.Editor;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.ContactCubes.Editor
{
    /// <summary>Isolated, silent editor + real PlayMode physics regression.</summary>
    public static class ContactCubeGravityRotationAudit
    {
        private const string Flag = "ContactCubeGravityRotationAudit.Running";
        private const string Fixtures = "Assets/__ContactCubeGravityRotationAudit";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static readonly List<GameObject> roots = new List<GameObject>();
        private static readonly List<ContactCube> cubes = new List<ContactCube>();
        private static PlayerController player;
        private static int checks;
        private static double started;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Only run in the silent isolated FlowRetirementProbe.");
            report.Clear(); checks = 0;
            try
            {
                VerifyArtRotation();
                SessionState.SetInt(Flag + ".Checks", checks);
                SessionState.SetString(Flag + ".Report", report.ToString());
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Flag, true);
                Resume(); EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag,false)) return;
            checks = SessionState.GetInt(Flag + ".Checks",0);
            report.Clear(); report.Append(SessionState.GetString(Flag + ".Report",""));
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Wait; EditorApplication.update += Wait;
        }
        private static void Wait()
        {
            if (EditorApplication.timeSinceStartup-started > 100) { Finish(false,"Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            SimulationMode oldMode = Physics.simulationMode;
            bool passed = false;
            try
            {
                AudioListener.pause = true; AudioListener.volume = 0;
                Physics.simulationMode = SimulationMode.Script;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"),
                    new Vector3(1000,20,0),Quaternion.identity).GetComponent<PlayerController>();
                roots.Add(player.gameObject);
                player.GetComponent<PlayerInputDriver>().enabled = false;
                player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                VerifyAllColors(); VerifyGrowthClimbing();
                passed = true;
            }
            catch (Exception error) { report.AppendLine(error.ToString()); Debug.LogException(error); }
            finally
            {
                for (int i=roots.Count-1;i>=0;i--) if (roots[i]!=null) Object.DestroyImmediate(roots[i]);
                roots.Clear(); cubes.Clear(); player = null; Physics.simulationMode = oldMode;
            }
            Finish(passed,"");
        }
        private static void Finish(bool passed,string detail)
        {
            EditorApplication.update -= Wait;
            SessionState.EraseBool(Flag);
            string text = (passed ? "PASS " : "FAIL ") + checks + "\n" + report + detail;
            File.WriteAllText("Library/ContactCubeGravityRotationAudit.txt",text);
            Debug.Log(text); EditorApplication.Exit(passed ? 0 : 1);
        }
        private static void Check(bool condition,string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("GRAVITY / ROTATION: " + label);
            report.AppendLine(label);
        }

        private static void VerifyArtRotation()
        {
            if (AssetDatabase.IsValidFolder(Fixtures)) throw new InvalidOperationException("Audit fixture folder already exists; inspect it before rerunning.");
            AssetDatabase.CreateFolder("Assets","__ContactCubeGravityRotationAudit");
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene,Fixtures+"/Rotation.unity");
                var texture = new Texture2D(48,8) { filterMode = FilterMode.Point, name="Native48x8" };
                AssetDatabase.CreateAsset(texture,Fixtures+"/Texture.asset");
                var sprite = Sprite.Create(texture,new Rect(0,0,48,8),Vector2.one*.5f,16);
                sprite.name="Native48x8"; AssetDatabase.AddObjectToAsset(sprite,texture);
                var palette = ScriptableObject.CreateInstance<GridArtPalette>();
                var brush = new GridArtBrush { sprite=sprite,colorId="green" };
                Check(GridArtPixelLayout.RestoreNative(brush,out _),"rotation fixture uses native source size");
                palette.brushes.Add(brush); AssetDatabase.CreateAsset(palette,Fixtures+"/Palette.asset");
                var map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette=palette;
                map.layers.Add(new GridArtLayer()); AssetDatabase.CreateAsset(map,Fixtures+"/Map.asset");
                for (int turn=0;turn<4;turn++) map.layers[0].Set(new Vector2Int(turn*6,0),0,turn);
                var quads = GridArtBuilder.BuildQuads(map.layers[0],palette);
                Check(quads.Count==4,"all orthogonal turns produce one quad per stamp");
                for (int turn=0;turn<4;turn++)
                {
                    GridArtQuad q = quads[turn];
                    Vector2 native = GridArtRotation.Size(new Vector2(3,.5f),turn);
                    Check(q.bounds.size==native,"rectangular natural width/height swap " + turn);
                    Vector2 pixels = GridArtRotation.Size(new Vector2(48,8),turn);
                    Check(Mathf.Abs(q.bounds.width/pixels.x-1f/16)<.000001f &&
                        Mathf.Abs(q.bounds.height/pixels.y-1f/16)<.000001f,"square art pixels retained " + turn);
                    for (int corner=0;corner<4;corner++)
                        Check(q.UV(corner)==GridArtRotation.SampleUV(q.uv,corner,turn),"shared preview/mesh UV corner " + turn+"/"+corner);
                    var erase = new GridArtLayer(); erase.Set(Vector2Int.zero,0,turn);
                    Vector2Int footprint = GridArtRotation.Size(brush.Size,turn);
                    Check(erase.Erase(footprint-Vector2Int.one,palette) && erase.cells.Count==0,
                        "erase from far covered cell follows rotated footprint " + turn);
                }
                var output = GridArtBuilder.Apply(map,scene,false,true);
                Mesh mesh = output.GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(mesh.vertexCount==16,"scene mesh retains four stamps");
                Vector2[] uv = mesh.uv;
                for (int turn=0;turn<4;turn++) for (int corner=0;corner<4;corner++)
                    Check(uv[turn*4+corner]==quads[turn].UV(corner),"actual scene mesh matches rotated UV " + turn+"/"+corner);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(Fixtures+"/Map.asset",ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<GridArtMap>(Fixtures+"/Map.asset");
                for (int turn=0;turn<4;turn++) Check(reloaded.layers[0].cells[turn].quarterTurns==turn,"rotation saved and reloaded " + turn);
                map = reloaded;
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(map,"旋转笔画验证");
                Check(map.layers[0].Set(Vector2Int.zero,0,1),"same anchor can repaint with changed rotation");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(map.layers[0].cells[0].quarterTurns==0,"undo restores stamp rotation");
                Undo.PerformRedo(); Check(map.layers[0].cells[0].quarterTurns==1,"redo restores stamp rotation"); Undo.ClearUndo(map);
                Check(GridArtRotation.Normalize(-1)==3 && GridArtRotation.Normalize(4)==0,"turn normalization wraps without accumulating rotation error");
                string json=EditorJsonUtility.ToJson(map);
                var copy=ScriptableObject.CreateInstance<GridArtMap>(); EditorJsonUtility.FromJsonOverwrite(json,copy);
                Check(copy.layers[0].cells[0].quarterTurns==1,"independent map copy preserves rotation"); Object.DestroyImmediate(copy);
                var linked = new GridArtLayer { sourceBrush=0, sourceQuarterTurns=1 };
                var walls = new HashSet<Vector2Int> { Vector2Int.zero,Vector2Int.one };
                Check(GridArtLevelSource.Synchronize(linked,walls) && linked.cells[0].quarterTurns==1,"automatic white-block fill retains chosen rotation");
                Check(!GridArtLevelSource.Synchronize(linked,walls),"unchanged linked geometry does not rebuild rotated stamps");
                walls.Add(Vector2Int.up);
                Check(GridArtLevelSource.Synchronize(linked,walls) && linked.cells[2].quarterTurns==1,"new linked wall adopts stored orthogonal rotation");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.DeleteAsset(Fixtures);
            }
        }

        private static ContactCube Spawn(CubeColor color,Vector3 position,bool gravity,RigidbodyConstraints? constraints=null,bool kinematic=false)
        {
            var root = new GameObject("Gravity fixture " + color); roots.Add(root); root.SetActive(false);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Content/ContactCubes/{color}_ContactCube.prefab");
            var go=Object.Instantiate(prefab,position,Quaternion.identity,root.transform);
            var body=go.GetComponent<Rigidbody>(); body.isKinematic=kinematic;
            if (constraints.HasValue) body.constraints=constraints.Value;
            var motion=go.GetComponent<ContactCubeMotion>();
            var settings=new SerializedObject(motion); settings.FindProperty("useGravity").boolValue=gravity; settings.ApplyModifiedPropertiesWithoutUndo();
            var cube=go.GetComponent<ContactCube>();
            settings=new SerializedObject(cube); settings.FindProperty("maximumLength").intValue=4;
            settings.FindProperty("stepSeconds").floatValue=.05f; settings.ApplyModifiedPropertiesWithoutUndo();
            // The real component editor captures an isolated prefab root. The inactive
            // staging parent only delays Awake until authored settings have been applied;
            // retaining it would capture a child path and test against a different root.
            roots.Add(go); go.transform.SetParent(null,true);
            cubes.Add(cube); Physics.SyncTransforms(); return cube;
        }
        private static void Tick(Component target)
        {
            if (target==null || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) return;
            target.GetType().GetMethod("FixedUpdate",Hidden)?.Invoke(target,null);
        }
        private static void Step(int count)
        {
            for (int i=0;i<count;i++)
            {
                foreach (var cube in cubes)
                {
                    Tick(cube.GetComponent<ContactCubeMotion>()); Tick(cube); Tick(cube.GetComponent<HorizontalCubeVine>());
                }
                Physics.Simulate(Time.fixedDeltaTime); Physics.SyncTransforms();
            }
        }
        private static void MovePlayer(Vector3 position,Vector2 movement)
        {
            player.transform.position=position; player.Motor.position=position;
            player.Motor.linearVelocity=Vector3.zero; player.SetMoveInput(movement); Physics.SyncTransforms();
        }
        private static void VerifyAllColors()
        {
            int index=0;
            foreach (CubeColor color in new[] {CubeColor.Red,CubeColor.Green,CubeColor.Blue})
            {
                var cube=Spawn(color,new Vector3(100+index++*20,20,0),false);
                var motion=cube.GetComponent<ContactCubeMotion>(); var body=cube.GetComponent<Rigidbody>();
                Vector3 origin=body.position; Step(10);
                Check(!motion.UsesGravity && !body.useGravity && body.isKinematic,"per-instance gravity off floats " + color);
                Check(Vector3.Distance(origin,body.position)<.0001f,"floating position retained " + color);
                motion.BeginGreenHookPull(); motion.SetGreenHookVelocity(Vector3.right*2); Physics.Simulate(.02f); Physics.SyncTransforms();
                Check(!body.useGravity && body.position.x>origin.x,"gravity-off cube remains hook-movable " + color);
                motion.EndGreenHookPull(); Check(!body.useGravity && body.isKinematic,"hook release restores explicit no-gravity " + color);
                motion.SetCarrySuspended(true); Check(body.isKinematic,"carry suspends " + color);
                motion.SetCarrySuspended(false); Check(!body.useGravity && body.isKinematic,"placing retains per-instance gravity " + color);
                motion.ResetForRoom(); Check(!motion.UsesGravity && !body.useGravity,"reset retains no-gravity " + color);
                var properties=new SerializedObject(motion); var property=properties.FindProperty("useGravity");
                Check(LevelEditorComponentOverrideUtility.TryCapture(motion,property,out var captured) && captured.Value=="0","planning captures explicit gravity Boolean " + color);
                Check(string.IsNullOrEmpty(captured.ComponentPath),"planning captures gravity relative to isolated prefab root " + color);
                motion.ConfigureGravity(true); Step(10);
                Check(motion.UsesGravity && body.useGravity && !body.isKinematic && body.position.y<origin.y-.03f,"gravity-on cube falls naturally " + color);
                motion.BeginGreenHookPull(); motion.SetGreenHookVelocity(Vector3.left*2); Physics.Simulate(.02f);
                Check(body.useGravity,"horizontal green pull preserves gravity " + color);
                motion.EndGreenHookPull(); motion.ResetForRoom(); Check(body.useGravity,"gravity-on survives hook/reset " + color);
                var clone=Spawn(color,new Vector3(100+index++*20,20,0),true);
                Check(LevelEditorComponentOverrideUtility.FindComponent(clone.gameObject,captured)==clone.GetComponent<ContactCubeMotion>(),
                    "planning gravity path resolves to clone Motion " + color);
                LevelEditorComponentOverrideUtility.ApplyOverrides(clone.gameObject,new List<LevelEditorComponentValueOverride>{captured});
                Check(!clone.GetComponent<ContactCubeMotion>().UsesGravity,"planning applies per-instance gravity override " + color);
                var fixedCube=Spawn(color,new Vector3(100+index++*20,20,0),true,RigidbodyConstraints.FreezeAll);
                var fixedMotion=fixedCube.GetComponent<ContactCubeMotion>(); var fixedBody=fixedCube.GetComponent<Rigidbody>();
                Vector3 fixedPosition=fixedBody.position; Step(8);
                Check(fixedBody.constraints==RigidbodyConstraints.FreezeAll && Vector3.Distance(fixedBody.position,fixedPosition)<.0001f,"legacy authored FreezeAll retained " + color);
                fixedMotion.BeginGreenHookPull(); fixedMotion.SetGreenHookVelocity(Vector3.up*2); Physics.Simulate(.02f);
                Check(fixedBody.constraints==RigidbodyConstraints.FreezeAll && Vector3.Distance(fixedBody.position,fixedPosition)<.0001f,"hook cannot clear author locks " + color);
                fixedMotion.EndGreenHookPull(); fixedMotion.ResetForRoom();
                Check(fixedBody.constraints==RigidbodyConstraints.FreezeAll,"reset keeps legacy author locks " + color);
            }
        }
        private static void VerifyGrowthClimbing()
        {
            int index=0;
            foreach (bool legacyLock in new[] {false,true})
            foreach (Vector3 direction in new[] {Vector3.right,Vector3.left,Vector3.up,Vector3.down})
            {
                Vector3 origin=new Vector3(400+index++*20,30,0);
                var green=Spawn(CubeColor.Green,origin,legacyLock,legacyLock ? RigidbodyConstraints.FreezeAll : (RigidbodyConstraints?)null);
                var blue=Spawn(CubeColor.Blue,origin-direction,false,RigidbodyConstraints.FreezeAll);
                Step(20);
                Check(green.Length==4 && Vector3.Dot(green.GrowthDirection,direction)>.999f,"fixed blue grows floating green in correct direction " + legacyLock+"/"+direction);
                var vine=green.GetComponent<HorizontalCubeVine>(); var body=green.GetComponentInChildren<BoxCollider>();
                Check(green.GetComponent<ContactCubeMotion>().IsStableClimbAnchor,"extended fixed/no-gravity green is stable anchor");
                Bounds bounds=body.bounds;
                foreach (float y in new[] {origin.y,bounds.center.y})
                {
                    vine.ResetForRoom(); MovePlayer(new Vector3(bounds.min.x-.515f,y,0),Vector2.up); Tick(vine);
                    Check(player.IsClimbing,"mother and grown side accept real player climb " + direction+"/"+y);
                }
                if (Mathf.Abs(direction.x)>.99f)
                {
                    foreach (bool top in new[] {true,false})
                    {
                        vine.ResetForRoom(); MovePlayer(new Vector3(bounds.center.x,top ? bounds.max.y+.515f : bounds.min.y-.515f,0),Vector2.zero); Tick(vine);
                        Check(player.IsHorizontalClimbing,"grown horizontal top/bottom accepts real player " + top);
                    }
                }
                vine.ResetForRoom(); Check(!player.IsClimbing && !player.IsHorizontalClimbing,"reset releases all climb leases");
                MovePlayer(new Vector3(1000,20,0),Vector2.zero);
                green.ResetForRoom(); green.GetComponent<ContactCubeMotion>().ResetForRoom();
                Check(green.Length==1 && green.GetComponent<ContactCubeMotion>().UsesGravity==legacyLock,"length reset preserves authored gravity selection");
                blue.gameObject.SetActive(false); green.gameObject.SetActive(false);
            }
            var kinematic=Spawn(CubeColor.Green,new Vector3(700,30,0),true,null,true);
            Check(kinematic.GetComponent<ContactCubeMotion>().IsStableClimbAnchor,"legacy authored kinematic green is a stable climbing anchor");
        }
    }
}
