using System.IO;
using Project.Items;
using Project.LevelEditor;
using Project.Pickups;
using Project.Rooms;
using UnityEditor;
using UnityEngine;

namespace Project.WorldItems.Editor
{
    public static class WorldItemSetup
    {
        public const string Folder = "Assets/_Project/Content/WorldItems";
        private const string PalettePath = "Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset";

        [MenuItem("Tools/2026TapTap/关卡/补齐收集品与四向尖刺")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Neutral.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new Color(.86f, .88f, .91f));
                AssetDatabase.CreateAsset(material, Folder + "/Neutral.mat");
            }
            CreateCollectible(material);
            string[] names = { "上", "下", "左", "右" };
            float[] angles = { 0f, 180f, 90f, -90f };
            for (int i = 0; i < names.Length; i++) CreateSpike(names[i], angles[i], material);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Project.LevelEditor.Editor.LevelEditorState.MarkPaletteChanged();
        }

        private static void Register(string label, GameObject prefab)
        {
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(PalettePath);
            if (palette == null) throw new System.InvalidOperationException("关卡道具库不存在");
            foreach (var entry in palette.PropEntries) if (entry.Prefab == prefab) return;
            var item = new LevelEditorPropEntry(); item.Configure(label, prefab); palette.AddProp(item);
            EditorUtility.SetDirty(palette);
        }

        private static void CreateCollectible(Material material)
        {
            string path = Folder + "/Collectible.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) { Register("收集品", existing); return; }
            var root = new GameObject("Collectible");
            try
            {
                root.SetActive(false);
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "外观"; visual.transform.SetParent(root.transform, false); visual.transform.localScale = Vector3.one * .55f;
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = material;
                var solid = root.AddComponent<SphereCollider>(); solid.radius = .275f;
                var body = root.AddComponent<Rigidbody>(); body.mass = .25f; body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var trigger = new GameObject("拾取区域"); trigger.transform.SetParent(root.transform, false);
                var pickupShape = trigger.AddComponent<SphereCollider>(); pickupShape.radius = .34f; pickupShape.isTrigger = true;
                root.AddComponent<RoomResetObject>(); root.AddComponent<CollectiblePickup>(); root.AddComponent<PickupFlightAnimator>();
                root.SetActive(true);
                Register("收集品", PrefabUtility.SaveAsPrefabAsset(root, path));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateSpike(string name, float angle, Material material)
        {
            string path = Folder + "/Spike_" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) { Register("尖刺·" + name, existing); return; }
            var root = new GameObject("Spike_" + name);
            try
            {
                root.AddComponent<NeutralSpike>();
                var tooth = new GameObject("尖刺区域"); tooth.transform.SetParent(root.transform, false);
                tooth.transform.localRotation = Quaternion.Euler(0, 0, angle);
                // Triangular prism: precise convex trigger, no invisible lethal corners of a box.
                Mesh mesh = new Mesh { name = "SpikePrism" };
                mesh.vertices = new[] { new Vector3(-.45f,-.5f,-.15f),new Vector3(.45f,-.5f,-.15f),new Vector3(0,.25f,-.15f),
                    new Vector3(-.45f,-.5f,.15f),new Vector3(.45f,-.5f,.15f),new Vector3(0,.25f,.15f) };
                mesh.triangles = new[] { 0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5 };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string meshPath = Folder + "/Spike_" + name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) is Mesh saved) { Object.DestroyImmediate(mesh); mesh = saved; }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                tooth.AddComponent<MeshFilter>().sharedMesh = mesh;
                tooth.AddComponent<MeshRenderer>().sharedMaterial = material;
                var trigger = tooth.AddComponent<MeshCollider>(); trigger.sharedMesh = mesh; trigger.convex = true; trigger.isTrigger = true;
                Register("尖刺·" + name, PrefabUtility.SaveAsPrefabAsset(root, path));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
