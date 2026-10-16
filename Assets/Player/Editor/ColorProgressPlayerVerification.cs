using System;
using System.Linq;
using Project.ColorBlocks;
using Project.CameraModes;
using Project.GameFlow;
using Project.LevelEditor.Editor;
using PlanningEditorPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    public static class ColorProgressPlayerVerification
    {
        private const string Key = "ColorProgressPlayerVerification";
        private static int checks, frames;
        private static void Check(bool pass, string label)
        { if (!pass) throw new Exception(label); checks++; Debug.Log("COLOR PLAYER PASS " + label); }

        public static void VerifyAssets()
        {
            Guard(); checks = 0;
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith("Assets/Plugins/"))
                .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentInChildren<PlayerController>(true) != null).ToArray();
            Check(prefabs.Length == 1 && prefabs[0] == StandardPlayerAsset.Path, "exactly one gameplay player prefab");
            var prefab = StandardPlayerAsset.Load();
            Check(prefab.transform.position == Vector3.zero, "prefab origin is world zero");
            Check(prefab.GetComponent<CapsuleCollider>().center == Vector3.zero &&
                prefab.GetComponent<CapsuleCollider>().height == 1 && prefab.GetComponent<CapsuleCollider>().radius == .5f, "authored collider is centered 1x1");
            Check(prefab.GetComponentsInChildren<Collider>(true).Length == 1, "single physical capsule without oversized trigger");
            Check(prefab.GetComponent<UnityEngine.Playables.PlayableDirector>().playableAsset == null &&
                !prefab.GetComponent<UnityEngine.Playables.PlayableDirector>().playOnAwake, "no default test timeline");
            int players = 0;
            foreach (string path in new[] { "Assets/_Project/Scenes/Playing/Gaming.unity", "Assets/Scenes/Pick.unity", "Assets/Scenes/Test.unity",
                "Assets/Scenes/SampleScene.unity", "Assets/RoyTest/RoyTestScene.unity",
                "Assets/_Project/Development/RoomLifecycleTests/RoomRespawnTest.unity", "Assets/_Project/Development/RoomLifecycleTests/MVMapImportTest.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).ToArray();
                Check(found.Length == 1, path + " one player");
                Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                    .Any(c => c.enabled && c.gameObject.activeInHierarchy), path + " active scene camera retained");
                var player = found[0]; players++;
                Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player) == StandardPlayerAsset.Path, path + " standard source");
                Check(player.GetComponent<CapsuleCollider>().center == Vector3.zero && player.GetComponent<CapsuleCollider>().height == 1,
                    path + " no stale collider overrides");
                Check(player.GetComponents<PlayerColorWheel>().Length == 1 && player.GetComponents<PlayerInputDriver>().Length == 1 &&
                    player.GetComponents<TaSource.LinkEffect.InteractionLinkEffect>().Length == 1, path + " no duplicate runtime components");
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var follow in root.GetComponentsInChildren<CameraFollowController>(true))
                        Check(follow.Target == player.transform, path + " camera binding preserved");
                    foreach (var context in root.GetComponentsInChildren<LevelSceneContext>(true))
                        Check(context.PlayerPrefab == prefab, path + " spawn uses standard prefab");
                }
            }
            var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var first = LevelEditorPlayerService.CreateAndBindAtOrigin();
            Check(first != null && first.transform.position == Vector3.zero && LevelEditorState.Player == first, "new map creates and binds origin player");
            Check(LevelEditorPlayerService.CreateAndBindAtOrigin() == first, "new map reuses player, no duplicate");
            var document = new PlanningDocument { hasUnassignedPlayerStart = true, playerStartWorld = Vector3.zero };
            var copy = PlanningDocument.FromPersistentJson(document.ToPersistentJson());
            Check(copy.TryGetPlayerStartWorldPosition(out var position) && position == Vector3.zero, "empty-room map persists world-origin spawn");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            LevelEditorState.Player = null;
            Debug.Log("COLOR PLAYER ASSET VERIFICATION PASSED " + checks + "; scenes=" + players);
        }

        public static void RunPlay()
        {
            Guard(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
        }
        private static void Guard()
        { if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new Exception("Isolated verification only"); }
        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            checks = frames = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 10) return;
            EditorApplication.update -= Tick;
            try
            {
                var service = ColorRuntimeService.Instance; service.ResetProgress();
                var actor = Object.Instantiate(StandardPlayerAsset.Load(), new Vector3(1000, 1000, 0), Quaternion.identity);
                var wheel = actor.GetComponent<PlayerColorWheel>();
                actor.GetComponent<PlayerInputDriver>().enabled = false;
                int worldEvents = 0, abilityEvents = 0;
                service.ColorUnlocked += _ => worldEvents++;
                service.AbilityUnlocked += _ => abilityEvents++;
                foreach (string id in new[] { "red", "green", "blue" })
                {
                    Check(!service.IsUnlocked(id) && !service.IsAbilityUnlocked(id), id + " starts independently locked");
                    var keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/ColorKey_" + id + ".prefab");
                    if (keyPrefab == null)
                        keyPrefab = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Content" })
                            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                            .First(p => p.GetComponent<ColorKeyPickup>()?.ColorTypeId == id);
                    var first = Object.Instantiate(keyPrefab).GetComponent<ColorKeyPickup>();
                    first.OnInteractionPlayerEntered(actor);
                    Check(first.CollectionRequested && first.UnlockResult == ColorKeyUnlockResult.SceneColor && service.IsUnlocked(id), id + " first real key unlocks world");
                    Check(!service.IsAbilityUnlocked(id) && !wheel.TrySelectColor(id), id + " first key cannot select player ability");
                    first.OnInteractionPlayerEntered(actor);
                    Object.DestroyImmediate(first.gameObject);
                    Check(!service.IsAbilityUnlocked(id), id + " duplicate trigger and disable do not advance stage");
                    var second = Object.Instantiate(keyPrefab).GetComponent<ColorKeyPickup>();
                    second.OnInteractionPlayerEntered(actor);
                    Check(second.CollectionRequested && second.UnlockResult == ColorKeyUnlockResult.PlayerAbility && service.IsAbilityUnlocked(id), id + " second real key unlocks ability");
                    Check(wheel.TrySelectColor(id), id + " wheel accepts second-stage ability");
                    Check(service.CollectColorKey(id) == ColorKeyUnlockResult.None, id + " further keys are idempotent");
                    Object.DestroyImmediate(second.gameObject);
                    service.ResetForRoom();
                    Check(service.IsUnlocked(id) && service.IsAbilityUnlocked(id), id + " room reset preserves both stages");
                }
                Check(worldEvents == 3 && abilityEvents == 3, "world and ability events emitted separately once per color");
                service.ResetProgress();
                Check(!service.IsUnlocked("red") && !service.IsAbilityUnlocked("red") && !wheel.HasSelectedColor, "new progress clears both and selection");
                service.UnlockAbility("blue");
                Check(service.IsAbilityUnlocked("blue") && !service.IsUnlocked("blue"), "ability API does not implicitly activate world");
                service.ResetProgress(); service.Unlock("green");
                Check(service.IsUnlocked("green") && !service.IsAbilityUnlocked("green"), "world API does not implicitly grant ability");
                Check(!service.IsAbilityUnlocked("missing-color") && !service.IsUnlocked("missing-color"), "unknown color is never unlocked");
                Object.DestroyImmediate(actor);
                Debug.Log("COLOR PLAYER PLAY VERIFICATION PASSED " + checks);
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            catch (Exception e)
            { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
