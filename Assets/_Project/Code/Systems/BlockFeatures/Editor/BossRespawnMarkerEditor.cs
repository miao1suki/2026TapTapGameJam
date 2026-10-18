using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(BossRespawnMarker))]
    public sealed class BossRespawnMarkerEditor :
        UnityEditor.Editor
    {
        private bool toolsHiddenCaptured;
        private bool previousToolsHidden;

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            BossRespawnMarker marker =
                (BossRespawnMarker)target;
            VisualElement root =
                ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "Boss 复活点标记"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "仅作为 Boss 复活位置的 Scene 标记，没有 Collider 或 Renderer。只能在 X 方向拖动，业务数据保存在重生点组件。"));
            root.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "所属重生点",
                () => marker.Owner != null
                    ? marker.Owner.name
                    : "未连接"));
            root.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "横向位置",
                () => marker.Owner != null
                    ? $"{marker.Owner.BossRespawnLocalX:0.###} 格"
                    : "-"));
            var selectOwner = new Button(() =>
            {
                if (marker.Owner == null)
                {
                    return;
                }

                Selection.activeGameObject =
                    marker.Owner.gameObject;
                EditorGUIUtility.PingObject(
                    marker.Owner.gameObject);
            })
            {
                text = "选中重生点"
            };
            root.Add(selectOwner);
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }

        private void OnSceneGUI()
        {
            BossRespawnMarker marker =
                (BossRespawnMarker)target;
            RespawnAnchorFeature owner = marker != null
                ? marker.Owner
                : null;
            if (owner == null ||
                !owner.IsBossArenaRespawnPoint ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RestoreSceneTools();
                return;
            }

            if (!toolsHiddenCaptured)
            {
                previousToolsHidden = Tools.hidden;
                toolsHiddenCaptured = true;
            }

            Tools.hidden = true;
            marker.SyncFromOwner();
            CompareFunction previousZTest = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = CompareFunction.Always;
            Handles.color = new Color(1f, .62f, .25f, .95f);

            Vector3 anchorPosition = owner.transform.position;
            Vector3 markerPosition = marker.transform.position;
            float handleSize =
                HandleUtility.GetHandleSize(markerPosition) *
                .13f;
            Handles.DrawDottedLine(
                new Vector3(
                    anchorPosition.x,
                    markerPosition.y,
                    markerPosition.z),
                markerPosition,
                5f);
            Handles.Label(
                markerPosition + Vector3.up *
                (handleSize * 2f),
                "Boss 复活位置");

            if (Handles.Button(
                    markerPosition,
                    Quaternion.identity,
                    handleSize * 1.35f,
                    handleSize,
                    Handles.SphereHandleCap))
            {
                Selection.activeGameObject =
                    marker.gameObject;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(
                markerPosition,
                Vector3.right,
                handleSize * 1.7f,
                Handles.CubeHandleCap,
                0f);
            if (EditorGUI.EndChangeCheck())
            {
                moved.y = markerPosition.y;
                moved.z = markerPosition.z;
                float localX = owner.transform
                    .InverseTransformPoint(moved).x;
                Undo.RecordObject(
                    owner,
                    "移动 Boss 复活位置");
                owner.SetBossRespawnLocalX(localX);
                EditorUtility.SetDirty(owner);
                if (PrefabUtility.IsPartOfPrefabInstance(owner))
                {
                    PrefabUtility
                        .RecordPrefabInstancePropertyModifications(
                            owner);
                }

                marker.SyncFromOwner();
                SceneView.RepaintAll();
            }

            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }

        private void OnDisable()
        {
            RestoreSceneTools();
        }

        private void RestoreSceneTools()
        {
            if (!toolsHiddenCaptured)
            {
                return;
            }

            Tools.hidden = previousToolsHidden;
            toolsHiddenCaptured = false;
        }
    }

    [InitializeOnLoad]
    internal static class BossRespawnMarkerMaintenance
    {
        static BossRespawnMarkerMaintenance()
        {
            EditorApplication.hierarchyChanged += ScheduleRepair;
            EditorApplication.delayCall += RepairAll;
        }

        private static void ScheduleRepair()
        {
            EditorApplication.delayCall -= RepairAll;
            EditorApplication.delayCall += RepairAll;
        }

        private static void RepairAll()
        {
            if (Application.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RespawnAnchorFeature[] anchors =
                Object.FindObjectsByType<RespawnAnchorFeature>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0;
                 index < anchors.Length;
                 index++)
            {
                RespawnAnchorFeature anchor = anchors[index];
                if (anchor == null ||
                    !anchor.IsBossArenaRespawnPoint)
                {
                    continue;
                }

                BossRespawnMarker marker =
                    anchor.EnsureBossRespawnMarker(true);
                if (marker == null)
                {
                    continue;
                }

                marker.gameObject.SetActive(true);
                marker.Bind(anchor);
                EditorUtility.SetDirty(anchor);
                EditorUtility.SetDirty(marker);
            }
        }
    }
}
