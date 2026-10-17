using UnityEngine;

namespace Project.WorldItems
{
    /// <summary>Routes a spike's child collider callbacks to its single gameplay owner.</summary>
    [AddComponentMenu(""), DisallowMultipleComponent]
    public sealed class SpikeContactRelay : MonoBehaviour
    {
        private NeutralSpike owner;

        internal void Bind(NeutralSpike value) => owner = value;
        private void OnTriggerEnter(Collider other) => Forward(other);
        private void OnTriggerStay(Collider other) => Forward(other);
        private void OnCollisionEnter(Collision collision) => Forward(collision.collider);
        private void OnCollisionStay(Collision collision) => Forward(collision.collider);
        private void Forward(Collider other)
        {
            if (owner != null) owner.Hit(other);
        }
    }
}
