using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A general store. Press the action button in front of it to buy seeds and building materials with gold.
/// Fill 'stock' with the items it sells; the price is each item's Buy Price.
/// Left/Right changes how many you buy at once. Needs a Collider2D (a trigger works well).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShopCounter : MonoBehaviour, IInteractable
{
    [SerializeField] private string shopName = "General Store";
    [SerializeField] private List<ItemData> stock = new List<ItemData>();
    [Tooltip("Seeds are only shown in the seasons they can be planted in (each seed's Plant In Spring / Summer / Autumn boxes).")]
    [SerializeField] private bool hideOutOfSeasonSeeds = true;
    [Range(0.1f, 2f), Tooltip("Every price in this shop is multiplied by this. 1 = full price, 0.95 = 5% off (the Sterling tower in the city).")]
    [SerializeField] private float priceMultiplier = 1f;

    /// <summary>What one of this item costs in this shop (never less than 1g).</summary>
    private int PriceOf(ItemData item) { return Mathf.Max(1, Mathf.RoundToInt(item.buyPrice * priceMultiplier)); }

    private static readonly int[] Quantities = { 1, 5, 10 };
    private int quantityIndex;
    private PlayerActions player;

    public string InteractPrompt { get { return "Shop"; } }

    public void Interact(PlayerActions p)
    {
        player = p;
        quantityIndex = 0;
        MenuUI.Open(shopName, BuildRows, "Up/Down: choose     Left/Right: amount     Space: buy     Esc: close");
    }

    private List<MenuRow> BuildRows()
    {
        Inventory inv = player != null ? player.Inventory : null;
        int qty = Quantities[quantityIndex];
        var rows = new List<MenuRow>();

        // Seeds are only on the shelf in the seasons they can be planted (so nothing in winter).
        bool knowSeason = TimeManager.Instance != null;
        TimeManager.Season season = knowSeason ? TimeManager.Instance.CurrentSeason : TimeManager.Season.Spring;
        int seedsHidden = 0;

        foreach (ItemData stocked in stock)
        {
            if (stocked == null) continue;
            ItemData item = stocked;
            bool isSeed = item.type == ItemData.ItemType.Seed;
            if (isSeed && hideOutOfSeasonSeeds && knowSeason && !item.CanPlantIn(season)) { seedsHidden++; continue; }

            int total = PriceOf(item) * qty;
            bool affordable = GameState.Gold >= total;

            rows.Add(new MenuRow
            {
                label = item.itemName,
                right = "x" + qty + "   " + total + "g",
                enabled = affordable,
                detail = (string.IsNullOrEmpty(item.description) ? "" : item.description + "\n")
                         + (isSeed ? "Plant in: " + item.PlantSeasonsText() + "\n" : "")
                         + "You have " + (inv != null ? inv.CountOf(item) : 0) + "   -   Gold: " + GameState.Gold + "g",
                onLeft = () => quantityIndex = Mathf.Max(0, quantityIndex - 1),
                onRight = () => quantityIndex = Mathf.Min(Quantities.Length - 1, quantityIndex + 1),
                onSelect = () => Buy(item, qty, inv)
            });
        }

        if (seedsHidden > 0)
            rows.Add(new MenuRow
            {
                label = season == TimeManager.Season.Winter ? "(no seeds in winter)" : "(" + seedsHidden + " seeds out of season)",
                enabled = false,
                detail = season == TimeManager.Season.Winter ? "Nothing grows in winter. Seeds are back in spring." : "Some seeds are only sold in the seasons they grow in."
            });
        if (rows.Count == 0) rows.Add(new MenuRow { label = "(nothing for sale)", enabled = false });
        return rows;
    }

    private void Buy(ItemData item, int qty, Inventory inv)
    {
        if (inv == null) return;
        int total = PriceOf(item) * qty;

        if (GameState.Gold < total) { GameEvents.ShowToast("Not enough gold."); return; }
        if (inv.CanFit(item, qty) < qty) { GameEvents.ShowToast("Your backpack is full."); return; }

        GameState.TrySpendGold(total);
        inv.Add(item, qty);
        GameEvents.ShowToast("Bought " + qty + " " + item.itemName);
        SterlingInfluence.NoteSale(priceMultiplier);   // buying at the Sterling tower (the only discount shop) brings more litter to the farm
    }
}
