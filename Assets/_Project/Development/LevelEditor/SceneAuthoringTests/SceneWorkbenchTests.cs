using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Reflection is deliberate: test asmdefs cannot reference Unity's predefined
// Assembly-CSharp-Editor. Authoring code stays in its established assembly.
public sealed class SceneWorkbenchTests
{
    [TestCase("hole")]
    [TestCase("adjacency")]
    [TestCase("overlap")]
    [TestCase("empty-disconnected")]
    [TestCase("snapshot-isolation")]
    [TestCase("staged-cancel")]
    [TestCase("occupied-erase")]
    [TestCase("legacy-island")]
    [TestCase("art-staged-cancel")]
    [TestCase("art-staged-undo")]
    [TestCase("art-owned-undo")]
    [TestCase("resource-stroke-cache")]
    [TestCase("session-status-refresh")]
    [TestCase("custom-mechanism-preview")]
    public void PreservesAuthoringContract(string name)
    {
        Type audit = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Project.LevelEditor.Editor.SceneWorkbenchAudit"))
            .FirstOrDefault(t => t != null);
        Assert.That(audit, Is.Not.Null, "Authoring Editor assembly did not load");
        try { audit.GetMethod("RunCase", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { name }); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }
}
