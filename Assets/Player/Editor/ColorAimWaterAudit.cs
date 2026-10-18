using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    // No authoring document or source scene is opened or saved by this audit.
    public static class ColorAimWaterAudit
    {
        private const string SessionKey = "ColorAimWaterAudit";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static int checks;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                !Environment.GetCommandLineArgs().Contains("-noaudio"))
                throw new InvalidOperationException("Run only in silent FlowRetirementProbe.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.update -= Wait;
            EditorApplication.update += Wait;
        }

        private static void Wait()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            bool passed = false;
            string report;
            try
            {
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                VerifyAim();
                Clear();
                VerifySteam();
                passed = true;
                report = "PASS " + checks;
            }
            catch (Exception error) { Debug.LogException(error); report = "FAIL " + checks + " " + error; }
            finally { Clear(); }
            File.WriteAllText("Library/ColorAimWaterAudit.txt", report);
            Debug.Log("COLOR AIM WATER " + report);
            SessionState.EraseBool(SessionKey);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks++;
            Debug.Log("COLOR AIM WATER PASS " + label);
        }

        private static GameObject Track(GameObject instance) { objects.Add(instance); return instance; }
        private static GameObject Cube(Vector3 position)
        {
            var cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position;
            return cube;
        }
        private static void Clear()
        {
            foreach (GameObject instance in objects) if (instance != null) Object.DestroyImmediate(instance);
            objects.Clear();
            Physics.SyncTransforms();
        }

        private static void VerifyAim()
        {
            var player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Content/Player/Player.prefab"), Vector3.zero, Quaternion.identity));
            player.GetComponent<PlayerInputDriver>().enabled = false;
            var sensor = player.GetComponent<PlayerInteractionSensor>();
            var camera = Track(new GameObject("Aim audit camera")).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0f, -20f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            var cube = Cube(new Vector3(2f, 0f, 0f));
            var target = cube.AddComponent<UniversalColorBlock>();
            Type queryType = typeof(PlayerColorWheel).Assembly.GetType("Project.Player.ColorAbilityAim", true);
            object query = Activator.CreateInstance(queryType, true);
            MethodInfo find = queryType.GetMethod("Find", Private);
            (string, object) Query(Vector3 cursor, string color = "red")
            {
                Physics.SyncTransforms();
                object[] args = { sensor, camera, (Vector2)camera.WorldToScreenPoint(cursor), color, .65f, null, Vector3.zero };
                string result = find.Invoke(query, args).ToString();
                return (result, args[5]);
            }
            var snapped = Query(new Vector3(2f, .9f, 0f));
            Check(snapped.Item1 == "Success" && ReferenceEquals(snapped.Item2, target), "red empty-space tolerance selects nearby real target");
            Check(Query(new Vector3(2f, .9f, 0f), "blue").Item1 == "Success", "blue receives the same tolerance");
            var customRoot = Cube(new Vector3(-2f, 0f, 0f));
            var custom = customRoot.AddComponent<ColorAimAuditCustomTarget>();
            Check(ReferenceEquals(Query(new Vector3(-2f, 0f, 0f)).Item2, custom),
                "custom direct-hit IColorApplicationTarget keeps its interface contract");
            Check(ReferenceEquals(Query(new Vector3(-2f, .9f, 0f)).Item2, custom),
                "custom non-feature target can opt into tolerance using CanApplyColor");
            Object.DestroyImmediate(customRoot);
            var waterPrefab = LevelEditorPaletteService.GetOrCreate().PropEntries
                .First(e => e?.Prefab?.GetComponent<WaterVisualFeature>() != null).Prefab;
            var underlay = Track(Object.Instantiate(waterPrefab, new Vector3(2f, 0f, -.3f), Quaternion.identity));
            Check(ReferenceEquals(Query(new Vector3(2f, 0f, 0f)).Item2, target),
                "same-cell foreground item wins over a water underlay closer to camera");
            Object.DestroyImmediate(underlay);
            var wall = Cube(new Vector3(2f, .9f, -.8f));
            Check(Query(new Vector3(2f, .9f, 0f)).Item1 == "Rejected", "precise wall hit is not bypassed by tolerance");
            Object.DestroyImmediate(wall);
            cube.transform.position = new Vector3(sensor.ScanRadius - .4f, 0f, 0f);
            Check(Query(new Vector3(sensor.ScanRadius + .05f, 0f, 0f)).Item1 == "OutOfRange", "pointer cannot extend total radius even toward an in-range collider");
            cube.transform.position = new Vector3(sensor.ScanRadius + .6f, 0f, 0f);
            Check(Query(new Vector3(sensor.ScanRadius - .05f, 0f, 0f)).Item1 == "Rejected", "tolerance does not select a point outside the original radius");
            cube.transform.position = new Vector3(2f, 0f, 0f);
            var wheel = player.GetComponent<PlayerColorWheel>();
            Check(wheel.TrySelectColor("red"), "red ability unlocked for indicator verification");
            wheel.Tick(camera.WorldToScreenPoint(new Vector3(sensor.ScanRadius - .2f, 0f, 0f)));
            var indicator = player.GetComponent<ColorAbilityAimIndicator>();
            var render = typeof(ColorAbilityAimIndicator).GetMethod("LateUpdate", Private);
            render.Invoke(indicator, null);
            var inside = player.transform.Find("__ColorAimInside")?.GetComponent<LineRenderer>();
            Check(inside != null && inside.enabled, "stable red tolerance indicator is generated after camera");
            var ringPoints = new Vector3[inside.positionCount];
            inside.GetPositions(ringPoints);
            Check(ringPoints.All(p => ((Vector2)p).sqrMagnitude <= sensor.ScanRadius * sensor.ScanRadius + .001f),
                "all colored edge points stay inside original circle");
            Color alpha = inside.startColor;
            render.Invoke(indicator, null);
            Check(inside.startColor == alpha, "stationary aim keeps constant color and alpha");
            var manager = Track(new GameObject("Aim audit rooms")).AddComponent<RoomLifecycleManager>();
            var region = new RoomRegion { id = "a", label = "A" };
            for (int x = -2; x <= 6; x++) for (int y = -2; y <= 3; y++) region.cells.Add(new Vector2Int(x, y));
            manager.Configure(new List<RoomRegion> { region }, 1f, string.Empty);
            cube.AddComponent<RoomResetObject>().Bind(manager, "other");
            Check(Query(new Vector3(2f, 0f, 0f)).Item1 == "Rejected", "authored room membership blocks a cross-room target in a large collider");
        }

        private static void VerifySteam()
        {
            var palette = LevelEditorPaletteService.GetOrCreate();
            var prefab = palette.PropEntries.First(e => e?.Prefab?.GetComponent<WaterVisualFeature>() != null).Prefab;
            var waterRoot = Track(Object.Instantiate(prefab, new Vector3(10f, 10.5f, 0f), Quaternion.identity));
            waterRoot.transform.localScale = new Vector3(4f, 3f, 1f);
            var water = waterRoot.GetComponent<WaterVisualFeature>();
            var shape = waterRoot.AddComponent<LevelEditorMergedShape>();
            shape.Configure(new Vector2Int(4, 3), new[] { new RectInt(0, 0, 1, 3), new RectInt(1, 2, 3, 1) });
            Physics.SyncTransforms();
            Bounds left = shape.Volumes[0].bounds, bottom = shape.Volumes[1].bounds;
            Vector3 click = new Vector3(bottom.max.x - .4f, bottom.center.y, bottom.center.z);
            Check(WaterSteamPlacement.TryFind(water, click, 1f, out Vector3 origin, out Vector2 half), "irregular merged water has an exposed local cell");
            Check(origin.x > bottom.max.x - 1f && Mathf.Abs(origin.y - bottom.max.y) < .001f,
                "steam uses clicked low arm, not merged global top or centre");
            Check(half.x <= .5f && half.y > 0f, "steam pushes only its local cell width");
            var upperEntry = palette.PropEntries.First(e => e?.Prefab?.GetComponent<WaterVisualFeature>() != null && e.Prefab != prefab);
            var upperRoot = Track(Object.Instantiate(upperEntry.Prefab,
                new Vector3(origin.x, origin.y + .5f, origin.z), Quaternion.identity));
            Physics.SyncTransforms();
            Check(WaterSteamPlacement.TryFind(water, click, 1f, out Vector3 connectedTop, out _) &&
                Mathf.Abs(connectedTop.x - origin.x) < .001f && connectedTop.y > origin.y + .9f,
                "another water family above raises steam to the connected real surface, not the internal seam");
            Object.DestroyImmediate(upperRoot);
            Physics.SyncTransforms();
            Check(((IColorApplicationHitTarget)water).ApplyColorAt("red", null, click) &&
                Vector3.Distance(water.SteamOrigin, origin) < .001f, "explicit click context reaches real water reaction");
            var actor = Track(new GameObject("Contact actor"));
            actor.transform.position = new Vector3(left.center.x, left.center.y, left.center.z);
            Check(water.ApplyColor("red", actor) && Mathf.Abs(water.SteamOrigin.y - left.max.y) < .001f,
                "ordinary ApplyColor contact calls retain compatibility and actor locality");
            water.ResetForRoom();
            Check((float)typeof(WaterVisualFeature).GetField("steamRemainingSeconds", Private).GetValue(water) == 0f,
                "room reset clears ongoing steam");
            int coveredX = Mathf.FloorToInt(origin.x), coveredY = Mathf.FloorToInt(origin.y + .002f);
            var roof = Cube(new Vector3(coveredX + .5f, coveredY + .5f, 0f));
            roof.AddComponent<LevelEditorPlacedBlock>().Configure(new Vector2Int(coveredX, coveredY), "Steam roof", Color.white, false);
            Physics.SyncTransforms();
            Check(WaterSteamPlacement.TryFind(water, click, 1f, out Vector3 fallback, out _) &&
                Mathf.Abs(fallback.x - origin.x) > .5f, "covered clicked column falls back to a nearby exposed water surface");
            foreach (BoxCollider volume in shape.Volumes)
            {
                if (volume == null || !volume.enabled) continue;
                Bounds bounds = volume.bounds;
                for (int x = Mathf.FloorToInt(bounds.min.x + .002f); x < Mathf.CeilToInt(bounds.max.x - .002f); x++)
                {
                    int y = Mathf.FloorToInt(bounds.max.y + .002f);
                    var block = Cube(new Vector3(x + .5f, y + .5f, 0f));
                    block.AddComponent<LevelEditorPlacedBlock>().Configure(new Vector2Int(x, y), "Closed roof", Color.white, false);
                }
            }
            Physics.SyncTransforms();
            Check(!WaterSteamPlacement.TryFind(water, click, 1f, out _, out _) &&
                !((IColorApplicationHitTarget)water).ApplyColorAt("red", actor, click), "fully covered water never spawns steam inside walls");
        }
    }

    public sealed class ColorAimAuditCustomTarget : MonoBehaviour, IColorApplicationTarget
    {
        public bool CanApplyColor(string colorId, GameObject actor) => isActiveAndEnabled;
        public bool ApplyColor(string colorId, GameObject actor) => CanApplyColor(colorId, actor);
    }
}
