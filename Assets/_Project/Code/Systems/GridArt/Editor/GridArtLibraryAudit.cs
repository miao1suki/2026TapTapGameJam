using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Project.GridArt.Editor
{
    /// <summary>Isolated append/pixel/import/layout regression with the real workspace cards.</summary>
    public static class GridArtLibraryAudit
    {
        private static readonly List<string> Report=new();
        private static GridArtWindow window;
        private static GridArtPalette library;
        private static GridArtBrushGrid grid;
        private static string folder;
        private static int checks,phase,ticks;
        private static double started;
        private static GameObject host;
        private static PanelSettings settings;
        private static RenderTexture target;
        private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').EndsWith("/Library/FlowRetirementProbe/Assets",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only run in FlowRetirementProbe.");
            checks=phase=ticks=0; Report.Clear(); started=EditorApplication.timeSinceStartup;
            try
            {
                library=GridArtLibraryService.Shared();
                var keys=library.brushes.Select(b=>b.sourceKey).ToArray();
                var sizes=library.brushes.Select(b=>b.DisplaySize).ToArray();
                var sprites=library.brushes.Select(b=>GridArtLibraryService.SpriteKey(b.sprite)).ToArray();
                int added=GridArtLibraryService.Synchronize(library);
                Check(library.brushes.Count==keys.Length+added,$"append only: {keys.Length} + {added} = {library.brushes.Count}");
                Check(library.brushes.Take(keys.Length).Select(b=>b.sourceKey).SequenceEqual(keys),"all original brush keys/indices retained");
                Check(library.brushes.Take(keys.Length).Select(b=>b.DisplaySize).SequenceEqual(sizes),"all original display sizes retained");
                Check(library.brushes.Take(keys.Length).Select(b=>GridArtLibraryService.SpriteKey(b.sprite)).SequenceEqual(sprites),"all original sprite GUID/local IDs retained");
                Check(GridArtLibraryService.Synchronize(library)==0,"second synchronize is idempotent");
                Check(library.brushes.Select(b=>b.sourceKey).Distinct().Count()==library.brushes.Count,"no duplicate source keys");
                foreach(var brush in library.brushes)
                    Check(GridArtPixelLayout.IsNative(brush),"native pixel size: "+brush.label);
                Check(library.brushes.Count(b=>b.category=="雨滴原图")==12,"real raindrop columns are x320/x352, not the grass fragments");
                Check(library.brushes.Count(b=>b.category=="地面草饰·组合")==12,"12 original two-row ground grass combinations at natural 1x2 cells");
                foreach(string path in library.brushes.Where(b=>b.sprite!=null).Select(b=>AssetDatabase.GetAssetPath(b.sprite.texture)).Distinct())
                {
                    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                    if(importer==null) continue;
                    Check(importer.filterMode==FilterMode.Point && !importer.mipmapEnabled && importer.npotScale==TextureImporterNPOTScale.None &&
                        importer.textureCompression==TextureImporterCompression.Uncompressed,"point/no mip/no resize/uncompressed: "+path);
                }
                foreach(var item in new[]{(GridArtProjectLibrary.BackgroundPath,4),("Assets/_Project/Resources/26TAPTAP/物体道具/471dca30b9bcb2527867589ad677e3e3.png",1)})
                {
                    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    texture.LoadImage(File.ReadAllBytes(item.Item1));
                    Check(GridArtworkMetrics.ExactExportScale(texture.GetPixels32(),texture.width,texture.height)==item.Item2,"verified source export multiplier: "+item.Item1);
                    Object.DestroyImmediate(texture);
                }
                AssetDatabase.SaveAssetIfDirty(library);
                folder="Assets/__GridArtLibraryAudit_"+Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                string scenePath=folder+"/Layout.unity"; EditorSceneManager.SaveScene(scene,scenePath);
                var map=ScriptableObject.CreateInstance<GridArtMap>(); map.ownerSceneGuid=AssetDatabase.AssetPathToGUID(scenePath);
                map.palette=library; map.layers.Add(new GridArtLayer()); AssetDatabase.CreateAsset(map,folder+"/Map.asset");
                window=ScriptableObject.CreateInstance<GridArtWindow>(); window.Show(); window.position=new Rect(20,20,1280,900); window.SetMap(map);
                EditorApplication.update+=Tick;
            }
            catch(Exception error) { Finish(error); }
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>100) throw new TimeoutException("Library layout phase "+phase);
                if(++ticks<12) return;
                if(phase<2)
                {
                    grid=window.rootVisualElement.Q<GridArtBrushGrid>();
                    if(grid==null || grid.contentRect.width<=0 || grid.Children().Count()<3 || grid.Children().First().worldBound.height<=0) return;
                    Check(Mathf.Abs(window.position.width-(phase==0?1280:920))<2,"requested workspace width applied: "+window.position.width);
                    var cards=grid.Children().Where(e=>e.ClassListContains("art-brush")).ToArray();
                    Check(cards.Length==library.brushes.Count,"all selections appear in actual workspace");
                    Check(Mathf.Abs(cards[0].worldBound.y-cards[1].worldBound.y)<1 && cards[1].worldBound.x>cards[0].worldBound.x+50,
                        "two columns in actual workspace at width "+window.position.width+" / viewport "+grid.contentRect.width+
                        " / cards "+cards[0].worldBound+"; "+cards[1].worldBound+" / style "+cards[0].style.width+" margin "+cards[0].resolvedStyle.marginLeft+","+cards[0].resolvedStyle.marginRight+
                        " padding "+cards[0].resolvedStyle.paddingLeft+","+cards[0].resolvedStyle.paddingRight+" borders "+cards[0].resolvedStyle.borderLeftWidth);
                    Check(cards.All(e=>e.worldBound.xMax<=grid.worldBound.xMax+.5f),"no horizontal card overflow at "+window.position.width);
                    if(phase==0) { window.position=new Rect(20,20,920,640); phase=1; ticks=0; return; }
                    // Reparent the real workspace cards into a renderable UI Toolkit panel.
                    grid.RemoveFromHierarchy();
                    target=new RenderTexture(1200,Mathf.CeilToInt(library.brushes.Count/12f)*100+30,24); target.Create();
                    settings=ScriptableObject.CreateInstance<PanelSettings>(); settings.scaleMode=PanelScaleMode.ConstantPixelSize; settings.targetTexture=target;
                    host=new GameObject("Shared library capture"); var document=host.AddComponent<UIDocument>(); document.panelSettings=settings;
                    var root=document.rootVisualElement; root.AddToClassList("art-workspace"); root.style.width=target.width; root.style.height=target.height;
                    root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_Project/Code/Systems/GridArt/Editor/GridArtWorkspace.uss"));
                    root.Add(grid); grid.Reflow(true); phase=2; ticks=0; return;
                }
                if(phase==2)
                {
                    grid.Reflow(true);
                    var panel=grid.panel;
                    panel.GetType().GetMethod("Repaint",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                        ?.Invoke(panel,new object[]{new Event{type=EventType.Repaint}});
                    var previous=RenderTexture.active; RenderTexture.active=target;
                    var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                    image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); RenderTexture.active=previous;
                    File.WriteAllBytes("Library/GridArtSharedLibrary.png",image.EncodeToPNG());
                    Check(image.GetPixels32().Count(c=>c.r>160 || c.g>160 || c.b>160)>5000,"real material thumbnails rendered to contact sheet");
                    Object.DestroyImmediate(image); Finish(null);
                }
            }
            catch(Exception error) { Finish(error); }
        }
        private static void Check(bool ok,string detail)
        { checks++; if(!ok) throw new InvalidOperationException("Check "+checks+": "+detail); Report.Add("PASS "+checks+": "+detail); }
        private static void Finish(Exception error)
        {
            EditorApplication.update-=Tick;
            if(host!=null) Object.DestroyImmediate(host); if(settings!=null) Object.DestroyImmediate(settings);
            if(target!=null) { target.Release(); Object.DestroyImmediate(target); }
            if(window!=null) window.Close();
            if(!string.IsNullOrEmpty(folder)) { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); AssetDatabase.DeleteAsset(folder); }
            string result=(error==null?"PASS ":"FAIL ")+checks+"\n"+string.Join("\n",Report)+"\n"+error;
            File.WriteAllText("Library/GridArtLibraryAudit.txt",result);
            if(error==null) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.Exit(error==null?0:1);
        }
    }
}
