using System;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Disposable controller checks; never opens or saves an authored scene.</summary>
public static class SharedCrankBrakeAudit
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++;
    }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Hidden).SetValue(target, value);
    private static void Tick(CrankPlatformFeature feature) =>
        typeof(CrankPlatformFeature).GetMethod("OnTick", Hidden).Invoke(feature, new object[] { .1f });
    private static T Create<T>(string name) where T : BlockFeature
    {
        var obj = new GameObject(name);
        var feature = obj.AddComponent<T>();
        feature.enabled = true;
        var runtime = obj.GetComponent<BlockRuntime>();
        if (runtime == null) runtime = obj.AddComponent<BlockRuntime>();
        runtime.RefreshFeatureSet();
        return feature;
    }
    private static void ExpectHeld(CrankPlatformFeature owner, string label)
    {
        float before = owner.Progress;
        Tick(owner);
        Check(Mathf.Abs(before - owner.Progress) < .00001f, label);
    }
    private static void ExpectAdvance(CrankPlatformFeature owner, string label)
    {
        float before = owner.Progress;
        Tick(owner);
        Check(Mathf.Abs(owner.Progress - before - .035f) < .00001f, label);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
            throw new InvalidOperationException("Use the silent isolated verification project only.");
        checks = 0;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = Create<CrankPlatformFeature>("owner");
            var secondary = Create<CrankPlatformFeature>("secondary");
            var other = Create<CrankPlatformFeature>("other owner");
            var primaryBrake = Create<MechanismBaseFeature>("primary brake");
            var secondaryBrake = Create<MechanismBaseFeature>("secondary brake");
            Check(owner.IsDriveAvailable && secondary.IsDriveAvailable && other.IsDriveAvailable, "active fixture");
            Check(secondary.BindSharedController(owner), "secondary joins group");
            owner.BindBase(primaryBrake); secondary.BindBase(secondaryBrake);
            Set(owner, "progress", .2f); Set(owner, "engaged", true);
            ExpectAdvance(owner, "unpowered group advances once");
            ExpectHeld(secondary, "secondary never advances its own clock");
            Check(Mathf.Abs(owner.Progress - secondary.Progress) < .00001f, "one authoritative progress");
            Set(secondaryBrake, "powered", true);
            Check(secondaryBrake.IsSignalActive, "active secondary brake fixture");
            ExpectHeld(owner, "secondary brake stops primary operation");
            Set(owner, "engaged", false); Set(secondary, "engaged", true);
            ExpectHeld(owner, "secondary brake stops secondary operation");
            Set(secondary, "engaged", false); Set(owner, "idleStartedAt", -1000f);
            ExpectHeld(owner, "secondary brake also stops idle rebound");
            Set(owner, "engaged", true); Set(primaryBrake, "powered", true);
            Set(secondaryBrake, "powered", false);
            ExpectHeld(owner, "primary brake still stops group");
            Set(secondaryBrake, "powered", true); Set(primaryBrake, "powered", false);
            ExpectHeld(owner, "releasing one of two brakes cannot release the other");
            Set(secondaryBrake, "powered", false);
            ExpectAdvance(owner, "all brakes released resumes without clearing progress");
            Set(secondaryBrake, "powered", true); secondary.enabled = false;
            ExpectAdvance(owner, "disabled secondary cannot phantom-brake owner");
            secondary.enabled = true;
            secondary.GetComponent<BlockRuntime>().RefreshFeatureSet();
            ExpectHeld(owner, "reenabled secondary restores brake binding");
            Check(secondary.BindSharedController(null), "unlink secondary");
            ExpectAdvance(owner, "unlinked brake no longer stops former owner");
            Set(secondary, "progress", .2f); Set(secondary, "engaged", true);
            ExpectHeld(secondary, "unlinked independent crank keeps its own brake");
            Check(secondary.BrakeBase == secondaryBrake && owner.BrakeBase == primaryBrake,
                "distinct authored brake references are preserved");
            Check(secondary.BindSharedController(other), "rebind to another group");
            Set(other, "progress", .4f); Set(other, "engaged", true);
            ExpectHeld(other, "brake follows secondary into new group");
            ExpectAdvance(owner, "old group remains independent after rebind");
            secondary.BindBase(null);
            ExpectAdvance(other, "removing secondary brake releases new group");
            Check(!other.BindSharedController(secondary) && !other.BindSharedController(other),
                "self and cyclic group bindings still rejected");
            secondary.BindBase(secondaryBrake);
            UnityEngine.Object.DestroyImmediate(secondary.gameObject);
            ExpectAdvance(other, "destroyed secondary leaves no phantom brake");
            string report = $"PASS {checks}: shared primary/secondary brakes, advance/rebound, resume, disabled/reenabled, unlink/rebind, independent bindings, cycles and destruction. Controller checks only; real platform motion covered by R1 Play audit.";
            File.WriteAllText("Library/SharedCrankBrakeAudit.txt", report);
            Debug.Log(report); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/SharedCrankBrakeAudit.txt", $"FAIL after {checks}: {error}");
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
