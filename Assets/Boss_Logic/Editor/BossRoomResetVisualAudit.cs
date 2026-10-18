using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.BossLogic.Editor
{
    /// <summary>Real prefab / Boss budgeted hide / R lifecycle regression, never saves authoring scenes.</summary>
    public static class BossRoomResetVisualAudit
    {
        private const string Flag = "BossRoomResetVisualAudit.Running";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static double started, next;
        private static int phase, checks;
        private static RoomLifecycleManager rooms;
        private static PlayerController player;
        private static BossController boss;
        private static DoorController door;
        private static DoorButton button;
        private static MovingPlatformFeature platform;
        private static RoomResetObject doorReset, platformReset;
        private static MechanismArtworkPresentation doorArt, platformArt;
        private static AsyncOperation unloading;
        private static SpriteRenderer unloadedVisual;
        private static Material platformMaterial;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Only run in the silent isolated FlowRetirementProbe project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start outside Play mode.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            started = EditorApplication.timeSinceStartup;
            phase = checks = 0;
            next = 0d;
            report.Clear();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 120d)
            { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f ||
                EditorApplication.timeSinceStartup < next) return;
            try
            {
                switch (phase++)
                {
                    case 0:
                        Setup();
                        Delay(.15);
                        break;
                    case 1:
                        VerifyLocked();
                        ColorRuntimeService.Instance.Unlock("red");
                        ColorAppearanceManager.Instance.Play("red", true, .8f);
                        Delay(.02);
                        break;
                    case 2:
                        // Requested seconds pass through the configured HSV
                        // multiplier. Observe the real phase, not wall-clock
                        // guesses that can accidentally test the base handoff.
                        if (ColorAppearanceManager.Instance.GetRealOpacity("red") <= .05f)
                        { phase--; Delay(.02); break; }
                        VerifyReveal();
                        Delay(.02);
                        break;
                    case 3:
                        if (!ColorAppearanceManager.Instance.IsComplete("red"))
                        { phase--; Delay(.02); break; }
                        VerifyComplete();
                        VerifyDoorProgress();
                        VerifyQueueRevision();
                        VerifyRepeatedResets();
                        VerifyLeaveAndDeath();
                        BeginUnload();
                        Delay(.15);
                        break;
                    case 4:
                        if (!unloading.isDone) { phase--; Delay(.1); break; }
                        Check(unloadedVisual == null, "unloaded scene removes hidden renderer with its owner");
                        Check(platformMaterial != null && platformArt.Visual.sharedMaterial == platformMaterial,
                            "additive scene unload does not destroy another platform's shared appearance material");
                        Visible(doorArt, "surviving opened door after scene unload");
                        Visible(platformArt, "surviving platform after scene unload");
                        Finish(true, "PASS " + checks);
                        break;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
        }

        private static void Setup()
        {
            AudioListener.pause = true;
            AudioListener.volume = 0f;
            Physics.simulationMode = SimulationMode.Script;
            ColorRuntimeService.Instance.ResetProgress();
            var playerObject = Prefab("Assets/_Project/Content/Player/Player.prefab", new Vector3(1.5f, 100.5f, 0f));
            player = playerObject.GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.ConfigureRespawn(PlayerRespawnMode.Standard);
            var regions = new List<RoomRegion> { Region("a", 0, 24), Region("b", 30, 36) };
            rooms = new GameObject("Audit Room Lifecycle").AddComponent<RoomLifecycleManager>();
            rooms.Configure(regions, 1f, string.Empty);
            Set(rooms, "player", player);
            door = Prefab("Assets/_Project/Content/Mechanisms/Door.prefab", new Vector3(8.5f, 100.5f, 0f)).GetComponent<DoorController>();
            button = Prefab("Assets/_Project/Content/Mechanisms/DoorButton.prefab", new Vector3(6.5f, 100.5f, 0f)).GetComponent<DoorButton>();
            door.SetButtons(new[] { button });
            Set(door, "unlockDelay", 0f);
            doorArt = MechanismArtworkPresentation.Ensure(door.gameObject);
            platform = Prefab("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(16.5f, 100.5f, 0f)).GetComponent<MovingPlatformFeature>();
            Set(platform, "driveMode", MovingPlatformDriveMode.Automatic);
            Set(platform, "startsActive", true);
            Set(platform, "reverseOnBlock", false);
            platformArt = MechanismArtworkPresentation.Ensure(platform.gameObject);
            doorReset = Member(door.gameObject);
            platformReset = Member(platform.gameObject);
            Member(button.gameObject);
            var bossObject = new GameObject("Audit Boss");
            bossObject.SetActive(false);
            bossObject.transform.position = new Vector3(1000f, 1000f, 0f);
            bossObject.AddComponent<BoxCollider>();
            bossObject.AddComponent<Rigidbody>().useGravity = false;
            boss = bossObject.AddComponent<BossController>();
            Set(boss, "enableAutonomousChase", false);
            bossObject.SetActive(true);
            Check(doorArt.Visual != null && platformArt.Visual != null, "real door/platform prefabs have artwork renderers");
        }

        private static void VerifyLocked()
        {
            Check(!platform.GetComponent<ColorObject>().IsActive && !platformArt.Visual.enabled,
                "locked platform keeps color activation and artwork gates");
            Renderer neutral = platform.GetComponent<ColorObject>().TargetRenderer;
            Check(neutral != null && neutral.enabled && neutral.sharedMaterial != null, "locked platform has valid neutral appearance");
            Hide(door.gameObject); Hide(platform.gameObject);
            Check(door.gameObject.activeSelf && platform.gameObject.activeSelf, "Boss hides render/collision only, not authored roots");
            Check(doorArt.Visual.forceRenderingOff && platformArt.Visual.forceRenderingOff, "actual Boss hide path takes render visibility leases");
            player.RequestRespawn();
            Visible(doorArt, "locked door after real R");
            Check(!platformArt.Visual.forceRenderingOff && !platformArt.Visual.enabled,
                "R releases platform hide without exposing locked color artwork");
            Check(!neutral.forceRenderingOff && neutral.sharedMaterial != null, "R restores locked neutral renderer without replacing material");
        }

        private static void VerifyReveal()
        {
            var appearance = ColorAppearanceManager.Instance;
            float opacity = appearance.GetRealOpacity("red");
            Check(appearance.ShowsUnlockedAppearance("red") && opacity > 0f && opacity < 1f, "fixture reaches real transparent reveal phase");
            platformArt.Refresh(0f);
            Material before = platformArt.Visual.sharedMaterial;
            Hide(platform.gameObject); player.RequestRespawn(); platformArt.Refresh(0f);
            Visible(platformArt, "platform reset during reveal");
            Check(platformArt.Visual.sharedMaterial == before && Mathf.Abs(opacity - appearance.GetRealOpacity("red")) < .001f,
                "room reset does not restart reveal or replace its shared material");
            Check(platform.IsAttached && platform.enabled && ColorRuntimeService.Instance.IsUnlocked("red"),
                "room reset preserves unlocked platform activation");
        }

        private static void VerifyComplete()
        {
            Check(ColorAppearanceManager.Instance.GetRealOpacity("red") > .999f, "color reveal completes after reset");
            platformArt.Refresh(0f);
            platformMaterial = platformArt.Visual.sharedMaterial;
            Vector3 before = platform.transform.position;
            Invoke(platform, "TickMotion", .25f);
            platformArt.Refresh(.25f);
            Check((platform.transform.position - before).sqrMagnitude > .000001f, "fixture platform actually moves while unlocked");
            Hide(platform.gameObject); player.RequestRespawn(); platformArt.Refresh(0f);
            Visible(platformArt, "moving platform after real R");
            Check((platform.transform.position - platformReset.AuthoredWorldPosition).sqrMagnitude < .000001f,
                "moving platform returns to author position");
            Check(platformArt.Visual.sharedMaterial == platformMaterial, "moving platform retains shared material after reset");
        }

        private static void VerifyDoorProgress()
        {
            Hide(door.gameObject); // Snapshot closed collider, then open while hidden.
            Check(button.TryPress(player.gameObject), "real latched button press accepted");
            Invoke(door, "AdvanceTimers", 1f);
            Check(door.State == DoorState.Unlocked && !door.GetComponent<Collider>().enabled, "hidden door still reaches unlocked state");
            player.RequestRespawn(); doorArt.Refresh(0f);
            Visible(doorArt, "opened persistent door after real R");
            Check(door.State == DoorState.Unlocked && button.IsPressed && !door.GetComponent<Collider>().enabled,
                "restoring hide lease keeps button progress and current open collider, not old closed snapshot");
            Check(doorReset.Persistent, "door remains persistent rather than bypassing progress contract");
        }

        private static void VerifyQueueRevision()
        {
            foreach (var owner in new[] { door.gameObject, platform.gameObject })
            {
                Invoke(boss, "QueueTouchedObject", owner);
                int revision = owner.GetComponent<RoomResetObject>().ResetRevision;
                player.RequestRespawn();
                Check(owner.GetComponent<RoomResetObject>().ResetRevision != revision, "real R invalidates queued old contact for " + owner.name);
                Invoke(boss, "LateUpdate");
                Check(owner.GetComponent<TemporaryObjectHideState>()?.IsHidden != true,
                    "pre-reset budgeted hide cannot hide freshly restored " + owner.name);
                Hide(owner);
                Check(owner.GetComponent<TemporaryObjectHideState>().IsHidden, "new post-reset contact can still hide " + owner.name);
                player.RequestRespawn();
            }
        }

        private static void VerifyRepeatedResets()
        {
            for (int i = 0; i < 8; i++)
            {
                Hide(door.gameObject); Hide(platform.gameObject);
                player.RequestRespawn();
                doorArt.Refresh(0f); platformArt.Refresh(0f);
                Visible(doorArt, "door R cycle " + i);
                Visible(platformArt, "platform R cycle " + i);
                Check(door.State == DoorState.Unlocked && button.IsPressed && platform.IsAttached,
                    "R cycle keeps progress/activation " + i);
            }
        }

        private static void VerifyLeaveAndDeath()
        {
            rooms.UpdatePlayerRegion(player);
            Hide(door.gameObject); Hide(platform.gameObject);
            player.transform.position = new Vector3(32.5f, 100.5f, 0f);
            player.GetComponent<Rigidbody>().position = player.transform.position;
            rooms.UpdatePlayerRegion(player);
            Check(rooms.CurrentRegionId == "b", "actual region transition enters next room");
            Visible(doorArt, "persistent door on leaving room");
            Visible(platformArt, "platform on leaving room");
            player.RequestRespawn();
            Hide(door.gameObject); Hide(platform.gameObject);
            player.EnterDeath(); player.RequestRespawn();
            Check(!player.IsDead, "real death and respawn completes");
            Visible(doorArt, "persistent door after death respawn");
            Visible(platformArt, "platform after death respawn");
            Check(door.State == DoorState.Unlocked && ColorRuntimeService.Instance.IsUnlocked("red"),
                "death/leave preserve door and scene-color progress");
        }

        private static void BeginUnload()
        {
            Scene scene = SceneManager.CreateScene("Audit Hidden Additive Scene");
            GameObject owner = Prefab("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Platform.prefab", new Vector3(60.5f, 100.5f, 0f));
            SceneManager.MoveGameObjectToScene(owner, scene);
            var art = MechanismArtworkPresentation.Ensure(owner);
            unloadedVisual = art.Visual;
            Hide(owner);
            unloading = SceneManager.UnloadSceneAsync(scene);
        }

        private static void Visible(MechanismArtworkPresentation art, string label)
        {
            art.Refresh(0f);
            Check(art != null && art.gameObject.activeInHierarchy && art.Visual != null && art.Visual.enabled &&
                !art.Visual.forceRenderingOff && art.Visual.sprite != null && art.Visual.sharedMaterial != null &&
                art.Visual.sharedMaterial.shader != null, label + " has active root, enabled unhidden sprite and valid material/shader");
        }
        private static RoomResetObject Member(GameObject owner)
        {
            var result = owner.GetComponent<RoomResetObject>() ?? owner.AddComponent<RoomResetObject>();
            rooms.Register(result);
            return result;
        }
        private static RoomRegion Region(string id, int start, int end)
        {
            var result = new RoomRegion { id = id, label = id };
            for (int x = start; x <= end; x++) for (int y = -105; y <= -95; y++) result.cells.Add(new Vector2Int(x, y));
            return result;
        }
        private static GameObject Prefab(string path, Vector3 position) =>
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
        private static void Hide(GameObject owner) { Invoke(boss, "QueueTouchedObject", owner); Invoke(boss, "LateUpdate"); }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
        private static object Invoke(object target, string method, params object[] values) => target.GetType().GetMethod(method, Hidden).Invoke(target, values);
        private static void Delay(double seconds) => next = EditorApplication.timeSinceStartup + seconds;
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("BOSS ROOM VISUAL FAIL: " + message);
            checks++; report.AppendLine("PASS " + message);
            Debug.Log("BOSS ROOM VISUAL PASS: " + message);
        }
        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(Flag);
            string directory = "Library/BossRoomResetVisualAudit";
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/Report.txt", message + "\n" + report);
            Debug.Log("BOSS ROOM VISUAL RESULT: " + message);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
