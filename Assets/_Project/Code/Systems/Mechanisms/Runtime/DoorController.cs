using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Project.Mechanisms
{
    public enum DoorState { Locked, Unlocked }

    [DisallowMultipleComponent]
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField, InspectorName("门轴")] private Transform hinge;
        [SerializeField, InspectorName("阻挡碰撞箱")] private Collider blockingCollider;
        [SerializeField, InspectorName("绑定按钮")] private List<DoorButton> buttons = new List<DoorButton>();
        [BlockParameter(Label = "开门时间", Group = "开门", Order = 0)]
        [SerializeField, Min(.01f)] private float openDuration = .6f;
        [BlockParameter(Label = "镜头移动时间", Group = "演出", Order = 0)]
        [SerializeField, Min(.1f)] private float cameraMoveDuration = 1f;
        [BlockParameter(Label = "镜头停留时间", Group = "演出", Order = 1)]
        [SerializeField, Min(0)] private float cameraHoldDuration = 1.2f;
        [BlockParameter(Label = "镜头返回时间", Group = "演出", Order = 2)]
        [SerializeField, Min(0)] private float cameraReturnDuration = .7f;
        [BlockParameter(Label = "房间重置时关门", Group = "重置", Order = 0)]
        [SerializeField] private bool resetWithRoom = true;
        private Quaternion closedRotation;
        private Tween opening;
        private Coroutine shotRoutine;
        private TimelineCamRig rig;
        private Transform shotAnchor, previousAnchor;
        private TimelineAsset ownedTimeline;
        private PlayableDirector director;
        private IDisposable controlLock;
        public DoorState State { get; private set; }
        public IReadOnlyList<DoorButton> Buttons => buttons;
        public event Action<DoorState> StateChanged;

        private void Awake()
        {
            if (blockingCollider == null) blockingCollider = GetComponent<Collider>();
            closedRotation = hinge != null ? hinge.localRotation : Quaternion.identity;
            State = DoorState.Locked;
        }
        private void OnEnable() { if (Application.isPlaying) EventMgr.OnRoomColorReset += HandleRoomReset; }
        private void OnDisable()
        {
            EventMgr.OnRoomColorReset -= HandleRoomReset;
            opening?.Kill();
            if (shotRoutine != null) StopCoroutine(shotRoutine);
            shotRoutine = null;
            CleanupShot();
        }
        public void SetButtons(IReadOnlyList<DoorButton> values)
        {
            foreach (var button in buttons) if (button != null && button.Owner == this) button.SetOwner(null);
            buttons.Clear();
            if (values != null) foreach (var button in values)
                if (button != null && !buttons.Contains(button)) { buttons.Add(button); button.SetOwner(this); }
        }
        public void ForgetButton(DoorButton button) => buttons.Remove(button);
        public void NotifyButtonPressed(GameObject actor)
        {
            if (!Application.isPlaying || State != DoorState.Locked || buttons.Count == 0) return;
            foreach (var button in buttons) if (button == null || !button.IsPressed) return;
            State = DoorState.Unlocked;
            if (blockingCollider != null) blockingCollider.enabled = false;
            opening?.Kill();
            if (hinge != null)
                opening = hinge.DOLocalRotateQuaternion(closedRotation * Quaternion.Euler(0, 90, 0), openDuration)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            StateChanged?.Invoke(State);
            shotRoutine = StartCoroutine(PlayDoorShot(actor));
        }
        public void ResetDoor()
        {
            opening?.Kill();
            if (shotRoutine != null) StopCoroutine(shotRoutine);
            shotRoutine = null;
            CleanupShot();
            State = DoorState.Locked;
            if (hinge != null) hinge.localRotation = closedRotation;
            if (blockingCollider != null) blockingCollider.enabled = true;
            foreach (var button in buttons) if (button != null) button.ResetButton();
            StateChanged?.Invoke(State);
        }
        private void HandleRoomReset() { if (resetWithRoom) ResetDoor(); }
        private IEnumerator PlayDoorShot(GameObject actor)
        {
            var manager = ProjectDiscovery.FindFirst<CameraControlManager>();
            if (manager == null) { shotRoutine = null; yield break; }
            rig = manager.GetComponent<TimelineCamRig>() ?? manager.gameObject.AddComponent<TimelineCamRig>();
            float deadline = Time.unscaledTime + 15f;
            while (rig != null && rig.isPlayingAnim && Time.unscaledTime < deadline) yield return null;
            if (rig == null || rig.isPlayingAnim) { shotRoutine = null; yield break; }
            var player = actor != null ? actor.GetComponentInParent<PlayerController>() : null;
            if (player is IPlayerControlLockTarget lockTarget) controlLock = lockTarget.AcquireControlLock(this);
            previousAnchor = rig.target;
            shotAnchor = new GameObject("Door Shot Anchor").transform;
            shotAnchor.position = transform.TransformPoint(new Vector3(0, .5f, 0));
            rig.target = shotAnchor;
            var state = manager.CurrentState;
            float distance = Mathf.Max(3f, Vector3.Distance(state.position, previousAnchor != null ? previousAnchor.position : transform.position));
            Vector3 offset = -(state.rotation * Vector3.forward) * distance;
            Quaternion planar = Quaternion.Euler(0, rig.PlanarYaw, 0);
            ownedTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
            ownedTimeline.name = "Door Unlock Camera";
            var track = ownedTimeline.CreateTrack<CameraTimelineTrack>(null, "门解锁镜头");
            track.restoreOriginOnEnd = true;
            var clip = track.CreateClip<CameraTimelineClip>();
            clip.start = 0;
            clip.duration = cameraMoveDuration + cameraHoldDuration;
            var asset = (CameraTimelineClip)clip.asset;
            asset.cameraMoveMode = CamMoveMode.SmoothLerp;
            asset.lockLookAtPlayer = false;
            asset.cameraTargetLocalPos = Quaternion.Inverse(planar) * offset;
            asset.cameraTargetEuler = (Quaternion.Inverse(planar) * state.rotation).eulerAngles;
            asset.useMotionCurve = true;
            float arrival = cameraMoveDuration / (cameraMoveDuration + cameraHoldDuration);
            asset.motionCurve = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(arrival, 1, 0, 0));
            if (arrival < 1) asset.motionCurve.AddKey(new Keyframe(1, 1, 0, 0));
            director = gameObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.playableAsset = ownedTimeline;
            director.SetGenericBinding(track, rig);
            rig.SetReturnDurationForCurrentShot(cameraReturnDuration);
            director.Play();
            deadline = Time.unscaledTime + (float)clip.duration + 1f;
            while (director != null && director.state == PlayState.Playing && Time.unscaledTime < deadline) yield return null;
            if (director != null) director.Stop();
            float returnDeadline = Time.unscaledTime + cameraReturnDuration;
            while (Time.unscaledTime < returnDeadline) yield return null;
            CleanupShot();
            shotRoutine = null;
        }
        private void CleanupShot()
        {
            if (director != null) { director.Stop(); Destroy(director); director = null; }
            if (rig != null && shotAnchor != null && rig.target == shotAnchor) rig.target = previousAnchor;
            if (shotAnchor != null) Destroy(shotAnchor.gameObject);
            if (ownedTimeline != null)
            {
                foreach (var track in ownedTimeline.GetOutputTracks())
                {
                    foreach (var clip in track.GetClips()) if (clip.asset != null) Destroy(clip.asset);
                    Destroy(track);
                }
                Destroy(ownedTimeline);
            }
            shotAnchor = null;
            ownedTimeline = null;
            controlLock?.Dispose();
            controlLock = null;
        }
    }
}
