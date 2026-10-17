using NUnit.Framework;
using Project.GridArt.Editor;
using UnityEngine;

namespace Project.GridArt.Tests
{
    public sealed class GridArtPixelTests
    {
        [TestCase(16,16,1,1)]
        [TestCase(32,32,2,2)]
        [TestCase(48,8,3,.5f)]
        [TestCase(15,16,.9375f,1)]
        public void NativePixelsDetermineDisplay(int width,int height,float x,float y)
        {
            var texture=new Texture2D(width,height);
            var sprite=Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f);
            try { Assert.That(GridArtPixelLayout.NativeSize(sprite,new Rect(0,0,1,1)),Is.EqualTo(new Vector2(x,y))); }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test] public void VerifiedFourTimesTailExportRemainsOneGridCell()
        {
            var texture=new Texture2D(1408,704) { name="tail" };
            var sprite=Sprite.Create(texture,new Rect(64,64,64,64),Vector2.one*.5f);
            try
            {
                Assert.That(GridArtPixelLayout.SourceScale(sprite),Is.EqualTo(4));
                Assert.That(GridArtPixelLayout.NativeSize(sprite,new Rect(0,0,1,1)),Is.EqualTo(Vector2.one));
                Assert.That(GridArtPixelLayout.IsRuleSprite(sprite),Is.True);
                Assert.That(GridArtPixelLayout.IsNative(new GridArtBrush { sprite=sprite,visualSize=Vector2.one }),Is.True);
            }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test] public void CropExcludesAtlasPaddingWithoutSqueezingAndOccupancyDoesNotScaleArt()
        {
            var texture=new Texture2D(128,64);
            var sprite=Sprite.Create(texture,new Rect(0,0,128,64),Vector2.one*.5f);
            try
            {
                var brush=new GridArtBrush { sprite=sprite,contentRect=new Rect(.25f,.25f,.375f,.125f),visualSize=Vector2.one };
                Assert.That(GridArtPixelLayout.IsNative(brush),Is.False);
                Assert.That(GridArtPixelLayout.RestoreNative(brush,out var error),Is.True,error);
                Assert.That(brush.DisplaySize,Is.EqualTo(new Vector2(3,.5f)));
                Assert.That(brush.Size,Is.EqualTo(new Vector2Int(3,1)));
                brush.size=new Vector2Int(9,7);
                Assert.That(brush.DisplaySize,Is.EqualTo(new Vector2(3,.5f)));
            }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test] public void LayerColorOverridesBrushWithoutIntroducingInteractionComponents()
        {
            var texture=new Texture2D(16,16);
            var sprite=Sprite.Create(texture,new Rect(0,0,16,16),Vector2.one*.5f);
            var palette=ScriptableObject.CreateInstance<GridArtPalette>();
            try
            {
                palette.brushes.Add(new GridArtBrush { sprite=sprite,colorId="red" });
                var layer=new GridArtLayer(); layer.Set(Vector2Int.zero,0);
                Assert.That(GridArtBuilder.BuildQuads(layer,palette)[0].colorId,Is.EqualTo("red"));
                layer.colorId="green";
                Assert.That(GridArtBuilder.BuildQuads(layer,palette)[0].colorId,Is.EqualTo("green"));
                layer.colorId="none";
                Assert.That(GridArtBuilder.BuildQuads(layer,palette)[0].colorId,Is.Empty);
            }
            finally { Object.DestroyImmediate(palette); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
    }
}
