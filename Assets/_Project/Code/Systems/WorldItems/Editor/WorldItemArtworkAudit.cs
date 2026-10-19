using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.ObjectArtwork;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.WorldItems.Editor
{
    public static class WorldItemArtworkAudit
    {
        private const string Flag = "WorldItemArtworkAudit.Running";
        private static int checks;
        private static double started;
        private static void Guard()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                !Environment.GetCommandLineArgs().Contains("-noaudio"))
                throw new InvalidOperationException("只能在静音FlowRetirementProbe隔离工程运行。");
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("WORLD ITEM ARTWORK: " + message);
            checks++; Debug.Log("WORLD ITEM ARTWORK PASS: " + message);
        }
        public static void BuildAndVerify()
        {
            Guard(); checks = 0; WorldItemArtworkSetup.Build(); VerifyAssets();
            File.WriteAllText("Library/WorldItemArtworkAssets.txt", "PASS " + checks);
        }
        private static void VerifyAssets()
        {
            var library = ObjectArtworkLibrary.Load();
            Check(library != null && library.neutralMeshMaterial != null, "shared neutral mesh material assigned");
            Check(library.neutralMeshMaterial.IsKeywordEnabled("_ALPHATEST_ON") &&
                library.neutralMeshMaterial.GetFloat("_AlphaClip") == 1f &&
                library.neutralMeshMaterial.GetFloat("_Cull") == 0f,
                "batched spike alpha pixels are cut out on both faces, never opaque cells");
            var importer = (TextureImporter)AssetImporter.GetAtPath(WorldItemArtworkSetup.CollectibleSource);
            Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.maxTextureSize == 128 &&
                importer.npotScale == TextureImporterNPOTScale.None && importer.textureCompression == TextureImporterCompression.Uncompressed,
                "collectible lossless point import, source preserved");
            Sprite collectible = library.FirstFrame(MechanismArtworkKind.Collectible);
            Check(collectible != null && collectible.rect == new Rect(0, 0, 32, 32) && collectible.pixelsPerUnit == 16,
                "collectible complete 32px frame, natural 2 cells, no sprite squeeze");
            foreach (var kind in new[] { MechanismArtworkKind.Spike, MechanismArtworkKind.BloodySpike,
                MechanismArtworkKind.LargeSpike, MechanismArtworkKind.BloodyLargeSpike })
            {
                Sprite sprite = library.FirstFrame(kind);
                Check(sprite != null && sprite.rect.size == new Vector2(16, 16) && sprite.pixelsPerUnit == 16, "native 16px spike frame " + kind);
            }
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            for (int size = 0; size < 2; size++) for (int blood = 0; blood < 2; blood++)
            {
                string id = "world-spike-" + (size == 0 ? "small" : "large") + (blood == 0 ? "" : "-bloody");
                var entry = palette.PropEntries.Single(item => item.EntryId == id);
                Check(entry.ComponentValueOverrides.Any(value => value.PropertyPath == "size" && value.Value == size.ToString()) &&
                    entry.ComponentValueOverrides.Any(value => value.PropertyPath == "initiallyBloody" && value.Value == blood.ToString()), "persisted variant defaults " + id);
                Check(ObjectArtworkPlanningPreview.TryGet(entry.Prefab, entry.ComponentValueOverrides, out var preview) &&
                    preview.Sprite == library.FirstFrame(ObjectArtworkLibrary.SpikeKind(size == 1, blood == 1)), "palette resolves actual 2D variant " + id);
            }
            var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/WorldItems/Collectible.prefab");
            Check(ObjectArtworkPlanningPreview.TryGet(itemPrefab, out var itemPreview) && itemPreview.Sprite == collectible && itemPreview.Cells == Vector2.one * 2,
                "collectible 2D planning uses natural 2x2 frame");
            string spriteId = GlobalObjectId.GetGlobalObjectIdSlow(collectible).ToString();
            WorldItemArtworkSetup.Build();
            Check(GlobalObjectId.GetGlobalObjectIdSlow(ObjectArtworkLibrary.Load().FirstFrame(MechanismArtworkKind.Collectible)).ToString() == spriteId,
                "rebuild preserves collectible SpriteID");
        }
        public static void Run()
        {
            Guard(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            started = EditorApplication.timeSinceStartup; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
            EditorApplication.update -= Tick; var simulation = Physics.simulationMode;
            try
            {
                AudioListener.volume = 0f; AudioListener.pause = true; Physics.simulationMode = SimulationMode.Script; checks = 0;
                foreach (NeutralSpike.SpikeSize size in Enum.GetValues(typeof(NeutralSpike.SpikeSize)))
                    foreach (var facing in new[] { NeutralSpike.SpikeFacing.Up, NeutralSpike.SpikeFacing.Down,
                        NeutralSpike.SpikeFacing.Left, NeutralSpike.SpikeFacing.Right }) VerifyKillAndBlood(size, facing);
                Finish(true, "PASS " + checks + " real trigger / per-cell blood / reset / native sprite checks");
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
            finally { Physics.simulationMode = simulation; }
        }
        private static void VerifyKillAndBlood(NeutralSpike.SpikeSize size, NeutralSpike.SpikeFacing facing)
        {
            var staging = new GameObject("Inactive spike staging"); staging.SetActive(false);
            var spikeObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/WorldItems/Spike_上.prefab"),
                new Vector3(100, 20, 0), Quaternion.identity, staging.transform);
            var spike = spikeObject.GetComponent<NeutralSpike>(); spike.Configure(size, false, facing);
            spikeObject.transform.localScale = new Vector3(3, 1, 1);
            var placed = spikeObject.AddComponent<LevelEditorPlacedBlock>();
            placed.Configure(Vector2Int.zero, "测试尖刺", Color.white, false, true, 1); placed.SetSizeCells(new Vector2Int(3, 1));
            var reset = spikeObject.AddComponent<RoomResetObject>();
            spikeObject.transform.SetParent(null, true); Object.DestroyImmediate(staging);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"),
                spikeObject.transform.position + Vector3.back * 4, Quaternion.identity).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false; player.Motor.useGravity = false; player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            var protection = typeof(PlayerController).GetField("invulnerableUntil", BindingFlags.Instance | BindingFlags.NonPublic);
            protection.SetValue(player, Time.time + 100);
            void Step(Vector3 position)
            {
                player.Motor.position = position; player.transform.position = position; player.Motor.WakeUp(); Physics.SyncTransforms();
                for (int i = 0; i < 3; i++) Physics.Simulate(.02f);
            }
            Step(spikeObject.transform.position + Vector3.right);
            Check(!player.IsDead && spike.BloodyCellCount == 0, "protected touch does not stain " + size + "/" + facing);
            protection.SetValue(player, 0f); Step(spikeObject.transform.position + Vector3.right);
            Check(player.IsDead && spike.BloodyCellCount == 1 && spike.IsCellBloody(new Vector2Int(2, 0)) &&
                !spike.IsCellBloody(Vector2Int.zero), "real callback stains only contacted merged cell " + size + "/" + facing);
            var presentation = MechanismArtworkPresentation.Ensure(spikeObject); presentation.Refresh(0f);
            var mesh = spikeObject.transform.Find("__SpikeArtwork").GetComponent<MeshFilter>().sharedMesh;
            Check(mesh != null && mesh.vertexCount == 12, "single batched mesh retains three cells " + size + "/" + facing);
            float cleanU = 368f / 447f, bloodU = 384f / 447f;
            Check(mesh.uv.Where((value, index) => index % 4 == 0).Count(value => Mathf.Abs(value.x - bloodU) < .00001f) == 1 &&
                mesh.uv.Where((value, index) => index % 4 == 0).Count(value => Mathf.Abs(value.x - cleanU) < .00001f) == 2, "only one quad selects bloody UV " + size + "/" + facing);
            reset.RestorePose(); reset.RestoreState(); spike.ResetForRoom();
            Check(spike.BloodyCellCount == 1, "R/room reset preserves session blood " + size + "/" + facing);
            spikeObject.SetActive(false); spikeObject.SetActive(true);
            Check(spike.BloodyCellCount == 1, "disable/re-enable preserves blood " + size + "/" + facing);
            var data = new SerializedObject(spike);
            Check(!data.FindProperty("initiallyBloody").boolValue && data.FindProperty("bloodyCells") == null,
                "runtime blood never changes authored/save parameters " + size + "/" + facing);
            Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(spikeObject);
        }
        private static void Finish(bool passed, string report)
        {
            SessionState.SetBool(Flag, false); EditorApplication.update -= Tick;
            File.WriteAllText("Library/WorldItemArtworkPlay.txt", report); if (!passed) Debug.LogError(report); else Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
