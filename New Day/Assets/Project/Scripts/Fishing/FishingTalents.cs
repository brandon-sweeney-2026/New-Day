using UnityEngine;

/// <summary>The combined effect of the rod and the talents for one fishing trip.</summary>
public class FishingBonuses
{
    public float noteSpeedMultiplier = 1f;   // < 1 = slower notes
    public int freeMisses;
    public float hitWindow = 0.13f;          // seconds either side of the beat that count as a hit
    public float perfectWindow = 0.045f;
    public float staminaGainMultiplier = 1f;
    public float luck;
    public float baitExtra;
    public float biteSpeed;                  // 0.2 = bites 20% sooner

    public static FishingBonuses Compute(FishingRodData rod)
    {
        var b = new FishingBonuses();
        if (rod != null)
        {
            b.noteSpeedMultiplier -= rod.slowTime;
            b.freeMisses += rod.extraMisses;
            b.hitWindow += rod.hitWindowBonus;
            b.staminaGainMultiplier += rod.staminaGain;
            b.luck += rod.luck;
            b.biteSpeed += rod.biteSpeed;
        }
        b.noteSpeedMultiplier -= FishingTalents.Level("slow") * 0.04f;
        b.freeMisses += FishingTalents.Level("misses");
        b.hitWindow += FishingTalents.Level("window") * 0.012f;
        b.perfectWindow += FishingTalents.Level("window") * 0.004f;
        b.staminaGainMultiplier += FishingTalents.Level("stamina") * 0.05f;
        b.luck += FishingTalents.Level("luck") * 0.06f;
        b.baitExtra += FishingTalents.Level("bait") * 1f;
        b.noteSpeedMultiplier = Mathf.Max(0.35f, b.noteSpeedMultiplier);
        return b;
    }
}

/// <summary>
/// Fishing experience, levels and talent points. This holds the numbers; a talent-tree screen can be added on top later.
/// Everything is stored in GameState counters ("fishing_xp", "talent_slow", ...), so it saves like the rest of the game.
///
/// Level L needs 25 x L x (L+1) / 2 total XP (25, 75, 150, 250...). Every level gives one talent point.
/// </summary>
public static class FishingTalents
{
    public class Talent
    {
        public string id, name, description; public int maxLevel;
        public Talent(string id, string name, string description, int max) { this.id = id; this.name = name; this.description = description; maxLevel = max; }
    }

    public static readonly Talent[] All =
    {
        new Talent("slow", "Calm Waters", "Notes move 4% slower per level.", 5),
        new Talent("misses", "Second Chance", "1 free miss per fish, per level.", 3),
        new Talent("window", "Keen Eye", "A little more time to hit each note.", 5),
        new Talent("stamina", "Strong Line", "Each hit fills the meter 5% more per level.", 5),
        new Talent("luck", "Lucky Angler", "Rare fish are 6% more likely per level.", 5),
        new Talent("bait", "Bait Master", "Bait attracts its fish even more.", 3),
    };

    public static int Xp { get { return GameState.GetCounter("fishing_xp"); } }

    public static int LevelForXp(int xp)
    {
        // largest L with 25*L*(L+1)/2 <= xp
        return Mathf.Max(0, Mathf.FloorToInt((Mathf.Sqrt(1f + 8f * xp / 25f) - 1f) * 0.5f));
    }

    public static int FishingLevel { get { return LevelForXp(Xp); } }
    public static int Level(string id) { return GameState.GetCounter("talent_" + id); }

    public static int PointsSpent
    {
        get { int n = 0; foreach (Talent t in All) n += Level(t.id); return n; }
    }
    public static int PointsAvailable { get { return Mathf.Max(0, FishingLevel - PointsSpent); } }

    public static void AddXp(int amount)
    {
        int before = FishingLevel;
        GameState.AddCounter("fishing_xp", amount);
        if (FishingLevel > before) GameEvents.ShowToast("Fishing level " + FishingLevel + "! You have a new talent point.");
    }

    /// <summary>Spends a point on a talent. Returns false if you have no points or it is at max.</summary>
    public static bool TrySpend(string id)
    {
        foreach (Talent t in All)
        {
            if (t.id != id) continue;
            if (PointsAvailable <= 0 || Level(id) >= t.maxLevel) return false;
            GameState.AddCounter("talent_" + id);
            return true;
        }
        return false;
    }

    /// <summary>Gives back all points (for testing, or a "respec" option).</summary>
    public static void Reset() { foreach (Talent t in All) GameState.SetCounter("talent_" + t.id, 0); }
}
