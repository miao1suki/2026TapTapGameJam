using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Project.Privacy;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.GameFlow.Editor
{
    /// <summary>Read-only source audit. Creates disposable font assets only in the isolated probe.</summary>
    public static class PrivacyFontCapacityAudit
    {
        private const string FontGuid = "97e10da648522f547bbec66357199a2a";

        [Serializable]
        private sealed class CapacityResult
        {
            public int atlasSize, commonUnique, commonMissingCount, policyUnique, policyMissingCount, atlasCount, cachedMissingCount;
            public bool commonComplete, policyComplete;
            public string commonMissing, commonMissingCodepoints, policyMissing, policyMissingCodepoints, cachedMissing;
            public string[] commonMissingOrigins;
        }

        [Serializable]
        private sealed class TextRequirement
        {
            public string source, text;
        }

        [Serializable]
        private sealed class UnsupportedGlyph
        {
            public string character, unicode;
            public string[] actualUiOrigins;
        }

        [Serializable]
        private sealed class Report
        {
            public string sourcePath, sourceSha256, sourcePopulation;
            public int sourceAtlasWidth, sourceAtlasHeight, sourcePointSize, sourcePadding;
            public bool sourceMultiAtlas, sourceBytesUnchanged, privacyCapacityPassed, uiCoveragePassed, supportedUiCoveragePassed, passed;
            public string error, warning, cachedCharacters;
            public string auditGoal = "Privacy capacity after real UI warm-up; known TTF-unsupported UI glyphs remain separately reported, not fixed.";
            public int cachedUnique;
            public TextRequirement[] uiTextRequirements;
            public UnsupportedGlyph[] unsupportedActualUi;
            public CapacityResult[] results;
        }

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("PrivacyFontCapacityAudit only runs in stopped FlowRetirementProbe.");

            var report = new Report();
            int exitCode = 1;
            string path = AssetDatabase.GUIDToAssetPath(FontGuid);
            byte[] before = null;
            try
            {
                if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Project Cubic_11 font asset is missing.");
                before = File.ReadAllBytes(path);
                var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (source == null || source.sourceFontFile == null) throw new InvalidOperationException("Cubic_11 source TTF is missing.");
                report.sourcePath = path;
                report.sourceSha256 = Hash(before);
                report.sourceAtlasWidth = source.atlasWidth;
                report.sourceAtlasHeight = source.atlasHeight;
                report.sourcePopulation = source.atlasPopulationMode.ToString();
                report.sourceMultiAtlas = source.isMultiAtlasTexturesEnabled;
                report.sourcePointSize = Mathf.RoundToInt(source.faceInfo.pointSize);
                report.sourcePadding = source.atlasPadding;

                string policy = VisibleCharacters(AndroidPrivacyPolicy.PlainText(File.ReadAllText(AndroidPrivacyBuildGuard.PolicyAsset)));
                var requirements = new List<TextRequirement>();
                string common = VisibleCharacters(CommonUiText(requirements));
                string cached = VisibleCharacters(string.Concat(source.characterTable.Where(character => character.unicode <= 0x10FFFF)
                    .Select(character => char.ConvertFromUtf32((int)character.unicode))));
                report.cachedCharacters = cached;
                report.cachedUnique = Codepoints(cached).Count();
                report.uiTextRequirements = requirements.ToArray();
                if (FontEngine.LoadFontFace(source.sourceFontFile, report.sourcePointSize) != FontEngineError.Success)
                    throw new InvalidOperationException("Cannot inspect source TTF character map.");
                var unsupported = new HashSet<int>(Codepoints(common).Where(value => !FontEngine.TryGetGlyphIndex((uint)value,out uint index) || index==0));
                report.unsupportedActualUi = unsupported.Select(value => new UnsupportedGlyph
                {
                    character = char.ConvertFromUtf32(value), unicode = "U+" + value.ToString("X4"),
                    actualUiOrigins = requirements.Where(requirement => requirement.text.Contains(char.ConvertFromUtf32(value)))
                        .Select(requirement => requirement.source).ToArray()
                }).ToArray();
                report.results = new[] { Test(source, 2048, common, cached, policy, requirements), Test(source, 4096, common, cached, policy, requirements) };
                // The capacity goal cannot repair glyphs genuinely absent from the source TTF.
                // Keep strict real-UI coverage visible, while requiring every supported UI glyph plus the complete policy.
                report.privacyCapacityPassed = report.results[1].policyComplete;
                report.uiCoveragePassed = report.results[1].commonComplete;
                report.supportedUiCoveragePassed = report.uiCoveragePassed || (report.results[1].commonMissingCount > 0 &&
                    Codepoints(report.results[1].commonMissing).All(unsupported.Contains));
                report.passed = report.supportedUiCoveragePassed && report.privacyCapacityPassed;
                if (!report.privacyCapacityPassed) report.error = "4096 did not cover the complete privacy policy after UI warm-up.";
                else if (!report.supportedUiCoveragePassed) report.error = "4096 still lost source-supported UI glyphs; see commonMissingOrigins.";
                if (!report.uiCoveragePassed) report.warning = "Real UI still contains missing glyphs. Confirm or correct the credited nickname; see unsupportedActualUi and commonMissingOrigins. This audit does not certify all UI text.";
                exitCode = report.passed ? 0 : 1;
            }
            catch (Exception error)
            {
                report.error = error.ToString();
                Debug.LogException(error);
            }
            finally
            {
                report.sourceBytesUnchanged = before != null && File.Exists(path) && before.SequenceEqual(File.ReadAllBytes(path));
                if (!report.sourceBytesUnchanged) { report.passed = false; exitCode = 1; }
                string output = Path.Combine("Library", "PrivacyFontCapacityAudit.json");
                File.WriteAllText(output, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("[PrivacyFontCapacityAudit] " + (report.passed ? "PASS" : "FAIL") + "; report=" + Path.GetFullPath(output));
                EditorApplication.Exit(exitCode);
            }
        }

        private static CapacityResult Test(TMP_FontAsset source, int size, string common, string cached, string policy, List<TextRequirement> requirements)
        {
            // Do not Instantiate(source): its atlas/material subassets would still point at the source asset.
            var scratch = TMP_FontAsset.CreateFontAsset(source.sourceFontFile, Mathf.RoundToInt(source.faceInfo.pointSize),
                source.atlasPadding, source.atlasRenderMode, size, size, AtlasPopulationMode.Dynamic, false);
            if (scratch == null) throw new InvalidOperationException("Cannot create disposable Cubic_11 font at " + size);
            scratch.name = "Disposable privacy capacity " + size;
            scratch.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                bool warm = scratch.TryAddCharacters(common, out string commonMissing);
                // Existing cache is additional atlas pressure, not a requirement merely because it was cached.
                scratch.TryAddCharacters(cached, out string cachedMissing);
                bool complete = scratch.TryAddCharacters(policy, out string policyMissing);
                return new CapacityResult
                {
                    atlasSize = size,
                    commonUnique = Codepoints(common).Distinct().Count(),
                    commonComplete = warm,
                    commonMissing = commonMissing ?? "",
                    commonMissingCodepoints = Describe(commonMissing ?? ""),
                    commonMissingCount = Codepoints(commonMissing ?? "").Distinct().Count(),
                    commonMissingOrigins = requirements.Where(requirement => Codepoints(commonMissing ?? "").Any(value =>
                        requirement.text.Contains(char.ConvertFromUtf32(value)))).Select(requirement => requirement.source).ToArray(),
                    cachedMissing = cachedMissing ?? "",
                    cachedMissingCount = Codepoints(cachedMissing ?? "").Distinct().Count(),
                    policyUnique = Codepoints(policy).Distinct().Count(),
                    policyComplete = complete,
                    policyMissing = policyMissing ?? "",
                    policyMissingCount = Codepoints(policyMissing ?? "").Distinct().Count(),
                    policyMissingCodepoints = Describe(policyMissing ?? ""),
                    atlasCount = scratch.atlasTextureCount
                };
            }
            finally
            {
                var textures = scratch.atlasTextures.ToArray();
                Material material = scratch.material;
                foreach (Texture2D texture in textures)
                    if (texture != null && !EditorUtility.IsPersistent(texture)) Object.DestroyImmediate(texture);
                if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
                Object.DestroyImmediate(scratch);
            }
        }

        private static string CommonUiText(List<TextRequirement> requirements)
        {
            var builder = new StringBuilder();
            void Add(string source, string text)
            {
                requirements.Add(new TextRequirement { source = source, text = text ?? "" });
                builder.AppendLine(text);
            }
            Add("Common dynamically generated UI labels", "开始游戏 制作名单 设置 退出 暂停 继续游戏 返回主菜单 同意 不同意 隐私政策 触控布局 保存 取消 恢复默认 操作 音量 画面 按键映射");
            foreach (string path in new[] { "Assets/_Project/Content/UI/SharedGameUI.prefab", "Assets/_Project/Resources/UI/WorldMainMenu.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Common UI prefab missing: " + path);
                foreach (Text text in prefab.GetComponentsInChildren<Text>(true)) Add(path + " :: " + TransformPath(text.transform), text.text);
                foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true)) Add(path + " :: " + TransformPath(text.transform), text.text);
            }
            return builder.ToString();
        }

        private static string TransformPath(Transform transform)
        {
            var names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent) names.Push(current.name);
            return string.Join("/", names);
        }

        private static string Describe(string text) => string.Join(" ", Codepoints(text).Distinct().Select(value => "U+" + value.ToString("X4")));

        private static IEnumerable<int> Codepoints(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                    yield return char.ConvertToUtf32(text[index], text[++index]);
                else yield return text[index];
            }
        }

        private static string VisibleCharacters(string text)
        {
            // Newline/tab/space are layout rather than visible glyph coverage. TMP's batch font API
            // may report control characters as missing even though the text layout handles them.
            return string.Concat(Codepoints(text).Where(value => !char.IsWhiteSpace(char.ConvertFromUtf32(value), 0) &&
                !char.IsControl(char.ConvertFromUtf32(value), 0)).Distinct().Select(char.ConvertFromUtf32));
        }

        private static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
