using UnityEngine;

/// <summary>
/// A door. Press the action button in front of it to walk through.
/// Use one on the outside of the house (leads INTO the room) and one inside by the exit (leads OUT).
/// Needs a Collider2D (a trigger works well).
/// Walking through plays a short animation (DoorTransition): the door painted on the building swings open
/// (DoorSwing, added automatically), the screen fades, and the front door swings shut behind you when you come out.
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

    [Header("Animation")]
    [Tooltip("Seconds the screen takes to fade to black (and the same again to fade back in). 0 = no fade.")]
    [Range(0f, 1f)] [SerializeField] private float fadeSeconds = 0.18f;

    private DoorSwing swing;

    public string InteractPrompt { get { return prompt; } }

    private void Awake()
    {
        // The swinging door. It stays inert if this door has no known building picture (for example inside a room).
        swing = GetComponent<DoorSwing>();
        if (swing == null) swing = gameObject.AddComponent<DoorSwing>();
    }

    public void Interact(PlayerActions player)
    {
        if (destination == null)
        {
            GameEvents.ShowToast("This door doesn't lead anywhere yet.");
            return;
        }

        DoorTransition.Walk(player, swing, destination, arriveFacing, () =>
        {
            if (enterInterior != null) enterInterior.EnterRoom();
            else HouseInterior.LeaveAnyRoom();
        }, fadeSeconds);
    }
}
