using System;
using System.IO;
using System.Linq;
using Project.BlockFeatures;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.BossLogic.Editor
{
    public static class BossArtworkVerification
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Debug.Log("BOSS ART PASS: " + message);
        }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("Only run in the disposable probe.");
            try
            {
                BossArtworkSetup.Build();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossArtworkSetup.PrefabPath);
                var root = Object.Instantiate(prefab);
                try
                {
                    root.transform.position = Vector3.zero;
                    var sprite = root.GetComponentInChildren<SpriteRenderer>();
                    var animator = sprite.GetComponent<Animator>();
                    var driver = new SerializedObject(root.GetComponent<BossAnimationDriver>());
                    Check(driver.FindProperty("animator").objectReferenceValue == animator, "driver bound to artwork Animator");
                    Check(driver.FindProperty("waitForPresentationDeathTrigger").boolValue, "death still waits for TA/Timeline handoff");
                    Check(root.transform.localScale == Vector3.one * 3, "physics root scale unchanged");
                    Check(!root.GetComponent<MeshRenderer>().enabled, "only placeholder hidden");
                    Check(Mathf.Abs(sprite.bounds.size.x - 5) < .001f && Mathf.Abs(sprite.bounds.size.y - 5) < .001f, "80 source pixels = 5 cells without inherited 3x stretching");
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BossArtworkSetup.ClipPath);
                    var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    Check(keys.Length == 13 && keys.Select(k => k.value).Distinct().Count() == 12, "12 real frames plus loop closure");
                    animator.Rebind(); animator.Update(0);
                    var first = sprite.sprite;
                    animator.Update(.15f);
                    Check(sprite.sprite != first, "Animator actually advances sprite frames");
                    animator.SetBool("Chasing", false);
                    animator.SetTrigger("Death"); animator.Update(.01f); animator.Update(.01f);
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Death VFX Handoff"), "death trigger hands off instead of playing invented death art");
                    var stopped = sprite.sprite; animator.Update(.3f);
                    Check(sprite.sprite == stopped, "idle sequence stops during death VFX handoff");
                    animator.SetBool("Chasing", true); animator.Update(.01f); animator.Update(.01f);
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle Move"), "reset/chase resumes animation");
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Render(sprite, "BossArtworkPreview", 6);
                }
                finally { Object.DestroyImmediate(root); }
                foreach (var color in new[] { "Red", "Green", "Blue" })
                {
                    var cube = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ContactCubes/" + color + "_ContactCube.prefab"));
                    try
                    {
                        cube.transform.position = Vector3.zero;
                        var presentation = MechanismArtworkPresentation.Ensure(cube);
                        var sprite = presentation.Visual;
                        Debug.Log("CUBE " + color + " bounds=" + sprite.bounds + " drawMode=" + sprite.drawMode + " size=" + sprite.size + " UV=" + string.Join(",", sprite.sprite.uv.Select(v => v.ToString("F5"))));
                        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Render(sprite, color + "CubeArtworkPreview", 1.5f);
                    }
                    finally { Object.DestroyImmediate(cube); }
                }
                File.WriteAllText("Library/BossArtworkVerification.txt", "PASS Boss import, animation, reset and death handoff; GPU=" + SystemInfo.graphicsDeviceType);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText("Library/BossArtworkVerification.txt", e.ToString()); EditorApplication.Exit(1); }
        }
        static void Render(SpriteRenderer sprite, string label, float extent)
        {
            var target = new RenderTexture(768, 768, 0, RenderTextureFormat.ARGB32);
            var command = new CommandBuffer();
            var before = RenderTexture.active;
            try
            {
                target.Create(); command.SetRenderTarget(target); command.ClearRenderTarget(true, true, new Color(.14f, .14f, .17f, 1));
                command.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-extent, extent, -extent, extent, -100, 100), true));
                command.DrawRenderer(sprite, sprite.sharedMaterial, 0, 0); Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = target;
                var image = new Texture2D(768, 768, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); image.Apply();
                File.WriteAllBytes("Library/" + label + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { RenderTexture.active = before; command.Release(); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
