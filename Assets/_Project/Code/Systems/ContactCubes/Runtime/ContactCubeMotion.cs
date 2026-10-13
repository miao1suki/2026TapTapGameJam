using System.Collections.Generic;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.ContactCubes
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-100), RequireComponent(typeof(ContactCube), typeof(Rigidbody))]
    public sealed class ContactCubeMotion : MonoBehaviour, IRoomColorResettable, ICarryMotionParticipant
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
            if (shape != null) shape.isTrigger = false;
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
            gravityProtectedUntil = 0;
            if (suspended)
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true; body.useGravity = false; IsSettled = false;
                RestorePlayerCollision();
            }
            else
            {
                body.position = transform.position; body.rotation = transform.rotation;
                body.isKinematic = false; body.useGravity = true; body.constraints = baseConstraints;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            }
        }
        public void SetCarrySuspended(bool suspended)
        {
            cube.SetCarrySuspended(suspended);
            SuspendMotion(suspended);
        }
        public bool CanPlaceCarriedAt(Vector3 worldPosition)
        {
            if (shape == null) return false;
            Transform t = shape.transform;
            Vector3 center = t.TransformPoint(shape.center) + worldPosition - transform.position;
            Vector3 scale = t.lossyScale;
            Vector3 half = Vector3.Scale(shape.size * .49f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(center, half, nearby, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == nearby.Length) return false;
            for (int i = 0; i < count; i++)
                if (nearby[i] != null && !nearby[i].transform.IsChildOf(transform)) return false;
            return true;
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
            bool flowing = flow.sqrMagnitude > .0001f;
            bool supported = HasSupport();
            IsSettled = !GravityProtected && supported &&
                (IsSettled && !flowing || Mathf.Abs(body.linearVelocity.y) <= settleVelocity);
            // Green remains a solid climbing surface even while its shape changes.
            // Only an explicit jet/carry command may release its horizontal position.
            RigidbodyConstraints locks = IsSettled && !flowing
                ? RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY
                : RigidbodyConstraints.None;
            if (cube.Color == CubeColor.Green && !flowing) locks |= RigidbodyConstraints.FreezePositionX;
            body.constraints = baseConstraints | locks;
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
            Bounds bounds = shape.bounds;
            // A constant-width bottom probe: shrinking the entire tall box by a
            // percentage leaves an ever-larger gap and loses support as it grows.
            Vector3 half = new Vector3(Mathf.Max(.005f,bounds.extents.x-.01f), .01f,
                Mathf.Max(.005f,bounds.extents.z-.01f));
            Vector3 origin = new Vector3(bounds.center.x,bounds.min.y+.035f,bounds.center.z);
            int count = Physics.BoxCastNonAlloc(origin, half, Vector3.down, supportHits,
                Quaternion.identity, .1f, ~0, QueryTriggerInteraction.Ignore);
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
                if (pair.Key != null) Physics.IgnoreCollision(shape, pair.Key,
                    cube.Color == CubeColor.Green ? false : pair.Value || !IsSettled);
        }
        public void ResetForRoom()
        {
            velocities.Clear(); gravityProtectedUntil = 0; hadHorizontalPush = false; IsSettled = false;
            SetCarrySuspended(false); body.linearVelocity = Vector3.zero;
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
