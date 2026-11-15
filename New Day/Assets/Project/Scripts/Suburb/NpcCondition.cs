using System;
using UnityEngine;

[Flags] public enum DayMask { Mon = 1, Tue = 2, Wed = 4, Thu = 8, Fri = 16, Sat = 32, Sun = 64 }
[Flags] public enum SeasonMask { Spring = 1, Summer = 2, Autumn = 4, Winter = 8 }

[Serializable]
public class CounterCheck
{
    [Tooltip("A GameState counter, e.g. shadows_banished, fairy_quests_done, fish_caught")] public string key = "";
    public int atLeast = 1;
    [Tooltip("-1 = no upper limit")] public int atMost = -1;
    public bool Passes()
    {
        if (string.IsNullOrEmpty(key)) return true;
        int v = GameState.GetCounter(key);
        return v >= atLeast && (atMost < 0 || v <= atMost);
    }
}

/// <summary>
/// "When does this apply?" Used by schedule routes and by chat lines. Everything you leave empty is ignored; everything you fill in must be true.
/// Days / Seasons / Weather: tick the ones that count (tick none = any).
/// </summary>
[Serializable]
public class NpcCondition
{
    [Header("Calendar")]
    public DayMask days = 0;
    public SeasonMask seasons = 0;
    [Tooltip("Day of the season, 1-28. 0 = ignore.")] public int dayFrom = 0;
    public int dayTo = 0;
    [Tooltip("Not before this year (0 = ignore).")] public int minYear = 0;
    [Tooltip("Not before this day of the whole game (0 = ignore).")] public int minTotalDays = 0;

    [Header("Weather")]
    public WeatherMask weather = 0;

    [Header("Game unlocks")]
    [Tooltip("ALL of these flags must be set (e.g. forest_grove_reached, forest_story_unlocked, fairy_met).")]
    public string[] requireFlags;
    [Tooltip("ANY ONE of these flags must be set (leave empty to ignore).")]
    public string[] anyFlags;
    [Tooltip("NONE of these flags may be set.")]
    public string[] forbidFlags;
    public CounterCheck[] counters;

    /// <summary>True when everything filled in is satisfied right now.</summary>
    public bool Matches()
    {
        var tm = TimeManager.Instance;
        if (tm != null)
        {
            if (days != 0 && (days & (DayMask)(1 << (tm.WeekDay % 7))) == 0) return false;
            if (seasons != 0 && (seasons & (SeasonMask)(1 << (int)tm.CurrentSeason)) == 0) return false;
            if (dayFrom > 0 && tm.Day < dayFrom) return false;
            if (dayTo > 0 && tm.Day > dayTo) return false;
            if (minYear > 0 && tm.Year < minYear) return false;
            if (minTotalDays > 0 && tm.TotalDays < minTotalDays) return false;
        }
        if (weather != 0 && (weather & WeatherManager.CurrentMask) == 0) return false;

        if (requireFlags != null) foreach (string f in requireFlags) if (!string.IsNullOrEmpty(f) && !GameState.HasFlag(f)) return false;
        if (forbidFlags != null) foreach (string f in forbidFlags) if (!string.IsNullOrEmpty(f) && GameState.HasFlag(f)) return false;
        if (anyFlags != null)
        {
            bool any = false, has = false;
            foreach (string f in anyFlags) { if (string.IsNullOrEmpty(f)) continue; has = true; if (GameState.HasFlag(f)) any = true; }
            if (has && !any) return false;
        }
        if (counters != null) foreach (CounterCheck c in counters) if (c != null && !c.Passes()) return false;
        return true;
    }
}
