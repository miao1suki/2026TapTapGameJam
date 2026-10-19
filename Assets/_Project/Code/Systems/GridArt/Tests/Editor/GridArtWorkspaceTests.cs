using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Project.GridArt.Editor;
using Object = UnityEngine.Object;

namespace Project.GridArt.Tests
{
    public sealed class GridArtWorkspaceTests
    {
        private string folder;
        private Scene scene;
        private GridArtMap map;
        private GridArtPalette library;
        private SceneSetup[] previous;

        [SetUp] public void Setup()
        {
            previous = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode && Enumerable.Range(0,SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty || string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
                Assert.Ignore("请先保存打开的场景，或在隔离验证项目运行此资产测试。");
            folder = "Assets/__SceneArtTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, folder + "/A.unity");
            library = ScriptableObject.CreateInstance<GridArtPalette>(); AssetDatabase.CreateAsset(library, folder + "/Library.asset");
            map = ScriptableObject.CreateInstance<GridArtMap>(); map.palette = library; map.layers.Add(new GridArtLayer());
            AssetDatabase.CreateAsset(map, folder + "/Map.asset");
        }
        [TearDown] public void Cleanup()
        {
            Undo.ClearAll();
            if (scene.IsValid()) AssetDatabase.DeleteAsset("Assets/_Project/Art/Generated/GridArt/" + AssetDatabase.AssetPathToGUID(scene.path));
            if (folder != null)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
                if (previous != null && previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
        private Sprite Sprite(string name, int width = 8)
        {
            var texture = new Texture2D(width, 8); texture.name = name;
            AssetDatabase.CreateAsset(texture, folder + "/" + name + ".asset");
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0,0,width,8), Vector2.one*.5f, 8);
            sprite.name = name; AssetDatabase.AddObjectToAsset(sprite, texture); return sprite;
        }
        private void AddBrush() => GridArtLibraryService.Add(library, Sprite("Tile"), Vector2.one, new Rect(0,0,1,1), "地面");
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target,args);
        private static void Field(object target, string name, object value) => target.GetType().GetField(name,System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target,value);
        private GridArtWindow Window()
        { var w=ScriptableObject.CreateInstance<GridArtWindow>(); w.CreateGUI(); w.SetMap(map); return w; }

