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
        [InspectorName("侧面梯子")] LadderSide, [InspectorName("岩浆")] Lava
    }
    [System.Serializable]
    public sealed class MechanismArtworkEntry
    {
        [InspectorName("物体类型")] public MechanismArtworkKind kind;
        [InspectorName("动画帧")] public Sprite[] frames = System.Array.Empty<Sprite>();
        [InspectorName("每帧秒数")] public float frameSeconds = .08f;
    }

    public sealed class ObjectArtworkLibrary : ScriptableObject
    {
        public Sprite respawnLocked;
        public Sprite respawnUsed;
        public Sprite[] respawnCurrent = System.Array.Empty<Sprite>();
        public Sprite[] plantBounce = System.Array.Empty<Sprite>();
        public Material neutralSpriteMaterial;
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

        public MechanismArtworkEntry Find(MechanismArtworkKind kind)
        {
            if (mechanisms != null) foreach (var entry in mechanisms)
                if (entry != null && entry.kind == kind) return entry;
            return null;
        }
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
