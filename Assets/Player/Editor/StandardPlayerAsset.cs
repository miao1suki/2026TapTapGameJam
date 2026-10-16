using System;
using System.Linq;
using Project.CameraModes;
using Project.GameFlow;
using Project.GreenHook;
using Project.Development.PickWheel;
using TaSource.LinkEffect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Project.Player.Editor
{
    /// <summary>The only authoring source for gameplay players.</summary>
    public static class StandardPlayerAsset
    {
        public const string Path = "Assets/_Project/Content/Player/Player.prefab";
        public static GameObject Load() => AssetDatabase.LoadAssetAtPath<GameObject>(Path);

        public static GameObject EnsureInScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid()) return null;
            var players = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).ToArray();
            if (players.Length > 1) throw new InvalidOperationException("当前场景有多个玩家，请先保留一个玩家。");
            if (players.Length == 1) return players[0].gameObject;
            var prefab = Load();
            if (prefab == null) throw new InvalidOperationException("缺少标准玩家预制体：" + Path);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(player, "创建标准玩家");
            WireScene(scene, player);
            return player;
        }

        public static void WireScene(Scene scene, GameObject player)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var context in root.GetComponentsInChildren<LevelSceneContext>(true))
                { context.SetPlayerPrefab(Load()); EditorUtility.SetDirty(context); }
                foreach (var follow in root.GetComponentsInChildren<CameraFollowController>(true))
                { follow.SetTarget(player.transform, false); EditorUtility.SetDirty(follow); }
                foreach (var manager in root.GetComponentsInChildren<CameraControlManager>(true))
                { manager.SetFocusTarget(player.transform, false); EditorUtility.SetDirty(manager); }
                foreach (var mode in root.GetComponentsInChildren<CameraModeController>(true))
                { mode.SetFollowTarget(player.transform, false); EditorUtility.SetDirty(mode); }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // One-time migration is explicit, never runs on opening the editor or entering play.
        public static void MigrateProject()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe"))
                throw new InvalidOperationException("请在隔离验证工程中执行迁移，避免覆盖当前窗口未保存的场景。");
            const string oldPrimary = "Assets/_Project/Content/Player/Player(暂定).prefab";
            if (Load() == null)
            {
                string error = AssetDatabase.MoveAsset(oldPrimary, Path);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            var contents = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                contents.name = "Player"; contents.tag = "Player";
                contents.transform.localPosition = Vector3.zero;
                contents.transform.localRotation = Quaternion.identity;
                contents.transform.localScale = Vector3.one;
                var capsule = contents.GetComponent<CapsuleCollider>();
                capsule.center = Vector3.zero; capsule.height = 1f; capsule.radius = .5f;
                foreach (var extra in contents.GetComponents<BoxCollider>()) UnityEngine.Object.DestroyImmediate(extra);
                Ensure<PlayerHealth>(contents); Ensure<PlayerFallDamage>(contents);
                var wheel = Ensure<PlayerColorWheel>(contents);
                var sensor = Ensure<PlayerInteractionSensor>(contents);
                SetReference(contents.GetComponent<PickWheelPrototype>(), "playerWheel", wheel);
                SetReference(contents.GetComponent<InteractionLinkEffect>(), "colorWheel", wheel);
                SetReference(contents.GetComponent<InteractionLinkEffect>(), "sensor", sensor);
                SetReference(contents.GetComponent<GreenHookAbility>(), "colorWheel", wheel);
                var visual = contents.transform.Find("Player Visual");
                if (visual != null) visual.localPosition = Vector3.down * .08f;
                var body = contents.GetComponent<Rigidbody>();
                body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var director = contents.GetComponent<PlayableDirector>();
                director.playOnAwake = false; director.playableAsset = null;
                PrefabUtility.SaveAsPrefabAsset(contents, Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/Plugins/")) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var players = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).ToArray();
                if (players.Length == 0) continue;
                foreach (var controller in players)
                {
                    var player = controller.gameObject;
                    // Scene-owned cameras are not part of the canonical player asset.
                    // Keep them alive while non-default prefab additions are cleared.
                    var cameras = player.GetComponentsInChildren<Camera>(true)
                        .Where(c => c.transform != player.transform).ToArray();
                    foreach (var camera in cameras) camera.transform.SetParent(null, true);
                    if (PrefabUtility.IsPartOfPrefabInstance(player))
                    {
                        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player) != Path)
                            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(player, Load(), new PrefabReplacingSettings {
                                objectMatchMode = ObjectMatchMode.ByHierarchy,
                                prefabOverridesOptions = PrefabOverridesOptions.ClearAllNonDefaultOverrides,
                                changeRootNameToAssetName = true
                            }, InteractionMode.AutomatedAction);
                        else PrefabUtility.RevertPrefabInstance(player, InteractionMode.AutomatedAction);
                    }
                    else
                        PrefabUtility.ConvertToPrefabInstance(player, Load(), new ConvertToPrefabInstanceSettings {
                            objectMatchMode = ObjectMatchMode.ByHierarchy,
                            componentsNotMatchedBecomesOverride = false,
                            gameObjectsNotMatchedBecomesOverride = false,
                            recordPropertyOverridesOfMatches = false,
                            changeRootNameToAssetName = true
                        }, InteractionMode.AutomatedAction);
                    foreach (var camera in cameras) camera.transform.SetParent(player.transform, true);
                    player.name = "Player";
                    player.transform.localScale = Vector3.one;
                    WireScene(scene, player);
                    Debug.Log("STANDARD PLAYER MIGRATED " + path + " @ " + player.transform.position);
                    count++;
                }
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save " + path);
            }
            // References in existing scenes were remapped by Unity before retiring this source.
            AssetDatabase.DeleteAsset("Assets/_Project/Content/Player/RuntimePlayer.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("STANDARD PLAYER MIGRATION COMPLETE " + count);
        }

        private static T Ensure<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
        private static void SetReference(Component component, string name, UnityEngine.Object value)
        {
            if (component == null) return;
            var so = new SerializedObject(component);
            so.FindProperty(name).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
