using System;
using UnityEngine;

namespace Project.Player
{
    public enum PlayerSpriteState { Idle, Run, Jump, Fall, Climb, Swim, Death }

    [CreateAssetMenu(menuName = "2026TapTap/玩家/精灵动画", fileName = "PlayerSprites")]
    public sealed class PlayerSpriteAnimationSet : ScriptableObject
    {
        [Serializable]
        public sealed class Clip
        {
            [InspectorName("状态")] public PlayerSpriteState state;
            [InspectorName("帧")] public Sprite[] frames = Array.Empty<Sprite>();
            [Min(.01f), InspectorName("每帧秒数")] public float secondsPerFrame = .08f;
            [InspectorName("循环")] public bool loop;
        }
        [SerializeField, InspectorName("动画")] private Clip[] clips = Array.Empty<Clip>();
        public Clip GetClip(PlayerSpriteState state)
        {
            foreach (Clip clip in clips) if (clip != null && clip.state == state) return clip;
            return null;
        }
        public void Configure(Clip[] value) => clips = value ?? Array.Empty<Clip>();
    }

    // One clock owns playback; transitions never leave coroutines or tweens behind.
    public sealed class PlayerSpriteAnimationClock
    {
        public PlayerSpriteState State { get; private set; } = PlayerSpriteState.Idle;
        public float Elapsed { get; private set; }
        private bool initialized, wasSuspended;
        public static PlayerSpriteState Resolve(PlayerStateId state, bool grounded, float x, float y, Vector2 input)
        {
            if (state == PlayerStateId.Death) return PlayerSpriteState.Death;
            if (state == PlayerStateId.Climbing || state == PlayerStateId.HorizontalClimbing) return PlayerSpriteState.Climb;
            if (state == PlayerStateId.Swimming) return PlayerSpriteState.Swim;
            if (!grounded) return y > .05f ? PlayerSpriteState.Jump : PlayerSpriteState.Fall;
            return Mathf.Abs(input.x) > .05f && Mathf.Abs(x) > .05f ? PlayerSpriteState.Run : PlayerSpriteState.Idle;
        }
        public void Advance(PlayerSpriteState requested, bool suspended, float delta)
        {
            if (suspended && requested != PlayerSpriteState.Death)
            {
                // A revived player must not keep an exhausted death clip while locked.
                if (!initialized || State == PlayerSpriteState.Death) Reset(PlayerSpriteState.Idle);
                wasSuspended = true;
                return;
            }
            if (!initialized || requested != State || wasSuspended)
            {
                Reset(requested);
                wasSuspended = false;
                return;
            }
            Elapsed += Mathf.Max(0f, delta);
        }
        public int Frame(PlayerSpriteAnimationSet.Clip clip)
        {
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return -1;
            double raw = Math.Floor(Elapsed / Math.Max(.01f, clip.secondsPerFrame));
            if (clip.loop) return (int)(raw % clip.frames.Length);
            if (State == PlayerSpriteState.Death && raw >= clip.frames.Length) return -1;
            return (int)Math.Min(raw, clip.frames.Length - 1);
        }
        public void Reset(PlayerSpriteState state)
        {
            State = state; Elapsed = 0f; initialized = true;
        }
    }
}
