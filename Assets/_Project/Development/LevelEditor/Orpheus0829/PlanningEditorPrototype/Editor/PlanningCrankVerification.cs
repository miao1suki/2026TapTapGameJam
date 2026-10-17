using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanningEditorPrototype
{
    internal static class PlanningCrankVerification
    {
        private static int checks;
        private static void Check(bool value,string label) { checks++; if (!value) throw new Exception(label); }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("隔离验证入口");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
                var crankEntry = palette.PropEntries.First(entry => entry.Prefab != null && entry.Prefab.GetComponent<CrankPlatformFeature>() != null);
                var crankBox = new PlanningBox(PlanningDetailType.Prop,new RectInt(8,8,1,1))
                    { id="crank-stable",propEntryId=crankEntry.EntryId,propEntryName=crankEntry.DisplayName };
                var document = new PlanningDocument(); var room = new PlanningRoom("Test");
                for(int x=0;x<16;x++) for(int y=0;y<16;y++) room.cells.Add(new PlanningCell(x,y));
                document.rooms.Add(room); room.boxes.Add(crankBox);
                var canvas = new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(document);
                Check(room.boxes.Count == 2,"first creation adds one separate platform");
                var auto = room.boxes.First(box => box.crankOwnerId == crankBox.id);
                Check(auto.id != crankBox.id && auto.propEntryId == PlanningMechanismUtility.LiftEntryId,"platform has distinct persistent ID and entry");
                Check(crankBox.singleInstance && crankBox.initialCrankPlatformCreated,"crank single instance and creation recorded");
                canvas.SetDocument(document); Check(room.boxes.Count == 2,"opening document twice does not duplicate platform");
                room.boxes.Remove(auto); canvas.SetDocument(document); Check(room.boxes.Count == 1,"deleting platform does not regenerate it");
                var first = new PlanningBox(PlanningDetailType.Prop,new RectInt(0,0,1,1))
                    { id="lift-a",paletteEntryId="",paletteEntryName="",propEntryId=PlanningMechanismUtility.LiftEntryId,propEntryName="曲柄升降平台",crankOwnerId=crankBox.id };
                var second = first.Clone(); second.id="lift-b"; second.x=1;
                room.boxes.Add(first); room.boxes.Add(second);
                Check(PlanningMechanismUtility.CanMerge(new[] { first,second }),"same controller merge permitted");
                second.crankOwnerId="other-crank";
                Check(!PlanningMechanismUtility.CanMerge(new[] { first,second }),"different controller merge refused"); second.crankOwnerId=crankBox.id;
                var selection=(HashSet<string>)typeof(PlanningCanvas).GetField("mergeSelection",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(canvas);
                selection.Add(first.id);selection.Add(second.id); canvas.MergeSelectedItems();
                var merged=room.boxes.First(box => box.isMerged);
                Check(merged.width==2 && merged.height==1 && room.boxes.Count==2,"actual canvas merges into one rectangle");
                Check(merged.crankOwnerId==crankBox.id && merged.id!=first.id && merged.id!=second.id,"merged persistent owner survives new ID");
                merged=PlanningBox.FromPersistentJson(JsonUtility.ToJson(merged.PersistentClone()));
                Check(merged.crankOwnerId==crankBox.id && merged.mergeParts.Count==2,"merged JSON survives reload");
                var root=new GameObject("GeneratedTest").transform;
                var crank=Spawn(root,crankEntry.Prefab,crankBox.id).GetComponent<CrankPlatformFeature>();
                Check(crank.GetComponentsInChildren<MovingPlatformFeature>(true).Length==0,"crank contains artwork but no platform child");
                var lift=Spawn(root,AssetDatabase.LoadAssetAtPath<GameObject>(PlanningMechanismUtility.LiftPath),merged.id).GetComponent<MovingPlatformFeature>();
                lift.transform.localScale=new Vector3(2,1,1);
                PlanningMechanismUtility.BindScene(root,document.rooms,document.assemblyPatches);
                Check(lift.CrankController==crank && lift.IsCrankControlled,"merged scene object binds correct controller");
                Object.DestroyImmediate(lift.gameObject);
                lift=Spawn(root,AssetDatabase.LoadAssetAtPath<GameObject>(PlanningMechanismUtility.LiftPath),merged.id).GetComponent<MovingPlatformFeature>();
                PlanningMechanismUtility.BindScene(root,document.rooms,document.assemblyPatches);
                Check(lift.CrankController==crank,"incremental replacement binds new object not destroyed reference");
                canvas.UnmergeSelectedItems();
                var restored=room.boxes.Where(box=>box.crankOwnerId==crankBox.id).ToArray();
                Check(restored.Length==2 && restored.All(box=>box.crankOwnerId==crankBox.id),"unmerge preserves controller on both platforms");
                Object.DestroyImmediate(lift.gameObject);
                foreach(var box in restored) Spawn(root,AssetDatabase.LoadAssetAtPath<GameObject>(PlanningMechanismUtility.LiftPath),box.id);
                PlanningMechanismUtility.BindScene(root,document.rooms,document.assemblyPatches);
                Check(root.GetComponentsInChildren<MovingPlatformFeature>(true).All(platform=>platform.CrankController==crank),"unmerged scene platforms relink independently");
                room.boxes.Remove(crankBox); Object.DestroyImmediate(crank.gameObject);
                PlanningMechanismUtility.BindScene(root,document.rooms,document.assemblyPatches);
                Check(root.GetComponentsInChildren<MovingPlatformFeature>(true).All(platform=>platform.CrankController==null),"deleted controller clears stale scene references");
                // Source IDs are also rewritten when ordinary base cells merge/unmerge.
                var baseA=new PlanningBox { id="base-a",x=5,y=6,width=1,height=1 };
                var baseB=new PlanningBox { id="base-b",x=6,y=6,width=1,height=1 };
                var baseMerged=new PlanningBox { id="base-merged",x=5,y=6,width=2,height=1 };
                var consumer=new PlanningBox { mechanismBaseId=baseB.id };
                PlanningMechanismUtility.RemapMergedSources(new[] { baseA,baseB },baseMerged,new[] { consumer });
                Check(consumer.mechanismBaseId==baseMerged.id && consumer.mechanismBaseCellOffset==Vector2Int.right,"base merge redirects exact bound cell");
                PlanningMechanismUtility.RemapCarvedSource(baseMerged,new[] { baseA,baseB },new[] { consumer });
                Check(consumer.mechanismBaseId==baseB.id && consumer.mechanismBaseCellOffset==Vector2Int.zero,"base unmerge restores correct source cell");
                Object.DestroyImmediate(root.gameObject);
                room.boxes.Clear(); room.boxes.Add(crankBox); room.boxes.Add(merged);
                Check(PlanningSceneBuilder.TryApplyDocument(document,out var buildMessage),"actual incremental scene build: "+buildMessage);
                var generated=GameObject.Find("__PlanningMapGenerated");
                var built=generated.GetComponentsInChildren<MovingPlatformFeature>(true);
                var builtCrank=generated.GetComponentInChildren<CrankPlatformFeature>(true);
                Check(built.Length==1 && built[0].CrankController==builtCrank,"actual merged build creates one controller consumer");
                Physics.SyncTransforms();
                Check(Mathf.Abs(built[0].GetComponent<Collider>().bounds.size.x-2)<.001f,"actual merged model/collider spans two cells: "+built[0].GetComponent<Collider>().bounds+" scale="+built[0].transform.lossyScale+" active="+built[0].gameObject.activeInHierarchy);
                var unchanged=built[0];
                Check(PlanningSceneBuilder.TryApplyDocument(document,out buildMessage),"no-change incremental apply");
                Check(generated.GetComponentInChildren<MovingPlatformFeature>(true)==unchanged,"unchanged platform instance retained");
                merged.width=3;
                Check(PlanningSceneBuilder.TryApplyDocument(document,out buildMessage),"changed merged platform incremental replacement");
                var replaced=generated.GetComponentInChildren<MovingPlatformFeature>(true);
                Check(replaced!=unchanged && replaced.CrankController==builtCrank,"changed merged platform binds live controller after rebuild");
                const string scenePath="Assets/_Project/Development/LevelEditor/Orpheus0829/PlanningEditorPrototype/Editor/CrankBindingProbe.unity";
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scenePath);
                EditorSceneManager.OpenScene(scenePath);
                generated=GameObject.Find("__PlanningMapGenerated");
                replaced=generated.GetComponentInChildren<MovingPlatformFeature>(true);
                Check(replaced.CrankController==generated.GetComponentInChildren<CrankPlatformFeature>(true),"saved scene reload retains serialized binding without editor rebuild");
                var authored=(GameObject)PrefabUtility.InstantiatePrefab(crankEntry.Prefab);
                var authoredCrank=authored.GetComponent<CrankPlatformFeature>();
                var added=Project.BlockFeatures.Editor.CrankPlatformAuthoring.AddPlatform(authoredCrank);
                Check(added!=null && added.transform.parent==authoredCrank.transform.parent && added.CrankController==authoredCrank,"Scene authoring creates bound sibling not child");
                var extra=Project.BlockFeatures.Editor.CrankPlatformAuthoring.AddPlatform(authoredCrank);
                Check(extra!=null && extra!=added && extra.CrankController==authoredCrank && authoredCrank.InitialPlatformCreated,"Scene authoring supports multiple platforms and stores creation marker");
                File.WriteAllText("Library/PlanningCrankVerification.txt","PASS "+checks); EditorApplication.Exit(0);
            }
            catch(Exception error)
            { File.WriteAllText("Library/PlanningCrankVerification.txt",error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static GameObject Spawn(Transform root,GameObject prefab,string id)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab); go.transform.SetParent(root,false);
            (go.GetComponent<LevelEditorPlacedBlock>() ?? go.AddComponent<LevelEditorPlacedBlock>()).SetPlanningSource(id,"",Vector2Int.zero);
            return go;
        }
    }
}
