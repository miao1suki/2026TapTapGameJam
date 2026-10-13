using System;
using System.Collections.Generic;
using System.IO;
using Project.BlockFeatures;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanningEditorPrototype
{
    internal static class PlanningMechanismVerification
    {
        private static int checks;
        private static void Check(bool value, string name)
        {
            checks++;
            if (!value) throw new Exception(name);
        }
        public static void Run()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("隔离验证入口");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var root = new GameObject("BindingVerification").transform;
                var baseBox = new PlanningBox { id = "base-stable-id", x = 3, y = 4, width = 2 };
                var consumer = new PlanningBox { id = "platform-stable-id", mechanismBaseId = baseBox.id, mechanismBaseCellOffset = Vector2Int.right };
                consumer = PlanningBox.FromPersistentJson(JsonUtility.ToJson(consumer.PersistentClone()));
                Check(consumer.mechanismBaseId == baseBox.id && consumer.mechanismBaseCellOffset == Vector2Int.right, "persistent JSON retains ID and cell, no instance ID");
                var room = new PlanningRoom("Test") { boxes = new List<PlanningBox> { baseBox, consumer } };
                var rooms = new[] { room };
                var patches = Array.Empty<PlanningBox>();
                var first = Base(root, baseBox.id, new Vector2Int(3, 4));
                var second = Base(root, baseBox.id, new Vector2Int(4, 4));
                var platform = Spawn(root, "Red_Platform").GetComponent<MovingPlatformFeature>();
                platform.gameObject.AddComponent<LevelEditorPlacedBlock>().SetPlanningSource(consumer.id, "", Vector2Int.zero);
                var crank = Spawn(root, "Red_Crank").GetComponent<CrankPlatformFeature>();
                crank.gameObject.AddComponent<LevelEditorPlacedBlock>().SetPlanningSource(consumer.id, "", Vector2Int.zero);
                PlanningMechanismUtility.BindScene(root, rooms, patches);
                Check(platform.LinkedControls.Count == 1 && platform.LinkedControls[0] == second, "batch binding picks clicked base, not first base");
                Check(crank.BrakeBase == second, "crank shares persistent binding contract");
                baseBox.x += 10;
                first.GetComponent<LevelEditorPlacedBlock>().SetPlanningSource(baseBox.id, "", new Vector2Int(13, 4));
                second.GetComponent<LevelEditorPlacedBlock>().SetPlanningSource(baseBox.id, "", new Vector2Int(14, 4));
                PlanningMechanismUtility.BindScene(root, rooms, patches);
                Check(platform.LinkedControls[0] == second && crank.BrakeBase == second, "moving base preserves relative cell binding");
                Object.DestroyImmediate(second.gameObject);
                second = Base(root, baseBox.id, new Vector2Int(14, 4));
                PlanningMechanismUtility.BindScene(root, rooms, patches);
                Check(platform.LinkedControls[0] == second && crank.BrakeBase == second, "rebuilding objects relinks new instance by authored ID");
                room.boxes.Remove(baseBox);
                PlanningMechanismUtility.BindScene(root, rooms, patches);
                Check(platform.LinkedControls.Count == 0 && crank.BrakeBase == null, "deleted base removes stale links");
                var batch = new PlanningBox { id="batch", x=10, y=5, width=3 };
                var part = new PlanningBox { id="part", x=11, y=5, width=2 };
                consumer.mechanismBaseId = batch.id; consumer.mechanismBaseCellOffset = Vector2Int.right;
                PlanningMechanismUtility.RemapCarvedSource(batch,new[] { part },new[] { consumer });
                Check(consumer.mechanismBaseId == part.id && consumer.mechanismBaseCellOffset == Vector2Int.zero,"erasing another base preserves selected cell binding");
                PlanningMechanismUtility.RemapCarvedSource(part, Array.Empty<PlanningBox>(),new[] { consumer });
                Check(string.IsNullOrEmpty(consumer.mechanismBaseId),"erasing selected base clears persistent binding");
                File.WriteAllText("Library/PlanningMechanismVerification.txt", "PASS " + checks);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/PlanningMechanismVerification.txt", error.ToString());
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
        private static GameObject Spawn(Transform root, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/" + name + ".prefab");
            return Object.Instantiate(prefab, root);
        }
        private static MechanismBaseFeature Base(Transform root, string id, Vector2Int cell)
        {
            var value = Spawn(root, "Red_Foundation");
            value.AddComponent<LevelEditorPlacedBlock>().SetPlanningSource(id, "", cell);
            return value.GetComponent<MechanismBaseFeature>();
        }
    }
}
