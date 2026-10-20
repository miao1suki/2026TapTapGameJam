using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.GridArt;
using Project.GridArt.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public sealed class EditorFlowPerformanceAudit : EditorWindow
{
    const BindingFlags P = BindingFlags.NonPublic | BindingFlags.Instance;
    static EditorFlowPerformanceAudit self;
    PlanningCanvas planning;
    GridArtWindow art;
    VisualElement canvas;
    SceneView scene;
    int phase, frame, profilerStart;
    double last, deadline;
    double drawMs;
    PlanningEditorWindow fullPlanning;
    readonly List<double> intervals = new(), updates = new();
    static readonly string[] Names = { "SceneNear", "SceneFar", "PlanningAssembly", "PlanningDetail", "ArtFar", "ArtNear", "SceneWithPlanning" };
    static void Set(object o,string f,object v) => o.GetType().GetField(f,P).SetValue(o,v);
    static object Get(object o,string f) => o.GetType().GetField(f,P).GetValue(o);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Disposable probe project only.");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
        self=CreateInstance<EditorFlowPerformanceAudit>(); self.titleContent=new GUIContent("编辑器性能验证");
        self.position=new Rect(0,0,1500,880); self.Show();
        ProfilerDriver.profileEditor=true; UnityEngine.Profiling.Profiler.enabled=true;
        ProfilerDriver.enabled=true; ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU,true);
        self.deadline=EditorApplication.timeSinceStartup+240; self.Setup(); EditorApplication.update+=self.Tick;
    }
    void Setup()
    {
        frame=0; drawMs=0; intervals.Clear(); updates.Clear(); rootVisualElement.Clear();
        if(phase<2 || phase==6)
        {
            scene=GetWindow<SceneView>(); scene.position=new Rect(0,0,1500,880); scene.Show(); scene.Focus();
            scene.in2DMode=true; scene.pivot=new Vector3(-70,10,0); scene.size=phase==0?18:180;
            if(phase==6){fullPlanning=CreateInstance<PlanningEditorWindow>();fullPlanning.Show();Set(fullPlanning,"autoSync",false);scene.Focus();}
        }
        else if(phase<4)
        {
            Focus();
            var doc=JsonUtility.FromJson<PlanningDocument>(File.ReadAllText("Assets/_Project/Scenes/Playing/Gaming.planning.json"));
            planning=new PlanningCanvas(); planning.SetDocument(doc);
            planning.SetPalette(Project.LevelEditor.Editor.LevelEditorPaletteService.GetOrCreate());
            planning.SetMode(phase==2?PlanningCanvasMode.Assembly:PlanningCanvasMode.Detail);
            planning.SelectRoom(doc.rooms.OrderByDescending(x=>x.boxes.Count).First().id);
            canvas=planning; rootVisualElement.Add(canvas);
        }
        else
        {
            Focus(); planning=null; art=CreateInstance<GridArtWindow>();
            Set(art,"map",AssetDatabase.LoadAssetAtPath<GridArtMap>("Assets/_Project/Scenes/Playing/Gaming_Art.asset"));
            Set(art,"autoSave",false); Set(art,"autoPreview",false);
            Set(art,"levelSource",GridArtLevelSource.Read?.Invoke(UnityEngine.SceneManagement.SceneManager.GetActiveScene()));
            var t=typeof(GridArtWindow).GetNestedType("ArtCanvas",BindingFlags.NonPublic);
            canvas=(VisualElement)Activator.CreateInstance(t,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{art},null);
            canvas.style.flexGrow=1; rootVisualElement.Add(canvas);
            t.GetMethod("UpdateReference").Invoke(canvas,null);
        }
        if(canvas!=null && phase>=2 && phase<=5)
        {
            var target=canvas.Q("art-overlay")??canvas;
            var draw=(Action<MeshGenerationContext>)Delegate.CreateDelegate(typeof(Action<MeshGenerationContext>),canvas,canvas.GetType().GetMethod("Draw",P));
            target.generateVisualContent-=draw;
            target.generateVisualContent+=c=>{var sw=Stopwatch.StartNew();draw(c);sw.Stop();if(frame>12)drawMs+=sw.Elapsed.TotalMilliseconds;};
        }
        profilerStart=ProfilerDriver.lastFrameIndex; last=EditorApplication.timeSinceStartup;
    }
    void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now>deadline) throw new TimeoutException(Names[phase]);
            if(frame>12) intervals.Add((now-last)*1000); last=now;
            if(++frame>72)
            {
                Report(); phase++;
                if(art!=null){DestroyImmediate(art);art=null;}
                if(phase==Names.Length){UnityEngine.Profiling.Profiler.enabled=false;Debug.Log("EDITOR_FLOW_PERF COMPLETE");EditorApplication.Exit(0);return;}
                Setup(); return;
            }
            var sw=Stopwatch.StartNew();
            if(phase<2 || phase==6){scene.pivot=new Vector3(-70+frame*.1f,10,0);scene.Repaint();}
            else if(phase<4) planning.RestoreView(new Vector2(700+frame*2,200),phase==2?.2f:1f);
            else {Set(canvas,"zoom",phase==4?5f:32f);Set(canvas,"pan",new Vector2(700+frame*2,250));canvas.GetType().GetMethod("RepaintView").Invoke(canvas,null);}
            sw.Stop(); if(frame>12)updates.Add(sw.Elapsed.TotalMilliseconds); Repaint();
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
    }
    void Report()
    {
        intervals.Sort();updates.Sort();
        Debug.Log($"EDITOR_FLOW_PERF {Names[phase]} medianInterval={intervals[intervals.Count/2]:F2} p95Interval={intervals[(int)(intervals.Count*.95)]:F2} medianUpdate={updates[updates.Count/2]:F2} drawSum={drawMs:F2} nodes={canvas?.Query<VisualElement>().ToList().Count} profiler={profilerStart}/{ProfilerDriver.lastFrameIndex}");
        var times=new Dictionary<string,(double time,int count)>();
        for(int f=Math.Max(profilerStart+12,ProfilerDriver.firstFrameIndex);f<ProfilerDriver.lastFrameIndex;f++)
        {
            using var data=ProfilerDriver.GetRawFrameDataView(f,0);
            if(!data.valid)continue;
            for(int i=0;i<data.sampleCount;i++)
            {
                string name=data.GetSampleName(i);
                if(!times.TryGetValue(name,out var v))v=default;
                times[name]=(v.time+data.GetSampleTimeMs(i),v.count+1);
            }
        }
        foreach(var item in times.OrderByDescending(x=>x.Value.time).Take(50))
            Debug.Log($"EDITOR_HOT {Names[phase]} {item.Key} total={item.Value.time:F2} calls={item.Value.count}");
    }
}

