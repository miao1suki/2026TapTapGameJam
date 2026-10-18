using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs authored lava/player prefabs through real PhysX trigger enter/stay callbacks.
// Reflection runs the normal cube motion step and clears respawn invulnerability;
// hazard/damage handlers are reached only by actual PhysX trigger callbacks.
public static class LavaOcclusionAudit
{
    private const string Flag = "LavaOcclusionAudit.Running";
    private const string LavaPath = "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Lava.prefab";
    private const string PlayerPath = "Assets/_Project/Content/Player/Player.prefab";
    private static readonly MethodInfo CubeMotionStep = typeof(ContactCubeMotion)
        .GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
    private static double started;
    private static int checks;
    private static Fixture pendingReset;
    private static int resetFrame;

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("Only run in the isolated probe project.");
        if (!Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, "-noaudio", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Lava death audit must run with -noaudio.");
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
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .15f) return;
        EditorApplication.update -= Tick;
        SimulationMode previous = Physics.simulationMode;
        try
        {
            _ = Project.Audio.AudioManager.Instance;
            AudioListener.volume = 0;
            AudioListener.pause = true;
            Physics.simulationMode = SimulationMode.Script;
            checks = 0;
            ColorRuntimeService.Instance.ResetProgress();
            ColorRuntimeService.Instance.Unlock("red");
            VerifyDroppedRedCube();
            VerifyLandingOnJustReleasedCube();
            VerifyAirborneCubeCollisionPolicy();
            VerifySettledTopContact();
            VerifyCoveredTopAndStay();
            VerifyExposedEdges();
            VerifyDirectSideContact();
            VerifyInvalidShields();
            VerifyCombinedSolids();
            VerifySparseLava();
            PrepareProtectionCoolingAndReset();
            // RoomResetObject destroys the cooled replacement at end of frame. Until then
            // it remains a real solid and must still shield the restored lava correctly.
            resetFrame = Time.frameCount;
            EditorApplication.update += VerifyResetAfterFrame;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
        finally { Physics.simulationMode = previous; }
    }

    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException("LAVA OCCLUSION: " + message);
        checks++;
        Debug.Log("LAVA OCCLUSION PASS: " + message);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root = new GameObject("Lava occlusion physics fixture");
        public readonly Vector3 Origin = new Vector3(150, 20, 0);
        public readonly GameObject Lava;
        public readonly PlayerController Player;
        public LavaHazardFeature Feature => Lava.GetComponent<LavaHazardFeature>();

