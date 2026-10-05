using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project
{
    /// <summary>
    /// 让组件以多态方式声明自己的发现键、标签和排序。
    /// </summary>
    public interface IProjectDiscoverySource
    {
        string DiscoveryId { get; }
        IReadOnlyList<string> DiscoveryTags { get; }
        int DiscoveryOrder { get; }
    }

    /// <summary>
    /// 项目级统一发现入口，避免各模块重复扫描场景、类型和组件。
    /// </summary>
    public static class ProjectDiscovery
    {
        public static IReadOnlyList<T> FindAll<T>(
            bool includeInactive = false)
            where T : Component
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(
                includeInactive
                    ? FindObjectsInactive.Include
                    : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            return found;
        }

        public static T FindFirst<T>(
            bool includeInactive = false)
            where T : Component
        {
            return UnityEngine.Object.FindFirstObjectByType<T>(
                includeInactive
                    ? FindObjectsInactive.Include
                    : FindObjectsInactive.Exclude);
        }

        public static T FindFirstInterface<T>(
            bool includeInactive = false)
            where T : class
        {
            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    includeInactive
                        ? FindObjectsInactive.Include
                        : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is T candidate)
                {
                    return candidate;
                }
            }

            return null;
        }

        public static Component FindFirst(
            Type type,
            bool includeInactive = false)
        {
            if (type == null ||
                !typeof(Component).IsAssignableFrom(type))
            {
                return null;
            }

            return UnityEngine.Object.FindFirstObjectByType(
                type,
                includeInactive
                    ? FindObjectsInactive.Include
                    : FindObjectsInactive.Exclude) as Component;
        }

        public static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            System.Reflection.Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type found = assemblies[index].GetType(
                    typeName,
                    false);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static IReadOnlyList<T> FindTagged<T>(
            string tag,
            bool includeInactive = false)
            where T : Component
        {
            var result = new List<T>();
            if (string.IsNullOrWhiteSpace(tag))
            {
                return result;
            }

            IReadOnlyList<T> found = FindAll<T>(includeInactive);
            for (int index = 0; index < found.Count; index++)
            {
                if (HasTag(found[index], tag))
                {
                    result.Add(found[index]);
                }
            }

            result.Sort(Compare);
            return result;
        }

        public static T FindFirstTagged<T>(
            string tag,
            bool includeInactive = false)
            where T : Component
        {
            IReadOnlyList<T> found = FindTagged<T>(
                tag,
                includeInactive);
            return found.Count > 0 ? found[0] : null;
        }

        public static IReadOnlyList<T> GetComponents<T>(
            GameObject root,
            bool includeChildren = false,
            bool includeInactive = false)
            where T : class
        {
            var result = new List<T>();
            if (root == null)
            {
                return result;
            }

            Component[] components = includeChildren
                ? root.GetComponentsInChildren<Component>(
                    includeInactive)
                : root.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is T candidate)
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        public static bool HasTag(
            Component source,
            string tag)
        {
            if (source is not IProjectDiscoverySource discoverySource ||
                string.IsNullOrWhiteSpace(tag) ||
                discoverySource.DiscoveryTags == null)
            {
                return false;
            }

            IReadOnlyList<string> tags =
                discoverySource.DiscoveryTags;
            for (int index = 0; index < tags.Count; index++)
            {
                if (string.Equals(
                        tags[index],
                        tag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public static IReadOnlyList<Type> FindImplementations<T>(
            bool includeAbstract = false)
        {
            var result = new List<Type>();
            foreach (Type type in UnityEditor.TypeCache
                         .GetTypesDerivedFrom<T>())
            {
                if (type == null ||
                    (!includeAbstract && type.IsAbstract) ||
                    (!includeAbstract && type.IsInterface))
                {
                    continue;
                }

                result.Add(type);
            }

            result.Sort((left, right) => string.CompareOrdinal(
                left.FullName,
                right.FullName));
            return result;
        }
#endif

        private static int Compare<T>(T left, T right)
            where T : Component
        {
            return CompareComponents(left, right);
        }

        private static int CompareComponents(
            Component left,
            Component right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int leftOrder = left is IProjectDiscoverySource leftSource
                ? leftSource.DiscoveryOrder
                : 0;
            int rightOrder = right is IProjectDiscoverySource rightSource
                ? rightSource.DiscoveryOrder
                : 0;
            int order = leftOrder.CompareTo(rightOrder);
            if (order != 0)
            {
                return order;
            }

            int name = string.CompareOrdinal(
                left.name,
                right.name);
            return name != 0
                ? name
                : left.GetInstanceID().CompareTo(
                    right.GetInstanceID());
        }
    }
}
