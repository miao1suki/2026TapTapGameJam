using System.IO;
using NUnit.Framework;
using Project.SurfaceTiles.Editor;
using UnityEditor;
using UnityEngine;

namespace Project.SurfaceTiles.Tests
{
    public sealed class SurfaceTileNativePixelTests
    {
        [Test] public void NativeReplacementKeepsSourceDimensionsAndLegacyReplacementContract()
        {
            var texture=new Texture2D(48,8);
            var sprite=Sprite.Create(texture,new Rect(0,0,48,8),Vector2.one*.5f);
            var palette=ScriptableObject.CreateInstance<SurfaceTilePalette>();
            try
            {
                palette.ReplaceTiles(new[] {sprite});
                Assert.That(palette.Tiles[0].SizeInCells,Is.EqualTo(Vector2.one),"Legacy callers retain their explicit one-cell contract.");
                palette.ReplaceTilesAtNativeScale(new[] {sprite});
                Assert.That(palette.Tiles[0].SizeInCells,Is.EqualTo(new Vector2(3,.5f)));
                Assert.That(palette.Tiles[0].FootprintCells,Is.EqualTo(new Vector2Int(3,1)));
            }
            finally { Object.DestroyImmediate(palette); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test] public void NewRecipeDefaultsToNativePixelsAndPreservesFrameMargins()
        {
            var recipe=ScriptableObject.CreateInstance<SurfaceTileSheetImportRecipe>();
            try
            {
                Assert.That(recipe.SourcePixelsPerCell,Is.EqualTo(16));
                Assert.That(recipe.UsesNativePixels,Is.True);
                Assert.That(recipe.TrimTransparentPixels,Is.False);
                Assert.That(recipe.AllowUpscale,Is.False);
            }
            finally { Object.DestroyImmediate(recipe); }
        }

        [TestCase(64,32,true)]
        [TestCase(32,16,false)]
        public void NativeGeneratorNeverResamplesSourceToFitOutput(int width,int height,bool valid)
        {
            string folder="Assets/__NativeTileTest_"+System.Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            var recipe=ScriptableObject.CreateInstance<SurfaceTileSheetImportRecipe>();
            var texture=new Texture2D(48,8);
            try
            {
                var pixels=new Color32[48*8];
                for(int i=0;i<pixels.Length;i++) pixels[i]=i%2==0 ? new Color32(255,0,0,255) : new Color32(0,0,255,255);
                texture.SetPixels32(pixels); texture.Apply();
                string source=folder+"/Original.png";
                File.WriteAllBytes(Path.GetFullPath(source),texture.EncodeToPNG()); AssetDatabase.ImportAsset(source);
                recipe.ConfigureSource(AssetDatabase.LoadAssetAtPath<Texture2D>(source));
                recipe.ConfigureOutput(folder+"/Output","Native",width,height,16,4,false,true,SurfaceTileOutputAnchor.Center);
                recipe.Regions.Add(new SurfaceTileSourceRegion("Platform",new RectInt(0,0,48,8)));
                bool generated=SurfaceTileSheetGenerator.Generate(recipe,out var result,out string error);
                Assert.That(generated,Is.EqualTo(valid),error);
                if (!valid) { Assert.That(error,Does.Contain("不会缩小素材")); Assert.That(AssetDatabase.IsValidFolder(folder+"/Output"),Is.False); return; }
                var entry=result.Palette.Tiles[0];
                Assert.That(entry.SizeInCells,Is.EqualTo(new Vector2(3,.5f)));
                Assert.That(entry.ContentRect.width*width,Is.EqualTo(48).Within(.0001f));
                Assert.That(entry.ContentRect.height*height,Is.EqualTo(8).Within(.0001f));
                Assert.That(entry.SourcePixelScale,Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(recipe); Object.DestroyImmediate(texture); AssetDatabase.DeleteAsset(folder); }
        }
    }
}
