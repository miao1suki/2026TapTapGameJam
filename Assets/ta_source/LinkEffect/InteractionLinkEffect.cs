using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace TaSource.LinkEffect
{
    // Reads existing gameplay contracts; never invokes an interaction command.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInteractionSensor))]
    public sealed class InteractionLinkEffect : MonoBehaviour
    {
        private static readonly int LinkColorId = Shader.PropertyToID("_LinkColor");
        private static readonly int LineThicknessId = Shader.PropertyToID("_LineThickness");
        private static readonly int FeasibleId = Shader.PropertyToID("_Feasible");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int EffectSpeedId = Shader.PropertyToID("_FlickerSpeed");
        private static readonly int StyleId = Shader.PropertyToID("_Style");
        private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
        private static readonly int DissolveProgressId = Shader.PropertyToID("_DissolveProgress");
        private static readonly int LineLengthId = Shader.PropertyToID("_LineLength");

        [SerializeField] private PlayerInteractionSensor sensor;
        [SerializeField] private PlayerColorWheel colorWheel;
        [SerializeField] private ColorCatalog colorCatalog;
        [SerializeField] private Material linkMaterial;
        [SerializeField] private Transform sourceAnchor;
        [SerializeField] private Vector3 sourceOffset = new Vector3(0f, .3f, 0f);
        [SerializeField, Min(.01f)] private float scanInterval = .12f;
        [SerializeField] private LayerMask targetMask = ~0;
        [FormerlySerializedAs("maxLinks"), SerializeField, Range(1, 32)] private int prewarmLinks = 1;
        [SerializeField, Range(2, 32)] private int curveSegments = 12;
        [SerializeField, Min(0f)] private float arcHeight = .08f;
        [SerializeField, Min(0f)] private float waveHeight = .055f;
        [SerializeField] private float cameraFacingOffset = -.18f;
        [SerializeField, Range(0f, 1f)] private float opacity = .68f;
        [FormerlySerializedAs("revealDuration"), SerializeField, Min(.05f)]
        [Tooltip("红蓝预览线整条淡入所需的生成时长；绿色绳索由独立钩子组件控制。")]
        private float generationDuration = .55f;
        [SerializeField, Min(.05f)] private float dissolveDuration = .3f;

        private struct Candidate
        {
            public Component Component;
            public Collider Collider;
            public float DistanceSqr;
            public bool Feasible;
            public int Key;
        }

        private sealed class LinkVisualState
        {
            public Vector3 Source;
            public Vector3 Destination;
            public Color Tint;
            public float Style;
            public float RevealProgress;
            public float RevealStartedAt;
            public float FadeStartedAt = -1f;
            public bool Feasible;
            public bool SeenThisFrame;
        }

        private Collider[] overlapBuffer = new Collider[128];
        private readonly List<MonoBehaviour> behaviourBuffer = new List<MonoBehaviour>(16);
        private readonly List<Candidate> candidates = new List<Candidate>(32);
        private readonly Dictionary<int, int> candidateIndices = new Dictionary<int, int>(32);
        private readonly List<LineRenderer> lines = new List<LineRenderer>(16);
        private readonly Dictionary<int, LinkVisualState> visualStates =
            new Dictionary<int, LinkVisualState>(32);
        private readonly List<int> staleRevealKeys = new List<int>(16);
        private readonly Stack<LinkVisualState> statePool = new Stack<LinkVisualState>(32);
        private Vector3[] linePoints;
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
        public Material LinkMaterial => linkMaterial;
        public int PooledLineCount => lines.Count;
        public Vector3 SourceWorldPosition => sourceAnchor != null
            ? sourceAnchor.position
            : transform.position + transform.TransformVector(new Vector3(sourceOffset.x, 0f, sourceOffset.z))
                + transform.up * sourceOffset.y;

        private void Awake()
        {
            ResolveReferences();
            properties = new MaterialPropertyBlock();
            if (linkMaterial != null)
            {
                // Legacy prefabs may still serialize sixteen. One visible target
                // now needs one pooled renderer, not sixteen hidden renderers.
                int initialCapacity = 1;
                EnsureLine(initialCapacity - 1);
                for (int index = 0; index < initialCapacity; index++) statePool.Push(new LinkVisualState());
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            nextScanTime = 0f;
            ClearVisualStates();
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
            if (!IsSupportedColor(ActiveColorId))
            {
                ActiveColorId = string.Empty;
                candidates.Clear();
                candidateIndices.Clear();
                ClearVisualStates();
                HideLines();
                nextScanTime = 0f;
                return;
            }
            if (player != null &&
                (player.IsControlLocked || player.IsCarrying || player.IsCarryBusy))
            {
                candidates.Clear();
                candidateIndices.Clear();
                ClearVisualStates();
                HideLines();
                nextScanTime = 0f;
                return;
            }
            if (sensor == null)
            {
                HideLines();
                return;
            }
            if (!string.Equals(previousColorId, ActiveColorId, StringComparison.OrdinalIgnoreCase))
            {
                previousColorId = ActiveColorId;
                nextScanTime = 0f;
                ClearVisualStates();
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
            Vector3 rangeCenter = sensor.transform.position;
            int count;
            do
            {
                count = Physics.OverlapSphereNonAlloc(rangeCenter, radius,
                    overlapBuffer, targetMask.value, QueryTriggerInteraction.Collide);
                if (count < overlapBuffer.Length || overlapBuffer.Length >= 1024) break;
                overlapBuffer = new Collider[overlapBuffer.Length * 2];
            } while (true);

            for (int hitIndex = 0; hitIndex < count; hitIndex++)
            {
                Collider hit = overlapBuffer[hitIndex];
                if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy ||
                    hit.transform.IsChildOf(transform)) continue;
                // Ripple/mouse-preview colliders belong to presentation, not
                // to the enclosing color target's gameplay interaction volume.
                if (hit.GetComponentInParent<InteractiveWater.InteractiveWater>() != null)
                    continue;
                float distanceSqr = (hit.ClosestPoint(rangeCenter) -
                    rangeCenter).sqrMagnitude;
                if (distanceSqr > radius * radius) continue;
                var water = hit.GetComponentInParent<WaterVisualFeature>();
                if (water != null)
                {
                    if (!WaterCellVisibility.TryVisiblePoint(water, hit, rangeCenter, radius, out Vector3 visible)) continue;
                    distanceSqr = (visible - rangeCenter).sqrMagnitude;
                }

                behaviourBuffer.Clear();
                hit.GetComponentsInParent(false, behaviourBuffer);
                // The link is only a color preview. Other interaction targets
                // are intentionally ignored even if they can be used with F.
                for (int index = 0; index < behaviourBuffer.Count; index++)
                {
                    MonoBehaviour behaviour = behaviourBuffer[index];
                    if (!(behaviour is IColorApplicationTarget colorTarget)) continue;
                    if (behaviour.isActiveAndEnabled &&
                        CanColorReact(behaviour, colorTarget, ActiveColorId))
                        AddCandidate(behaviour, hit, distanceSqr, true);
                    break;
                }
            }
            candidates.Sort((left, right) =>
            {
                if (left.Feasible != right.Feasible) return left.Feasible ? -1 : 1;
                int distance = left.DistanceSqr.CompareTo(right.DistanceSqr);
                return distance != 0 ? distance : left.Key.CompareTo(right.Key);
            });
        }

        // CanApplyColor alone is insufficient: BlockFeature implements it for
        // every feature, including those whose OnColorApplied always fails.
        private bool CanColorReact(MonoBehaviour behaviour,
            IColorApplicationTarget target, string colorId)
        {
            bool red = IsColor(colorId, "red");
            bool blue = IsColor(colorId, "blue");
            if (!target.CanApplyColor(colorId, gameObject)) return false;
            if (behaviour is UniversalColorBlock) return true;
            if (behaviour is BouncePlantFeature) return red;
            if (behaviour is PlantObstacleFeature plant)
                return red && plant.IsIntact;
            if (behaviour is LavaHazardFeature lava)
                return blue && lava.BlueReplacementPrefab != null;
            if (behaviour is ClimbableVineFeature vine)
                return red || (blue && !(vine is LadderSonFeature) &&
                    vine.MaxGrowthBlocks > 0 &&
                    vine.CurrentGrowthBlocks < vine.MaxGrowthBlocks);
            if (behaviour is WaterVisualFeature) return red;
            return false;
        }

        private void AddCandidate(Component component, Collider hit,
            float distanceSqr, bool feasible)
        {
            ColorObject colorObject = component.GetComponentInParent<ColorObject>();
            // A vine's authored and runtime segments share one logical root.
            // Keep the closest real collider as the endpoint, not the root pivot.
            var vine = component as ClimbableVineFeature;
            int key = vine != null ? vine.Root.gameObject.GetInstanceID()
                : colorObject != null ? colorObject.gameObject.GetInstanceID()
                : component.gameObject.GetInstanceID();
            if (candidateIndices.TryGetValue(key, out int index))
            {
                Candidate existing = candidates[index];
                if ((feasible && !existing.Feasible) ||
                    (feasible == existing.Feasible && distanceSqr < existing.DistanceSqr))
                {
                    existing.Component = component;
                    existing.Collider = hit;
                    existing.DistanceSqr = distanceSqr;
                }
                existing.Feasible |= feasible;
                candidates[index] = existing;
                return;
            }
            candidateIndices.Add(key, candidates.Count);
            candidates.Add(new Candidate
            {
                Component = component, Collider = hit,
                DistanceSqr = distanceSqr, Feasible = feasible, Key = key
            });
        }

        private void DrawLines()
        {
            Color tint = ResolveColor(ActiveColorId);
            float style = IsColor(ActiveColorId, "red") ? 1f
                : IsColor(ActiveColorId, "blue") ? 2f
                : IsColor(ActiveColorId, "green") ? 3f : 0f;
            float lineThickness = linkMaterial.HasProperty(LineThicknessId)
                ? linkMaterial.GetFloat(LineThicknessId) : .2f;
            float noiseStrength = linkMaterial.HasProperty(NoiseStrengthId)
                ? linkMaterial.GetFloat(NoiseStrengthId) : .75f;
            float effectSpeed = linkMaterial.HasProperty(EffectSpeedId)
                ? linkMaterial.GetFloat(EffectSpeedId) : 7f;
            Vector3 rangeCenter = sensor.transform.position;
            float rangeRadius = sensor.ScanRadius;
            int visible = 0;
            foreach (LinkVisualState state in visualStates.Values)
                state.SeenThisFrame = false;

            for (int index = 0; index < candidates.Count; index++)
            {
                Candidate candidate = candidates[index];
                if (candidate.Component == null ||
                    !candidate.Component.gameObject.activeInHierarchy ||
                    (candidate.Collider != null && !candidate.Collider.enabled) ||
                    !TryGetLineEndpoints(candidate, rangeCenter, rangeRadius,
                        out Vector3 source, out Vector3 destination)) continue;

                if (!visualStates.TryGetValue(candidate.Key, out LinkVisualState state))
                {
                    state = statePool.Count > 0 ? statePool.Pop() : new LinkVisualState();
                    state.RevealStartedAt = Time.time;
                    state.FadeStartedAt = -1f;
                    state.RevealProgress = 0f;
                    visualStates.Add(candidate.Key, state);
                }
                else if (state.FadeStartedAt >= 0f)
                {
                    state.FadeStartedAt = -1f;
                    state.RevealStartedAt = Time.time;
                }

                state.SeenThisFrame = true;
                state.Source = source;
                state.Destination = destination;
                state.Tint = tint;
                state.Style = style;
                state.Feasible = candidate.Feasible;
                state.RevealProgress = Mathf.SmoothStep(0f, 1f,
                    (Time.time - state.RevealStartedAt) /
                    (generationDuration > 0f ? generationDuration : .55f));
                DrawLine(EnsureLine(visible), state, lineThickness, noiseStrength,
                    effectSpeed, 0f, rangeCenter, rangeRadius);
                visible++;
                break; // candidates are nearest-first; never draw a second target.
            }

            staleRevealKeys.Clear();
            foreach (KeyValuePair<int, LinkVisualState> item in visualStates)
            {
                LinkVisualState state = item.Value;
                if (state.SeenThisFrame) continue;
                if (state.FadeStartedAt < 0f) state.FadeStartedAt = Time.time;
                float dissolve = (Time.time - state.FadeStartedAt) /
                    Mathf.Max(.05f, dissolveDuration);
                if (dissolve >= 1f || visible > 0)
                {
                    staleRevealKeys.Add(item.Key);
                    continue;
                }
                DrawLine(EnsureLine(visible), state, lineThickness, noiseStrength,
                    effectSpeed, dissolve, rangeCenter, rangeRadius);
                visible++;
            }
            for (int index = 0; index < staleRevealKeys.Count; index++)
            {
                int key = staleRevealKeys[index];
                statePool.Push(visualStates[key]);
                visualStates.Remove(key);
            }
            for (int index = visible; index < lines.Count; index++) lines[index].enabled = false;
            VisibleLinkCount = visible;
        }

        private bool TryGetLineEndpoints(Candidate candidate, Vector3 rangeCenter,
            float rangeRadius, out Vector3 source, out Vector3 destination)
        {
            source = SourceWorldPosition;
            destination = candidate.Collider != null
                ? candidate.Collider.ClosestPoint(rangeCenter)
                : candidate.Component.transform.position;
            if ((destination - rangeCenter).sqrMagnitude > rangeRadius * rangeRadius)
                return false;

            if (candidate.Collider != null)
                destination = candidate.Collider.ClosestPoint(source);
            var water = candidate.Component as WaterVisualFeature;
            if (water != null && candidate.Collider != null &&
                !WaterCellVisibility.TryVisiblePoint(water, candidate.Collider, source, rangeRadius, out destination)) return false;
            Vector3 sourceOffsetFromCenter = source - rangeCenter;
            if (sourceOffsetFromCenter.sqrMagnitude > rangeRadius * rangeRadius)
                source = rangeCenter + sourceOffsetFromCenter.normalized * rangeRadius;
            Vector3 targetOffsetFromCenter = destination - rangeCenter;
            if (targetOffsetFromCenter.sqrMagnitude > rangeRadius * rangeRadius)
                destination = rangeCenter + targetOffsetFromCenter.normalized * rangeRadius;

            source.z += cameraFacingOffset;
            destination.z += cameraFacingOffset;
            return true;
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

        private void DrawLine(LineRenderer line, LinkVisualState state,
            float lineThickness, float noiseStrength, float effectSpeed,
            float dissolveProgress, Vector3 rangeCenter, float rangeRadius)
        {
            Vector3 delta = state.Destination - state.Source;
            Vector3 side = Vector3.Cross(Vector3.forward, delta.normalized);
            int segments = Mathf.Clamp(curveSegments, 2, 32);
            if (linePoints == null || linePoints.Length != segments + 1)
                linePoints = new Vector3[segments + 1];
            line.positionCount = segments + 1;
            // Reserve transparent geometry around the trunk for leaves, petals and sparks.
            // The material thickness still controls the visible core and motif dimensions.
            line.widthMultiplier = Mathf.Max(.01f, lineThickness * 3f);
            if (line.sharedMaterial != linkMaterial) line.sharedMaterial = linkMaterial;
            float lineLength = 0f;
            for (int index = 0; index <= segments; index++)
            {
                float t = (float)index / segments;
                float envelope = Mathf.Sin(Mathf.PI * t);
                float seed = state.Source.x * 13.1f + state.Destination.y * 5.7f;
                float noise = Mathf.PerlinNoise(t * 3.5f +
                    Time.time * Mathf.Max(.1f, effectSpeed) * .18f, seed);
                float motion = ((noise - .5f) * 2f * noiseStrength +
                    Mathf.Sin(t * 11f - Time.time * effectSpeed) * .25f) * waveHeight;
                Vector3 point = Vector3.Lerp(state.Source, state.Destination, t) +
                    side * envelope * (arcHeight + motion);
                Vector3 fromRangeCenter = point - rangeCenter;
                if (fromRangeCenter.sqrMagnitude > rangeRadius * rangeRadius)
                    point = rangeCenter + fromRangeCenter.normalized * rangeRadius;
                linePoints[index] = point;
                if (index > 0) lineLength += Vector3.Distance(linePoints[index - 1], point);
            }
            line.SetPositions(linePoints);
            properties.Clear();
            properties.SetColor(LinkColorId, state.Tint);
            properties.SetFloat(FeasibleId, state.Feasible ? 1f : 0f);
            properties.SetFloat(OpacityId, opacity);
            properties.SetFloat(StyleId, state.Style);
            properties.SetFloat(RevealProgressId, state.RevealProgress);
            properties.SetFloat(DissolveProgressId, dissolveProgress);
            properties.SetFloat(LineLengthId, Mathf.Max(.01f, lineLength));
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

        private static bool IsSupportedColor(string colorId) =>
            IsColor(colorId, "red") || IsColor(colorId, "blue");

        private void HideLines()
        {
            for (int index = 0; index < lines.Count; index++) lines[index].enabled = false;
            VisibleLinkCount = 0;
        }

        private void OnDisable()
        {
            HideLines();
            candidates.Clear();
            candidateIndices.Clear();
            ClearVisualStates();
            staleRevealKeys.Clear();
            VisibleLinkCount = 0;
            ActiveColorId = string.Empty;
        }

        private void ClearVisualStates()
        {
            foreach (var state in visualStates.Values) statePool.Push(state);
            visualStates.Clear();
        }

        private void OnDestroy()
        {
            for (int index = 0; index < lines.Count; index++)
                if (lines[index] != null) Destroy(lines[index].gameObject);
            lines.Clear();
            statePool.Clear();
        }

        private void OnValidate()
        {
            scanInterval = Mathf.Max(.01f, scanInterval);
            curveSegments = Mathf.Clamp(curveSegments, 2, 32);
            prewarmLinks = Mathf.Clamp(prewarmLinks, 1, 32);
            generationDuration = generationDuration > 0f
                ? Mathf.Max(.05f, generationDuration) : .55f;
            dissolveDuration = dissolveDuration > 0f
                ? Mathf.Max(.05f, dissolveDuration) : .3f;
        }
    }
}
