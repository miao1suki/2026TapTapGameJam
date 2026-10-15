using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum DialogueBubbleAlignment
{
    [InspectorName("居中")] Center = 0,
    [InspectorName("靠左")] Left = 1,
    [InspectorName("靠右")] Right = 2,
    [InspectorName("左上")] TopLeft = 3,
    [InspectorName("上方")] Top = 4,
    [InspectorName("右上")] TopRight = 5,
    [InspectorName("左下")] BottomLeft = 6,
    [InspectorName("下方")] Bottom = 7,
    [InspectorName("右下")] BottomRight = 8
}

public class DialogueTimelineClip : PlayableAsset, ITimelineClipAsset
{
    [Header("文本内容")]
    [TextArea(2, 6)]
    [Tooltip("气泡中显示的完整对话文本")]
    public string dialogueText = "……";
    [Tooltip("项目统一字体；留空时读取 DialogueBubbleDefaults")]
    public TMP_FontAsset font;
    [Min(.01f)]
    [Tooltip("文字基础字号")]
    public float fontSize = 2.6f;
    [Min(.01f)]
    [Tooltip("自动缩放时允许的最小字号")]
    public float minFontSize = 1.2f;
    [Tooltip("开启后文字会在气泡范围内自动缩放")]
    public bool autoSizeText;
    [Tooltip("行间距")]
    public float lineSpacing;
    [Tooltip("文字在气泡内的对齐方式")]
    public DialogueBubbleAlignment alignment =
        DialogueBubbleAlignment.Center;
    [Tooltip("文字颜色")]
    public Color textColor = Color.black;
    [Tooltip("是否启用文字描边")]
    public bool enableTextOutline;
    [Tooltip("文字描边颜色")]
    public Color textOutlineColor = Color.white;
    [Range(0f, 1f)]
    [Tooltip("文字描边宽度")]
    public float textOutlineWidth = .12f;
    [Tooltip("是否允许 TMP 富文本标签")]
    public bool richText = true;
    [Tooltip("是否按字符逐个显示")]
    public bool revealTextByCharacters = true;
    [Min(0f)]
    [Tooltip("上浮完成后、开始逐字显示前的等待时间")]
    public float typewriterStartDelay = .15f;
    [Min(.01f)]
    [Tooltip("打字机每秒显示多少个字符")]
    public float charactersPerSecond = 18f;

    [Header("气泡外观")]
    [Tooltip("气泡 Sprite；留空时读取 DialogueBubbleDefaults")]
    public Sprite bubbleSprite;
    [Tooltip("气泡材质；留空时读取 DialogueBubbleDefaults")]
    public Material bubbleMaterial;
    [Tooltip("气泡整体染色")]
    public Color bubbleTint = Color.white;
    [Tooltip("气泡在角色本地空间中的大小")]
    public Vector2 bubbleSize = new Vector2(2.8f, 2.1f);
    [Tooltip("文字四周留白")]
    public Vector2 textPadding = new Vector2(.34f, .28f);
    [Tooltip("保持气泡原始比例；关闭后按 bubbleSize 拉伸")]
    public bool preserveAspect = true;
    [Tooltip("Sprite 有九宫格边框时使用 Sliced 缩放")]
    public bool sliced;
    [Tooltip("Unity Sorting Layer 名称")]
    public string sortingLayerName = "Default";
    [Tooltip("气泡排序序号；文字固定在其上方一层")]
    public int sortingOrder = 100;
    [Tooltip("是否持续跟随绑定的角色")]
    public bool followBinding = true;
    [Tooltip("是否始终朝向主相机")]
    public bool faceCamera = true;

    [Header("浮现动画")]
    [Tooltip("片段开始时的本地位置")]
    public Vector3 startLocalOffset =
        new Vector3(0f, 1.25f, 0f);
    [Tooltip("浮现结束后的本地位置")]
    public Vector3 endLocalOffset =
        new Vector3(0f, 1.65f, 0f);
    [Min(0f)]
    [Tooltip("自下向上浮现的持续时间")]
    public float riseDuration = .6f;
    [Tooltip("浮现位移曲线")]
    public AnimationCurve riseCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("气泡起始缩放")]
    public Vector3 startScale =
        new Vector3(.82f, .82f, 1f);
    [Tooltip("气泡结束缩放")]
    public Vector3 endScale = Vector3.one;
    [Min(0f)]
    [Tooltip("整体透明度淡入时长")]
    public float fadeInDuration = .18f;
    [Tooltip("淡入曲线")]
    public AnimationCurve fadeInCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Min(0f)]
    [Tooltip("片段结束前淡出时长")]
    public float fadeOutDuration = .18f;
    [Tooltip("淡出曲线")]
    public AnimationCurve fadeOutCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        DialogueBehaviour behaviour =
            new DialogueBehaviour
            {
                clip = this
            };
        return ScriptPlayable<DialogueBehaviour>.Create(
            graph,
            behaviour);
    }
}
