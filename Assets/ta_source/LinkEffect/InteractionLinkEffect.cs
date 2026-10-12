using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaSource.LinkEffect
{
    // Reads existing gameplay contracts; never invokes an interaction command.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInteractionSensor))]
    public sealed class InteractionLinkEffect : MonoBehaviour
    {
        private static readonly int LinkColorId = Shader.PropertyToID("_LinkColor");
        private static readonly int FeasibleId = Shader.PropertyToID("_Feasible");
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
        [SerializeField, Range(1, 32)] private int maxLinks = 16;
        [SerializeField, Min(.01f)] private float width = .2f;
        [SerializeField, Range(2, 32)] private int curveSegments = 12;
        [SerializeField, Min(0f)] private float arcHeight = .08f;
        [SerializeField, Min(0f)] private float waveHeight = .02f;
        [SerializeField] private float cameraFacingOffset = -.18f;
        [SerializeField, Range(0f, 1f)] private float opacity = .68f;

        private struct Candidate
        {
            public Component Component;
            public Collider Collider;
            public Transform Anchor;
            public float DistanceSqr;
            public bool Feasible;
            public bool ColorPreview;
        }

        private Collider[] overlapBuffer = new Collider[128];
        private readonly List<MonoBehaviour> behaviourBuffer = new List<MonoBehaviour>(16);
        private readonly List<Candidate> candidates = new List<Candidate>(32);
        private readonly Dictionary<int, int> candidateIndices = new Dictionary<int, int>(32);
        private readonly List<LineRenderer> lines = new List<LineRenderer>(16);
        private MaterialPropertyBlock properties;
        private PlayerController player;
        private float nextScanTime;
        private string previousColorId = string.Empty;

        public string CurrentTargetName => candidates.Count > 0 && candidates[0].Component != null
            ? candidates[0].Component.gameObject.name : "无";
        public string ActiveColorId { get; private set; } = string.Empty;
        public bool IsInteractionFeasible => candidates.Count > 0 && candidates[0].Feasible;
        public bool IsLinkVisible => VisibleLinkCount > 0;
        public int DetectedTargetCount => candidates.Count;
        public int VisibleLinkCount { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            ResolveReferences();
            nextScanTime = 0f;
        }

        private void LateUpdate()
        {
            if (linkMaterial == null || Time.timeScale <= 0f)
            {
                HideLines();
                return;
            }
            ResolveReferences();
            ActiveColorId = colorWheel != null ? colorWheel.SelectedColorId : string.Empty;
            if (string.IsNullOrWhiteSpace(ActiveColorId) && previewWhenNoColor)
                ActiveColorId = previewColorId;
            if (sensor == null)
            {
                HideLines();
                return;
            }
            if (!string.Equals(previousColorId, ActiveColorId, StringComparison.OrdinalIgnoreCase))
            {
                previousColorId = ActiveColorId;
                nextScanTime = 0f;
            }
            if (Time.time >= nextScanTime)
            {
                ScanTargets();
                nextScanTime = Time.time + Mathf.Max(.01f, scanInterval);
            }
            DrawLines();
        }

        private void ResolveReferences()
        {
            if (sensor == null) sensor = GetComponent<PlayerInteractionSensor>();
            if (colorWheel == null) colorWheel = GetComponent<PlayerColorWheel>();
            if (player == null) player = GetComponent<PlayerController>();
        }

        private void ScanTargets()
        {
            candidates.Clear();
            candidateIndices.Clear();
            float radius = sensor.ScanRadius;
            int count;
            do
            {
                count = Physics.OverlapSphereNonAlloc(transform.position, radius,
                    overlapBuffer, targetMask.value, QueryTriggerInteraction.Collide);
                if (count < overlapBuffer.Length || overlapBuffer.Length >= 1024) break;
                overlapBuffer = new Collider[overlapBuffer.Length * 2];
            } while (true);

            bool hasColor = !string.IsNullOrWhiteSpace(ActiveColorId);
            for (int hitIndex = 0; hitIndex < count; hitIndex++)
            {
                Collider hit = overlapBuffer[hitIndex];
                if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy ||
                    hit.transform.IsChildOf(transform)) continue;
                float distanceSqr = (hit.ClosestPoint(transform.position) -
                    transform.position).sqrMagnitude;
                if (distanceSqr > radius * radius) continue;

                behaviourBuffer.Clear();
                hit.GetComponentsInParent(false, behaviourBuffer);
                for (int index = 0; index < behaviourBuffer.Count; index++)
                {
                    MonoBehaviour behaviour = behaviourBuffer[index];
                    if (behaviour == null || !behaviour.isActiveAndEnabled) continue;
                    if (hasColor && behaviour is IColorApplicationTarget colorTarget &&
                        TryReadColorTarget(behaviour, colorTarget, ActiveColorId,
                            out bool colorFeasible))
                        AddCandidate(behaviour, hit, null, distanceSqr,
                            colorFeasible, true);
                    if (behaviour is IInteractionTarget interactionTarget)
                    {
                        ILinkEffectPreviewTarget preview = behaviour as ILinkEffectPreviewTarget;
                        bool feasible = preview != null
                            ? preview.CanLink(gameObject, ActiveColorId)
                            : interactionTarget.CanInteract(gameObject);
                        AddCandidate(behaviour, hit, preview?.LinkAnchor,
                            distanceSqr, feasible, false);
                    }
                }
            }
            ReadCarryTargets(radius);
            candidates.Sort((left, right) =>
            {
                if (left.Feasible != right.Feasible) return left.Feasible ? -1 : 1;
                return left.DistanceSqr.CompareTo(right.DistanceSqr);
            });
        }

        // BlockFeature exposes IColorApplicationTarget for every feature, while
        // its default OnColorApplied returns false. Only real handlers qualify.
        private bool TryReadColorTarget(MonoBehaviour behaviour,
            IColorApplicationTarget target, string colorId, out bool feasible)
        {
            feasible = false;
            bool red = IsColor(colorId, "red");
            bool blue = IsColor(colorId, "blue");
            bool handlesColor;
            if (behaviour is UniversalColorBlock) handlesColor = true;
            else if (behaviour is BouncePlantFeature) handlesColor = red;
            else if (behaviour is PlantObstacleFeature plant)
                handlesColor = red && plant.IsIntact;
            else if (behaviour is LavaHazardFeature lava)
                handlesColor = blue && lava.BlueReplacementPrefab != null;
            else if (behaviour is ClimbableVineFeature vine)
                handlesColor = red || (blue && !(vine is LadderSonFeature) &&
                    vine.MaxGrowthBlocks > 0 && vine.CurrentGrowthBlocks < vine.MaxGrowthBlocks);
            else if (behaviour is WaterVisualFeature) handlesColor = red;
            else if (behaviour is BlockFeature) return false;
            else handlesColor = true;

            feasible = handlesColor && target.CanApplyColor(colorId, gameObject);
            return true;
        }

        private void ReadCarryTargets(float radius)
        {
            if (player == null || player.IsCarrying || player.IsCarryBusy) return;
            float cellSize = GridCellSizeUtility.Resolve(player);
            Vector3 playerPosition = player.CarryCenterWorldPosition;
            int playerX = Mathf.FloorToInt(playerPosition.x / cellSize);
            int playerY = Mathf.FloorToInt(playerPosition.y / cellSize);
            IReadOnlyList<IPlayerCarryTarget> carryTargets = PlayerCarryTargetRegistry.All;
            for (int index = 0; index < carryTargets.Count; index++)
            {
                IPlayerCarryTarget carryTarget = carryTargets[index];
                Component component = carryTarget as Component;
                if (component == null || !(component is Behaviour behaviour) ||
                    !behaviour.isActiveAndEnabled || carryTarget.CarryTransform == null ||
                    !carryTarget.CanCarry(player)) continue;
                Vector3 position = carryTarget.CarryTransform.position;
                float distanceSqr = (position - playerPosition).sqrMagnitude;
                if (distanceSqr > radius * radius) continue;
                int x = Mathf.FloorToInt(position.x / cellSize);
                int y = Mathf.FloorToInt(position.y / cellSize);
                bool adjacent = Mathf.Abs(x - playerX) + Mathf.Abs(y - playerY) == 1;
                AddCandidate(component, null, carryTarget.CarryTransform,
                    distanceSqr, adjacent, false);
            }
        }

        private void AddCandidate(Component component, Collider hit, Transform anchor,
            float distanceSqr, bool feasible, bool colorPreview)
        {
            ColorObject colorObject = component.GetComponentInParent<ColorObject>();
            int key = colorObject != null ? colorObject.gameObject.GetInstanceID()
                : component.gameObject.GetInstanceID();
            if (candidateIndices.TryGetValue(key, out int index))
            {
                Candidate existing = candidates[index];
                if (existing.ColorPreview && !colorPreview) return;
                if (colorPreview && !existing.ColorPreview)
                    existing.Feasible = false;
                if ((colorPreview && !existing.ColorPreview) ||
                    (feasible && !existing.Feasible) ||
                    (feasible == existing.Feasible && distanceSqr < existing.DistanceSqr))
                {
                    existing.Component = component;
                    existing.Collider = hit;
                    existing.Anchor = anchor;
                    existing.DistanceSqr = distanceSqr;
                }
                existing.Feasible |= feasible;
                existing.ColorPreview = colorPreview;
                candidates[index] = existing;
                return;
            }
            candidateIndices.Add(key, candidates.Count);
            candidates.Add(new Candidate
            {
                Component = component, Collider = hit, Anchor = anchor,
                DistanceSqr = distanceSqr, Feasible = feasible,
                ColorPreview = colorPreview
            });
        }

        private void DrawLines()
        {
            int count = Mathf.Min(candidates.Count, maxLinks);
            Color tint = ResolveColor(ActiveColorId);
            int visible = 0;
            for (int index = 0; index < count; index++)
            {
                Candidate candidate = candidates[index];
                if (candidate.Component == null ||
                    !candidate.Component.gameObject.activeInHierarchy ||
                    (candidate.Collider != null && !candidate.Collider.enabled)) continue;
                DrawLine(EnsureLine(visible), candidate, tint);
                visible++;
            }
            for (int index = visible; index < lines.Count; index++) lines[index].enabled = false;
            VisibleLinkCount = visible;
        }

        private LineRenderer EnsureLine(int index)
        {
            while (lines.Count <= index)
            {
                GameObject child = new GameObject("__InteractionPixelLink");
                child.hideFlags = HideFlags.DontSave;
                child.transform.SetParent(transform, false);
                LineRenderer line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.numCapVertices = 0;
                line.numCornerVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sortingOrder = 1000;
                line.sharedMaterial = linkMaterial;
                line.enabled = false;
                lines.Add(line);
            }
            return lines[index];
        }

        private void DrawLine(LineRenderer line, Candidate candidate, Color tint)
        {
            Vector3 source = sourceAnchor != null
                ? sourceAnchor.position : transform.TransformPoint(sourceOffset);
            Vector3 destination = candidate.Anchor != null
                ? candidate.Anchor.position
                : candidate.Collider != null
                    ? candidate.Collider.ClosestPoint(source)
                    : candidate.Component.transform.position;
            if ((destination - source).sqrMagnitude < .01f)
                destination = candidate.Component.transform.position;
            source.z += cameraFacingOffset;
            destination.z += cameraFacingOffset;
            Vector3 delta = destination - source;
            Vector3 side = Vector3.Cross(Vector3.forward, delta.normalized);
            int segments = Mathf.Clamp(curveSegments, 2, 32);
            line.positionCount = segments + 1;
            line.widthMultiplier = Mathf.Max(.01f, width);
            if (line.sharedMaterial != linkMaterial) line.sharedMaterial = linkMaterial;
            for (int index = 0; index <= segments; index++)
            {
                float t = (float)index / segments;
                float envelope = Mathf.Sin(Mathf.PI * t);
                float motion = Mathf.Sin(t * 11f - Time.time * 4f) * waveHeight;
                line.SetPosition(index, Vector3.Lerp(source, destination, t) +
                    side * envelope * (arcHeight + motion));
            }
            properties.Clear();
            properties.SetColor(LinkColorId, tint);
            properties.SetFloat(FeasibleId, candidate.Feasible ? 1f : 0f);
            properties.SetFloat(OpacityId, opacity);
            line.SetPropertyBlock(properties);
            line.enabled = true;
        }

        private Color ResolveColor(string colorId)
        {
            if (string.IsNullOrWhiteSpace(colorId)) return new Color(.75f, .85f, 1f);
            ColorTypeDefinition definition = colorCatalog != null
                ? colorCatalog.Find(colorId) : null;
            if (definition != null) return definition.swatch;
            if (IsColor(colorId, "green")) return new Color(.08f, 1f, .08f);
            if (IsColor(colorId, "blue")) return new Color(.08f, .28f, 1f);
            return new Color(1f, .08f, .08f);
        }

        private static bool IsColor(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private void HideLines()
        {
            for (int index = 0; index < lines.Count; index++) lines[index].enabled = false;
            VisibleLinkCount = 0;
        }

        private void OnDisable()
        {
            for (int index = 0; index < lines.Count; index++)
                if (lines[index] != null) Destroy(lines[index].gameObject);
            lines.Clear();
            candidates.Clear();
            candidateIndices.Clear();
            VisibleLinkCount = 0;
            ActiveColorId = string.Empty;
        }

        private void OnValidate()
        {
            scanInterval = Mathf.Max(.01f, scanInterval);
            width = Mathf.Max(.01f, width);
            curveSegments = Mathf.Clamp(curveSegments, 2, 32);
            maxLinks = Mathf.Clamp(maxLinks, 1, 32);
        }
    }
}
