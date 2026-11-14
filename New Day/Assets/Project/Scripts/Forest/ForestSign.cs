using UnityEngine;

/// <summary>
/// A signpost the player can read (press the action button). Use it to explain things: how the forest works, what a stump wants, how to fight the shadows.
/// Edit the pages in the Inspector. Place signs with the Level Editor (Objects palette) or by dragging the ForestSign prefab into the scene.
/// Needs a trigger Collider2D (added automatically).
/// </summary>
public class ForestSign : MonoBehaviour, IInteractable
{
    [Tooltip("Shown as the speaker's name.")]
    public string signTitle = "Signpost";
    [Tooltip("Each entry is one page of the sign. Press the action button to turn the page.")]
    [TextArea(2, 6)] public string[] pages = { "Write what the sign says here." };
    public Color nameColor = new Color(0.55f, 0.35f, 0.15f);
    [Tooltip("The little prompt shown next to the sign.")]
    public string prompt = "Read the sign";

    private DialogueSpeaker speaker;

    public string InteractPrompt { get { return prompt; } }

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.7f; c.isTrigger = true;
        }
        speaker = new DialogueSpeaker { speakerName = signTitle, nameColor = nameColor };
    }

    public void Interact(PlayerActions player)
    {
        if (DialogueBox.IsOpen) return;
        if (pages == null || pages.Length == 0) { GameEvents.ShowToast("The sign is blank."); return; }
        if (speaker == null) speaker = new DialogueSpeaker { speakerName = signTitle, nameColor = nameColor };
        speaker.speakerName = signTitle;
        DialogueBox.Show(speaker, pages);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.9f, 0.7f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.9f, 1f, 0f));
    }
}
