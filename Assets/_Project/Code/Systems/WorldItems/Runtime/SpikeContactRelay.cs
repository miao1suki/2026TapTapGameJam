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
        private void OnTriggerEnter2D(Collider2D other) => Forward(other);
        private void OnTriggerStay2D(Collider2D other) => Forward(other);
        private void OnCollisionEnter2D(Collision2D collision) => Forward(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => Forward(collision.collider);
        private void Forward(Collider2D other)
        {
            if (owner != null) owner.Hit(other, cell);
        }
    }
}
