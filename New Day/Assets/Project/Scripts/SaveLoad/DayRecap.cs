using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a tally of the day for the end-of-day recap screen: the experience earned in each skill and
/// everything the mailbox shipped.
///
///  - BeginDay() writes down every skill's XP as the day starts. At the end of the day the difference is "XP earned today".
///  - ShippingBin calls AddShipped() for each stack it sells.
///  - TimeManager calls FinishDay() as the new day begins: it auto saves, then shows DayRecapScreen.
///
/// This is a plain static class, so there is nothing to put in the scene.
/// </summary>
public static class DayRecap
{
    /// <summary>One line of the "Shipped" list.</summary>
    public class ShippedLine
    {
        public ItemData item;
        public int count;
        public int quality;
        public int gold;
    }

    /// <summary>One line of the "Experience" list.</summary>
    public class XpLine
    {
        public string skill;
        public int gained;
        public int levelBefore, levelAfter;
    }

    /// <summary>Everything the recap screen shows.</summary>
    public class Summary
    {
        public string date = "";
        public bool passedOut;
        public readonly List<XpLine> xp = new List<XpLine>();
        public readonly List<ShippedLine> shipped = new List<ShippedLine>();
        public int totalGold;
        public bool hasSlot;        // false = the game was not started from the title screen, so there is nowhere to save
        public bool saved;
        public int slot;
    }

    private const string FishingCounter = "fishing_xp";

    private static readonly Dictionary<string, int> xpAtStart = new Dictionary<string, int>();
    private static readonly List<ShippedLine> shipped = new List<ShippedLine>();

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        xpAtStart.Clear();          // no entry = 0 XP, which is right for a fresh game
        shipped.Clear();
    }

    /// <summary>Starts counting a new day from the XP the player has right now.</summary>
    public static void BeginDay()
    {
        xpAtStart.Clear();
        foreach (PlayerSkills.Skill s in PlayerSkills.All)
        {
            string key = PlayerSkills.CounterFor(s);
            xpAtStart[key] = GameState.GetCounter(key);
        }
        xpAtStart[FishingCounter] = GameState.GetCounter(FishingCounter);
        shipped.Clear();
    }

    /// <summary>Called by ShippingBin for each stack it sells. Stacks of the same item and quality are added together.</summary>
    public static void AddShipped(ItemData item, int count, int quality, int gold)
    {
        if (item == null || count <= 0) return;
        foreach (ShippedLine line in shipped)
        {
            if (line.item != item || line.quality != quality) continue;
            line.count += count;
            line.gold += gold;
            return;
        }
        shipped.Add(new ShippedLine { item = item, count = count, quality = quality, gold = gold });
    }

    /// <summary>
    /// The day is over (slept, passed out or knocked out). Saves the game, then shows the recap.
    /// 'endedDate' is the day that just ended, for example "Mon, Spring 3".
    /// </summary>
    public static void FinishDay(string endedDate, bool passedOut)
    {
        var summary = new Summary { date = endedDate, passedOut = passedOut };

        foreach (PlayerSkills.Skill s in PlayerSkills.All)
        {
            int before = StartXp(PlayerSkills.CounterFor(s));
            int now = GameState.GetCounter(PlayerSkills.CounterFor(s));
            if (now > before)
                summary.xp.Add(new XpLine { skill = s.ToString(), gained = now - before,
                                            levelBefore = PlayerSkills.LevelForXp(before), levelAfter = PlayerSkills.LevelForXp(now) });
        }
        int fishBefore = StartXp(FishingCounter), fishNow = GameState.GetCounter(FishingCounter);
        if (fishNow > fishBefore)
            summary.xp.Add(new XpLine { skill = "Fishing", gained = fishNow - fishBefore,
                                        levelBefore = FishingTalents.LevelForXp(fishBefore), levelAfter = FishingTalents.LevelForXp(fishNow) });

        // Most valuable first.
        summary.shipped.AddRange(shipped);
        summary.shipped.Sort((a, b) => b.gold.CompareTo(a.gold));
        foreach (ShippedLine line in summary.shipped) summary.totalGold += line.gold;

        // Start counting the new day BEFORE saving, so a game loaded from this save counts from the same numbers.
        BeginDay();

        summary.slot = SaveSystem.CurrentSlot;
        summary.hasSlot = summary.slot >= 0;
        summary.saved = SaveSystem.AutoSave();

        DayRecapScreen.Show(summary);
    }

    private static int StartXp(string key)
    {
        int v;
        return xpAtStart.TryGetValue(key, out v) ? v : 0;
    }
}
