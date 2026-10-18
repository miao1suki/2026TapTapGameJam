using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Player;
using Project.ObjectArtwork;
using UnityEngine;

namespace Project.Mechanisms
{
    public enum OneWayPlatformColor
    {
        [InspectorName("原白色（兼容旧关卡）")] Neutral,
        [InspectorName("红色")] Red,
        [InspectorName("绿色")] Green,
        [InspectorName("蓝色")] Blue
    }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider)), DefaultExecutionOrder(-200)]
    public sealed class OneWayPlatform : MonoBehaviour, IRoomColorResettable
    {
        [BlockParameter(Label = "顶面容差（格）", Group = "碰撞设置")]
        [SerializeField, Min(0f), InspectorName("顶面容差（格）")]
        private float topToleranceCells = .05f;
        [BlockParameter(Label = "美术颜色", Group = "美术颜色", Tooltip = "只改变图案与HSV颜色层，三色玩法相同，未解锁颜色也可站立。")]
        [SerializeField, InspectorName("美术颜色")]
        private OneWayPlatformColor appearanceColor;
        private int artworkLayer = -1;
        private OneWayPlatformColor cachedColor = (OneWayPlatformColor)(-1);
        public OneWayPlatformColor AppearanceColor => appearanceColor;
        public MechanismArtworkKind ArtworkKind => ObjectArtworkLibrary.OneWayPlatformKind((int)appearanceColor);
        public string AppearanceColorId => appearanceColor == OneWayPlatformColor.Red ? "red"
            : appearanceColor == OneWayPlatformColor.Green ? "green" : appearanceColor == OneWayPlatformColor.Blue ? "blue" : null;
        public int ArtworkLayer
        {
            get
            {
                if (cachedColor == appearanceColor && artworkLayer >= 0) return artworkLayer;
                cachedColor = appearanceColor;
                var catalog = Resources.Load<ColorCatalog>("ColorBlocks/ColorCatalog");
                var definition = catalog != null ? catalog.Find(AppearanceColorId) : null;
                artworkLayer = definition != null && definition.unityLayer >= 0 ? definition.unityLayer : gameObject.layer;
                return artworkLayer;
            }
        }
        // Appearance only: no ColorObject, unlock gate, color application or
        // room-reset activation semantics are added to the physical platform.
        public void ConfigureAppearance(OneWayPlatformColor value)
        {
            appearanceColor = value;
            cachedColor = (OneWayPlatformColor)(-1);
            artworkLayer = -1;
        }
        private BoxCollider surface;
        private readonly Dictionary<Collider, bool> pairs = new Dictionary<Collider, bool>();
        private readonly Dictionary<Collider, float> previousFeet = new Dictionary<Collider, float>();
        private void Awake() => surface = GetComponent<BoxCollider>();
        public bool CanSupport(PlayerController player)
        {
            if (player == null || surface == null || !surface.enabled) return false;
            Collider body = player.GetComponent<Collider>();
            if (body == null || player.VerticalVelocity > .1f) return false;
            float feet = Feet(player, body);
            float threshold = surface.bounds.max.y - topToleranceCells * GridCellSizeUtility.Resolve(this);
            return feet >= threshold || previousFeet.TryGetValue(body, out float previous) && previous >= threshold;
        }
        private void FixedUpdate()
        {
            foreach (PlayerController player in PlayerController.ActivePlayers)
            {
                if (player == null || player.gameObject.scene != gameObject.scene) continue;
                Collider body = player.GetComponent<Collider>();
                if (body == null) continue;
                if (!pairs.ContainsKey(body)) pairs.Add(body, Physics.GetIgnoreCollision(surface, body));
                Physics.IgnoreCollision(surface, body, pairs[body] || !CanSupport(player));
                previousFeet[body] = Feet(player, body);
            }
        }
        public void ResetForRoom() => Restore();
        private void OnDisable() => Restore();
        private void Restore()
        {
            if (surface != null) foreach (var pair in pairs) if (pair.Key != null) Physics.IgnoreCollision(surface, pair.Key, pair.Value);
            pairs.Clear();
            previousFeet.Clear();
        }
        private static float Feet(PlayerController player, Collider body) => body.bounds.min.y + player.Motor.position.y - player.transform.position.y;
    }
}
