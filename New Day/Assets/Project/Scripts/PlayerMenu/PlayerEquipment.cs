using System;
using UnityEngine;

/// <summary>
/// What the player is wearing: hat, armor, gloves, boots, two rings and a necklace.
///
/// An item can be worn when its ItemData has Type = Equipment and an Equip Slot. Its bonuses
/// (Defense, Bonus Max Health and so on) are numbers on the same ItemData; PlayerBonuses adds them up.
///
/// Wearing an item takes one out of the backpack; taking it off puts it back.
/// This is a plain static class (nothing to put in the scene), so what you wear stays on when you change scenes.
/// </summary>
public static class PlayerEquipment
{
    public const int SlotCount = 7;

    /// <summary>The name shown for each slot, in order.</summary>
    public static readonly string[] SlotNames = { "Hat", "Armor", "Gloves", "Boots", "Ring 1", "Ring 2", "Necklace" };

    /// <summary>The kind of item each slot accepts (both ring slots take a Ring).</summary>
    public static readonly ItemData.EquipSlot[] SlotKinds =
    {
        ItemData.EquipSlot.Hat, ItemData.EquipSlot.Armor, ItemData.EquipSlot.Gloves, ItemData.EquipSlot.Boots,
        ItemData.EquipSlot.Ring, ItemData.EquipSlot.Ring, ItemData.EquipSlot.Necklace,
    };

    private static readonly ItemData[] worn = new ItemData[SlotCount];

    /// <summary>Raised when something is put on or taken off.</summary>
    public static event Action OnChanged;

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        for (int i = 0; i < SlotCount; i++) worn[i] = null;
        OnChanged = null;
    }

    /// <summary>The item in a slot, or null when the slot is empty.</summary>
    public static ItemData Get(int slot)
    {
        return slot >= 0 && slot < SlotCount ? worn[slot] : null;
    }

    /// <summary>
    /// Puts back what a save file says was being worn (used by SaveSystem). 'items' has one entry per slot; null = empty.
    /// Nothing is taken from the backpack: a worn item was never in it.
    /// </summary>
    public static void LoadFromSave(ItemData[] items)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            ItemData item = items != null && i < items.Length ? items[i] : null;
            worn[i] = Fits(item, i) ? item : null;
        }
        Changed();
    }

    /// <summary>Can this item go in this slot?</summary>
    public static bool Fits(ItemData item, int slot)
    {
        return item != null && slot >= 0 && slot < SlotCount
            && item.type == ItemData.ItemType.Equipment && item.equipSlot == SlotKinds[slot];
    }

    /// <summary>Puts on an item from the backpack. Whatever was in the slot goes back into the backpack.</summary>
    public static bool Equip(int slot, ItemData item, Inventory inventory)
    {
        if (!Fits(item, slot) || inventory == null) return false;
        if (!inventory.Remove(item, 1)) return false;

        ItemData old = worn[slot];
        worn[slot] = item;
        if (old != null) GiveBack(old, inventory);      // there is always room: we just took one item out

        Changed();
        GameEvents.ShowToast("Put on " + item.itemName + ".");
        return true;
    }

    /// <summary>Takes an item off and puts it in the backpack. Returns false if the backpack is full.</summary>
    public static bool Unequip(int slot, Inventory inventory)
    {
        ItemData old = Get(slot);
        if (old == null || inventory == null) return false;
        if (inventory.CanFit(old, 1) < 1)
        {
            GameEvents.ShowToast("Your backpack is full.");
            return false;
        }

        worn[slot] = null;
        inventory.Add(old, 1);
        Changed();
        GameEvents.ShowToast("Took off " + old.itemName + ".");
        return true;
    }

    private static void GiveBack(ItemData item, Inventory inventory)
    {
        int leftover = inventory.Add(item, 1);
        // No room after all? Drop it at the player's feet rather than lose it.
        if (leftover > 0 && PlayerStats.Instance != null)
            ItemPickup.Spawn(item, leftover, PlayerStats.Instance.transform.position + Vector3.down * 0.5f);
    }

    private static void Changed()
    {
        if (OnChanged != null) OnChanged();
        PlayerBonuses.NotifyChanged();
    }

    /// <summary>The bonuses of one item as text, for example "+2 defense, +10 max health".</summary>
    public static string StatsText(ItemData item)
    {
        if (item == null) return "";
        var parts = new System.Collections.Generic.List<string>();
        if (item.defense != 0) parts.Add(Signed(item.defense) + " defense");
        if (item.bonusMaxHealth != 0) parts.Add(Signed(item.bonusMaxHealth) + " max health");
        if (item.bonusMaxMagic != 0f) parts.Add(Signed(item.bonusMaxMagic) + " max magic");
        if (item.bonusMoveSpeedPercent != 0f) parts.Add(Signed(item.bonusMoveSpeedPercent) + "% walking speed");
        if (item.bonusDamagePercent != 0f) parts.Add(Signed(item.bonusDamagePercent) + "% damage");
        return parts.Count == 0 ? "No bonuses." : string.Join(", ", parts.ToArray());
    }

    private static string Signed(float value)
    {
        return (value > 0f ? "+" : "") + value.ToString("0.#");
    }
}
