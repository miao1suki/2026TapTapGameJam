using NUnit.Framework;
using UnityEngine;

namespace Project.InputAbstraction.Tests
{
    public sealed class DesktopCursorPolicyTests
    {
        [TestCase(true, true, CursorLockMode.Confined)]
        [TestCase(true, false, CursorLockMode.None)]
        [TestCase(false, true, CursorLockMode.None)]
        [TestCase(false, false, CursorLockMode.None)]
        public void FocusBoundaryUsesConfinementNotRelativeMouse(bool desktop, bool focused, CursorLockMode expected)
        {
            Assert.That(DesktopCursorPolicy.ResolveLockMode(desktop, focused), Is.EqualTo(expected));
        }
    }
}
