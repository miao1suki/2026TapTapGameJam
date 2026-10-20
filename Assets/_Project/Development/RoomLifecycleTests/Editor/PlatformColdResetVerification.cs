using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Rooms.Editor
{
    // Run only in the disposable probe, never change an author's open scene/save.
    public static class PlatformColdResetVerification
    {
        private const string Flag = "PlatformColdResetVerification";
        private static int checks;
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("Use isolated FlowRetirementProbe.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }
        public static void RunFormal()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
                throw new InvalidOperationException("Use isolated FlowRetirementProbe.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
            SessionState.SetBool(Flag + ".Formal", true);
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (SessionState.GetBool(Flag, false)) EditorApplication.delayCall += Verify;
        }
        private static void Verify()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.delayCall += Verify; return; }
            SessionState.SetBool(Flag, false);
            try
            {
                if (SessionState.GetBool(Flag + ".Formal", false))
                {
                    SessionState.SetBool(Flag + ".Formal", false);
                    VerifyFormal(); Finish(null); return;
                }
                var service = ColorRuntimeService.Instance;
                service.ResetProgress();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab");
                var parent = new GameObject("Translated author root").transform;
                parent.position = new Vector3(-51f, 27f, 0f);
                var instance = Object.Instantiate(prefab, new Vector3(-91f, -73f, 0f), Quaternion.identity, parent);
                var platform = instance.GetComponent<MovingPlatformFeature>();
                var member = instance.AddComponent<RoomResetObject>();
                Vector3 author = instance.transform.position;
                Check(!platform.IsAttached, "locked platform has never attached");
                for (int i = 0; i < 3; i++)
                {
                    member.RestorePose(); member.RestoreState();
                    Check(Vector3.Distance(instance.transform.position, author) < .0001f,
                        "cold room reset preserves authored pose " + i + "; actual=" + instance.transform.position);
                }
                service.Unlock("red");
                Check(platform.IsAttached, "unlock attaches previously reset platform");
                Check(Vector3.Distance(platform.PreviewOrigin, author) < .0001f, "unlock retains route origin");
                Physics.SyncTransforms();
                var collider = instance.GetComponent<Collider>();
                Check(collider != null && collider.enabled && !collider.isTrigger &&
                    collider.bounds.Contains(author), "solid support remains at author position");
                instance.transform.position += Vector3.right * 2;
                member.RestorePose(); member.RestoreState();
                Check(Vector3.Distance(instance.transform.position, author) < .0001f, "warm reset restores author pose");
                instance.SetActive(false); instance.SetActive(true);
                Check(Vector3.Distance(platform.PreviewOrigin, author) < .0001f, "disable/enable retains origin");
                service.ResetProgress();
                member.RestorePose(); member.RestoreState();
                service.Unlock("red");
                Check(Vector3.Distance(platform.PreviewOrigin, author) < .0001f, "new session/re-unlock retains origin");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        private static void VerifyFormal()
        {
            var service = ColorRuntimeService.Instance;
            service.ResetProgress();
            var platforms = Object.FindObjectsByType<MovingPlatformFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(platforms.Length > 10, "formal map contains authored platforms");
            var expected = new Dictionary<MovingPlatformFeature, Vector3>();
            foreach (var platform in platforms)
            {
                var member = platform.GetComponent<RoomResetObject>();
                Check(member != null, "formal platform has reset member " + platform.name);
                expected[platform] = member.AuthoredWorldPosition;
                for (int i = 0; i < 2; i++)
                {
                    member.RestorePose(); member.RestoreState();
                    Check(Vector3.Distance(platform.transform.position, expected[platform]) < .001f,
                        "formal cold reset " + platform.name);
                }
            }
            service.Unlock("red");
            Physics.SyncTransforms();
            var details = new StringBuilder();
            foreach (var platform in platforms)
            {
                Check(Vector3.Distance(platform.PreviewOrigin, expected[platform]) < .001f,
                    "formal unlocked route origin " + platform.name);
                Check(platform.GetComponent<Collider>()?.enabled == true, "formal collision retained " + platform.name);
                details.AppendLine(platform.name + " origin=" + platform.PreviewOrigin);
            }
            foreach (var cube in Object.FindObjectsByType<Project.ContactCubes.ContactCube>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                details.AppendLine("CUBE " + cube.name + " scale=" + cube.transform.lossyScale.ToString("F5"));
                foreach (var shape in cube.GetComponentsInChildren<BoxCollider>(true))
                    details.AppendLine("  collider=" + shape.size.ToString("F5") + " scale=" + shape.transform.lossyScale.ToString("F5"));
                foreach (var visual in cube.GetComponentsInChildren<SpriteRenderer>(true))
                    details.AppendLine("  visual=" + visual.size.ToString("F5") + " scale=" + visual.transform.lossyScale.ToString("F5"));
            }
            File.WriteAllText("Library/PlatformFormalDetails.txt", details.ToString());
        }
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            checks++;
        }
        private static void Finish(Exception error)
        {
            string result = error == null ? "PASS " + checks + " platform lifecycle assertions" : error.ToString();
            File.WriteAllText("Library/PlatformColdResetResult.txt", result);
            Debug.Log(result);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
