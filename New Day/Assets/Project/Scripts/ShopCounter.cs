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

        foreach (ItemData stocked in stock)
        {
            if (stocked == null) continue;
            ItemData item = stocked;
            int total = item.buyPrice * qty;
            bool affordable = GameState.Gold >= total;

            rows.Add(new MenuRow
            {
                label = item.itemName,
                right = "x" + qty + "   " + total + "g",
                enabled = affordable,
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
        int total = item.buyPrice * qty;

        if (GameState.Gold < total) { GameEvents.ShowToast("Not enough gold."); return; }
        if (inv.CanFit(item, qty) < qty) { GameEvents.ShowToast("Your backpack is full."); return; }

        GameState.TrySpendGold(total);
        inv.Add(item, qty);
        GameEvents.ShowToast("Bought " + qty + " " + item.itemName);
    }
}
