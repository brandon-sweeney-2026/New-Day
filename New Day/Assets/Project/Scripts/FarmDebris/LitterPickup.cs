using UnityEngine;

/// <summary>
/// A piece of pollution litter lying on the ground: a crushed can, a bit of debris, a black tar lump or a purple goo lump.
/// Stand in front of it and press the action button to pick it up. It goes in your backpack as a waste item, which you can
/// take to the treatment plant in the city (that cleans the river) or tip into the river (that makes it worse).
///
/// Lumps also soil the tile they sit on while they lie there ("Pollutes Tile"), and that tile is cleaned again when you pick them up.
/// Needs a Collider2D (a trigger, so the player can walk over it). Farm > Set Up > Farm Debris makes the prefabs.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LitterPickup : MonoBehaviour, IInteractable
{
    [Header("What you get")]
    [Tooltip("The waste item put in the backpack.")]
    [SerializeField] private ItemData item;
    [Min(1)] [SerializeField] private int amount = 1;

    [Header("Pollution")]
    [Range(0, PollutionSettings.MaxLevel), Tooltip("While it lies here, the tile under it is at least this polluted (0 = it doesn't pollute). Crops grow worse on polluted soil.")]
    [SerializeField] private int pollutesTile = 0;

    [Header("Text")]
    [Tooltip("Shown on screen when you stand in front of it. Empty = 'Pick up <item name>'.")]
    [SerializeField] private string prompt = "";

    public string InteractPrompt
    {
        get
        {
            if (!string.IsNullOrEmpty(prompt)) return prompt;
            return item != null ? "Pick up " + item.itemName : "Pick up";
        }
    }

    private Vector3Int cell;
    private int raisedBy;     // how much this lump raised its tile, so picking it up takes exactly that away again

    private void Start()
    {
        if (pollutesTile <= 0) return;
        PollutionMap map = PollutionMap.Instance;
        FarmManager farm = FindAnyObjectByType<FarmManager>();
        if (map == null || farm == null) return;

        // the object's position is at its feet; the tile is the one just above
        cell = farm.WorldToCell(transform.position + Vector3.up * 0.4f);
        int before = map.GetLevel(cell);
        if (before < pollutesTile)
        {
            map.SetLevel(cell, pollutesTile);
            raisedBy = pollutesTile - before;
        }
    }

    public void Interact(PlayerActions p)
    {
        if (p == null) return;
        Inventory inv = p.Inventory;
        if (item != null)
        {
            if (inv == null || inv.CanFit(item, amount) < amount) { GameEvents.ShowToast("Your backpack is full."); return; }
            inv.Add(item, amount);
            GameEvents.ShowToast("Picked up " + (amount > 1 ? amount + " " : "") + item.itemName + ".");
        }

        // the soil under a lump recovers once it is gone
        if (raisedBy > 0 && PollutionMap.Instance != null) PollutionMap.Instance.Add(cell, -raisedBy);
        raisedBy = 0;

        GameState.AddCounter("litter_collected");
        Destroy(gameObject);
    }
}
