using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.GridArt.Editor;
using Project.LevelEditor;
using Project.BlockFeatures.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PlanningEditorPrototype
{
    public static class PlanningArtBlockReferenceAudit
    {
        private static readonly List<string> Report = new();
        private static GameObject host;
        private static PanelSettings settings;
        private static VisualElement root;
        private static GridArtLevelReference reference;
        private static GridArtLevelSnapshot pickSnapshot;
        private static RenderTexture target;
        private static int ticks, checks, captureStage;
        private static double started;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run only in the isolated probe.");
            Report.Clear(); ticks = checks = captureStage = 0; started = EditorApplication.timeSinceStartup;
            try
            {
                Sources();
                // Render the actual UI Toolkit element to a runtime panel target.
                // EditorPanel has no targetTexture API in Unity 6000.3.
                target = new RenderTexture(800, 500, 24, RenderTextureFormat.ARGB32); target.Create();
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ConstantPixelSize; settings.targetTexture = target;
                host = new GameObject("Reference render audit");
                var document = host.AddComponent<UIDocument>(); document.panelSettings = settings;
                root = document.rootVisualElement;
                root.style.width = 800; root.style.height = 500;
                root.style.backgroundColor = new Color(.12f, .15f, .19f);
                reference = new GridArtLevelReference(); root.Add(reference);
                var snapshot = new GridArtLevelSnapshot();
                snapshot.AddReference("black", new GridArtQuad(new Rect(0, 0, 3, 2), new Rect(0,0,1,1), Texture2D.whiteTexture), new Color(.04f,.04f,.04f), GridArtReferenceLayer.Wall);
                snapshot.AddReference("white", new GridArtQuad(new Rect(4, 0, 3, 2), new Rect(0,0,1,1), Texture2D.whiteTexture), new Color(.95f,.95f,.95f), GridArtReferenceLayer.Wall);
                reference.Configure(snapshot); reference.SetReferenceVisibility(true, 1f);
                reference.SetView(new Vector2(60,300), 64, new Rect(0,0,800,500));
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish(e); }
        }

        private static void Sources()
        {
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            Check(palette != null, "real palette loads");
            var white = palette.Entries.First(e=>e.DisplayName=="白方块");
            var black = palette.Entries.First(e=>e.DisplayName=="黑方块");
            var doc = new PlanningDocument(); var room = new PlanningRoom("blocks"); room.cells.Add(new PlanningCell(0,0)); doc.rooms.Add(room);
            room.boxes.Add(new PlanningBox { id="legacy-white", type=PlanningDetailType.Platform, paletteEntryId=white.EntryId, x=0,y=0,width=3,height=2 });
            room.boxes.Add(new PlanningBox { id="black", type=PlanningDetailType.Solid, paletteEntryId=black.EntryId,x=4,y=0,width=3,height=2 });
            room.boxes.Add(new PlanningBox { id="stale-id-name", type=PlanningDetailType.Hazard,paletteEntryId="missing",paletteEntryName=white.DisplayName,x=8,y=0,width=1,height=1 });
            room.boxes.Add(new PlanningBox { id="legacy-no-palette", type=PlanningDetailType.Platform,x=10,y=0,width=1,height=1 });
            string before = JsonUtility.ToJson(doc); var snapshot = new GridArtLevelSnapshot();
            PlanningArtAdapter.ReadDocument(doc,palette,snapshot);
            Check(snapshot.referenceItems.Count(i=>i.layer==GridArtReferenceLayer.Wall)==4, "black, legacy Platform white, stale-ID/name and enum-only fallback are all retained");
            Check(snapshot.referenceItems.Single(i=>i.id=="legacy-white").tint==white.Color && snapshot.referenceItems.Single(i=>i.id=="black").tint==black.Color, "black/white use the real block palette colors");
            Check(snapshot.referenceItems.Single(i=>i.id=="stale-id-name").tint==white.Color && snapshot.referenceItems.Single(i=>i.id=="legacy-no-palette").tint==white.Color, "fallback order agrees with the SceneBuilder");
            Check(snapshot.groups[0].bounds.Count==4, "all block types also participate in automatic wall/floor painting");
            Check(JsonUtility.ToJson(doc)==before, "reference does not migrate enum, layout or palette identity");
            foreach (var entry in palette.Entries.Where(e=>e!=null))
            {
                var custom = new PlanningDocument(); var r = new PlanningRoom("custom"); r.cells.Add(new PlanningCell(0,0)); custom.rooms.Add(r);
                r.boxes.Add(new PlanningBox { id="custom", type=PlanningDetailType.Platform,paletteEntryId=entry.EntryId,width=1,height=1 });
                var s = new GridArtLevelSnapshot(); PlanningArtAdapter.ReadDocument(custom,palette,s);
                Check(s.referenceItems.Single(i=>i.id=="custom").tint==entry.Color, "block palette color retained: "+entry.DisplayName);
            }
            var irregular = new PlanningDocument(); var irregularRoom = new PlanningRoom("holes");
            irregularRoom.cells.Add(new PlanningCell(0,0)); irregular.rooms.Add(irregularRoom);
            foreach (var entry in new[]{black,white})
            {
                var merged = new PlanningBox { id=entry.DisplayName,type=PlanningDetailType.Platform,paletteEntryId=entry.EntryId,
                    width=3,height=3,isMerged=true,x=entry==black?0:5 };
                merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox {x=0,y=0,width=1,height=3}));
                merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox {x=1,y=2,width=2,height=1}));
                irregularRoom.boxes.Add(merged);
            }
            var irregularSnapshot = new GridArtLevelSnapshot(); PlanningArtAdapter.ReadDocument(irregular,palette,irregularSnapshot);
            Check(irregularSnapshot.Cells("",1).Count==10, "L-shaped black and white merges retain only their ten real cells");
            Check(!irregularSnapshot.Cells("",1).Contains(new Vector2Int(1,-1)) &&
                !irregularSnapshot.Cells("",1).Contains(new Vector2Int(6,-1)), "black/white merged bounding boxes do not fill holes");
            Check(irregularSnapshot.referenceItems.Where(i=>i.layer==GridArtReferenceLayer.Wall).Sum(i=>i.quad.bounds.width*i.quad.bounds.height)==10,
                "displayed wall geometry has the same sparse coverage as automatic fill");
            foreach (string path in new[]{"Assets/Scenes/Pick.planning.json","Assets/Scenes/MRoom2.planning.json","Assets/Scenes/MRoom3.planning.json","Assets/_Project/Scenes/Playing/Gaming.planning.json"})
            {
                if(!File.Exists(path)) continue;
                string json=File.ReadAllText(path); var map=JsonUtility.FromJson<PlanningDocument>(json); var s=new GridArtLevelSnapshot(); PlanningArtAdapter.ReadDocument(map,palette,s);
                var blocks=map.rooms.SelectMany(r=>r.boxes).Concat(map.assemblyPatches).Where(b=>string.IsNullOrEmpty(b.propEntryId)&&string.IsNullOrEmpty(b.propEntryName)).ToList();
                var shown=s.referenceItems.Where(i=>i.layer==GridArtReferenceLayer.Wall).Select(i=>i.id).ToHashSet();
                Check(blocks.All(b=>shown.Contains(b.id)), path+": every authored black/white/custom block has reference geometry ("+blocks.Count+")");
                Check(File.ReadAllText(path)==json, path+": inspection leaves author save untouched");
                if(path.Contains("Pick.")) pickSnapshot=s;
            }
            foreach(var prop in palette.PropEntries.Where(p=>p?.Prefab!=null))
            {
                bool supported=ObjectArtworkPlanningPreview.TryGet(prop.Prefab,Vector2.one,prop.ComponentValueOverrides,out var art);
                if(!supported)
                {
                    var fallbackDoc = new PlanningDocument(); var r = new PlanningRoom("markers"); r.cells.Add(new PlanningCell(0,0)); fallbackDoc.rooms.Add(r);
                    r.boxes.Add(new PlanningBox {id="marker",propEntryId=prop.EntryId,propEntryName=prop.DisplayName,width=2,height=3});
                    var s = new GridArtLevelSnapshot(); PlanningArtAdapter.ReadDocument(fallbackDoc,palette,s);
                    Check(s.referenceOnlyNames.Contains(prop.DisplayName) && s.referenceItems.Count==4 &&
                        s.referenceItems.All(i=>i.layer==GridArtReferenceLayer.EditorOverlay) && s.groups[0].bounds.Count==0,
                        "missing art has a non-wall read-only outline, not a silent omission: "+prop.DisplayName);
                }
                else if(art.Hidden) Report.Add("INTENTIONALLY HIDDEN: "+prop.DisplayName);
            }
            var brokenDoc = new PlanningDocument(); var brokenRoom = new PlanningRoom("old ID"); brokenRoom.cells.Add(new PlanningCell(0,0)); brokenDoc.rooms.Add(brokenRoom);
            var existing = palette.PropEntries.First(p=>p?.Prefab!=null && p.DisplayName=="红色接触方块");
            brokenRoom.boxes.Add(new PlanningBox {id="named-prop",propEntryId="stale",propEntryName=existing.DisplayName});
            var brokenSnapshot = new GridArtLevelSnapshot(); PlanningArtAdapter.ReadDocument(brokenDoc,palette,brokenSnapshot);
            Check(brokenSnapshot.referenceItems.Any(i=>i.layer==GridArtReferenceLayer.Object), "stale prop ID still resolves the current named 2D art");
            var hiddenSign = palette.PropEntries.First(p=>p?.Prefab!=null && p.Prefab.GetComponent<Project.Subtitles.SubtitleTrigger>()!=null);
            var signDoc = new PlanningDocument(); var signRoom = new PlanningRoom("hidden");signRoom.cells.Add(new PlanningCell(0,0));signDoc.rooms.Add(signRoom);
            var signBox = new PlanningBox {id="sign",propEntryId=hiddenSign.EntryId,hasComponentOverrides=true};
            var setting = new LevelEditorComponentValueOverride();
            setting.Configure("",typeof(Project.Subtitles.SubtitleTrigger).FullName,0,"showSign","Boolean","false",null);
            signBox.componentOverrides.Add(setting);signRoom.boxes.Add(signBox);
            var signSnapshot = new GridArtLevelSnapshot();PlanningArtAdapter.ReadDocument(signDoc,palette,signSnapshot);
            Check(signSnapshot.referenceItems.Count==0 && signSnapshot.referenceOnlyNames.Count==0,
                "explicitly hidden sign stays hidden rather than becoming a placeholder");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                foreach(var entry in new[]{black,white})
                {
                    var go=new GameObject(entry.DisplayName);SceneManager.MoveGameObjectToScene(go,scene);
                    var block=go.AddComponent<LevelEditorPlacedBlock>();
                    block.Configure(new Vector2Int(entry==black?0:5,0),entry.DisplayName,entry.Color,true);
                    block.SetSizeCells(new Vector2Int(3,3));
                    go.AddComponent<LevelEditorMergedShape>().Configure(new Vector2Int(3,3),
                        new[]{new RectInt(0,0,1,3),new RectInt(1,2,2,1)});
                }
                var s=PlanningArtAdapter.Read(scene);
                Check(s.referenceItems.Count==4 && s.referenceItems.Any(i=>i.tint==black.Color) && s.referenceItems.Any(i=>i.tint==white.Color),
                    "scene-only fallback also preserves both block colors and merged parts");
                Check(s.Cells("",1).Count==10,"scene-only fallback keeps the same sparse wall coverage");
                foreach(var rootObject in scene.GetRootGameObjects())
                {
                    var serialized = new SerializedObject(rootObject.GetComponent<LevelEditorPlacedBlock>());
                    serialized.FindProperty("hasColorData").boolValue=false; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var legacyScene=PlanningArtAdapter.Read(scene);
                Check(legacyScene.referenceItems.Any(i=>i.tint==black.Color) && legacyScene.referenceItems.Any(i=>i.tint==white.Color),
                    "older scene blocks without serialized color data resolve the named palette color");
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
            PlanningArtReferenceAudit.Run();
            Check(File.ReadAllText("Library/PlanningArtReferenceAudit.txt").StartsWith("PASS 23"), "existing 23-item reference regression remains green");
        }

        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>60) throw new TimeoutException("attached reference render");
                if(root.panel==null) return;
                var panel=root.panel;
                var repaint=panel.GetType().GetMethod("Repaint",Flags,null,new[]{typeof(Event)},null);
                if(repaint==null) throw new InvalidOperationException("Runtime panel repaint API unavailable");
                repaint.Invoke(panel,new object[]{new Event{type=EventType.Repaint}});
                if(++ticks<15) return;
                Report.Add("PANEL "+panel.GetType()+" size="+root.contentRect);
                var previous=RenderTexture.active; RenderTexture.active=target;
                var pixels=new Texture2D(800,500,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,800,500),0,0);pixels.Apply();RenderTexture.active=previous;
                string name=captureStage==0?"GridArtBlackWhite.png":captureStage==1?"GridArtBlackWhiteDefaultOpacity.png":"GridArtPickReference.png";
                File.WriteAllBytes("Library/"+name,pixels.EncodeToPNG());
                Color b=pixels.GetPixel(124,264), w=pixels.GetPixel(380,264);
                int wallPixels=captureStage==2?pixels.GetPixels32().Count(c=>c.r>128 && c.g>128 && c.b>128):0;
                Report.Add("GPU "+name+" black="+b+" white="+w);
                Object.DestroyImmediate(pixels);
                if(captureStage==0)
                {
                    Check(b.r<.10f && w.r>.8f, "real attached UI renders both black and white reference blocks");
                    reference.SetReferenceVisibility(true,.35f); captureStage++; ticks=0; return;
                }
                if(captureStage==1)
                {
                    Check(w.r-b.r>.20f && b.r<.115f,"both block colors remain distinguishable at the default 35% opacity");
                    if(pickSnapshot==null) throw new InvalidOperationException("Actual Pick planning fixture missing");
                    reference.Configure(pickSnapshot); var bounds=pickSnapshot.worldBounds;
                    float zoom=Mathf.Min(740/bounds.width,440/bounds.height);
                    reference.SetView(new Vector2(400-bounds.center.x*zoom,250+bounds.center.y*zoom),zoom,new Rect(0,0,800,500));
                    reference.SetReferenceVisibility(true,.65f);captureStage++;ticks=0;return;
                }
                Check(reference.QuadCount>80 && reference.VisibleBatchCount>0 && wallPixels>5000,
                    "actual Pick source including its eighty white block records renders in the same UI element ("+wallPixels+" visible light pixels)");
                Finish(null);
            }
            catch(Exception e) { Finish(e); }
        }
        private static void Check(bool value,string message) { checks++; if(!value)throw new InvalidOperationException(message);Report.Add("PASS "+message); }
        private static void Finish(Exception error)
        {
            EditorApplication.update-=Tick;
            if(error!=null) { Report.Add(error.ToString()); Debug.LogException(error); }
            if(host!=null) Object.DestroyImmediate(host);
            if(settings!=null) Object.DestroyImmediate(settings);
            if(target!=null) { target.Release();Object.DestroyImmediate(target); }
            File.WriteAllText("Library/PlanningArtBlockReferenceAudit.txt",(error==null?"PASS ":"FAIL ")+checks+"\n"+string.Join("\n",Report));
            EditorApplication.Exit(error==null?0:1);
        }
    }
}
