using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.BossLogic;
using Project.CameraModes;
using Project.InputAbstraction;
using Project.Items;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Project.FastTravel.Editor
{
    public static class TravelMapAudit
    {
        private const string Flag = "TravelMapAudit.Running";
        private static int checks, phase;
        private static BossController boss;
        private static double started, phaseAt;
        private static WorldTravelMap map;
        private static PlayerController player;
        private static RespawnAnchorFeature destination, locked, bossPoint;
        private static RoomLifecycleManager rooms;
        private static Vector3 start;
        private static readonly List<string> report = new List<string>();
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets")) throw new InvalidOperationException("Only run isolated.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = 0; started = EditorApplication.timeSinceStartup; checks = 0; report.Clear();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); report.Add(message); }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 100) throw new TimeoutException();
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .4f) return;
                AudioListener.volume = 0f; AudioListener.pause = true;
                if (phase == 0) { Setup(); phase = 1; phaseAt = EditorApplication.timeSinceStartup; return; }
                if (phase == 1 && EditorApplication.timeSinceStartup - phaseAt > .5)
                {
                    Check(map.IsUnlocked && map.CanUseHere, "30% plus defeated Boss unlocks map outside its region");
                    Check(map.TryOpen() && map.IsOpen && player.IsControlLocked && GameplayPresentationGate.IsSuppressed, "map opens through player/input lease");
                    var graphic = Object.FindFirstObjectByType<TravelMapGraphic>();
                    Check(graphic != null && graphic.CachedSpanCount == 18, "three 8x6 rooms are cached as 18 row runs, no object art");
                    Vector2 pivot = new Vector2(70f, -30f), before = graphic.Center + pivot / graphic.Scale;
                    graphic.Zoom(1.4f, pivot);
                    Check(Vector2.Distance(before, graphic.Center + pivot / graphic.Scale) < .001f, "zoom preserves pointer world pivot");
                    Check(!map.TryTravel(locked), "locked checkpoint rejected");
                    Check(!map.TryTravel(bossPoint), "Boss checkpoint rejected");
                    Check(player.Motor.isKinematic && player.IsInvulnerable, "map safety is owned by player controller");
                    graphic.Zoom(.5f, Vector2.zero); // leave headroom below the intentional maximum zoom
                    var first = new PointerEventData(EventSystem.current) { pointerId = 7, position = new Vector2(500, 400) };
                    var second = new PointerEventData(EventSystem.current) { pointerId = 8, position = new Vector2(600, 400) };
                    graphic.OnPointerDown(first); graphic.OnPointerDown(second);
                    float pinchScale = graphic.Scale;
                    second.delta = new Vector2(60, 0); second.position += second.delta; graphic.OnDrag(second);
                    Check(graphic.Scale > pinchScale, "touch pinch changes zoom without separate device polling");
                    graphic.OnPointerUp(first); graphic.OnEndDrag(second);
                    map.Close(); Check(!player.IsControlLocked && !GameplayPresentationGate.IsSuppressed, "close returns owned control and presentation leases");
                    Check(!player.Motor.isKinematic && !player.IsDead, "close restores body mode without killing player");
                    var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstruction.transform.position = new Vector3(10.5f, 1.5f, 0); obstruction.transform.localScale = new Vector3(2, 3, 2);
                    Physics.SyncTransforms(); string originalRegion = rooms.CurrentRegionId;
                    Check(!player.TryTravelToCheckpoint(destination) && rooms.CurrentRegionId == originalRegion,
                        "blocked destination rejects without changing source room ownership");
                    Object.DestroyImmediate(obstruction);
                    VerifyCrossRoomFallback();
                    Check(map.TryOpen(), "reopen reuses UI safely");
                    player.enabled = false;
                    phase = 7; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (phase == 7 && EditorApplication.timeSinceStartup - phaseAt > .12)
                {
                    Check(!map.IsOpen && !map.IsTravelling && !GameplayPresentationGate.IsSuppressed &&
                        !player.IsControlLocked && !player.Motor.isKinematic,
                        "disabling only PlayerController closes map and returns body/UI ownership");
                    Check(!map.TryOpen(), "disabled player cannot reopen map through T or touch entry");
                    player.enabled = true;
                    VerifyLeaseEpoch();
                    Check(map.TryOpen(), "map reopens after player component is enabled again");
                    var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                    gameView.position = new Rect(0, 0, 1280, 720); gameView.Show(); gameView.Focus();
                    phase = 2; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (phase == 2 && EditorApplication.timeSinceStartup - phaseAt > .5)
                {
                    var graphic = Object.FindFirstObjectByType<TravelMapGraphic>();
                    Canvas.ForceUpdateCanvases();
                    var mesh = graphic.canvasRenderer.GetMesh();
                    Check(mesh.vertexCount > 0 && !graphic.canvasRenderer.cull,
                        "room layer has visible rendered geometry (" + mesh.vertexCount + " vertices)");
                    report.Add("Map renderer: rect=" + graphic.rectTransform.rect + " bounds=" + mesh.bounds +
                        " material=" + graphic.materialForRendering + " texture=" + graphic.mainTexture +
                        " clip=" + graphic.canvasRenderer.hasRectClipping + " color=" + graphic.canvasRenderer.GetColor());
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/TravelMap.png"));
                    phase = 6; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (phase == 6 && EditorApplication.timeSinceStartup - phaseAt > .2)
                {
                    Check(map.TryTravel(destination), "activated non-Boss checkpoint begins animated travel");
                    start = player.transform.position; phase = 3; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (phase == 3)
                {
                    if (EditorApplication.timeSinceStartup - phaseAt < .18)
                    { Check(Vector3.Distance(start, player.transform.position) < .08f, "player waits during camera pullout"); return; }
                    if (map.IsTravelling || EditorApplication.timeSinceStartup - phaseAt < 1.8) return;
                    Check(!map.IsOpen && !player.IsControlLocked && !GameplayPresentationGate.IsSuppressed, "arrival releases camera/player/UI leases");
                    Check(Mathf.Abs(player.transform.position.x - destination.RespawnPosition.x) < .2f, "safe grounded checkpoint landing");
                    Check(rooms.CurrentRegionId == "b", "room lifecycle follows teleport without stale source ownership");
                    Check(PlayerRespawnPointRegistry.TryFindPreferred(player, player.transform.position, out var point) && ReferenceEquals(point, destination), "destination is current respawn binding");
                    Check(map.TryOpen(), "map works after travel");
                    boss.ApplyBossRespawnState(boss.MaxHealth, boss.transform.position);
                    phase = 4; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (phase == 4 && EditorApplication.timeSinceStartup - phaseAt > .12)
                {
                    Check(!map.IsOpen && !map.IsTravelling && !player.IsControlLocked && !GameplayPresentationGate.IsSuppressed,
                        "new Boss fight immediately cancels open map and returns all leases");
                    boss.Kill(); Check(map.TryOpen() && map.TryTravel(destination), "travel can start again after fight ends");
                    Time.timeScale = 0; phase = 5; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (phase == 5 && EditorApplication.timeSinceStartup - phaseAt > .12)
                {
                    Check(!map.IsTravelling && !player.IsControlLocked && !GameplayPresentationGate.IsSuppressed,
                        "pause during travel immediately releases owned leases");
                    Time.timeScale = 1;
                    Check(map.TryOpen(), "map reopens after interrupted travel"); map.enabled = false;
                    Check(!player.IsControlLocked && !GameplayPresentationGate.IsSuppressed, "component disable clears UI and leases");
                    Finish(true, "");
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        private static void VerifyLeaseEpoch()
        {
            // These handles deliberately survive a component lifecycle. The
            // previous generation must never release the new generation's lock.
            IDisposable stale = player.AcquireTravelSafetyLock(typeof(TravelMapAudit));
            Check(player.Motor.isKinematic && player.IsInvulnerable, "first safety generation owns body and protection");
            player.enabled = false; player.enabled = true;
            IDisposable current = player.AcquireTravelSafetyLock(typeof(TravelMapAudit));
            stale.Dispose();
            Check(player.Motor.isKinematic && player.IsInvulnerable && player.IsControlLocked && player.ControlLockDepth == 1,
                "stale safety disposal after disable does not remove new body/protection/control lease");
            current.Dispose();
            Check(!player.Motor.isKinematic && !player.IsControlLocked && !player.IsInvulnerable,
                "current safety generation releases exactly its own state");

            IDisposable staleControl = player.AcquireControlLock(typeof(TravelMapAudit));
            player.enabled = false; player.enabled = true;
            IDisposable currentControl = player.AcquireControlLock(typeof(TravelMapAudit));
            staleControl.Dispose();
            Check(player.IsControlLocked && player.ControlLockDepth == 1,
                "stale ordinary control handle cannot decrement current generation");
            currentControl.Dispose();
            Check(!player.IsControlLocked, "current ordinary control handle still releases normally");
        }
        private static void VerifyCrossRoomFallback()
        {
            RespawnAnchorFeature boundary = Point("boundary checkpoint", new Vector3(7.95f, 0, 0), true);
            var data = new SerializedObject(boundary);
            data.FindProperty("respawnRadiusBlocks").floatValue = .75f; data.ApplyModifiedPropertiesWithoutUndo();
            var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.name = "blocks every in-room candidate";
            obstruction.transform.position = new Vector3(7.15f, 1.5f, 0);
            obstruction.transform.localScale = new Vector3(1.4f, 3, 2);
            Physics.SyncTransforms();

            // Prove there really is a physically valid neighbor fallback. A
            // rejection caused solely by bad fixture geometry would miss the bug.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var grounded = typeof(PlayerController).GetMethod("TryGetGroundedRespawnPosition", flags);
            var unblocked = typeof(PlayerController).GetMethod("IsTravelPositionUnblocked", flags);
            // The standard capsule is almost one cell wide. At +.375 its
            // left skin still touches the wall ending at X=7.85; +.75 is
            // the next actual search candidate and clears it completely.
            object[] arguments = { boundary.RespawnPosition + Vector3.right * .75f, null };
            bool hasGround = (bool)grounded.Invoke(player, arguments);
            Vector3 landing = (Vector3)arguments[1];
            Check(hasGround && (bool)unblocked.Invoke(player, new object[] { landing }) && rooms.FindRegion(landing) == "b",
                "boundary fixture offers a safe physical fallback in neighboring room");
            string origin = rooms.CurrentRegionId; Vector3 position = player.transform.position;
            Check(!player.TryTravelToCheckpoint(boundary) && rooms.CurrentRegionId == origin &&
                Vector3.Distance(position, player.transform.position) < .001f,
                "checkpoint search rejects neighboring-room fallback without relocating or changing source owner");
            Object.DestroyImmediate(obstruction); Object.DestroyImmediate(boundary.gameObject); Physics.SyncTransforms();
        }
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            var regions = new List<RoomRegion>();
            for (int room = 0; room < 3; room++)
            {
                var r = new RoomRegion { id = ((char)('a' + room)).ToString(), label = "测试房间 " + (room + 1) };
                for (int x = room * 8; x < room * 8 + 8; x++) for (int y = -6; y < 0; y++) r.cells.Add(new Vector2Int(x, y));
                regions.Add(r);
            }
            rooms = new GameObject("Map rooms").AddComponent<RoomLifecycleManager>(); rooms.Configure(regions, 1f, "");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(12, -.5f, 0); floor.transform.localScale = new Vector3(24, 1, 4);
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"), new Vector3(2.5f, .5f, 0), Quaternion.identity).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            new SerializedObject(rooms).FindProperty("player").objectReferenceValue = player;
            var roomData = new SerializedObject(rooms); roomData.FindProperty("player").objectReferenceValue = player; roomData.ApplyModifiedPropertiesWithoutUndo();
            var cameraObject = new GameObject("Travel camera", typeof(Camera), typeof(CameraControlManager), typeof(CameraModeController)); cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<CameraControlManager>().SetFocusTarget(player.transform, true);
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var settings = Resources.Load<TravelMapSettings>("UI/TravelMapSettings");
            Check(settings != null && settings.Font != null && Mathf.Approximately(settings.RequiredRatio, .3f), "shipped settings use project font and 30% gate");
            map = player.GetComponent<WorldTravelMap>(); Check(map != null, "standard player auto-installs map without another prefab");
            Check(!map.TryOpen(), "T map cannot open before unlock");
            for (int i = 0; i < 10; i++)
            {
                var obj = new GameObject("Map collectible " + i, typeof(Rigidbody), typeof(CollectiblePickup)); obj.transform.position = new Vector3(3 + i * .05f, 3, 0);
                if (i < 3) obj.GetComponent<CollectiblePickup>().RestoreCollectedState();
            }
            CollectibleProgressService.RefreshSceneCatalog(scene);
            destination = Point("destination", new Vector3(10.5f, 0, 0), true);
            locked = Point("locked", new Vector3(12.5f, 0, 0), false);
            bossPoint = Point("boss", new Vector3(18.5f, 0, 0), true); bossPoint.SetBossArenaRespawnPoint(true);
            BossTravelProgress.SetBossRegion(scene, "c");
            var bossObject = new GameObject("Boss flag fixture", typeof(BoxCollider)); bossObject.transform.position = new Vector3(18.5f, 2, 0);
            boss = bossObject.AddComponent<BossController>(); boss.ActivateHealthSystem();
            Check(!map.TryOpen(), "active fight prohibits map"); boss.Kill();
            Physics.SyncTransforms(); rooms.UpdatePlayerRegion(player);
        }
        private static RespawnAnchorFeature Point(string name, Vector3 position, bool active)
        {
            var obj = new GameObject(name); obj.transform.position = position; var point = obj.AddComponent<RespawnAnchorFeature>();
            if (active) point.Activate(); return point;
        }
        private static void Finish(bool passed, string details)
        {
            Time.timeScale = 1;
            EditorApplication.update -= Tick; SessionState.EraseBool(Flag);
            string result = (passed ? "PASS " : "FAIL ") + checks + "\n" + string.Join("\n", report) + "\n" + details;
            File.WriteAllText("Library/TravelMapAudit.txt", result); Debug.Log(result); EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
