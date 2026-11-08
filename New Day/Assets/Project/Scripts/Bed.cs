using UnityEngine;

/// <summary>
/// A bed. Press the action button in front of it to sleep: the day ends, crops grow, health and magic refill.
/// Needs a Collider2D (a trigger works well). The player wakes up at 'wakePoint' (an empty child object in front of the bed).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Bed : MonoBehaviour, IInteractable
{
    /// <summary>The bed the player wakes up in (the first one in the scene).</summary>
    public static Bed Main { get; private set; }

    [SerializeField] private Transform wakePoint;
    [Tooltip("Set this if the bed is inside the house, so the camera follows you indoors when you wake up.")]
    [SerializeField] private HouseInterior interior;
    [Tooltip("Earliest hour you may sleep (24h clock). 0 = any time.")]
    [SerializeField] private int earliestHour = 0;

    private void Awake()
    {
        if (Main == null) Main = this;
    }

    private void OnDestroy()
    {
        if (Main == this) Main = null;
    }

    public string InteractPrompt { get { return "Sleep"; } }

    public void Interact(PlayerActions player)
    {
        TimeManager time = TimeManager.Instance;
        if (time == null)
        {
            GameEvents.ShowToast("There is no TimeManager in the scene.");
            return;
        }

        // Hours after midnight (0-5) count as late, so only block if it's earlier than earliestHour during the day.
        if (time.HourOfDay >= 6 && time.HourOfDay < earliestHour)
        {
            GameEvents.ShowToast("You're not tired yet.");
            return;
        }

        time.Sleep();
        GameEvents.ShowToast("You slept well. " + time.GetDateString());
    }

    /// <summary>Moves a transform (the player) next to the bed.</summary>
    public void PlaceAtWakePoint(Transform t)
    {
        Vector3 target = wakePoint != null ? wakePoint.position : transform.position + Vector3.down;
        var rb = t.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = target; rb.linearVelocity = Vector2.zero; }
        t.position = target;

        if (interior != null) interior.EnterRoom();
        else HouseInterior.LeaveAnyRoom();
    }
}
