using UnityEngine;

namespace Project.ObjectArtwork
{
    public enum RespawnArtworkState { Locked, Used, Current }
    public enum MechanismArtworkKind
    {
        [InspectorName("门")] Door, [InspectorName("门按钮")] DoorButton,
        [InspectorName("曲柄")] Crank, [InspectorName("移动平台")] MovingPlatform,
        [InspectorName("曲柄平台")] CrankPlatform, [InspectorName("红色方块")] RedCube,
        [InspectorName("绿色方块")] GreenCube, [InspectorName("蓝色方块")] BlueCube,
        [InspectorName("红色钥匙")] RedKey, [InspectorName("绿色钥匙")] GreenKey,
        [InspectorName("蓝色钥匙")] BlueKey, [InspectorName("单向平台")] OneWayPlatform,
        [InspectorName("红色按钮")] RedButton, [InspectorName("正面梯子")] LadderFront,
        [InspectorName("侧面梯子")] LadderSide, [InspectorName("岩浆")] Lava,
        [InspectorName("绿色门按钮")] GreenDoorButton, [InspectorName("蓝色门按钮")] BlueDoorButton,
        [InspectorName("尖刺")] Spike,
        [InspectorName("基座")] MechanismBase,
        [InspectorName("植物障碍")] PlantObstacle,
        [InspectorName("黑曜石")] Obsidian,
        [InspectorName("红色单向平台")] RedOneWayPlatform,
        [InspectorName("绿色单向平台")] GreenOneWayPlatform,
        [InspectorName("蓝色单向平台")] BlueOneWayPlatform,
        [InspectorName("小尖刺·带血")] BloodySpike,
        [InspectorName("大尖刺")] LargeSpike,
        [InspectorName("大尖刺·带血")] BloodyLargeSpike,
        [InspectorName("成就收集品")] Collectible
    }
    [System.Serializable]
    public sealed class MechanismArtworkEntry
    {
        [InspectorName("物体类型")] public MechanismArtworkKind kind;
        [InspectorName("动画帧")] public Sprite[] frames = System.Array.Empty<Sprite>();
        [InspectorName("每帧秒数")] public float frameSeconds = .08f;
        [InspectorName("支撑顶边距帧底（像素）")] public float supportTopPixels;
    }

    public sealed class ObjectArtworkLibrary : ScriptableObject
    {
        public Sprite respawnLocked;
        public Sprite respawnUsed;
        public Sprite[] respawnCurrent = System.Array.Empty<Sprite>();
        public Sprite[] plantBounce = System.Array.Empty<Sprite>();
        public Material neutralSpriteMaterial;
        public Material neutralMeshMaterial;
        public Material colorSpriteMaterial;
        public float respawnFrameSeconds = .08f;
        public float plantFrameSeconds = .08f;
        public int plantIdleFrame = 5;
        public float plantIdleTop = 1f;
        public Sprite[] vineGrowth = System.Array.Empty<Sprite>();
        public int vineInitialFrame = 24;
        public int vineGrownFrame = 160;
        public float vineIdleFrameSeconds = 1f / 12f;
        public MechanismArtworkEntry[] mechanisms = System.Array.Empty<MechanismArtworkEntry>();
        public Sprite[] doorIndicators = System.Array.Empty<Sprite>();
        public Sprite[] doorWhiteIndicators = System.Array.Empty<Sprite>();
        public Sprite doorClosed;
        public Sprite[] signboards = System.Array.Empty<Sprite>();

        public static MechanismArtworkKind DoorButtonKind(int index) => index == 1
            ? MechanismArtworkKind.GreenDoorButton : index == 2 ? MechanismArtworkKind.BlueDoorButton : MechanismArtworkKind.DoorButton;

