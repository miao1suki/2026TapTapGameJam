using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.BossLogic
{
    public sealed class BossIntroClip : PlayableAsset, ITimelineClipAsset
    {
        [Header("Boss 来源")]
        public BossController bossPrefab;
        public bool useExistingBoss = true;
        public string bossTag = "Boss";
        public bool disableTrackingAtStart = true;

        [Header("入场位置")]
        public bool usePlayerPositionAsSpawn = true;
        public string playerTag = "Player";
        public bool useOwnerAsSpawnAnchor = true;
        public Vector3 spawnOffset;
        public Vector3 landingOffset = new Vector3(2f, -3f, 0f);

        [Header("摄影机")]
        public bool takeCameraControl = true;
        public bool requestSide2D = true;
        public float side2DTransitionSeconds = .4f;
        public float side2DYawDegrees;
        public float cameraMoveSeconds = 2f;
        public float cameraDistance = 10f;
        public float cameraHeight = 1.5f;
        public float cameraLookHeight = 1.2f;
        public float cameraReturnSeconds = .25f;

        [Header("动作时序")]
        public float standSeconds = 1f;
        public float jumpDuration = .65f;
        public float jumpArcHeight = 2f;
        public float landingHoldSeconds = .15f;

        [Header("结束")]
        public bool enableTrackingAtEnd = true;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            var behaviour = new BossIntroBehaviour
            {
                clip = this,
                owner = owner
            };
            return ScriptPlayable<BossIntroBehaviour>.Create(
                graph,
                behaviour);
        }
    }
}
