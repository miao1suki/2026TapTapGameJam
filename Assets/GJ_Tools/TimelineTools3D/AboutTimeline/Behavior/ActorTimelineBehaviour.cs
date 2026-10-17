using System;
using Project.BossLogic;
using Project.Player;
using UnityEngine;
using UnityEngine.Playables;

public sealed class ActorTimelineBehaviour : PlayableBehaviour
{
    public ActorTimelineClipBase clip;
    public GameObject owner;

    private Transform actor;
    private Rigidbody body;
    private PlayerController player;
    private BossController boss;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Quaternion startRotation;
    private bool initialized;
    private bool finished;
    private bool teleported;
    private bool jumpStarted;
    private bool preparedRigidbody;
    private bool warnedMissingActor;
    private bool previousKinematic;
    private bool previousUseGravity;
    private RigidbodyInterpolation previousInterpolation;
    private CollisionDetectionMode previousCollisionMode;
    private IDisposable playerControlLock;
    private Vector2 previousMoveInput;
    private bool previousSprint;
    private bool previousJumpHeld;
    private bool playerInputCaptured;
    private float activeMotionDuration = 1f;

    public override void OnBehaviourPlay(
        Playable playable,
        FrameData info)
    {
        Initialize();
    }

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        if (clip == null)
        {
            return;
        }

        if (!initialized)
        {
            Initialize();
        }

        ResolveActor(playerData);
        if (actor == null)
        {
            return;
        }

