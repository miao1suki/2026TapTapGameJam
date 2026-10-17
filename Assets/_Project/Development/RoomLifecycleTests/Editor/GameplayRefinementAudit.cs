using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.GreenHook;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using Project.ObjectArtwork;
using Project.Subtitles;
using TaSource.LinkEffect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated gameplay regressions; never opens or saves an author's scene.</summary>
public static class GameplayRefinementAudit
{
    private const string Flag = "GameplayRefinementAudit.Running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static double started;
    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Hidden);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException("GAMEPLAY REFINEMENT: " + message);
        checks++; Debug.Log("GAMEPLAY REFINEMENT PASS: " + message);
    }
    private static GameObject Spawn(string relative, Vector3 position) => Object.Instantiate(
        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + relative), position, Quaternion.identity);
    private static GameObject Solid(string name, Vector3 position)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name; item.transform.position = position; return item;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("此验证仅可在隔离工程执行。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .15f) return;
        EditorApplication.update -= Tick;
        var mode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
            checks = 0;
            VerifyPlantParameters(); VerifyVineAndLinks(); VerifyCarryBase(); VerifyDoorDelays(); VerifySignboards(); VerifyWaterWallLinks(); VerifyBaseGravity();
            Finish(true, "PASS " + checks + " gameplay refinement assertions");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
        finally { Physics.simulationMode = mode; }
    }
    private static PlayerController Player(Vector3 position)
    {
        var player = Spawn("Player/Player.prefab", position).GetComponent<PlayerController>();
        player.GetComponent<PlayerInputDriver>().enabled = false;
        player.Motor.constraints = RigidbodyConstraints.FreezeAll;
        return player;
    }
    private static void VerifyPlantParameters()
    {
        var plant = Spawn("ColorBlocks/Prefabs/Green_JumpPlant.prefab", new Vector3(0, 50, 0)).GetComponent<BouncePlantFeature>();
        Check(plant.NormalRiseCells == 8 && plant.HighRiseCells == 8 && plant.CarryRiseCells == 4, "existing plant defaults remain 8 / 8 / 4");
        var property = new SerializedObject(plant); property.FindProperty("normalRiseCells").floatValue = 5.25f; property.ApplyModifiedPropertiesWithoutUndo();
        Check(plant.GetRiseCells(1, false) == 5.25f, "instance height changes actual bounce response");
        Check(plant.GetRiseCells(5, true) == 8, "instance ordinary height does not overwrite high bounce");
        Check(plant.CarryRiseCells == 4, "instance ordinary height does not overwrite carry bounce");
        Check(Field(typeof(BouncePlantFeature), "normalRiseCells").GetCustomAttribute<BlockParameterAttribute>() != null,
            "instance height uses existing editor parameter contract");
        Check(LevelEditorComponentOverrideUtility.TryCapture(plant, property.FindProperty("normalRiseCells"), out var value),
            "editor captures plant height as an instance override");
        value = JsonUtility.FromJson<LevelEditorComponentValueOverride>(JsonUtility.ToJson(value));
        var restored = Spawn("ColorBlocks/Prefabs/Green_JumpPlant.prefab", new Vector3(4, 50, 0)).GetComponent<BouncePlantFeature>();
        LevelEditorComponentOverrideUtility.ApplyOverrides(restored.gameObject, new[] { value }, false);
        Check(restored.GetRiseCells(1, false) == 5.25f && restored.CarryRiseCells == 4,
            "planning override JSON round trip restores custom height only");
        Object.DestroyImmediate(restored.gameObject);
        Object.DestroyImmediate(plant.gameObject);
    }
    private static void VerifyVineAndLinks()
    {
        Vector3 origin = new Vector3(0, 20, 0);
        var root = Spawn("ColorBlocks/Prefabs/Green_Ladder.prefab", origin).GetComponent<ClimbableVineFeature>();
        Set(root, "growInstantly", true); Set(root, "maxGrowthBlocks", 4);
        Set(root, "growthSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Green_LadderSon.prefab"));
        var edgeWall = Solid("Touching edge wall", origin + new Vector3(1, 1, 0)); Physics.SyncTransforms();
        Check((bool)Call(root, "CanSpawnSegment", 0), "adjacent wall touching next cell edge is not a false obstruction");
        Object.DestroyImmediate(edgeWall);
        var ownShape = Solid("Own root collider", origin + Vector3.up); ownShape.transform.SetParent(root.transform, true); Physics.SyncTransforms();
        Check((bool)Call(root, "CanSpawnSegment", 0), "same-root collision geometry never blocks its own growth");
        Object.DestroyImmediate(ownShape);
        var wall = Solid("Growth wall", origin + Vector3.up * 2); Physics.SyncTransforms();
        root.GrowTo(4);
        Check(root.CurrentGrowthBlocks == 1 && root.IsGrowthBlocked, "wall stops at last free growth cell");
        Object.DestroyImmediate(wall);
        var obstacle = Spawn("ColorBlocks/Prefabs/Green_PlantObstacle.prefab", origin + Vector3.up * 2); Physics.SyncTransforms(); root.GrowTo(4);
        Check(root.CurrentGrowthBlocks == 1 && root.IsGrowthBlocked, "plant obstacle blocks growth");
        var obstacleFeature = obstacle.GetComponent<PlantObstacleFeature>();
        var obstacleArt = MechanismArtworkPresentation.Ensure(obstacle);
        Check(obstacleFeature.BreakObstacle() && !obstacleFeature.ShowsArtwork, "burning obstacle clears artwork availability");
        obstacleArt.Refresh(0); Check(!obstacleArt.Visual.enabled, "artwork late refresh cannot resurrect burned obstacle sprite");
        obstacleFeature.ResetForRoom(); obstacleArt.Refresh(0);
        Check(obstacleFeature.IsIntact && obstacleArt.Visual.enabled, "room reset restores obstacle solidity and sprite");
        Object.DestroyImmediate(obstacle);
        var platform = Spawn("ColorBlocks/Prefabs/Red_Platform.prefab", origin + Vector3.up * 2); Physics.SyncTransforms(); root.GrowTo(4);
        Check(root.CurrentGrowthBlocks == 1 && root.IsGrowthBlocked, "moving platform blocks growth");
        Object.DestroyImmediate(platform);
        var water = Spawn("ColorBlocks/Prefabs/Blue_WaterSource.prefab", origin + Vector3.up * 2);
        var decoration = new GameObject("Non-solid decoration"); decoration.transform.position = origin + Vector3.up * 3;
        decoration.AddComponent<SpriteRenderer>();
        Physics.SyncTransforms(); root.GrowTo(4);
        Check(root.CurrentGrowthBlocks == 4 && !root.IsGrowthBlocked, "water triggers and decoration do not stop growth");
        var segments = (List<GameObject>)Get(root, "spawnedSegments");
        Check(segments.All(s => s.GetComponent<ClimbableVineFeature>().Root == root), "all spawned segments preserve logical root");
        Object.DestroyImmediate(water); Object.DestroyImmediate(decoration);
        var player = Player(origin + new Vector3(2, 2.9f, 0));
        var effect = player.GetComponent<InteractionLinkEffect>();
        Check(player.GetComponent<PlayerColorWheel>().TrySelectColor("red"), "red selected for read-only vine preview");
        Physics.SyncTransforms(); Call(effect, "LateUpdate");
        Check(effect.DetectedTargetCount == 1, "five-cell vine produces only one logical link");
        var candidates = (IList)Get(effect, "candidates");
        var hit = (Collider)candidates[0].GetType().GetField("Collider").GetValue(candidates[0]);
        Check(Mathf.Abs(hit.transform.position.y - (origin.y + 3)) < .01f, "link uses nearest real segment, not mother pivot");
        int firstKey = (int)candidates[0].GetType().GetField("Key").GetValue(candidates[0]);
        player.transform.position = origin + Vector3.right * 2; player.Motor.position = player.transform.position; Physics.SyncTransforms(); Call(effect, "ScanTargets");
        candidates = (IList)Get(effect, "candidates");
        hit = (Collider)candidates[0].GetType().GetField("Collider").GetValue(candidates[0]);
        Check(hit.gameObject == root.gameObject, "nearest endpoint follows player to mother cell");
        Check((int)candidates[0].GetType().GetField("Key").GetValue(candidates[0]) == firstKey, "changing nearest segment does not create another reveal key");
        var second = Spawn("ColorBlocks/Prefabs/Green_Ladder.prefab", origin + Vector3.right * 4);
        Physics.SyncTransforms(); Call(effect, "ScanTargets");
        Check(effect.DetectedTargetCount == 2, "independent vine roots remain independent links");
        Object.DestroyImmediate(second); Object.DestroyImmediate(player.gameObject);
        foreach (GameObject segment in segments.ToArray()) Object.DestroyImmediate(segment);
        segments.Clear(); Object.DestroyImmediate(root.gameObject);
    }
    private static void VerifyCarryBase()
    {
        Vector3 origin = new Vector3(30.5f, 20.5f, 0);
        var player = Player(origin + Vector3.left);
        var baseObject = Spawn("ColorBlocks/Prefabs/Red_Foundation.prefab", origin);
        var mechanism = baseObject.GetComponent<MechanismBaseFeature>();
        var carry = baseObject.GetComponent<CarryableFeature>();
        var body = baseObject.GetComponent<Rigidbody>();
        Check(carry != null && carry.CanCarryNow, "base prefab uses existing carry target");
        Check(mechanism is ICarryMotionParticipant && baseObject.GetComponent<GreenHookBodyMotion>() != null,
            "carry and hook share the existing base physics owner");
        var energy = Spawn("ContactCubes/Red_ContactCube.prefab", origin + Vector3.right);
        energy.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        Physics.SyncTransforms(); Call(mechanism, "ScanForEnergy");
        Check(mechanism.IsSignalActive, "arbitrary red cube still powers base");
        var baseArt = MechanismArtworkPresentation.Ensure(baseObject); baseArt.Refresh(0);
        Check(baseArt.Visual.sprite == ObjectArtworkLibrary.Load().Find(MechanismArtworkKind.MechanismBase).frames[1],
            "powered base uses active artwork frame");
        bool kinematic = body.isKinematic, gravity = body.useGravity;
        Check(carry.BeginCarry(player), "existing carry command picks up base");
        Check(!mechanism.IsSignalActive && !mechanism.CanBeGreenHooked(player.gameObject), "carried base suspends power and hook qualification");
        baseArt.Refresh(0);
        Check(baseArt.Visual.sprite == ObjectArtworkLibrary.Load().Find(MechanismArtworkKind.MechanismBase).frames[0],
            "suspended base returns to inactive artwork frame");
        Check(body.isKinematic && !body.useGravity && !baseObject.GetComponent<Collider>().enabled,
            "carry alone owns pose while base colliders are disabled");
        Call(mechanism, "ScanForEnergy");
        Check(!mechanism.IsSignalActive, "even an explicit energy scan cannot power a carried base");
        carry.CancelCarry(); Physics.SyncTransforms(); Call(mechanism, "ScanForEnergy");
        Check((baseObject.transform.position - origin).sqrMagnitude < .00001f && body.isKinematic == kinematic && body.useGravity == gravity,
            "cancel restores authored pose and original body settings");
        Check(mechanism.IsSignalActive && mechanism.CanBeGreenHooked(player.gameObject), "put-down/cancel restores energy and hook contracts");
        var region = new RoomRegion { id = "base_a", label = "Base A" };
        var other = new RoomRegion { id = "base_b", label = "Base B" };
        for (int x = 25; x <= 35; x++) for (int y = -25; y <= -15; y++) region.cells.Add(new Vector2Int(x, y));
        other.cells.Add(new Vector2Int(40, -21));
        var manager = new GameObject("Base room manager").AddComponent<RoomLifecycleManager>();
        manager.Configure(new List<RoomRegion> { region, other }, 1, "");
        var member = baseObject.AddComponent<RoomResetObject>(); manager.Register(member);
        Check(carry.BeginCarry(player), "base may be carried again after restoration");
        Check(!carry.CanPlaceCarryAt(player, new Vector3(40.5f, 20.5f, 0)), "carry rejects crossing authored room boundary");
        manager.ResetRegion("base_a"); Physics.SyncTransforms();
        Check(!carry.IsCarryActive && (baseObject.transform.position - origin).sqrMagnitude < .00001f,
            "region/R reset restores carried base to authored position");
        Set(manager, "player", player); Set(manager, "currentRegionId", "base_a");
        Check(carry.BeginCarry(player), "base carry may resume before leaving room");
        player.transform.position = new Vector3(40.5f, 20.5f, 0); player.Motor.position = player.transform.position;
        manager.UpdatePlayerRegion(player);
        Check(!carry.IsCarryActive && (baseObject.transform.position - origin).sqrMagnitude < .00001f,
            "actual player region transition cancels base carry and restores authored position");
        player.transform.position = origin + Vector3.left; player.Motor.position = player.transform.position; Set(manager, "currentRegionId", "base_a");
        Check(carry.BeginCarry(player), "base carry may resume before respawn");
        RoomLifecycleManager.BeforeRespawn(player, origin + Vector3.left);
        Check(!carry.IsCarryActive && (baseObject.transform.position - origin).sqrMagnitude < .00001f,
            "shared R/death respawn entry restores carried base to authored position");
        Check(ColorRuntimeService.Instance.IsUnlocked("red"), "base reset preserves scene color unlock");
        ColorRuntimeService.Instance.ResetProgress();
        Check(!carry.CanCarryNow, "locked base cannot be picked up");
        ColorRuntimeService.Instance.Unlock("red");
        Check(carry.CanCarryNow, "scene color unlock enables base pickup without player ability");
        Object.DestroyImmediate(energy); Object.DestroyImmediate(baseObject); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(manager.gameObject);
        ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true); ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
    }
    private static void VerifyDoorDelays()
    {
        var player = Player(new Vector3(70, 20, 0));
        var door = Spawn("Mechanisms/Door.prefab", new Vector3(75, 20, 0)).GetComponent<DoorController>();
        var held = Spawn("Mechanisms/DoorButton.prefab", new Vector3(73, 20, 0)).GetComponent<DoorButton>();
        var latched = Spawn("Mechanisms/DoorButton.prefab", new Vector3(74, 20, 0)).GetComponent<DoorButton>();
        Set(held, "mode", DoorButtonMode.Held); Set(door, "unlockDelay", .8f); Set(door, "closeDelay", .6f);
        door.SetButtons(new[] { held, latched });
        held.TryPress(player.gameObject); latched.TryPress(player.gameObject);
        Check(door.IsWaitingToOpen && door.State == DoorState.Locked, "both buttons start existing open delay");
        Call(door, "AdvanceTimers", .79f); Check(door.State == DoorState.Locked, "door keeps pattern visible before opening");
        Call(door, "AdvanceTimers", .02f); Check(door.State == DoorState.Unlocked, "door opens after open delay");
        held.ResetButton();
        Check(door.IsWaitingToClose && door.State == DoorState.Unlocked && !door.GetComponent<Collider>().enabled,
            "releasing held button starts delay but keeps doorway passable");
        Call(door, "AdvanceTimers", .3f); Check(door.State == DoorState.Unlocked, "door remains open during release delay");
        held.TryPress(player.gameObject);
        Check(!door.IsWaitingToClose && door.State == DoorState.Unlocked, "repress cancels pending close without replaying opening");
        Call(door, "AdvanceTimers", 1f); Check(door.State == DoorState.Unlocked, "cancelled close cannot fire later");
        held.ResetButton(); Call(door, "AdvanceTimers", 0f);
        Check(door.IsWaitingToClose && door.State == DoorState.Unlocked, "paused timer does not consume delay");
        Call(door, "AdvanceTimers", .61f);
        Check(door.State == DoorState.Locked && !door.IsWaitingToClose && door.GetComponent<Collider>().enabled,
            "release deadline closes and restores collision");
        Check(latched.IsPressed, "closing preserves other latched button progress");
        held.TryPress(player.gameObject); Call(door, "AdvanceTimers", 1f); held.ResetButton();
        door.ResetDoor(); Call(door, "AdvanceTimers", 10f);
        Check(!door.IsWaitingToOpen && !door.IsWaitingToClose && door.State == DoorState.Locked,
            "reset cancels both timers without callbacks reopening door");
        held.TryPress(player.gameObject); latched.TryPress(player.gameObject); Call(door, "AdvanceTimers", 1f); held.ResetButton();
        door.enabled = false;
        Check(!door.IsWaitingToClose && !door.IsWaitingToOpen, "disable clears pending timers");
        door.enabled = true;
        Check(door.IsWaitingToClose, "re-enable reevaluates released pressure");
        door.SetButtons(Array.Empty<DoorButton>()); Call(door, "AdvanceTimers", 1f);
        Check(door.State == DoorState.Locked && !door.IsWaitingToClose, "unbind cannot leave an orphan open door");
        Check(Field(typeof(DoorController), "closeDelay").GetCustomAttribute<BlockParameterAttribute>() != null,
            "close delay available to editor instance parameter mode");
        Object.DestroyImmediate(held.gameObject); Object.DestroyImmediate(latched.gameObject); Object.DestroyImmediate(door.gameObject); Object.DestroyImmediate(player.gameObject);
    }
    private static void VerifySignboards()
    {
        var artwork = ObjectArtworkLibrary.Load();
        Check(artwork != null && artwork.signboards != null && artwork.signboards.Length == 7,
            "seven authored signboard choices are available");
        var sign = Spawn("ColorBlocks/Prefabs/ScreenNoteTool.prefab", new Vector3(95, 20, 0)).GetComponent<SubtitleTrigger>();
        sign.RefreshSignVisual();
        var collider = sign.GetComponent<BoxCollider>();
        Vector3 size = collider.size, center = collider.center;
        var visual = sign.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "Subtitle Sign Visual");
        Check(sign.ShowSign && visual.enabled && visual.sprite == artwork.signboards[0],
            "legacy subtitle trigger receives visible default signboard at runtime");
        Check(!sign.GetComponent<MeshRenderer>().enabled && visual.enabled,
            "signboard remains visible independently of hidden legacy placeholder");
        sign.transform.localScale = new Vector3(4, 2, 1); sign.RefreshSignVisual();
        Check(Mathf.Abs(visual.bounds.size.x - visual.sprite.rect.width / 16f) < .001f &&
            Mathf.Abs(visual.bounds.size.y - visual.sprite.rect.height / 16f) < .001f,
            "signboard retains sixteen-source-pixels per cell under nonuniform parent scale");
        var serialized = new SerializedObject(sign);
        var sprite = serialized.FindProperty("signSprite"); sprite.objectReferenceValue = artwork.signboards[4];
        serialized.ApplyModifiedPropertiesWithoutUndo(); sign.RefreshSignVisual();
        Check(visual.sprite == artwork.signboards[4], "sign image parameter changes only scene artwork");
        Check(LevelEditorComponentOverrideUtility.TryCapture(sign, sprite, out var value),
            "editor captures selected sign sprite");
        var values = LevelEditorComponentOverrideUtility.PersistentValues(new[] { value });
        value = JsonUtility.FromJson<LevelEditorComponentValueOverride>(JsonUtility.ToJson(values[0]));
        Check(value.Value.Contains("GlobalObjectId"), "sign sprite save uses persistent asset identity, not session instance ID");
        var restored = Spawn("ColorBlocks/Prefabs/ScreenNoteTool.prefab", new Vector3(100, 20, 0)).GetComponent<SubtitleTrigger>();
        LevelEditorComponentOverrideUtility.ApplyOverrides(restored.gameObject, new[] { value }, false); restored.RefreshSignVisual();
        Check(restored.SignSprite == artwork.signboards[4], "sign sprite restores through planning JSON round trip");
        serialized.Update(); serialized.FindProperty("showSign").boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); sign.RefreshSignVisual();
        Check(!visual.enabled && collider.enabled && collider.isTrigger && collider.size == size && collider.center == center,
            "hide/change artwork never changes subtitle trigger geometry");
        var player = Player(new Vector3(95, 20, 0));
        var service = new SubtitleCapture(); SubtitleServiceRegistry.Register(service);
        Call(sign, "OnTriggerEnter", player.GetComponent<Collider>());
        Call(sign, "OnTriggerEnter", player.GetComponent<Collider>());
        Check(service.Count == 1 && service.Cue != null && service.Cue.text == "ScreenNoteTool",
            "sign appearance preserves text cue and play-once subtitle semantics");
        SubtitleServiceRegistry.Unregister(service);
        Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(sign.gameObject); Object.DestroyImmediate(restored.gameObject);
    }
    private sealed class SubtitleCapture : ISubtitleService
    {
        public int Count; public SubtitleCue Cue;
        public void Show(SubtitleCue cue) { Count++; Cue = cue; }
        public void ShowLarge(string text, SubtitleStyle style = null) { }
        public void ShowBottom(string text, SubtitleStyle style = null) { }
        public void Hide(SubtitleLayer layer, bool immediate = false) { }
        public void HideAll(bool immediate = false) { }
    }
    private static void VerifyWaterWallLinks()
    {
        Vector3 position = new Vector3(115.5f, 20.5f, 0);
        var water = Spawn("ColorBlocks/Prefabs/Blue_WaterSource.prefab", position);
        var wall = Solid("Water covering authored wall", position);
        wall.AddComponent<LevelEditorPlacedBlock>().Configure(new Vector2Int(115, 20), "Wall", Color.white, false);
        var player = Player(position + Vector3.left * 2);
        var effect = player.GetComponent<InteractionLinkEffect>();
        player.GetComponent<PlayerColorWheel>().TrySelectColor("red"); Physics.SyncTransforms(); Call(effect, "LateUpdate");
        Check(effect.DetectedTargetCount == 0, "water fully covered by authored wall has no ability preview link");
        Object.DestroyImmediate(wall); Physics.SyncTransforms(); Call(effect, "ScanTargets");
        Check(effect.DetectedTargetCount == 1, "removing covering wall restores visible water link");
        Object.DestroyImmediate(water); Object.DestroyImmediate(player.gameObject);
    }
    private static void VerifyBaseGravity()
    {
        Vector3 origin = new Vector3(130.5f, 20.5f, 0);
        var owner = Spawn("ColorBlocks/Prefabs/Red_Foundation.prefab", origin);
        var mechanism = owner.GetComponent<MechanismBaseFeature>(); var body = owner.GetComponent<Rigidbody>();
        Check(mechanism.UsesGravity && !body.isKinematic && body.useGravity,
            "base defaults to real dynamic gravity after color activation");
        Check(Field(typeof(MechanismBaseFeature), "useGravity").GetCustomAttribute<BlockParameterAttribute>() != null,
            "base gravity switch is available as Chinese instance parameter");
        var floor = Solid("Base gravity floor", origin + Vector3.down * 3);
        Physics.SyncTransforms();
        for (int i = 0; i < 60; i++) { Call(mechanism, "FixedUpdate"); Physics.Simulate(.02f); }
        Check(body.position.y < origin.y - 1.5f && body.position.y > origin.y - 2.1f,
            "enabled gravity falls onto a real solid support");
        Vector3 resting = body.position;
        body.AddForce(Vector3.right * 100, ForceMode.Impulse);
        for (int i = 0; i < 4; i++) { Call(mechanism, "FixedUpdate"); Physics.Simulate(.02f); }
        Check(Mathf.Abs(body.position.x - resting.x) < .001f && Mathf.Abs(body.position.y - resting.y) < .02f,
            "settled base remains solid but cannot be pushed horizontally");
        Set(mechanism, "useGravity", false); Call(mechanism, "FixedUpdate");
        Object.DestroyImmediate(floor); body.position = origin; owner.transform.position = origin; Physics.SyncTransforms();
        for (int i = 0; i < 10; i++) { Call(mechanism, "FixedUpdate"); Physics.Simulate(.02f); }
        Check(body.isKinematic && !body.useGravity && (body.position - origin).sqrMagnitude < .00001f,
            "disabled instance gravity preserves a floating authored position");
        var player = Player(origin + Vector3.left);
        var carry = owner.GetComponent<CarryableFeature>();
        Check(carry.BeginCarry(player), "gravity-disabled base remains carryable"); carry.CancelCarry();
        Check(body.isKinematic && !body.useGravity, "carry restore respects disabled gravity parameter");
        var hook = owner.GetComponent<GreenHookBodyMotion>(); hook.BeginGreenHookPull(); hook.SetGreenHookVelocity(Vector3.right * 2);
        Call(mechanism, "FixedUpdate");
        Check(hook.IsHookMoving && !body.isKinematic && !body.useGravity && body.linearVelocity.x > 1.9f,
            "base physics does not override active green-hook ownership");
        hook.EndGreenHookPull(false, Vector3.zero, 0); Call(mechanism, "FixedUpdate");
        Check(body.isKinematic && !body.useGravity, "hook release restores disabled gravity parameter");
        Set(mechanism, "useGravity", true); Call(mechanism, "FixedUpdate");
        Check(carry.BeginCarry(player), "gravity-enabled base remains carryable"); carry.CancelCarry();
        Check(!body.isKinematic && body.useGravity, "carry restore respects enabled gravity parameter");
        mechanism.ResetForRoom(); Check(body.useGravity && !body.isKinematic, "room state reset retains authored enabled gravity");
        Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(owner);
    }
    private static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
        File.WriteAllText("Library/GameplayRefinementResult.txt", message); Debug.Log(message);
        if (EditorApplication.isPlaying)
        { EditorApplication.ExitPlaymode(); EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1); }
        else EditorApplication.Exit(passed ? 0 : 1);
    }
}
