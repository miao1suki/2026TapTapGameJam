using Project.BlockFeatures;
using Project.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/Boss/Boss 玩家连线")]
    public sealed class BossPlayerLinkEffect : MonoBehaviour
    {
        private static readonly int LinkColorId =
            Shader.PropertyToID("_LinkColor");
        private static readonly int LineThicknessId =
            Shader.PropertyToID("_LineThickness");
        private static readonly int FeasibleId =
            Shader.PropertyToID("_Feasible");
        private static readonly int OpacityId =
            Shader.PropertyToID("_Opacity");
        private static readonly int NoiseStrengthId =
            Shader.PropertyToID("_NoiseStrength");
        private static readonly int EffectSpeedId =
            Shader.PropertyToID("_FlickerSpeed");
        private static readonly int StyleId =
            Shader.PropertyToID("_Style");
        private static readonly int RevealProgressId =
            Shader.PropertyToID("_RevealProgress");
        private static readonly int DissolveProgressId =
            Shader.PropertyToID("_DissolveProgress");
        private static readonly int LineLengthId =
            Shader.PropertyToID("_LineLength");

        [Header("目标")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private Vector3 bossOffset =
            Vector3.zero;
        [SerializeField] private Vector3 playerOffset =
            new Vector3(0f, .35f, 0f);
        [SerializeField] private float cameraFacingOffset = -.18f;

        [Header("三种线条材质")]
        [SerializeField] private Material redMaterial;
        [SerializeField] private Material blueMaterial;
        [SerializeField] private Material greenMaterial;
        [SerializeField] private Color redTint =
            new Color(1f, .08f, .08f, 1f);
        [SerializeField] private Color blueTint =
            new Color(.08f, .28f, 1f, 1f);
        [SerializeField] private Color greenTint =
            new Color(.08f, 1f, .08f, 1f);

        [Header("出现与消失")]
        [SerializeField, Min(0f)]
        private float generationDuration = .55f;
        [SerializeField, Min(0f)]
        private float dissolveDuration = .3f;

        [Header("距离表现")]
        [SerializeField, Min(0f)]
        private float minimumDistanceBlocks = 1.5f;
        [SerializeField, Min(.01f)]
        private float maximumDistanceBlocks = 18f;
        [SerializeField, Min(0f)]
        private float farOpacity = .4f;
        [SerializeField, Range(0f, 1f)]
        private float nearOpacity = .78f;
        [SerializeField, Min(0f)]
        private float farLineThickness = .024f;
        [SerializeField, Min(0f)]
        private float nearLineThickness = .9f;
        [SerializeField, Range(.25f, 1f)]
        private float lerpSharpness = 1.35f;

        [Header("线条形状")]
        [SerializeField, Range(2, 32)]
        private int curveSegments = 20;
        [SerializeField, Min(0f)]
        private float arcHeight = .16f;
        [SerializeField, Min(0f)]
        private float waveHeight = .055f;
        [SerializeField, Range(0f, .9f)]
        private float styleSeparationRatio = .4f;
        [SerializeField]
        private string sortingLayerName = "BossTop";
        [SerializeField] private int sortingOrder = 32760;

        private readonly LineRenderer[] lines =
            new LineRenderer[3];
        private readonly Material[] styleMaterials =
            new Material[3];
        private readonly Material[] runtimeMaterials =
            new Material[3];
        private readonly Color[] styleTints =
            new Color[3];
        private BossController controller;
        private PlayerController player;
        private Vector3[] points;
        private float nextPlayerLookupTime;
        private float transitionStartedAt;
        private float revealProgress;
        private float dissolveProgress;
        private LinkState state = LinkState.Hidden;

        private enum LinkState
        {
            Hidden,
            Appearing,
            Visible,
            Dissolving
        }

        private void Awake()
        {
            controller = GetComponent<BossController>();
            EnsureLines();
            ResolvePlayer(true);
        }

        private void OnEnable()
        {
            BindController();
            ResolvePlayer(true);
            if (controller != null &&
                controller.IsHealthSystemActive)
            {
                BeginAppear();
            }
            else
            {
                HideImmediate();
            }
        }

        private void LateUpdate()
        {
            UpdateTransition();
            ResolvePlayer(false);
            if (player == null || state == LinkState.Hidden)
            {
                SetVisible(false);
                return;
            }

            DrawLink();
        }

        private void OnDisable()
        {
            UnbindController();
            state = LinkState.Hidden;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index] != null)
                {
                    Destroy(lines[index].gameObject);
                }
            }
            for (int index = 0;
                 index < runtimeMaterials.Length;
                 index++)
            {
                if (runtimeMaterials[index] != null)
                {
                    Destroy(runtimeMaterials[index]);
                    runtimeMaterials[index] = null;
                }
            }
        }

        private void BindController()
        {
            if (controller == null)
            {
                controller = GetComponent<BossController>();
            }
            if (controller == null)
            {
                return;
            }

            controller.HealthSystemActivated -=
                HandleHealthSystemActivated;
            controller.HealthSystemActivated +=
                HandleHealthSystemActivated;
            controller.Died -= HandleBossDied;
            controller.Died += HandleBossDied;
        }

        private void UnbindController()
        {
            if (controller == null)
            {
                return;
            }

            controller.HealthSystemActivated -=
                HandleHealthSystemActivated;
            controller.Died -= HandleBossDied;
        }

        private void HandleHealthSystemActivated()
        {
            BeginAppear();
        }

        private void HandleBossDied()
        {
            BeginDissolve();
        }

        private void BeginAppear()
        {
            state = LinkState.Appearing;
            transitionStartedAt = Time.time;
            revealProgress = 0f;
            dissolveProgress = 0f;
        }

        private void BeginDissolve()
        {
            if (state == LinkState.Hidden)
            {
                return;
            }

            state = LinkState.Dissolving;
            transitionStartedAt = Time.time;
        }

        private void UpdateTransition()
        {
            if (state == LinkState.Appearing)
            {
                revealProgress = generationDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        (Time.time - transitionStartedAt) /
                        generationDuration);
                revealProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    revealProgress);
                if (revealProgress >= 1f)
                {
                    state = LinkState.Visible;
                }
            }
            else if (state == LinkState.Dissolving)
            {
                dissolveProgress = dissolveDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        (Time.time - transitionStartedAt) /
                        dissolveDuration);
                dissolveProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    dissolveProgress);
                if (dissolveProgress >= 1f)
                {
                    HideImmediate();
                }
            }
        }

        private void HideImmediate()
        {
            state = LinkState.Hidden;
            revealProgress = 0f;
            dissolveProgress = 0f;
            SetVisible(false);
        }

        private void ResolvePlayer(bool force)
        {
            if (!force &&
                player != null &&
                player.isActiveAndEnabled &&
                player.CompareTag(playerTag))
            {
                return;
            }

            if (!force &&
                Time.unscaledTime < nextPlayerLookupTime)
            {
                return;
            }

            nextPlayerLookupTime =
                Time.unscaledTime + .35f;
            player = null;
            if (string.IsNullOrWhiteSpace(playerTag))
            {
                return;
            }

            GameObject found;
            try
            {
                found = GameObject.FindGameObjectWithTag(
                    playerTag);
            }
            catch (UnityException)
            {
                return;
            }

            player = found != null
                ? found.GetComponent<PlayerController>()
                : null;
        }

        private void EnsureLines()
        {
            styleMaterials[0] = redMaterial;
            styleMaterials[1] = blueMaterial;
            styleMaterials[2] = greenMaterial;
            styleTints[0] = redTint;
            styleTints[1] = blueTint;
            styleTints[2] = greenTint;
            string[] names =
            {
                "__BossPlayerLink_Red",
                "__BossPlayerLink_Blue",
                "__BossPlayerLink_Green"
            };
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index] == null)
                {
                    GameObject child = new GameObject(names[index]);
                    child.transform.SetParent(transform, false);
                    LineRenderer line =
                        child.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.alignment = LineAlignment.View;
                    line.textureMode = LineTextureMode.Stretch;
                    line.numCapVertices = 0;
                    line.numCornerVertices = 0;
                    line.shadowCastingMode =
                        ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    line.sortingLayerName = sortingLayerName;
                    line.sortingOrder = sortingOrder;
                    lines[index] = line;
                }

                Material material = styleMaterials[index] != null
                    ? styleMaterials[index]
                    : styleMaterials[0];
                if (material != null &&
                    runtimeMaterials[index] == null)
                {
                    runtimeMaterials[index] =
                        new Material(material)
                        {
                            name = material.name +
                                   " · Boss Link " +
                                   (index + 1),
                            hideFlags = HideFlags.DontSave
                        };
                    runtimeMaterials[index].SetFloat(
                        StyleId,
                        index + 1f);
                    runtimeMaterials[index].SetColor(
                        LinkColorId,
                        styleTints[index]);
                }

                lines[index].sharedMaterial =
                    runtimeMaterials[index];
                lines[index].positionCount =
                    Mathf.Clamp(curveSegments, 2, 32) + 1;
            }
        }

        private void DrawLink()
        {
            EnsureLines();
            Vector3 source =
                transform.position + bossOffset;
            Vector3 destination =
                player.transform.position + playerOffset;
            source.z += cameraFacingOffset;
            destination.z += cameraFacingOffset;
            Vector3 delta = destination - source;
            float distance =
                delta.magnitude /
                Mathf.Max(
                    .001f,
                    GridCellSizeUtility.Resolve(this));
            float normalized = Mathf.InverseLerp(
                maximumDistanceBlocks,
                minimumDistanceBlocks,
                distance);
            normalized = Mathf.Pow(
                Mathf.Clamp01(normalized),
                Mathf.Max(.25f, lerpSharpness));
            float opacity = Mathf.Lerp(
                farOpacity,
                nearOpacity,
                normalized);
            float thickness = Mathf.Lerp(
                farLineThickness,
                nearLineThickness,
                normalized);
            int segments =
                Mathf.Clamp(curveSegments, 2, 32);
            if (points == null ||
                points.Length != segments + 1)
            {
                points = new Vector3[segments + 1];
            }

            Vector3 side = delta.sqrMagnitude > .0001f
                ? Vector3.Cross(
                    Vector3.forward,
                    delta.normalized)
                : Vector3.right;
            float length = 0f;
            for (int style = 0; style < lines.Length; style++)
            {
                LineRenderer line = lines[style];
                if (line == null ||
                    line.sharedMaterial == null)
                {
                    line.enabled = false;
                    continue;
                }

                Vector3 styleOffset =
                    side *
                    (style - 1f) *
                    thickness *
                    styleSeparationRatio;
                for (int segment = 0;
                     segment <= segments;
                     segment++)
                {
                    float t = (float)segment / segments;
                    float envelope =
                        Mathf.Sin(Mathf.PI * t);
                    float wave =
                        Mathf.Sin(
                            t * Mathf.PI * 6f +
                            Time.time * 2.2f +
                            style * 1.7f) *
                        waveHeight *
                        envelope;
                    points[segment] =
                        Vector3.Lerp(source, destination, t) +
                        styleOffset * envelope +
                        side *
                        (arcHeight + wave) *
                        envelope;
                }

                if (style == 0)
                {
                    length = 0f;
                    for (int pointIndex = 1;
                         pointIndex < points.Length;
                         pointIndex++)
                    {
                        length += Vector3.Distance(
                            points[pointIndex - 1],
                            points[pointIndex]);
                    }
                }

                line.positionCount = points.Length;
                line.SetPositions(points);
                line.widthMultiplier =
                    Mathf.Max(.01f, thickness * 3f);
                Material runtime = runtimeMaterials[style];
                runtime.SetColor(
                    LinkColorId,
                    styleTints[style]);
                runtime.SetFloat(
                    LineThicknessId,
                    thickness);
                runtime.SetFloat(FeasibleId, 1f);
                runtime.SetFloat(OpacityId, opacity);
                runtime.SetFloat(NoiseStrengthId, .6f);
                runtime.SetFloat(EffectSpeedId, 5.5f);
                runtime.SetFloat(
                    StyleId,
                    style + 1f);
                runtime.SetFloat(
                    RevealProgressId,
                    revealProgress);
                runtime.SetFloat(
                    DissolveProgressId,
                    dissolveProgress);
                runtime.SetFloat(
                    LineLengthId,
                    Mathf.Max(.01f, length));
                line.enabled = true;
            }
        }

        private void SetVisible(bool visible)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index] != null)
                {
                    lines[index].enabled = visible;
                }
            }
        }
    }
}
