using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only inventory of final Scene evidence, not a planning import format.</summary>
public static class FinalScenePlanningAudit
{
    [Serializable] private sealed class Document { public Room[] rooms; public Box[] assemblyPatches; public string playerStartRoomId; public Vector2 playerStartLocal; public Vector3 playerStartWorld; }
    [Serializable] private sealed class Room { public string id; public Box[] boxes; }
    [Serializable] private sealed class Box { public string id; }
    [Serializable] private sealed class Row
    {
        public string objectId, hierarchy, planningId;
        public Vector2Int markerCell, markerSize, planningLocalCell;
        public Vector3 localPosition, worldPosition, localScale, worldScale, markerDerivedCenter;
        public Quaternion localRotation;
        public bool markerPositionDiffers;
        public string[] serializedComponents;
    }
    [Serializable] private sealed class Report
    {
        public string scenePath, sceneSHA256, planningSHA256;
        public string formatWarning = "Diagnostic only: component JSON uses session references; this is NOT an importable or round-trip planning file. Scene remains authoritative.";
        public int planningRecords, sceneInstances, markerPositionDifferences;
        public string[] planningIdsWithoutInstances, sceneIdsAbsentFromPlanning, repeatedSourceLocalKeys;
        public Vector3[] playerPositions;
        public string plannedPlayerRoom;
        public Vector2 plannedPlayerLocal;
        public Vector3 plannedPlayerUnassignedWorld;
        public Row[] rows;
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/NeutralMechanismProbe/Assets"))
            throw new InvalidOperationException("Use isolated NeutralMechanismProbe; audit must not replace an open author scene.");
        var summaries = new List<string>();
        try
        {
            foreach (string name in new[] { "R1", "R2", "R3" })
            {
                string path = $"Assets/Scenes/{name}/{name}.unity";
                string planning = $"Assets/Scenes/{name}/{name}.planning.json";
                string before = Hash(path);
                var document = JsonUtility.FromJson<Document>(File.ReadAllText(planning));
                var boxes = (document.rooms ?? Array.Empty<Room>()).SelectMany(r => r.boxes ?? Array.Empty<Box>())
                    .Concat(document.assemblyPatches ?? Array.Empty<Box>()).ToArray();
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var blocks = roots.SelectMany(r => r.GetComponentsInChildren<LevelEditorPlacedBlock>(true)).ToArray();
                var rows = blocks.Select(b => new Row
                {
                    objectId = GlobalObjectId.GetGlobalObjectIdSlow(b).ToString(),
                    hierarchy = Hierarchy(b.transform), planningId = b.PlanningBoxId,
                    markerCell = b.Cell, markerSize = b.SizeCells, planningLocalCell = b.PlanningLocalCell,
                    localPosition = b.transform.localPosition, worldPosition = b.transform.position,
                    localScale = b.transform.localScale, worldScale = b.transform.lossyScale, localRotation = b.transform.localRotation,
                    markerDerivedCenter = new Vector3((b.Cell.x + b.SizeCells.x * .5f) * b.CellWorldSize, (b.Cell.y + b.SizeCells.y * .5f) * b.CellWorldSize, b.transform.position.z),
                    serializedComponents = b.GetComponents<MonoBehaviour>().Where(c => c != null).Select(c => c.GetType().FullName + "\n" + EditorJsonUtility.ToJson(c)).ToArray()
                }).ToArray();
                foreach (var row in rows) row.markerPositionDiffers = (row.worldPosition - row.markerDerivedCenter).sqrMagnitude > .000001f;
                var planned = new HashSet<string>(boxes.Select(b => b.id));
                var actual = new HashSet<string>(rows.Select(r => r.planningId));
                var report = new Report
                {
                    scenePath = path, sceneSHA256 = before, planningSHA256 = Hash(planning),
                    planningRecords = boxes.Length, sceneInstances = blocks.Length,
                    markerPositionDifferences = rows.Count(r => r.markerPositionDiffers),
                    planningIdsWithoutInstances = planned.Except(actual).OrderBy(s => s).ToArray(),
                    sceneIdsAbsentFromPlanning = actual.Except(planned).OrderBy(s => s).ToArray(),
                    repeatedSourceLocalKeys = rows.GroupBy(r => r.planningId + " @ " + r.planningLocalCell)
                        .Where(g => g.Count() > 1).Select(g => g.Key + " count=" + g.Count()).ToArray(),
                    playerPositions = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.CompareTag("Player")).Select(t => t.position).ToArray(),
                    plannedPlayerRoom = document.playerStartRoomId, plannedPlayerLocal = document.playerStartLocal,
                    plannedPlayerUnassignedWorld = document.playerStartWorld, rows = rows
                };
                if (Hash(path) != before) throw new InvalidOperationException("Author Scene changed during read-only audit: " + path);
                File.WriteAllText($"Library/{name}.final-scene-audit.json", JsonUtility.ToJson(report, true));
                summaries.Add($"{name}: planning records={report.planningRecords}, scene instances={report.sceneInstances}, marker center differences={report.markerPositionDifferences}, stale planning IDs={report.planningIdsWithoutInstances.Length}, unknown source IDs={report.sceneIdsAbsentFromPlanning.Length}, repeated source/local keys={report.repeatedSourceLocalKeys.Length}, players={string.Join(";", report.playerPositions.Select(p => p.ToString("F4")))}; SHA={before}");
            }
            File.WriteAllLines("Library/FinalScenePlanningAudit.txt", summaries);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("Library/FinalScenePlanningAuditFailure.txt", ex.ToString());
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }

    private static string Hash(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    private static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
}
