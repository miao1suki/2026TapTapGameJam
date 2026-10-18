using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Mechanisms;
using Project.ObjectArtwork;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Disposable-probe-only asset upgrade and native-pixel/platform regression.</summary>
[InitializeOnLoad]
public static class PlatformArtworkRefreshAudit
{
    private const string Root = "Assets/_Project/Resources/26TAPTAP/物体道具/";
    private const string PlayKey = "PlatformArtworkRefreshAudit.Play";
    private static int checks;
    private static ObjectArtworkLibrary library;
    static PlatformArtworkRefreshAudit()
    {
        if (SessionState.GetBool(PlayKey, false)) EditorApplication.update += PlayUpdate;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("PLATFORM ART FAIL: " + message);
        checks++; Debug.Log("PLATFORM ART PASS: " + message);
    }
    public static void BuildAndVerify()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
            throw new InvalidOperationException("仅允许静音隔离工程升级与验证。");
        int exit = 0;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            library = ObjectArtworkLibrary.Load();
            string[] vineIds = library.vineGrowth.Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()).ToArray();
            string[] signs = library.signboards.Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()).ToArray();
            WorldObjectArtworkSetup.BuildPlatformFrames();
            WorldObjectArtworkSetup.BuildColoredOneWayPlatforms();
            Check(vineIds.SequenceEqual(library.vineGrowth.Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString())), "192 TA vine references untouched");
            Check(signs.SequenceEqual(library.signboards.Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString())), "signboard references untouched");
            foreach (var pair in new[] { (MechanismArtworkKind.CrankPlatform, 6, "43452C733652487AC26FD041686C880B.png"),
                (MechanismArtworkKind.MovingPlatform, 9, "BB587FC3F1E5565D857F67F813F19880.png") })
            {
                var entry = library.Find(pair.Item1);
                Check(entry.frames.Length == pair.Item2 && entry.frames.All(frame => frame.rect.size == new Vector2(48, 16)), pair.Item1 + " complete48x16 frames and correct count");
                Check(entry.supportTopPixels == 14 && entry.frames.All(frame => frame.pixelsPerUnit == 16), pair.Item1 + " native16PPU and support at14px");
                Check(File.ReadAllBytes(Root + pair.Item3).SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(entry.frames[0]))), pair.Item1 + " generated image exactly matches new artist source");
                var importer = (TextureImporter)AssetImporter.GetAtPath(Root + pair.Item3);
                Check(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple &&
                    importer.maxTextureSize == 512 && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.npotScale == TextureImporterNPOTScale.None && importer.textureCompression == TextureImporterCompression.Uncompressed,
                    pair.Item1 + " source pixel-safe import settings");
            }
            Platforms(); Colors(); GpuContactSheet();
            string[] ids = library.mechanisms.SelectMany(entry => entry.frames).Where(sprite => sprite != null)
                .Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()).ToArray();
            WorldObjectArtworkSetup.BuildPlatformFrames(); WorldObjectArtworkSetup.BuildColoredOneWayPlatforms();
            Check(ids.SequenceEqual(library.mechanisms.SelectMany(entry => entry.frames).Where(sprite => sprite != null)
                .Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString())), "repeat build preserves all persistent sprite IDs");
            File.WriteAllText("Library/PlatformArtworkRefreshResult.txt", "PASS " + checks + " platform artwork assertions");
            Debug.Log("PLATFORM ART FINAL PASS " + checks);
        }
        catch (Exception error)
        {
            exit = 1; File.WriteAllText("Library/PlatformArtworkRefreshResult.txt", error.ToString()); Debug.LogException(error);
        }
        EditorApplication.Exit(exit);
    }

    public static void RunPlayVerification()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
            throw new InvalidOperationException("仅允许静音隔离工程验证。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(PlayKey, true);
        EditorApplication.update -= PlayUpdate; EditorApplication.update += PlayUpdate;
        EditorApplication.EnterPlaymode();
    }
    private static void PlayUpdate()
    {
        if (!SessionState.GetBool(PlayKey, false) || !EditorApplication.isPlaying || Time.time < .5f) return;
        SessionState.SetBool(PlayKey, false); EditorApplication.update -= PlayUpdate;
        int exit = 0;
        try
        {
            checks = 0; library = ObjectArtworkLibrary.Load();
            ColorRuntimeService.Instance.ResetProgress();
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab")).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false; player.Motor.isKinematic = true;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Mechanisms/OneWayPlatform.prefab");
            foreach (var color in new[] { OneWayPlatformColor.Red, OneWayPlatformColor.Green, OneWayPlatformColor.Blue })
            {
                var owner = Object.Instantiate(prefab);
                var platform = owner.GetComponent<OneWayPlatform>(); platform.ConfigureAppearance(color);
                var presentation = MechanismArtworkPresentation.Ensure(owner); presentation.Refresh(0);
                player.transform.position = Vector3.up; player.Motor.position = Vector3.up;
                Physics.SyncTransforms();
                Check(!ColorRuntimeService.Instance.IsUnlocked(platform.AppearanceColorId) && platform.enabled && platform.CanSupport(player),
                    color + " locked appearance does not disable runtime physical support");
                Check(presentation.Visual.enabled && presentation.Visual.gameObject.layer == platform.ArtworkLayer &&
                    presentation.Visual.sharedMaterial == library.neutralSpriteMaterial, color + " visible neutral material uses classified HSV layer");
                var block = new MaterialPropertyBlock(); presentation.Visual.GetPropertyBlock(block);
                Check(block.GetFloat("_ColorMaskGroup") == -1f && block.GetFloat("_ColorMaskOpacity") == 1f,
                    color + " runtime mask follows decoration semantics, not color reveal opacity");
                platform.ResetForRoom(); presentation.Refresh(0);
                Check(platform.enabled && platform.CanSupport(player) && presentation.Visual.enabled && presentation.Visual.sharedMaterial != null,
                    color + " room reset keeps artwork and physical support");
                ColorRuntimeService.Instance.Unlock(platform.AppearanceColorId); presentation.Refresh(0);
                Check(platform.CanSupport(player) && presentation.Visual.enabled && presentation.Visual.sharedMaterial == library.neutralSpriteMaterial,
                    color + " unlocking color does not replace material or change gameplay");
                Object.DestroyImmediate(owner);
            }
            File.WriteAllText("Library/PlatformArtworkPlayResult.txt", "PASS " + checks + " runtime platform assertions");
            Debug.Log("PLATFORM ART PLAY FINAL PASS " + checks);
        }
        catch (Exception error)
        {
            exit = 1; File.WriteAllText("Library/PlatformArtworkPlayResult.txt", error.ToString()); Debug.LogException(error);
        }
        EditorApplication.Exit(exit);
    }
    private static void Platforms()
    {
        foreach (string name in new[] { "Red_Platform", "Red_CrankLift" })
        foreach (int length in new[] { 1, 2, 3, 5 })
        foreach (bool vertical in new[] { false, true })
        {
            var owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/" + name + ".prefab"));
            try
            {
                // Audit the same exact authored box in both orientations, without
                // changing the real prefab's thin/legacy collision contract.
                var box = owner.GetComponent<BoxCollider>();
                // The crank prefab retains a thin legacy BoxCollider. Author a
                // true length-cell vertical fixture through root scale, exactly
                // as the planner does, without changing that collider contract.
                owner.transform.localScale = vertical ? new Vector3(1f / box.size.x, length / box.size.y, 1)
                    : new Vector3(length / box.size.x, 1, 1);
                // Collider.bounds is native-physics cached after an immediate
                // edit-mode Transform change. Flush only in this audit fixture,
                // not in the production artwork path, before comparing edges.
                Physics.SyncTransforms();
                string before = EditorJsonUtility.ToJson(box);
                var presentation = MechanismArtworkPresentation.Ensure(owner); presentation.Refresh(0);
                var visual = presentation.Visual;
                bool actualVertical = ObjectArtworkLibrary.PlatformVisualIsVertical(box.bounds.size);
                Check(Mathf.Abs(visual.transform.TransformVector(Vector3.right / 16).magnitude - 1f / 16) < .0001f &&
                    Mathf.Abs(visual.transform.TransformVector(Vector3.up / 16).magnitude - 1f / 16) < .0001f,
                    name + " square native pixels length" + length + " vertical" + vertical);
                Vector3 support = visual.transform.TransformPoint(new Vector3(0, 14f / 16, 0));
                Check(actualVertical ? Mathf.Abs(support.x - box.bounds.min.x) < .0001f : Mathf.Abs(support.y - box.bounds.max.y) < .0001f,
                    name + " opaque edge exactly matches support length" + length + " vertical" + vertical +
                    " actualVertical=" + actualVertical + " support=" + support.ToString("F5") + " bounds=" + box.bounds);
                Check(before == EditorJsonUtility.ToJson(box), name + " visual keeps original collision length" + length + " vertical" + vertical);
                Check(visual.sprite.rect.width <= length * 16 && visual.sprite.rect.height == 16,
                    name + " full frame with native short crop length" + length + " vertical" + vertical);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
    private static void Colors()
    {
        var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
        string[] ids = { PlanningDoorUtility.RedOneWayPlatformEntryId, PlanningDoorUtility.GreenOneWayPlatformEntryId, PlanningDoorUtility.BlueOneWayPlatformEntryId };
        for (int i = 0; i < 3; i++)
        {
            var entries = palette.PropEntries.Where(entry => entry.EntryId == ids[i]).ToArray();
            Check(entries.Length == 1, "one stable palette entry for " + ids[i]);
            var entry = entries[0];
            Check(PlanningDoorUtility.Footprint(entry.EntryId) == Vector2Int.one, "new16px one-way tile defaults to one cell " + ids[i]);
            var owner = Object.Instantiate(entry.Prefab);
            try
            {
                var shape = owner.GetComponent<BoxCollider>(); string before = EditorJsonUtility.ToJson(shape);
                LevelEditorComponentOverrideUtility.ApplyOverrides(owner, entry.ComponentValueOverrides, false);
                var platform = owner.GetComponent<OneWayPlatform>();
                Check((int)platform.AppearanceColor == i + 1 && owner.GetComponent<ColorObject>() == null && shape.enabled,
                    "appearance enum persists without color-object gating " + ids[i]);
                var presentation = MechanismArtworkPresentation.Ensure(owner);
                Check(presentation.Visual.sprite.rect.size == new Vector2(16, 16) && presentation.Visual.enabled,
                    "full16px colored art shown while colors remain locked " + ids[i]);
                Check(ObjectArtworkPlanningPreview.TryGet(entry.Prefab, entry.ComponentValueOverrides, out var preview) &&
                    preview.Sprite == library.FirstFrame(ObjectArtworkLibrary.OneWayPlatformKind(i + 1)) && preview.Cells == Vector2.one,
                    "palette and canvas resolve override art " + ids[i]);
                Check(presentation.Visual.gameObject.layer == platform.ArtworkLayer && platform.ArtworkLayer != 0,
                    "visual has its color HSV layer without changing physics layer " + ids[i]);
                platform.ConfigureAppearance((OneWayPlatformColor)(i == 2 ? 1 : i + 2)); presentation.Refresh(0);
                Check(presentation.Visual.sprite == library.FirstFrame(platform.ArtworkKind) && before == EditorJsonUtility.ToJson(shape),
                    "changing color swaps only art and keeps collision " + ids[i]);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(library.FirstFrame(platform.ArtworkKind))));
                    Check(texture.GetPixels32().All(color => color.a == 0 || color.r > 7 || color.g > 7 || color.b > 7), "pure-black source background cleared " + ids[i]);
                }
                finally { Object.DestroyImmediate(texture); }
            }
            finally { Object.DestroyImmediate(owner); }
        }
        Check(PlanningDoorUtility.Footprint(PlanningDoorUtility.OneWayPlatformEntryId) == new Vector2Int(3, 1), "legacy white one-way placement and ID retained");
    }

    private static void GpuContactSheet()
    {
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "real GPU available for platform contact sheet");
        const int width = 1536, height = 1152;
        var owners = new List<GameObject>();
        var sprites = new List<Sprite>();
        var textures = new List<Texture2D>();
        var renderers = new List<SpriteRenderer>();
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var command = new CommandBuffer { name = "Platform artwork support and native pixel contact sheet" };
        RenderTexture previous = RenderTexture.active;
        try
        {
            // One source grid pixel is one artwork pixel, rendered at4x. All
            // platforms below are real prefab presenters, not software thumbnails.
            var grid = new Texture2D(24 * 16, 18 * 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            textures.Add(grid);
            var pixels = new Color32[grid.width * grid.height];
            for (int y = 0; y < grid.height; y++) for (int x = 0; x < grid.width; x++)
                pixels[y * grid.width + x] = x % 16 == 0 || y % 16 == 0
                    ? new Color32(50, 61, 77, 255) : new Color32(30, 37, 49, 255);
            grid.SetPixels32(pixels); grid.Apply();
            var gridSprite = Sprite.Create(grid, new Rect(0, 0, grid.width, grid.height), Vector2.zero, 16, 0, SpriteMeshType.FullRect);
            sprites.Add(gridSprite);
            var white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            sprites.Add(white);
            SpriteRenderer Quad(Sprite sprite, Vector3 position, Vector3 scale, Color tint)
            {
                var owner = new GameObject("Contact-sheet backing guide"); owners.Add(owner);
                owner.transform.SetPositionAndRotation(position, Quaternion.identity); owner.transform.localScale = scale;
                var renderer = owner.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.sharedMaterial = library.neutralSpriteMaterial; renderer.color = tint; renderers.Add(renderer);
                return renderer;
            }
            Quad(gridSprite, Vector3.zero, Vector3.one, Color.white);
            void Backing(Bounds b, bool vertical)
            {
                var tint = new Color(.62f, .65f, .7f, 1);
                Quad(white, new Vector3(b.center.x, b.center.y, .5f), new Vector3(b.size.x, b.size.y, 1), tint);
                if (!vertical)
                {
                    foreach (float x in new[] { b.min.x - .25f, b.max.x + .25f })
                        Quad(white, new Vector3(x, b.max.y - .5f, .5f), new Vector3(.5f, 1, 1), tint);
                }
                else foreach (float y in new[] { b.min.y - .25f, b.max.y + .25f })
                    Quad(white, new Vector3(b.min.x + .5f, y, .5f), new Vector3(1, .5f, 1), tint);
            }
            var visuals = new List<SpriteRenderer>();
            for (int row = 0; row < 2; row++) for (int column = 0; column < 6; column++)
            {
                string name = row == 0 ? "Red_Platform" : "Red_CrankLift";
                var owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/" + name + ".prefab"));
                owners.Add(owner);
                int length = column % 3 + 1; bool vertical = column >= 3;
                owner.transform.position = new Vector3(2 + column * 4, 14 - row * 6, 0);
                var box = owner.GetComponent<BoxCollider>();
                owner.transform.localScale = vertical ? new Vector3(1f / box.size.x, length / box.size.y, 1)
                    : new Vector3(length / box.size.x, 1, 1);
                // A square1x1 footprint has no long-axis hint; rotate its authored
                // root to also exercise a genuinely vertical one-cell sprite.
                if (vertical && length == 1) owner.transform.rotation = Quaternion.Euler(0, 0, 90);
                Physics.SyncTransforms();
                var presentation = MechanismArtworkPresentation.Ensure(owner); presentation.Refresh(0);
                Backing(owner.GetComponent<BoxCollider>().bounds, vertical); visuals.Add(presentation.Visual);
            }
            for (int i = 0; i < 3; i++)
            {
                var owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Mechanisms/OneWayPlatform.prefab"));
                owners.Add(owner); owner.transform.position = new Vector3(4 + i * 8, 2.5f, 0);
                owner.GetComponent<OneWayPlatform>().ConfigureAppearance((OneWayPlatformColor)(i + 1));
                Physics.SyncTransforms();
                var presentation = MechanismArtworkPresentation.Ensure(owner); presentation.Refresh(0);
                Backing(owner.GetComponent<BoxCollider>().bounds, false); visuals.Add(presentation.Visual);
            }
            target.Create(); command.SetRenderTarget(target); command.ClearRenderTarget(true, true, Color.clear);
            command.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, 24, 0, 18, -10, 10), true));
            foreach (var renderer in renderers) command.DrawRenderer(renderer, renderer.sharedMaterial, 0, 0);
            foreach (var renderer in visuals) command.DrawRenderer(renderer, renderer.sharedMaterial, 0, 0);
            Graphics.ExecuteCommandBuffer(command); RenderTexture.active = target;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false, true); textures.Add(result);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.Apply();
            if (SystemInfo.graphicsUVStartsAtTop)
            {
                // GL.GetGPUProjectionMatrix(...,true) flips the Direct3D render
                // target; make the saved PNG top row match the world top row.
                var source = result.GetPixels32(); var upright = new Color32[source.Length];
                for (int y = 0; y < height; y++) Array.Copy(source, y * width, upright, (height - 1 - y) * width, width);
                result.SetPixels32(upright); result.Apply();
            }
            Check(result.GetPixels32().Count(pixel => pixel.a > 200 && pixel.r > pixel.g * 1.4f && pixel.r > 80) > 1000,
                "GPU contact sheet contains actual rendered platform pixels");
            File.WriteAllBytes("Library/PlatformArtworkContactSheet.png", result.EncodeToPNG());
            File.WriteAllText("Library/PlatformArtworkContactSheet.txt", "Top row: automatic platform. Middle row: crank platform. Each row columns: horizontal1,2,3 cells; vertical1,2,3 cells. Bottom: red, green, blue one-way. Grey rectangles show unchanged colliders and adjoining walls.64 screen pixels/cell =4 screen pixels/art pixel. All images are rendered from actual SpriteRenderer geometry/materials, not pasted thumbnails.");
        }
        finally
        {
            RenderTexture.active = previous; command.Release(); target.Release(); Object.DestroyImmediate(target);
            foreach (var owner in owners) if (owner != null) Object.DestroyImmediate(owner);
            foreach (var sprite in sprites) if (sprite != null) Object.DestroyImmediate(sprite);
            foreach (var texture in textures) if (texture != null) Object.DestroyImmediate(texture);
        }
    }
}
