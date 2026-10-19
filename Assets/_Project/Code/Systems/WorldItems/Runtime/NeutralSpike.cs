using Project.ColorBlocks;
using Project.LevelEditor;
using Project.Player;
using UnityEngine;

namespace Project.WorldItems
{
    [DisallowMultipleComponent]
    public sealed class NeutralSpike : MonoBehaviour, IRoomColorResettable
    {
        private MeshCollider sourceVolume;
        private Mesh tiledContactMesh;

        private void Awake() => RefreshContacts();
        private void OnEnable() => RefreshContacts();
        private void Start() => RefreshContacts();
        public void ResetForRoom() => RefreshContacts();

        private void RefreshContacts()
        {
            ConfigureMergedContacts();
            BindChildContacts();
        }

        private void ConfigureMergedContacts()
        {
            if (tiledContactMesh != null)
            {
                // RoomResetObject restores authored collider states before this hook.
                if (sourceVolume != null) sourceVolume.enabled = false;
                return;
            }
            var placed = GetComponent<LevelEditorPlacedBlock>();
            var shape = GetComponent<LevelEditorMergedShape>();
            Vector2Int size = shape != null ? shape.Dimensions : placed != null ? placed.SizeCells : Vector2Int.one;
            if (size.x < 1 || size.y < 1 || size == Vector2Int.one) return;
            sourceVolume = GetComponentInChildren<MeshCollider>(true);
            if (sourceVolume == null || !sourceVolume.enabled || sourceVolume.sharedMesh == null) return;

            // Scaling a whole row into one giant triangle leaves its outer cells
            // without a trigger. Repeat the original convex prism per occupied cell.
            // Bake rotation before compensating nonuniform root scale (left/right).
            var vertices = sourceVolume.sharedMesh.vertices;
            Matrix4x4 toRoot = transform.worldToLocalMatrix * sourceVolume.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = toRoot.MultiplyPoint3x4(vertices[i]);
                vertices[i] = new Vector3(point.x / size.x, point.y / size.y, point.z);
            }
            tiledContactMesh = new Mesh { name = "Spike Cell Contact", hideFlags = HideFlags.DontSave };
            tiledContactMesh.vertices = vertices;
            tiledContactMesh.triangles = sourceVolume.sharedMesh.triangles;
            tiledContactMesh.RecalculateBounds();
            if (shape != null)
            {
                foreach (var rect in shape.Rectangles)
                    foreach (var cell in rect.allPositionsWithin) AddCellContact(cell, size);
            }
            else
                foreach (var cell in new RectInt(Vector2Int.zero, size).allPositionsWithin) AddCellContact(cell, size);
            sourceVolume.enabled = false;
        }

        private void AddCellContact(Vector2Int cell, Vector2Int size)
        {
            var child = new GameObject("__SpikeContact_" + cell.x + "_" + cell.y) { hideFlags = HideFlags.DontSave };
            child.layer = sourceVolume.gameObject.layer;
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3((cell.x + .5f) / size.x - .5f, .5f - (cell.y + .5f) / size.y, 0f);
            var volume = child.AddComponent<MeshCollider>();
            volume.sharedMesh = tiledContactMesh; volume.convex = true; volume.isTrigger = true;
        }

        private void OnDestroy()
        {
            if (tiledContactMesh != null) Destroy(tiledContactMesh);
        }

        private void BindChildContacts()
        {
            // Static child colliders do not send physics messages to an arbitrary
            // parent MonoBehaviour. Keep their exact shapes and forward locally;
            // no extra Rigidbody or per-frame overlap scan is needed.
            foreach (var shape in GetComponentsInChildren<Collider>(true))
            {
                if (shape.gameObject == gameObject || shape.GetComponentInParent<NeutralSpike>() != this) continue;
                var relay = shape.GetComponent<SpikeContactRelay>();
                if (relay == null) relay = shape.gameObject.AddComponent<SpikeContactRelay>();
                relay.Bind(this);
            }
        }

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);
        private void OnCollisionEnter(Collision collision) => Hit(collision.collider);
        private void OnCollisionStay(Collision collision) => Hit(collision.collider);

        internal void Hit(Collider other)
        {
            if (!isActiveAndEnabled || other == null) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && !player.IsDead && !player.IsInvulnerable)
                player.EnterDeath(PlayerDeathCause.Spike, this);
        }
    }
}
