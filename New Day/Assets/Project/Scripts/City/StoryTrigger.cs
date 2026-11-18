using UnityEngine;

/// <summary>
/// An invisible area that remembers the player walked into it: it sets a flag (once), and can show a message and add a quest
/// to the journal. Used for the tower's back room, where the player sees the barrels going into the river.
/// Put it on an object with a BoxCollider2D (it is made a trigger).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StoryTrigger : MonoBehaviour
{
    [Tooltip("The flag set when the player walks in, for example saw_tower_dumping. Nothing happens the second time.")]
    public string flag = "saw_something";
    [TextArea, Tooltip("Shown on screen the first time. Empty = no message.")]
    public string message = "";

    [Header("Quest journal (optional)")]
    [Tooltip("Empty = no quest is added.")]
    public string questId = "";
    public string questTitle = "";
    public string questGiver = "You";
    [TextArea] public string questText = "";

    private void Awake() { GetComponent<BoxCollider2D>().isTrigger = true; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(flag) || GameState.HasFlag(flag)) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        GameState.SetFlag(flag);
        if (!string.IsNullOrEmpty(message)) GameEvents.ShowToast(message);
        if (!string.IsNullOrEmpty(questId)) QuestLog.Add(questId, questTitle, questGiver, questText);
    }
}