        [Test] public void PanAndZoomReuseArtworkGeometryAndKeepOverlayAboveAllLayers()
        {
            AddBrush();
            for (int x=0; x<20; x++) map.layers[0].Set(new Vector2Int(x,-2),0);
            var overlayLayer = new GridArtLayer { tint = new Color(1,1,1,.5f), sortingOrder=10 };
            overlayLayer.Set(new Vector2Int(0,-2),0); map.layers.Add(overlayLayer);
            var w = Window();
            try
            {
                var canvas = (VisualElement)w.GetType().GetField("canvas",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(w);
                var artwork = canvas.Q("artwork-cache"); var overlay = canvas.Q("art-overlay");
                Assert.That(artwork.childCount,Is.GreaterThan(0));
                Assert.That(canvas.IndexOf(overlay),Is.GreaterThan(canvas.IndexOf(artwork)));
                var originalBatches = artwork.Children().ToArray();
                string data = EditorJsonUtility.ToJson(map);
                Field(canvas,"pan",new Vector2(-100,380)); Field(canvas,"zoom",5f);
                canvas.GetType().GetMethod("RepaintView").Invoke(canvas,null);
                Assert.That(artwork.Children().ToArray(),Is.EqualTo(originalBatches));
                Assert.That(artwork.style.translate.value.x.value,Is.EqualTo(-100));
                Assert.That(artwork.style.scale.value.value.x,Is.EqualTo(5f/32).Within(.0001f));
                Assert.That(EditorJsonUtility.ToJson(map),Is.EqualTo(data));
                map.layers[0].Set(new Vector2Int(30,0),0);
                canvas.GetType().GetMethod("Invalidate").Invoke(canvas,null);
                Assert.That(artwork.Children().First(),Is.Not.SameAs(originalBatches[0]));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test] public void RetainedArtworkPreservesTintLayerOrderUvsAndParallaxOffsets()
        {
            AddBrush(); map.layers[0].sortingOrder=-100; map.layers[0].Set(new Vector2Int(-3,2),0);
            var top = new GridArtLayer { sortingOrder=20, tint=new Color(1,.5f,.25f,.3f), parallax=Vector2.one*.5f };
            top.Set(new Vector2Int(-3,2),0); map.layers.Add(top);
            var w=Window();
            try
            {
                Field(w,"previewTravel",new Vector2(4,2));
                var canvas=(VisualElement)w.GetType().GetField("canvas",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(w);
                canvas.GetType().GetMethod("Invalidate").Invoke(canvas,null);
                var quads=(System.Collections.Generic.List<(GridArtQuad,Color)>)canvas.GetType()
                    .GetField("quads",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(canvas);
                Assert.That(quads.Count,Is.EqualTo(2));
                Assert.That(quads[0].Item1.bounds.position,Is.EqualTo(new Vector2(-3,2)));
                Assert.That(quads[1].Item1.bounds.position,Is.EqualTo(new Vector2(-1,3)));
                Assert.That(quads[1].Item2,Is.EqualTo(top.tint));
                Assert.That(quads[1].Item1.uv,Is.EqualTo(quads[0].Item1.uv));
                top.visible=false; canvas.GetType().GetMethod("Invalidate").Invoke(canvas,null);
                quads=(System.Collections.Generic.List<(GridArtQuad,Color)>)canvas.GetType()
                    .GetField("quads",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(canvas);
                Assert.That(quads.Count,Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator AttachedCanvasPanAndZoomKeepArtworkAlignedWithoutRetessellatingTiles()
        {
            AddBrush(); map.layers[0].Set(new Vector2Int(-3,2),0);
            var w=Window();
            try
            {
                w.position=new Rect(20,20,1200,800); w.Show();
                for(int i=0;i<5;i++) yield return null;
                var canvas=(VisualElement)w.GetType().GetField("canvas",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(w);
                Field(canvas,"pan",new Vector2(240,320)); Field(canvas,"zoom",32f);
                canvas.GetType().GetMethod("RepaintView").Invoke(canvas,null); w.Repaint();
                for(int i=0;i<5;i++) yield return null;
                var artwork=canvas.Q("artwork-cache"); var batch=artwork.Children().Single();
                int rebuilds=0; batch.generateVisualContent+=context=>rebuilds++;
                batch.MarkDirtyRepaint(); w.Repaint();
                for(int i=0;i<5;i++) yield return null;
                Assert.That(rebuilds,Is.GreaterThan(0),"visible artwork must really render before measuring its reuse");
                rebuilds=0;
                Field(canvas,"pan",new Vector2(260,340)); Field(canvas,"zoom",16f);
                canvas.GetType().GetMethod("RepaintView").Invoke(canvas,null); w.Repaint();
                for(int i=0;i<5;i++) yield return null;
                Vector2 actual=canvas.WorldToLocal(batch.LocalToWorld(Vector2.zero));
                Assert.That(actual.x,Is.EqualTo(260-3*16).Within(.05f));
                Assert.That(actual.y,Is.EqualTo(340-3*16).Within(.05f));
                Assert.That(batch.resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
                Assert.That(rebuilds,Is.Zero,"pan/zoom should retain tile geometry, not redraw it each frame");
                Field(canvas,"pan",new Vector2(-10000,-10000));
                canvas.GetType().GetMethod("RepaintView").Invoke(canvas,null);
                for(int i=0;i<3;i++) yield return null;
                Assert.That(batch.resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
                Assert.That(map.layers[0].cells.Single().position,Is.EqualTo(new Vector2Int(-3,2)));
            }
            finally { w.Close(); }
        }
        private static void ToggleValue(Toggle toggle, bool value)
        {
            bool before=toggle.value; toggle.SetValueWithoutNotify(value);
            using (var evt=ChangeEvent<bool>.GetPooled(before,value)) { evt.target=toggle; toggle.SendEvent(evt); }
        }

        [Test] public void LayersCanShareCellWithoutReplacingBaseAndKeepIndependentVisibility()
        {
            AddBrush();
            GridArtLibraryService.Add(library,Sprite("Overlay"),Vector2.one,new Rect(0,0,1,1),"装饰");
            var baseLayer=map.layers[0]; baseLayer.sortingOrder=-100; baseLayer.Set(Vector2Int.zero,0);
            var overlay=new GridArtLayer { label="草边",depth=-.55f,sortingOrder=-80,tint=new Color(1,1,1,.5f) };
            overlay.Set(Vector2Int.zero,1); map.layers.Add(overlay);
            var output=GridArtBuilder.Apply(map,scene,false);
            Assert.That(baseLayer.cells.Single().brush,Is.Zero);
            Assert.That(overlay.cells.Single().brush,Is.EqualTo(1));
            var renderers=output.GetComponentsInChildren<MeshRenderer>();
            Assert.That(renderers.Length,Is.EqualTo(2));
            Assert.That(renderers.Select(r=>r.sortingOrder),Is.EquivalentTo(new[]{-100,-80}));
            Assert.That(renderers.Single(r=>r.sortingOrder==-80).sharedMaterial.GetColor("_BaseColor").a,Is.EqualTo(.5f));
            overlay.visible=false; GridArtBuilder.Apply(map,scene,false);
            Assert.That(output.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(1));
            Assert.That(overlay.cells.Single().brush,Is.EqualTo(1));
            overlay.visible=true; overlay.Erase(Vector2Int.zero,library); GridArtBuilder.Apply(map,scene,false);
            Assert.That(baseLayer.cells.Single().brush,Is.Zero);
            Assert.That(GridArtBuilder.BuildQuads(baseLayer,library).Count,Is.EqualTo(1));
        }

        [Test] public void AutosaveDefaultsOnAndDoesNotSaveUnrelatedSceneOrAssetChanges()
        {
            AddBrush(); var other=ScriptableObject.CreateInstance<GridArtMap>(); AssetDatabase.CreateAsset(other,folder+"/Other.asset");
            EditorUtility.SetDirty(other); var cube=new GameObject("尚未保存的玩法调整");
            var w=Window();
            try
            {
                Assert.That(w.rootVisualElement.Q<Toggle>("art-autosave").value,Is.True);
                map.layers[0].Set(Vector2Int.zero,0); EditorUtility.SetDirty(map); Invoke(w,"ArtChanged",false);
                Invoke(w,"FlushPending",false);
                Assert.That(EditorUtility.IsDirty(map),Is.False); Assert.That(EditorUtility.IsDirty(other),Is.True);
                Assert.That(scene.isDirty,Is.True); Assert.That(System.IO.File.ReadAllText(scene.path),Does.Not.Contain("尚未保存的玩法调整"));
                Assert.That(System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(map)),Does.Contain("brush: 0"));
            }
            finally { Object.DestroyImmediate(w); }
        }
        [Test] public void ClosingFlushesMapAndReopeningRestoresWithoutSavingWholeScene()
        {
            AddBrush(); var w=Window(); map.ownerSceneGuid=AssetDatabase.AssetPathToGUID(scene.path);
            map.layers[0].Set(new Vector2Int(-5,7),0); EditorUtility.SetDirty(map); Invoke(w,"ArtChanged",false);
            Object.DestroyImmediate(w);
            Assert.That(System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(map)),Does.Contain("x: -5"));
            w=ScriptableObject.CreateInstance<GridArtWindow>();
            try { w.CreateGUI(); Assert.That(w.Map,Is.SameAs(map)); Assert.That(w.Map.layers[0].cells.Single().position,Is.EqualTo(new Vector2Int(-5,7))); }
            finally { Object.DestroyImmediate(w); }
        }
        [Test] public void DisablingAutosaveKeepsEditsDirtyUntilExplicitSave()
        {
            AddBrush(); var w=Window(); w.Show();
            try
            {
                ToggleValue(w.rootVisualElement.Q<Toggle>("art-autosave"),false);
                Assert.That((bool)typeof(GridArtWindow).GetField("autoSave",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(w),Is.False,"自动保存开关事件");
                map.layers[0].Set(Vector2Int.zero,0); EditorUtility.SetDirty(map); Invoke(w,"ArtChanged",false); Invoke(w,"FlushPending",false);
                Assert.That(EditorUtility.IsDirty(map),Is.True);
                Assert.That(System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(map)),Does.Not.Contain("brush: 0"));
                ToggleValue(w.rootVisualElement.Q<Toggle>("art-autosave"),true); Invoke(w,"FlushPending",false);
                Assert.That(EditorUtility.IsDirty(map),Is.False);
            }
            finally { Object.DestroyImmediate(w); }
        }
        [Test] public void AutosaveCoalescesBurstAndIdleTickDoesNoSourceScans()
        {
            AddBrush(); var original=GridArtLevelSource.Read; int scans=0;
            GridArtLevelSource.Read=s=>{ scans++; return new GridArtLevelSnapshot(); }; var w=Window();
            try
            {
                Invoke(w,"RefreshLevelSource"); int reads=scans;
                var sidebar=w.rootVisualElement.Q<Image>("art-current-preview");
                for(int i=0;i<100;i++) { map.layers[0].Set(new Vector2Int(i,0),0); EditorUtility.SetDirty(map); Invoke(w,"ArtChanged",false); Invoke(w,"AutomationTick"); }
                Assert.That(scans,Is.EqualTo(reads)); Assert.That(w.rootVisualElement.Q<Image>("art-current-preview"),Is.SameAs(sidebar));
                Assert.That(System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(map)),Does.Not.Contain("brush: 0"));
                Invoke(w,"FlushPending",false); string disk=System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(map));
                for(int i=0;i<100;i++) Invoke(w,"AutomationTick");
                Assert.That(scans,Is.EqualTo(reads)); Assert.That(System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(map)),Is.EqualTo(disk));
            }
            finally { Object.DestroyImmediate(w); GridArtLevelSource.Read=original; }
        }
        [Test] public void FollowingLevelAddsDeletesMovesAndKeepsDecorations()
        {
            AddBrush(); var original=GridArtLevelSource.Read; var snapshot=new GridArtLevelSnapshot();
            var group=new GridArtLevelGroup { id="room",label="房间A" }; group.bounds.Add(new Rect(0,0,2,1)); snapshot.groups.Add(group);
            GridArtLevelSource.Read=s=>snapshot;
            var layer=map.layers[0]; layer.followLevel=true; layer.sourceGroup="room"; layer.sourceBrush=0;
            map.layers.Add(new GridArtLayer()); map.layers[1].Set(Vector2Int.zero,0); var w=Window();
            try
            {
                Invoke(w,"RefreshLevelSource"); Assert.That(layer.cells.Count,Is.EqualTo(2));
                group.bounds.Clear(); group.bounds.Add(new Rect(-4,2,3,1)); Invoke(w,"RefreshLevelSource");
                CollectionAssert.AreEquivalent(new[]{new Vector2Int(-4,2),new Vector2Int(-3,2),new Vector2Int(-2,2)},layer.cells.Select(c=>c.position));
                Assert.That(map.layers[1].cells.Count,Is.EqualTo(1));
                snapshot.groups.Clear(); Invoke(w,"RefreshLevelSource"); Assert.That(layer.cells.Count,Is.Zero);
                Assert.That(map.layers[1].cells.Count,Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(w); GridArtLevelSource.Read=original; }
        }
        [Test] public void LevelSourceScopesDeduplicateMergedWhiteBlocks()
        {
            var snapshot=new GridArtLevelSnapshot(); var a=new GridArtLevelGroup {id="a"}; var b=new GridArtLevelGroup {id="b"};
            a.bounds.Add(new Rect(-2,-1,4,1)); b.bounds.Add(new Rect(1,-1,2,1)); snapshot.groups.Add(a); snapshot.groups.Add(b);
            Assert.That(snapshot.Cells("",1).Count,Is.EqualTo(5)); Assert.That(snapshot.Cells("a",1).Count,Is.EqualTo(4));
            Assert.That(snapshot.Cells("missing",1).Count,Is.Zero);
        }
        [Test] public void PreviewReusesUnchangedChunkObjectsAndSupportsUndoAfterAutoApply()
        {
            AddBrush(); map.layers[0].Set(Vector2Int.zero,0); map.layers[0].Set(new Vector2Int(40,0),0);
            var output=GridArtBuilder.Apply(map,scene,false); var stable=output.GetComponentsInChildren<MeshFilter>().Single(f=>f.sharedMesh.bounds.center.x>32);
            string stablePath=AssetDatabase.GetAssetPath(stable.sharedMesh); var write=System.IO.File.GetLastWriteTimeUtc(stablePath);
            Undo.ClearAll(); Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(map,"手绘一格");
            map.layers[0].Set(Vector2Int.right,0); map.revision++; var w=Window();
            try
            {
                Invoke(w,"ArtChanged",false); Invoke(w,"FlushPending",false);
                Assert.That(stable,Is.Not.Null); Assert.That(System.IO.File.GetLastWriteTimeUtc(stablePath),Is.EqualTo(write));
                Undo.PerformUndo(); Invoke(w,"FlushPending",false);
                Assert.That(map.layers[0].cells.Count,Is.EqualTo(2));
                Assert.That(output.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount),Is.EqualTo(8));
                Undo.PerformRedo(); Invoke(w,"FlushPending",false); Assert.That(map.layers[0].cells.Count,Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(w); }
        }
        [Test] public void SingleCellEditingOnLargeMapHasConstantLookupAndStableNoopSource()
        {
            AddBrush(); var layer=map.layers[0]; var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int y=0;y<100;y++) for(int x=0;x<100;x++) layer.Set(new Vector2Int(x,y),0);
            double build=timer.Elapsed.TotalMilliseconds; timer.Restart();
            for(int i=0;i<10000;i++) Assert.That(layer.Set(new Vector2Int(i%100,i/100),0),Is.False);
            Debug.Log("GridArt 10000格创建="+build.ToString("0.00")+"ms；10000次重复落笔="+timer.Elapsed.TotalMilliseconds.ToString("0.00")+"ms");
            layer.sourceBrush=0; var existing=layer.cells[0];
            Assert.That(GridArtLevelSource.Synchronize(layer,new System.Collections.Generic.HashSet<Vector2Int>(layer.cells.Select(c=>c.position))),Is.False);
            Assert.That(layer.cells[0],Is.SameAs(existing));
            timer.Restart();
            for(int i=0;i<10000;i++) Assert.That(layer.Erase(new Vector2Int(i%100,i/100),library),Is.True);
            Debug.Log("GridArt 10000格擦除="+timer.Elapsed.TotalMilliseconds.ToString("0.00")+"ms"); Assert.That(layer.cells.Count,Is.Zero);
        }
        [Test] public void EraserHandlesOverlappingLargeStampsAfterIndexedRemovals()
        {
            AddBrush(); library.brushes.Add(new GridArtBrush{size=new Vector2Int(3,2)});
            var layer=map.layers[0]; layer.Set(Vector2Int.zero,1); layer.Set(Vector2Int.one,0); layer.Set(new Vector2Int(9,9),0);
            Assert.That(layer.Erase(Vector2Int.one,library),Is.True);
            Assert.That(layer.cells.Count,Is.EqualTo(1)); Assert.That(layer.Find(new Vector2Int(9,9)),Is.Zero);
            layer.Set(new Vector2Int(-3,-2),1); Assert.That(layer.Erase(new Vector2Int(-1,-1),library),Is.True);
            Assert.That(layer.cells.Count,Is.EqualTo(1));
        }
        [Test] public void ExternalDiskChangeIsNotOverwrittenByAutosave()
        {
            AddBrush(); var w=Window(); string path=AssetDatabase.GetAssetPath(map);
            try
            {
                map.layers[0].Set(Vector2Int.zero,0); EditorUtility.SetDirty(map); Invoke(w,"ArtChanged",false);
                string remote=System.IO.File.ReadAllText(path)+"\n# external version\n"; System.IO.File.WriteAllText(path,remote);
                Invoke(w,"FlushPending",false);
                Assert.That(System.IO.File.ReadAllText(path),Is.EqualTo(remote));
                Assert.That(w.rootVisualElement.Query<Label>().ToList().Any(l=>l.text.Contains("外部更新")),Is.True);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test] public void UnionUsesMultipleSheetsWithoutReorderingOrDuplicates()
        {
            var a = Sprite("A"); var b = Sprite("B", 16);
            Assert.That(GridArtLibraryService.Add(library,a,Vector2.one,new Rect(0,0,1,1),"地面"), Is.True);
            string key = library.Get(0).sourceKey;
            Assert.That(GridArtLibraryService.Add(library,b,new Vector2(2,1),new Rect(0,0,1,1),"装饰"), Is.True);
            Assert.That(GridArtLibraryService.Add(library,a,Vector2.one,new Rect(0,0,1,1),"其他"), Is.False);
            Assert.That(library.brushes.Count, Is.EqualTo(2)); Assert.That(library.Get(0).sourceKey, Is.EqualTo(key));
            Assert.That(library.Get(1).Size, Is.EqualTo(new Vector2Int(2,1)));
        }
        [Test] public void SharedMigrationRemapsEveryLayerAndKeepsSourceUntouched()
        {
            AddBrush(); map.layers[0].Set(Vector2Int.zero,0); map.layers.Add(new GridArtLayer()); map.layers[1].Set(Vector2Int.one,0);
            var other = ScriptableObject.CreateInstance<GridArtPalette>(); AssetDatabase.CreateAsset(other, folder + "/Other.asset");
            other.brushes.Add(new GridArtBrush { label = "原有素材" });
            GridArtLibraryService.Connect(map,other);
            Assert.That(map.palette, Is.SameAs(other)); Assert.That(other.brushes.Count, Is.EqualTo(2));
            Assert.That(map.layers.All(l => l.cells[0].brush == 1), Is.True);
            Assert.That(library.brushes.Count, Is.EqualTo(1));
            GridArtLibraryService.Connect(map,other); Assert.That(other.brushes.Count, Is.EqualTo(2));
        }
        [Test] public void FractionalSizeAndCutPaddingAreRespected()
        {
            var sprite = Sprite("Strip", 16);
            GridArtLibraryService.Add(library, sprite, new Vector2(2,.25f), new Rect(.25f,0,.5f,1), "草边");
            map.layers[0].Set(Vector2Int.zero,0);
            var q = GridArtBuilder.BuildQuads(map.layers[0],library).Single();
            Assert.That(q.bounds.size, Is.EqualTo(new Vector2(2,.25f)));
            Assert.That(q.uv, Is.EqualTo(new Rect(.25f,0,.5f,1)));
        }
        [Test] public void MergedAndUnmergedBoundsGiveIdenticalCoverage()
        {
            var singles = new[] { new Bounds(new Vector3(.5f,.5f,0),Vector3.one),new Bounds(new Vector3(1.5f,.5f,0),Vector3.one) };
            var merged = new[] { new Bounds(new Vector3(1,.5f,0),new Vector3(2,1,1)) };
            CollectionAssert.AreEquivalent(GridArtGeometry.Cells(singles,1),GridArtGeometry.Cells(merged,1));
            Assert.That(GridArtGeometry.Cells(singles.Concat(singles),1).Count, Is.EqualTo(2));
        }
        [Test] public void GeometrySupportsNegativeCoordinatesAndCapsLargeFill()
        {
            var result = GridArtGeometry.Cells(new[] {new Bounds(new Vector3(-.5f,-.5f,0),Vector3.one)},1);
            CollectionAssert.AreEquivalent(new[] {new Vector2Int(-1,-1)},result);
            Assert.Throws<InvalidOperationException>(()=> GridArtGeometry.Cells(new[] {new Bounds(Vector3.zero,Vector3.one*10000)},1));
        }
        [Test] public void SceneApplyBatchesTexturesAndDoesNotTouchGameplay()
        {
            AddBrush(); GridArtLibraryService.Add(library,Sprite("Second"),Vector2.one,new Rect(0,0,1,1),"装饰");
            map.layers[0].Set(Vector2Int.zero,0); map.layers[0].Set(Vector2Int.right,1);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(cube,scene);
            var material = cube.GetComponent<Renderer>().sharedMaterial;
            var output = GridArtBuilder.Apply(map,scene);
            Assert.That(output.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(2));
            Assert.That(cube.GetComponent<Renderer>().sharedMaterial,Is.SameAs(material));
            Assert.That(cube.GetComponent<BoxCollider>(),Is.Not.Null);
            Assert.That(output.GetComponentsInChildren<Collider>().Length,Is.Zero);
            foreach (var filter in output.GetComponentsInChildren<MeshFilter>())
            { Assert.That(filter.sharedMesh.normals.Length,Is.EqualTo(4)); Assert.That(filter.sharedMesh.tangents.Length,Is.EqualTo(4)); }
        }
        [Test] public void SavedSceneOwnershipPreventsCrossLevelEdits()
        {
            AddBrush(); map.layers[0].Set(Vector2Int.zero,0); GridArtBuilder.Apply(map,scene);
            var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                EditorSceneManager.SaveScene(other, folder + "/B.unity");
                Assert.Throws<InvalidOperationException>(() => GridArtBuilder.Apply(map,other));
                var copy = Object.Instantiate(map); copy.ownerSceneGuid = "";
                AssetDatabase.CreateAsset(copy,folder + "/Copy.asset");
                Assert.That(GridArtBuilder.Apply(copy,other),Is.Not.Null);
                Assert.That(copy.ownerSceneGuid,Is.Not.EqualTo(map.ownerSceneGuid));
            }
            finally { EditorSceneManager.CloseScene(other,true); }
        }
        [Test] public void InvalidNormalAtlasFailsBeforeReplacingOutput()
        {
            AddBrush(); map.layers[0].Set(Vector2Int.zero,0); var output = GridArtBuilder.Apply(map,scene);
            int count = output.transform.childCount;
            var bad = new Texture2D(16,16);
            try { library.Get(0).normalMap = bad; Assert.Throws<InvalidOperationException>(() => GridArtBuilder.Apply(map,scene)); Assert.That(output.transform.childCount,Is.EqualTo(count)); }
            finally { Object.DestroyImmediate(bad); }
        }
        [Test] public void LitToUnlitClearsObsoleteShaderKeywords()
        {
            AddBrush(); map.layers[0].Set(Vector2Int.zero,0); map.layers[0].lighting3D = true;
            var output = GridArtBuilder.Apply(map,scene);
            Assert.That(output.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
            map.layers[0].lighting3D = false; output = GridArtBuilder.Apply(map,scene);
            var material = output.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.That(material.shader.name,Is.EqualTo("2026TapTap/Grid Art/Unlit"));
            Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),Is.False);
        }
        [Test] public void ToolkitWorkspaceHasPreviewLayersAndNoImgui()
        {
            AddBrush(); var window = ScriptableObject.CreateInstance<GridArtWindow>();
            try
            {
                window.CreateGUI(); window.SetMap(map);
                Assert.That(window.rootVisualElement.Q<IMGUIContainer>(),Is.Null);
                Assert.That(window.rootVisualElement.Q<Toggle>("scene-paint"),Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Image>("art-current-preview"),Is.Not.Null);
                Assert.That(window.rootVisualElement.Query<Button>().ToList().Any(b=> b.text == "接入共享素材库"),Is.True);
                window.Refresh(); Assert.That(map.layers[0].cells.Count,Is.Zero);
            }
            finally { Object.DestroyImmediate(window); }
        }
        [Test] public void ScenePainterCancellationRevertsWholeStroke()
        {
            AddBrush(); var window = ScriptableObject.CreateInstance<GridArtWindow>();
            try
            {
                window.CreateGUI(); window.SetMap(map); GridArtScenePainter.SetTarget(window);
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(map,"取消测试笔画"); map.layers[0].Set(Vector2Int.zero,0); map.layers[0].Set(Vector2Int.one,0);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                typeof(GridArtScenePainter).GetField("group",flags).SetValue(null,group);
                typeof(GridArtScenePainter).GetField("dragging",flags).SetValue(null,true);
                typeof(GridArtScenePainter).GetField("changed",flags).SetValue(null,true);
                GridArtScenePainter.SetTarget(null);
                Assert.That(map.layers[0].cells.Count,Is.Zero); Assert.That(GridArtScenePainter.IsPainting,Is.False);
            }
            finally { GridArtScenePainter.SetTarget(null); Object.DestroyImmediate(window); }
        }
        [Test] public void SceneApplyUndoRedoRestoresGeneratedHierarchyAndMap()
        {
            AddBrush(); map.layers[0].Set(Vector2Int.zero,0); var output=GridArtBuilder.Apply(map,scene);
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(map,"新增美术笔画");
            map.layers[0].Set(Vector2Int.right,0); map.revision++; GridArtBuilder.Apply(map,scene);
            Undo.CollapseUndoOperations(group); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(map.layers[0].cells.Count,Is.EqualTo(1));
            Assert.That(output.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount,Is.EqualTo(4));
            Undo.PerformRedo(); Assert.That(map.layers[0].cells.Count,Is.EqualTo(2));
            Assert.That(output.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount,Is.EqualTo(8));
        }
        [Test] public void OutputSurvivesSceneSaveAndReload()
        {
            AddBrush(); map.layers[0].Set(Vector2Int.zero,0); GridArtBuilder.Apply(map,scene); EditorSceneManager.SaveScene(scene);
            string path=scene.path; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene=EditorSceneManager.OpenScene(path); var output=scene.GetRootGameObjects().Select(g=>g.GetComponent<GridArtScene>()).First(g=>g!=null);
            Assert.That(output.map,Is.SameAs(map));
            Assert.That(AssetDatabase.Contains(output.GetComponentInChildren<MeshFilter>().sharedMesh),Is.True);
            Assert.That(AssetDatabase.Contains(output.GetComponentInChildren<MeshRenderer>().sharedMaterial),Is.True);
        }
        [Test] public void RegeneratingSlicedSpritesRepairsReferencesWithoutChangingBrushSlots()
        {
            string sourcePath=folder+"/Source.png";
            var texture=new Texture2D(16,512); var pixels=new Color[16*512];
            for(int y=4;y<12;y++) for(int x=4;x<12;x++) pixels[y*16+x]=Color.green;
            texture.SetPixels(pixels); texture.Apply();
            System.IO.File.WriteAllBytes(sourcePath,texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(sourcePath);
            void Generate() => Assert.That(Project.SurfaceTiles.Editor.SurfaceTileEditorBridge.GeneratePaletteFromSheet(sourcePath,
                folder+"/Cut","Tiles",2,1,0,0,16,16,8,false,out string message),Is.True,message);
            Generate();
            var tiles=AssetDatabase.FindAssets("t:SurfaceTilePalette",new[] {folder}).Select(g=>AssetDatabase.LoadAssetAtPath<Project.SurfaceTiles.SurfaceTilePalette>(AssetDatabase.GUIDToAssetPath(g))).Single();
            Assert.That(GridArtLibraryService.Import(library,tiles,8),Is.EqualTo(1));
            string key=library.Get(0).sourceKey; int count=library.brushes.Count;
            map.layers[0].Set(Vector2Int.zero,0); Generate();
            GridArtLibraryService.Import(library,tiles,8);
            Assert.That(library.brushes.Count,Is.EqualTo(count)); Assert.That(library.Get(0).sourceKey,Is.EqualTo(key));
            Assert.That(library.Get(0).sprite,Is.SameAs(tiles.Tiles[0].Sprite));
            Assert.That(GridArtBuilder.BuildQuads(map.layers[0],library).Count,Is.EqualTo(1));
        }

        [Test] public void ColoredArtSplitsSameAtlasAndRestoresAlphaMasksAfterSceneReload()
        {
            var previousColors=GridArtColorSource.Read;
            GridArtColorSource.Read=()=>new[] { new GridArtColorDefinition("red","红色",8),new GridArtColorDefinition("blue","蓝色",10) };
            try
            {
                AddBrush();
                library.brushes[0].colorId="red";
                library.brushes.Add(new GridArtBrush { sprite=library.brushes[0].sprite,colorId="blue",visualSize=Vector2.one });
                map.layers[0].Set(Vector2Int.zero,0); map.layers[0].Set(Vector2Int.right,1);
                map.layers[0].tint=new Color(1,1,1,.35f);
                EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
                var output=GridArtBuilder.Apply(map,scene,false);
                Assert.That(output.GetComponentsInChildren<MeshRenderer>().Select(r=>r.gameObject.layer).OrderBy(i=>i),Is.EqualTo(new[] {8,10}));
                EditorSceneManager.SaveScene(scene);
                string path=scene.path;
                scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                output=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridArtScene>()).Single();
                // EditMode must not dispatch MonoBehaviour messages through Unity's
                // play-mode scheduler. Invoke only the restored initialization hook.
                Invoke(output,"OnEnable");
                var renderers=output.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers.Length,Is.EqualTo(2),"Two colors in one atlas must not share one layer mesh.");
                foreach (var renderer in renderers)
                {
                    var mask=new MaterialPropertyBlock(); renderer.GetPropertyBlock(mask);
                    Assert.That(mask.GetFloat("_ColorMaskGroup"),Is.EqualTo(-1f));
                    Assert.That(mask.GetFloat("_ColorMaskUseVertexAlpha"),Is.EqualTo(0f));
                    Assert.That(mask.GetFloat("_ColorMaskReal"),Is.EqualTo(1f));
                    Assert.That(mask.GetFloat("_ColorMaskOpacity"),Is.EqualTo(.35f).Within(.0001f));
                    Assert.That(mask.GetTexture("_ColorMaskAlphaTexture"),Is.SameAs(renderer.sharedMaterial.GetTexture("_BaseMap")));
                }
            }
            finally { GridArtColorSource.Read=previousColors; }
        }

        [Test] public void SupplementaryDecorationLibraryIsAppendOnlyAndPreservesEdgePixels()
        {
            AddBrush();
            GridArtBrush original=library.brushes[0]; string originalKey=original.sourceKey;
            Assert.That(GridArtSupplementLibrary.Import(library),Is.EqualTo(96));
            Assert.That(library.brushes.Count,Is.EqualTo(97));
            Assert.That(library.brushes[0],Is.SameAs(original));
            Assert.That(library.brushes[0].sourceKey,Is.EqualTo(originalKey));
            var imported=library.brushes.Skip(1).ToArray();
            Assert.That(imported.Select(b=>b.colorId).Distinct(),Is.EquivalentTo(new[] {"red","blue","green"}));
            Assert.That(imported.Count(b=>b.category=="雨滴原图"),Is.EqualTo(12));
            Assert.That(imported.Count(b=>b.category=="地面草饰·拆分片"),Is.EqualTo(24));
            Assert.That(imported.Count(b=>b.category=="地面草饰·组合"),Is.EqualTo(12));
            Assert.That(imported.Count(b=>b.category=="墙面波纹"),Is.EqualTo(6));
            Assert.That(imported.All(b=>AssetDatabase.Contains(b.sprite)),Is.True);
            var edge=imported.Where(b=>b.sourceKey.EndsWith("_8",StringComparison.Ordinal)).ToArray();
            Assert.That(edge.Length,Is.EqualTo(6));
            foreach (var brush in edge)
            {
                Assert.That(brush.sprite.rect.width,Is.EqualTo(15));
                Assert.That(brush.DisplaySize,Is.EqualTo(new Vector2(15f/16f,1f)));
                Assert.That(brush.sprite.rect.xMax,Is.EqualTo(brush.sprite.texture.width));
            }
            string[] keys=library.brushes.Select(b=>b.sourceKey).ToArray();
            Assert.That(GridArtSupplementLibrary.Import(library),Is.Zero);
            Assert.That(library.brushes.Select(b=>b.sourceKey),Is.EqualTo(keys));
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            string path=AssetDatabase.GetAssetPath(library);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var restored=AssetDatabase.LoadAssetAtPath<GridArtPalette>(path);
            Assert.That(restored.brushes.Select(b=>b.sourceKey),Is.EqualTo(keys));
            Assert.That(restored.brushes.Skip(1).All(b=>b.sprite!=null && AssetDatabase.Contains(b.sprite)),Is.True);
        }

        [Test] public void ProjectSceneSelectionsUseNativePixelsAndStableSlots()
        {
            AddBrush(); var first=library.brushes[0];
            Assert.That(GridArtProjectLibrary.Import(library),Is.GreaterThan(55));
            Assert.That(library.brushes[0],Is.SameAs(first));
            var imported=library.brushes.Skip(1).ToArray();
            Assert.That(imported.All(GridArtPixelLayout.IsNative),Is.True);
            Assert.That(imported.Single(b=>b.sourceKey.EndsWith("#scene:frame_whole")).DisplaySize,Is.EqualTo(Vector2.one*5));
            Assert.That(imported.Single(b=>b.sourceKey=="project-artwork:OneWayPlatform:idle").DisplaySize,Is.EqualTo(new Vector2(3,.5f)));
            Assert.That(imported.Single(b=>b.sourceKey=="project-artwork:MovingPlatform:idle").DisplaySize,Is.EqualTo(new Vector2(3,1)));
            Assert.That(imported.Single(b=>b.sourceKey=="project-artwork:RedCube:idle").colorId,Is.EqualTo("red"));
            string[] keys=library.brushes.Select(b=>b.sourceKey).ToArray();
            Assert.That(keys.Distinct().Count(),Is.EqualTo(keys.Length));
            Assert.That(GridArtProjectLibrary.Import(library),Is.Zero);
            Assert.That(library.brushes.Select(b=>b.sourceKey),Is.EqualTo(keys));
        }

        [Test] public void SupplementCorrectionsRetainCustomNamesAndLegacyUvs()
        {
            GridArtSupplementLibrary.Import(library);
            var grass=library.brushes.Single(b=>b.sourceKey.EndsWith("#RainRaw_red_0_0"));
            var sprite=grass.sprite; var size=grass.DisplaySize;
            grass.label="红色雨滴图案 01"; grass.category="雨滴原图";
            var custom=library.brushes.Single(b=>b.sourceKey.EndsWith("#RainRaw_red_0_1"));
            custom.label="策划自命名"; custom.category="我的素材";
            GridArtSupplementLibrary.Import(library);
            Assert.That(grass.label,Does.Contain("地面草饰")); Assert.That(grass.category,Is.EqualTo("地面草饰·拆分片"));
            Assert.That(grass.sprite,Is.SameAs(sprite)); Assert.That(grass.DisplaySize,Is.EqualTo(size));
            Assert.That(custom.label,Is.EqualTo("策划自命名")); Assert.That(custom.category,Is.EqualTo("我的素材"));
        }

        [TestCase(180,1)] [TestCase(200,2)] [TestCase(216,2)] [TestCase(310,3)]
        public void BrushCardsFitViewportWithoutHorizontalOverflow(float width,int columns)
        {
            Assert.That(GridArtBrushGrid.Columns(width),Is.EqualTo(columns));
            Assert.That((GridArtBrushGrid.CardWidth(width)+6)*columns,Is.LessThanOrEqualTo(width));
        }
    }
}
