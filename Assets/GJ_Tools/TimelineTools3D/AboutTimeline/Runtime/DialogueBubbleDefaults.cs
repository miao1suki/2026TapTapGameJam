using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DialogueBubbleDefaults",
    menuName = "2026TapTap/Timeline/对话气泡默认配置")]
public sealed class DialogueBubbleDefaults : ScriptableObject
{
    private const string ResourcePath = "UI/DialogueBubbleDefaults";

    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private Material bubbleMaterial;
    [SerializeField] private TMP_FontAsset font;

    private static DialogueBubbleDefaults cached;

    public Sprite BubbleSprite => bubbleSprite;

    public Material BubbleMaterial => bubbleMaterial;

    public TMP_FontAsset Font => font;

    public static DialogueBubbleDefaults Load()
    {
        if (cached != null)
        {
            return cached;
        }

        cached = Resources.Load<DialogueBubbleDefaults>(
            ResourcePath);
        return cached;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cached = null;
    }
}
