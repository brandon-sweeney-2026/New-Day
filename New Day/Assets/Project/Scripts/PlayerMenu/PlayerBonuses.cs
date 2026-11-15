using System;
using UnityEngine;

/// <summary>
/// Adds up every bonus the player has, from perks (PlayerSkills) and from worn items (PlayerEquipment).
/// This is the ONE place the rest of the game asks "how much extra does the player have?":
///
///   PlayerStats       -> MaxHealth, MaxMagic, ReduceDamage()
///   PlayerController  -> MoveSpeedMultiplier
///   EnemyBase         -> ScaleDamage()
///   PlayerActions     -> ToolPower()
///   CropManager       -> ExtraCropChance
///
/// To add a new kind of bonus: add a name to PlayerMenuSettings.BonusKind, add a property here, and read it where it matters.
/// </summary>
public static class PlayerBonuses
{
    /// <summary>Raised when a perk is bought or equipment changes.</summary>
    public static event Action OnChanged;

    // One total per BonusKind. Worked out at most once per frame.
    private static readonly float[] totals = new float[Enum.GetValues(typeof(PlayerMenuSettings.BonusKind)).Length];
    private static int totalsFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        OnChanged = null;
        totalsFrame = -1;
    }

    /// <summary>Call after anything that changes a bonus, so health and speed update straight away.</summary>
    public static void NotifyChanged()
    {
        totalsFrame = -1;
        if (OnChanged != null) OnChanged();
    }

    // ------------------------------------------------------------------
    // What the game reads
    // ------------------------------------------------------------------

    public static int MaxHealth { get { return Mathf.RoundToInt(Total(PlayerMenuSettings.BonusKind.MaxHealth)); } }
    public static float MaxMagic { get { return Total(PlayerMenuSettings.BonusKind.MaxMagic); } }
    public static int Defense { get { return Mathf.Max(0, Mathf.RoundToInt(Total(PlayerMenuSettings.BonusKind.Defense))); } }

    /// <summary>1 = normal walking speed, 1.1 = 10% faster.</summary>
    public static float MoveSpeedMultiplier { get { return Mathf.Max(0.1f, 1f + Total(PlayerMenuSettings.BonusKind.MoveSpeedPercent) / 100f); } }

    /// <summary>1 = normal damage, 1.12 = 12% more.</summary>
    public static float DamageMultiplier { get { return Mathf.Max(0f, 1f + Total(PlayerMenuSettings.BonusKind.DamagePercent) / 100f); } }

    /// <summary>Chance (0-1) of one extra crop at harvest.</summary>
    public static float ExtraCropChance { get { return Mathf.Clamp01(Total(PlayerMenuSettings.BonusKind.ExtraCropChancePercent) / 100f); } }

    /// <summary>Extra hits per swing for the axe or the pickaxe (0 for every other tool).</summary>
    public static int ToolPower(ItemData.ToolAction tool)
    {
        if (tool == ItemData.ToolAction.Axe) return Mathf.RoundToInt(Total(PlayerMenuSettings.BonusKind.AxePower));
        if (tool == ItemData.ToolAction.Pickaxe) return Mathf.RoundToInt(Total(PlayerMenuSettings.BonusKind.PickaxePower));
        return 0;
    }

    /// <summary>Damage the PLAYER takes after defense. A hit always does at least 1.</summary>
    public static int ReduceDamage(int amount)
    {
        if (amount <= 0) return amount;
        return Mathf.Max(1, amount - Defense);
    }

    /// <summary>Damage the player DEALS after the damage bonus.</summary>
    public static int ScaleDamage(int amount)
    {
        if (amount <= 0) return amount;
        return Mathf.Max(1, Mathf.RoundToInt(amount * DamageMultiplier));
    }

    // ------------------------------------------------------------------
    // Adding it all up
    // ------------------------------------------------------------------

    private static float Total(PlayerMenuSettings.BonusKind kind)
    {
        if (totalsFrame != Time.frameCount) Recalculate();
        return totals[(int)kind];
    }

    private static void Recalculate()
    {
        totalsFrame = Time.frameCount;
        for (int i = 0; i < totals.Length; i++) totals[i] = 0f;

        // Perks: points in the perk x amount per level.
        foreach (PlayerMenuSettings.Perk p in PlayerMenuSettings.Current.perks)
        {
            if (p == null) continue;
            int level = Mathf.Min(PlayerSkills.PerkLevel(p.id), p.maxLevel);
            if (level > 0) totals[(int)p.bonus] += level * p.amountPerLevel;
        }

        // Worn items.
        for (int slot = 0; slot < PlayerEquipment.SlotCount; slot++)
        {
            ItemData item = PlayerEquipment.Get(slot);
            if (item == null) continue;
            totals[(int)PlayerMenuSettings.BonusKind.Defense] += item.defense;
            totals[(int)PlayerMenuSettings.BonusKind.MaxHealth] += item.bonusMaxHealth;
            totals[(int)PlayerMenuSettings.BonusKind.MaxMagic] += item.bonusMaxMagic;
            totals[(int)PlayerMenuSettings.BonusKind.MoveSpeedPercent] += item.bonusMoveSpeedPercent;
            totals[(int)PlayerMenuSettings.BonusKind.DamagePercent] += item.bonusDamagePercent;
        }
    }
}
