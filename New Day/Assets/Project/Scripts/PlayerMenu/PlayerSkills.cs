using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's skills (Farming, Mining, Foraging, Combat): experience, levels and perk points.
/// (Fishing has its own class, FishingTalents, which works the same way; the menu shows both.)
///
/// HOW XP IS EARNED: the game already counts things in GameState counters (crops_harvested, mined_rocks...).
/// This class listens for those counters going up and turns them into XP using the "Xp Rules" list in
/// the Player Menu Settings asset. So no other script has to call anything to give XP.
///
/// Everything is stored in GameState counters ("xp_farming", "perk_green_thumb"...), so it will save with the rest.
/// Every level gives one perk point to spend in that skill's experience tree.
/// </summary>
public static class PlayerSkills
{
    public enum Skill { Farming, Mining, Foraging, Combat }

    public static readonly Skill[] All = { Skill.Farming, Skill.Mining, Skill.Foraging, Skill.Combat };

    // The last value we saw for each counter, so we can tell how much it went up by.
    private static readonly Dictionary<string, int> lastSeen = new Dictionary<string, int>();

    // GameState clears its events when Play is pressed, so subscribe again each time (just before the first scene loads).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartListening()
    {
        lastSeen.Clear();
        GameState.OnStateChanged += OnStateChanged;
    }

    // ------------------------------------------------------------------
    // XP and levels
    // ------------------------------------------------------------------

    private static string XpKey(Skill skill) { return "xp_" + skill.ToString().ToLowerInvariant(); }

    public static int Xp(Skill skill) { return GameState.GetCounter(XpKey(skill)); }

    /// <summary>Total XP needed to reach a level: base x L x (L+1) / 2.</summary>
    public static int XpForLevel(int level)
    {
        return PlayerMenuSettings.Current.xpForFirstLevel * level * (level + 1) / 2;
    }

    public static int LevelForXp(int xp)
    {
        // The largest L with base * L * (L+1) / 2 <= xp.
        float b = Mathf.Max(1, PlayerMenuSettings.Current.xpForFirstLevel);
        int level = Mathf.Max(0, Mathf.FloorToInt((Mathf.Sqrt(1f + 8f * xp / b) - 1f) * 0.5f));
        // Guard against rounding right at a level boundary.
        while (XpForLevel(level + 1) <= xp) level++;
        while (level > 0 && XpForLevel(level) > xp) level--;
        return level;
    }

    public static int Level(Skill skill) { return LevelForXp(Xp(skill)); }

    /// <summary>How far through the current level you are, 0-1 (for the bar in the menu).</summary>
    public static float LevelProgress(Skill skill)
    {
        int xp = Xp(skill);
        int level = LevelForXp(xp);
        int from = XpForLevel(level), to = XpForLevel(level + 1);
        return to > from ? Mathf.Clamp01((xp - from) / (float)(to - from)) : 0f;
    }

    /// <summary>All skill levels added together, plus the fishing level.</summary>
    public static int TotalLevel
    {
        get
        {
            int total = FishingTalents.FishingLevel;
            foreach (Skill s in All) total += Level(s);
            return total;
        }
    }

    public static void AddXp(Skill skill, int amount)
    {
        if (amount <= 0) return;
        int before = Level(skill);
        GameState.AddCounter(XpKey(skill), amount);
        int after = Level(skill);
        if (after > before) GameEvents.ShowToast(skill + " level " + after + "! You have a new perk point.");
    }

    // ------------------------------------------------------------------
    // Perks (the experience tree)
    // ------------------------------------------------------------------

    /// <summary>How many points have been put into a perk.</summary>
    public static int PerkLevel(string perkId) { return GameState.GetCounter("perk_" + perkId); }

    public static List<PlayerMenuSettings.Perk> PerksOf(Skill skill)
    {
        var list = new List<PlayerMenuSettings.Perk>();
        foreach (PlayerMenuSettings.Perk p in PlayerMenuSettings.Current.perks)
            if (p != null && p.skill == skill) list.Add(p);
        return list;
    }

    public static int PointsSpent(Skill skill)
    {
        int n = 0;
        foreach (PlayerMenuSettings.Perk p in PlayerMenuSettings.Current.perks)
            if (p != null && p.skill == skill) n += PerkLevel(p.id);
        return n;
    }

    public static int PointsAvailable(Skill skill) { return Mathf.Max(0, Level(skill) - PointsSpent(skill)); }

    /// <summary>Spends one point on a perk. Returns false if there are no points left or it is already at its top level.</summary>
    public static bool TrySpend(PlayerMenuSettings.Perk perk)
    {
        if (perk == null) return false;
        if (PointsAvailable(perk.skill) <= 0 || PerkLevel(perk.id) >= perk.maxLevel) return false;
        GameState.AddCounter("perk_" + perk.id);
        PlayerBonuses.NotifyChanged();      // max health, speed and so on may have changed
        return true;
    }

    // ------------------------------------------------------------------
    // Turning counters into XP
    // ------------------------------------------------------------------

    private static void OnStateChanged(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        // Our own counters also raise this event; ignore them or XP would feed itself.
        if (key.StartsWith("xp_") || key.StartsWith("perk_")) return;

        PlayerMenuSettings.XpRule rule = null;
        foreach (PlayerMenuSettings.XpRule r in PlayerMenuSettings.Current.xpRules)
        {
            if (r == null || string.IsNullOrEmpty(r.counter)) continue;
            if (r.startsWith ? key.StartsWith(r.counter) : key == r.counter) { rule = r; break; }
        }
        if (rule == null) return;

        int value = GameState.GetCounter(key);
        int last;
        lastSeen.TryGetValue(key, out last);
        lastSeen[key] = value;

        int gained = value - last;
        if (gained > 0) AddXp(rule.skill, gained * rule.xpEach);
    }
}
