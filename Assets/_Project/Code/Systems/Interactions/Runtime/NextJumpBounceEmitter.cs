using Project.Player;
using UnityEngine;

namespace Project.Interactions
{
    [DisallowMultipleComponent]
    public sealed class NextJumpBounceEmitter : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float multiplier = 1.5f;

        public void ArmNextJump(GameObject unusedContext)
        {
            NextJumpBounceService.Arm(multiplier);
        }
    }
}