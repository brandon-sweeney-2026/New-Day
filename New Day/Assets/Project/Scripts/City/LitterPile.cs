using UnityEngine;

/// <summary>
/// A pile of rubbish on a city street. Press the action button to pick it up: you get a few waste items
/// (take them to the treatment plant, or... the river is right there). The pile is back the next morning.
/// Every piece picked up counts towards the counter litter_collected (City Hall has a job for that).
/// Needs a Collider2D (a trigger is fine) and a SpriteRenderer.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LitterPile : MonoBehaviour, IInteractable
{
    [Tooltip("A unique name for this pile, so the game remembers which ones were picked up today.")]
    public string pileId = "pile_1";
    [Tooltip("One of these is given for each piece (picked at random).")]
    public ItemData[] items = new ItemData[0];
    [Tooltip("How many pieces: least and most.")]
    public Vector2Int pieces = new Vector2Int(1, 3);

    private SpriteRenderer sr;
    private Collider2D col;

    public string InteractPrompt { get { return "Pick up litter"; } }

    private string Key { get { return "litter_day_" + pileId; } }
    private static int Today { get { return TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 1; } }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    private void Start()
    {
        Refresh();
        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay += Refresh;
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay -= Refresh;
    }

    /// <summary>Shown unless it was picked up today.</summary>
    private void Refresh()
    {
        bool here = GameState.GetCounter(Key) != Today;
        if (sr != null) sr.enabled = here;
        if (col != null) col.enabled = here;
    }

    public void Interact(PlayerActions player)
    {
        if (items == null || items.Length == 0 || player == null || player.Inventory == null) return;
        int count = Random.Range(pieces.x, pieces.y + 1), got = 0;
        for (int i = 0; i < count; i++)
        {
            ItemData item = items[Random.Range(0, items.Length)];
            if (item == null) continue;
            if (player.Inventory.Add(item, 1) > 0) { GameEvents.ShowToast("Your backpack is full."); break; }
            got++;
        }
        if (got == 0) return;
        GameState.SetCounter(Key, Today);
        GameState.AddCounter("litter_collected", got);
        GameEvents.ShowToast("Picked up " + got + " piece" + (got == 1 ? "" : "s") + " of litter.");
        Refresh();
    }
}
