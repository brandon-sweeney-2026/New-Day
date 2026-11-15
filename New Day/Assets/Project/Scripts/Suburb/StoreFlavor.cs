using UnityEngine;

/// <summary>Something in a shop you can look at with the action button and it says a line (produce bins, bins, posters...).</summary>
[RequireComponent(typeof(Collider2D))]
public class StoreFlavor : MonoBehaviour, IInteractable
{
    public string prompt = "Look";
    [TextArea] public string[] lines = { "Nothing special." };

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions player)
    {
        if (lines == null || lines.Length == 0) return;
        GameEvents.ShowToast(lines[Random.Range(0, lines.Length)]);
    }
}
