using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Project.BossLogic.Editor
{
    public static class BossWalkTimelineInstaller
    {
        private const string TimelinePath =
            "Assets/_Project/Content/Boss/Timelines/Boss_appear.playable";
        private const string SessionKey =
            "BossWalkTimelineInstaller.AutoRun.9";
        private const double ActionStart = 4d;
        private const double ActionDuration = 1.4d;
        private const double LandingHoldStart = 6d;
        private const double TimelineEnd = 6.55d;
        private const double FinalEventTime = 6.54d;

        [InitializeOnLoadMethod]
        private static void AutoInstallOnce()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += Install;
        }

        [MenuItem("Tools/2026TapTap/Boss/同步 Boss 入场演出")]
        public static void Install()
        {
            TimelineAsset timeline =
                AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                    TimelinePath);
            if (timeline == null)
            {
                Debug.LogWarning(
                    $"[BossWalkTimeline] 找不到 Timeline：{TimelinePath}");
                return;
            }

            TimelineClip existingTimelineClip = null;
            BossTimelineClip existingWalk = null;
            TimelineClip jumpTimelineClip = null;
            BossTimelineClip existingJump = null;
            TimelineClip spawnTimelineClip = null;
            BossTimelineClip spawnClip = null;
            TimelineClip holdTimelineClip = null;
            BossTimelineClip holdClip = null;
            TimelineClip trackingTimelineClip = null;
            BossTimelineClip trackingClip = null;
            TimelineClip healthTimelineClip = null;
            TimelineClip cameraTimelineClip = null;
            List<TimelineClip> playerClipsToDelete =
                new List<TimelineClip>();
            foreach (TrackAsset outputTrack in timeline.GetOutputTracks())
            {
                foreach (TimelineClip clip in outputTrack.GetClips())
                {
                    if (clip.displayName == "Boss 走一格" &&
                        clip.asset is BossTimelineClip existingWalkClip)
                    {
                        existingTimelineClip = clip;
                        existingWalk = existingWalkClip;
                    }

                    if (clip.displayName == "Boss 生成/发呆" &&
                        clip.asset is BossTimelineClip spawnAsset)
                    {
                        spawnTimelineClip = clip;
                        spawnClip = spawnAsset;
                    }

                    if (clip.displayName == "Boss 跳跃" &&
                        clip.asset is BossTimelineClip jumpClip)
                    {
                        jumpTimelineClip = clip;
                        existingJump = jumpClip;
                    }

                    if (clip.displayName == "Boss 落地发呆" &&
                        clip.asset is BossTimelineClip holdAsset)
                    {
                        holdTimelineClip = clip;
                        holdClip = holdAsset;
                    }

                    if (clip.displayName == "Boss 开启运动" &&
                        clip.asset is BossTimelineClip trackingAsset)
                    {
                        trackingTimelineClip = clip;
                        trackingClip = trackingAsset;
                    }

                    if (clip.displayName == "Activate Boss Health")
                    {
                        healthTimelineClip = clip;
                    }

                    if (clip.displayName == "Boss 入场镜头")
                    {
                        cameraTimelineClip = clip;
                    }

                    if (clip.asset is BossTimelineClip actorClip &&
                        actorClip.actorTag == "Player")
                    {
                        playerClipsToDelete.Add(clip);
                    }
                }
            }

            foreach (TimelineClip playerClip in playerClipsToDelete)
            {
                timeline.DeleteClip(playerClip);
            }

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            if (player != null &&
                player.scene.name == "Boss" &&
                spawnClip != null)
            {
                spawnClip.targetOffset = player.transform.position;
                EditorUtility.SetDirty(spawnClip);
            }

            if (spawnTimelineClip != null)
            {
                SetClipWindow(
                    spawnTimelineClip,
                    0d,
                    spawnTimelineClip.duration);
            }

            if (jumpTimelineClip != null && existingJump != null)
            {
                SetClipWindow(
                    jumpTimelineClip,
                    ActionStart,
                    ActionDuration);
                ConfigureInputClip(existingJump, false, true);
                EditorUtility.SetDirty(existingJump);
            }

            if (existingWalk != null)
            {
                SetClipWindow(
                    existingTimelineClip,
                    ActionStart,
                    ActionDuration);
                ConfigureInputClip(existingWalk, true, false);
                EditorUtility.SetDirty(existingWalk);
            }
            else
            {
                BossTimelineTrack track =
                    timeline.CreateTrack<BossTimelineTrack>(
                        null,
                        "Boss Move Input");
                TimelineClip walkClip =
                    track.CreateClip<BossTimelineClip>();
                walkClip.displayName = "Boss 走一格";
                SetClipWindow(
                    walkClip,
                    ActionStart,
                    ActionDuration);
                BossTimelineClip walk =
                    (BossTimelineClip)walkClip.asset;
                ConfigureInputClip(walk, true, false);
                EditorUtility.SetDirty(walk);
            }

            if (holdTimelineClip != null && holdClip != null)
            {
                SetClipWindow(
                    holdTimelineClip,
                    LandingHoldStart,
                    TimelineEnd - LandingHoldStart);
                ConfigureInputClip(holdClip, false, false);
                EditorUtility.SetDirty(holdClip);
            }

            if (trackingTimelineClip != null && trackingClip != null)
            {
                SetClipWindow(
                    trackingTimelineClip,
                    FinalEventTime,
                    .01d);
                ConfigureInputClip(trackingClip, false, false);
                trackingClip.setTrackingOnStart = true;
                trackingClip.trackingOnStart = true;
                EditorUtility.SetDirty(trackingClip);
            }

            if (healthTimelineClip != null)
            {
                SetClipWindow(
                    healthTimelineClip,
                    FinalEventTime,
                    .01d);
            }

            if (cameraTimelineClip != null)
            {
                SetClipWindow(
                    cameraTimelineClip,
                    0d,
                    FinalEventTime);
            }

            SerializedObject timelineObject =
                new SerializedObject(timeline);
            SerializedProperty fixedDuration =
                timelineObject.FindProperty("m_FixedDuration");
            SerializedProperty durationMode =
                timelineObject.FindProperty("m_DurationMode");
            if (fixedDuration != null)
            {
                fixedDuration.doubleValue = TimelineEnd;
            }

            if (durationMode != null)
            {
                durationMode.intValue = 0;
            }

            timelineObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SessionState.SetBool(SessionKey, true);
            Debug.Log(
                $"[BossWalkTimeline] 已同步 Boss 入场演出：动作 {ActionStart:0.###}–{ActionStart + ActionDuration:0.###} 秒，Timeline {TimelineEnd:0.###} 秒");
        }

        private static void ConfigureInputClip(
            BossTimelineClip clip,
            bool move,
            bool jump)
        {
            clip.actorTag = "Boss";
            clip.useTrackBinding = true;
            clip.motionMode = move
                ? ActorTimelineMotionMode.Linear
                : jump
                    ? ActorTimelineMotionMode.JumpArc
                    : ActorTimelineMotionMode.None;
            clip.useOwnerAsOrigin = false;
            clip.targetOffset = Vector3.zero;
            clip.motionDuration = (float)ActionDuration;
            clip.moveInput = move ? 1f : 0f;
            clip.driveRigidbody = false;
            clip.clearVelocity = false;
            clip.lockControlWhileDriving = false;
            clip.spawnBoss = false;
            clip.bossPrefab = null;
            clip.setTrackingOnStart = false;
            clip.trackingOnStart = false;
            clip.setTrackingOnEnd = false;
            clip.trackingOnEnd = false;
            clip.useBossJumpState = jump;
            clip.killAtEnd = false;
        }

        private static void SetClipWindow(
            TimelineClip clip,
            double start,
            double duration)
        {
            if (clip == null)
            {
                return;
            }

            clip.start = start;
            clip.duration = duration;
        }
    }
}
