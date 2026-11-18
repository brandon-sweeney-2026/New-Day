using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The smelter in the Sterling tower: hand over ore, some fuel and a fee, and get a metal bar back.
/// Fill 'Melts' in the Inspector (the city builder does it for copper, iron, gold and platinum).
/// Needs a Collider2D (the machine's own solid one is fine).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Smelter : MonoBehaviour, IInteractable
{
    /// <summary>One thing the smelter can make.</summary>
    [Serializable]
    public class Melt
    {
        public ItemData ore;
        [Min(1)] public int oreCount = 3;
        [Tooltip("Burned as fuel (Coal). Leave empty for no fuel.")]
        public ItemData fuel;
        [Min(0)] public int fuelCount = 1;
        public ItemData bar;
        [Min(1)] public int barCount = 1;
        [Min(0), Tooltip("Gold charged for each smelt, before the discount.")]
        public int fee = 20;
    }

    [SerializeField] private string title = "Sterling Smelting";
    [SerializeField] private string prompt = "Smelt ore";
    public List<Melt> melts = new List<Melt>();
    [Range(0.1f, 2f), Tooltip("The fee is multiplied by this. 0.95 = 5% off.")]
    [SerializeField] private float priceMultiplier = 0.95f;
    [Min(0), Tooltip("Game minutes that pass for each smelt.")]
    [SerializeField] private int minutesPerSmelt = 20;

    private PlayerActions player;

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions p)
    {
        player = p;
        MenuUI.Open(title, BuildRows, "Up/Down: choose     Space: smelt     Esc: close");
    }

    private int FeeOf(Melt m) { return Mathf.RoundToInt(m.fee * priceMultiplier); }

    private bool HasAll(Melt m, Inventory inv)
    {
        if (inv == null || m.ore == null || m.bar == null) return false;
        if (inv.CountOf(m.ore) < m.oreCount) return false;
        if (m.fuel != null && m.fuelCount > 0 && inv.CountOf(m.fuel) < m.fuelCount) return false;
        return GameState.Gold >= FeeOf(m);
    }

    private List<MenuRow> BuildRows()
    {
        Inventory inv = player != null ? player.Inventory : null;
        var rows = new List<MenuRow>();
        foreach (Melt melt in melts)
        {
            if (melt == null || melt.ore == null || melt.bar == null) continue;
            Melt m = melt;
            bool can = HasAll(m, inv);
            string needs = m.ore.itemName + " " + (inv != null ? inv.CountOf(m.ore) : 0) + "/" + m.oreCount;
            if (m.fuel != null && m.fuelCount > 0) needs += ", " + m.fuel.itemName + " " + (inv != null ? inv.CountOf(m.fuel) : 0) + "/" + m.fuelCount;
            rows.Add(new MenuRow
            {
                label = m.bar.itemName,
                right = FeeOf(m) + "g",
                enabled = can,
                color = can ? (Color?)new Color(0.7f, 1f, 0.7f) : null,
                detail = "Needs: " + needs + "\nMakes: " + m.barCount + " " + m.bar.itemName + "   (" + minutesPerSmelt + " min)\nGold: " + GameState.Gold + "g",
                onSelect = () => Run(m, inv)
            });
        }
        if (rows.Count == 0) rows.Add(new MenuRow { label = "(nothing to smelt)", enabled = false });
        return rows;
    }

    private void Run(Melt m, Inventory inv)
    {
        if (!HasAll(m, inv)) { GameEvents.ShowToast("You need the ore, the fuel and the fee."); return; }
        if (inv.CanFit(m.bar, m.barCount) < m.barCount) { GameEvents.ShowToast("Your backpack is full."); return; }

        GameState.TrySpendGold(FeeOf(m));
        inv.Remove(m.ore, m.oreCount);
        if (m.fuel != null && m.fuelCount > 0) inv.Remove(m.fuel, m.fuelCount);
        inv.Add(m.bar, m.barCount);
        GameState.AddCounter("bars_smelted", m.barCount);
        SterlingInfluence.NoteDealing();   // the smelter is in the Sterling tower
        GameEvents.ShowToast("Smelted " + m.barCount + " " + m.bar.itemName);
        if (minutesPerSmelt > 0 && TimeManager.Instance != null) TimeManager.Instance.AddMinutes(minutesPerSmelt);
    }
}
