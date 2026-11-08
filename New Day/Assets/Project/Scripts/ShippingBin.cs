using UnityEngine;

/// <summary>
/// A shipping bin. Press the action button in front of it to sell. Payment is instant.
/// Crops that grew in polluted soil sell for less (see PollutionSettings).
/// Needs a Collider2D (a trigger works well).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShippingBin : MonoBehaviour, IInteractable
{
    public enum SellMode
    {
        [Tooltip("Sell only the stack in the slot you are holding.")] EquippedStack,
        [Tooltip("Sell every Crop item in your backpack at once.")] AllCrops
    }

    [SerializeField] private SellMode mode = SellMode.EquippedStack;
    [Tooltip("Item types the bin accepts.")]
    [SerializeField] private bool acceptCrops = true;
    [SerializeField] private bool acceptResources = true;
    [SerializeField] private bool acceptFood = true;
    [Tooltip("Multiplies every price (e.g. 1.2 for a lucrative market day).")]
    [SerializeField] private float priceMultiplier = 1f;

    public string InteractPrompt { get { return mode == SellMode.AllCrops ? "Sell all crops" : "Sell"; } }

    public void Interact(PlayerActions player)
    {
        Inventory inv = player.Inventory;
        if (inv == null) return;

        int totalGold = 0;
        int totalItems = 0;

        if (mode == SellMode.EquippedStack)
        {
            SellSlot(inv, inv.SelectedIndex, ref totalGold, ref totalItems);
        }
        else
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                ItemStack s = inv.GetSlot(i);
                if (!s.IsEmpty && s.item.type == ItemData.ItemType.Crop) SellSlot(inv, i, ref totalGold, ref totalItems);
            }
        }

        if (totalItems == 0)
        {
            GameEvents.ShowToast(mode == SellMode.AllCrops ? "You have no crops to sell." : "Hold something you want to sell.");
            return;
        }

        GameState.AddGold(totalGold);
        GameEvents.ShowToast("Sold " + totalItems + (totalItems == 1 ? " item" : " items") + " for " + totalGold + "g");
    }

    private void SellSlot(Inventory inv, int index, ref int gold, ref int items)
    {
        ItemStack s = inv.GetSlot(index);
        if (s == null || s.IsEmpty || !Accepts(s.item)) return;

        int pollution = s.pollution;
        ItemData item = s.item;
        int count = s.count;

        int each = Mathf.RoundToInt(item.GetSellPrice(pollution) * priceMultiplier);
        inv.RemoveFromSlot(index, count);

        gold += each * count;
        items += count;

        GameState.AddCounter("items_sold", count);
        if (item.type == ItemData.ItemType.Crop) GameState.AddCounter("crops_sold", count);
        GameState.AddCounter("sold_" + item.itemName.ToLowerInvariant().Replace(' ', '_'), count);
    }

    private bool Accepts(ItemData item)
    {
        switch (item.type)
        {
            case ItemData.ItemType.Crop: return acceptCrops;
            case ItemData.ItemType.Resource: return acceptResources;
            case ItemData.ItemType.Food: return acceptFood;
            default: return false;   // tools, weapons, spells and seeds can't be shipped
        }
    }
}
