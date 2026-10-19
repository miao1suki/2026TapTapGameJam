using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Project.CameraModes;
using Project.Player;
using Project.WordNote;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace Project.StoryStart
{
    internal sealed class StoryStartCutsceneRunner :
        MonoBehaviour
    {
        private StoryStartTimelineSettings settings;
        private PlayableDirector director;
        private PlayerController player;
        private CameraControlManager cameraManager;
        private TimelineCamRig cameraRig;
        private CameraViewModeRequestHandle viewModeHandle;
        private IDisposable playerControlLock;
        private WordNoteHandle message;
        private GameObject curtainRoot;
        private RectTransform topCurtain;
        private RectTransform bottomCurtain;
        private RectTransform leftCurtain;
        private RectTransform rightCurtain;
        private Vector2 topBottomTarget;
        private Vector2 leftRightTarget;
        private Vector3 originCameraPosition;
        private Quaternion originCameraRotation;
        private float baseOrthographicSize;
        private float baseFieldOfView;
        private float lensMultiplier = 1f;
        private bool running;
        private bool cleaned;

        public void Begin(
            StoryStartTimelineSettings valueSettings,
            PlayableDirector valueDirector)
        {
            settings = valueSettings ??
                       new StoryStartTimelineSettings();
            director = valueDirector;
            running = true;
            StartCoroutine(Run());
        }

        public void Abort()
        {
            running = false;
            StopAllCoroutines();
            Cleanup();
        }

        private IEnumerator Run()
        {
            if (!TryInitialize())
            {
                Finish();
                yield break;
            }

            BuildCurtains();
            yield return AnimateCurtains(0f, 1f);
            yield return ChangeLens(
                settings.lensMultiplier,
                settings.lensExpandDuration);
            yield return PlayLines(settings.openingLines);
            if (!running)
            {
                yield break;
            }

            Vector3 destination = player.transform.position;
            float cellSize = Mathf.Max(
                .01f,
                player.GridCellWorldSize);
            Vector3 leftPosition =
                destination +
                Vector3.left *
                Mathf.Max(
                    0f,
                    settings.playerLeftDistanceBlocks) *
                cellSize;
            player.SetPresentationPosition(leftPosition);
            yield return AnimateCurtains(1f, 0f);

            float distance = Vector3.Distance(
                leftPosition,
                destination);
            float moveDuration =
                distance /
                Mathf.Max(
                    .01f,
                    settings.playerReturnSpeedBlocksPerSecond *
                    cellSize);
            yield return MovePlayerAndRestoreLens(
                leftPosition,
                destination,
                moveDuration);

            yield return PlayLines(settings.closingLines);
            Finish();
        }

        private bool TryInitialize()
        {
            GameObject playerObject = FindTagged(
                settings.playerTag);
            player = playerObject != null
                ? playerObject.GetComponent<PlayerController>()
                : null;
            if (player == null)
            {
                Debug.LogError(
                    "[StoryStart] 场景中没有找到玩家。");
                return false;
            }
            if (WordNoteUI.Instance == null)
            {
                Debug.LogError(
                    "[StoryStart] 场景中缺少 WordNote_UI。");
                return false;
            }
            if (settings.font == null)
            {
                Debug.LogError(
                    "[StoryStart] 未配置 Cubic_11 SDF 字体。");
                return false;
            }

            cameraManager =
                ProjectDiscovery.FindFirst<
                    CameraControlManager>();
            if (cameraManager == null &&
                Camera.main != null)
            {
                cameraManager =
                    Camera.main.GetComponent<
                        CameraControlManager>();
            }
            if (cameraManager == null)
            {
                Debug.LogError(
                    "[StoryStart] 场景中缺少 CameraControlManager。");
                return false;
            }

            playerControlLock =
                player.AcquireControlLock(this);
            if (!player.BeginPresentationMotion())
            {
                Debug.LogError(
                    "[StoryStart] 无法接管玩家 Rigidbody。");
                return false;
            }

            cameraRig = cameraManager.GetComponent<
                            TimelineCamRig>() ??
                        cameraManager.gameObject.AddComponent<
                            TimelineCamRig>();
            StopCompetingCameraDirectors();
            if (cameraRig.IsTimelineControlling ||
                cameraRig.isPlayingAnim)
            {
                cameraRig.ReleaseTimelineControl(false);
            }
            viewModeHandle = cameraRig.RequestViewMode(
                CameraViewMode.Side2D,
                CameraTransition.Immediate);
            cameraRig.Acquire(0f);
            cameraRig.SetReturnDurationForCurrentShot(
                Mathf.Max(
                    0f,
                    settings.cameraReleaseSeconds));

            CameraState state = cameraManager.CurrentState;
            originCameraPosition = state.position;
            originCameraRotation = state.rotation;
            baseOrthographicSize = Mathf.Max(
                .01f,
                state.orthographicSize);
            baseFieldOfView = state.fieldOfView;
            ApplyCamera();
            return true;
        }

        private void StopCompetingCameraDirectors()
        {
            IReadOnlyList<PlayableDirector> directors =
                ProjectDiscovery.FindAll<PlayableDirector>(
                    true);
            for (int index = 0;
                 index < directors.Count;
                 index++)
            {
                PlayableDirector candidate =
                    directors[index];
                if (candidate == null ||
                    candidate == director ||
                    candidate.state != PlayState.Playing ||
                    candidate.playableAsset is not
                        TimelineAsset timeline)
                {
                    continue;
                }

                bool hasCameraTrack = false;
                foreach (TrackAsset track in
                         timeline.GetOutputTracks())
                {
                    if (track is CameraTimelineTrack)
                    {
                        hasCameraTrack = true;
                        break;
                    }
                }
                if (hasCameraTrack)
                {
                    candidate.Stop();
                }
            }
        }

        private IEnumerator PlayLines(string[] lines)
        {
            if (lines == null)
            {
                yield break;
            }
            for (int index = 0;
                 index < lines.Length && running;
                 index++)
            {
                yield return PlayLine(lines[index]);
                if (running &&
                    settings.messageGapSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        settings.messageGapSeconds);
                }
            }
        }

        private IEnumerator PlayLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            message = WordNoteUI.Instance.RequestTimeline(
                text,
                BuildStyle());
            while (running &&
                   message != null &&
                   message.State !=
                   WordNoteMessageState.Finished)
            {
                yield return null;
            }
            message = null;
        }

        private WordNoteStyle BuildStyle()
        {
            WordNoteStyle style = new WordNoteStyle
            {
                font = settings.font,
                fontSize = Mathf.Max(
                    1f,
                    settings.fontSize),
                bold = true,
                richText = true,
                fontColor = WordNoteColor.White,
                useOutline = true,
                outlineColor = WordNoteColor.Black,
                textAlignment =
                    WordNoteTextAlignment.Center,
                position = WordNotePosition.Center,
                participateInStackLayout = false,
                displayMode =
                    WordNoteDisplayMode.Timed,
                displayDuration = Mathf.Max(
                    0f,
                    settings.messageDisplaySeconds),
                useEnterAnimation = true,
                enterAnimation =
                    WordNoteAnimation.Typewriter,
                useExitAnimation = true,
                exitAnimation =
                    WordNoteAnimation.Typewriter
            };
            style.enterMotion = BuildTypewriterMotion();
            style.exitMotion = BuildTypewriterMotion();
            return style;
        }

        private WordNoteMotionSettings BuildTypewriterMotion()
        {
            return new WordNoteMotionSettings
            {
                useCombinedChannels = true,
                useFade = false,
                useTypewriter = true,
                typewriterCharactersPerSecond =
                    Mathf.Max(
                        1f,
                        settings
                            .typewriterCharactersPerSecond),
                duration = .01f,
                startAlpha = 1f,
                endAlpha = 1f,
                startScale = Vector3.one,
                endScale = Vector3.one
            };
        }

        private IEnumerator ChangeLens(
            float targetMultiplier,
            float duration)
        {
            float start = lensMultiplier;
            Tween tween = DOTween.To(
                    () => start,
                    value =>
                    {
                        lensMultiplier = value;
                        ApplyCamera();
                    },
                    targetMultiplier,
                    Mathf.Max(0f, duration))
                .SetEase(Ease.InOutCubic)
                .SetUpdate(true).SetTarget(this);
            yield return tween.WaitForCompletion();
            lensMultiplier = targetMultiplier;
            ApplyCamera();
        }

        private IEnumerator MovePlayerAndRestoreLens(
            Vector3 start,
            Vector3 end,
            float duration)
        {
            float startLens = lensMultiplier;
            Tween tween = DOTween.To(
                    () => 0f,
                    progress =>
                    {
                        player.SetPresentationPosition(
                            Vector3.Lerp(
                                start,
                                end,
                                progress));
                        lensMultiplier = Mathf.Lerp(
                            startLens,
                            1f,
                            progress);
                        ApplyCamera();
                    },
                    1f,
                    Mathf.Max(.01f, duration))
                .SetEase(Ease.InOutSine)
                .SetUpdate(true).SetTarget(this);
            yield return tween.WaitForCompletion();
            player.SetPresentationPosition(end);
            lensMultiplier = 1f;
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            if (cameraRig == null)
            {
                return;
            }
            cameraRig.SetShotLens(
                baseOrthographicSize *
                Mathf.Max(0f, lensMultiplier),
                baseFieldOfView);
            cameraRig.SetShotTransform(
                originCameraPosition,
                originCameraRotation);
        }

        private void BuildCurtains()
        {
            curtainRoot = new GameObject(
                "Story Start Curtains",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            Canvas canvas =
                curtainRoot.GetComponent<Canvas>();
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder =
                settings.curtainSortingOrder;
            CanvasScaler scaler =
                curtainRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.Expand;
            CanvasGroup group =
                curtainRoot.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            topCurtain = CreateCurtain(
                "Top",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(.5f, 1f));
            bottomCurtain = CreateCurtain(
                "Bottom",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(.5f, 0f));
            leftCurtain = CreateCurtain(
                "Left",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, .5f));
            rightCurtain = CreateCurtain(
                "Right",
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, .5f));

            Canvas.ForceUpdateCanvases();
            RectTransform rootRect =
                curtainRoot.GetComponent<RectTransform>();
            float width = Mathf.Max(
                1f,
                rootRect.rect.width);
            float height = Mathf.Max(
                1f,
                rootRect.rect.height);
            float overscan = Mathf.Max(
                0f,
                settings.curtainOverscan);
            topBottomTarget = new Vector2(
                0f,
                height * (.5f + overscan));
            leftRightTarget = new Vector2(
                width * (.5f + overscan),
                0f);
            SetCurtainProgress(0f);
        }

        private RectTransform CreateCurtain(
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            GameObject panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(
                curtainRoot.transform,
                false);
            RectTransform rect =
                panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            Image image = panel.GetComponent<Image>();
            image.color = settings.curtainColor;
            image.raycastTarget = false;
            return rect;
        }

        private IEnumerator AnimateCurtains(
            float from,
            float to)
        {
            SetCurtainProgress(from);
            Tween tween = DOTween.To(
                    () => from,
                    SetCurtainProgress,
                    to,
                    Mathf.Max(
                        0f,
                        to > from
                            ? settings.curtainCoverDuration
                            : settings.curtainRevealDuration))
                .SetEase(Ease.InOutCubic)
                .SetUpdate(true).SetTarget(this);
            yield return tween.WaitForCompletion();
            SetCurtainProgress(to);
        }

        private void SetCurtainProgress(float progress)
        {
            float clamped =
                Mathf.Clamp01(progress);
            if (topCurtain != null)
            {
                topCurtain.sizeDelta =
                    topBottomTarget * clamped;
            }
            if (bottomCurtain != null)
            {
                bottomCurtain.sizeDelta =
                    topBottomTarget * clamped;
            }
            if (leftCurtain != null)
            {
                leftCurtain.sizeDelta =
                    leftRightTarget * clamped;
            }
            if (rightCurtain != null)
            {
                rightCurtain.sizeDelta =
                    leftRightTarget * clamped;
            }
        }

        private void Finish()
        {
            if (cleaned)
            {
                return;
            }
            running = false;
            Cleanup();
            director?.Stop();
        }

        private void Cleanup()
        {
            if (cleaned)
            {
                return;
            }
            cleaned = true;
            running = false;

            if (message != null)
            {
                message.ForceDisappear();
                message.Release(false);
                message = null;
            }

            ReleaseCamera();
            if (player != null)
            {
                player.EndPresentationMotion();
            }
            playerControlLock?.Dispose();
            playerControlLock = null;

            if (curtainRoot != null)
            {
                DOTween.Kill(curtainRoot);
                Destroy(curtainRoot);
                curtainRoot = null;
            }
            DOTween.Kill(this);
        }

        private void ReleaseCamera()
        {
            if (viewModeHandle.IsValid)
            {
                if (cameraRig != null)
                {
                    cameraRig.ReleaseViewMode(
                        viewModeHandle);
                }
                else
                {
                    viewModeHandle.Release(true);
                }
            }
            viewModeHandle = default;
            if (cameraRig != null &&
                cameraRig.IsTimelineControlling)
            {
                cameraRig.ReleaseTimelineControl(true);
            }
            cameraRig = null;
            cameraManager = null;
        }

        private static GameObject FindTagged(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }
            try
            {
                return GameObject.FindGameObjectWithTag(
                    tag);
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
