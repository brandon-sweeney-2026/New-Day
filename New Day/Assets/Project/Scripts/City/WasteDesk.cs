using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the player gets rid of trash and polluted things. Used in two ways:
///   - at the WASTE TREATMENT PLANT (Into River = off): costs a fee, and helps the river a little;
///   - on the RIVER BANK (Into River = on): free, but everything thrown in makes the river dirtier, which hurts the forest and the farm.
///
/// What counts as waste: the items in 'Trash Items', and any stack with pollution on it (crops grown in polluted soil).
/// THE FEE: Fee Percent (20%) of an item's sell price, for each item. Handing over everything at once is charged at the
/// CHEAPEST item's price for all of them (when 'Cheapest Sets The Fee' is ticked), so bringing a mixed load is a good deal.
/// Needs a Collider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WasteDesk : MonoBehaviour, IInteractable
{
    [SerializeField] private string title = "Waste Treatment";
    [SerializeField] private string prompt = "Hand over waste";
    [Tooltip("ON = this is the river bank: no fee, but the river gets dirtier.")]
    [SerializeField] private bool intoRiver = false;

    [Header("What counts as waste")]
    [SerializeField] private List<ItemData> trashItems = new List<ItemData>();
    [Tooltip("Also take anything with pollution on it (crops from polluted soil).")]
    [SerializeField] private bool takePollutedItems = true;

    [Header("The fee (treatment plant only)")]
    [Range(0f, 1f), Tooltip("Part of the item's sell price charged for each item. 0.2 = 20%.")]
    [SerializeField] private float feePercent = 0.2f;
    [Min(0), Tooltip("The least one item ever costs to treat.")]
    [SerializeField] private int minimumFeePerItem = 1;
    [Tooltip("'Everything' is charged at the cheapest item's price for all of it.")]
    [SerializeField] private bool cheapestSetsTheFee = true;

    private PlayerActions player;

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions p)
    {
        player = p;
        MenuUI.Open(title, BuildRows, intoRiver ? "Up/Down: choose     Space: throw it in     Esc: close" : "Up/Down: choose     Space: hand over     Esc: close");
    }

    private bool IsWaste(ItemStack s)
    {
        if (s == null || s.IsEmpty) return false;
        if (s.item.isWaste || trashItems.Contains(s.item)) return true;
        return takePollutedItems && s.pollution > 0;
    }

    private int FeeEach(ItemData item)
    {
        if (intoRiver) return 0;
        return Mathf.Max(minimumFeePerItem, Mathf.RoundToInt(item.sellPrice * feePercent));
    }

    private List<MenuRow> BuildRows()
    {
        Inventory inv = player != null ? player.Inventory : null;
        var rows = new List<MenuRow>();
        if (inv == null) return rows;

        int allCount = 0, allFee = 0, cheapest = int.MaxValue;
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            ItemStack s = inv.GetSlot(i);
            if (!IsWaste(s)) continue;
            int slot = i;
            int fee = FeeEach(s.item) * s.count;
            allCount += s.count; allFee += fee; cheapest = Mathf.Min(cheapest, FeeEach(s.item));
            string dirty = s.pollution > 0 ? "  [" + PollutionSettings.Current.Label(s.pollution) + " pollution]" : "";
            rows.Add(new MenuRow
            {
                label = s.item.itemName + " x" + s.count + dirty,
                right = intoRiver ? "free" : fee + "g",
                enabled = GameState.Gold >= fee,
                detail = intoRiver ? "Into the river it goes. Nobody is watching... but the river remembers."
                                   : FeeEach(s.item) + "g each (" + Mathf.RoundToInt(feePercent * 100f) + "% of what it is worth).   Gold: " + GameState.Gold + "g",
                onSelect = () => Dispose(inv, slot, slot, -1)
            });
        }

        if (allCount > 0 && rows.Count > 1)
        {
            if (!intoRiver && cheapestSetsTheFee) allFee = cheapest * allCount;
            int total = allFee;
            rows.Insert(0, new MenuRow
            {
                label = "Everything (" + allCount + " items)",
                right = intoRiver ? "free" : total + "g",
                enabled = GameState.Gold >= total,
                color = new Color(1f, 0.9f, 0.5f),
                detail = intoRiver ? "Tip the whole lot into the river."
                                   : (cheapestSetsTheFee ? "A mixed load is charged at the cheapest item's rate: " + cheapest + "g each." : "All of it at once.") + "   Gold: " + GameState.Gold + "g",
                onSelect = () => Dispose(inv, 0, Inventory.SlotCount - 1, total)
            });
        }
        if (rows.Count == 0)
            rows.Add(new MenuRow { label = "(you have no waste)", enabled = false, detail = "Trash, sludge, scrap and anything with pollution on it can be handed over here." });
        return rows;
    }

    /// <summary>Gets rid of the waste in slots first..last. 'fixedFee' below 0 = add up each item's own fee.</summary>
    private void Dispose(Inventory inv, int first, int last, int fixedFee)
    {
        int fee = 0, count = 0;
        for (int i = first; i <= last; i++)
        {
            ItemStack s = inv.GetSlot(i);
            if (!IsWaste(s)) continue;
            fee += FeeEach(s.item) * s.count;
            count += s.count;
        }
        if (count == 0) return;
        if (fixedFee >= 0) fee = fixedFee;
        if (!GameState.TrySpendGold(fee)) { GameEvents.ShowToast("Not enough gold for the fee (" + fee + "g)."); return; }

        for (int i = first; i <= last; i++)
        {
            ItemStack s = inv.GetSlot(i);
            if (IsWaste(s)) inv.RemoveFromSlot(i, s.count);
        }

        if (intoRiver)
        {
            GameState.AddCounter(RiverPollution.DumpedKey, count);
            GameEvents.ShowToast("Splash. " + count + " item" + (count == 1 ? "" : "s") + " in the river. It looks a little worse downstream.");
        }
        else
        {
            GameState.AddCounter(RiverPollution.TreatedKey, count);
            GameEvents.ShowToast("Treated " + count + " item" + (count == 1 ? "" : "s") + (fee > 0 ? " for " + fee + "g." : "."));
        }
    }
}
