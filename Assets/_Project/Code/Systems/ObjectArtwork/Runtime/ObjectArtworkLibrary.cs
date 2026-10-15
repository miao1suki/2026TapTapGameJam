using UnityEngine;

namespace Project.ObjectArtwork
{
    public enum RespawnArtworkState { Locked, Used, Current }

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
