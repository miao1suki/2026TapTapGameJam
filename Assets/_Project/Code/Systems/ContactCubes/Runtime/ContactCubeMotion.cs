using System.Collections.Generic;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.ContactCubes
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-100), RequireComponent(typeof(ContactCube), typeof(Rigidbody))]
    public sealed class ContactCubeMotion : MonoBehaviour, IRoomColorResettable
    {
        [SerializeField, Min(0), InspectorName("水平喷出重力保护（秒）")] private float launchGravityGrace = .25f;
        [SerializeField, Min(.001f), InspectorName("落稳竖直速度容差")] private float settleVelocity = .08f;
        private Rigidbody body;
        private ContactCube cube;
        private BoxCollider shape;
        private RigidbodyConstraints baseConstraints;
        private readonly Dictionary<Component, Vector3> velocities = new Dictionary<Component, Vector3>();
        private readonly List<Component> deadSources = new List<Component>();
        private readonly Dictionary<Collider, bool> playerPairs = new Dictionary<Collider, bool>();
        private readonly Collider[] nearby = new Collider[64];
        private readonly RaycastHit[] supportHits = new RaycastHit[32];
        private float gravityProtectedUntil;
        private bool hadHorizontalPush;
        public bool IsSettled { get; private set; }
        public bool GravityProtected => Time.time < gravityProtectedUntil;
        public bool IsSuspended { get; private set; }
        private void Awake()
        {
            body = GetComponent<Rigidbody>(); cube = GetComponent<ContactCube>(); shape = GetComponentInChildren<BoxCollider>();
            baseConstraints = body.constraints;
        }
        private void OnEnable() { if (cube == null) cube = GetComponent<ContactCube>(); cube.PushRequested += OnPush; }
        private void OnPush(Component source, Vector3 velocity, float dt)
        {
            if (source == null) return;
            if (velocity.sqrMagnitude < .0001f) velocities.Remove(source); else velocities[source] = velocity;
        }
        // Carry controller calls true while held, false after placing; it owns held poses.
        public void SuspendMotion(bool suspended)
        {
            IsSuspended = suspended; velocities.Clear(); hadHorizontalPush = false;
            if (suspended) { body.isKinematic = true; body.useGravity = false; IsSettled = false; }
            else { body.isKinematic = false; body.useGravity = true; body.constraints = baseConstraints; }
        }
        private void FixedUpdate()
        {
            if (shape == null || !shape.enabled || IsSuspended || body.isKinematic) return;
            deadSources.Clear(); foreach (var pair in velocities)
                if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || (pair.Key is Behaviour behaviour && !behaviour.isActiveAndEnabled)) deadSources.Add(pair.Key);
            foreach (var source in deadSources) velocities.Remove(source);
            Vector3 flow = Vector3.zero; foreach (Vector3 velocity in velocities.Values) flow += velocity;
            bool horizontal = Mathf.Abs(flow.x) > .01f && Mathf.Abs(flow.y) < .01f;
            if (horizontal && !hadHorizontalPush)
            {
                gravityProtectedUntil = Time.time + launchGravityGrace;
                body.linearVelocity = new Vector3(flow.x, 0, 0);
            }
            hadHorizontalPush = horizontal;
            body.useGravity = !GravityProtected;
            IsSettled = !GravityProtected && Mathf.Abs(body.linearVelocity.y) <= settleVelocity && HasSupport();
            bool flowing = flow.sqrMagnitude > .0001f;
            body.constraints = baseConstraints | (IsSettled && !flowing ? RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            if (flowing)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.x = flow.x;
                if (Mathf.Abs(flow.y) > .01f) velocity.y = flow.y;
                else if (GravityProtected) velocity.y = 0;
                velocity.z = 0; body.linearVelocity = velocity;
            }
            RefreshPlayerCollision();
        }
        private bool HasSupport()
        {
            Transform frame = shape.transform;
            Vector3 scale = frame.lossyScale;
            Vector3 half = Vector3.Scale(shape.size * .48f, new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            int count = Physics.BoxCastNonAlloc(frame.TransformPoint(shape.center) + Vector3.up * .025f, half, Vector3.down, supportHits,
                frame.rotation, .1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = supportHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerController>() != null) continue;
                if (supportHits[i].normal.y > .5f) return true;
            }
            return false;
        }
        private void RefreshPlayerCollision()
        {
            Bounds bounds = shape.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one, nearby,
                Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = nearby[i];
                if (hit != null && hit.GetComponentInParent<PlayerController>() != null && !playerPairs.ContainsKey(hit))
                    playerPairs.Add(hit, Physics.GetIgnoreCollision(shape, hit));
            }
            foreach (var pair in playerPairs)
                if (pair.Key != null) Physics.IgnoreCollision(shape, pair.Key, pair.Value || !IsSettled);
        }
        public void ResetForRoom()
        {
            velocities.Clear(); gravityProtectedUntil = 0; hadHorizontalPush = false; IsSettled = false;
            SuspendMotion(false); body.linearVelocity = Vector3.zero;
            RestorePlayerCollision();
        }
        private void RestorePlayerCollision()
        {
            foreach (var pair in playerPairs) if (pair.Key != null && shape != null) Physics.IgnoreCollision(shape, pair.Key, pair.Value);
            playerPairs.Clear();
        }
        private void OnDisable()
        {
            if (cube != null) cube.PushRequested -= OnPush;
            velocities.Clear(); RestorePlayerCollision();
            if (body != null) { body.useGravity = true; body.constraints = baseConstraints; }
        }
    }
}
