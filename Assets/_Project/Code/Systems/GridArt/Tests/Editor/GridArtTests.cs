using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Project.GridArt.Editor;
using UnityEngine.UIElements;

namespace Project.GridArt.Tests
{
    public sealed class GridArtTests
    {
        [TestCase(false,false,false,GridArtCorner.OuterCorner)]
        [TestCase(true,false,false,GridArtCorner.HorizontalEdge)]
        [TestCase(false,true,false,GridArtCorner.VerticalEdge)]
        [TestCase(true,true,false,GridArtCorner.InnerCorner)]
        [TestCase(true,true,true,GridArtCorner.Fill)]
        public void RulesRespectDiagonal(bool h,bool v,bool d,GridArtCorner expected)
            => Assert.That(GridArtRules.Resolve(h,v,d),Is.EqualTo(expected));
        [Test]
        public void NegativeCoordinatesMatchWorldGrid()
        {
            var map=ScriptableObject.CreateInstance<GridArtMap>();
            try { map.cellSize=2; Assert.That(map.WorldToCell(new Vector3(-.1f,3.9f,0),Vector3.zero),Is.EqualTo(new Vector2Int(-1,1))); }
            finally { Object.DestroyImmediate(map); }
        }
        [Test]
        public void MultiCellStampErasesFromAnyCoveredCell()
        {
            var palette=ScriptableObject.CreateInstance<GridArtPalette>();
            try
            {
                palette.brushes.Add(new GridArtBrush{size=new Vector2Int(3,2)});
                var layer=new GridArtLayer(); layer.Set(new Vector2Int(-3,0),0);
                Assert.That(layer.Erase(new Vector2Int(-1,1),palette),Is.True);
                Assert.That(layer.cells.Count,Is.Zero);
            }
            finally { Object.DestroyImmediate(palette); }
        }
        [Test]
        public void ParallaxUsesAbsoluteReferenceWithoutDepthOrAccumulatedDrift()
        {
            Assert.That(GridArtScene.ParallaxOffset(new Vector3(10,8,999),new Vector2(.25f,.5f),0),Is.EqualTo(new Vector3(7.5f,4,0)));
            Assert.That(GridArtScene.ParallaxOffset(Vector3.one,Vector2.one,0),Is.EqualTo(Vector3.zero));
            Assert.That(GridArtScene.ParallaxOffset(new Vector3(.11f,0,0),Vector2.zero,.125f).x,Is.EqualTo(.125f));
        }
        [Test]
        public void NeighborDeletionAndMixedBrushesChangeCornerRules()
        {
            var palette=ScriptableObject.CreateInstance<GridArtPalette>();
            var texture=new Texture2D(64*13,64);
            var sprites=new Sprite[13];
            try
            {
                for(int i=0;i<13;i++) sprites[i]=Sprite.Create(texture,new Rect(i*64,0,64,64),Vector2.one*.5f);
                palette.brushes.Add(new GridArtBrush{autoTerrain=true,pieces=sprites});
                palette.brushes.Add(new GridArtBrush{autoTerrain=true,pieces=sprites});
                var layer=new GridArtLayer(); layer.Set(Vector2Int.zero,0); layer.Set(Vector2Int.right,0); layer.Set(Vector2Int.up,0);
                var quads=GridArtBuilder.BuildQuads(layer,palette);
                Assert.That(quads.Count,Is.EqualTo(12));
                Assert.That(quads[3].uv.x,Is.EqualTo((12.5f/13)).Within(.0001f)); // TR inner corner
                layer.Set(Vector2Int.one,0);
                Assert.That(GridArtBuilder.BuildQuads(layer,palette)[3].uv.x,Is.EqualTo(4.5f/13).Within(.0001f));
                layer.Set(Vector2Int.right,1);
                Assert.That(GridArtBuilder.BuildQuads(layer,palette)[3].uv.x,Is.EqualTo(5.5f/13).Within(.0001f));
                layer.Erase(Vector2Int.up,palette);
                Assert.That(GridArtBuilder.BuildQuads(layer,palette)[3].uv.x,Is.EqualTo(2.5f/13).Within(.0001f));
            }
            finally { foreach(var s in sprites) Object.DestroyImmediate(s); Object.DestroyImmediate(texture); Object.DestroyImmediate(palette); }
        }
        [Test]
        public void MapSerializesLayerIdentityAndBrushesWithoutInstanceIds()
        {
            var map=ScriptableObject.CreateInstance<GridArtMap>(); var clone=ScriptableObject.CreateInstance<GridArtMap>();
            try
            {
                var layer=new GridArtLayer(); layer.Set(new Vector2Int(-3,7),2); map.layers.Add(layer);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(map),clone);
                Assert.That(clone.layers[0].id,Is.EqualTo(layer.id)); Assert.That(clone.layers[0].cells[0].position,Is.EqualTo(new Vector2Int(-3,7)));
            }
            finally { Object.DestroyImmediate(map); Object.DestroyImmediate(clone); }
        }
        [Test]
        public void GestureUndoRestoresEntireMapAndCanRedo()
        {
            var map=ScriptableObject.CreateInstance<GridArtMap>(); map.layers.Add(new GridArtLayer());
            try
            {
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(map,"美术笔画测试");
                map.layers[0].Set(Vector2Int.zero,0); map.layers[0].Set(Vector2Int.one,0); map.revision++;
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Assert.That(map.layers[0].cells.Count,Is.Zero); Assert.That(map.revision,Is.Zero);
                Undo.PerformRedo(); Assert.That(map.layers[0].cells.Count,Is.EqualTo(2)); Assert.That(map.revision,Is.EqualTo(1));
            }
            finally { Undo.ClearUndo(map); Object.DestroyImmediate(map); }
        }
        [Test]
        public void WindowUsesToolkitAndSourcePicker()
        {
            var window=ScriptableObject.CreateInstance<GridArtWindow>();
            try { window.CreateGUI(); Assert.That(window.rootVisualElement.Q<IMGUIContainer>(),Is.Null); Assert.That(window.rootVisualElement.Q<UnityEditor.UIElements.ObjectField>(),Is.Not.Null); }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