        float time = (float)playable.GetTime();
        TickMotion(time);
    }

    public override void OnBehaviourPause(
        Playable playable,
        FrameData info)
    {
        Finish();
    }

    public override void OnGraphStop(Playable playable)
    {
        Finish();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        Finish();
    }

    private void Initialize()
    {
        if (clip == null || initialized)
        {
            return;
        }

        TrySpawnBoss();
        ResolveActor(null);
        if (actor == null)
        {
            if (!warnedMissingActor)
            {
                warnedMissingActor = true;
                Debug.LogWarning(
                    $"[ActorTimeline] 找不到 Tag={clip.actorTag} 的角色。",
                    owner);
            }

            return;
        }

        initialized = true;
        warnedMissingActor = false;
        finished = false;
        teleported = false;
        jumpStarted = false;
        startPosition = actor.position;
        startRotation = actor.rotation;
        targetPosition = ResolveTargetPosition();
        body = actor.GetComponent<Rigidbody>();
        player = actor.GetComponent<PlayerController>();
        boss = actor.GetComponent<BossController>();
        activeMotionDuration = ResolveMotionDuration();

        if (clip is PlayerTimelineClip playerClip)
        {
            ApplyPlayerStart(playerClip);
        }
        else if (clip is BossTimelineClip bossClip)
        {
            ApplyBossStart(bossClip);
        }

        if (clip.motionMode == ActorTimelineMotionMode.JumpArc &&
            boss != null &&
            clip is BossTimelineClip jumpBossClip &&
            jumpBossClip.useBossJumpState)
        {
            jumpStarted = boss.RequestJump();
        }
        else if (boss != null &&
                 clip is BossTimelineClip &&
                 clip.motionMode == ActorTimelineMotionMode.Linear)
        {
            ApplyBossLinearStart();
        }
        else if (boss != null &&
                 clip is BossTimelineClip &&
                 clip.motionMode != ActorTimelineMotionMode.None)
        {
            // Boss movement stays inside BossController; no Rigidbody takeover.
        }
        else if (clip.motionMode != ActorTimelineMotionMode.None)
        {
            PrepareRigidbody();
        }

        if (clip.motionMode == ActorTimelineMotionMode.Teleport)
        {
            TeleportNow();
        }
    }

    private void ResolveActor(object playerData)
    {
        if (actor != null)
        {
            return;
        }

        if (clip != null && clip.useTrackBinding)
        {
            Transform bound = playerData as Transform;
            if (bound != null)
            {
                actor = bound;
                return;
            }
        }

        if (clip == null ||
            string.IsNullOrWhiteSpace(clip.actorTag))
        {
            return;
        }

        GameObject tagged =
            GameObject.FindGameObjectWithTag(clip.actorTag);
        if (tagged != null)
        {
            actor = tagged.transform;
        }
    }

    private Vector3 ResolveTargetPosition()
    {
        Transform origin =
            clip.useOwnerAsOrigin && owner != null
                ? owner.transform
                : null;
        return origin != null
            ? origin.TransformPoint(clip.targetOffset)
            : clip.targetOffset;
    }

    private void TrySpawnBoss()
    {
        if (actor != null ||
            clip is not BossTimelineClip bossClip ||
            !bossClip.spawnBoss ||
            bossClip.bossPrefab == null)
        {
            return;
        }

        BossController instance = UnityEngine.Object.Instantiate(
            bossClip.bossPrefab,
            ResolveTargetPosition(),
            bossClip.bossPrefab.transform.rotation);
        instance.name = bossClip.bossPrefab.name;
        actor = instance.transform;
    }

    private float ResolveMotionDuration()
    {
        return Mathf.Max(.01f, clip.motionDuration);
    }

    private void ApplyPlayerStart(PlayerTimelineClip playerClip)
    {
        if (player == null)
        {
            return;
        }

        previousMoveInput = player.MoveInput;
        previousSprint = player.SprintRequested;
        previousJumpHeld = player.JumpHeld;
        playerInputCaptured = true;

        if (playerClip.applyMoveInput)
        {
            player.SetMoveInput(playerClip.moveInput);
        }

        if (playerClip.applySprint)
        {
            player.SetSprintInput(playerClip.sprint);
        }

        if (playerClip.applyJumpHeld)
        {
            player.SetJumpHeld(playerClip.jumpHeld);
        }

        if (playerClip.requestJump)
        {
            if (playerClip.jumpBoostSpeed >= 0f)
            {
                player.ApplyJumpBoost(playerClip.jumpBoostSpeed);
            }

            player.RequestJump();
        }

        if (playerClip.lockControlWhileClip ||
            (clip.lockControlWhileDriving &&
             clip.motionMode != ActorTimelineMotionMode.None &&
             clip.driveRigidbody))
        {
            playerControlLock = player.AcquireControlLock(this);
        }
    }

    private void ApplyBossStart(BossTimelineClip bossClip)
    {
        if (boss == null)
        {
            return;
        }

        if (bossClip.setTrackingOnStart)
        {
            boss.SetTrackingEnabled(bossClip.trackingOnStart);
        }
    }

    private void ApplyBossLinearStart()
    {
        if (boss == null ||
            clip is not BossTimelineClip bossClip)
        {
            return;
        }

        boss.SetWalkInput(bossClip.moveInput);
    }

    private void PrepareRigidbody()
    {
        if (!clip.driveRigidbody || body == null)
        {
            return;
        }

        previousKinematic = body.isKinematic;
        previousUseGravity = body.useGravity;
        previousInterpolation = body.interpolation;
        previousCollisionMode = body.collisionDetectionMode;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
        preparedRigidbody = true;
    }

    private void TickMotion(float time)
    {
        if (clip.motionMode == ActorTimelineMotionMode.None ||
            clip.motionMode == ActorTimelineMotionMode.Teleport ||
            (boss != null && clip is BossTimelineClip) ||
            (jumpStarted &&
             clip is BossTimelineClip bossClip &&
             bossClip.useBossJumpState))
        {
            return;
        }

        float duration = activeMotionDuration;
        float progress = Mathf.Clamp01(time / duration);
        float curve = clip.motionCurve != null
            ? clip.motionCurve.Evaluate(progress)
            : progress;
        Vector3 position = Vector3.Lerp(
            startPosition,
            targetPosition,
            curve);
        if (clip.motionMode == ActorTimelineMotionMode.JumpArc)
        {
            position.y +=
                4f *
                clip.jumpArcHeight *
                progress *
                (1f - progress);
        }

        WritePosition(position);
    }

    private void TeleportNow()
    {
        if (teleported)
        {
            return;
        }

        teleported = true;
        if (boss != null)
        {
            boss.TeleportTo(targetPosition, startRotation);
        }
        else
        {
            WritePosition(targetPosition);
            actor.rotation = startRotation;
        }
    }

    private void WritePosition(Vector3 position)
    {
        if (actor == null)
        {
            return;
        }

        if (body != null)
        {
            body.position = position;
        }

        actor.position = position;
    }

    private void Finish()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        bool bossStateJump =
            jumpStarted &&
            clip is BossTimelineClip;
        bool bossControllerMotion =
            boss != null &&
            clip is BossTimelineClip &&
            clip.motionMode != ActorTimelineMotionMode.None;
        if (!bossStateJump &&
            !bossControllerMotion &&
            clip != null &&
            (clip.motionMode == ActorTimelineMotionMode.Linear ||
             clip.motionMode == ActorTimelineMotionMode.JumpArc))
        {
            WritePosition(targetPosition);
        }

        if (clip is PlayerTimelineClip playerClip)
        {
            FinishPlayer(playerClip);
        }
        else if (clip is BossTimelineClip bossClip)
        {
            FinishBoss(bossClip);
        }

        RestoreRigidbody();
        playerControlLock?.Dispose();
        playerControlLock = null;
        actor = null;
        body = null;
        player = null;
        boss = null;
        playerInputCaptured = false;
    }

    private void FinishPlayer(PlayerTimelineClip playerClip)
    {
        if (player == null)
        {
            return;
        }

        if (playerClip.restoreInputOnEnd &&
            playerInputCaptured)
        {
            player.SetMoveInput(previousMoveInput);
            player.SetSprintInput(previousSprint);
            player.SetJumpHeld(previousJumpHeld);
        }
        else
        {
            player.ClearBufferedInput();
        }
    }

    private void FinishBoss(BossTimelineClip bossClip)
    {
        if (boss == null)
        {
            return;
        }

        if (bossClip.setTrackingOnEnd)
        {
            boss.SetTrackingEnabled(bossClip.trackingOnEnd);
        }

        if (bossClip.motionMode == ActorTimelineMotionMode.Linear)
        {
            boss.ClearWalkInput();
        }

        if (bossClip.killAtEnd)
        {
            boss.Kill();
        }
    }

    private void RestoreRigidbody()
    {
        if (!preparedRigidbody || body == null)
        {
            return;
        }

        preparedRigidbody = false;
        body.useGravity = previousUseGravity;
        body.isKinematic = previousKinematic;
        body.interpolation = previousInterpolation;
        body.collisionDetectionMode = previousCollisionMode;
        if (clip.clearVelocity)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        body.WakeUp();
    }
}
