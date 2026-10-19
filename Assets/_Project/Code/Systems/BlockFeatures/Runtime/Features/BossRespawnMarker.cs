using UnityEngine;

namespace Project.BlockFeatures
{
    [DisallowMultipleComponent]
    public sealed class BossRespawnMarker : MonoBehaviour
    {
        public const string ObjectName = "Boss Respawn Marker";

        [SerializeField, HideInInspector]
        private RespawnAnchorFeature owner;

        public RespawnAnchorFeature Owner
        {
            get
            {
                if (owner == null)
                {
                    owner = GetComponentInParent<RespawnAnchorFeature>();
                }

                return owner;
            }
        }

        public bool Bind(RespawnAnchorFeature value)
        {
            bool changed = owner != value;
            owner = value;
            return SyncFromOwner() || changed;
        }

        public bool SyncFromOwner()
        {
            RespawnAnchorFeature anchor = Owner;
            if (anchor == null)
            {
                return false;
            }

            bool changed = false;
            if (name != ObjectName)
            {
                name = ObjectName;
                changed = true;
            }

            Vector3 targetPosition = new Vector3(
                anchor.BossRespawnLocalX,
                0f,
                0f);
            if (transform.localPosition != targetPosition)
            {
                transform.localPosition = targetPosition;
                changed = true;
            }

            return changed;
        }

        private void Awake()
        {
            SyncFromOwner();
        }

        private void OnEnable()
        {
            SyncFromOwner();
        }

        private void OnValidate()
        {
            SyncFromOwner();
        }
    }
}
