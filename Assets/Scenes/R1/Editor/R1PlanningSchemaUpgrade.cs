using System;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit main-schema migration of R1 metadata, never a layout rebuild.</summary>
public static class R1PlanningSchemaUpgrade
{
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0 || EditorApplication.isPlaying)
            throw new InvalidOperationException("Only migrate a disposable silent copy of R1.");
        var scene = EditorSceneManager.OpenScene(R1LevelBuilder.ScenePath);
        var doc = PlanningSaveStore.Read(R1LevelBuilder.SavePath);
        var blocks = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None)
            .Where(p => !string.IsNullOrEmpty(p.PlanningBoxId)).ToArray();
        if (blocks.Length != 76) throw new InvalidOperationException("Expected remote R1's 76 authored roots.");
        var before = blocks.ToDictionary(p => p.PlanningBoxId, p =>
            (instance: p.GetInstanceID(), position: p.transform.position, rotation: p.transform.rotation, scale: p.transform.localScale));
        int upgraded = 0;
        foreach (var block in blocks)
        {
            string signature = block.PlanningSignature;
            int split = FirstJsonEnd(signature);
            var source = JsonUtility.FromJson<PlanningBox>(signature.Substring(0, split));
            string normalized = JsonUtility.ToJson(source.PersistentClone()) + signature.Substring(split);
            if (normalized == signature) continue;
            // Re-serialization only adds current default schema fields. A changed
            // authored parameter still differs from the staged desired signature.
            block.SetPlanningSource(block.PlanningBoxId, normalized, block.PlanningLocalCell);
            EditorUtility.SetDirty(block);
            if (PrefabUtility.IsPartOfPrefabInstance(block)) PrefabUtility.RecordPrefabInstancePropertyModifications(block);
            upgraded++;
        }
        try
        {
            PlanningSceneBuilder.SuppressSceneUndo = true;
            if (!PlanningSceneBuilder.TryApplyDocument(doc, out string message)) throw new InvalidOperationException(message);
            var after = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None)
                .Where(p => !string.IsNullOrEmpty(p.PlanningBoxId)).ToArray();
            if (after.Length != before.Count) throw new InvalidOperationException("Refuse migration: authored count changed.");
            foreach (var block in after)
            {
                var saved = before[block.PlanningBoxId];
                if (block.GetInstanceID() != saved.instance ||
                    Vector3.Distance(block.transform.position, saved.position) > .0001f ||
                    Quaternion.Angle(block.transform.rotation, saved.rotation) > .001f || block.transform.localScale != saved.scale)
                    throw new InvalidOperationException("Refuse migration: identity/pose changed for " + block.PlanningBoxId);
            }
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save migrated metadata.");
            PlanningSaveStore.Save(R1LevelBuilder.SavePath, doc);
            R1HorizontalMirror.VerifySaved();
            string report = $"PASS: normalized {upgraded} R1 metadata signatures; all 76 instance identities and exact manual poses retained. Paired planning saved with main's default schema; saved reload/no-op verification passed. No layout rebuild or mirror performed.";
            File.WriteAllText("Library/R1PlanningSchemaUpgrade.txt", report);
            Debug.Log(report);
        }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
    }

    private static int FirstJsonEnd(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '{') throw new InvalidOperationException("Missing R1 planning signature.");
        bool quoted = false, escaped = false;
        int depth = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quoted = false;
                continue;
            }
            if (c == '"') quoted = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i + 1;
        }
        throw new InvalidOperationException("Malformed R1 planning signature.");
    }
}
