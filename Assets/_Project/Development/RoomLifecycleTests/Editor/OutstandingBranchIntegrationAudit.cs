using System;
using System.IO;
using System.Linq;
using System.Text;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.LevelEditor;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Read-only integration evidence; never saves or rebuilds a designer scene.</summary>
public static class OutstandingBranchIntegrationAudit
{
    static int checks;
    static readonly StringBuilder report = new StringBuilder();
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++; report.AppendLine("PASS " + message);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').EndsWith("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new InvalidOperationException("Silent isolated probe only");
        int code=0;
        try
        {
            const string scenePath="Assets/_Project/Scenes/Playing/Gaming.unity";
            var doc=PlanningDocument.FromPersistentJson(File.ReadAllText(Path.ChangeExtension(scenePath,"planning.json")));
            var formal=PlanningDocument.FromPersistentJson(File.ReadAllText("Assets/_Project/Content/LevelMaps/正式关卡.json"));
            Check(doc.ToPersistentJson()==formal.ToPersistentJson(),"formal and paired planning match");
            var baseline=PlanningDocument.FromPersistentJson(File.ReadAllText("Library/Map17Original.json"));
            var withoutStart=PlanningDocument.FromPersistentJson(doc.ToPersistentJson());
            int starts=0;
            foreach(var r in withoutStart.rooms) starts+=r.boxes.RemoveAll(b=>b.id=="box_gameplay_start_anchor");
            Check(starts==1,"exactly one invisible Start added");
            Check(withoutStart.ToPersistentJson()==baseline.ToPersistentJson(),"all MVMap17 authored objects, bindings and layout metadata unchanged");
            Check(doc.rooms.Count==20,"20 map rooms preserved");
            var scene=EditorSceneManager.OpenScene(scenePath);
            var roots=scene.GetRootGameObjects();
            var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var placed=roots.SelectMany(r=>r.GetComponentsInChildren<LevelEditorPlacedBlock>(true)).ToArray();
            var boxes=doc.rooms.SelectMany(r=>r.boxes).Concat(doc.assemblyPatches).ToArray();
            Check(boxes.Length==1407 && placed.Length==boxes.Length,"1406 map objects plus Start, one scene instance per record");
            Check(boxes.Select(b=>b.id).Distinct().Count()==boxes.Length,"unique author IDs");
            foreach(var b in boxes) Check(placed.Count(p=>p.PlanningBoxId==b.id)==1,"instance "+b.id);
            foreach(var t in all) Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0 &&
                !PrefabUtility.IsPrefabAssetMissing(t.gameObject),"scene dependency "+t.name);
            var manager=roots.SelectMany(r=>r.GetComponentsInChildren<RoomLifecycleManager>(true)).Single();
            Check(PlanningDocument.FromPersistentJson(manager.PlanningJson).ToPersistentJson()==doc.ToPersistentJson(),"embedded planning matches paired map");
            var player=roots.SelectMany(r=>r.GetComponentsInChildren<PlayerController>(true)).Single();
            var start=placed.Single(p=>p.PlanningBoxId=="box_gameplay_start_anchor");
            var anchor=start.GetComponentInChildren<TeleportAnchorFeature>(true);
            Check(anchor!=null && anchor.TeleportId=="Start" && Vector3.Distance(anchor.transform.position,player.transform.position)<.0001f,"Start exact new-map player spawn");
            Check(start.GetComponentsInChildren<Renderer>(true).Length==0 && start.GetComponentsInChildren<Collider>(true).Length==0,"Start is invisible and nonblocking");
            Check(roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Count(c=>c.enabled)==1,"one active world camera");
            Check(EditorBuildSettings.scenes.Count(s=>s.enabled)==1 && EditorBuildSettings.scenes.Single(s=>s.enabled).path==scenePath,"single Gaming build entry retained");
            foreach(string path in new[]{"Assets/ta_source/Rain/RainPreview.unity","Assets/ta_source/firefly/FireflyPreview.unity","Assets/ta_source/flowmagma/StaticTest/FlowMagmaPreview.unity"})
            {
                var preview=EditorSceneManager.OpenScene(path);
                foreach(var r in preview.GetRootGameObjects()) foreach(var t in r.GetComponentsInChildren<Transform>(true))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0 && !PrefabUtility.IsPrefabAssetMissing(t.gameObject),"optional TA preview dependency "+t.name);
            }
            R3LevelBuilder.FinalAudit();
            report.AppendLine("PASS R3 source, scene, no-op apply, attachment remapping and reference audits");
        }
        catch(Exception e) {code=1; report.AppendLine(e.ToString()); Debug.LogException(e);}
        File.WriteAllText("Library/OutstandingBranchIntegrationAudit.txt",$"Checks: {checks}\n"+report);
        EditorApplication.Exit(code);
    }
}
