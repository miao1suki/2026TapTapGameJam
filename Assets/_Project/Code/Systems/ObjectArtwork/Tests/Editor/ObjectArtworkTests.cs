using NUnit.Framework;
using UnityEngine;

namespace Project.ObjectArtwork.Tests
{
    public sealed class ObjectArtworkTests
    {
        [TestCase(false, false, RespawnArtworkState.Locked)]
        [TestCase(true, false, RespawnArtworkState.Used)]
        [TestCase(true, true, RespawnArtworkState.Current)]
        public void RespawnUsesThreeDistinctStates(bool activated, bool current, RespawnArtworkState expected) =>
            Assert.AreEqual(expected, ObjectArtworkLibrary.ResolveRespawn(activated, current));
        [Test] public void AnimationLoopsAndOneShotEnds()
        {
            Assert.AreEqual(0, ObjectArtworkLibrary.Frame(1.45f, .08f, 18, true));
            Assert.AreEqual(5, ObjectArtworkLibrary.Frame(5f, .08f, 6, false));
            Assert.AreEqual(-1, ObjectArtworkLibrary.Frame(0f, .08f, 0, true));
        }
        [TestCase(4f, 1f, 1f)]
        [TestCase(3f, 2f, .5f)]
        public void MergedPlantsRetainSquarePixelRatio(float x, float y, float cell)
        {
            var scale = ObjectArtworkLibrary.TileScale(new Vector3(x, y, 1f), cell);
            Assert.AreEqual(cell, scale.x * x, .0001f);
            Assert.AreEqual(cell, scale.y * y, .0001f);
        }
        [Test] public void ActualLibraryContainsAllFrames()
        {
            var library = ObjectArtworkLibrary.Load();
            Assert.NotNull(library); Assert.NotNull(library.respawnLocked); Assert.NotNull(library.respawnUsed);
            Assert.AreEqual(18, library.respawnCurrent.Length); Assert.AreEqual(6, library.plantBounce.Length);
            Assert.That(library.plantIdleTop, Is.InRange(.01f, 1f));
            Assert.AreEqual(16f, library.plantBounce[0].pixelsPerUnit);
            var settings = new UnityEditor.TextureImporterSettings();
            ((UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(library.plantBounce[0]))).ReadTextureSettings(settings);
            Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType);
        }
    }
}
