using UnityEngine;

namespace Project.WorldItems
{
    /// <summary>Routes a spike's child collider callbacks to its single gameplay owner.</summary>
    [AddComponentMenu(""), DisallowMultipleComponent]
    public sealed class SpikeContactRelay : MonoBehaviour
    {
        private NeutralSpike owner;
        private Vector2Int cell;

        internal void Bind(NeutralSpike value) => owner = value;
        internal void Bind(NeutralSpike value, Vector2Int valueCell) { owner = value; cell = valueCell; }
        private void OnTriggerEnter(Collider other) => Forward(other);
        private void OnTriggerStay(Collider other) => Forward(other);
        private void OnCollisionEnter(Collision collision) => Forward(collision.collider);
        private void OnCollisionStay(Collision collision) => Forward(collision.collider);
        private void Forward(Collider other)
        {
            if (owner != null) owner.Hit(other, cell);
        }
    }
}
