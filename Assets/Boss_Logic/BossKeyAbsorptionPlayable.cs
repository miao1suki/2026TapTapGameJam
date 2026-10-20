using System.Collections.Generic;
using Project.ColorBlocks;
using Project.BlockFeatures;
using Project.Items;
using Project.Pickups;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public sealed class BossKeyAbsorptionPlayable :
        PlayableAsset
    {
        [SerializeField] private GameObject redKeyPrefab;
        [SerializeField] private GameObject blueKeyPrefab;
        [SerializeField] private GameObject greenKeyPrefab;
        [SerializeField, Min(1)] private int copiesPerColor = 2;
        [SerializeField, Min(0f)] private float emergeStagger = .18f;
        [SerializeField, Min(.01f)] private float emergeDuration = .55f;
        [SerializeField, Min(0f)] private float assemblyHold = .3f;
        [SerializeField, Min(0f)] private float attractStagger = .12f;
        [SerializeField, Min(.01f)] private float attractDuration = .42f;
        [Tooltip("相对表演坐标系的方向偏移，运行时叠加到玩家中心。")]
        [SerializeField] private Vector3 playerBodyOffset =
            new Vector3(0f, .5f, 0f);
        [SerializeField, Min(0f)] private float hoverHeight = 1.5f;
        [SerializeField, Min(0f)] private float slotSpacing = .36f;
        [SerializeField, Min(0f)] private float slotArcHeight = .18f;
        [SerializeField, Min(0f)] private float hoverBobAmplitude = .025f;
        [SerializeField, Min(0f)] private float hoverBobSpeed = 3.5f;
        [SerializeField, Min(0f)] private float emergeSpinDegrees = 180f;
        [SerializeField, Min(0f)] private float hoverSpinDegrees = 180f;
        [SerializeField, Min(0f)] private float hoverWobbleDegrees = 8f;
        [SerializeField, Min(0f)] private float hoverWobbleSpeed = 7f;
        [Tooltip("相对表演坐标系的方向偏移，运行时叠加到 Boss 中心。")]
        [SerializeField] private Vector3 bossAbsorbOffset =
            new Vector3(0f, .5f, 0f);
        [SerializeField, Min(0f)] private float attractArcHeight = .55f;
        [SerializeField, Min(0f)] private float attractSpinDegrees = 360f;
        [SerializeField, Min(0f)] private float visualScaleMultiplier = 1f;
        [SerializeField] private string sortingLayerName = "BossTop";
        [SerializeField] private int sortingOrder = 32760;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            PlayableDirector resolvedDirector =
                BossTimelinePerformanceFrame.ResolveDirector(
                    graph,
                    owner);
            BossKeyAbsorptionBehaviour behaviour =
                new BossKeyAbsorptionBehaviour
                {
                    redKeyPrefab = redKeyPrefab,
                    blueKeyPrefab = blueKeyPrefab,
                    greenKeyPrefab = greenKeyPrefab,
                    copiesPerColor = copiesPerColor,
                    emergeStagger = emergeStagger,
                    emergeDuration = emergeDuration,
                    assemblyHold = assemblyHold,
                    attractStagger = attractStagger,
                    attractDuration = attractDuration,
                    playerBodyOffset = playerBodyOffset,
                    hoverHeight = hoverHeight,
                    slotSpacing = slotSpacing,
                    slotArcHeight = slotArcHeight,
                    hoverBobAmplitude = hoverBobAmplitude,
                    hoverBobSpeed = hoverBobSpeed,
                    emergeSpinDegrees = emergeSpinDegrees,
                    hoverSpinDegrees = hoverSpinDegrees,
                    hoverWobbleDegrees = hoverWobbleDegrees,
                    hoverWobbleSpeed = hoverWobbleSpeed,
                    bossAbsorbOffset = bossAbsorbOffset,
                    attractArcHeight = attractArcHeight,
                    attractSpinDegrees = attractSpinDegrees,
                    visualScaleMultiplier =
                        visualScaleMultiplier,
                    sortingLayerName = sortingLayerName,
                    sortingOrder = sortingOrder,
                    director = resolvedDirector
                };
            return ScriptPlayable<BossKeyAbsorptionBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class BossKeyAbsorptionBehaviour :
        PlayableBehaviour
    {
        private sealed class CinematicKey
        {
            public GameObject gameObject;
            public Transform transform;
            public Vector3 baseScale;
            public Vector3 bodyPosition;
            public Vector3 hoverPosition;
            public float emergeStart;
            public float emergeEnd;
            public float attractStart;
            public float attractEnd;
            public bool removed;
        }

        public GameObject redKeyPrefab;
        public GameObject blueKeyPrefab;
        public GameObject greenKeyPrefab;
        public int copiesPerColor = 2;
        public float emergeStagger = .18f;
        public float emergeDuration = .55f;
        public float assemblyHold = .3f;
        public float attractStagger = .12f;
        public float attractDuration = .42f;
        public Vector3 playerBodyOffset =
            new Vector3(0f, .5f, 0f);
        public float hoverHeight = 1.5f;
        public float slotSpacing = .36f;
        public float slotArcHeight = .18f;
        public float hoverBobAmplitude = .025f;
        public float hoverBobSpeed = 3.5f;
        public float emergeSpinDegrees = 180f;
        public float hoverSpinDegrees = 180f;
        public float hoverWobbleDegrees = 8f;
        public float hoverWobbleSpeed = 7f;
        public Vector3 bossAbsorbOffset =
            new Vector3(0f, .5f, 0f);
        public float attractArcHeight = .55f;
        public float attractSpinDegrees = 360f;
        public float visualScaleMultiplier = 1f;
        public string sortingLayerName = "BossTop";
        public int sortingOrder = 32760;
        public PlayableDirector director;

        private readonly List<CinematicKey> keys =
            new List<CinematicKey>();
        private GameObject cinematicRoot;
        private Transform player;
        private Transform boss;
        private BossTimelinePerformanceFrame frame;
        private bool initialized;
        private bool failed;

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Cleanup();
            initialized = false;
            failed = false;
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (failed)
            {
                return;
            }
            if (!initialized && !Initialize())
            {
                failed = true;
                return;
            }

            float time = (float)playable.GetTime();
            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                CinematicKey key = keys[index];
                if (key.removed ||
                    key.transform == null)
                {
                    continue;
                }

                if (time < key.emergeStart)
                {
                    key.transform.position =
                        key.bodyPosition;
                    key.transform.localScale =
                        Vector3.zero;
                    continue;
                }

                if (time < key.emergeEnd)
                {
                    float progress = SmoothProgress(
                        time,
                        key.emergeStart,
                        key.emergeEnd);
                    key.transform.position =
                        Vector3.Lerp(
                            key.bodyPosition,
                            key.hoverPosition,
                            progress);
                    key.transform.localScale =
                        key.baseScale *
                        visualScaleMultiplier *
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            progress);
                    key.transform.rotation =
                        Quaternion.Euler(
                            0f,
                            0f,
                            progress *
                            emergeSpinDegrees);
                    continue;
                }

                Vector3 hover =
                    GetHoverPosition(
                        key.hoverPosition,
                        index,
                        time);
                if (time < key.attractStart)
                {
                    key.transform.position = hover;
                    key.transform.localScale =
                        key.baseScale *
                        visualScaleMultiplier;
                    key.transform.rotation =
                        Quaternion.Euler(
                            0f,
                            0f,
                            hoverSpinDegrees +
                            Mathf.Sin(
                                time *
                                hoverWobbleSpeed +
                                index) *
                            hoverWobbleDegrees);
                    continue;
                }

                float attract = SmoothProgress(
                    time,
                    key.attractStart,
                    key.attractEnd);
                Vector3 target =
                    boss.position +
                    frame.ToWorldOffset(
                        bossAbsorbOffset);
                Vector3 control =
                    Vector3.Lerp(hover, target, .5f) +
                    frame.ToWorldOffset(
                        Vector3.up *
                        attractArcHeight);
                key.transform.position =
                    QuadraticBezier(
                        hover,
                        control,
                        target,
                        attract);
                key.transform.localScale =
                    key.baseScale *
                    visualScaleMultiplier *
                    (1f - attract);
                key.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        hoverSpinDegrees +
                        attract *
                        attractSpinDegrees);
                if (time >= key.attractEnd)
                {
                    key.removed = true;
                    DestroyObject(key.gameObject);
                }
            }
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            Cleanup();
        }

        public override void OnGraphStop(Playable playable)
        {
            Cleanup();
        }

        public override void OnPlayableDestroy(
            Playable playable)
        {
            Cleanup();
        }

        private bool Initialize()
        {
            GameObject playerObject =
                FindTagged("Player");
            GameObject bossObject =
                FindTagged("Boss");
            PlayerController playerController =
                playerObject != null
                    ? playerObject.GetComponent<
                        PlayerController>()
                    : null;
            BossController bossController =
                bossObject != null
                    ? bossObject.GetComponent<
                        BossController>()
                    : null;
            if (playerController == null ||
                bossController == null)
            {
                return false;
            }
            if (!BossTimelinePerformanceFrame.TryResolve(
                    director,
                    nameof(BossKeyAbsorptionPlayable),
                    out frame))
            {
                return false;
            }

            player = playerController.transform;
            boss = bossController.transform;

            cinematicRoot =
                new GameObject("Boss六钥匙吸收演出");
            cinematicRoot.transform.SetParent(
                frame.Anchor,
                false);
            cinematicRoot.SetActive(false);
            List<GameObject> prefabs =
                BuildPrefabOrder();
            int count = prefabs.Count;
            Vector3 body =
                player.position +
                frame.ToWorldOffset(
                    playerBodyOffset);
            Vector3 head =
                player.position +
                frame.ToWorldOffset(
                    new Vector3(
                        0f,
                        hoverHeight,
                        0f));
            float emergeTotal =
                Mathf.Max(0, count - 1) *
                emergeStagger +
                emergeDuration;
            float attractBase =
                emergeTotal +
                assemblyHold;

            for (int index = 0;
                 index < count;
                 index++)
            {
                GameObject prefab = prefabs[index];
                GameObject instance = Object.Instantiate(
                    prefab,
                    cinematicRoot.transform);
                instance.name =
                    prefab.name + "_Cinematic";
                DisableGameplay(instance);
                MechanismArtworkPresentation
                    .PrepareCinematicClone(
                        instance,
                        sortingLayerName,
                        sortingOrder);

                Vector3 slot =
                    BuildHoverSlot(
                        head,
                        index,
                        count);
                CinematicKey key =
                    new CinematicKey
                    {
                        gameObject = instance,
                        transform = instance.transform,
                        baseScale =
                            instance.transform.localScale,
                        bodyPosition = body,
                        hoverPosition = slot,
                        emergeStart =
                            index * emergeStagger,
                        emergeEnd =
                            index * emergeStagger +
                            emergeDuration,
                        attractStart =
                            attractBase +
                            index * attractStagger,
                        attractEnd =
                            attractBase +
                            index * attractStagger +
                            attractDuration
                    };
                instance.transform.position =
                    body;
                instance.transform.localScale =
                    Vector3.zero;
                keys.Add(key);
            }

            cinematicRoot.SetActive(true);
            initialized = true;
            return true;
        }

        private List<GameObject> BuildPrefabOrder()
        {
            var result = new List<GameObject>();
            int count = Mathf.Max(1, copiesPerColor);
            for (int copy = 0; copy < count; copy++)
            {
                if (redKeyPrefab != null)
                {
                    result.Add(redKeyPrefab);
                }
                if (blueKeyPrefab != null)
                {
                    result.Add(blueKeyPrefab);
                }
                if (greenKeyPrefab != null)
                {
                    result.Add(greenKeyPrefab);
                }
            }
            return result;
        }

        private Vector3 BuildHoverSlot(
            Vector3 head,
            int index,
            int count)
        {
            float centered =
                index - (count - 1) * .5f;
            float normalized = count <= 1
                ? 0f
                : centered /
                  Mathf.Max(1f, (count - 1) * .5f);
            float arc =
                1f -
                normalized * normalized;
            return head +
                   frame.ToWorldOffset(
                       new Vector3(
                           centered * slotSpacing,
                           arc * slotArcHeight,
                           0f));
        }

        private Vector3 GetHoverPosition(
            Vector3 slot,
            int index,
            float time)
        {
            return slot +
                   frame.ToWorldOffset(Vector3.up) *
                   (Mathf.Sin(
                        time *
                        hoverBobSpeed +
                        index) *
                    hoverBobAmplitude);
        }

        private static float SmoothProgress(
            float time,
            float start,
            float end)
        {
            if (end <= start)
            {
                return 1f;
            }

            float progress = Mathf.Clamp01(
                (time - start) / (end - start));
            return Mathf.SmoothStep(0f, 1f, progress);
        }

        private static Vector3 QuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * start +
                   2f * inverse * progress * control +
                   progress * progress * end;
        }

        private static GameObject FindTagged(
            string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            try
            {
                return GameObject.FindGameObjectWithTag(tag);
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private static void DisableGameplay(
            GameObject instance)
        {
            Collider[] colliders =
                instance.GetComponentsInChildren<Collider>(
                    true);
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                colliders[index].enabled = false;
            }

            Rigidbody[] bodies =
                instance.GetComponentsInChildren<Rigidbody>(
                    true);
            for (int index = 0;
                 index < bodies.Length;
                 index++)
            {
                bodies[index].detectCollisions = false;
                bodies[index].isKinematic = true;
            }

            ColorKeyPickup[] pickups =
                instance.GetComponentsInChildren<
                    ColorKeyPickup>(true);
            for (int index = 0;
                 index < pickups.Length;
                 index++)
            {
                pickups[index].enabled = false;
            }

            DropItemBase[] drops =
                instance.GetComponentsInChildren<
                    DropItemBase>(true);
            for (int index = 0;
                 index < drops.Length;
                 index++)
            {
                drops[index].enabled = false;
            }

            PickupFlightAnimator[] flights =
                instance.GetComponentsInChildren<
                    PickupFlightAnimator>(true);
            for (int index = 0;
                 index < flights.Length;
                 index++)
            {
                flights[index].enabled = false;
            }

            PlayableDirector[] directors =
                instance.GetComponentsInChildren<
                    PlayableDirector>(true);
            for (int index = 0;
                 index < directors.Length;
                 index++)
            {
                directors[index].Stop();
                directors[index].enabled = false;
            }

            MechanismArtworkPresentation[] artworks =
                instance.GetComponentsInChildren<
                    MechanismArtworkPresentation>(true);
            for (int index = 0;
                 index < artworks.Length;
                 index++)
            {
                artworks[index].enabled = false;
            }
        }

        private void Cleanup()
        {
            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                CinematicKey key = keys[index];
                if (key.gameObject != null)
                {
                    DestroyObject(key.gameObject);
                }
            }
            keys.Clear();

            if (cinematicRoot != null)
            {
                DestroyObject(cinematicRoot);
            }
            cinematicRoot = null;
            player = null;
            boss = null;
            frame = default;
            initialized = false;
            failed = false;
        }

        private static void DestroyObject(
            Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
