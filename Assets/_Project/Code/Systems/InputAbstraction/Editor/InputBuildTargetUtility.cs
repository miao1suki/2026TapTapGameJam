using System.Collections.Generic;
using Project;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Project.InputAbstraction.Editor
{
    internal static class InputBuildTargetUtility
    {
        internal static InputPlatformMode Current => FromBuildTarget(EditorUserBuildSettings.activeBuildTarget);

        internal static InputPlatformMode FromBuildTarget(BuildTarget target)
        {
            return target == BuildTarget.Android || target == BuildTarget.iOS
                ? InputPlatformMode.Mobile
                : InputPlatformMode.Desktop;
        }
    }

    internal sealed class PlatformUILayoutBuildTargetSync : IActiveBuildTargetChanged
    {
        public int callbackOrder => 100;

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            InputPlatformMode previousMode = InputBuildTargetUtility.FromBuildTarget(previousTarget);
            InputPlatformMode newMode = InputBuildTargetUtility.FromBuildTarget(newTarget);
            if (previousMode == newMode)
            {
                return;
            }

            IReadOnlyList<PlatformUILayoutController> controllers =
                ProjectDiscovery.FindAll<
                    PlatformUILayoutController>(true);
            for (int index = 0; index < controllers.Count; index++)
            {
                PlatformUILayoutController controller = controllers[index];
                if (!controller.SynchronizeWhenBuildTargetChanges ||
                    EditorUtility.IsPersistent(controller))
                {
                    continue;
                }

                PlatformUILayoutControllerEditor.RecordLayoutObjects(
                    controller,
                    "Switch platform UI preset");
                controller.CaptureLayout(previousMode);
                controller.ApplyLayout(newMode);
                PlatformUILayoutControllerEditor.MarkDirty(controller);
            }

            if (controllers.Count > 0)
            {
                Debug.Log($"Platform UI switched: {previousMode} -> {newMode}.");
            }
        }
    }
}
