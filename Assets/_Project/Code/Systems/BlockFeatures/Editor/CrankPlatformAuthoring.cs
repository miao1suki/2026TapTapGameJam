using System.Collections.Generic;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    [InitializeOnLoad]
    internal static class CrankPlatformAuthoring
    {
        internal const string PlatformPath = "Assets/_Project/Content/ColorBlocks/Prefabs/Red_CrankLift.prefab";
        private static readonly HashSet<int> pending = new HashSet<int>();
        static CrankPlatformAuthoring() => ObjectChangeEvents.changesPublished += Changed;
        private static void Changed(ref ObjectChangeEventStream stream)
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i=0;i<stream.length;i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy) continue;
                stream.GetCreateGameObjectHierarchyEvent(i,out var change);
                pending.Add(change.instanceId);
            }
            EditorApplication.delayCall -= Flush;
            EditorApplication.delayCall += Flush;
        }
        private static void Flush()
        {
            var ids = new List<int>(pending); pending.Clear();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (int id in ids)
            {
                var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid() || EditorSceneManager.IsPreviewScene(go.scene)) continue;
                foreach (var crank in go.GetComponentsInChildren<CrankPlatformFeature>(true))
                {
                    if (crank.InitialPlatformCreated) continue;
                    // Planner creation is transactional and must own its document IDs.
                    var placed = crank.GetComponent<LevelEditorPlacedBlock>();
                    if (placed != null && !string.IsNullOrEmpty(placed.PlanningBoxId)) continue;
                    AddPlatform(crank);
                }
            }
        }
        internal static MovingPlatformFeature AddPlatform(CrankPlatformFeature crank)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPath);
            if (crank == null || prefab == null || EditorApplication.isPlaying || !crank.gameObject.scene.IsValid()) return null;
            int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("添加曲柄升降平台");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab,crank.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go,"添加升降平台");
            go.transform.SetParent(crank.transform.parent,true);
            go.transform.position = crank.transform.position + new Vector3(1,1,0) * GridCellSizeUtility.Resolve(crank);
            var platform = go.GetComponent<MovingPlatformFeature>(); platform.BindCrank(crank);
            Undo.RecordObject(crank,"记录曲柄平台创建"); crank.MarkInitialPlatformCreated();
            PrefabUtility.RecordPrefabInstancePropertyModifications(crank);
            PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            EditorUtility.SetDirty(crank); EditorUtility.SetDirty(platform);
            EditorSceneManager.MarkSceneDirty(crank.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll(); return platform;
        }
    }
}
