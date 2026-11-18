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
    [Range(0, 3), Tooltip("Crop quality: 0 = Bronze (ordinary), 1 = Silver, 2 = Gold, 3 = Platinum. Comes from fertilizer in the soil (see SoilCare).")]
    public int quality;

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
///
/// The backpack is shared by every scene: its contents are kept in static fields, so they stay
/// when you walk to another scene. Starting Items are handed out once per play session, by the
/// Player in the first scene that loads (the lists on the Players in other scenes are then skipped).
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

    // These three are 'static': they belong to the game, not to one Player object.
    // Every scene has its own Player (and so its own Inventory component), but they all share
    // this one backpack, which is why your items stay with you when you change scenes.
    private static ItemStack[] slots;
    private static int selected;
    // True once the Starting Items have been handed out, so the next scene does not hand them out again.
    private static bool startingItemsGiven;

    /// <summary>Raised when any slot's contents change.</summary>
    public event Action OnChanged;
    /// <summary>Raised when a different slot is selected.</summary>
    public event Action OnSelectionChanged;

    public int SelectedIndex { get { return selected; } }
    public ItemStack Selected { get { return slots[selected]; } }
    /// <summary>The equipped item, or null if the slot is empty.</summary>
    public ItemData EquippedItem { get { return slots[selected].IsEmpty ? null : slots[selected].item; } }

    // Static data survives between Play sessions when "Enter Play Mode Options" is on,
    // so empty the backpack explicitly every time Play is pressed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        slots = null;
        selected = 0;
        startingItemsGiven = false;
    }

    // ------------------------------------------------------------------
    // Saving and loading (used by SaveSystem)
    // ------------------------------------------------------------------

    /// <summary>All 20 slots, for the save system to write down.</summary>
    public static ItemStack[] AllSlots { get { EnsureSlots(); return slots; } }

    /// <summary>The slot that is selected, whichever scene's Player is asking.</summary>
    public static int SavedSelectedIndex { get { return selected; } }

    /// <summary>
    /// Replaces the whole backpack with what a save file held. 'saved' has one entry per slot.
    /// The Starting Items are NOT handed out afterwards: a loaded game already has its items.
    /// </summary>
    public static void LoadFromSave(ItemStack[] saved, int selectedIndex)
    {
        EnsureSlots();
        for (int i = 0; i < SlotCount; i++)
        {
            ItemStack from = saved != null && i < saved.Length ? saved[i] : null;
            bool empty = from == null || from.IsEmpty;
            slots[i].item = empty ? null : from.item;
            slots[i].count = empty ? 0 : from.count;
            slots[i].pollution = empty ? 0 : from.pollution;
            slots[i].quality = empty ? 0 : from.quality;
        }
        selected = Mathf.Clamp(selectedIndex, 0, SlotCount - 1);
        startingItemsGiven = true;
    }

    /// <summary>Makes the 20 empty slots if they do not exist yet.</summary>
    private static void EnsureSlots()
    {
        if (slots != null) return;
        slots = new ItemStack[SlotCount];
        for (int i = 0; i < SlotCount; i++) slots[i] = new ItemStack();
    }

    private void Awake()
    {
        Instance = this;

        // Make the 20 empty slots only once per play session (the first scene that loads).
        EnsureSlots();

        // Hand out the Starting Items only in the first scene. Later scenes keep the backpack as it is.
        if (!startingItemsGiven)
        {
            startingItemsGiven = true;
            foreach (StartingItem s in startingItems)
                if (s != null && s.item != null) Add(s.item, s.count);
        }
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
    public int CanFit(ItemData item, int count, int pollution = 0, int quality = 0)
    {
        if (item == null) return 0;
        int room = 0;
        foreach (ItemStack s in slots)
        {
            if (s.IsEmpty) room += item.maxStack;
            else if (Matches(s, item, pollution, quality)) room += item.maxStack - s.count;
            if (room >= count) return count;
        }
        return room;
    }

    /// <summary>Adds items, topping up existing stacks first. Returns how many did NOT fit.</summary>
    public int Add(ItemData item, int count, int pollution = 0, int quality = 0)
    {
        if (item == null || count <= 0) return 0;

        // 1) top up matching stacks
        foreach (ItemStack s in slots)
        {
            if (count <= 0) break;
            if (s.IsEmpty || !Matches(s, item, pollution, quality)) continue;
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
            s.quality = quality;
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
        if (s.count <= 0) { s.item = null; s.count = 0; s.pollution = 0; s.quality = 0; }
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

    /// <summary>Total number of items (all stacks) accepted by a test, e.g. i => i.type == ItemData.ItemType.Crop.</summary>
    public int CountMatching(Predicate<ItemData> match)
    {
        int total = 0;
        foreach (ItemStack s in slots) if (!s.IsEmpty && match(s.item)) total += s.count;
        return total;
    }

    /// <summary>
    /// Removes 'count' items accepted by the test. 'pollutionSum' adds up (pollution x amount) of everything removed,
    /// so cooking can average the pollution of its ingredients. Returns false (and removes nothing) if there aren't enough.
    /// </summary>
    public bool TryConsume(Predicate<ItemData> match, int count, out int pollutionSum)
    {
        pollutionSum = 0;
        if (CountMatching(match) < count) return false;

        for (int i = 0; i < SlotCount && count > 0; i++)
        {
            ItemStack s = slots[i];
            if (s.IsEmpty || !match(s.item)) continue;
            int pollution = s.pollution;
            int taken = RemoveFromSlot(i, count);
            pollutionSum += pollution * taken;
            count -= taken;
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

    private static bool Matches(ItemStack s, ItemData item, int pollution, int quality)
    {
        // Different pollution levels never merge, so a dirty harvest can't hide inside a clean stack. The same goes for crop quality.
        return s.item == item && s.pollution == pollution && s.quality == quality;
    }
}
