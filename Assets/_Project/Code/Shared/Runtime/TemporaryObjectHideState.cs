using UnityEngine;

namespace Project
{
    [DisallowMultipleComponent]
    public sealed class TemporaryObjectHideState : MonoBehaviour
    {
        private Collider[] colliders;
        private Renderer[] renderers;
        private Rigidbody[] bodies;
        private bool[] colliderStates;
        private bool[] rendererStates;
        private bool[] bodyKinematicStates;
        private bool[] bodyGravityStates;
        private bool hidden;

        public bool IsHidden => hidden;

        public void Hide()
        {
            if (hidden)
            {
                return;
            }

            Cache();
            hidden = true;
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = false;
                }
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].forceRenderingOff = true;
                }
            }

            for (int index = 0; index < bodies.Length; index++)
            {
                if (bodies[index] != null)
                {
                    if (!bodies[index].isKinematic)
                    {
                        bodies[index].linearVelocity = Vector3.zero;
                        bodies[index].angularVelocity = Vector3.zero;
                    }

                    bodies[index].useGravity = false;
                    bodies[index].isKinematic = true;
                }
            }
        }

        public void Restore()
        {
            if (!hidden)
            {
                return;
            }

            hidden = false;
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = colliderStates[index];
                }
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].forceRenderingOff =
                        rendererStates[index];
                }
            }

            for (int index = 0; index < bodies.Length; index++)
            {
                if (bodies[index] != null)
                {
                    bodies[index].isKinematic =
                        bodyKinematicStates[index];
                    bodies[index].useGravity =
                        bodyGravityStates[index];
                }
            }
        }

        private void Cache()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            bodies = GetComponentsInChildren<Rigidbody>(true);
            colliderStates = new bool[colliders.Length];
            rendererStates = new bool[renderers.Length];
            bodyKinematicStates = new bool[bodies.Length];
            bodyGravityStates = new bool[bodies.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                colliderStates[index] =
                    colliders[index] != null && colliders[index].enabled;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                rendererStates[index] =
                    renderers[index] != null &&
                    renderers[index].forceRenderingOff;
            }

            for (int index = 0; index < bodies.Length; index++)
            {
                bodyKinematicStates[index] =
                    bodies[index] != null && bodies[index].isKinematic;
                bodyGravityStates[index] =
                    bodies[index] != null && bodies[index].useGravity;
            }
        }
    }
}
