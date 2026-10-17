using System;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Asset, geometry and actual GPU pixel-grid regression checks in the disposable probe.</summary>
public static class ArtGridRefinementAudit
{
    private static int checks;
    private static ObjectArtworkLibrary artwork;
    private const string Content = "Assets/_Project/Content/";
    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException("ART GRID: " + message);
        checks++; Debug.Log("ART GRID PASS: " + message);
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < .002f;
    private static GameObject Spawn(string path) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Content + path));
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
            throw new InvalidOperationException("此验证仅可在 Library/FlowRetirementProbe 隔离工程执行。");
        int code = 0;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            checks = 0;
            WorldObjectArtworkSetup.BuildPlatformFrames();
            WorldObjectArtworkSetup.BuildSupplementArtwork();
            artwork = ObjectArtworkLibrary.Load();
            Metrics(); Assets(); Ladders(); Platforms(); ShortPlatforms(); Respawn(); KeysAndSupplementary(); Spikes(); PlanningPreviews(); PixelGpu();
            string result = "PASS " + checks + " artwork/grid assertions; GPU=" + SystemInfo.graphicsDeviceType;
            File.WriteAllText("Library/ArtGridRefinementResult.txt", result); Debug.Log(result);
        }
        catch (Exception error)
        {
            code = 1; File.WriteAllText("Library/ArtGridRefinementResult.txt", error.ToString()); Debug.LogException(error);
        }
        EditorApplication.Exit(code);
    }
    private static int ExportScale(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Check(texture.LoadImage(File.ReadAllBytes(path)), "read source " + Path.GetFileName(path));
            return GridArtworkMetrics.ExactExportScale(texture.GetPixels32(), texture.width, texture.height);
        }
        finally { Object.DestroyImmediate(texture); }
    }
    private static void Metrics()
    {
        Check(GridArtworkMetrics.PixelsToCells(new Vector2(16, 16)) == Vector2.one, "16 effective artwork pixels equal one cell");
        Check(GridArtworkMetrics.PixelsToCells(new Vector2(64, 64), 4) == Vector2.one, "verified 4x export converts encoded64 to effective16");
        Check(GridArtworkMetrics.PixelsToCells(new Vector2(32, 32)) == Vector2.one * 2, "32 genuine pixels retain two-cell visual size");
        Check(GridArtworkMetrics.PlacementFootprint(new Vector2(48, 8)) == new Vector2Int(3, 1), "48x8 art has three by one placement, not one stretched cell");
        Check(GridArtworkMetrics.PlacementFootprint(new Vector2(16.000002f, 16)) == Vector2Int.one, "floating crop rounding cannot add a placement cell");
        Check(ExportScale("Assets/_Project/Resources/26TAPTAP/场景背景/tail.png") == 4, "tail is exact RGBA 4x nearest-neighbor export");
        Check(ExportScale("Assets/_Project/Art/Generated/ObjectArtwork/Respawn Current.png") == 1, "respawn animation is not a 2x export");
        Check(ExportScale("Assets/_Project/Art/Generated/ObjectArtwork/Respawn States.png") == 1, "respawn stills are not a 2x export");
        Check(ExportScale("Assets/_Project/Resources/26TAPTAP/物体道具/移动平台80ms每帧.png") == 1, "moving platform source pixels are native");
    }
    private static void Assets()
    {
        foreach (var pair in new[] { (MechanismArtworkKind.CrankPlatform, 6), (MechanismArtworkKind.MovingPlatform, 8) })
        {
            var frames = artwork.Find(pair.Item1).frames;
            Check(frames.Length == pair.Item2 && frames.All(frame => frame.rect.size == new Vector2(48, 16)), pair.Item1 + " preserves complete 48x16 frame and original frame count");
            Check(frames.All(frame => frame.pixelsPerUnit == 16), pair.Item1 + " uses 16 PPU");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0]));
            Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None,
                pair.Item1 + " remains point sampled without rescaling or mipmaps");
        }
        Check(artwork.signboards.Length == 7 && artwork.signboards.All(frame => frame.rect.size == new Vector2(16, 16)), "seven complete signboard cells imported");
        Check(artwork.Find(MechanismArtworkKind.MechanismBase).frames.Length == 2 &&
            artwork.Find(MechanismArtworkKind.MechanismBase).frames.All(frame => frame.rect.size == new Vector2(16, 16)), "base has two native-cell powered/unpowered sprites");
        Check(artwork.FirstFrame(MechanismArtworkKind.PlantObstacle).rect.size == new Vector2(16, 16) &&
            artwork.FirstFrame(MechanismArtworkKind.Obsidian).rect.size == new Vector2(16, 16), "plant obstacle and obsidian use their own complete native cells");
        var spike = artwork.FirstFrame(MechanismArtworkKind.Spike);
        Check(spike != null && spike.rect.size == new Vector2(16, 16), "spike uses one complete authored cell, not a squeezed two-cell strip");
        string path = AssetDatabase.GetAssetPath(spike);
        var supplementary = (TextureImporter)AssetImporter.GetAtPath(path);
        supplementary.GetSourceTextureWidthAndHeight(out int width, out int height);
        Check(width == 447 && height == 336 && supplementary.maxTextureSize == 512 && supplementary.spritePixelsPerUnit == 16,
            "supplement sheet preserves source447x336 with max512 and 16PPU");
        var before = artwork.signboards.Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()).ToArray();
        WorldObjectArtworkSetup.BuildSupplementArtwork();
        var after = artwork.signboards.Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()).ToArray();
        Check(before.SequenceEqual(after), "rebuilding supplementary slices preserves persistent sign sprite IDs");
    }
    private static void Ladders()
    {
        Check(PlanningDoorUtility.Footprint(PlanningDoorUtility.SideLadderEntryId) == Vector2Int.one &&
            PlanningDoorUtility.Footprint(PlanningDoorUtility.LeftSideLadderEntryId) == Vector2Int.one, "both wall-side ladders default to1x1");
        foreach (var pair in new[] { ("Mechanisms/LadderSide.prefab", LadderWallSide.Right), ("Mechanisms/LadderSideLeft.prefab", LadderWallSide.Left) })
        {
            var owner = Spawn(pair.Item1);
            try
            {
                var ladder = owner.GetComponent<NeutralLadder>();
                var visual = MechanismArtworkPresentation.Ensure(owner).Visual;
                Check(ladder.SideFacing && ladder.WallSide == pair.Item2 && visual.flipX == (pair.Item2 == LadderWallSide.Left), "side ladder retains explicit wall side " + pair.Item2);
                Check(Near(visual.bounds.size.x, 1), "ladder artwork fills authored width independently of its narrow trigger");
                var box = owner.GetComponent<BoxCollider>(); box.size = new Vector3(box.size.x, 5, box.size.z);
                MechanismArtworkPresentation.Ensure(owner).Refresh(0);
                Check(Near(box.size.y, 5) && Near(visual.bounds.size.y, 5), "existing long ladder retains five-cell trigger and repeated art");
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
    private static void Platforms()
    {
        Check(PlanningDoorUtility.Footprint(PlanningDoorUtility.MovingPlatformEntryId) == new Vector2Int(3, 1), "new moving platform placement is3x1");
        Check(PlanningDoorUtility.Footprint(PlanningDoorUtility.OneWayPlatformEntryId) == new Vector2Int(3, 1), "new one-way platform placement is3x1");
        var owner = Spawn("ColorBlocks/Prefabs/Red_Platform.prefab");
        try
        {
            var collider = owner.GetComponent<BoxCollider>();
            Vector3 originalSize = collider.size, originalCenter = collider.center;
            var presentation = MechanismArtworkPresentation.Ensure(owner);
            Check(originalSize == Vector3.one, "legacy platform prefab geometry remains a single cell");
            owner.transform.localScale = new Vector3(1, 5, 1); presentation.Refresh(0);
            var visual = presentation.Visual;
            Check(Near(Quaternion.Angle(visual.transform.localRotation, Quaternion.Euler(0, 0, 90)), 0) && Near(visual.size.x, 5), "five-cell vertical platform rotates art before tiling its full length");
            Vector3 pixelX = visual.transform.TransformVector(Vector3.right / 16), pixelY = visual.transform.TransformVector(Vector3.up / 16);
            Check(Near(pixelX.magnitude, 1f / 16) && Near(pixelY.magnitude, 1f / 16), "vertical nonuniform parent retains square native pixels");
            Check(collider.size == originalSize && collider.center == originalCenter, "visual refresh does not mutate existing collision geometry");
            var shape = owner.AddComponent<LevelEditorMergedShape>();
            shape.Configure(new Vector2Int(1, 5), new[] { new RectInt(0, 0, 1, 2), new RectInt(0, 3, 1, 2) });
            presentation.Refresh(0);
            Check(!shape.ContainsCell(new Vector2Int(0, 2)) && shape.Rectangles.Count == 2, "irregular merged platform preserves its empty middle cell");
            var pieces = owner.GetComponentsInChildren<SpriteRenderer>(true).Where(part => part.name.StartsWith("__MergedShapeVisual_", StringComparison.Ordinal)).ToArray();
            Check(pieces.Length == 2 && pieces.All(part => Near(Quaternion.Angle(part.transform.localRotation, Quaternion.Euler(0, 0, 90)), 0) && Near(part.size.x, 2)),
                "irregular vertical platform art rotates per rectangle without filling its hole");
        }
        finally { Object.DestroyImmediate(owner); }
        var lava = Spawn("ColorBlocks/Prefabs/Red_Lava.prefab");
        try
        {
            var presentation = MechanismArtworkPresentation.Ensure(lava);
            if (presentation != null)
            {
                var shape = lava.AddComponent<LevelEditorMergedShape>();
                shape.Configure(new Vector2Int(2, 2), new[] { new RectInt(0, 0, 1, 2), new RectInt(1, 1, 1, 1) });
                presentation.Refresh(0);
                Check(!shape.ContainsCell(new Vector2Int(1, 0)), "non-platform sprite geometry still preserves holes");
                Check(lava.GetComponentsInChildren<SpriteRenderer>().Where(part => part.name.StartsWith("__MergedShapeVisual_", StringComparison.Ordinal)).All(part => Near(Quaternion.Angle(part.transform.localRotation, Quaternion.identity), 0)),
                    "lava does not inherit platform rotation logic");
            }
        }
        finally { Object.DestroyImmediate(lava); }
    }
    private static void ShortPlatforms()
    {
        foreach (string path in new[] { "ColorBlocks/Prefabs/Red_Platform.prefab", "ColorBlocks/Prefabs/Red_CrankLift.prefab", "Mechanisms/OneWayPlatform.prefab" })
        foreach (int length in new[] { 1, 2 })
        {
            var owner = Spawn(path);
            try
            {
                owner.transform.localScale = Vector3.Scale(owner.transform.localScale, new Vector3(length, 1, 1));
                var collider = owner.GetComponent<BoxCollider>();
                string before = EditorJsonUtility.ToJson(collider);
                var presentation = MechanismArtworkPresentation.Ensure(owner);
                presentation.Refresh(0);
                var visual = presentation.Visual;
                Check(Near(visual.sprite.rect.width, length * 16), path + " clips native short-platform pixels " + length);
                Check(Near(visual.bounds.size.x, length), path + " retains authored short-platform width " + length);
                Check(Near(visual.transform.TransformVector(Vector3.right / 16).magnitude, 1f / 16) &&
                    Near(visual.transform.TransformVector(Vector3.up / 16).magnitude, 1f / 16), path + " short-platform pixels stay square " + length);
                Check(before == EditorJsonUtility.ToJson(collider), path + " short-platform clipping preserves collision " + length);
                var originalSprite = visual.sprite;
                presentation.Refresh(.16f);
                Check(Near(visual.sprite.rect.width, length * 16) && Near(visual.bounds.size.x, length), path + " animation frames cannot widen short platform " + length);
                Check(PlatformArtworkLayout.CroppedFrame(originalSprite, length) == originalSprite,
                    path + " already cropped sprite is reused without extra allocation " + length);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }

    private static void Respawn()
    {
        var owner = Spawn("ColorBlocks/Prefabs/Respawn_Block.prefab");
        try
        {
            var colliders = owner.GetComponentsInChildren<Collider>(true);
            var before = colliders.Select(EditorJsonUtility.ToJson).ToArray();
            var presentation = WorldObjectSpritePresentation.Ensure(owner); presentation.Refresh(0);
            Check(Near(presentation.Visual.bounds.size.x, 2) && Near(presentation.Visual.bounds.size.y, 2), "respawn genuine32x32 artwork naturally occupies2x2 visual cells");
            var after = owner.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson).ToArray();
            Check(before.SequenceEqual(after), "respawn visual conversion preserves existing activation collider configuration without inventing a box");
        }
        finally { Object.DestroyImmediate(owner); }
    }
    private static void Spikes()
    {
        foreach (string direction in new[] { "上", "下", "左", "右" })
        {
            var owner = Spawn("WorldItems/Spike_" + direction + ".prefab");
            try
            {
                var collider = owner.GetComponentInChildren<MeshCollider>(); var original = collider.sharedMesh;
                var presentation = MechanismArtworkPresentation.Ensure(owner);
                Check(presentation.Kind == MechanismArtworkKind.Spike && presentation.Visual.sprite.rect.width == 16,
                    "four-direction spike uses a single native art cell " + direction);
                Check(Quaternion.Angle(presentation.Visual.transform.rotation, collider.transform.rotation) < .01f && collider.sharedMesh == original && collider.isTrigger,
                    "spike art follows existing direction without modifying hit volume " + direction);
                Check(Near(collider.sharedMesh.bounds.min.y, -.5f) && Near(collider.sharedMesh.bounds.max.y, -.25f),
                    "spike damage triangle follows visible quarter-cell height, not former invisible0.75 volume " + direction);
                Physics.SyncTransforms();
                Vector3 safe = collider.transform.TransformPoint(new Vector3(0, -.1f, 0));
                Vector3 touching = collider.transform.TransformPoint(new Vector3(0, -.4f, 0));
                Check(Vector3.Distance(collider.ClosestPoint(safe), safe) > .1f &&
                    Vector3.Distance(collider.ClosestPoint(touching), touching) < .001f,
                    "spike physical trigger excludes invisible upper area but retains visible base " + direction);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }

    private static void PlanningPreviews()
    {
        foreach (string path in new[]
        {
            "Mechanisms/Door.prefab", "Mechanisms/DoorHorizontal.prefab", "Mechanisms/DoorButton.prefab",
            "Mechanisms/LadderFront.prefab", "Mechanisms/LadderSide.prefab", "Mechanisms/LadderSideLeft.prefab", "Mechanisms/OneWayPlatform.prefab",
            "ContactCubes/Red_ContactCube.prefab", "ContactCubes/Green_ContactCube.prefab", "ContactCubes/Blue_ContactCube.prefab",
            "ColorBlocks/Prefabs/Red_Foundation.prefab", "ColorBlocks/Prefabs/Green_PlantObstacle.prefab", "ColorBlocks/Prefabs/CoolLava_Block.prefab",
            "ColorBlocks/Prefabs/Red_Lava.prefab", "ColorBlocks/Prefabs/Red_Crank.prefab", "ColorBlocks/Prefabs/Red_CrankLift.prefab",
            "ColorBlocks/Prefabs/Red_Platform.prefab", "ColorBlocks/Prefabs/Green_JumpPlant.prefab", "ColorBlocks/Prefabs/Respawn_Block.prefab",
            "ColorBlocks/Prefabs/ColorKey_red.prefab", "ColorBlocks/Prefabs/ColorKey_green.prefab", "ColorBlocks/Prefabs/ColorKey_blue.prefab",
            "ColorBlocks/Prefabs/Green_Ladder.prefab", "WorldItems/Spike_上.prefab", "WorldItems/Spike_左.prefab"
        })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Content + path);
            Check(prefab != null && ObjectArtworkPlanningPreview.TryGet(prefab, out var preview) &&
                preview.Sprite != null && preview.Texture != null && !preview.Hidden && preview.Cells.x > 0 && preview.Cells.y > 0,
                "planning resolves actual 2D artwork without a prefab-camera screenshot: " + path);
        }
        var respawn = AssetDatabase.LoadAssetAtPath<GameObject>(Content + "ColorBlocks/Prefabs/Respawn_Block.prefab");
        ObjectArtworkPlanningPreview.TryGet(respawn, out var anchorPreview);
        Check(anchorPreview.Sprite == artwork.respawnLocked && anchorPreview.Cells == new Vector2(2, 2), "respawn preview preserves native two-cell artwork");
        var ladder = AssetDatabase.LoadAssetAtPath<GameObject>(Content + "Mechanisms/LadderSideLeft.prefab");
        ObjectArtworkPlanningPreview.TryGet(ladder, out var ladderPreview);
        Check(ladderPreview.FlipX && ladderPreview.Cells == Vector2.one, "left-wall ladder 2D preview mirrors native ladder art");
        var vine = AssetDatabase.LoadAssetAtPath<GameObject>(Content + "ColorBlocks/Prefabs/Green_Ladder.prefab");
        ObjectArtworkPlanningPreview.TryGet(vine, new Vector2(1, 5), null, out var vinePreview);
        Check(vinePreview.Texture.width == 20 && vinePreview.Texture.height == 80 && Near(vinePreview.Cells.x, 1.2f) && Near(vinePreview.Cells.y, 5),
            "TA vine planning preview uses one cached 16-world-pixel five-cell sequence image rather than repeated 3D thumbnails");
        for (int i = 0; i < 3; i++)
        {
            Check(ObjectArtworkPlanningPreview.TryGetWaterTile(i, out var water) && water.Cells == Vector2.one &&
                Near(water.UV.height * water.Texture.height, 16), "water planning preview matches runtime surface/body/interior UV crop " + i);
        }
    }
    private static void KeysAndSupplementary()
    {
        foreach (string color in new[] { "red", "green", "blue" })
        {
            var owner = Spawn("ColorBlocks/Prefabs/ColorKey_" + color + ".prefab");
            try
            {
                var visual = MechanismArtworkPresentation.Ensure(owner).Visual;
                Check(Near(visual.bounds.size.x, 1) && Near(visual.bounds.size.y, 1), color + " key retains native16 pixels despite0.5 prefab root scale");
                Check(Near(visual.bounds.center.y, owner.transform.position.y), color + " key keeps the same collection-flight center");
            }
            finally { Object.DestroyImmediate(owner); }
        }
        foreach (var pair in new[] { ("Red_Foundation.prefab", MechanismArtworkKind.MechanismBase),
            ("Green_PlantObstacle.prefab", MechanismArtworkKind.PlantObstacle), ("CoolLava_Block.prefab", MechanismArtworkKind.Obsidian) })
        {
            var owner = Spawn("ColorBlocks/Prefabs/" + pair.Item1);
            try
            {
                var presentation = MechanismArtworkPresentation.Ensure(owner);
                Check(presentation != null && presentation.Kind == pair.Item2 && presentation.Visual.sprite == artwork.FirstFrame(pair.Item2), "known supplementary art binds to existing gameplay " + pair.Item2);
                if (pair.Item2 == MechanismArtworkKind.Obsidian)
                {
                    var shape = owner.AddComponent<LevelEditorMergedShape>();
                    owner.transform.localScale = new Vector3(3, 2, 1);
                    shape.Configure(new Vector2Int(3, 2), new[] { new RectInt(0, 0, 1, 2), new RectInt(2, 1, 1, 1) });
                    presentation.Refresh(0);
                    Check(!shape.ContainsCell(new Vector2Int(1, 1)) && shape.Rectangles.Count == 2,
                        "obsidian preserves lava-reaction coverage holes rather than filling bounding box");
                    Check(owner.GetComponentsInChildren<SpriteRenderer>().Where(part => part.name.StartsWith("__MergedShapeVisual_", StringComparison.Ordinal)).Count() == 2,
                        "obsidian tiles independent occupied rectangles at native pixel scale");
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }

    private static Texture2D Render(Sprite sprite, float height, bool mask, float opacity, string label)
    {
        const int width = 512, pixelsHigh = 640;
        // Float alpha avoids declaring a faint but real reveal pixel invisible
        // merely because an 8-bit, alpha-blended readback rounded it to zero.
        var target = new RenderTexture(width, pixelsHigh, 24, RenderTextureFormat.ARGBFloat) { name = label, antiAliasing = 1 };
        var previous = RenderTexture.active;
        var owner = new GameObject("GPU sprite " + label);
        var material = new Material(Shader.Find(mask ? "Hidden/2026TapTap/ColorObjectMask" : "Project/Objects/Pixel Sprite Reveal"));
        var command = new CommandBuffer { name = "Artwork pixel regression" };
        try
        {
            Check(material.shader != null && !ShaderUtil.ShaderHasError(material.shader), "GPU shader compiles " + material.shader.name);
            target.Create();
            var renderer = owner.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
            owner.transform.position = new Vector3(0, 0, 0);
            owner.transform.localScale = new Vector3(1.25f / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
            var properties = new MaterialPropertyBlock();
            GridArtworkMetrics.ConfigurePixelSampling(properties, sprite, 1);
            properties.SetTexture("_ColorMaskAlphaTexture", sprite.texture);
            properties.SetFloat("_ColorMaskUseVertexAlpha", 1); properties.SetFloat("_ColorMaskGroup", 1); properties.SetFloat("_ColorMaskReal", 1);
            properties.SetFloat("_ColorRevealOpacity", opacity);
            renderer.SetPropertyBlock(properties);
            material.SetFloat("_ColorRevealOpacity", opacity);
            command.SetGlobalVector("_ColorAppearanceRealAlpha", Vector4.one * opacity);
            command.SetGlobalVector("_ColorAppearanceBaseAlpha", Vector4.one * (1 - opacity));
            command.SetRenderTarget(target); command.ClearRenderTarget(true, true, Color.clear);
            command.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-2, 2, -.5f, 4.5f, -1, 1), true));
            command.DrawRenderer(renderer, material, 0, 0);
            Graphics.ExecuteCommandBuffer(command);
            RenderTexture.active = target;
            var result = new Texture2D(width, pixelsHigh, TextureFormat.RGBAFloat, false, true);
            result.ReadPixels(new Rect(0, 0, width, pixelsHigh), 0, 0); result.Apply();
            var screenshot = new Texture2D(width, pixelsHigh, TextureFormat.RGBA32, false, true);
            screenshot.SetPixels(result.GetPixels()); screenshot.Apply();
            File.WriteAllBytes("Library/" + label + ".png", screenshot.EncodeToPNG());
            Object.DestroyImmediate(screenshot);
            return result;
        }
        finally
        {
            RenderTexture.active = previous; command.Release(); target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(material); Object.DestroyImmediate(owner);
        }
    }
    private static bool Visible(Color32 pixel) => pixel.a > 0;
    private static bool Visible(Color pixel) => pixel.a > .00000001f;
    private static void CompareMask(Texture2D color, Texture2D mask, bool expected, string label)
    {
        var a = color.GetPixels(); var b = mask.GetPixels(); int visible = 0, differences = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (Visible(a[i])) visible++;
            if (Visible(a[i]) != Visible(b[i])) differences++;
        }
        Check(expected ? visible > 50 : visible == 0, label + " has correct transparent handoff visibility");
        Check(differences <= 2, label + " sprite and HSV mask sample the same outline (different pixels=" + differences + ")");
    }
    private static void UniformWorldPixels(Texture2D image, string label)
    {
        const int step = 8; // 128 render pixels per cell / 16 artwork pixels per cell.
        Color32[] pixels = image.GetPixels32(); int mixed = 0, populated = 0;
        for (int y = 0; y < image.height; y += step) for (int x = 0; x < image.width; x += step)
        {
            Color32 reference = pixels[y * image.width + x]; bool active = false, differs = false;
            for (int iy = 0; iy < step; iy++) for (int ix = 0; ix < step; ix++)
            {
                Color32 pixel = pixels[(y + iy) * image.width + x + ix]; active |= Visible(pixel);
                differs |= Math.Abs(pixel.r - reference.r) > 2 || Math.Abs(pixel.g - reference.g) > 2 || Math.Abs(pixel.b - reference.b) > 2 || Math.Abs(pixel.a - reference.a) > 2;
            }
            if (active) populated++; if (differs) mixed++;
        }
        Check(populated > 10 && mixed <= 2, label + " uses equal8x8 screen pixels at1/16 world-cell density (mixed=" + mixed + ")");
    }
    private static void PixelGpu()
    {
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "real GPU available; this audit must not run with -nographics");
        Vector4 oldReal = Shader.GetGlobalVector("_ColorAppearanceRealAlpha"), oldBase = Shader.GetGlobalVector("_ColorAppearanceBaseAlpha");
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[64 * 64];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            pixels[y * 64 + x] = x < 32 ? new Color32(255, 0, 0, 255) : new Color32(0, (byte)((x + y) % 2 == 0 ? 255 : 80), 0, (byte)((x + 2 * y) % 5 == 0 ? 0 : 255));
        texture.SetPixels32(pixels); texture.Apply();
        var frame = Sprite.Create(texture, new Rect(32, 0, 32, 64), new Vector2(.5f, 0), 16, 0, SpriteMeshType.FullRect);
        try
        {
            foreach (var pair in new[] { (frame, 1f, "short"), (frame, 4f, "long"),
                (artwork.vineGrowth[artwork.vineInitialFrame], 1f, "vine_short"), (artwork.vineGrowth[artwork.vineGrownFrame], 4f, "vine_long") })
            {
                foreach (float opacity in new[] { 0f, .25f, .65f, 1f })
                {
                    string label = "ArtGrid_" + pair.Item3 + "_" + Mathf.RoundToInt(opacity * 100);
                    var color = Render(pair.Item1, pair.Item2, false, opacity, label + "_color");
                    var mask = Render(pair.Item1, pair.Item2, true, opacity, label + "_mask");
                    try
                    {
                        CompareMask(color, mask, opacity > 0, label);
                        if (opacity == 1) UniformWorldPixels(color, label);
                        if (pair.Item1 == frame) Check(color.GetPixels32().All(pixel => pixel.r <= 1), label + " cannot sample neighboring red atlas frame");
                    }
                    finally { Object.DestroyImmediate(color); Object.DestroyImmediate(mask); }
                }
            }
        }
        finally
        {
            Shader.SetGlobalVector("_ColorAppearanceRealAlpha", oldReal); Shader.SetGlobalVector("_ColorAppearanceBaseAlpha", oldBase);
            Object.DestroyImmediate(frame); Object.DestroyImmediate(texture);
        }
    }
}
