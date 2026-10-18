using System;
using System.Collections;
using Project.CameraModes;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.Mechanisms
{
    /// <summary>One explicitly requested mechanism shot; never installs itself on an object.</summary>
    internal sealed class MechanismCameraShot : IDisposable
    {
        private readonly MonoBehaviour owner;
        private Coroutine routine;
        private TimelineCamRig rig;
        private Transform anchor, previousAnchor;
        private TimelineAsset timeline;
        private PlayableDirector director;
        private IDisposable controlLock;
        public bool IsPlaying => routine != null || director != null;

        public MechanismCameraShot(MonoBehaviour owner) => this.owner = owner;

        public bool TryPlay(GameObject actor, Vector3 localFocus, float move, float hold, float returning,
            bool lockPlayer, Action arrived = null)
        {
            if (IsPlaying || owner == null || !owner.isActiveAndEnabled || !Application.isPlaying) return false;
            // Check before starting: a missing camera must not leave a completed Coroutine handle marked busy.
            var manager = ProjectDiscovery.FindFirst<CameraControlManager>();
            if (manager == null) return false;
            routine = owner.StartCoroutine(Play(manager, actor, localFocus, Mathf.Max(.1f, move),
                Mathf.Max(0f, hold), Mathf.Max(0f, returning), lockPlayer, arrived));
            return true;
        }

        private IEnumerator Play(CameraControlManager manager, GameObject actor, Vector3 localFocus,
            float move, float hold, float returning, bool lockPlayer, Action arrived)
        {
            // Always yield once so completion cannot race assignment of the Coroutine handle.
            yield return null;
            if (manager == null || owner == null || !owner.isActiveAndEnabled) { routine = null; yield break; }
            rig = manager.GetComponent<TimelineCamRig>() ?? manager.gameObject.AddComponent<TimelineCamRig>();
            float deadline = Time.unscaledTime + 15f;
            while (rig != null && rig.isPlayingAnim && Time.unscaledTime < deadline) yield return null;
            if (rig == null || rig.isPlayingAnim) { Cleanup(); routine = null; yield break; }
            if (lockPlayer)
            {
                var player = actor != null ? actor.GetComponentInParent<PlayerController>() : null;
                if (player == null) player = ProjectDiscovery.FindFirst<PlayerController>();
                if (player is IPlayerControlLockTarget target) controlLock = target.AcquireControlLock(owner);
            }
            previousAnchor = rig.target;
            anchor = new GameObject("Mechanism Shot Anchor").transform;
            anchor.position = owner.transform.TransformPoint(localFocus);
            rig.target = anchor;
            var state = manager.CurrentState;
            float distance = Mathf.Max(3f, Vector3.Distance(state.position,
                previousAnchor != null ? previousAnchor.position : owner.transform.position));
            Vector3 offset = -(state.rotation * Vector3.forward) * distance;
            Quaternion planar = Quaternion.Euler(0f, rig.PlanarYaw, 0f);
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "Optional Mechanism Camera";
            var track = timeline.CreateTrack<CameraTimelineTrack>(null, "可选机关镜头");
            track.restoreOriginOnEnd = true;
            var clip = track.CreateClip<CameraTimelineClip>();
            clip.start = 0;
            clip.duration = move + hold;
            var asset = (CameraTimelineClip)clip.asset;
            asset.cameraMoveMode = CamMoveMode.SmoothLerp;
            asset.lockLookAtPlayer = false;
            asset.cameraTargetLocalPos = Quaternion.Inverse(planar) * offset;
            asset.cameraTargetEuler = (Quaternion.Inverse(planar) * state.rotation).eulerAngles;
            asset.useMotionCurve = true;
            float arrival = move / (float)clip.duration;
            asset.motionCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(arrival, 1f, 0f, 0f));
            if (arrival < 1f) asset.motionCurve.AddKey(new Keyframe(1f, 1f, 0f, 0f));
            director = owner.gameObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.playableAsset = timeline;
            director.SetGenericBinding(track, rig);
            rig.SetReturnDurationForCurrentShot(returning);
            director.Play();
            bool notified = false;
            deadline = Time.time + (float)clip.duration + 1f;
            while (director != null && director.state == PlayState.Playing && Time.time < deadline)
            {
                if (owner != null && anchor != null) anchor.position = owner.transform.TransformPoint(localFocus);
                if (!notified && director.time >= move) { notified = true; arrived?.Invoke(); }
                yield return null;
            }
            if (!notified && director != null && director.time >= move) arrived?.Invoke();
            if (director != null) director.Stop();
            float elapsed = 0f;
            while (elapsed < returning) { elapsed += Time.deltaTime; yield return null; }
            Cleanup();
            routine = null;
        }

        public void Dispose()
        {
            if (routine != null && owner != null) owner.StopCoroutine(routine);
            routine = null;
            Cleanup();
        }

        private void Cleanup()
        {
            if (director != null) { director.Stop(); UnityEngine.Object.Destroy(director); director = null; }
            if (rig != null && anchor != null && rig.target == anchor) rig.target = previousAnchor;
            if (anchor != null) UnityEngine.Object.Destroy(anchor.gameObject);
            if (timeline != null)
            {
                foreach (var track in timeline.GetOutputTracks())
                {
                    foreach (var clip in track.GetClips()) if (clip.asset != null) UnityEngine.Object.Destroy(clip.asset);
                    UnityEngine.Object.Destroy(track);
                }
                UnityEngine.Object.Destroy(timeline);
            }
            anchor = null; timeline = null; rig = null; previousAnchor = null;
            controlLock?.Dispose(); controlLock = null;
        }
    }
}
