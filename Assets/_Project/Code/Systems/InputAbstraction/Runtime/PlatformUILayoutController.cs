using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.InputAbstraction
{
    [Serializable]
    public struct RectTransformLayout
    {
        [SerializeField] private Vector2 anchorMin;
        [SerializeField] private Vector2 anchorMax;
        [SerializeField] private Vector2 anchoredPosition;
        [SerializeField] private Vector2 sizeDelta;
        [SerializeField] private Vector2 pivot;
        [SerializeField] private Vector3 localScale;
        [SerializeField] private Quaternion localRotation;

        public static RectTransformLayout Capture(RectTransform target)
        {
            return new RectTransformLayout
            {
                anchorMin = target.anchorMin,
                anchorMax = target.anchorMax,
                anchoredPosition = target.anchoredPosition,
                sizeDelta = target.sizeDelta,
                pivot = target.pivot,
                localScale = target.localScale,
                localRotation = target.localRotation,
            };
        }

        public void Apply(RectTransform target)
        {
            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.anchoredPosition = anchoredPosition;
            target.sizeDelta = sizeDelta;
            target.pivot = pivot;
            target.localScale = localScale;
            target.localRotation = localRotation;
        }
    }

    [Serializable]
    public sealed class PlatformUILayoutBinding
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private bool hasDesktopLayout;
        [SerializeField] private RectTransformLayout desktopLayout;
        [SerializeField] private bool hasMobileLayout;
        [SerializeField] private RectTransformLayout mobileLayout;

        public RectTransform Target => target;

        public PlatformUILayoutBinding(RectTransform layoutTarget)
        {
            target = layoutTarget;
            if (target != null)
            {
                desktopLayout = RectTransformLayout.Capture(target);
                mobileLayout = desktopLayout;
                hasDesktopLayout = true;
                hasMobileLayout = true;
            }
        }

        public void Capture(InputPlatformMode mode)
        {
            if (target == null)
            {
                return;
            }

            if (mode == InputPlatformMode.Mobile)
            {
                mobileLayout = RectTransformLayout.Capture(target);
                hasMobileLayout = true;
            }
            else
            {
                desktopLayout = RectTransformLayout.Capture(target);
                hasDesktopLayout = true;
            }
        }

        public void Apply(InputPlatformMode mode)
        {
            if (target == null)
            {
                return;
            }

            if (mode == InputPlatformMode.Mobile && hasMobileLayout)
            {
                mobileLayout.Apply(target);
            }
            else if (mode == InputPlatformMode.Desktop && hasDesktopLayout)
            {
                desktopLayout.Apply(target);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlatformUILayoutController : MonoBehaviour
    {
        [SerializeField]
        private bool applyOnAwake = true;

        [SerializeField]
        private bool synchronizeWhenBuildTargetChanges = true;

        [SerializeField]
        private List<PlatformUILayoutBinding> layoutBindings =
            new List<PlatformUILayoutBinding>();

        [SerializeField]
        private List<GameObject> desktopOnlyObjects = new List<GameObject>();

        [SerializeField]
        private List<GameObject> mobileOnlyObjects = new List<GameObject>();

        public bool SynchronizeWhenBuildTargetChanges => synchronizeWhenBuildTargetChanges;
        public IReadOnlyList<PlatformUILayoutBinding> LayoutBindings => layoutBindings;
        public IReadOnlyList<GameObject> DesktopOnlyObjects => desktopOnlyObjects;
        public IReadOnlyList<GameObject> MobileOnlyObjects => mobileOnlyObjects;

        public void CaptureLayout(InputPlatformMode mode)
        {
            mode = Normalize(mode);
            for (int index = 0; index < layoutBindings.Count; index++)
            {
                layoutBindings[index]?.Capture(mode);
            }
        }

        public void ApplyLayout(InputPlatformMode mode)
        {
            mode = Normalize(mode);
            for (int index = 0; index < layoutBindings.Count; index++)
            {
                layoutBindings[index]?.Apply(mode);
            }

            SetObjectsActive(desktopOnlyObjects, mode == InputPlatformMode.Desktop);
            SetObjectsActive(mobileOnlyObjects, mode == InputPlatformMode.Mobile);
        }

        public void ReplaceLayoutTargets(IEnumerable<RectTransform> targets)
        {
            Dictionary<RectTransform, PlatformUILayoutBinding> existing =
                new Dictionary<RectTransform, PlatformUILayoutBinding>();
            for (int index = 0; index < layoutBindings.Count; index++)
            {
                PlatformUILayoutBinding binding = layoutBindings[index];
                if (binding?.Target != null && !existing.ContainsKey(binding.Target))
                {
                    existing.Add(binding.Target, binding);
                }
            }

            layoutBindings.Clear();
            foreach (RectTransform target in targets)
            {
                if (target == null || target == transform || ContainsTarget(target))
                {
                    continue;
                }

                layoutBindings.Add(existing.TryGetValue(target, out PlatformUILayoutBinding binding)
                    ? binding
                    : new PlatformUILayoutBinding(target));
            }
        }

        public void SetVisibilityGroups(
            IEnumerable<GameObject> desktopObjects,
            IEnumerable<GameObject> mobileObjects)
        {
            desktopOnlyObjects.Clear();
            mobileOnlyObjects.Clear();
            AddUnique(desktopOnlyObjects, desktopObjects);
            AddUnique(mobileOnlyObjects, mobileObjects);
        }

        private void Awake()
        {
            if (applyOnAwake && Application.isPlaying)
            {
                ApplyLayout(InputPlatformResolver.Current);
            }
        }

        private bool ContainsTarget(RectTransform candidate)
        {
            for (int index = 0; index < layoutBindings.Count; index++)
            {
                if (layoutBindings[index]?.Target == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static InputPlatformMode Normalize(InputPlatformMode mode)
        {
            return mode == InputPlatformMode.Automatic
                ? InputPlatformResolver.Current
                : mode;
        }

        private static void SetObjectsActive(List<GameObject> objects, bool active)
        {
            for (int index = 0; index < objects.Count; index++)
            {
                if (objects[index] != null && objects[index].activeSelf != active)
                {
                    objects[index].SetActive(active);
                }
            }
        }

        private static void AddUnique(List<GameObject> destination, IEnumerable<GameObject> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (GameObject value in source)
            {
                if (value != null && !destination.Contains(value))
                {
                    destination.Add(value);
                }
            }
        }
    }
}
