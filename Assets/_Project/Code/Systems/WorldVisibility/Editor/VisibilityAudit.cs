using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Project.WorldVisibility.Editor
{
    /// <summary>Explicit silent disposable-project verification. Never edits an open authoring scene.</summary>
    public static class VisibilityAudit
    {
        static int checks;
        static void Check(bool value,string text) { checks++; if (!value) throw new Exception(text); }
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\','/').EndsWith("/Library/FlowRetirementProbe/Assets",StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only use the disposable FlowRetirementProbe.");
            try
            {
                VisibilitySetup.BuildAssets();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisibilitySetup.FountainPrefab);
                Check(prefab && prefab.GetComponent<FountainArtwork>().FrameCount == 8,"eight fountain frames in prefab");
                Check(prefab.GetComponentsInChildren<Collider>().Length == 0,"fountain is presentation only");
                foreach (var frame in AssetDatabase.FindAssets("t:Texture2D",new[] { "Assets/_Project/Resources/26TAPTAP/喷泉动画" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(frame); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Check(sprite && sprite.rect.size == new Vector2(82,66) && sprite.pixelsPerUnit == 16,"native full-frame sprite dimensions");
                    Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None &&
                        importer.textureCompression == TextureImporterCompression.Uncompressed,"pixel import configuration");
                    Check(sprite.pivot == new Vector2(41,0),"stable bottom-centre pivot");
                }
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var camera = new GameObject("光照审计相机").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0,0,-10); camera.orthographic = true; camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white; camera.allowHDR = false;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                var viewer = new GameObject("光照审计观察者").transform;
                var system = camera.gameObject.AddComponent<WorldVisibility2D>(); system.targetCamera = camera; system.viewer = viewer;
                system.connectedSpaceVisibility = false; // Explicit coverage of the retained legacy lighting mode.
                system.findPlayer = false; system.previewInEditor = true; system.playerLightRadiusCells = 9; system.sightRadiusCells = 12;
                system.ambient = 0; system.maskResolution = 1;
                var wall = new GameObject("审计墙体").AddComponent<BoxCollider>(); wall.transform.position = new Vector3(1,0,0); wall.size = new Vector3(.5f,4,1);
                var occluder = wall.gameObject.AddComponent<VisibilityOccluder2D>(); occluder.shapes = new[] { wall };
                Check(system.Prepare(camera),"prepare camera");
                var mesh = system.SightMesh;
                Check(mesh && mesh.vertexCount > 128,"endpoint rays augment circular boundary");
                int revision = system.GeometryRevision; system.Prepare(camera);
                Check(system.SightMesh == mesh && system.GeometryRevision == revision,"unchanged geometry reuses mesh and revision");
                system.sightRadiusCells = 9; system.playerLightRadiusCells = 12; system.Prepare(camera);
                Check(system.SightMesh.bounds.size.y < 18.01f,"swapping sight and light ranges invalidates cached geometry");
                system.sightRadiusCells = 12; system.playerLightRadiusCells = 9;
                var edges = new List<VisibilityGeometry.Segment>(); VisibilityGeometry.AddBox(wall,edges);
                Check(Mathf.Abs(VisibilityGeometry.RayDistance(Vector2.zero,Vector2.right,edges[3],20)-.75f)<.001f,"wall at real near face");
                Check(VisibilityGeometry.RayDistance(Vector2.zero,Vector2.up,edges[3],20)==20,"parallel ray no false hit");
                // Exact two-cell border cancellation must not introduce a line through their union.
                edges.Clear(); wall.size = Vector3.one; wall.transform.position = Vector3.zero; VisibilityGeometry.AddBox(wall,edges);
                wall.transform.position = Vector3.right; VisibilityGeometry.AddBox(wall,edges); VisibilityGeometry.Compact(edges);
                Check(edges.Count == 4,"adjacent wall edges compact into exterior rectangle");
                wall.transform.position = new Vector3(1,0,0); wall.size = new Vector3(.5f,4,1);
                var target = new RenderTexture(640,400,24); camera.targetTexture = target; camera.aspect = 1.6f;
                var image = new Texture2D(640,400,TextureFormat.RGBA32,false);
                Capture("Visibility_Wall.png");
                Check(Sample(new Vector2(-1,0)).r>.5f,"GPU player-side illuminated");
                Check(Sample(new Vector2(2,0)).r<.03f,"GPU wall hides opposite side");
                for (int i=0;i<20;i++)
                {
                    camera.transform.position = new Vector3(i*.0031f,i*.0027f,-10);
                    Capture("Visibility_CameraMotion.png");
                    Check(Sample(new Vector2(-1,0)).r>.5f && Sample(new Vector2(2,0)).r<.03f,"final camera movement preserves occlusion alignment");
                }
                camera.transform.position = new Vector3(0,0,-10);
                var lamp = new GameObject("墙后审计光源").AddComponent<VisibilityLight2D>(); lamp.transform.position = new Vector3(2,0,0); lamp.radiusCells = 3;
                Capture("Visibility_HiddenLamp.png"); Check(Sample(new Vector2(2,0)).r<.03f,"distant light never reveals through wall");
                system.respectPlayerSight = false;
                Capture("Visibility_IndependentLamp.png"); Check(Sample(new Vector2(2,0)).r>.5f,"optional independent scene illumination");
                system.respectPlayerSight = true; wall.enabled = false;
                Capture("Visibility_Open.png"); Check(Sample(new Vector2(2,0)).r>.4f,"disabled/open occluder restores sight");
                wall.enabled = true; wall.transform.position = new Vector3(-1,0,0);
                Capture("Visibility_MovedWall.png"); Check(Sample(new Vector2(-2,0)).r<.03f,"moving occluder updates without room transition");
                Check(Sample(new Vector2(2,0)).r>.4f,"previous occluder position leaves no stale shadow");
                // Two separated actual wall volumes leave a real passage, not their union AABB.
                wall.transform.position = new Vector3(1,2,0); wall.size = new Vector3(.5f,2,1);
                var lower = new GameObject("审计下墙").AddComponent<BoxCollider>(); lower.transform.position = new Vector3(1,-2,0); lower.size = new Vector3(.5f,2,1);
                occluder.shapes = new[] {wall,lower};
                Capture("Visibility_Gap.png"); Check(Sample(new Vector2(2,0)).r>.4f,"real gap between irregular wall volumes passes light");
                Check(Sample(new Vector2(2,3)).r<.03f,"solid beside gap still blocks");
                var watch = Stopwatch.StartNew();
                for (int i=0;i<300;i++) { viewer.position = new Vector3(i*.0001f,0,0); system.Prepare(camera); }
                watch.Stop(); double fixtureMilliseconds = watch.Elapsed.TotalMilliseconds/300;
                camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                // Verify actual local Pick as a read-only snapshot. No scene is ever saved by this audit.
                string pickPath = "Assets/Scenes/Pick.unity";
                EditorSceneManager.OpenScene(pickPath);
                camera = Camera.main; Check(camera,"Pick main camera");
                var player = GameObject.FindGameObjectWithTag("Player"); Check(player,"Pick standard player");
                system = camera.gameObject.AddComponent<WorldVisibility2D>(); system.targetCamera = camera; system.viewer = player.transform;
                system.previewInEditor = true; system.playerLightRadiusCells = 16; system.sightRadiusCells = 28;
                var a = new GameObject("Pick 测试暖光").AddComponent<VisibilityLight2D>(); a.transform.position = player.transform.position+new Vector3(3,2,0); a.radiusCells = 6;
                var b = new GameObject("Pick 测试蓝光").AddComponent<VisibilityLight2D>(); b.transform.position = player.transform.position+new Vector3(-4,2,0); b.radiusCells = 6; b.tint = new Color(.5f,.7f,1);
                camera.transform.position = new Vector3(player.transform.position.x,player.transform.position.y+2,-10);
                camera.orthographic = true; camera.orthographicSize = 10; camera.aspect = 1.6f;
                target = new RenderTexture(960,600,24); image = new Texture2D(960,600,TextureFormat.RGBA32,false); camera.targetTexture = target;
                system.Prepare(camera); Check(system.CachedSegments>0,"Pick actual solid geometry cached");
                Capture("Visibility_Pick.png");
                watch.Restart(); for (int i=0;i<120;i++) { player.transform.position += new Vector3(.001f,0,0); system.Prepare(camera); }
                watch.Stop(); double pickMilliseconds = watch.Elapsed.TotalMilliseconds/120;
                camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                var report = $"PASS {checks} assertions; 8 native82x66 frames, exact export1, PPU16; real URP/D3D11 GPU occlusion/open/moving/source gate; Pick read-only snapshot {system.CachedSegments} static segments. CPU Prepare fixture={fixtureMilliseconds:F3}ms, Pick={pickMilliseconds:F3}ms (local Editor, not Android budget or full frame profiler). No scene saved.\n";
                File.WriteAllText("Library/VisibilityAudit.txt",report); Debug.Log(report); EditorApplication.Exit(0);
                void Capture(string path)
                {
                    camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                    image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); RenderTexture.active = previous;
                    File.WriteAllBytes("Library/"+path,image.EncodeToPNG());
                }
                Color Sample(Vector2 world)
                {
                    var viewport = camera.WorldToViewportPoint(world);
                    return image.GetPixel(Mathf.Clamp((int)(viewport.x*image.width),0,image.width-1),Mathf.Clamp((int)(viewport.y*image.height),0,image.height-1));
                }
            }
            catch(Exception error) { Debug.LogException(error); File.WriteAllText("Library/VisibilityAudit.txt","FAIL "+error); EditorApplication.Exit(1); }
        }
    }
}
