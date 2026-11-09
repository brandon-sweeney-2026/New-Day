using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The blue mailbox where you ship goods. Press the action button in front of it to open the shipping screen:
/// move items from your backpack into the box (and back out again if you change your mind).
/// Everything left in the box is SOLD at the end of the day, when you go to sleep or pass out, and the money is added then.
/// Crops that grew in polluted soil sell for less (see PollutionSettings).
///
/// Put it on an object with a Collider2D (a trigger works well). Farm > Shipping > Set Up Mailbox does it all for you.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShippingBin : MonoBehaviour, IInteractable
{
    public const int Columns = 10;
    public const int Rows = 2;
    public const int SlotCount = Columns * Rows;

    [Header("What can be shipped")]
    [SerializeField] private bool acceptCrops = true;
    [SerializeField] private bool acceptResources = true;
    [SerializeField] private bool acceptFood = true;
    [Tooltip("Multiplies every price (e.g. 1.2 for a lucrative market day).")]
    [SerializeField] private float priceMultiplier = 1f;

    [Header("Mailbox pictures (the setup menu fills these in)")]
    [SerializeField] private SpriteRenderer mailboxRenderer;
    [SerializeField] private Sprite emptySprite;          // flag down
    [SerializeField] private Sprite fullSprite;           // flag up: there is something inside

    [Header("Shipping screen art (optional)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite slotSprite;
    [SerializeField] private Sprite selectSprite;
    [SerializeField] private Sprite totalBoxSprite;
    [SerializeField] private Sprite envelopeSprite;

    [Header("Look of the total")]
    [SerializeField] private Color totalColor = new Color(0.3f, 0.95f, 0.4f);

    // The contents are shared by every mailbox, so they survive scene changes.
    private static readonly ItemStack[] pending = NewSlots();
    public static ItemStack[] Pending { get { return pending; } }

    private static ItemStack[] NewSlots()
    {
        var a = new ItemStack[SlotCount];
        for (int i = 0; i < a.Length; i++) a[i] = new ItemStack();
        return a;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        for (int i = 0; i < pending.Length; i++) { pending[i].item = null; pending[i].count = 0; pending[i].pollution = 0; }
    }

    public string InteractPrompt { get { return "Ship items"; } }
    public Color TotalColor { get { return totalColor; } }
    public Sprite PanelSprite { get { return panelSprite; } }
    public Sprite SlotSprite { get { return slotSprite; } }
    public Sprite SelectSprite { get { return selectSprite; } }
    public Sprite TotalBoxSprite { get { return totalBoxSprite; } }
    public Sprite EnvelopeSprite { get { return envelopeSprite; } }

    private void OnEnable()
    {
        FarmManager.OnBeforeDayAdvanced += Settle;
        RefreshSprite();
    }

    private void OnDisable()
    {
        FarmManager.OnBeforeDayAdvanced -= Settle;
    }

    public void Interact(PlayerActions player)
    {
        if (player.Inventory == null) return;
        ShippingUI.Open(this, player.Inventory);
    }

    // ------------------------------------------------------------------
    // The box's contents
    // ------------------------------------------------------------------

    public bool Accepts(ItemData item)
    {
        if (item == null) return false;
        switch (item.type)
        {
            case ItemData.ItemType.Crop: return acceptCrops;
            case ItemData.ItemType.Resource: return acceptResources;
            case ItemData.ItemType.Food: return acceptFood;
            default: return false;   // tools, weapons, spells and seeds can't be shipped
        }
    }

    public int PriceEach(ItemStack s)
    {
        if (s == null || s.IsEmpty) return 0;
        return Mathf.RoundToInt(s.item.GetSellPrice(s.pollution) * priceMultiplier);
    }

    /// <summary>The money you will get tonight for everything in the box.</summary>
    public int Total()
    {
        int total = 0;
        foreach (ItemStack s in pending) if (!s.IsEmpty) total += PriceEach(s) * s.count;
        return total;
    }

    public int ItemCount()
    {
        int n = 0;
        foreach (ItemStack s in pending) if (!s.IsEmpty) n += s.count;
        return n;
    }

    /// <summary>Puts items in the box (stacking with matching ones). Returns how many did NOT fit.</summary>
    public int TryAdd(ItemData item, int count, int pollution)
    {
        if (item == null || count <= 0) return count;
        int max = Mathf.Max(1, item.maxStack);

        foreach (ItemStack s in pending)
        {
            if (count <= 0) break;
            if (s.IsEmpty || s.item != item || s.pollution != pollution) continue;
            int move = Mathf.Min(count, max - s.count);
            s.count += move;
            count -= move;
        }
        foreach (ItemStack s in pending)
        {
            if (count <= 0) break;
            if (!s.IsEmpty) continue;
            int move = Mathf.Min(count, max);
            s.item = item; s.count = move; s.pollution = pollution;
            count -= move;
        }
        RefreshSprite();
        return count;
    }

    /// <summary>Takes up to 'count' out of one box slot. Returns how many were removed.</summary>
    public int RemoveFromSlot(int index, int count)
    {
        if (index < 0 || index >= SlotCount) return 0;
        ItemStack s = pending[index];
        if (s.IsEmpty) return 0;
        int removed = Mathf.Min(count, s.count);
        s.count -= removed;
        if (s.count <= 0) { s.item = null; s.count = 0; s.pollution = 0; }
        RefreshSprite();
        return removed;
    }

    public void RefreshSprite()
    {
        if (mailboxRenderer == null) mailboxRenderer = GetComponent<SpriteRenderer>();
        if (mailboxRenderer == null) return;
        Sprite s = ItemCount() > 0 ? fullSprite : emptySprite;
        if (s != null) mailboxRenderer.sprite = s;
    }

    // ------------------------------------------------------------------
    // End of the day: everything in the box is sold
    // ------------------------------------------------------------------

    private void Settle()
    {
        int gold = 0, items = 0;
        foreach (ItemStack s in pending)
        {
            if (s.IsEmpty) continue;

            gold += PriceEach(s) * s.count;
            items += s.count;

            GameState.AddCounter("items_sold", s.count);
            if (s.item.type == ItemData.ItemType.Crop) GameState.AddCounter("crops_sold", s.count);
            GameState.AddCounter("sold_" + s.item.itemName.ToLowerInvariant().Replace(' ', '_'), s.count);

            s.item = null; s.count = 0; s.pollution = 0;
        }

        RefreshSprite();
        if (items == 0) return;

        GameState.AddGold(gold);
        GameEvents.ShowToast("Shipped " + items + (items == 1 ? " item" : " items") + ": +" + gold + "g");
    }
}
