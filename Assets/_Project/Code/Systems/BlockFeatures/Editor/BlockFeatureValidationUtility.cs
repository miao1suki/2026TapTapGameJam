using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    public enum BlockFeatureValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class BlockFeatureValidationIssue
    {
        internal BlockFeatureValidationIssue(
            BlockFeatureValidationSeverity severity,
            string message)
        {
            Severity = severity;
            Message = message;
        }

        public BlockFeatureValidationSeverity Severity { get; }
        public string Message { get; }
    }

    public sealed class BlockFeatureValidationReport
    {
        private readonly List<BlockFeatureValidationIssue> issues =
            new List<BlockFeatureValidationIssue>();

        public IReadOnlyList<BlockFeatureValidationIssue> Issues => issues;
        public bool HasErrors
        {
            get
            {
                for (int index = 0; index < issues.Count; index++)
                {
                    if (issues[index].Severity ==
                        BlockFeatureValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public string GetSummary()
        {
            if (issues.Count == 0)
            {
                return "组件功能配置有效。";
            }

            var builder = new System.Text.StringBuilder();
            for (int index = 0; index < issues.Count; index++)
            {
                BlockFeatureValidationIssue issue = issues[index];
                builder.Append(issue.Severity ==
                               BlockFeatureValidationSeverity.Error
                    ? "[错误] "
                    : "[警告] ");
                builder.AppendLine(issue.Message);
            }

            return builder.ToString();
        }

        internal void AddError(string message)
        {
            issues.Add(new BlockFeatureValidationIssue(
                BlockFeatureValidationSeverity.Error,
                message));
        }

        internal void AddWarning(string message)
        {
            issues.Add(new BlockFeatureValidationIssue(
                BlockFeatureValidationSeverity.Warning,
                message));
        }
    }

    public static class BlockFeatureValidationUtility
    {
        public static BlockFeatureValidationReport Validate(
            GameObject root)
        {
            var report = new BlockFeatureValidationReport();
            if (root == null)
            {
                report.AddError("没有可校验的方块对象。");
                return report;
            }

            IReadOnlyList<BlockFeature> allFeatures =
                ProjectDiscovery.GetComponents<BlockFeature>(root);
            var features = new List<BlockFeature>();
            for (int index = 0;
                 index < allFeatures.Count;
                 index++)
            {
                if (allFeatures[index] != null &&
                    allFeatures[index].enabled)
                {
                    features.Add(allFeatures[index]);
                }
            }

            if (features.Count == 0)
            {
                return report;
            }

            if (root.GetComponent<BlockRuntime>() == null)
            {
                report.AddError(
                    "存在功能组件，但方块缺少 BlockRuntime。");
                return report;
            }

            var counts = new Dictionary<Type, int>();
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                Type type = feature.GetType();
                counts.TryGetValue(type, out int count);
                counts[type] = count + 1;

                BlockFeatureMetadata metadata =
                    BlockFeatureMetadataCache.Get(type);
                if (string.IsNullOrWhiteSpace(metadata.DisplayName))
                {
                    report.AddError(
                        $"{type.Name} 缺少中文 DisplayName。");
                }

                if (string.IsNullOrWhiteSpace(
                        metadata.DefaultColorId))
                {
                    report.AddError(
                        $"{type.Name} 缺少 DefaultColorId。");
                }

                if (count + 1 > metadata.MaxPerBlock)
                {
                    report.AddError(
                        $"{type.Name} 超过允许数量 " +
                        $"{metadata.MaxPerBlock}。");
                }

                ValidateRequirements(
                    report,
                    features,
                    metadata);
                ValidateConflicts(
                    report,
                    features,
                    metadata);
            }

            return report;
        }

        private static void ValidateRequirements(
            BlockFeatureValidationReport report,
            IReadOnlyList<BlockFeature> features,
            BlockFeatureMetadata metadata)
        {
            for (int index = 0; index < metadata.Requires.Count; index++)
            {
                Type required = metadata.Requires[index];
                if (required == null || !required.IsInterface)
                {
                    report.AddError(
                        $"{metadata.FeatureType.Name} 的 Requires " +
                        "必须声明能力接口。");
                    continue;
                }

                if (!HasFeatureImplementing(features, required))
                {
                    report.AddError(
                        $"{metadata.FeatureType.Name} 缺少依赖能力 " +
                        $"{required.Name}。");
                }
            }
        }

        private static void ValidateConflicts(
            BlockFeatureValidationReport report,
            IReadOnlyList<BlockFeature> features,
            BlockFeatureMetadata metadata)
        {
            for (int index = 0; index < metadata.Conflicts.Count; index++)
            {
                Type conflict = metadata.Conflicts[index];
                if (conflict != null &&
                    HasFeatureImplementing(features, conflict))
                {
                    report.AddError(
                        $"{metadata.FeatureType.Name} 与 " +
                        $"{conflict.Name} 冲突。");
                }
            }
        }

        private static bool HasFeatureImplementing(
            IReadOnlyList<BlockFeature> features,
            Type capability)
        {
            for (int index = 0; index < features.Count; index++)
            {
                if (capability.IsInstanceOfType(features[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
