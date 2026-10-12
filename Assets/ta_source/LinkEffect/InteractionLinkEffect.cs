using System;
using Project.ColorBlocks;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaSource.LinkEffect
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInteractionSensor))]
    public sealed class InteractionLinkEffect : MonoBehaviour
    {
        private static readonly int LinkColorId = Shader.PropertyToID("_LinkColor");
        private static readonly int FeasibleId = Shader.PropertyToID("_Feasible");
        private static readonly int LinkCountId = Shader.PropertyToID("_LinkCount");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        [SerializeField] private PlayerInteractionSensor sensor;
        [SerializeField] private PlayerColorWheel colorWheel;
        [SerializeField] private ColorCatalog colorCatalog;
        [SerializeField] private Material linkMaterial;
        [SerializeField] private Transform sourceAnchor;
        [SerializeField] private Vector3 sourceOffset = new Vector3(0f, .9f, 0f);
        [SerializeField, Min(.01f)] private float scanInterval = .12f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool previewWhenNoColor;
        [SerializeField] private string previewColorId = "red";
        [SerializeField, Min(.01f)] private float width = .13f;
        [SerializeField, Range(2, 32)] private int curveSegments = 12;
        [SerializeField, Min(0f)] private float arcHeight = .12f;
        [SerializeField, Min(0f)] private float waveHeight = .035f;
        [SerializeField, Min(1f)] private float linksPerUnit = 3.5f;
        [SerializeField] private float cameraFacingOffset = -.18f;
        [SerializeField, Range(0f, 1f)] private float opacity = .95f;

        private readonly Collider[] overlapBuffer = new Collider[64];
        private LineRenderer line;
        private MaterialPropertyBlock properties;
        private Component targetComponent;
        private Collider targetCollider;
        private IInteractionTarget interactionTarget;
        private ILinkEffectPreviewTarget previewTarget;
        private float nextScanTime;
        private string previousColorId = string.Empty;

        public string CurrentTargetName => targetComponent != null
            ? targetComponent.gameObject.name : "无";
        public string ActiveColorId { get; private set; } = string.Empty;
        public bool IsInteractionFeasible { get; private set; }
        public bool IsLinkVisible => line != null && line.enabled;

        private void Awake()
        {
            ResolveReferences();
            CreateLine();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CreateLine();
            nextScanTime = 0f;
        }

        private void LateUpdate()
        {
            if (line == null || linkMaterial == null || Time.timeScale <= 0f)
            {
                HideLink();
                return;
            }

            if (sensor == null || colorWheel == null)
            {
                ResolveReferences();
            }

            ActiveColorId = colorWheel != null
                ? colorWheel.SelectedColorId : string.Empty;
            if (string.IsNullOrWhiteSpace(ActiveColorId) && previewWhenNoColor)
            {
                ActiveColorId = previewColorId;
            }

            if (string.IsNullOrWhiteSpace(ActiveColorId) || sensor == null)
            {
                HideLink();
                return;
            }

            if (!string.Equals(previousColorId, ActiveColorId,
                    StringComparison.OrdinalIgnoreCase))
            {
                previousColorId = ActiveColorId;
                nextScanTime = 0f;
            }

            if (Time.time >= nextScanTime || targetComponent == null ||
                targetCollider == null || !targetCollider.enabled)
            {
                FindNearestCandidate();
                nextScanTime = Time.time + Mathf.Max(.01f, scanInterval);
            }

            if (targetComponent == null || targetCollider == null)
            {
                HideLink();
                return;
            }

            IsInteractionFeasible = previewTarget != null
                ? previewTarget.CanLink(gameObject, ActiveColorId)
                : interactionTarget != null && interactionTarget.CanInteract(gameObject);

            DrawLink();
        }

        private void ResolveReferences()
        {
            if (sensor == null) sensor = GetComponent<PlayerInteractionSensor>();
            if (colorWheel == null) colorWheel = GetComponent<PlayerColorWheel>();
        }

        private void CreateLine()
        {
            if (line != null || !Application.isPlaying) return;

            GameObject child = new GameObject("__InteractionLinkPreview");
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(transform, false);
            line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 1000;
            line.sharedMaterial = linkMaterial;
            line.enabled = false;
            properties = new MaterialPropertyBlock();
        }

        private void FindNearestCandidate()
        {
            targetComponent = null;
            targetCollider = null;
            interactionTarget = null;
            previewTarget = null;

            float radius = sensor.ScanRadius;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, radius, overlapBuffer,
                targetMask.value, QueryTriggerInteraction.Collide);
            float nearestSqr = radius * radius;

            for (int hitIndex = 0; hitIndex < count; hitIndex++)
            {
                Collider hit = overlapBuffer[hitIndex];
                if (hit == null || !hit.enabled ||
                    hit.transform.IsChildOf(transform)) continue;

                float distanceSqr = (hit.ClosestPoint(transform.position) -
                                     transform.position).sqrMagnitude;
                if (distanceSqr >= nearestSqr) continue;

                MonoBehaviour[] behaviours =
                    hit.GetComponentsInParent<MonoBehaviour>();
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null || !behaviour.isActiveAndEnabled ||
                        !(behaviour is IInteractionTarget candidate)) continue;

                    nearestSqr = distanceSqr;
                    targetComponent = behaviour;
                    targetCollider = hit;
                    interactionTarget = candidate;
                    previewTarget = behaviour as ILinkEffectPreviewTarget;
                    break;
                }
            }
        }

        private void DrawLink()
        {
            if (line.sharedMaterial != linkMaterial) line.sharedMaterial = linkMaterial;

            Vector3 source = sourceAnchor != null
                ? sourceAnchor.position : transform.TransformPoint(sourceOffset);
            Vector3 destination = previewTarget != null &&
                                  previewTarget.LinkAnchor != null
                ? previewTarget.LinkAnchor.position
                : targetCollider.ClosestPoint(source);
            if ((destination - source).sqrMagnitude < .01f)
            {
                destination = targetComponent.transform.position;
            }

            source.z += cameraFacingOffset;
            destination.z += cameraFacingOffset;
            Vector3 delta = destination - source;
            Vector3 side = Vector3.Cross(Vector3.forward, delta.normalized);
            int segments = Mathf.Clamp(curveSegments, 2, 32);
            line.positionCount = segments + 1;
            line.widthMultiplier = Mathf.Max(.01f, width);

            for (int index = 0; index <= segments; index++)
            {
                float t = (float)index / segments;
                float envelope = Mathf.Sin(Mathf.PI * t);
                float motion = Mathf.Sin(t * 11f - Time.time * 4f) * waveHeight;
                line.SetPosition(index, Vector3.Lerp(source, destination, t) +
                    side * envelope * (arcHeight + motion));
            }

            Color tint = ResolveColor(ActiveColorId);
            properties.Clear();
            properties.SetColor(LinkColorId, tint);
            properties.SetFloat(FeasibleId, IsInteractionFeasible ? 1f : 0f);
            properties.SetFloat(LinkCountId,
                Mathf.Max(2f, delta.magnitude * linksPerUnit));
            properties.SetFloat(OpacityId, opacity);
            line.SetPropertyBlock(properties);
            line.enabled = true;
        }

        private Color ResolveColor(string colorId)
        {
            ColorTypeDefinition definition = colorCatalog != null
                ? colorCatalog.Find(colorId) : null;
            if (definition != null) return definition.swatch;
            if (string.Equals(colorId, "green", StringComparison.OrdinalIgnoreCase))
                return new Color(.08f, 1f, .08f);
            if (string.Equals(colorId, "blue", StringComparison.OrdinalIgnoreCase))
                return new Color(.08f, .28f, 1f);
            return new Color(1f, .08f, .08f);
        }

        private void HideLink()
        {
            if (line != null) line.enabled = false;
            IsInteractionFeasible = false;
        }

        private void OnDisable()
        {
            if (line != null) Destroy(line.gameObject);
            line = null;
            properties = null;
            targetComponent = null;
            targetCollider = null;
            interactionTarget = null;
            previewTarget = null;
            ActiveColorId = string.Empty;
        }

        private void OnValidate()
        {
            scanInterval = Mathf.Max(.01f, scanInterval);
            width = Mathf.Max(.01f, width);
            linksPerUnit = Mathf.Max(1f, linksPerUnit);
            curveSegments = Mathf.Clamp(curveSegments, 2, 32);
        }
    }
}
