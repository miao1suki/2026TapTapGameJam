using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.Mechanisms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Silent, isolated regression; never saves the author scenes.</summary>
public static class DoorFootprintAudit
{
    static int checks;
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio")) throw new InvalidOperationException("Silent isolated probe only");
        int exit = 0, doors = 0;
        try
        {
            foreach (string scenePath in new[] { "Assets/_Project/Scenes/Playing/Gaming.unity", "Assets/Scenes/R1/R1.unity", "Assets/Scenes/R2/R2.unity", "Assets/_Project/Scenes/Boss.unity" })
            {
                var scene = EditorSceneManager.OpenScene(scenePath);
                foreach (var door in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DoorController>(true)))
                {
                    Verify(door); doors++;
                }
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (bool horizontal in new[] { false, true })
            foreach (float scale in new[] { 1f, 2f, .5f })
            {
                var path = "Assets/_Project/Content/Mechanisms/" + (horizontal ? "DoorHorizontal" : "Door") + ".prefab";
                var owner = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                owner.transform.position = new Vector3(5.1875f, 8.3125f, .125f);
                owner.transform.localScale = new Vector3(scale, 2f * scale, 1f);
                var placed = owner.AddComponent<LevelEditorPlacedBlock>();
                placed.Configure(new Vector2Int(5, 8), "Door", Color.white, false, true);
                placed.SetSizeCells(horizontal ? new Vector2Int(2, 1) : new Vector2Int(1, 2));
                Verify(owner.GetComponent<DoorController>());
                UnityEngine.Object.DestroyImmediate(owner);
            }
            Check(doors > 0, "real authored doors were tested");
            Debug.Log("DoorFootprintAudit PASS " + checks + " checks, " + doors + " authored doors");
        }
        catch (Exception error) { Debug.LogException(error); exit = 1; }
        File.WriteAllText("Library/DoorFootprintAudit.txt", (exit == 0 ? "PASS " : "FAIL ") + checks + " checks, " + doors + " authored doors");
        EditorApplication.Exit(exit);
    }
    static void Verify(DoorController door)
    {
        var transform = door.transform;
        var position = transform.position; var rotation = transform.rotation; var scale = transform.localScale;
        var collider = door.GetComponent<BoxCollider>();
        var previousCentre = transform.TransformPoint(collider.center);
        bool planning = door.TryGetPlanningWorldFootprint(out var expected);
        if (!planning) expected = collider.bounds;
        typeof(DoorController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(door, null);
        Check(transform.position == position && transform.rotation == rotation && transform.localScale == scale,
            "preserve author root including fractional pose: " + door.name);
        Check((transform.TransformPoint(collider.center) - previousCentre).sqrMagnitude < .000001f, "preserve physical centre: " + door.name);
        Check((collider.bounds.size - expected.size).sqrMagnitude < .000001f, "physical footprint: " + door.name);
        var presentation = MechanismArtworkPresentation.Ensure(door.gameObject);
        presentation.Refresh(0f);
        var visual = presentation.Visual.bounds;
        // Only planning doors have a map-size contract; standalone custom-scale doors
        // retain their existing native geometry instead of being silently migrated.
        if (planning)
        {
            Check(Mathf.Abs(visual.min.x - expected.min.x) < .0001f && Mathf.Abs(visual.max.x - expected.max.x) < .0001f,
                "art horizontal bounds equal collision: " + door.name);
            Check(Mathf.Abs(visual.min.y - expected.min.y) < .0001f && Mathf.Abs(visual.max.y - expected.max.y) < .0001f,
                "art vertical bounds equal collision: " + door.name);
            var preview = typeof(Project.BlockFeatures.Editor.ArtworkScenePreview).GetMethod("Footprint", BindingFlags.NonPublic | BindingFlags.Static);
            var previewBounds = (Bounds)preview.Invoke(null, new object[] { door.gameObject, door.GetComponent<LevelEditorPlacedBlock>().CellWorldSize });
            Check((previewBounds.center - expected.center).sqrMagnitude < .000001f && (previewBounds.size - expected.size).sqrMagnitude < .000001f,
                "Scene preview shares runtime footprint: " + door.name);
        }
        // Repeat after disable/reset must not shrink again or move the root.
        door.ResetDoor(); presentation.Refresh(0f);
        Check((collider.bounds.size - expected.size).sqrMagnitude < .000001f && transform.position == position, "reset remains stable: " + door.name);
    }
}
