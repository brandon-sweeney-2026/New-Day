using UnityEngine;

/// <summary>
/// The front door of a suburb house. Works like HouseDoor, plus opening hours and conditions, so houses can be
/// "closed" at night or until something has happened. Everything is editable in the Inspector.
///   - Open From / Open To: hours of the day (24h; 8 and 21 = open 8 am to 9 pm).
///   - Conditions: extra requirements (a game flag, season, a counter...) using the same options as NPC routes.
///   - Closed Line: what shows when you try the door while it's closed.
/// Needs a Collider2D (a trigger works well).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SuburbHouseDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private string houseName = "House";
    [Tooltip("Where the player appears inside (an empty object just inside the front door).")]
    [SerializeField] private Transform destination;
    [SerializeField] private HouseInterior enterInterior;
    [SerializeField] private Vector2 arriveFacing = Vector2.up;

    [Header("Opening hours")]
    [Range(0f, 24f)] [SerializeField] private float openFrom = 8f;
    [Range(0f, 24f)] [SerializeField] private float openTo = 21f;
    [Tooltip("Tick to ignore the hours (always open).")]
    [SerializeField] private bool alwaysOpen = false;
    [Tooltip("Extra requirements. Empty = none.")]
    [SerializeField] private NpcCondition conditions = new NpcCondition();
    [TextArea] [SerializeField] private string closedLine = "Nobody answers. Maybe come back in the morning.";

    [Header("Animation")]
    [Tooltip("Seconds the screen takes to fade to black (and the same again to fade back in). 0 = no fade.")]
    [Range(0f, 1f)] [SerializeField] private float fadeSeconds = 0.18f;

    private DoorSwing swing;

    public string InteractPrompt { get { return IsOpen() ? "Enter " + houseName : "Knock"; } }

    private void Awake()
    {
        // The front door painted on the house swings open when you go in (see DoorSwing).
        swing = GetComponent<DoorSwing>();
        if (swing == null) swing = gameObject.AddComponent<DoorSwing>();
    }

    public bool IsOpen()
    {
        if (conditions != null && !conditions.Matches()) return false;
        if (alwaysOpen) return true;
        TimeManager tm = TimeManager.Instance;
        if (tm == null) return true;
        float h = tm.HoursDecimal;
        if (openFrom <= openTo) return h >= openFrom && h < openTo;
        return h >= openFrom || h < openTo;          // wraps past midnight
    }

    public void Interact(PlayerActions player)
    {
        if (!IsOpen()) { GameEvents.ShowToast(closedLine); return; }
        if (destination == null) { GameEvents.ShowToast("This door doesn't lead anywhere yet."); return; }

        // Door swings open, screen fades, player arrives inside (see DoorTransition).
        DoorTransition.Walk(player, swing, destination, arriveFacing, () =>
        {
            if (enterInterior != null) enterInterior.EnterRoom();
        }, fadeSeconds);
    }
}
