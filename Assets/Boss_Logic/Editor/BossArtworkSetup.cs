using System;
using System.IO;
using System.Linq;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project.BossLogic.Editor
{
    /// <summary>Explicit, repeatable art import; never runs on scene load.</summary>
    public static class BossArtworkSetup
    {
        public const string PrefabPath = "Assets/_Project/Content/Boss/Boss(暂定).prefab";
        public const string Source = "Assets/_Project/Resources/26TAPTAP/Boss_Idle&Move_";
        public const string Output = "Assets/Boss_Logic/Artwork";
        public const string ClipPath = Output + "/Boss Idle Move.anim";
        public const string ControllerPath = Output + "/Boss Artwork.controller";
        public const float FramesPerSecond = 12f;

        [MenuItem("Tools/2026TapTap/Boss/接入待机与移动美术")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/Boss_Logic", "Artwork");
            string[] paths = Directory.GetFiles(Source, "Boss_Idle&Move_*.png").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Length != 12) throw new InvalidOperationException("Boss 待机移动序列应有12帧。");
            var frames = new Sprite[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i].Replace('\\', '/');
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!raw.LoadImage(File.ReadAllBytes(path)) || raw.width != 80 || raw.height != 80 ||
                        GridArtworkMetrics.ExactExportScale(raw.GetPixels32(), raw.width, raw.height) != 1)
                        throw new InvalidOperationException("Boss 帧尺寸／有效像素倍率与80×80原画标准不符：" + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(raw); }
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = GridArtworkMetrics.PixelsPerCell;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 128;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                foreach (string target in new[] { "Standalone", "Android", "iPhone" })
                {
                    var platform = importer.GetPlatformTextureSettings(target);
                    platform.overridden = false;
                    importer.SetPlatformTextureSettings(platform);
                }
                importer.SaveAndReimport();
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
            clip.frameRate = FramesPerSecond;
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / FramesPerSecond, value = frames[i % frames.Length] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.AddParameter("Chasing", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
                var machine = controller.layers[0].stateMachine;
                var alive = machine.AddState("Idle Move");
                alive.motion = clip; alive.writeDefaultValues = false;
                machine.defaultState = alive;
                // No fabricated death animation: stop changing the sprite and
                // leave deathStarted + the existing Timeline available to TA VFX.
                var death = machine.AddState("Death VFX Handoff");
                death.writeDefaultValues = false;
                var stop = alive.AddTransition(death);
                stop.hasExitTime = false; stop.duration = 0;
                stop.AddCondition(AnimatorConditionMode.If, 0, "Death");
                var resume = death.AddTransition(alive);
                resume.hasExitTime = false; resume.duration = 0;
                resume.AddCondition(AnimatorConditionMode.If, 0, "Chasing");
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform child = root.transform.Find("Boss Artwork");
                if (child == null) { child = new GameObject("Boss Artwork").transform; child.SetParent(root.transform, false); }
                var sprite = child.GetComponent<SpriteRenderer>();
                if (sprite == null) sprite = child.gameObject.AddComponent<SpriteRenderer>();
                sprite.sprite = frames[0];
                sprite.sharedMaterial = ObjectArtworkLibrary.Load().neutralSpriteMaterial;
                sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sprite.receiveShadows = false;
                child.localPosition = new Vector3(0, 0, -.515f);
                child.localRotation = Quaternion.identity;
                child.localScale = GridArtworkMetrics.NaturalSpriteScale(frames[0], root.transform.lossyScale, 1f);
                var animator = child.GetComponent<Animator>();
                if (animator == null) animator = child.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.GetComponent<BossAnimationDriver>().SetAnimator(animator);
                // Only replace this known placeholder, not health bars or TA particles.
                var placeholder = root.GetComponent<MeshRenderer>();
                if (placeholder != null) placeholder.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Boss artwork: 12 frames, 12fps, 16px/cell, unchanged physics and death Timeline.");
        }
    }
}
