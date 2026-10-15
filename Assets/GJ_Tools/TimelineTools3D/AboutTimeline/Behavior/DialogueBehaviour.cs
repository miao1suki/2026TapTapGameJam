using UnityEngine;
using UnityEngine.Playables;

public class DialogueBehaviour : PlayableBehaviour
{
    public DialogueTimelineClip clip;

    private DialogueBubbleView view;
    private Transform binding;

    public override void OnBehaviourPlay(
        Playable playable,
        FrameData info)
    {
        DisposeView();
        binding = null;
    }

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        Transform currentBinding = playerData as Transform;
        if (clip == null || currentBinding == null)
        {
            DisposeView();
            binding = null;
            return;
        }

        if (view == null || binding != currentBinding)
        {
            DisposeView();
            binding = currentBinding;
            view = DialogueBubbleView.Create(
                currentBinding,
                clip);
        }

        if (view == null)
        {
            return;
        }

        view.UpdateView(
            (float)playable.GetTime(),
            (float)playable.GetDuration());
    }

    public override void OnBehaviourPause(
        Playable playable,
        FrameData info)
    {
        DisposeView();
        binding = null;
    }

    public override void OnGraphStop(Playable playable)
    {
        DisposeView();
        binding = null;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        DisposeView();
        binding = null;
    }

    private void DisposeView()
    {
        if (view == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(view.gameObject);
        }
        else
        {
            Object.DestroyImmediate(view.gameObject);
        }

        view = null;
    }
}
