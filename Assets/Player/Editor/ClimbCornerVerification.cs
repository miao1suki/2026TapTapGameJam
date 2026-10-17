using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Real prefab/collider regression; only runs in the silent disposable probe project.</summary>
    public static class ClimbCornerVerification
    {
        private const string Key = "ClimbCornerVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks;
        private static void Call(object target, string method)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var found = type.GetMethod(method, Private);
                if (found != null) { found.Invoke(target, null); return; }
            }
            throw new MissingMethodException(target.GetType().Name, method);
        }
        private static void Check(bool pass, string label)
        { if (!pass) throw new Exception(label); checks++; Debug.Log("CORNER PASS " + label); }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe") || !Environment.GetCommandLineArgs().Contains("-noaudio"))
                throw new Exception("Isolated -noaudio verification only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            EditorApplication.update -= Tick;
            var oldSimulation = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                for (int kind = 0; kind < 4; kind++)
                    foreach (int side in new[] { -1, 1 }) VerifyUp(kind, side);
                VerifyCoveredTop();
                VerifyChain();
                VerifyHorizontalWrap();
                Finish(true, "PASS " + checks);
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
            finally { Physics.simulationMode = oldSimulation; }
        }
        private static GameObject Spawn(string path, Vector3 position) =>
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path), position, Quaternion.identity);
        private static PlayerController Player(Vector3 position)
        {
            var result = Spawn("Player/Player.prefab", position).GetComponent<PlayerController>();
            result.GetComponent<PlayerInputDriver>().enabled = false;
            result.Motor.interpolation = RigidbodyInterpolation.None;
            Move(result, position); return result;
        }
        private static Component Surface(int kind, Vector3 position)
        {
            if (kind == 3) return Spawn("ColorBlocks/Prefabs/Green_Ladder.prefab", position).GetComponent<ClimbableVineFeature>();
            var obj = Spawn("ContactCubes/Green_ContactCube.prefab", position);
            var cube = obj.GetComponent<ContactCube>();
            var serialized = new SerializedObject(cube);
            serialized.FindProperty("initialLength").intValue = kind == 0 ? 1 : 4;
            serialized.ApplyModifiedPropertiesWithoutUndo(); cube.ResetForRoom();
            if (kind == 2) obj.transform.rotation = Quaternion.Euler(0, 0, 90);
            obj.GetComponent<Rigidbody>().isKinematic = true;
            typeof(ContactCubeMotion).GetField("<IsSettled>k__BackingField", Private).SetValue(obj.GetComponent<ContactCubeMotion>(), true);
            return obj.GetComponent<HorizontalCubeVine>();
        }
        private static void Refresh(Component source)
        {
            Call(source, source is ClimbableVineFeature ? "RefreshClimbingPlayers" : "FixedUpdate");
        }
        private static void Step(PlayerController player, params Component[] surfaces)
        {
            foreach (var surface in surfaces) Refresh(surface);
            Call(player, "FixedUpdate"); Physics.Simulate(.02f); Physics.SyncTransforms();
        }
        private static void Move(PlayerController player, Vector3 position)
        {
            player.transform.position = player.Motor.position = position;
            player.Motor.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        private static Bounds Bounds(Component surface) => surface.GetComponentInChildren<BoxCollider>().bounds;
        private static void VerifyUp(int kind, int side)
        {
            var source = Surface(kind, new Vector3(100 + kind * 25, 30 + side * 8, 0));
            Physics.SyncTransforms(); var bounds = Bounds(source);
            float edge = side > 0 ? bounds.max.x : bounds.min.x;
            var player = Player(new Vector3(edge + side * .52f, bounds.max.y - .2f, 0));
            player.SetMoveInput(Vector2.up);
            bool protectedOnce = false, completed = false;
            float maximumStep = 0f;
            for (int i = 0; i < 170; i++)
            {
                Vector3 before = player.Motor.position;
                Step(player, source); protectedOnce |= player.IsClimbCornerProtected;
                maximumStep = Mathf.Max(maximumStep, Vector3.Distance(before, player.Motor.position));
                if (protectedOnce && !player.IsClimbCornerProtected && player.Motor.position.y > bounds.max.y + .35f)
                { completed = true; break; }
            }
            string label = kind + "/" + side;
            Check(protectedOnce && completed, "upward outer corner completes " + label + " at " + player.Motor.position);
            Check(maximumStep < .15f, "corner uses bounded motor velocity, not teleport " + label);
            // A fresh corner remains explicitly releasable through the existing jump path.
            Object.DestroyImmediate(player.gameObject);
            player = Player(new Vector3(edge + side * .52f, bounds.max.y - .2f, 0));
            player.SetMoveInput(Vector2.up);
            for (int i = 0; i < 10 && !player.IsClimbCornerProtected; i++) Step(player, source);
            Check(player.IsClimbCornerProtected, "fresh player attaches through surface " + label);
            player.RequestJump();
            Check(!player.IsClimbCornerProtected && !player.IsHorizontalClimbing && player.Motor.linearVelocity.y < 0,
                "jump drops out of upward corner " + label);
            Step(player, source); Check(!player.IsClimbCornerProtected, "jump reattach cooldown " + label);
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(source.gameObject);
        }
        private static void VerifyCoveredTop()
        {
            var source = Surface(3, new Vector3(210, 40, 0));
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.position = source.transform.position + Vector3.up;
            // A grown segment can be a child of the root; its collider must not be excluded.
            roof.transform.SetParent(source.transform, true);
            Physics.SyncTransforms(); var bounds = Bounds(source);
            var player = Player(new Vector3(bounds.max.x + .52f, bounds.max.y - .2f, 0));
            player.SetMoveInput(Vector2.up); bool protectedOnce = false;
            for (int i = 0; i < 4; i++) { Step(player, source); protectedOnce |= player.IsClimbCornerProtected; }
            Check(!protectedOnce, "covered root top does not start premature flip");
            Object.DestroyImmediate(roof); Physics.SyncTransforms();
            for (int i = 0; i < 4; i++) { Step(player, source); protectedOnce |= player.IsClimbCornerProtected; }
            Check(protectedOnce, "removing actual top obstruction permits flip");
            source.gameObject.SetActive(false);
            Check(!player.IsClimbCornerProtected, "disabled vine cancels corner ownership");
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(source.gameObject);
        }
        private static void VerifyChain()
        {
            var root = (ClimbableVineFeature)Surface(3, new Vector3(240, 40, 0));
            var upper = Spawn("ColorBlocks/Prefabs/Green_LadderSon.prefab", root.transform.position + Vector3.up)
                .GetComponent<ClimbableVineFeature>();
            upper.transform.SetParent(root.transform, true);
            root.ConfigureInitialGrowth(new[] { upper.gameObject });
            Physics.SyncTransforms();
            var player = Player(root.transform.position + new Vector3(1.02f, .25f, 0));
            player.SetMoveInput(Vector2.up); bool upperProtected = false, rootProtected = false, completed = false;
            for (int i = 0; i < 180; i++)
            {
                Step(player, root, upper);
                rootProtected |= player.IsClimbCornerProtectedBy(root);
                upperProtected |= player.IsClimbCornerProtectedBy(upper);
                if (upperProtected && !player.IsClimbCornerProtected && player.Motor.position.y > Bounds(upper).max.y + .35f)
                { completed = true; break; }
            }
            Check(!rootProtected && upperProtected && completed, "linked vine passes buried edge and wraps only exposed upper segment");
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(root.gameObject);
        }
        private static void VerifyHorizontalWrap()
        {
            foreach (int side in new[] { -1, 1 })
            {
                var source = Surface(1, new Vector3(270, 40 + side * 10, 0));
                Physics.SyncTransforms(); var bounds = Bounds(source);
                float edge = side > 0 ? bounds.max.x : bounds.min.x;
                var player = Player(new Vector3(edge - side * .1f, bounds.max.y + .52f, 0));
                player.SetMoveInput(Vector2.right * side);
                for (int i = 0; i < 210; i++) Step(player, source);
                Check(player.IsHorizontalClimbing && player.Motor.position.y < bounds.min.y - .35f &&
                    player.Motor.position.y > bounds.min.y - .8f, "existing top-to-underside wrap preserved " + side);
                player.enabled = false; player.enabled = true;
                Move(player, new Vector3(edge - side * .1f, bounds.min.y - .52f, 0));
                player.SetHorizontalVineSurface(source, player.Motor.position.y, 2f);
                player.SetMoveInput(Vector2.right * side);
                for (int i = 0; i < 210; i++) Step(player, source);
                Check(player.IsHorizontalClimbing && player.Motor.position.y > bounds.max.y + .35f &&
                    player.Motor.position.y < bounds.max.y + .8f, "existing underside-to-top wrap preserved " + side);
                Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(source.gameObject);
            }
        }
        private static void Finish(bool pass, string result)
        {
            File.WriteAllText(Path.Combine(Application.dataPath, "../Library/ClimbCornerVerification.txt"), result);
            SessionState.SetBool(Key, false); EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