        public Fixture(Vector3? lavaScale = null, bool sparse = false)
        {
            var staging = new GameObject("Inactive lava staging");
            staging.SetActive(false);
            Lava = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LavaPath), Origin,
                Quaternion.identity, staging.transform);
            Lava.transform.localScale = lavaScale ?? Vector3.one;
            if (sparse)
                Lava.AddComponent<LevelEditorMergedShape>().Configure(new Vector2Int(3, 1),
                    new[] { new RectInt(0, 0, 1, 1), new RectInt(2, 0, 1, 1) });
            Lava.transform.SetParent(Root.transform, true);
            Object.DestroyImmediate(staging);
            Player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath),
                Origin + Vector3.left * 4f, Quaternion.identity, Root.transform).GetComponent<PlayerController>();
            Player.GetComponent<PlayerInputDriver>().enabled = false;
            Player.Motor.useGravity = false;
            Player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            SetInvulnerability(Player, 0);
            Step();
            Check(!Player.IsDead, "outside lava is safe");
        }

        public BoxCollider Solid(Vector3 offset, Vector3 size)
        {
            var value = new GameObject("Real solid collider");
            value.transform.SetParent(Root.transform, false);
            value.transform.position = Origin + offset;
            var box = value.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        public void MovePlayer(Vector3 offset)
        {
            Player.Motor.position = Origin + offset;
            Player.transform.position = Origin + offset;
            Step();
        }

        public void Step(int frames = 3)
        {
            Player.Motor.WakeUp();
            Physics.SyncTransforms();
            for (int i = 0; i < frames; i++) Physics.Simulate(.02f);
        }

        public void StepMotion(ContactCubeMotion motion, int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                CubeMotionStep.Invoke(motion, null);
                Step(1);
            }
        }

        public void CheckEntered(string message)
        {
            var values = new List<BlockDebugValue>();
            Feature.CollectDebugValues(values);
            Check(values.Exists(value => value.Label == "接触玩家" && Convert.ToInt32(value.Value) > 0), message);
        }

        public void Dispose() => Object.DestroyImmediate(Root);
    }

    private static void VerifyDroppedRedCube()
    {
        using var f = new Fixture();
        f.Solid(Vector3.down, new Vector3(3, 1, 2));
        var red = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab"),
            f.Origin + Vector3.up * 2.5f, Quaternion.identity, f.Root.transform);
        Rigidbody body = red.GetComponent<Rigidbody>();
        body.WakeUp();
        f.Step(100);
        Check(Mathf.Abs(body.position.y - f.Origin.y) < .05f && Mathf.Abs(body.linearVelocity.y) < .05f,
            "authored red cube physically falls into the one-cell lava pit and settles on the floor");
        red.GetComponent<ContactCubeMotion>().enabled = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        body.useGravity = false;
        Collider solid = red.GetComponentInChildren<BoxCollider>();
        Check(solid.enabled && !solid.isTrigger, "red cube uses its actual solid collider");
        float actualTop = solid.bounds.max.y - f.Origin.y;
        f.MovePlayer(new Vector3(0, actualTop + .492f, 0));
        f.CheckEntered("protected feet still receive a real lava Trigger callback");
        Check(!f.Player.IsDead,
            $"standing on the actual dropped red cube top is safe (top={actualTop:F5})");
    }

    private static void VerifySettledTopContact()
    {
        foreach (float depth in new[] { .005f, .015f, .025f })
        {
            using var f = new Fixture();
            // Contact cubes have a stable .02-cell horizontal physical clearance per side.
            BoxCollider solid = f.Solid(Vector3.down * depth, new Vector3(.96f, 1, 1));
            f.MovePlayer(new Vector3(0, solid.bounds.max.y - f.Origin.y + .492f, 0));
            f.CheckEntered($"settled top depth {depth:F3} receives real lava Trigger contact");
            Check(!f.Player.IsDead,
                $"a genuinely supporting .96-wide block tolerates {depth:F3}-cell numerical settling");
            f.Step(20);
            Check(!f.Player.IsDead, "settled top remains safe through repeated physics Trigger Stay");
            solid.transform.position += Vector3.left * 4;
            f.Step();
            Check(f.Player.IsDead, "moving a settled support away immediately restores exposed lava danger");
        }

        using (var f = new Fixture())
        {
            BoxCollider solid = f.Solid(new Vector3(0, -.02f, 0), new Vector3(.96f, 1, 1));
            f.MovePlayer(new Vector3(0, solid.bounds.max.y - f.Origin.y + .51f, 0));
            f.CheckEntered("a settled support's real contact-offset shell still overlaps the lava trigger");
            Check(!f.Player.IsDead, "a capsule resting above the support in its small contact shell remains safe");
        }
        using (var f = new Fixture())
        {
            BoxCollider solid = f.Solid(new Vector3(0, -.02f, 0), new Vector3(.96f, 1, 1));
            f.Player.Motor.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            f.Player.Motor.useGravity = true;
            f.MovePlayer(new Vector3(0, 2, 0));
            f.Step(100);
            float footGap = f.Player.GetComponent<Collider>().bounds.min.y - solid.bounds.max.y;
            Check(!f.Player.IsDead && Mathf.Abs(footGap) < .035f,
                $"real falling player settles safely on a slightly sunken filled lava pit (foot gap={footGap:F5})");
        }
        using (var f = new Fixture())
        {
            f.Solid(new Vector3(0, -.04f, 0), new Vector3(.96f, 1, 1));
            f.MovePlayer(new Vector3(0, .952f, 0));
            Check(f.Player.IsDead, "an actually submerged support outside numerical settling skin remains dangerous");
        }
        using (var f = new Fixture())
        {
            f.Solid(new Vector3(0, -.02f, 0), new Vector3(.96f, 1, 1));
            f.MovePlayer(new Vector3(0, .9f, 0));
            Check(f.Player.IsDead, "deep player penetration is not mistaken for a harmless top contact");
        }
        using (var f = new Fixture(new Vector3(3, 1, 1)))
        {
            f.Solid(new Vector3(0, -.02f, 0), new Vector3(.96f, 1, 1));
            f.MovePlayer(new Vector3(.42f, .78f, 0));
            Check(f.Player.IsDead, "a sunken narrow block still leaves exposed lava at its rounded player edge");
        }
    }

    private static void VerifyLandingOnJustReleasedCube()
    {
        foreach (string color in new[] { "Red", "Blue" })
        {
            ColorRuntimeService.Instance.Unlock(color.ToLowerInvariant());
            using var f = new Fixture();
            f.Solid(Vector3.down, new Vector3(3, 1, 2));
            f.Solid(new Vector3(-1.5f, .5f, 0), new Vector3(2, 2, 2));
            f.Solid(new Vector3(1.5f, .5f, 0), new Vector3(2, 2, 2));
            GameObject cube = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Project/Content/ContactCubes/{color}_ContactCube.prefab"),
                f.Origin + Vector3.up * .25f, Quaternion.identity, f.Root.transform);
            var motion = cube.GetComponent<ContactCubeMotion>();
            Rigidbody body = cube.GetComponent<Rigidbody>();
            Collider block = cube.GetComponentInChildren<BoxCollider>();
            Collider actor = f.Player.GetComponent<Collider>();
            motion.BeginGreenHookPull();
            f.MovePlayer(new Vector3(0, 1.6f, 0));
            motion.EndGreenHookPull(false);
            f.Player.Motor.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            f.Player.Motor.useGravity = true;
            f.Player.Motor.linearVelocity = Vector3.down * 2;
            Physics.SyncTransforms();
            CubeMotionStep.Invoke(motion, null);
            Check(!motion.IsSettled && !Physics.GetIgnoreCollision(block, actor),
                $"just-released airborne {color} keeps a real top collision before its first settled frame");
            f.StepMotion(motion, 120);
            float footGap = actor.bounds.min.y - block.bounds.max.y;
            Check(!f.Player.IsDead && Mathf.Abs(footGap) < .04f && motion.IsSettled &&
                  Mathf.Abs(body.linearVelocity.y) < .05f,
                $"player lands physically on just-released {color} in a filled one-cell lava pit " +
                $"(foot gap={footGap:F5}, block y={body.position.y - f.Origin.y:F5})");
            Check(!Physics.GetIgnoreCollision(block, actor), $"settled {color} keeps its player's support collision");
        }
    }

    private static void VerifyAirborneCubeCollisionPolicy()
    {
        using var f = new Fixture();
        GameObject cube = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab"),
            f.Origin + Vector3.up * 3, Quaternion.identity, f.Root.transform);
        var motion = cube.GetComponent<ContactCubeMotion>();
        Collider block = cube.GetComponentInChildren<BoxCollider>();
        Collider actor = f.Player.GetComponent<Collider>();
        f.MovePlayer(new Vector3(.8f, 3, 0));
        CubeMotionStep.Invoke(motion, null);
        Check(!motion.IsSettled && Physics.GetIgnoreCollision(block, actor),
            "an airborne red still passes the player's side under the existing design");
        f.MovePlayer(new Vector3(0, block.bounds.max.y - f.Origin.y + .5f, 0));
        CubeMotionStep.Invoke(motion, null);
        Check(!Physics.GetIgnoreCollision(block, actor),
            "moving above an unsettled red restores its top collision before simulation");
        f.MovePlayer(new Vector3(.8f, block.bounds.center.y - f.Origin.y, 0));
        CubeMotionStep.Invoke(motion, null);
        Check(Physics.GetIgnoreCollision(block, actor), "moving back beside an airborne red preserves its side-pass rule");
        motion.enabled = false;
        Check(!Physics.GetIgnoreCollision(block, actor), "motion disable restores only the collision ignore it owns");
        Physics.IgnoreCollision(block, actor, true);
        motion.enabled = true;
        f.MovePlayer(new Vector3(0, block.bounds.max.y - f.Origin.y + .5f, 0));
        CubeMotionStep.Invoke(motion, null);
        Check(Physics.GetIgnoreCollision(block, actor), "top support does not override an externally owned collision ignore");
        motion.enabled = false;
        Check(Physics.GetIgnoreCollision(block, actor), "disabling motion preserves the external collision ignore");
        Physics.IgnoreCollision(block, actor, false);
    }

    private static void VerifyCoveredTopAndStay()
    {
        using var f = new Fixture();
        BoxCollider solid = f.Solid(Vector3.zero, Vector3.one);
        f.MovePlayer(new Vector3(0, .992f, 0));
        f.CheckEntered("solid-covered top enters lava through actual physics");
        Check(!f.Player.IsDead, "shallow numerical overlap on solid-covered lava top is safe");
        f.Step(10);
        Check(!f.Player.IsDead, "repeated Trigger Stay remains safe while coverage persists");
        solid.transform.position += Vector3.left * 4;
        f.Step();
        Check(f.Player.IsDead && f.Player.GetComponent<PlayerHealth>().IsDead,
            "moving the shield away kills through Trigger Stay without requiring re-entry");
    }

    private static void VerifyExposedEdges()
    {
        using (var f = new Fixture(new Vector3(3, 1, 1)))
        {
            f.Solid(Vector3.zero, Vector3.one);
            f.MovePlayer(new Vector3(.42f, .8f, 0));
            Check(f.Player.IsDead,
                "exposed edge kills even when the player's centre is above the covering block");
        }
        using (var f = new Fixture())
        {
            f.Solid(new Vector3(0, -.25f, 0), new Vector3(1, .5f, 1));
            f.MovePlayer(new Vector3(0, .74f, 0));
            Check(f.Player.IsDead, "a submerged support does not grant immunity to lava above its top");
        }
        using (var f = new Fixture(new Vector3(3, 1, 1)))
        {
            f.Solid(Vector3.zero, Vector3.one);
            f.MovePlayer(new Vector3(-1, 0, 0));
            Check(f.Player.IsDead, "a covered cell does not disable neighbouring cells of merged lava");
        }
    }

    private static void VerifyDirectSideContact()
    {
        using (var f = new Fixture(new Vector3(3, 1, 1)))
        {
            f.Solid(Vector3.zero, Vector3.one);
            f.MovePlayer(new Vector3(.95f, 0, 0));
            Check(f.Player.IsDead, "direct side contact with exposed lava is lethal");
        }
        using (var f = new Fixture())
        {
            f.Solid(Vector3.zero, Vector3.one);
            f.MovePlayer(new Vector3(.992f, 0, 0));
            f.CheckEntered("covered side receives an actual lava contact callback");
            Check(!f.Player.IsDead, "actual solid-covered side is safe, not only upward support");
        }
    }

    private static void VerifyInvalidShields()
    {
        using (var f = new Fixture())
        {
            f.Solid(Vector3.zero, Vector3.one).isTrigger = true;
            f.MovePlayer(Vector3.zero);
            Check(f.Player.IsDead, "water and other Trigger volumes cannot shield lava");
        }
        using (var f = new Fixture())
        {
            f.Solid(Vector3.zero, Vector3.one).enabled = false;
            f.MovePlayer(Vector3.zero);
            Check(f.Player.IsDead, "disabled solids do not shield lava");
        }
        using (var f = new Fixture())
        {
            BoxCollider solid = f.Solid(Vector3.zero, Vector3.one);
            Collider actor = f.Player.GetComponent<Collider>();
            Physics.IgnoreCollision(actor, solid);
            f.MovePlayer(Vector3.zero);
            Check(f.Player.IsDead, "ignored solid-player collision pairs cannot provide shielding");
        }
        using (var f = new Fixture())
        {
            const int shieldLayer = 10;
            BoxCollider solid = f.Solid(Vector3.zero, Vector3.one);
            solid.gameObject.layer = shieldLayer;
            int playerLayer = f.Player.GetComponent<Collider>().gameObject.layer;
            bool wasIgnored = Physics.GetIgnoreLayerCollision(playerLayer, shieldLayer);
            try
            {
                Physics.IgnoreLayerCollision(playerLayer, shieldLayer, true);
                f.MovePlayer(Vector3.zero);
                Check(f.Player.IsDead, "ignored collision layers cannot provide shielding");
            }
            finally { Physics.IgnoreLayerCollision(playerLayer, shieldLayer, wasIgnored); }
        }
        using (var f = new Fixture())
        {
            var decoration = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decoration.transform.SetParent(f.Root.transform, true);
            decoration.transform.position = f.Origin;
            decoration.transform.localScale = Vector3.one * 3;
            Object.DestroyImmediate(decoration.GetComponent<Collider>());
            f.MovePlayer(Vector3.zero);
            Check(f.Player.IsDead, "large decorative Renderer bounds do not provide shielding");
        }
        using (var f = new Fixture(new Vector3(3, 1, 1)))
        {
            BoxCollider solid = f.Solid(Vector3.zero, new Vector3(.25f, 1, 1));
            var renderer = solid.gameObject.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            f.MovePlayer(new Vector3(.42f, .8f, 0));
            Check(f.Player.IsDead, "exact narrow collider is used instead of a presumed occupied grid cell");
        }
    }

    private static void VerifyCombinedSolids()
    {
        using (var f = new Fixture(new Vector3(2, 1, 1)))
        {
            f.Solid(new Vector3(-.5f, 0, 0), Vector3.one);
            f.Solid(new Vector3(.5f, 0, 0), Vector3.one).transform.rotation = Quaternion.Euler(0, 0, 90);
            f.MovePlayer(new Vector3(0, .992f, 0));
            f.CheckEntered("seam above two colliders receives real Trigger contact");
            Check(!f.Player.IsDead, "union of adjacent solids covers the contact, including a ninety-degree box");
        }
        using (var f = new Fixture(new Vector3(3, 1, 1)))
        {
            BoxCollider left = f.Solid(new Vector3(-1, 0, 0), Vector3.one);
            BoxCollider right = left.gameObject.AddComponent<BoxCollider>();
            right.center = Vector3.right * 2;
            f.MovePlayer(Vector3.zero);
            Check(f.Player.IsDead, "holes between compound solid colliders remain dangerous");
        }
        using (var f = new Fixture(new Vector3(2, 1, 1)))
        {
            f.Solid(new Vector3(-.505f, 0, 0), new Vector3(.99f, 1, 1));
            f.Solid(new Vector3(.505f, 0, 0), new Vector3(.99f, 1, 1));
            f.MovePlayer(new Vector3(0, .992f, 0));
            Check(f.Player.IsDead, "a thin exposed gap between two shielding solids is still lethal");
        }
    }

    private static void VerifySparseLava()
    {
        using (var f = new Fixture(new Vector3(3, 1, 1), true))
        {
            f.MovePlayer(Vector3.zero);
            Check(!f.Player.IsDead, "sparse merged lava hole has no invented broad Trigger volume");
            f.Solid(Vector3.left, Vector3.one);
            f.MovePlayer(new Vector3(-1, .992f, 0));
            f.CheckEntered("covered sparse lava cell receives real Trigger contact");
            Check(!f.Player.IsDead, "one occupied sparse lava cell can be shielded independently");
            f.MovePlayer(new Vector3(1, .992f, 0));
            Check(f.Player.IsDead, "other uncovered sparse lava cell remains lethal");
        }
    }

    private static void PrepareProtectionCoolingAndReset()
    {
        var f = new Fixture();
        pendingReset = f;
        SetInvulnerability(f.Player, Time.time + 100);
        f.MovePlayer(Vector3.zero);
        Check(!f.Player.IsDead, "respawn invulnerability remains respected");
        var manager = f.Root.AddComponent<RoomLifecycleManager>();
        var member = f.Lava.AddComponent<RoomResetObject>();
        member.Bind(manager, "lava-audit");
        ColorRuntimeService.Instance.Unlock("blue");
        Check(f.Feature.ApplyColor("blue", f.Player.gameObject) && !f.Lava.activeSelf,
            "blue cooling still disables the authored lava");
        SetInvulnerability(f.Player, 0);
        f.Step();
        Check(!f.Player.IsDead, "cooled lava does not damage the player");
        member.RestorePose();
        member.RestoreState();
        Check(f.Lava.activeSelf && ColorRuntimeService.Instance.IsUnlocked("red"),
            "room reset restores authored lava without relocking red");
    }

    private static void VerifyResetAfterFrame()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Reset frame timeout"); return; }
        if (!EditorApplication.isPlaying || Time.frameCount <= resetFrame + 1) return;
        EditorApplication.update -= VerifyResetAfterFrame;
        SimulationMode previous = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            pendingReset.Step();
            Check(pendingReset.Player.IsDead && pendingReset.Player.GetComponent<PlayerHealth>().IsDead,
                "after deferred replacement removal, real Trigger Stay restores unlocked lava danger");
            Finish(true, "PASS " + checks);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
        finally { Physics.simulationMode = previous; }
    }

    private static void SetInvulnerability(PlayerController player, float until) => typeof(PlayerController)
        .GetField("invulnerableUntil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, until);

    private static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Flag, false);
        EditorApplication.update -= Tick;
        EditorApplication.update -= VerifyResetAfterFrame;
        pendingReset?.Dispose();
        pendingReset = null;
        File.WriteAllText("Library/LavaOcclusionResult.txt", message);
        Debug.Log(message);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
        else EditorApplication.Exit(passed ? 0 : 1);
    }
}