        public static MechanismArtworkKind OneWayPlatformKind(int color) => color == 1
            ? MechanismArtworkKind.RedOneWayPlatform : color == 2 ? MechanismArtworkKind.GreenOneWayPlatform
            : color == 3 ? MechanismArtworkKind.BlueOneWayPlatform : MechanismArtworkKind.OneWayPlatform;
        public static bool IsOneWayPlatform(MechanismArtworkKind kind) => kind == MechanismArtworkKind.OneWayPlatform ||
            kind == MechanismArtworkKind.RedOneWayPlatform || kind == MechanismArtworkKind.GreenOneWayPlatform || kind == MechanismArtworkKind.BlueOneWayPlatform;
        public static MechanismArtworkKind SpikeKind(bool large, bool bloody) => large
            ? bloody ? MechanismArtworkKind.BloodyLargeSpike : MechanismArtworkKind.LargeSpike
            : bloody ? MechanismArtworkKind.BloodySpike : MechanismArtworkKind.Spike;

        // Three symbols keep their authored locations; shorter lists remain centered.
        public static Vector3 DoorIndicatorOffset(int index, int count) => new Vector3(0f,
            count == 2 ? (index == 0 ? 5f : -13f) / 16f : 0f, -.001f);

        public MechanismArtworkEntry Find(MechanismArtworkKind kind)
        {
            if (mechanisms != null) foreach (var entry in mechanisms)
                if (entry != null && entry.kind == kind) return entry;
            return null;
        }
        public Sprite FirstFrame(MechanismArtworkKind kind)
        {
            var entry = Find(kind);
            return entry != null && entry.frames != null && entry.frames.Length > 0 ? entry.frames[0] : null;
        }
        public static bool PlatformVisualIsVertical(Vector2 dimensions) => dimensions.y > dimensions.x;

        // A 90-degree sprite rotation swaps the axes before the author's nonuniform
        // root scale. Compensate those swapped axes to retain square source pixels.
        public static Vector3 PlatformTileScale(Vector3 authorScale, float cell, bool vertical) => vertical
            ? new Vector3(cell / Mathf.Max(.001f, Mathf.Abs(authorScale.y)),
                cell / Mathf.Max(.001f, Mathf.Abs(authorScale.x)), 1f / Mathf.Max(.001f, Mathf.Abs(authorScale.z)))
            : TileScale(authorScale, cell);
        public static int ProgressFrame(float progress, int count) => count <= 0 ? -1
            : Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress) * (count - 1)), 0, count - 1);

        public int VineFrame(float progress, float idleTime, bool animateIdle)
        {
            if (vineGrowth == null || vineGrowth.Length == 0) return -1;
            int first = Mathf.Clamp(vineInitialFrame, 0, vineGrowth.Length - 1);
            int last = Mathf.Clamp(vineGrownFrame, first, vineGrowth.Length - 1);
            if (animateIdle && progress >= .999f)
                return last + Frame(idleTime, vineIdleFrameSeconds, vineGrowth.Length - last, true);
            return Mathf.RoundToInt(Mathf.Lerp(first, last, Mathf.Clamp01(progress)));
        }
        public static ObjectArtworkLibrary Load() => Resources.Load<ObjectArtworkLibrary>("ObjectArtwork/Objects");
        public static RespawnArtworkState ResolveRespawn(bool activated, bool current) => current
            ? RespawnArtworkState.Current : activated ? RespawnArtworkState.Used : RespawnArtworkState.Locked;
        public static int Frame(float elapsed, float seconds, int count, bool loop)
        {
            if (count <= 0) return -1;
            int index = Mathf.Max(0, Mathf.FloorToInt(elapsed / Mathf.Max(.01f, seconds)));
            return loop ? index % count : Mathf.Min(index, count - 1);
        }
        // Counteract nonuniform author scale so each repeated plant retains its pixel aspect ratio.
        public static Vector3 TileScale(Vector3 authorScale, float cell) => new Vector3(
            cell / Mathf.Max(.001f, Mathf.Abs(authorScale.x)),
            cell / Mathf.Max(.001f, Mathf.Abs(authorScale.y)),
            1f / Mathf.Max(.001f, Mathf.Abs(authorScale.z)));
    }
}
