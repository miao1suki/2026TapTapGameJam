using System;
using System.Collections.Generic;
using Project.ColorBlocks;
using Project.BlockFeatures;
using Project.Items;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic.Editor
{
    public static class BossEntranceRequirementsAudit
    {
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/"))
                throw new InvalidOperationException("Run this audit only in an isolated Library project.");
            int checks = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException(label);
                checks++;
            }
            foreach (int count in new[] { 0, 1, 3, 4, 5 })
                Check(!BossEntranceRequirements.CanEnter(new CollectibleProgressSnapshot(100, 100), count),
                    "Even full collectibles cannot bypass missing keys: " + count);
            Check(!BossEntranceRequirements.CanEnter(new CollectibleProgressSnapshot(29, 100), 6), "Six keys cannot bypass the 30 percent threshold");
            Check(!BossEntranceRequirements.CanEnter(new CollectibleProgressSnapshot(0, 100), 6), "Zero collectibles still reject with six keys");
            Check(BossEntranceRequirements.CanEnter(new CollectibleProgressSnapshot(30, 100), 6), "Exactly 30 percent plus six keys passes");
            Check(!BossEntranceRequirements.CanEnter(new CollectibleProgressSnapshot(10, 35), 6), "Thirty percent of 35 rounds up, ten still fails");
            Check(BossEntranceRequirements.CanEnter(new CollectibleProgressSnapshot(11, 35), 6), "Thirty percent of 35 requires eleven");
            Check(!BossEntranceRequirements.CanEnter(new CollectibleProgressSnapshot(0, 0), 6), "Missing collectible denominator never allows entry");
            string hint = BossEntranceRequirements.RequirementHint(new CollectibleProgressSnapshot(11, 35), 5);
            Check(hint.Contains("30") && hint.Contains("6把颜色钥匙") && hint.Contains("11/35") && hint.Contains("5/6"), "Hint includes both conditions and both current counts");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Test字体/[可商用]Cubic‑11 俐方体 11 号/Cubic_11 SDF.asset");
            Check(font != null, "Formal Cubic font exists");
            font.TryAddCharacters(hint, out string missing);
            Check(string.IsNullOrEmpty(missing), "Formal font covers the dynamic hint: " + missing);

            var triggerRoot = new GameObject("Rejected Once Boss entry fixture");
            var runnerRoot = new GameObject("Rejected Once Boss entry runner");
            try
            {
                triggerRoot.AddComponent<BoxCollider>().isTrigger = true;
                var director = triggerRoot.AddComponent<PlayableDirector>();
                var trigger = triggerRoot.AddComponent<ContactTimelineTrigger>();
                var runnerType = typeof(EnterBossTimelinePlayable).Assembly.GetType("Project.BossLogic.EnterBossTimelineRunner", true);
                var runner = runnerRoot.AddComponent(runnerType);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(ContactTimelineTrigger).GetField("played", flags).SetValue(trigger, true);
                runnerType.GetField("director", flags).SetValue(runner, director);
                runnerType.GetField("rejectedEntry", flags).SetValue(runner, true);
                runnerType.GetMethod("Finish", flags).Invoke(runner, null);
                Check(!trigger.HasTriggered && trigger.ContactCount == 0, "Rejected Timeline resets Once and contact bookkeeping for the next entry check");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runnerRoot);
                UnityEngine.Object.DestroyImmediate(triggerRoot);
            }

            // Exercise the real save-restore API: the two saved color sets are already the six pickup stages.
            var previous = ColorRuntimeService.Existing;
            var serviceRoot = new GameObject("Boss requirement isolated color progress");
            serviceRoot.SetActive(false);
            var service = serviceRoot.AddComponent<ColorRuntimeService>();
            var type = typeof(ColorRuntimeService);
            const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var staticInstance = type.GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            type.GetField("catalog", hidden).SetValue(service, Resources.Load<ColorCatalog>("ColorBlocks/ColorCatalog"));
            staticInstance.SetValue(null, service);
            try
            {
                foreach (var abilities in new[] { Array.Empty<string>(), new[] { "red", "green" }, new[] { "red", "green", "blue" } })
                {
                    service.RestoreRunProgress(new[] { "red", "green", "blue" }, abilities);
                    Check(BossEntranceRequirements.CollectedColorKeyCount == 3 + abilities.Length,
                        "Saved scene and ability stages restore without a new save schema");
                }
                service.RestoreRunProgress(Array.Empty<string>(), new[] { "red", "green", "blue" });
                Check(BossEntranceRequirements.CollectedColorKeyCount == 3, "Debug ability-only progress does not impersonate six keys");
                Check(PlayerThinkingTimelinePlayable.HasSufficientCollectibles(60, 100) &&
                    !PlayerThinkingTimelinePlayable.HasSufficientCollectibles(59, 100), "Ending remains 60 percent");
            }
            finally
            {
                staticInstance.SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(serviceRoot);
            }
            Debug.Log("BOSS_ENTRANCE_REQUIREMENTS_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
    }
}
