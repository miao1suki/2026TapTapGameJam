using NUnit.Framework;
using Project.SurfaceTiles.Editor;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project.SurfaceTiles.Tests
{
    public sealed class SurfaceTileTests
    {
        [Test]
        public void Importer_UsesToolkitSourcePickerAndCollapsibleSettings()
        {
            var window = ScriptableObject.CreateInstance<SurfaceTileSheetImporterWindow>();
            try
            {
                window.CreateGUI();
                var field = window.rootVisualElement.Q<ObjectField>();
                Assert.That(field, Is.Not.Null);
                Assert.That(field.objectType, Is.EqualTo(typeof(Texture2D)));
                Assert.That(field.allowSceneObjects, Is.False);
                Assert.That(window.rootVisualElement.Query<Foldout>().ToList().Count, Is.GreaterThanOrEqualTo(3));
                Assert.That(window.rootVisualElement.Q<IMGUIContainer>(), Is.Null);
                Assert.That(window.rootVisualElement.Q<Slider>(), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(window); }
        }
        [Test]
        public void GridSize_UsesWorldDimensionsForEveryFace()
        {
            GameObject target = new GameObject("Block");
            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 3f, 4f);
            target.transform.localScale = new Vector3(2f, 1f, 0.5f);

            Assert.That(
                SurfaceTileGeometry.GetGridSize(
                    target.transform,
                    collider,
                    SurfaceTileFace.Front,
                    1f),
                Is.EqualTo(new Vector2Int(4, 3)));
            Assert.That(
                SurfaceTileGeometry.GetGridSize(
                    target.transform,
                    collider,
                    SurfaceTileFace.Right,
                    1f),
                Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(
                SurfaceTileGeometry.GetGridSize(
                    target.transform,
                    collider,
                    SurfaceTileFace.Top,
                    1f),
                Is.EqualTo(new Vector2Int(4, 2)));

            Object.DestroyImmediate(target);
        }

        [Test]
        public void CellLookup_MapsFaceCornersAndClampsFarEdge()
        {
            GameObject target = new GameObject("Block");
            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.size = new Vector3(4f, 2f, 1f);
            SurfaceTileFaceBasis basis = SurfaceTileGeometry.GetLocalBasis(
                collider,
                SurfaceTileFace.Front);

            Assert.That(
                SurfaceTileGeometry.TryGetCell(
                    collider,
                    SurfaceTileFace.Front,
                    basis.Point(0.01f, 0.01f),
                    new Vector2Int(4, 2),
                    out Vector2Int first),
                Is.True);
            Assert.That(first, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(
                SurfaceTileGeometry.TryGetCell(
                    collider,
                    SurfaceTileFace.Front,
                    basis.Point(1f, 1f),
                    new Vector2Int(4, 2),
                    out Vector2Int last),
                Is.True);
            Assert.That(last, Is.EqualTo(new Vector2Int(3, 1)));

            Object.DestroyImmediate(target);
        }

        [Test]
        public void Placement_ReplacesSameCellAndBuildsOneQuad()
        {
            Texture2D texture = new Texture2D(2, 2);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                2f);
            SurfaceTilePalette palette =
                ScriptableObject.CreateInstance<SurfaceTilePalette>();
            palette.ReplaceTiles(new[] { sprite });
            GameObject target = new GameObject("Block");
            target.AddComponent<BoxCollider>();
            SurfaceTileBlock block = target.AddComponent<SurfaceTileBlock>();
            block.Configure(palette, 1f);
            string tileId = palette.Tiles[0].Id;

            block.SetTile(
                SurfaceTileFace.Front,
                Vector2Int.zero,
                tileId,
                0,
                false,
                false);
            block.SetTile(
                SurfaceTileFace.Front,
                Vector2Int.zero,
                tileId,
                1,
                true,
                false);
            Mesh mesh = SurfaceTileMeshBuilder.BuildCellMesh(block);

            Assert.That(block.Placements.Count, Is.EqualTo(1));
            Assert.That(block.Placements[0].QuarterTurns, Is.EqualTo(1));
            Assert.That(block.Placements[0].FlipX, Is.True);
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            Assert.That(mesh.triangles.Length, Is.EqualTo(6));
            Assert.That(mesh.bounds.min.x, Is.LessThan(-0.5f));
            Assert.That(mesh.bounds.max.x, Is.GreaterThan(0.5f));
            Assert.That(mesh.bounds.min.y, Is.LessThan(-0.5f));
            Assert.That(mesh.bounds.max.y, Is.GreaterThan(0.5f));
            Assert.That(mesh.bounds.size.x, Is.LessThan(1.02f));
            Assert.That(mesh.bounds.size.y, Is.LessThan(1.02f));

            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void TransparentBase_DisablesAndRestoresOriginalRenderer()
        {
            GameObject target = new GameObject("TransparentBaseBlock");
            target.AddComponent<BoxCollider>();
            MeshRenderer sourceRenderer = target.AddComponent<MeshRenderer>();
            SurfaceTileBlock block = target.AddComponent<SurfaceTileBlock>();

            Assert.That(sourceRenderer.enabled, Is.True);
            Assert.That(block.TransparentBase, Is.False);

            block.SetTransparentBase(true);
            Assert.That(block.TransparentBase, Is.True);
            Assert.That(sourceRenderer.enabled, Is.False);

            block.SetTransparentBase(false);
            Assert.That(block.TransparentBase, Is.False);
            Assert.That(sourceRenderer.enabled, Is.True);

            Object.DestroyImmediate(target);
        }

        [Test]
        public void UvTransform_RotatesAndFlipsDeterministically()
        {
            Vector2 rotated = SurfaceTileMeshBuilder.TransformTileUv(
                new Vector2(0f, 0f),
                1,
                false,
                false);
            Vector2 flipped = SurfaceTileMeshBuilder.TransformTileUv(
                new Vector2(0.25f, 0.75f),
                0,
                true,
                true);

            Assert.That(rotated, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(flipped, Is.EqualTo(new Vector2(0.75f, 0.25f)));
        }

        [Test]
        public void PlacementRect_UsesMultiCellSizeRotationAndEdgeAnchor()
        {
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                4f);
            sprite.name = "Wide";
            SurfaceTilePalette palette =
                ScriptableObject.CreateInstance<SurfaceTilePalette>();
            palette.ReplaceTiles(new[] { sprite });
            palette.ConfigureTileLayout(
                "Wide",
                new Vector2(3f, 1f),
                new Rect(0f, 0f, 1f, 1f));
            SurfaceTilePalette.Entry entry = palette.Tiles[0];

            Rect normal = SurfaceTileGeometry.GetPlacementRect(
                entry,
                new Vector2Int(1, 2),
                0,
                SurfaceTileAnchor.BottomLeft);
            Rect rotated = SurfaceTileGeometry.GetPlacementRect(
                entry,
                new Vector2Int(1, 2),
                1,
                SurfaceTileAnchor.BottomLeft);
            palette.ConfigureTileLayout(
                "Wide",
                new Vector2(1f, 0.25f),
                new Rect(0f, 0f, 1f, 1f));
            Rect topEdge = SurfaceTileGeometry.GetPlacementRect(
                entry,
                new Vector2Int(2, 4),
                0,
                SurfaceTileAnchor.TopEdge);

            Assert.That(normal, Is.EqualTo(new Rect(1f, 2f, 3f, 1f)));
            Assert.That(rotated, Is.EqualTo(new Rect(1f, 2f, 1f, 3f)));
            Assert.That(topEdge, Is.EqualTo(new Rect(2f, 4.75f, 1f, 0.25f)));

            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void PlacementOffset_ShiftsTileAndAllowsBoundedEdgeOverhang()
        {
            Texture2D texture = new Texture2D(2, 2);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                2f);
            sprite.name = "Grass";
            SurfaceTilePalette palette =
                ScriptableObject.CreateInstance<SurfaceTilePalette>();
            palette.ReplaceTiles(new[] { sprite });
            GameObject target = new GameObject("OffsetBlock");
            target.AddComponent<BoxCollider>();
            SurfaceTileBlock block = target.AddComponent<SurfaceTileBlock>();
            block.Configure(palette, 1f);

            Assert.That(block.AddTile(
                SurfaceTileFace.Front,
                Vector2Int.zero,
                palette.Tiles[0].Id,
                0,
                false,
                false,
                SurfaceTileAnchor.TopEdge,
                true,
                new Vector2(0.1f, 0.25f)), Is.True);
            SurfaceTilePlacement placement = block.Placements[0];
            Rect rect = SurfaceTileGeometry.GetPlacementRect(
                palette.Tiles[0],
                placement);
            Mesh preview = SurfaceTileMeshBuilder.BuildCellMesh(block);

            Assert.That(placement.OffsetCells, Is.EqualTo(new Vector2(0.1f, 0.25f)));
            Assert.That(rect, Is.EqualTo(new Rect(0.1f, 0.25f, 1f, 1f)));
            Assert.That(preview.vertexCount, Is.EqualTo(4));
            Assert.That(preview.bounds.max.y, Is.GreaterThan(0.75f));
            Assert.That(preview.bounds.max.y, Is.LessThan(0.76f));

            Object.DestroyImmediate(preview);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void LayeredPlacements_KeepBackgroundAndEraseTopFirst()
        {
            Texture2D texture = new Texture2D(2, 2);
            Sprite background = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                2f);
            Sprite leaves = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                2f);
            background.name = "Background";
            leaves.name = "Leaves";
            SurfaceTilePalette palette =
                ScriptableObject.CreateInstance<SurfaceTilePalette>();
            palette.ReplaceTiles(new[] { background, leaves });
            GameObject target = new GameObject("LayeredBlock");
            target.AddComponent<BoxCollider>();
            SurfaceTileBlock block = target.AddComponent<SurfaceTileBlock>();
            block.Configure(palette, 1f);

            Assert.That(block.AddTile(
                SurfaceTileFace.Front,
                Vector2Int.zero,
                palette.Tiles[0].Id,
                0,
                false,
                false,
                SurfaceTileAnchor.BottomLeft,
                true), Is.True);
            Assert.That(block.AddTile(
                SurfaceTileFace.Front,
                Vector2Int.zero,
                palette.Tiles[1].Id,
                0,
                false,
                false,
                SurfaceTileAnchor.Center,
                true), Is.True);

            Assert.That(block.Placements.Count, Is.EqualTo(2));
            Mesh layeredMesh = SurfaceTileMeshBuilder.BuildCellMesh(block);
            Assert.That(layeredMesh.vertexCount, Is.EqualTo(8));
            Assert.That(block.TryGetPlacement(
                SurfaceTileFace.Front,
                Vector2Int.zero,
                out SurfaceTilePlacement top), Is.True);
            Assert.That(top.TileId, Is.EqualTo(palette.Tiles[1].Id));
            Assert.That(top.Layer, Is.EqualTo(1));

            Assert.That(block.RemoveTile(
                SurfaceTileFace.Front,
                Vector2Int.zero), Is.True);
            Assert.That(block.TryGetPlacement(
                SurfaceTileFace.Front,
                Vector2Int.zero,
                out SurfaceTilePlacement remaining), Is.True);
            Assert.That(remaining.TileId, Is.EqualTo(palette.Tiles[0].Id));

            Object.DestroyImmediate(layeredMesh);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(background);
            Object.DestroyImmediate(leaves);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Bake_CreatesPersistentTextureMaterialAndMesh()
        {
            const string temporaryRoot = "Assets/__SurfaceTileBakeTest";
            const string generatedParent = "Assets/_Project/Generated";
            const string generatedRoot = generatedParent + "/SurfaceTiles";
            const string generatedFolder =
                generatedRoot + "/BakeTest";
            bool generatedParentExisted = AssetDatabase.IsValidFolder(generatedParent);
            bool generatedRootExisted = AssetDatabase.IsValidFolder(generatedRoot);
            try
            {
                if (!AssetDatabase.IsValidFolder(temporaryRoot))
                {
                    AssetDatabase.CreateFolder("Assets", "__SurfaceTileBakeTest");
                }

                Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.SetPixels(new[]
                {
                    Color.red, Color.green, Color.blue, Color.white
                });
                source.Apply();
                string texturePath = temporaryRoot + "/Tile.png";
                File.WriteAllBytes(Path.GetFullPath(texturePath), source.EncodeToPNG());
                Object.DestroyImmediate(source);
                AssetDatabase.ImportAsset(
                    texturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(
                    texturePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);

                SurfaceTilePalette palette =
                    ScriptableObject.CreateInstance<SurfaceTilePalette>();
                palette.ReplaceTiles(new[] { sprite });
                string palettePath = temporaryRoot + "/Palette.asset";
                AssetDatabase.CreateAsset(palette, palettePath);
                AssetDatabase.SaveAssets();
                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                Assert.That(EditorSceneManager.SaveScene(
                    scene,
                    temporaryRoot + "/BakeTest.unity"), Is.True);
                palette = AssetDatabase.LoadAssetAtPath<SurfaceTilePalette>(
                    palettePath);

                GameObject target = new GameObject("BakeBlock");
                target.AddComponent<BoxCollider>();
                SurfaceTileBlock block = target.AddComponent<SurfaceTileBlock>();
                block.Configure(palette, 1f);
                Assert.That(block.AddTile(
                    SurfaceTileFace.Front,
                    Vector2Int.zero,
                    palette.Tiles[0].Id,
                    0,
                    false,
                    false,
                    SurfaceTileAnchor.TopEdge,
                    true,
                    new Vector2(0f, 0.25f)), Is.True);

                Assert.That(
                    SurfaceTileAssetBaker.Bake(block, out string message),
                    Is.True,
                    message);
                Assert.That(block.BakeUpToDate, Is.True);
                Assert.That(AssetDatabase.Contains(block.BakedMesh), Is.True);
                Assert.That(AssetDatabase.Contains(block.BakedMaterial), Is.True);
                Assert.That(
                    block.BakedMesh.name,
                    Is.EqualTo(Path.GetFileNameWithoutExtension(
                        AssetDatabase.GetAssetPath(block.BakedMesh))));
                Assert.That(block.BakedMaterial.mainTexture, Is.Not.Null);
                Assert.That(
                    block.BakedMesh.bounds.max.y,
                    Is.GreaterThan(0.75f));
                Assert.That(block.BakedMesh.bounds.max.y, Is.LessThan(0.76f));
                Assert.That(
                    AssetDatabase.IsValidFolder(generatedFolder),
                    Is.True);
                Assert.That(
                    message,
                    Does.Contain(Path.GetFullPath(generatedFolder)));
                target.transform.localScale = new Vector3(2f, 1f, 1f);
                Assert.That(block.BakeUpToDate, Is.False);
            }
            finally
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                AssetDatabase.DeleteAsset(temporaryRoot);
                AssetDatabase.DeleteAsset(generatedFolder);
                if (!generatedRootExisted)
                {
                    AssetDatabase.DeleteAsset(generatedRoot);
                }

                if (!generatedParentExisted)
                {
                    AssetDatabase.DeleteAsset(generatedParent);
                }

                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void IrregularImporter_DetectsSeparatedAlphaRegions()
        {
            Texture2D source = new Texture2D(12, 8, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[12 * 8];
            PaintRect(pixels, 12, new RectInt(1, 1, 2, 2), Color.white);
            PaintRect(pixels, 12, new RectInt(8, 4, 3, 2), Color.white);
            source.SetPixels32(pixels);
            source.Apply();

            System.Collections.Generic.List<RectInt> regions =
                SurfaceTileSheetGenerator.DetectRegions(
                    source,
                    0,
                    2,
                    0,
                    0);

            Assert.That(regions.Count, Is.EqualTo(2));
            Assert.That(regions, Does.Contain(new RectInt(1, 1, 2, 2)));
            Assert.That(regions, Does.Contain(new RectInt(8, 4, 3, 2)));
            Object.DestroyImmediate(source);
        }

        [Test]
        public void IrregularImporter_CanGroupPiecesIntoOneCompleteBlock()
        {
            Texture2D source = new Texture2D(12, 6, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[12 * 6];
            PaintRect(pixels, 12, new RectInt(1, 2, 2, 2), Color.white);
            PaintRect(pixels, 12, new RectInt(5, 2, 2, 2), Color.white);
            source.SetPixels32(pixels);
            source.Apply();

            System.Collections.Generic.List<RectInt> completeOnly =
                SurfaceTileSheetGenerator.DetectCompleteRegionsWithOptionalPieces(
                    source,
                    0,
                    6,
                    1,
                    3,
                    0,
                    false);
            System.Collections.Generic.List<RectInt> completeAndPieces =
                SurfaceTileSheetGenerator.DetectCompleteRegionsWithOptionalPieces(
                    source,
                    0,
                    6,
                    1,
                    3,
                    0,
                    true);

            Assert.That(completeOnly.Count, Is.EqualTo(1));
            Assert.That(completeOnly[0], Is.EqualTo(new RectInt(1, 2, 6, 2)));
            Assert.That(completeAndPieces.Count, Is.EqualTo(3));
            Assert.That(completeAndPieces[0], Is.EqualTo(new RectInt(1, 2, 6, 2)));
            Assert.That(completeAndPieces, Does.Contain(new RectInt(1, 2, 2, 2)));
            Assert.That(completeAndPieces, Does.Contain(new RectInt(5, 2, 2, 2)));
            Object.DestroyImmediate(source);
        }

        [Test]
        public void IrregularImporter_GeneratesUniformPersistentSpritesAndKeepsIds()
        {
            const string root = "Assets/__SurfaceTileImporterTest";
            try
            {
                if (!AssetDatabase.IsValidFolder(root))
                {
                    AssetDatabase.CreateFolder("Assets", "__SurfaceTileImporterTest");
                }

                Texture2D source = new Texture2D(8, 4, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[8 * 4];
                PaintRect(pixels, 8, new RectInt(0, 0, 2, 2), Color.red);
                PaintRect(pixels, 8, new RectInt(5, 1, 3, 3), Color.green);
                source.SetPixels32(pixels);
                source.Apply();
                string sourcePath = root + "/Source.png";
                File.WriteAllBytes(Path.GetFullPath(sourcePath), source.EncodeToPNG());
                Object.DestroyImmediate(source);
                AssetDatabase.ImportAsset(
                    sourcePath,
                    ImportAssetOptions.ForceSynchronousImport);
                Texture2D sourceAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    sourcePath);

                SurfaceTileSheetImportRecipe recipe =
                    ScriptableObject.CreateInstance<SurfaceTileSheetImportRecipe>();
                recipe.ConfigureSource(sourceAsset);
                recipe.ConfigureOutput(
                    root + "/Generated",
                    "TestTiles",
                    16,
                    16,
                    2,
                    1,
                    true,
                    true,
                    SurfaceTileOutputAnchor.Center);
                recipe.Regions.Add(new SurfaceTileSourceRegion(
                    "Red",
                    new RectInt(0, 0, 3, 3)));
                recipe.Regions.Add(new SurfaceTileSourceRegion(
                    "Green",
                    new RectInt(4, 0, 4, 4)));
                AssetDatabase.CreateAsset(recipe, root + "/Recipe.asset");

                Assert.That(
                    SurfaceTileSheetGenerator.Generate(
                        recipe,
                        out SurfaceTileSheetGenerateResult first,
                        out string firstError),
                    Is.True,
                    firstError);
                Assert.That(first.TileCount, Is.EqualTo(2));
                Assert.That(first.Palette.Tiles.Count, Is.EqualTo(2));
                Assert.That(first.Palette.Tiles.All(item =>
                    item.Sprite.rect.size == new Vector2(16f, 16f)), Is.True);
                Assert.That(
                    first.Palette.Tiles[0].SizeInCells,
                    Is.EqualTo(Vector2.one));
                Assert.That(
                    first.Palette.Tiles[1].SizeInCells,
                    Is.EqualTo(new Vector2(1.5f, 1.5f)));
                Assert.That(
                    first.Palette.Tiles[0].ContentRect.width,
                    Is.EqualTo(0.875f).Within(0.0001f));
                Assert.That(first.Palette.UsesSingleTexture(), Is.True);
                string redId = first.Palette.Tiles[0].Id;
                string greenId = first.Palette.Tiles[1].Id;

                Assert.That(
                    SurfaceTileSheetGenerator.Generate(
                        recipe,
                        out SurfaceTileSheetGenerateResult second,
                        out string secondError),
                    Is.True,
                    secondError);
                Assert.That(second.Palette.Tiles[0].Id, Is.EqualTo(redId));
                Assert.That(second.Palette.Tiles[1].Id, Is.EqualTo(greenId));
                Assert.That(second.Palette.PreviewMaterial, Is.Not.Null);
                Assert.That(AssetDatabase.Contains(second.Palette.Tiles[0].Sprite), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        private static void PaintRect(
            Color32[] pixels,
            int width,
            RectInt rect,
            Color32 color)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    pixels[y * width + x] = color;
                }
            }
        }
    }
}
