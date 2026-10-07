using System.Collections;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.ColorBlocks
{
    public enum UniversalColorBlockFadeMode
    {
        [InspectorName("持续染色")] Persistent = 0,
        [InspectorName("过一会褪色")] FadeAfterDelay = 1
    }

    /// <summary>
    /// Fixed early-game color proxy. It colors itself with a solid color and
    /// broadcasts the same color command to its four cardinal neighbors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UniversalColorBlock : MonoBehaviour,
        IInteractionTarget,
        IColorApplicationTarget,
        IRoomColorResettable
    {
        private static readonly HashSet<int> ActiveBroadcasts =
            new HashSet<int>();
        private static readonly Vector3[] CardinalDirections =
        {
            Vector3.left,
            Vector3.right,
            Vector3.up,
            Vector3.down
        };

        [SerializeField, Tooltip("万能方块自身的状态渲染器；会显示当前选中颜色的纯色。")]
        private Renderer statusRenderer;
        [SerializeField, Tooltip("纯色持续保留，或延迟后自动恢复中性色。")]
        private UniversalColorBlockFadeMode fadeMode =
            UniversalColorBlockFadeMode.Persistent;
        [SerializeField, Min(0f), Tooltip("选择“过一会褪色”时，等待多少秒后恢复中性色。")]
        private float fadeDelaySeconds = 3f;

        private readonly Collider[] neighborBuffer = new Collider[64];
        private Material runtimeStatusMaterial;
        private Coroutine fadeRoutine;
        private float fadeRemainingSeconds = -1f;
        private string currentColorId = string.Empty;
        private int lastBroadcastTargetCount;

        public string CurrentColorId => currentColorId;
        public UniversalColorBlockFadeMode FadeMode => fadeMode;
        public float FadeDelaySeconds => fadeDelaySeconds;
        public float FadeRemainingSeconds => fadeRemainingSeconds;
        public int LastBroadcastTargetCount =>
            lastBroadcastTargetCount;

        private void Awake()
        {
            ResolveStatusRenderer();
        }

        private void OnEnable()
        {
            ResolveStatusRenderer();
            EventMgr.OnRoomColorReset += ResetForRoom;
        }

        private void OnDisable()
        {
            EventMgr.OnRoomColorReset -= ResetForRoom;
            StopFadeRoutine();
        }

        private void OnDestroy()
        {
            if (runtimeStatusMaterial != null)
            {
                Destroy(runtimeStatusMaterial);
                runtimeStatusMaterial = null;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled &&
                   ColorRuntimeService.Existing != null;
        }

        public bool TryInteract(GameObject interactor)
        {
            Project.Player.PlayerColorWheel wheel =
                interactor != null
                    ? interactor.GetComponentInParent<
                        Project.Player.PlayerColorWheel>()
                    : null;
            if (wheel == null || !wheel.HasSelectedColor)
            {
                return false;
            }

            return ApplyColor(wheel.SelectedColorId, interactor);
        }

        public bool CanApplyColor(
            string colorId,
            GameObject actor)
        {
            return isActiveAndEnabled &&
                   ColorRuntimeService.Existing != null &&
                   !string.IsNullOrWhiteSpace(colorId) &&
                   ColorRuntimeService.Existing.IsUnlocked(colorId);
        }

        public bool ApplyColor(
            string colorId,
            GameObject actor)
        {
            if (!CanApplyColor(colorId, actor))
            {
                return false;
            }

            int id = GetInstanceID();
            if (!ActiveBroadcasts.Add(id))
            {
                return true;
            }

            try
            {
                currentColorId = colorId;
                ApplyStatusColor(colorId);
                lastBroadcastTargetCount =
                    BroadcastToCardinalNeighbors(colorId, actor);
                ScheduleFade();
                return true;
            }
            finally
            {
                ActiveBroadcasts.Remove(id);
            }
        }

        public void ResetForRoom()
        {
            StopFadeRoutine();
            currentColorId = string.Empty;
            lastBroadcastTargetCount = 0;
            SetPureStatusColor(Color.white);
        }

        public void ClearColor()
        {
            StopFadeRoutine();
            currentColorId = string.Empty;
            SetPureStatusColor(Color.white);
        }

        private void ResolveStatusRenderer()
        {
            if (statusRenderer == null)
            {
                statusRenderer = GetComponent<Renderer>();
            }
        }

        private void ApplyStatusColor(string colorId)
        {
            ColorTypeDefinition definition =
                ColorRuntimeService.Existing?.Catalog?.Find(colorId);
            if (definition != null)
            {
                SetPureStatusColor(definition.swatch);
            }
        }

        private void SetPureStatusColor(Color color)
        {
            if (statusRenderer == null)
            {
                return;
            }

            if (runtimeStatusMaterial == null)
            {
                Material source =
                    ColorRuntimeService.Existing?.Catalog?.NeutralMaterial;
                if (source == null)
                {
                    return;
                }

                runtimeStatusMaterial = new Material(source)
                {
                    name = $"{name} Pure Color"
                };
            }

            runtimeStatusMaterial.SetColor("_BaseColor", color);
            runtimeStatusMaterial.SetColor("_OutlineColor", color);
            runtimeStatusMaterial.SetFloat("_BorderWidth", 0f);
            statusRenderer.sharedMaterial = runtimeStatusMaterial;
        }

        private void ScheduleFade()
        {
            StopFadeRoutine();
            if (fadeMode != UniversalColorBlockFadeMode.FadeAfterDelay)
            {
                return;
            }

            fadeRoutine = StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            fadeRemainingSeconds = Mathf.Max(0f, fadeDelaySeconds);
            while (fadeRemainingSeconds > 0f)
            {
                yield return null;
                fadeRemainingSeconds -= Time.deltaTime;
            }

            fadeRemainingSeconds = -1f;
            fadeRoutine = null;
            currentColorId = string.Empty;
            SetPureStatusColor(Color.white);
        }

        private void StopFadeRoutine()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            fadeRemainingSeconds = -1f;
        }

        private int BroadcastToCardinalNeighbors(
            string colorId,
            GameObject actor)
        {
            float cellSize = GridCellSizeUtility.Resolve(this);
            Vector3 halfExtents =
                Vector3.one * (cellSize * .45f);
            var visited = new HashSet<int>();
            int appliedCount = 0;

            for (int directionIndex = 0;
                 directionIndex < CardinalDirections.Length;
                 directionIndex++)
            {
                Vector3 center =
                    transform.position +
                    CardinalDirections[directionIndex] * cellSize;
                int count = Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    neighborBuffer,
                    Quaternion.identity,
                    ~0,
                    QueryTriggerInteraction.Collide);

                for (int index = 0; index < count; index++)
                {
                    IColorApplicationTarget target =
                        ResolveTarget(neighborBuffer[index]);
                    if (target == null ||
                        ReferenceEquals(target, this))
                    {
                        continue;
                    }

                    Component component = target as Component;
                    int targetId = component != null
                        ? component.GetInstanceID()
                        : target.GetHashCode();
                    if (!visited.Add(targetId) ||
                        !target.CanApplyColor(colorId, actor))
                    {
                        continue;
                    }

                    if (target.ApplyColor(colorId, actor))
                    {
                        appliedCount++;
                    }
                }
            }

            return appliedCount;
        }

        private static IColorApplicationTarget ResolveTarget(
            Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours =
                collider.GetComponentsInParent<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IColorApplicationTarget target)
                {
                    return target;
                }
            }

            return null;
        }
    }
}
