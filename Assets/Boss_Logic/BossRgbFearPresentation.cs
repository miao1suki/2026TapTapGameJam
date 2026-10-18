using System.Collections.Generic;
using TapTap.TA.RgbGlitch;
using UnityEngine;

namespace Project.BossLogic
{
    /// <summary>Boss-only presentation bridge, active only while a right-move executor actually owns motion.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/Boss/RGB 惊恐表现桥接")]
    public sealed class BossRgbFearPresentation : MonoBehaviour
    {
        private static readonly HashSet<BossRgbFearPresentation> sources = new HashSet<BossRgbFearPresentation>();
        private static RgbGlitchSceneDriver appliedDriver;
        private BossRightMoveExecutor executor;
        private bool engaged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSources()
        {
            sources.Clear();
            appliedDriver = null;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            executor = GetComponent<BossRightMoveExecutor>();
            if (executor == null) return;
            executor.MotionStateChanged += HandleMotionChanged;
            if (executor.IsMoving) HandleMotionChanged(true);
        }

        private void Update()
        {
            if (!engaged) return;
            if (executor == null || !executor.isActiveAndEnabled || !executor.IsMoving)
            {
                HandleMotionChanged(false);
                return;
            }
            // Rebind only if a legitimate player replacement acquires the lifecycle driver.
            ApplyToCurrentPlayer();
        }

        private void HandleMotionChanged(bool moving)
        {
            if (moving)
            {
                engaged = true;
                sources.Add(this);
                ApplyToCurrentPlayer();
                return;
            }

            engaged = false;
            sources.Remove(this);
            if (sources.Count != 0) return;
            if (appliedDriver != null && appliedDriver == RgbGlitchSceneDriver.Active)
                appliedDriver.EndBossFear(1f);
            appliedDriver = null;
        }

        private static void ApplyToCurrentPlayer()
        {
            RgbGlitchSceneDriver current = RgbGlitchSceneDriver.Active;
            if (current == null || current == appliedDriver) return;
            appliedDriver = current;
            current.BeginBossFear(1f);
        }

        private void OnDisable()
        {
            if (executor != null) executor.MotionStateChanged -= HandleMotionChanged;
            if (engaged) HandleMotionChanged(false);
            executor = null;
        }
    }
}
