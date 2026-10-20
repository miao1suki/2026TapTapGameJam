using System.Collections.Generic;
using UnityEngine;

namespace Project.BlockFeatures
{
    [DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/道具/传送目标方块")]
    public sealed class TeleportAnchorFeature : MonoBehaviour
    {
        private static readonly Dictionary<string, TeleportAnchorFeature>
            Registered = new Dictionary<string, TeleportAnchorFeature>();
        private string registeredId;

        [SerializeField] private string teleportId = "teleport_target";
        [SerializeField] private bool disableCollidersOnEnable = true;
        [SerializeField] private bool hideRenderersOnEnable = true;

        public string TeleportId => teleportId;
        public static IEnumerable<TeleportAnchorFeature>
            RegisteredAnchors => Registered.Values;
        public bool IsRegistered =>
            !string.IsNullOrWhiteSpace(registeredId) &&
            Registered.TryGetValue(
                registeredId,
                out TeleportAnchorFeature current) &&
            current == this;
        public static int RegistryCount => Registered.Count;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Registered.Clear();
        }

        private void OnEnable()
        {
            ApplyPresentation();
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public static bool TeleportObject(
            GameObject objectToTeleport,
            string destinationId)
        {
            if (objectToTeleport == null ||
                string.IsNullOrWhiteSpace(destinationId) ||
                !Registered.TryGetValue(
                    destinationId.Trim(),
                    out TeleportAnchorFeature destination) ||
                destination == null)
            {
                return false;
            }

            Transform destinationTransform =
                destination.transform;
            Rigidbody body =
                objectToTeleport.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position =
                    destinationTransform.position;
                body.rotation =
                    destinationTransform.rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.WakeUp();
            }
            objectToTeleport.transform.SetPositionAndRotation(
                destinationTransform.position,
                destinationTransform.rotation);
            return true;
        }

        public static bool TryGetAnchor(
            string id,
            out TeleportAnchorFeature anchor)
        {
            anchor = null;
            return !string.IsNullOrWhiteSpace(id) &&
                   Registered.TryGetValue(
                       id.Trim(),
                       out anchor) &&
                   anchor != null;
        }

        public void SetTeleportId(string value)
        {
            Unregister();
            teleportId = value ?? string.Empty;
            if (isActiveAndEnabled)
            {
                Register();
            }
        }

        private void Register()
        {
            string id = teleportId?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            if (Registered.TryGetValue(
                    id,
                    out TeleportAnchorFeature existing) &&
                existing != null &&
                existing != this)
            {
                Debug.LogWarning(
                    "[TeleportAnchor] 传送 ID 重复，后注册的目标不会覆盖：" +
                    id,
                    this);
                registeredId = null;
                return;
            }
            Registered[id] = this;
            registeredId = id;
        }

        private void Unregister()
        {
            string id = registeredId;
            if (string.IsNullOrEmpty(id) ||
                !Registered.TryGetValue(
                    id,
                    out TeleportAnchorFeature current) ||
                current != this)
            {
                return;
            }
            Registered.Remove(id);
            registeredId = null;
        }

        private void OnValidate()
        {
            if (!Application.isPlaying ||
                !isActiveAndEnabled)
            {
                return;
            }
            Unregister();
            Register();
        }

        private void ApplyPresentation()
        {
            if (disableCollidersOnEnable)
            {
                Collider[] colliders =
                    GetComponentsInChildren<Collider>(true);
                for (int index = 0;
                     index < colliders.Length;
                     index++)
                {
                    colliders[index].enabled = false;
                }
            }

            if (hideRenderersOnEnable)
            {
                Renderer[] renderers =
                    GetComponentsInChildren<Renderer>(true);
                for (int index = 0;
                     index < renderers.Length;
                     index++)
                {
                    renderers[index].enabled = false;
                }
            }
        }
    }
}
