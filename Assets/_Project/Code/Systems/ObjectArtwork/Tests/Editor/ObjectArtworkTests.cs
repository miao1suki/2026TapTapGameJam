using NUnit.Framework;
using UnityEngine;

namespace Project.ObjectArtwork.Tests
{
    public sealed class ObjectArtworkTests
    {
        [Test] public void OriginalGreenBlueButtonSheetIsFourPixelPerfectFrames()
        {
            const string path = "Assets/_Project/Resources/26TAPTAP/物体道具/蓝绿按钮80ms每帧.png";
            var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
            Assert.AreEqual(UnityEditor.TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(UnityEditor.SpriteImportMode.Multiple, importer.spriteImportMode);
            Assert.AreEqual(FilterMode.Point, importer.filterMode); Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(UnityEditor.TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(64, importer.maxTextureSize); Assert.AreEqual(16, importer.spritePixelsPerUnit);
            var sprites = System.Array.FindAll(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path), item => item is Sprite);
            Assert.AreEqual(4, sprites.Length);
            foreach (Sprite sprite in sprites) Assert.AreEqual(new Vector2(16,16), sprite.rect.size);
        }
        [TestCase(0, MechanismArtworkKind.DoorButton)]
        [TestCase(1, MechanismArtworkKind.GreenDoorButton)]
        [TestCase(2, MechanismArtworkKind.BlueDoorButton)]
        public void DoorButtonsUseRedGreenBlueBindingOrder(int index, MechanismArtworkKind expected) =>
            Assert.AreEqual(expected, ObjectArtworkLibrary.DoorButtonKind(index));

        [TestCase(MechanismArtworkKind.DoorButton, 0)]
        [TestCase(MechanismArtworkKind.GreenDoorButton, 1)]
        [TestCase(MechanismArtworkKind.BlueDoorButton, 2)]
        public void ButtonClipsHaveTwoUnmixedSixteenPixelFrames(MechanismArtworkKind kind, int dominant)
        {
            var clip = ObjectArtworkLibrary.Load().Find(kind);
            Assert.NotNull(clip); Assert.AreEqual(2, clip.frames.Length);
            var source = new Texture2D(2, 2);
            try
            {
                source.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(clip.frames[0])));
                foreach (var sprite in clip.frames) Assert.AreEqual(new Vector2(16,16), sprite.rect.size);
                int colored = 0;
                foreach (var pixel in source.GetPixels())
                {
                    if (pixel.a <= 0 || Mathf.Max(pixel.r,pixel.g,pixel.b)-Mathf.Min(pixel.r,pixel.g,pixel.b) < .01f) continue;
                    colored++;
                    Assert.That(pixel[dominant], Is.GreaterThan(pixel[(dominant + 1) % 3]));
                    Assert.That(pixel[dominant], Is.GreaterThan(pixel[(dominant + 2) % 3]));
                }
                Assert.That(colored, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test] public void DoorHasBlankFrameAndIndependentWhiteSymbols()
        {
            var library = ObjectArtworkLibrary.Load();
            Assert.NotNull(library.doorClosed); Assert.AreEqual(3, library.doorWhiteIndicators.Length);
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(library.doorClosed)));
                Assert.AreEqual(0, texture.GetPixel(8,16).a, "Closed door frame must not contain a baked white/red center symbol.");
                foreach (var sprite in library.doorWhiteIndicators) Assert.NotNull(sprite);
                Assert.AreEqual(0f, ObjectArtworkLibrary.DoorIndicatorOffset(0,1).y);
                Assert.That(ObjectArtworkLibrary.DoorIndicatorOffset(0,2).y, Is.GreaterThan(0));
                Assert.That(ObjectArtworkLibrary.DoorIndicatorOffset(1,2).y, Is.LessThan(0));
            }
            finally { Object.DestroyImmediate(texture); }
        }
        [TestCase("Door")]
        [TestCase("LadderFront")]
        [TestCase("LadderSide")]
        public void MechanismBackgroundIsTransparent(string name)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(System.IO.File.ReadAllBytes(
                    "Assets/_Project/Art/Generated/ObjectArtwork/" + name + ".png")));
                Assert.That(texture.GetPixel(0, 0).a, Is.Zero);
                Assert.That(System.Array.Exists(texture.GetPixels(), pixel => pixel.a > .9f), Is.True);
                Assert.That(System.Array.Exists(texture.GetPixels(), pixel => pixel.a > .01f &&
                    pixel.r <= .025f && pixel.g <= .025f && pixel.b <= .025f), Is.False,
                    "Door interiors and gaps between ladder rungs must also be transparent.");
            }
            finally { Object.DestroyImmediate(texture); }
        }
        [TestCase(0f, 0)] [TestCase(.5f, 14)] [TestCase(1f, 27)]
        [TestCase(-1f, 0)] [TestCase(2f, 27)]
        public void CrankFramesScrubTheSamePathInBothDirections(float progress, int expected) =>
            Assert.AreEqual(expected, ObjectArtworkLibrary.ProgressFrame(progress, 28));
        [Test] public void CrankReboundNeverRestartsAtFrameZero()
        {
            int previous = 28;
            for (int i = 20; i >= 0; i--)
            {
                int frame = ObjectArtworkLibrary.ProgressFrame(i / 20f, 28);
                Assert.That(frame, Is.LessThanOrEqualTo(previous)); previous = frame;
            }
            Assert.AreEqual(-1, ObjectArtworkLibrary.ProgressFrame(.5f, 0));
        }
        [Test] public void MechanismLibraryHasCompletePixelClips()
        {
            var library = ObjectArtworkLibrary.Load();
            Assert.AreEqual(28, library.Find(MechanismArtworkKind.Crank).frames.Length);
            Assert.AreEqual(6, library.Find(MechanismArtworkKind.CrankPlatform).frames.Length);
            Assert.AreEqual(8, library.Find(MechanismArtworkKind.MovingPlatform).frames.Length);
            Assert.AreEqual(4, library.Find(MechanismArtworkKind.Door).frames.Length);
            Assert.AreEqual(3, library.doorIndicators.Length);
            foreach (var entry in library.mechanisms) foreach (var frame in entry.frames)
            {
                Assert.NotNull(frame); Assert.AreEqual(FilterMode.Point, frame.texture.filterMode);
                Assert.AreEqual(1, frame.texture.mipmapCount);
            }
        }
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

        [TestCase(-1f, 24)]
        [TestCase(0f, 24)]
        [TestCase(.5f, 92)]
        [TestCase(1f, 160)]
        public void VineGrowthUsesForwardAndReverseNormalizedFrames(float progress, int expected)
        {
            var library = ScriptableObject.CreateInstance<ObjectArtworkLibrary>();
            try { library.vineGrowth = new Sprite[192]; Assert.AreEqual(expected, library.VineFrame(progress, 0f, false)); }
            finally { Object.DestroyImmediate(library); }
        }

        [Test] public void VineIdleLoopsOnlyAfterFullGrowth()
        {
            var library = ScriptableObject.CreateInstance<ObjectArtworkLibrary>();
            try
            {
                library.vineGrowth = new Sprite[192];
                Assert.AreEqual(166, library.VineFrame(1f, .51f, true));
                Assert.AreEqual(92, library.VineFrame(.5f, .51f, true));
                Assert.AreEqual(160, library.VineFrame(1f, 32f / 12f, true));
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test] public void GeneratedVineSpritesHaveBottomPivotsAndNoMissingFrames()
        {
            var library = ObjectArtworkLibrary.Load();
            Assert.AreEqual(192, library.vineGrowth.Length);
            foreach (var sprite in library.vineGrowth)
            {
                Assert.NotNull(sprite); Assert.AreEqual(0f, sprite.pivot.y);
                Assert.That(sprite.rect.width, Is.LessThan(100f));
                Assert.That(sprite.texture.height, Is.LessThanOrEqualTo(2048));
            }
        }
    }
}
