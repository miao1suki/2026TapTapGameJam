using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.ContactCubes
{
    public interface ICubeJetPushReceiver
    {
        void SetCubeJetVelocity(Component source, Vector3 velocity, float deltaTime);
    }

    public sealed class CubeWaterJet : MonoBehaviour
    {
        private ContactCube owner;
        private ContactCube red;
        private float length, width, speed;
        private Transform visual;
        private MeshRenderer visualRenderer;
        private Collider[] hits = new Collider[64];
        private readonly HashSet<Component> previous = new HashSet<Component>();
        private readonly HashSet<Component> current = new HashSet<Component>();
        public Vector3 Direction { get; private set; }
        public void Configure(ContactCube source, ContactCube redContact, float distance, float diameter, float velocity, Material material)
        {
            owner = source; red = redContact; length = distance; width = diameter; speed = velocity;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (visual == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "水柱预览"; visual = cube.transform;
                Collider collider = cube.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                visual.SetParent(transform, false); visualRenderer = cube.GetComponent<MeshRenderer>();
                visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            visualRenderer.sharedMaterial = material;
            UpdatePose();
        }
        private void UpdatePose()
        {
            if (owner == null || red == null) return;
            Direction = (owner.transform.position - red.transform.position).normalized;
            if (Direction.sqrMagnitude < .001f) { visualRenderer.enabled = false; return; }
            visualRenderer.enabled = true;
            transform.position = owner.transform.position;
            transform.rotation = Quaternion.FromToRotation(Vector3.right, Direction);
            visual.position = transform.position + Direction * (length * .5f);
            visual.rotation = transform.rotation;
            Vector3 parentScale = transform.lossyScale;
            visual.localScale = new Vector3(length / Mathf.Abs(parentScale.x), width / Mathf.Abs(parentScale.y), width / Mathf.Abs(parentScale.z));
        }
        private void FixedUpdate()
        {
            if (owner == null || !owner.CanMaintainJetWith(red)) { gameObject.SetActive(false); return; }
            UpdatePose(); current.Clear();
            if (Direction.sqrMagnitude > .001f)
            {
                int count;
                do
                {
                    count = Physics.OverlapBoxNonAlloc(transform.position + Direction * length * .5f,
                        new Vector3(length, width, width) * .5f, hits, transform.rotation, ~0, QueryTriggerInteraction.Collide);
                    if (count < hits.Length) break;
                    System.Array.Resize(ref hits, hits.Length * 2);
                } while (hits.Length <= 4096);
                for (int i = 0; i < count; i++)
                {
                    Collider hit = hits[i];
                    if (hit == null || hit.GetComponentInParent<CubeWaterJet>() != null) continue;
                    ContactCube cube = hit.GetComponentInParent<ContactCube>();
                    if (cube == owner || cube == red) continue;
                    Component target = cube;
                    if (target == null) target = hit.GetComponentInParent<PlayerController>();
                    if (target == null)
                        foreach (MonoBehaviour behaviour in hit.GetComponentsInParent<MonoBehaviour>())
                            if (behaviour.isActiveAndEnabled && behaviour is ICubeJetPushReceiver) { target = behaviour; break; }
                    if (target != null && current.Add(target)) Apply(target, Direction * speed, Time.fixedDeltaTime);
                }
            }
            foreach (Component target in previous) if (target != null && !current.Contains(target)) Apply(target, Vector3.zero, 0);
            previous.Clear(); foreach (Component target in current) previous.Add(target);
        }
        private void Apply(Component target, Vector3 velocity, float dt)
        {
            if (target is ContactCube cube) cube.RequestPush(this, velocity, dt);
            else if (target is PlayerController player)
            {
                // Existing water velocity is consumed only in swimming mode.
                if (velocity.sqrMagnitude > .0001f) player.EnterWater(this);
                else player.ExitWater(this);
                player.SetWaterVelocity(this, new Vector2(velocity.x, velocity.y));
            }
            else if (target is ICubeJetPushReceiver receiver) receiver.SetCubeJetVelocity(this, velocity, dt);
        }
        private void OnDisable()
        {
            foreach (Component target in previous) if (target != null) Apply(target, Vector3.zero, 0);
            previous.Clear(); current.Clear();
        }
    }
}
