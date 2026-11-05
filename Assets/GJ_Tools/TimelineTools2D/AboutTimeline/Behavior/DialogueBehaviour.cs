using Project.WordNote;
using UnityEngine;
using UnityEngine.Playables;

public class DialogueBehaviour : PlayableBehaviour
{
    public DialogueTimelineClip clip;

    private WordNoteHandle handle;
    private bool attempted;

    public override void OnBehaviourPlay(
        Playable playable,
        FrameData info)
    {
        ResetMessage();
        TryShow();
    }

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        if (!attempted || handle == null)
        {
            TryShow();
        }
    }

    public override void OnBehaviourPause(
        Playable playable,
        FrameData info)
    {
        CloseMessage();
    }

    public override void OnGraphStop(Playable playable)
    {
        CloseMessage();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        CloseMessage();
    }

    private void TryShow()
    {
        if (attempted ||
            clip == null ||
            string.IsNullOrWhiteSpace(clip.dialogueText))
        {
            return;
        }

        WordNoteUI ui = WordNoteUI.Instance;
        if (ui == null)
        {
            return;
        }

        attempted = true;
        handle = ui.RequestTimeline(
            clip.dialogueText,
            clip.BuildStyle());
    }

    private void ResetMessage()
    {
        handle = null;
        attempted = false;
    }

    private void CloseMessage()
    {
        if (handle != null)
        {
            handle.Release();
        }

        ResetMessage();
    }
}
