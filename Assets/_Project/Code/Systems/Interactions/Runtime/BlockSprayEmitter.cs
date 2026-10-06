using System.Collections;
using Project.ColorBlocks;
using UnityEngine;

namespace Project.Interactions
{
    [DisallowMultipleComponent]
    public sealed class BlockSprayEmitter : MonoBehaviour
    {
        [SerializeField] private Vector3 localOrigin = new Vector3(0f, .5f, 0f);
        [SerializeField] private Vector3 localDirection = Vector3.right;
        [SerializeField, Min(.1f)] private float distance = 4f;
        [SerializeField, Min(.05f)] private float duration = 1.5f;
        [SerializeField, Min(.01f)] private float radius = .12f;
        [SerializeField] private Color sprayColor = Color.white;
        private Vector3 interactionPoint;
        private bool hasInteractionPoint;
        private Coroutine active;
        private GameObject visual;

        public void SetInteractionPoint(Vector3 point)
        {
            interactionPoint = point;
            hasInteractionPoint = true;
        }

        public void ApplySpray(GameObject context)
        {
            Vector3 origin = ResolveOrigin(context);
            Vector3 direction = transform.TransformDirection(localDirection);
            if (direction.sqrMagnitude < .0001f)
            {
                direction = Vector3.right;
            }
            direction.Normalize();
            if (active != null) StopCoroutine(active);
            DestroyVisual();
            active = StartCoroutine(ShowSpray(origin, direction));
            hasInteractionPoint = false;
        }

        public void ApplyPlayerInteraction(GameObject actor)
        {
            ApplySpray(actor);
        }

        private Vector3 ResolveOrigin(GameObject context)
        {
            if (hasInteractionPoint) return interactionPoint;
            Collider collider = GetComponent<Collider>();
            if (collider != null && context != null)
            {
                return collider.ClosestPoint(context.transform.position);
            }
            return transform.TransformPoint(localOrigin);
        }

        private IEnumerator ShowSpray(Vector3 origin, Vector3 direction)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Color Spray";
            visual.transform.position = origin + direction * distance * .5f;
            visual.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            visual.transform.localScale = new Vector3(radius, distance * .5f, radius);
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = sprayColor == Color.white
                    ? ResolveColor()
                    : sprayColor;
            }
            yield return new WaitForSeconds(duration);
            DestroyVisual();
            active = null;
        }

        private Color ResolveColor()
        {
            ColorBlock block = GetComponent<ColorBlock>();
            if (block == null) return Color.white;
            string colorId = string.IsNullOrWhiteSpace(block.CurrentColorTypeId)
                ? block.BaseColorTypeId
                : block.CurrentColorTypeId;
            return colorId == "blue"
                ? new Color(.2f, .65f, 1f, .85f)
                : new Color(1f, .2f, .2f, .85f);
        }

        private void OnDisable()
        {
            if (active != null) StopCoroutine(active);
            active = null;
            DestroyVisual();
            hasInteractionPoint = false;
        }

        private void DestroyVisual()
        {
            if (visual == null) return;
            if (Application.isPlaying) Destroy(visual);
            else DestroyImmediate(visual);
            visual = null;
        }
    }
}
