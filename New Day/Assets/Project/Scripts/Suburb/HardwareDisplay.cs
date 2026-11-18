using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A display in Frank's Home &amp; Hardware. Press the action button to browse it:
///   - 'Upgrades' = house upgrade categories you can BUY here (floors, windows, cabinets...). Leave empty for none.
///   - 'Materials' = loose building materials sold for gold (wood, paint, glass...). Leave empty for none.
/// If it has both, you pick which one first. Frank himself uses one with everything.
/// Needs a Collider2D (a solid one is fine).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HardwareDisplay : MonoBehaviour, IInteractable
{
    [SerializeField] private string title = "Hardware";
    [SerializeField] private string prompt = "Browse";
    [Tooltip("Which house upgrade categories this display sells. Empty = none.")]
    [SerializeField] private HouseCategory[] upgrades = new HouseCategory[0];
    [Tooltip("Materials sold for gold (each item's Buy Price).")]
    [SerializeField] private List<ItemData> materials = new List<ItemData>();
    [Tooltip("Only used if the display sells nothing: a line shown when you look at it.")]
    [TextArea] [SerializeField] private string lookLine = "";
    [Range(0.1f, 2f), Tooltip("Every price here is multiplied by this. 1 = full price, 0.95 = 5% off (the Sterling tower in the city).")]
    [SerializeField] private float priceMultiplier = 1f;

    /// <summary>What one of this item costs at this display (never less than 1g).</summary>
    private int PriceOf(ItemData item) { return Mathf.Max(1, Mathf.RoundToInt(item.buyPrice * priceMultiplier)); }

    private static readonly int[] Quantities = { 1, 5, 10 };
    private int quantityIndex;
    private PlayerActions player;

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions p)
    {
        player = p;
        quantityIndex = 0;
        bool hasUp = upgrades != null && upgrades.Length > 0;
        bool hasMat = materials != null && materials.Count > 0;

        if (hasUp && !hasMat) OpenUpgrades();
        else if (hasMat && !hasUp) OpenMaterials();
        else if (hasUp && hasMat) MenuUI.Open(title, BuildTop, "Up/Down: choose     Space: open     Esc: close");
        else if (!string.IsNullOrEmpty(lookLine)) GameEvents.ShowToast(lookLine);
    }

    private List<MenuRow> BuildTop()
    {
        return new List<MenuRow>
        {
            new MenuRow { label = "House upgrades", detail = "Floors, walls, windows, cabinets and more. Paid with gold and materials.", onSelect = OpenUpgrades },
            new MenuRow { label = "Building materials", detail = "Wood, paint, glass and more.", onSelect = OpenMaterials },
            new MenuRow { label = "Close", onSelect = MenuUI.CloseAll }
        };
    }

    private void OpenUpgrades()
    {
        HouseUpgradeData.PriceMultiplier = priceMultiplier;      // house upgrades cost this much here
        HouseCustomizer.OpenCategories(player, title, upgrades, true);
    }

    private void OpenMaterials()
    {
        MenuUI.Open(title + " - materials", BuildMaterialRows, "Up/Down: choose     Left/Right: amount     Space: buy     Esc: back");
    }

    private List<MenuRow> BuildMaterialRows()
    {
        Inventory inv = player != null ? player.Inventory : null;
        int qty = Quantities[quantityIndex];
        var rows = new List<MenuRow>();
        foreach (ItemData stocked in materials)
        {
            if (stocked == null) continue;
            ItemData item = stocked;
            int total = PriceOf(item) * qty;
            rows.Add(new MenuRow
            {
                label = item.itemName,
                right = "x" + qty + "   " + total + "g",
                enabled = GameState.Gold >= total,
                detail = (string.IsNullOrEmpty(item.description) ? "" : item.description + "\n") + "You have " + (inv != null ? inv.CountOf(item) : 0) + "   -   Gold: " + GameState.Gold + "g",
                onLeft = () => quantityIndex = Mathf.Max(0, quantityIndex - 1),
                onRight = () => quantityIndex = Mathf.Min(Quantities.Length - 1, quantityIndex + 1),
                onSelect = () => Buy(item, qty, inv)
            });
        }
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
        SterlingInfluence.NoteSale(priceMultiplier);   // Sterling Home (discounted) counts as dealing with Sterling
    }
}
