using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Project.BossLogic
{
    [Serializable]
    public sealed class BossStateEvent : UnityEvent<BossStateId>
    {
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController))]
    public sealed class BossAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private bool driveAnimator = true;
        [SerializeField]
        [Tooltip("开启后，死亡动画等待 Boss 死亡 Timeline 在第 1 秒调用 PlayDeathAnimationNow。")]
        private bool waitForPresentationDeathTrigger;
        [SerializeField] private string chaseBoolParameter = "Chasing";
        [SerializeField] private string hitTriggerParameter = "Hit";
        [SerializeField] private string deathTriggerParameter = "Death";
        [SerializeField] private string jumpTriggerParameter = "Jump";
        [SerializeField]
        [Tooltip("Animator 没有死亡状态时，演出等待的默认秒数。")]
        [Min(0f)]
        private float deathAnimationFallbackSeconds = 2f;
        [SerializeField]
        private string deathStateName = "Death";
        [SerializeField] private BossStateEvent stateChanged =
            new BossStateEvent();
        [SerializeField] private UnityEvent hitStarted =
            new UnityEvent();
        [SerializeField] private UnityEvent deathStarted =
            new UnityEvent();
        [SerializeField] private UnityEvent jumpStarted =
            new UnityEvent();

        [Header("RGB 死亡溶解")]
        [SerializeField] private bool enableRgbDeathDissolve = true;
        [SerializeField] private Material rgbDeathDissolveMaterial;
        [SerializeField, Min(.05f)]
        private float rgbDeathDissolveSpeed = .65f;
        [SerializeField, Min(0f)]
        private float rgbDeathHorizontalScatter = .75f;
        [SerializeField]
        private bool loopRgbDeathDissolvePreview;
        [SerializeField, Min(0f)]
        private float rgbDeathDissolvePreviewLoopInterval = .5f;

        private const int DissolveMeshColumns = 20;
        private const int DissolveMeshRows = 20;
        private const string DissolveMaterialResourcePath =
            "BossDeathRgbDissolve";
        private static readonly int MainTextureId =
            Shader.PropertyToID("_MainTex");
        private static readonly int DissolveProgressId =
            Shader.PropertyToID("_DissolveProgress");
        private static readonly int HorizontalScatterId =
            Shader.PropertyToID("_HorizontalScatter");

        private sealed class DeathDissolveRendererState
        {
            public SpriteRenderer source;
            public bool sourceWasEnabled;
            public GameObject fragmentsObject;
            public Mesh fragmentsMesh;
            public MeshRenderer fragmentsRenderer;
            public Material fragmentsMaterial;
            public Bounds originalMeshBounds;
        }

        private BossController controller;
        private SpriteRenderer[] deathDissolveSourceRenderers =
            Array.Empty<SpriteRenderer>();
        private bool deathAnimationTriggered;
        private float deathAnimationStartedAt;
        private readonly List<DeathDissolveRendererState>
            deathDissolveRenderers =
                new List<DeathDissolveRendererState>();
        private bool dissolveMaterialLookupAttempted;
        private bool deathDissolveActive;
        private bool deathDissolvePreview;
        private float deathDissolveStartedAt;
        private float previewRestoreAt = -1f;
        private Rigidbody previewFrozenBody;
        private bool previewBodyStateCaptured;
        private bool previewBodyWasKinematic;
        private bool previewBodyUsedGravity;
        private Vector3 previewBodyLinearVelocity;
        private Vector3 previewBodyAngularVelocity;
        private float nextRgbDeathDissolvePreviewAt = -1f;

        public event Action<BossStateId, BossStateId> StateForwarded;
        public float DeathAnimationDurationSeconds
        {
            get
            {
                if (CanUseRgbDeathDissolve())
                {
                    return 1f / Mathf.Max(
                        .05f,
                        rgbDeathDissolveSpeed);
                }

                if (animator != null &&
                    !string.IsNullOrWhiteSpace(
                        deathStateName))
                {
                    AnimatorStateInfo info =
                        animator.GetCurrentAnimatorStateInfo(0);
                    if (info.IsName(deathStateName) &&
                        info.length > 0f)
                    {
                        return info.length;
                    }
                }

                return Mathf.Max(
                    0f,
                    deathAnimationFallbackSeconds);
            }
        }

        public float DeathAnimationElapsedSeconds =>
            deathAnimationTriggered
                ? Mathf.Max(
                    0f,
                    Time.time - deathAnimationStartedAt)
                : 0f;

        public float DeathAnimationProgress
        {
            get
            {
                float duration =
                    DeathAnimationDurationSeconds;
                return duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        DeathAnimationElapsedSeconds /
                        duration);
            }
        }

        public bool IsDeathAnimationComplete =>
            deathAnimationTriggered &&
            DeathAnimationProgress >= 1f;

        private void OnEnable()
        {
            controller = GetComponent<BossController>();
            deathDissolveSourceRenderers =
                GetComponentsInChildren<SpriteRenderer>(true);
            if (controller != null)
            {
                controller.StateChanged += OnStateChanged;
                ApplyState(controller.State);
                if (controller.State == BossStateId.Death &&
                    !waitForPresentationDeathTrigger)
                {
                    TriggerDeathAnimation();
                }
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.StateChanged -= OnStateChanged;
            }

            RestoreDeathDissolve();
        }

        private void OnDestroy()
        {
            RestoreDeathDissolve();
        }

        private void Update()
        {
            if (deathDissolveActive)
            {
                UpdateDeathDissolve();
                return;
            }

            if (deathDissolvePreview &&
                previewRestoreAt >= 0f &&
                Time.time >= previewRestoreAt)
            {
                RestoreDeathDissolve();
                nextRgbDeathDissolvePreviewAt =
                    loopRgbDeathDissolvePreview
                        ? Time.time + Mathf.Max(
                            0f,
                            rgbDeathDissolvePreviewLoopInterval)
                        : -1f;
                return;
            }

            if (!Application.isPlaying ||
                !loopRgbDeathDissolvePreview ||
                deathDissolvePreview ||
                (controller != null && controller.IsDead) ||
                (nextRgbDeathDissolvePreviewAt >= 0f &&
                 Time.time < nextRgbDeathDissolvePreviewAt))
            {
                return;
            }

            nextRgbDeathDissolvePreviewAt = Time.time +
                Mathf.Max(.1f, rgbDeathDissolvePreviewLoopInterval);
            PreviewRgbDeathDissolve();
        }

        public void SetAnimator(Animator value)
        {
            animator = value;
            if (isActiveAndEnabled && controller != null)
            {
                ApplyState(controller.State);
            }
        }

        private void OnStateChanged(
            BossStateId previous,
            BossStateId next)
        {
            if (next != BossStateId.Death)
            {
                RestoreDeathDissolve();
            }

            ApplyState(next);
            StateForwarded?.Invoke(previous, next);
            stateChanged?.Invoke(next);
            if (next == BossStateId.Hit)
            {
                hitStarted?.Invoke();
            }
            else if (next == BossStateId.Death)
            {
                if (!waitForPresentationDeathTrigger)
                {
                    TriggerDeathAnimation();
                }
            }
            else if (next == BossStateId.Jump)
            {
                jumpStarted?.Invoke();
            }
        }

        private void ApplyState(BossStateId state)
        {
            if (state != BossStateId.Death)
            {
                deathAnimationTriggered = false;
            }

            if (!driveAnimator || animator == null)
            {
                return;
            }

            SetBool(chaseBoolParameter, state == BossStateId.Chase);
            if (state == BossStateId.Hit)
            {
                SetTrigger(hitTriggerParameter);
            }
            else if (state == BossStateId.Jump)
            {
                SetTrigger(jumpTriggerParameter);
            }
        }

        public void PlayDeathAnimationNow()
        {
            if (controller == null)
            {
                controller = GetComponent<BossController>();
            }

            if (controller == null || !controller.IsDead)
            {
                return;
            }

            TriggerDeathAnimation();
        }

        private void TriggerDeathAnimation()
        {
            if (deathAnimationTriggered)
            {
                return;
            }

            deathAnimationTriggered = true;
            deathAnimationStartedAt = Time.time;
            if (driveAnimator && animator != null)
            {
                SetTrigger(deathTriggerParameter);
            }
            deathStarted?.Invoke();
            BeginDeathDissolve(false);
        }

        /// <summary>
        /// Inspector preview entry point. It plays only the visual effect and
        /// restores the source sprite after the preview finishes.
        /// </summary>
        public void PreviewRgbDeathDissolve()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "请进入 Play Mode 后再预览 Boss RGB 死亡溶解。",
                    this);
                return;
            }

            if (!enableRgbDeathDissolve)
            {
                Debug.LogWarning(
                    "请先启用 RGB 死亡溶解。",
                    this);
                return;
            }

            RestoreDeathDissolve();
            if (!BeginDeathDissolve(true))
            {
                Debug.LogWarning(
                    "没有找到可用于预览的 Boss SpriteRenderer 或死亡溶解材质。",
                    this);
            }
        }

        private bool BeginDeathDissolve(bool preview)
        {
            if (!preview)
            {
                RestoreDeathDissolve();
            }

            if (!enableRgbDeathDissolve)
            {
                return false;
            }

            Material template = ResolveDeathDissolveMaterial();
            if (template == null ||
                !template.HasProperty(DissolveProgressId) ||
                !template.HasProperty(HorizontalScatterId))
            {
                return false;
            }

            deathDissolveSourceRenderers =
                GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer[] sourceRenderers =
                deathDissolveSourceRenderers;
            for (int index = 0;
                 index < sourceRenderers.Length;
                 index++)
            {
                SpriteRenderer source = sourceRenderers[index];
                if (source == null || source.sprite == null)
                {
                    continue;
                }

                DeathDissolveRendererState state =
                    CreateDeathDissolveRenderer(
                        source,
                        template);
                if (state == null)
                {
                    continue;
                }

                deathDissolveRenderers.Add(state);
                source.enabled = false;
            }

            if (deathDissolveRenderers.Count == 0)
            {
                return false;
            }

            if (preview)
            {
                FreezeBodyForDissolvePreview();
            }

            deathDissolvePreview = preview;
            deathDissolveActive = true;
            deathDissolveStartedAt = Time.time;
            previewRestoreAt = -1f;
            ApplyDeathDissolveProgress(0f);
            return true;
        }

        private DeathDissolveRendererState
            CreateDeathDissolveRenderer(
                SpriteRenderer source,
                Material template)
        {
            Sprite sprite = source.sprite;
            Texture2D texture = sprite != null
                ? sprite.texture
                : null;
            if (texture == null)
            {
                return null;
            }

            Mesh mesh = CreateDissolveMesh(
                sprite,
                source.color,
                source.flipX,
                source.flipY);
            if (mesh == null)
            {
                return null;
            }

            var fragmentObject = new GameObject(
                "Boss RGB Death Fragments");
            fragmentObject.transform.SetParent(null, false);
            fragmentObject.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation);
            fragmentObject.transform.localScale =
                source.transform.lossyScale;
            fragmentObject.layer = source.gameObject.layer;

            MeshFilter filter =
                fragmentObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer =
                fragmentObject.AddComponent<MeshRenderer>();
            Material material = new Material(template)
            {
                name = template.name + " (Boss Runtime)",
                hideFlags = HideFlags.DontSave
            };
            material.SetTexture(MainTextureId, texture);
            material.SetFloat(DissolveProgressId, 0f);
            material.SetFloat(
                HorizontalScatterId,
                Mathf.Max(0f, rgbDeathHorizontalScatter));
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.renderingLayerMask = source.renderingLayerMask;
            renderer.rendererPriority = source.rendererPriority;

            Bounds originalBounds = mesh.bounds;
            Bounds expandedBounds = originalBounds;
            expandedBounds.Expand(
                new Vector3(
                    Mathf.Max(0f, rgbDeathHorizontalScatter) * 2f,
                    0f,
                    0f));
            mesh.bounds = expandedBounds;

            return new DeathDissolveRendererState
            {
                source = source,
                sourceWasEnabled = source.enabled,
                fragmentsObject = fragmentObject,
                fragmentsMesh = mesh,
                fragmentsRenderer = renderer,
                fragmentsMaterial = material,
                originalMeshBounds = originalBounds
            };
        }

        private static Mesh CreateDissolveMesh(
            Sprite sprite,
            Color tint,
            bool flipX,
            bool flipY)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }

            Bounds spriteBounds = sprite.bounds;
            Rect uvBounds = GetSpriteUvBounds(sprite);
            float uMin = uvBounds.xMin;
            float uMax = uvBounds.xMax;
            float vMin = uvBounds.yMin;
            float vMax = uvBounds.yMax;
            int vertexCount =
                DissolveMeshColumns *
                DissolveMeshRows *
                4;

            var vertices = new List<Vector3>(vertexCount);
            var uvs = new List<Vector2>(vertexCount);
            var colors = new List<Color>(vertexCount);
            var cellData = new List<Vector2>(vertexCount);
            var scatterData = new List<Vector2>(vertexCount);
            var cellShape = new List<Vector4>(vertexCount);
            var triangles = new List<int>(
                DissolveMeshColumns * DissolveMeshRows * 6);
            float cellWidth =
                spriteBounds.size.x / DissolveMeshColumns;
            float cellHeight =
                spriteBounds.size.y / DissolveMeshRows;

            for (int row = 0;
                 row < DissolveMeshRows;
                 row++)
            {
                float y0 = Mathf.Lerp(
                    spriteBounds.min.y,
                    spriteBounds.max.y,
                    row / (float)DissolveMeshRows);
                float y1 = Mathf.Lerp(
                    spriteBounds.min.y,
                    spriteBounds.max.y,
                    (row + 1f) / DissolveMeshRows);
                float v0 = Mathf.Lerp(
                    vMin,
                    vMax,
                    row / (float)DissolveMeshRows);
                float v1 = Mathf.Lerp(
                    vMin,
                    vMax,
                    (row + 1f) / DissolveMeshRows);
                if (flipY)
                {
                    float swap = v0;
                    v0 = v1;
                    v1 = swap;
                }

                for (int column = 0;
                     column < DissolveMeshColumns;
                     column++)
                {
                    float x0 = Mathf.Lerp(
                        spriteBounds.min.x,
                        spriteBounds.max.x,
                        column / (float)DissolveMeshColumns);
                    float x1 = Mathf.Lerp(
                        spriteBounds.min.x,
                        spriteBounds.max.x,
                        (column + 1f) / DissolveMeshColumns);
                    float u0 = Mathf.Lerp(
                        uMin,
                        uMax,
                        column / (float)DissolveMeshColumns);
                    float u1 = Mathf.Lerp(
                        uMin,
                        uMax,
                        (column + 1f) / DissolveMeshColumns);
                    if (flipX)
                    {
                        float mirroredX0 = -x1;
                        float mirroredX1 = -x0;
                        x0 = mirroredX0;
                        x1 = mirroredX1;
                    }

                    if (flipY)
                    {
                        float mirroredY0 = -y1;
                        float mirroredY1 = -y0;
                        y0 = mirroredY0;
                        y1 = mirroredY1;
                    }

                    float randomOrder = Hash01(
                        column,
                        row,
                        17);
                    float dissolveOrder = Mathf.Clamp01(
                        row / (float)DissolveMeshRows * .66f +
                        randomOrder * .34f);
                    float colorChannel = Hash01(
                        column,
                        row,
                        43);
                    float horizontalOffset =
                        Hash01(column, row, 71) * 2f - 1f;

                    int first = vertices.Count;
                    vertices.Add(new Vector3(x0, y0, 0f));
                    vertices.Add(new Vector3(x0, y1, 0f));
                    vertices.Add(new Vector3(x1, y1, 0f));
                    vertices.Add(new Vector3(x1, y0, 0f));

                    uvs.Add(new Vector2(u0, v0));
                    uvs.Add(new Vector2(u0, v1));
                    uvs.Add(new Vector2(u1, v1));
                    uvs.Add(new Vector2(u1, v0));
                    for (int vertex = 0; vertex < 4; vertex++)
                    {
                        colors.Add(tint);
                        cellData.Add(new Vector2(
                            dissolveOrder,
                            colorChannel));
                        scatterData.Add(new Vector2(
                            horizontalOffset,
                            0f));
                        float cornerX =
                            vertex == 2 || vertex == 3 ? 1f : 0f;
                        float cornerY =
                            vertex == 1 || vertex == 2 ? 1f : 0f;
                        cellShape.Add(new Vector4(
                            cornerX,
                            cornerY,
                            cellWidth,
                            cellHeight));
                    }

                    triangles.Add(first);
                    triangles.Add(first + 1);
                    triangles.Add(first + 2);
                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 3);
                }
            }

            var mesh = new Mesh
            {
                name = "Boss RGB Death Fragments",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetUVs(1, cellData);
            mesh.SetUVs(2, scatterData);
            mesh.SetUVs(3, cellShape);
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Rect GetSpriteUvBounds(Sprite sprite)
        {
            Texture texture = sprite.texture;
            try
            {
                Rect pixelRect = sprite.textureRect;
                return new Rect(
                    pixelRect.xMin / texture.width,
                    pixelRect.yMin / texture.height,
                    pixelRect.width / texture.width,
                    pixelRect.height / texture.height);
            }
            catch (Exception)
            {
                Vector2[] spriteUvs = sprite.uv;
                if (spriteUvs == null || spriteUvs.Length == 0)
                {
                    return new Rect(0f, 0f, 1f, 1f);
                }

                float minU = spriteUvs[0].x;
                float maxU = spriteUvs[0].x;
                float minV = spriteUvs[0].y;
                float maxV = spriteUvs[0].y;
                for (int index = 1;
                     index < spriteUvs.Length;
                     index++)
                {
                    minU = Mathf.Min(minU, spriteUvs[index].x);
                    maxU = Mathf.Max(maxU, spriteUvs[index].x);
                    minV = Mathf.Min(minV, spriteUvs[index].y);
                    maxV = Mathf.Max(maxV, spriteUvs[index].y);
                }

                return Rect.MinMaxRect(minU, minV, maxU, maxV);
            }
        }

        private static float Hash01(
            int x,
            int y,
            int salt)
        {
            float value = Mathf.Sin(
                x * 127.1f +
                y * 311.7f +
                salt * 74.7f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        private void UpdateDeathDissolve()
        {
            float speed = Mathf.Max(.05f, rgbDeathDissolveSpeed);
            float progress = Mathf.Clamp01(
                (Time.time - deathDissolveStartedAt) * speed);
            ApplyDeathDissolveProgress(progress);
            if (progress < 1f)
            {
                return;
            }

            deathDissolveActive = false;
            for (int index = 0;
                 index < deathDissolveRenderers.Count;
                 index++)
            {
                DeathDissolveRendererState state =
                    deathDissolveRenderers[index];
                if (state.fragmentsRenderer != null)
                {
                    state.fragmentsRenderer.enabled = false;
                }
            }

            if (deathDissolvePreview)
            {
                previewRestoreAt = Time.time + .45f;
            }
        }

        private void ApplyDeathDissolveProgress(float progress)
        {
            float scatter =
                Mathf.Max(0f, rgbDeathHorizontalScatter);
            for (int index = 0;
                 index < deathDissolveRenderers.Count;
                 index++)
            {
                DeathDissolveRendererState state =
                    deathDissolveRenderers[index];
                if (state.fragmentsMaterial != null)
                {
                    state.fragmentsMaterial.SetFloat(
                        DissolveProgressId,
                        progress);
                    state.fragmentsMaterial.SetFloat(
                        HorizontalScatterId,
                        scatter);
                }

                if (state.fragmentsMesh != null)
                {
                    Bounds bounds = state.originalMeshBounds;
                    bounds.Expand(new Vector3(scatter * 2f, 0f, 0f));
                    state.fragmentsMesh.bounds = bounds;
                }
            }
        }

        private void RestoreDeathDissolve()
        {
            deathDissolveActive = false;
            bool restorePreviewBody = deathDissolvePreview;
            deathDissolvePreview = false;
            previewRestoreAt = -1f;
            for (int index = deathDissolveRenderers.Count - 1;
                 index >= 0;
                 index--)
            {
                DeathDissolveRendererState state =
                    deathDissolveRenderers[index];
                if (state.source != null)
                {
                    state.source.enabled = state.sourceWasEnabled;
                }

                if (state.fragmentsRenderer != null)
                {
                    state.fragmentsRenderer.enabled = false;
                }

                DestroyRuntimeObject(state.fragmentsObject);
                DestroyRuntimeObject(state.fragmentsMesh);
                DestroyRuntimeObject(state.fragmentsMaterial);
            }

            deathDissolveRenderers.Clear();
            if (restorePreviewBody)
            {
                RestorePreviewBody();
            }
        }

        private void FreezeBodyForDissolvePreview()
        {
            if (previewBodyStateCaptured)
            {
                return;
            }

            previewFrozenBody = controller != null
                ? controller.Body
                : GetComponent<Rigidbody>();
            if (previewFrozenBody == null)
            {
                return;
            }

            previewBodyWasKinematic = previewFrozenBody.isKinematic;
            previewBodyUsedGravity = previewFrozenBody.useGravity;
            previewBodyLinearVelocity = previewFrozenBody.linearVelocity;
            previewBodyAngularVelocity = previewFrozenBody.angularVelocity;
            previewBodyStateCaptured = true;

            if (!previewBodyWasKinematic)
            {
                previewFrozenBody.linearVelocity = Vector3.zero;
                previewFrozenBody.angularVelocity = Vector3.zero;
            }

            previewFrozenBody.useGravity = false;
            previewFrozenBody.isKinematic = true;
        }

        private void RestorePreviewBody()
        {
            if (!previewBodyStateCaptured || previewFrozenBody == null)
            {
                previewBodyStateCaptured = false;
                previewFrozenBody = null;
                return;
            }

            previewFrozenBody.isKinematic = previewBodyWasKinematic;
            previewFrozenBody.useGravity = previewBodyUsedGravity;
            if (!previewBodyWasKinematic)
            {
                previewFrozenBody.linearVelocity = previewBodyLinearVelocity;
                previewFrozenBody.angularVelocity = previewBodyAngularVelocity;
            }

            previewBodyStateCaptured = false;
            previewFrozenBody = null;
        }

        private static void DestroyRuntimeObject(
            UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }

        private bool CanUseRgbDeathDissolve()
        {
            if (!enableRgbDeathDissolve)
            {
                return false;
            }

            Material material = ResolveDeathDissolveMaterial();
            if (material == null ||
                !material.HasProperty(DissolveProgressId) ||
                !material.HasProperty(HorizontalScatterId))
            {
                return false;
            }

            if (deathDissolveSourceRenderers == null)
            {
                deathDissolveSourceRenderers =
                    GetComponentsInChildren<SpriteRenderer>(true);
            }

            for (int index = 0;
                 index < deathDissolveSourceRenderers.Length;
                 index++)
            {
                SpriteRenderer source =
                    deathDissolveSourceRenderers[index];
                if (source != null &&
                    source.sprite != null &&
                    source.sprite.texture != null)
                {
                    return true;
                }
            }

            return false;
        }

        private Material ResolveDeathDissolveMaterial()
        {
            if (rgbDeathDissolveMaterial != null)
            {
                return rgbDeathDissolveMaterial;
            }

            if (!dissolveMaterialLookupAttempted)
            {
                dissolveMaterialLookupAttempted = true;
                rgbDeathDissolveMaterial =
                    Resources.Load<Material>(
                        DissolveMaterialResourcePath);
            }

            return rgbDeathDissolveMaterial;
        }

        private void SetBool(string parameter, bool value)
        {
            if (!string.IsNullOrWhiteSpace(parameter) &&
                HasParameter(parameter, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(parameter, value);
            }
        }

        private void SetTrigger(string parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter) &&
                HasParameter(parameter, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(parameter);
            }
        }

        private bool HasParameter(
            string parameter,
            AnimatorControllerParameterType type)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters =
                animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter candidate = parameters[index];
                if (candidate.type == type &&
                    string.Equals(
                        candidate.name,
                        parameter,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
