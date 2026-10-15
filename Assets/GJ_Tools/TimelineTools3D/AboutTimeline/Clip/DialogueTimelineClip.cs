using Project.WordNote;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class DialogueTimelineClip : PlayableAsset, ITimelineClipAsset
{
    [Header("文本内容")]
    [TextArea(2, 6)]
    [Tooltip("交给 WordNote_UI 显示的完整文本")]
    public string dialogueText = "……";

    [Tooltip("与 WordNote_UI 完全一致的消息样式参数")]
    public WordNoteStyle style = new WordNoteStyle();

    public ClipCaps clipCaps => ClipCaps.None;

    public WordNoteStyle BuildStyle()
    {
        return style != null
            ? style.Clone()
            : new WordNoteStyle();
    }

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
