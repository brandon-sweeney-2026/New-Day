using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One slot's contents. 'pollution' (0-5) is remembered for harvested crops so the sale price can use it.</summary>
[Serializable]
public class ItemStack
{
    public ItemData item;
    public int count;
    [Range(0, 5)] public int pollution;

    public bool IsEmpty { get { return item == null || count <= 0; } }
}

/// <summary>
/// The player's backpack: 20 slots (two rows of ten). The bottom row is the hotbar.
/// The SELECTED slot is the equipped item - that's what the action button uses.
///
/// Controls: number keys 1-9,0 select the bottom row. Mouse wheel / Q / R cycle through all 20 slots.
/// (Clicking a slot with the mouse also selects it, see GameHUD.)
///
/// Put this on the Player. Fill 'startingItems' in the Inspector to hand out tools at the start.
/// </summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public const int Columns = 10;
    public const int Rows = 2;
    public const int SlotCount = Columns * Rows;

    [Serializable]
    public class StartingItem
    {
        public ItemData item;
        [Min(1)] public int count = 1;
    }

    [Header("Starting items (placed in order from slot 1)")]
    [SerializeField] private List<StartingItem> startingItems = new List<StartingItem>();

    [Header("Controls")]
    [SerializeField] private bool mouseWheelCycles = true;
    [Tooltip("Keys that cycle the equipped slot. Set to None to disable.")]
    [SerializeField] private Key previousKey = Key.Q;
    [SerializeField] private Key nextKey = Key.R;

    private readonly ItemStack[] slots = new ItemStack[SlotCount];
    private int selected;

    /// <summary>Raised when any slot's contents change.</summary>
    public event Action OnChanged;
    /// <summary>Raised when a different slot is selected.</summary>
    public event Action OnSelectionChanged;

    public int SelectedIndex { get { return selected; } }
    public ItemStack Selected { get { return slots[selected]; } }
    /// <summary>The equipped item, or null if the slot is empty.</summary>
    public ItemData EquippedItem { get { return slots[selected].IsEmpty ? null : slots[selected].item; } }

    private void Awake()
    {
        Instance = this;
        for (int i = 0; i < SlotCount; i++) slots[i] = new ItemStack();

        foreach (StartingItem s in startingItems)
            if (s != null && s.item != null) Add(s.item, s.count);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameState.InputLocked) return;
        Keyboard kb = Keyboard.current;

        if (kb != null)
        {
            // 1..9 then 0 map to slots 0..9.
            Key[] digits =
            {
                Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
                Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
            };
            for (int i = 0; i < digits.Length; i++)
                if (kb[digits[i]].wasPressedThisFrame) Select(i);

            if (previousKey != Key.None && kb[previousKey].wasPressedThisFrame) Select(selected - 1);
            if (nextKey != Key.None && kb[nextKey].wasPressedThisFrame) Select(selected + 1);
        }

        if (mouseWheelCycles && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll > 0.1f) Select(selected - 1);
            else if (scroll < -0.1f) Select(selected + 1);
        }
    }

    // ------------------------------------------------------------------
    // Selecting
    // ------------------------------------------------------------------

    /// <summary>Equips a slot. Wraps around at both ends.</summary>
    public void Select(int index)
    {
        index = ((index % SlotCount) + SlotCount) % SlotCount;
        if (index == selected) return;
        selected = index;
        if (OnSelectionChanged != null) OnSelectionChanged();
    }

    public ItemStack GetSlot(int index)
    {
        return (index >= 0 && index < SlotCount) ? slots[index] : null;
    }

    // ------------------------------------------------------------------
    // Adding and removing
    // ------------------------------------------------------------------

    /// <summary>How many of this item would fit right now.</summary>
    public int CanFit(ItemData item, int count, int pollution = 0)
    {
        if (item == null) return 0;
        int room = 0;
        foreach (ItemStack s in slots)
        {
            if (s.IsEmpty) room += item.maxStack;
            else if (Matches(s, item, pollution)) room += item.maxStack - s.count;
            if (room >= count) return count;
        }
        return room;
    }

    /// <summary>Adds items, topping up existing stacks first. Returns how many did NOT fit.</summary>
    public int Add(ItemData item, int count, int pollution = 0)
    {
        if (item == null || count <= 0) return 0;

        // 1) top up matching stacks
        foreach (ItemStack s in slots)
        {
            if (count <= 0) break;
            if (s.IsEmpty || !Matches(s, item, pollution)) continue;
            int move = Mathf.Min(count, item.maxStack - s.count);
            s.count += move;
            count -= move;
        }

        // 2) fill empty slots
        foreach (ItemStack s in slots)
        {
            if (count <= 0) break;
            if (!s.IsEmpty) continue;
            int move = Mathf.Min(count, item.maxStack);
            s.item = item;
            s.count = move;
            s.pollution = pollution;
            count -= move;
        }

        if (OnChanged != null) OnChanged();
        return count;
    }

    /// <summary>Removes up to 'count' from one slot. Returns how many were removed.</summary>
    public int RemoveFromSlot(int index, int count)
    {
        ItemStack s = GetSlot(index);
        if (s == null || s.IsEmpty) return 0;

        int removed = Mathf.Min(count, s.count);
        s.count -= removed;
        if (s.count <= 0) { s.item = null; s.count = 0; s.pollution = 0; }
        if (OnChanged != null) OnChanged();
        return removed;
    }

    /// <summary>Total of one item across every slot (all pollution levels).</summary>
    public int CountOf(ItemData item)
    {
        int total = 0;
        foreach (ItemStack s in slots) if (!s.IsEmpty && s.item == item) total += s.count;
        return total;
    }

    public bool Has(ItemData item, int count = 1) { return CountOf(item) >= count; }

    /// <summary>Removes items from any slots (ignores pollution). Returns true if the full amount was removed.</summary>
    public bool Remove(ItemData item, int count)
    {
        if (CountOf(item) < count) return false;
        for (int i = 0; i < SlotCount && count > 0; i++)
        {
            if (slots[i].IsEmpty || slots[i].item != item) continue;
            count -= RemoveFromSlot(i, count);
        }
        return true;
    }

    /// <summary>Swaps two slots (used by the right-click rearrange in the HUD).</summary>
    public void Swap(int a, int b)
    {
        if (a == b || a < 0 || b < 0 || a >= SlotCount || b >= SlotCount) return;
        ItemStack tmp = slots[a];
        slots[a] = slots[b];
        slots[b] = tmp;
        if (OnChanged != null) OnChanged();
    }

    private static bool Matches(ItemStack s, ItemData item, int pollution)
    {
        // Different pollution levels never merge, so a dirty harvest can't hide inside a clean stack.
        return s.item == item && s.pollution == pollution;
    }
}
