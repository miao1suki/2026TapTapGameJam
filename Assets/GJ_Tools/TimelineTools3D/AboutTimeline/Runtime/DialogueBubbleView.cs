using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueBubbleView : MonoBehaviour
{
    private const string RuntimeObjectName =
        "Timeline Dialogue Bubble (Runtime)";

    private DialogueTimelineClip clip;
    private SpriteRenderer bubbleRenderer;
    private TextMeshPro text;
    private Renderer textRenderer;
    private Transform target;
    private Vector3 fixedWorldPosition;
    private Quaternion fixedWorldRotation;
    private bool initialized;

    public static DialogueBubbleView Create(
        Transform target,
        DialogueTimelineClip clip)
    {
        if (target == null || clip == null)
        {
            return null;
        }

        DialogueBubbleDefaults defaults = DialogueBubbleDefaults.Load();
        Sprite sprite = clip.bubbleSprite != null
            ? clip.bubbleSprite
            : defaults != null
                ? defaults.BubbleSprite
                : null;
        TMP_FontAsset font = clip.font != null
            ? clip.font
            : defaults != null
                ? defaults.Font
                : null;
        Material material = clip.bubbleMaterial != null
            ? clip.bubbleMaterial
            : defaults != null
                ? defaults.BubbleMaterial
                : null;

        if (sprite == null || font == null)
        {
            Debug.LogWarning(
                "[DialogueTrack] 对话气泡缺少 Sprite 或 Cubic_11 SDF 字体，片段不会显示",
                clip);
            return null;
        }

        GameObject root = new GameObject(RuntimeObjectName);
        root.hideFlags = HideFlags.DontSave;
        DialogueBubbleView view =
            root.AddComponent<DialogueBubbleView>();
        view.Initialize(
            target,
            clip,
            sprite,
            material,
            font);
        return view;
    }

    private void Initialize(
        Transform bindTarget,
        DialogueTimelineClip timelineClip,
        Sprite sprite,
        Material material,
        TMP_FontAsset font)
    {
        target = bindTarget;
        clip = timelineClip;

        if (clip.followBinding)
        {
            transform.SetParent(target, false);
        }
        else
        {
            fixedWorldPosition = target.position;
            fixedWorldRotation = target.rotation;
            transform.position = fixedWorldPosition;
            transform.rotation = fixedWorldRotation;
        }

        GameObject bubbleObject = new GameObject(
            "Bubble",
            typeof(SpriteRenderer));
        bubbleObject.transform.SetParent(transform, false);
        bubbleRenderer =
            bubbleObject.GetComponent<SpriteRenderer>();
        bubbleRenderer.sprite = sprite;
        if (material != null)
        {
            bubbleRenderer.sharedMaterial = material;
        }

        ConfigureBubbleSize(sprite);

        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TextMeshPro));
        textObject.transform.SetParent(transform, false);

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(.5f, .5f);
        textRect.anchorMax = new Vector2(.5f, .5f);
        textRect.pivot = new Vector2(.5f, .5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(
            Mathf.Max(.05f, clip.bubbleSize.x - clip.textPadding.x * 2f),
            Mathf.Max(.05f, clip.bubbleSize.y - clip.textPadding.y * 2f));

        text = textObject.GetComponent<TextMeshPro>();
        text.font = font;
        text.fontSize = Mathf.Max(.01f, clip.fontSize);
        text.fontSizeMin = Mathf.Max(.01f, clip.minFontSize);
        text.fontSizeMax = Mathf.Max(text.fontSizeMin, clip.fontSize);
        text.enableAutoSizing = clip.autoSizeText;
        text.lineSpacing = clip.lineSpacing;
        text.alignment = GetAlignment(clip.alignment);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = clip.richText;
        text.raycastTarget = false;
        text.text = clip.dialogueText ?? string.Empty;
        text.outlineColor = clip.textOutlineColor;
        text.outlineWidth = clip.enableTextOutline
            ? Mathf.Clamp01(clip.textOutlineWidth)
            : 0f;
        text.color = clip.textColor;
        text.maxVisibleCharacters = clip.revealTextByCharacters
            ? 0
            : int.MaxValue;
        text.ForceMeshUpdate();

        textRenderer = text.renderer;
        ApplySortingOrder();
        initialized = true;
    }

    public void UpdateView(float time, float duration)
    {
        if (!initialized)
        {
            return;
        }

        float safeTime = Mathf.Max(0f, time);
        float rise = clip.riseDuration <= .0001f
            ? 1f
            : Mathf.Clamp01(safeTime / clip.riseDuration);
        float shapedRise = Evaluate(clip.riseCurve, rise);
        Vector3 offset = Vector3.Lerp(
            clip.startLocalOffset,
            clip.endLocalOffset,
            shapedRise);

        if (clip.followBinding)
        {
            transform.localPosition = offset;
        }
        else
        {
            transform.position =
                fixedWorldPosition + fixedWorldRotation * offset;
        }

        if (!clip.faceCamera)
        {
            transform.rotation = transform.parent != null
                ? transform.parent.rotation
                : fixedWorldRotation;
        }

        float scale = Evaluate(clip.riseCurve, rise);
        transform.localScale = Vector3.Lerp(
            clip.startScale,
            clip.endScale,
            scale);

        float fadeIn = clip.fadeInDuration <= .0001f
            ? 1f
            : Mathf.Clamp01(safeTime / clip.fadeInDuration);
        float fadeOut = 1f;
        if (clip.fadeOutDuration > .0001f && duration > 0f)
        {
            fadeOut = Mathf.Clamp01(
                (duration - safeTime) / clip.fadeOutDuration);
        }

        float alpha = Mathf.Clamp01(
            Evaluate(clip.fadeInCurve, fadeIn) *
            Evaluate(clip.fadeOutCurve, fadeOut));
        ApplyAlpha(alpha);
        ApplyVisibleCharacters(safeTime);
    }

    private void LateUpdate()
    {
        if (!clip.faceCamera)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            transform.rotation = camera.transform.rotation;
        }
    }

    private void ApplyVisibleCharacters(float time)
    {
        if (!clip.revealTextByCharacters)
        {
            text.maxVisibleCharacters = int.MaxValue;
            return;
        }

        float revealTime = Mathf.Max(
            0f,
            time -
            clip.riseDuration -
            clip.typewriterStartDelay);
        int visible = Mathf.FloorToInt(
            revealTime *
            Mathf.Max(.01f, clip.charactersPerSecond));
        int total = text.textInfo.characterCount;
        text.maxVisibleCharacters = Mathf.Clamp(
            visible,
            0,
            total);
    }

    private void ApplyAlpha(float alpha)
    {
        Color bubbleColor = clip.bubbleTint;
        bubbleColor.a *= alpha;
        bubbleRenderer.color = bubbleColor;

        Color color = clip.textColor;
        color.a *= alpha;
        text.color = color;

        Color outline = clip.textOutlineColor;
        outline.a *= alpha;
        text.outlineColor = outline;
    }

    private void ApplySortingOrder()
    {
        int layerId = SortingLayer.NameToID(
            clip.sortingLayerName);
        bubbleRenderer.sortingLayerID = layerId;
        bubbleRenderer.sortingOrder = clip.sortingOrder;
        textRenderer.sortingLayerID = layerId;
        textRenderer.sortingOrder = clip.sortingOrder + 1;
    }

    private void ConfigureBubbleSize(Sprite sprite)
    {
        Vector2 size = new Vector2(
            Mathf.Max(.05f, clip.bubbleSize.x),
            Mathf.Max(.05f, clip.bubbleSize.y));

        if (clip.sliced && sprite.border.sqrMagnitude > .0001f)
        {
            bubbleRenderer.drawMode = SpriteDrawMode.Sliced;
            bubbleRenderer.size = size;
            return;
        }

        Vector3 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= .0001f || spriteSize.y <= .0001f)
        {
            return;
        }

        if (clip.preserveAspect)
        {
            float aspectScale = Mathf.Min(
                size.x / spriteSize.x,
                size.y / spriteSize.y);
            bubbleRenderer.transform.localScale =
                Vector3.one * aspectScale;
            return;
        }

        bubbleRenderer.transform.localScale = new Vector3(
            size.x / spriteSize.x,
            size.y / spriteSize.y,
            1f);
    }

    private static TextAlignmentOptions GetAlignment(
        DialogueBubbleAlignment alignment)
    {
        switch (alignment)
        {
            case DialogueBubbleAlignment.TopLeft:
                return TextAlignmentOptions.TopLeft;
            case DialogueBubbleAlignment.Top:
                return TextAlignmentOptions.Top;
            case DialogueBubbleAlignment.TopRight:
                return TextAlignmentOptions.TopRight;
            case DialogueBubbleAlignment.Left:
                return TextAlignmentOptions.Left;
            case DialogueBubbleAlignment.Right:
                return TextAlignmentOptions.Right;
            case DialogueBubbleAlignment.BottomLeft:
                return TextAlignmentOptions.BottomLeft;
            case DialogueBubbleAlignment.Bottom:
                return TextAlignmentOptions.Bottom;
            case DialogueBubbleAlignment.BottomRight:
                return TextAlignmentOptions.BottomRight;
            default:
                return TextAlignmentOptions.Center;
        }
    }

    private static float Evaluate(
        AnimationCurve curve,
        float progress)
    {
        if (curve == null || curve.length < 2)
        {
            return Mathf.Clamp01(progress);
        }

        return Mathf.Clamp01(curve.Evaluate(
            Mathf.Clamp01(progress)));
    }

    private void OnDestroy()
    {
        if (bubbleRenderer != null)
        {
            bubbleRenderer.sprite = null;
        }
    }
}
