using UnityEngine;

namespace Project.Interactions
{
    [DisallowMultipleComponent]
    public sealed class AnchorPoint : MonoBehaviour
    {
        [SerializeField] private string anchorId;
        public string AnchorId => anchorId;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.25f, 1f, .55f, .9f);
            Gizmos.DrawWireSphere(transform.position, .25f);
        }
    }
}