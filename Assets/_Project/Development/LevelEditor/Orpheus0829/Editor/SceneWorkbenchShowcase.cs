using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.GridArt;
using Project.GridArt.Editor;
using Project.Rooms;
using Project.Mechanisms;
using Project.ObjectArtwork;
using Project.BlockFeatures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.LevelEditor.Editor
{
    /// <summary>Explicit authoring fixture generator. Never runs when opening the workbench.</summary>
    public static class SceneWorkbenchShowcase
    {
        public const string Folder = "Assets/_Project/Development/LevelEditor/SceneAuthoringShowcase";
        public const string ScenePath = Folder + "/MossAqueduct.unity";

        public static void OpenForReview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            SceneLevelEditorOverlay.Open();
            var view=SceneView.lastActiveSceneView; view.in2DMode=true;
            view.Frame(new Bounds(new Vector3(21,9,0),new Vector3(48,27,1)),true);
            SceneWorkbenchAudit.StartReviewObservations(ScenePath);
        }

        [MenuItem("Tools/2026TapTap/关卡编辑器/验证/创建苔石水廊展示")]
        public static void Create()
        {
            foreach (string path in new[] { ScenePath, Folder + "/ShowcaseMap.asset", Folder + "/ShowcasePalette.asset", Folder + "/ShowcaseOwnership.asset", Folder + "/StoneCollider.prefab" })
                if (File.Exists(path)) throw new InvalidOperationException("展示作者数据已存在，拒绝覆盖：" + path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var manager = new GameObject("房间管理器 · 苔石水廊").AddComponent<RoomLifecycleManager>();
            List<Vector2Int> Region(int x, int y, int w, int h) => Enumerable.Range(0, w * h)
                .Select(i => SceneGridSpace.SceneToRegion(new Vector2Int(x + i % w, y + i / w))).ToList();
            manager.ConfigureAuthoredRegions(new[] {
                new RoomRegion { id="moss_west", label="苔影庭院", cells=Region(-2,-2,18,22) },
                new RoomRegion { id="moss_bridge", label="水廊木桥", corridor=true, cells=Region(16,-2,6,22) },
                new RoomRegion { id="moss_east", label="铜门阶庭", cells=Region(22,-2,22,22) }
            }, 1);

            Sprite Single(string name)
            {
                string path = Folder + "/" + name + ".png";
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new InvalidOperationException("Missing generated artwork: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spritePivot = Vector2.zero;
                settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            Single("MossTerrainBands");
            var terrainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/MossTerrainBands.png");
            var palette = ScriptableObject.CreateInstance<GridArtPalette>();
            AssetDatabase.CreateAsset(palette, Folder + "/ShowcasePalette.asset");
            var terrain = new GridArtBrush { label="苔石 · 全轮廓连接", category="自动地形", autoTerrain=true, colorId="none" };
            for (int i=0;i<13;i++) {
                terrain.pieces[i] = Sprite.Create(terrainTexture,new Rect(i*16,0,16,16),Vector2.zero,16,0,SpriteMeshType.FullRect);
                terrain.pieces[i].name = "MossPiece_" + i;
                AssetDatabase.AddObjectToAsset(terrain.pieces[i],palette);
            }
            ConfigureWideContour(palette,terrain);
            palette.brushes.Add(terrain);
            int Add(string source, string label, string category)
            {
                var sprite=Single(source);
                palette.brushes.Add(new GridArtBrush { label=label, category=category, sprite=sprite,
                    size=new Vector2Int(Mathf.CeilToInt(sprite.rect.width/16),Mathf.CeilToInt(sprite.rect.height/16)),
                    visualSize=sprite.rect.size/16, colorId="none" });
                return palette.brushes.Count-1;
            }
            int arch=Add("MossArch","苔石拱门","建筑");
            int bridge=Add("WoodBridge","铜钉木桥","建筑");
            int vine=Add("HangingVine","垂挂苔藤","植物");
            int fern=Add("FlowerFern","白花蕨丛","植物");
            int planter=Add("StonePlanter","石台花钵","植物");
            int water=Add("PoolWater","青碧水面","水景");
            int front=Add("ForegroundFern","前景深蕨","植物");
            int background=Add("DistantAqueduct","远景水道","远景");
            Add("BronzeDoor","铜门装饰","建筑"); Add("AmberButton","琥珀标识","建筑");
            EditorUtility.SetDirty(palette);

            var map=ScriptableObject.CreateInstance<GridArtMap>(); map.palette=palette;
            var bg=new GridArtLayer { id="moss_background",label="远景 · 不跟随房间",depth=10,sortingOrder=-100,locked=true,tint=new Color(.6f,.7f,.75f,1),parallax=new Vector2(.25f,.25f) };
            var stone=new GridArtLayer { id="moss_terrain",label="地形 · 自动包边",depth=1,sortingOrder=0 };
            var architecture=new GridArtLayer { id="moss_architecture",label="拱门与木桥",depth=.6f,sortingOrder=10 };
            var plants=new GridArtLayer { id="moss_plants",label="植物 · 手绘修饰",depth=.3f,sortingOrder=20 };
            var pool=new GridArtLayer { id="moss_water",label="水景 · 装饰",depth=.8f,sortingOrder=5 };
            var foreground=new GridArtLayer { id="moss_foreground",label="近景蕨叶",depth=-.5f,sortingOrder=40 };
            map.layers.AddRange(new[] {bg,stone,pool,architecture,plants,foreground});
            bg.Set(new Vector2Int(-3,-4),background);
            var solids=ComposeTerrain();
            foreach(var c in solids.OrderBy(c=>c.y).ThenBy(c=>c.x)) stone.Set(c,0);
            architecture.Set(new Vector2Int(4,6),arch); architecture.Set(new Vector2Int(35,6),arch);
            architecture.Set(new Vector2Int(15,3),bridge); architecture.Set(new Vector2Int(19,3),bridge);
            foreach(var c in new[] {new Vector2Int(1,6),new Vector2Int(10,6),new Vector2Int(23,7),new Vector2Int(29,9),new Vector2Int(38,6)}) plants.Set(c,fern);
            plants.Set(new Vector2Int(2,10),vine); plants.Set(new Vector2Int(9,10),vine); plants.Set(new Vector2Int(25,12),vine); plants.Set(new Vector2Int(34,12),vine);
            plants.Set(new Vector2Int(12,5),planter); plants.Set(new Vector2Int(33,6),planter);
            pool.Set(new Vector2Int(15,0),water); pool.Set(new Vector2Int(19,0),water);
            foreground.Set(new Vector2Int(-1,-1),front); foreground.Set(new Vector2Int(11,-1),front); foreground.Set(new Vector2Int(38,-1),front);
            AssetDatabase.CreateAsset(map,Folder+"/ShowcaseMap.asset");
            var ownership=ScriptableObject.CreateInstance<GridArtRegionOwnership>();
            AssetDatabase.CreateAsset(ownership,Folder+"/ShowcaseOwnership.asset");
            string Owner(Vector2Int c) => c.x<16 ? "moss_west" : c.x<22 ? "moss_bridge" : "moss_east";
            foreach(var layer in new[] {stone,architecture,plants,pool,foreground}) foreach(var c in layer.cells) ownership.Set(layer.id,c.position,Owner(c.position));
            EditorUtility.SetDirty(ownership);

            var prefabRoot=new GameObject("苔石碰撞格"); prefabRoot.AddComponent<BoxCollider2D>();
            var stonePrefab=PrefabUtility.SaveAsPrefabAsset(prefabRoot,Folder+"/StoneCollider.prefab"); UnityEngine.Object.DestroyImmediate(prefabRoot);
            var collisionRoot=new GameObject("实体地形 · 与美术初始对齐");
            foreach(var c in solids) {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(stonePrefab,scene); go.transform.SetParent(collisionRoot.transform,false);
                go.transform.position=new Vector3(c.x+.5f,c.y+.5f,0);
                var block=go.AddComponent<LevelEditorPlacedBlock>(); block.Configure(c,"苔石",Color.white,false,false,1);
                block.SetPlanningSource("moss_stone_"+c.x+"_"+c.y,"",Vector2Int.zero);
                go.AddComponent<RoomResetObject>().Bind(manager,Owner(c)); PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            GameObject Mechanism(string prefab,string name,Vector2Int c) {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab),scene);
                go.name=name; go.transform.position=new Vector3(c.x+.5f,c.y+.5f,0);
                var block=go.GetComponent<LevelEditorPlacedBlock>()??go.AddComponent<LevelEditorPlacedBlock>();
                block.Configure(c,name,Color.white,false,true,1); if(go.GetComponent<DoorController>()!=null) block.SetSizeCells(new Vector2Int(1,2));
                block.SetPlanningSource("moss_"+name,"",Vector2Int.zero);
                (go.GetComponent<RoomResetObject>()??go.AddComponent<RoomResetObject>()).Bind(manager,Owner(c));
                PrefabUtility.RecordPrefabInstancePropertyModifications(block);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<RoomResetObject>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); return go;
            }
            var door=Mechanism("Assets/_Project/Content/Mechanisms/Door.prefab","铜门机关",new Vector2Int(36,6)).GetComponent<DoorController>();
            var button=Mechanism("Assets/_Project/Content/Mechanisms/DoorButton.prefab","门前压力按钮",new Vector2Int(34,6)).GetComponent<DoorButton>();
            door.SetButtons(new[]{button}); PrefabUtility.RecordPrefabInstancePropertyModifications(door); PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            ConfigureMechanismArtwork(door,button);
            var camera=new GameObject("展示相机").AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=13.5f;
            camera.transform.position=new Vector3(21,9.5f,-20); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.09f,.12f); camera.tag="MainCamera";
            EditorSceneManager.SaveScene(scene,ScenePath);
            var art=GridArtBuilder.Apply(map,scene,false,true); art.SetCamera(camera); art.gameObject.AddComponent<GridArtRegionOwnershipLink>().SetOwnership(ownership);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Validate(scene,map,door,button,solids.Count);
        }

        private static void Validate(Scene scene,GridArtMap map,DoorController door,DoorButton button,int solidCount)
        {
            if(door.Buttons.Count!=1||door.Buttons[0]!=button||button.Owner!=door) throw new InvalidOperationException("展示机关引用不同步");
            if(map.layers.First(l=>l.id=="moss_terrain").cells.Count!=solidCount) throw new InvalidOperationException("地形作者格不匹配");
            File.WriteAllText("Library/SceneWorkbenchShowcase.txt",$"Created {scene.path}\nSolid cells {solidCount}; layers {map.layers.Count}; art anchors {map.layers.Sum(l=>l.cells.Count)}; real door/button reference pair valid.\nAuthoring demonstration, not a playable level or full gameplay test.\n");
        }

        public static void CreateFromCommandLine()
        {
            try { Create(); ExportPreview(); EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void ConfigureWideContour(GridArtPalette palette,GridArtBrush brush)
        {
            string path=Folder+"/MossTerrainWide.png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture.width!=416||texture.height!=32) throw new InvalidOperationException("宽轮廓图集规格不匹配");
            var assets=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(palette));
            foreach(var rejected in assets.OfType<Sprite>().Where(s=>s.name.StartsWith("MossInset_",StringComparison.Ordinal)))
                UnityEngine.Object.DestroyImmediate(rejected,true);
            brush.widePieces=new Sprite[13];
            for(int i=0;i<13;i++) {
                string name="MossWide_"+i;
                var sprite=assets.OfType<Sprite>().FirstOrDefault(s=>s!=null&&s.name==name);
                if(sprite==null) {
                    sprite=Sprite.Create(texture,new Rect(i*32,0,32,32),Vector2.zero,16,0,SpriteMeshType.FullRect);
                    sprite.name=name; AssetDatabase.AddObjectToAsset(sprite,palette);
                }
                brush.widePieces[i]=sprite;
            }
        }

        private static HashSet<Vector2Int> ComposeTerrain()
        {
            var cells=new HashSet<Vector2Int>();
            void Rect(int x,int y,int w,int h) { for(int yy=y;yy<y+h;yy++) for(int xx=x;xx<x+w;xx++) cells.Add(new Vector2Int(xx,yy)); }
            void Cut(int x,int y,int w,int h) { for(int yy=y;yy<y+h;yy++) for(int xx=x;xx<x+w;xx++) cells.Remove(new Vector2Int(xx,yy)); }
            Rect(0,0,16,6); Cut(0,0,2,2); Cut(15,0,1,1); Cut(13,5,3,1);
            Rect(22,0,21,6); Rect(23,6,4,1); Rect(26,6,5,2); Rect(29,6,4,3);
            Rect(2,12,11,4); Cut(2,12,1,1); Cut(12,12,1,1); Cut(2,15,1,1);
            Rect(24,14,12,4); Cut(24,14,1,1); Cut(35,14,1,1); Cut(24,17,3,1); Cut(35,17,1,1);
            Rect(38,6,5,10); Cut(38,15,1,1); Cut(42,15,1,1);
            return cells;
        }

        private static void ConfigureMechanismArtwork(DoorController door,DoorButton button)
        {
            string path=Folder+"/ShowcaseMechanisms.asset";
            var library=AssetDatabase.LoadAssetAtPath<ObjectArtworkLibrary>(path);
            if(library==null) {
                library=UnityEngine.Object.Instantiate(ObjectArtworkLibrary.Load());
                library.name="苔石水廊 · 独立机关美术"; AssetDatabase.CreateAsset(library,path);
            }
            Sprite Sprite(string file) {
                var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s=>s.name==file);
                if(sprite!=null) return sprite;
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+file+".png");
                sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,0),16,0,SpriteMeshType.FullRect);
                sprite.name=file; AssetDatabase.AddObjectToAsset(sprite,library); return sprite;
            }
            var doorSprite=Sprite("BronzeDoor"); var buttonSprite=Sprite("AmberButton");
            library.doorClosed=doorSprite;
            library.mechanisms=new[] {
                new MechanismArtworkEntry {kind=MechanismArtworkKind.Door,frames=new[]{doorSprite}},
                new MechanismArtworkEntry {kind=MechanismArtworkKind.DoorButton,frames=new[]{buttonSprite}}
            };
            // The generated amber medallion belongs on the button, not over the door frame.
            library.doorWhiteIndicators=Array.Empty<Sprite>(); library.doorIndicators=Array.Empty<Sprite>();
            EditorUtility.SetDirty(library);
            foreach(var owner in new[]{door.gameObject,button.gameObject}) {
                var presentation=owner.GetComponent<MechanismArtworkPresentation>()??owner.AddComponent<MechanismArtworkPresentation>();
                var serialized=new SerializedObject(presentation);
                serialized.FindProperty("artwork").objectReferenceValue=library; serialized.ApplyModifiedPropertiesWithoutUndo();
                presentation.Refresh(0f); PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
            }
        }

        public static void RefineCompositionFromCommandLine()
        {
            try {
                var scene=EditorSceneManager.OpenScene(ScenePath);
                var map=AssetDatabase.LoadAssetAtPath<GridArtMap>(Folder+"/ShowcaseMap.asset");
                var ownership=AssetDatabase.LoadAssetAtPath<GridArtRegionOwnership>(Folder+"/ShowcaseOwnership.asset");
                var terrain=map.layers.Single(l=>l.id=="moss_terrain");
                var manager=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RoomLifecycleManager>()).Single();
                var collisionRoot=scene.GetRootGameObjects().Single(r=>r.name=="实体地形 · 与美术初始对齐");
                string Owner(Vector2Int c) => c.x<16 ? "moss_west" : c.x<22 ? "moss_bridge" : "moss_east";
                foreach(var cell in terrain.cells.ToArray()) { terrain.Erase(cell.position,map.palette); ownership.Set(terrain.id,cell.position,""); }
                foreach(Transform child in collisionRoot.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/StoneCollider.prefab");
                var solids=ComposeTerrain();
                foreach(var c in solids.OrderBy(c=>c.y).ThenBy(c=>c.x)) {
                    terrain.Set(c,0); ownership.Set(terrain.id,c,Owner(c));
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene); go.transform.SetParent(collisionRoot.transform,false);
                    go.transform.position=new Vector3(c.x+.5f,c.y+.5f,0);
                    var block=go.AddComponent<LevelEditorPlacedBlock>(); block.Configure(c,"苔石",Color.white,false,false,1);
                    block.SetPlanningSource("moss_stone_"+c.x+"_"+c.y,"",Vector2Int.zero);
                    go.AddComponent<RoomResetObject>().Bind(manager,Owner(c)); PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                }
                var plants=map.layers.Single(l=>l.id=="moss_plants");
                foreach(var shift in new[]{(new Vector2Int(1,5),new Vector2Int(1,6)),(new Vector2Int(10,5),new Vector2Int(10,6))}) {
                    var old=plants.cells.FirstOrDefault(c=>c.position==shift.Item1);
                    if(old==null) continue;
                    int brush=old.brush;
                    plants.Erase(shift.Item1,map.palette); ownership.Set(plants.id,shift.Item1,"");
                    plants.Set(shift.Item2,brush); ownership.Set(plants.id,shift.Item2,Owner(shift.Item2));
                }
                var arches=map.layers.Single(l=>l.id=="moss_architecture");
                var oldArch=arches.cells.FirstOrDefault(c=>c.position==new Vector2Int(4,5));
                if(oldArch!=null) {
                    arches.Erase(oldArch.position,map.palette); ownership.Set(arches.id,oldArch.position,"");
                    arches.Set(new Vector2Int(4,6),oldArch.brush); ownership.Set(arches.id,new Vector2Int(4,6),"moss_west");
                }
                var door=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DoorController>()).Single();
                var button=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DoorButton>()).Single();
                ConfigureMechanismArtwork(door,button); ConfigureWideContour(map.palette,map.palette.brushes[0]);
                map.revision++; EditorUtility.SetDirty(map); EditorUtility.SetDirty(ownership); EditorUtility.SetDirty(map.palette);
                AssetDatabase.SaveAssets(); GridArtBuilder.Apply(map,scene,false,true); EditorSceneManager.SaveScene(scene);
                Validate(scene,map,door,button,solids.Count); RefreshConnections(); ExportPreview(); EditorApplication.Exit(0);
            } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void ApplyWidePrototypeFromCommandLine()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath);
                var map = AssetDatabase.LoadAssetAtPath<GridArtMap>(Folder+"/ShowcaseMap.asset");
                var palette = map.palette;
                var brush = palette.brushes[0];
                ConfigureWideContour(palette,brush);
                EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
                GridArtBuilder.Apply(map,scene,false,true);
                EditorSceneManager.SaveScene(scene); ExportPreview(); EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void ExportPreview()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            ExportCamera(scene,"Library/MossAqueductPreview.png",768,432);
        }

        private static void RefreshConnections()
        {
            string path=Folder+"/MossConnections.unity";
            if(!File.Exists(path)) return;
            var scene=EditorSceneManager.OpenScene(path);
            var map=AssetDatabase.LoadAssetAtPath<GridArtMap>(Folder+"/ConnectionMap.asset");
            var negative=map.layers.Single(l=>l.id=="connection_case_11");
            foreach(var cell in negative.cells.ToArray()) negative.Erase(cell.position,map.palette);
            for(int y=-7;y<-3;y++) for(int x=-5;x<0;x++) if(x!=-3||y!=-5) negative.Set(new Vector2Int(x,y),0);
            scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single().transform.position=new Vector3(41,29,-20);
            map.revision++; EditorUtility.SetDirty(map); AssetDatabase.SaveAssets();
            GridArtBuilder.Apply(map,scene,false,true); EditorSceneManager.SaveScene(scene);
            ExportCamera(scene,"Library/MossConnectionsPreview.png",1536,1248);
        }

        public static void CreateConnectionFixtureFromCommandLine()
        {
            try {
                string path=Folder+"/MossConnections.unity";
                if(File.Exists(path)) throw new InvalidOperationException("连接样板已存在，拒绝覆盖");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var map=ScriptableObject.CreateInstance<GridArtMap>();
                map.palette=AssetDatabase.LoadAssetAtPath<GridArtPalette>(Folder+"/ShowcasePalette.asset");
                HashSet<Vector2Int> Rect(int w,int h) => Enumerable.Range(0,w*h).Select(i=>new Vector2Int(i%w,i/w)).ToHashSet();
                var shapes=new List<HashSet<Vector2Int>> {
                    Rect(8,5),Rect(8,1),Rect(1,6),Rect(1,1),
                    Rect(8,6).Where(c=>!(c.x>=2&&c.x<=5&&c.y>=2&&c.y<=3)).ToHashSet(),
                    Rect(8,5).Where(c=>c.x<2||c.y<2).ToHashSet(),
                    Rect(8,5).Where(c=>c.y>2||c.x>=3&&c.x<=4).ToHashSet(),
                    Rect(7,7).Where(c=>c.x>=2&&c.x<=3||c.y>=2&&c.y<=3).ToHashSet(),
                    Rect(8,5).Where(c=>c.y<=c.x/2).ToHashSet(),
                    Rect(8,6).Where(c=>!(c.x>=2&&c.x<=5&&c.y>=2)).ToHashSet(),
                    new HashSet<Vector2Int>{Vector2Int.zero,Vector2Int.one,new Vector2Int(2,2)},
                    Rect(5,4).Where(c=>c!=new Vector2Int(2,2)).Select(c=>c-new Vector2Int(5,7)).ToHashSet()
                };
                for(int i=0;i<shapes.Count;i++) {
                    var layer=new GridArtLayer {id="connection_case_"+i,label="连接样例 "+i};
                    foreach(var c in shapes[i]) layer.Set(c+(i==11?Vector2Int.zero:new Vector2Int(i%4*22,54-i/4*20)),0);
                    map.layers.Add(layer);
                }
                var cross=new GridArtLayer {id="cross_chunk",label="跨 x31/32 · 刻意孔洞"};
                for(int y=0;y<4;y++) for(int x=-2;x<35;x++) if(!(x>=29&&x<=32&&y>=1&&y<=2)) cross.Set(new Vector2Int(x,y),0);
                map.layers.Add(cross);
                AssetDatabase.CreateAsset(map,Folder+"/ConnectionMap.asset");
                var ownership=ScriptableObject.CreateInstance<GridArtRegionOwnership>();
                AssetDatabase.CreateAsset(ownership,Folder+"/ConnectionOwnership.asset");
                var camera=new GameObject("连接检查相机").AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=39;
                camera.transform.position=new Vector3(41,29,-20); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(26/255f,42/255f,54/255f); camera.tag="MainCamera";
                EditorSceneManager.SaveScene(scene,path);
                var output=GridArtBuilder.Apply(map,scene,false,true); output.SetCamera(camera); output.gameObject.AddComponent<GridArtRegionOwnershipLink>().SetOwnership(ownership);
                AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
                ExportCamera(scene,"Library/MossConnectionsPreview.png",1536,1248); EditorApplication.Exit(0);
            } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void ExportCamera(Scene scene,string output,int width,int height)
        {
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).First();
            var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32); texture.Create();
            var previous=RenderTexture.active; var oldTarget=camera.targetTexture;
            var png=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try {
                camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture;
                png.ReadPixels(new Rect(0,0,width,height),0,0); png.Apply(); File.WriteAllBytes(output,png.EncodeToPNG());
            } finally { camera.targetTexture=oldTarget; RenderTexture.active=previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(png); }
        }
    }
}
