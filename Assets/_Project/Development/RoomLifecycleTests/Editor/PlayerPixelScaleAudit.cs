using System;
using System.IO;
using System.Reflection;
using Project.ObjectArtwork;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerPixelScaleAudit
{
    private const string Flag = "PlayerPixelScaleAudit.Running";
    private static double started;
    private static int checks;
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
        checks++; Debug.Log("PLAYER PIXEL PASS: " + message);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("此验证仅可在隔离工程执行。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .1f) return;
        EditorApplication.update -= Tick;
        GameObject instance = null;
        try
        {
            checks = 0;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
            instance = Object.Instantiate(prefab, new Vector3(100, 100, 0), Quaternion.identity);
            var player = instance.GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            var presentation = instance.GetComponent<PlayerSpritePresentation>();
            var capsule = instance.GetComponent<CapsuleCollider>();
            Check(presentation != null && presentation.Visual != null, "standard prefab creates its existing sprite presentation");
            float radius = capsule.radius, height = capsule.height;
            Vector3 center = capsule.center;
            Check(Mathf.Approximately(radius, .5f) && Mathf.Approximately(height, 1), "standard collision stays one by one cell");
            var frames = Resources.Load<PlayerSpriteAnimationSet>("PlayerVisuals/PlayerSprites");
            foreach (PlayerSpriteState state in Enum.GetValues(typeof(PlayerSpriteState)))
            {
                var clip = frames.GetClip(state);
                Check(clip != null && clip.frames.Length > 0, state + " animation remains connected");
                foreach (var sprite in clip.frames)
                    Check(sprite != null && GridArtworkMetrics.SpriteCells(sprite) == Vector2.one * 1.5f,
                        state + " keeps authored24x24 frame at1.5 cells");
            }
            var refresh = typeof(PlayerSpritePresentation).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (Vector3 scale in new[] { Vector3.one, new Vector3(2, .5f, 1) })
            {
                instance.transform.localScale = scale; refresh.Invoke(presentation, new object[] { 0f });
                SpriteRenderer visual = presentation.Visual;
                float px = Mathf.Abs(visual.transform.lossyScale.x) / visual.sprite.pixelsPerUnit;
                float py = Mathf.Abs(visual.transform.lossyScale.y) / visual.sprite.pixelsPerUnit;
                Check(Mathf.Abs(px - 1f / 16f) < .00001f && Mathf.Abs(py - px) < .00001f,
                    "animation pixels remain square at1/16 cell independent of parent scale " + scale);
                float expectedBottom = instance.transform.TransformPoint(center).y -
                    Mathf.Max(height, radius * 2) * Mathf.Abs(scale.y) * .5f - .08f - 2f / 16f;
                float actualBottom = visual.transform.position.y - visual.sprite.pivot.y * py;
                Check(Mathf.Abs(actualBottom - expectedBottom) < .0001f, "frame bottom adds two art pixels below existing feet offset " + scale);
                Check(capsule.radius == radius && capsule.height == height && capsule.center == center,
                    "visual refresh cannot alter capsule " + scale);
            }
            Check(Mathf.Approximately(presentation.DeathAnimationDuration,
                frames.GetClip(PlayerSpriteState.Death).frames.Length * frames.GetClip(PlayerSpriteState.Death).secondsPerFrame),
                "death animation duration is unchanged");
            Finish(true, "PASS " + checks + " player pixel assertions");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
        finally { if (instance != null) Object.DestroyImmediate(instance); }
    }
    private static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
        File.WriteAllText("Library/PlayerPixelScaleResult.txt", message); Debug.Log(message);
        if (EditorApplication.isPlaying)
        { EditorApplication.ExitPlaymode(); EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1); }
        else EditorApplication.Exit(passed ? 0 : 1);
    }
}
