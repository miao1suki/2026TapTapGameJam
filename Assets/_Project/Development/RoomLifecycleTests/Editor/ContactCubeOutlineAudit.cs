using System;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ContactCubes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Run only in the isolated probe. Uses the real sprite shader rather than a
// resized PNG, so regressions in tiled sprite geometry/sampling are observable.
public static class ContactCubeOutlineAudit
{
    public static void Run()
    {
        if (!Application.dataPath.Contains("FlowRetirementProbe"))
            throw new InvalidOperationException("Run in the isolated FlowRetirementProbe project.");
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            throw new InvalidOperationException("This audit requires a real GPU (no -nographics).");
        int frames = 0;
        try
        {
            foreach (string color in new[] { "Red", "Green", "Blue" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/ContactCubes/" + color + "_ContactCube.prefab");
                var owner = Object.Instantiate(prefab);
                try
                {
                    var motion = owner.GetComponent<ContactCubeMotion>();
                    // Editor instantiation does not invoke the runtime Awake.
                    // Exercise the production clearance setup, not a manually
                    // patched .96 fixture with no logical-size bookkeeping.
                    typeof(ContactCubeMotion).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(motion, null);
                    var shape = owner.GetComponentInChildren<BoxCollider>();
                    Vector3 physicalSize = shape.size;
                    var visual = MechanismArtworkPresentation.Ensure(owner).Visual;
                    if (color != "Green")
                    {
                        if (Mathf.Abs(physicalSize.x - .96f) > .0001f || Mathf.Abs(visual.size.x - 1f) > .0001f)
                            throw new InvalidOperationException(color + " does not separate .96 physical width from full-cell artwork.");
                        // Negative control reproduces the old exact layout:
                        // left texel survives, the last/right texel is cropped.
                        Vector2 size = visual.size;
                        visual.size = new Vector2(physicalSize.x, size.y);
                        bool oldFailed = false;
                        try { Render(visual, color + "Legacy", 128f, 0f); }
                        catch (InvalidOperationException) { oldFailed = true; }
                        visual.size = size;
                        if (!oldFailed) throw new InvalidOperationException("Legacy cropped-width control did not reproduce: " + color);
                    }
                    foreach (float pixelsPerCell in new[] { 64f, 100f, 128f })
                    foreach (float phase in new[] { 0f, .25f, .5f, .75f })
                    {
                        // Integer widths give an unambiguous equal-border
                        // assertion while subpixel camera phases still vary.
                        Render(visual, color, pixelsPerCell, phase);
                        frames++;
                    }
                    if (shape.size != physicalSize)
                        throw new InvalidOperationException("Artwork changed the physical clearance box.");
                }
                finally { Object.DestroyImmediate(owner); }
            }
            File.WriteAllText("Library/ContactCubeOutlineAudit.txt", "PASS " + frames + " GPU frames; native source-pixel border symmetry preserved.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("Library/ContactCubeOutlineAudit.txt", e.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static void Render(SpriteRenderer visual, string color, float pixelsPerCell, float phase)
    {
        const int width = 256;
        var rt = new RenderTexture(width, width, 0, RenderTextureFormat.ARGB32);
        var command = new CommandBuffer();
        var before = RenderTexture.active;
        var image = new Texture2D(width, width, TextureFormat.RGBA32, false);
        try
        {
            rt.Create();
            var background = new Color(.8f, .15f, .1f, 1f);
            command.SetRenderTarget(rt);
            command.ClearRenderTarget(true, true, background);
            float extent = width / (2f * pixelsPerCell);
            var bounds = visual.bounds;
            Vector3 center = bounds.center + Vector3.right * (phase / pixelsPerCell);
            command.SetViewProjectionMatrices(Matrix4x4.Translate(-center),
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-extent, extent, -extent, extent, -100, 100), true));
            command.DrawRenderer(visual, visual.sharedMaterial, 0, 0);
            Graphics.ExecuteCommandBuffer(command);
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, width), 0, 0);
            image.Apply();
            // Both edge columns are the same dark source texel. Sample away
            // from the internal cross and count edge-colored runs at each side.
            Color32[] pixels = image.GetPixels32();
            int row = width / 2 - Mathf.RoundToInt(pixelsPerCell * .4f);
            float projectedWidth = bounds.size.x * pixelsPerCell;
            int left = Mathf.CeilToInt(width / 2f - projectedWidth / 2f - phase - .5f);
            int right = Mathf.CeilToInt(width / 2f + projectedWidth / 2f - phase - .5f) - 1;
            Color32 edge = pixels[row * width + left];
            int l = 0, r = 0;
            while (l < pixelsPerCell / 4 && Same(pixels[row * width + left + l], edge)) l++;
            while (r < pixelsPerCell / 4 && Same(pixels[row * width + right - r], edge)) r++;
            if (phase == 0 && pixelsPerCell == 128)
                File.WriteAllBytes("Library/" + color + "CubeOutlineAudit.png", image.EncodeToPNG());
            if (Mathf.Abs(l - r) > 1 || l < pixelsPerCell / 16 - 1 || r < pixelsPerCell / 16 - 1)
                throw new InvalidOperationException(color + " border mismatch: left=" + l + ", right=" + r + ", ppc=" + pixelsPerCell + ", phase=" + phase);
        }
        finally
        {
            RenderTexture.active = before;
            command.Release();
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(image);
        }
    }

    private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
}
