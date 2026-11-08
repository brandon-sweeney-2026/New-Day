using UnityEngine;

/// <summary>
/// A door. Press the action button in front of it to walk through.
/// Use one on the outside of the house (leads INTO the room) and one inside by the exit (leads OUT).
/// Needs a Collider2D (a trigger works well).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HouseDoor : MonoBehaviour, IInteractable
{
    [Tooltip("Where the player appears on the other side (an empty child object just in front of the other door).")]
    [SerializeField] private Transform destination;
    [Tooltip("The room this door goes into. Leave empty for the door that leads OUT of the house.")]
    [SerializeField] private HouseInterior enterInterior;
    [SerializeField] private string prompt = "Open door";
    [Tooltip("Direction the player faces after walking through.")]
    [SerializeField] private Vector2 arriveFacing = Vector2.up;

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions player)
    {
        if (destination == null)
        {
            GameEvents.ShowToast("This door doesn't lead anywhere yet.");
            return;
        }

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = destination.position; rb.linearVelocity = Vector2.zero; }
        player.transform.position = destination.position;

        if (enterInterior != null) enterInterior.EnterRoom();
        else HouseInterior.LeaveAnyRoom();

        if (player.Controller != null) player.Controller.FaceDirection(arriveFacing);
    }
}
