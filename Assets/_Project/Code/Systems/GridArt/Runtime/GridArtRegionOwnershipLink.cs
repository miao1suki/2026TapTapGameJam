using UnityEngine;

namespace Project.GridArt
{
    // Unity needs a matching source filename to persist this component's
    // MonoScript reference across Scene/Prefab reloads.
    [AddComponentMenu("")]
    public sealed class GridArtRegionOwnershipLink : MonoBehaviour
    {
        [SerializeField, HideInInspector] private GridArtRegionOwnership ownership;
        public GridArtRegionOwnership Ownership => ownership;
        public void SetOwnership(GridArtRegionOwnership value) => ownership = value;
    }
}
