using UnityEngine;

/// <summary>
/// Keeps count of how much business the player does with Victor Sterling and his tower.
/// The more you deal with Sterling, the more pollution litter (cans, debris, black and purple lumps) washes up on your farm.
///
/// What counts as one "dealing" (all stored in the GameState counter "sterling_dealings"):
///   - buying anything in the Sterling tower (the seed shop, Sterling Home, a house upgrade there),
///   - smelting ore at Sterling Smelting,
///   - talking to Victor Sterling (at most once a day).
/// The shops know they are Sterling's because they sell below full price (Price Multiplier under 1); no other shop does.
/// Nothing to set up: the shops and Victor call NoteDealing() themselves.
/// </summary>
public static class SterlingInfluence
{
    /// <summary>GameState counter: every purchase, smelt or (daily) chat with Sterling.</summary>
    public const string DealingsKey = "sterling_dealings";
    /// <summary>GameState counter: the day number of the last chat with Victor that counted.</summary>
    public const string LastTalkDayKey = "sterling_last_talk_day";

    /// <summary>How many times the player has dealt with Sterling.</summary>
    public static int Dealings { get { return GameState.GetCounter(DealingsKey); } }

    /// <summary>Counts one dealing with Sterling.</summary>
    public static void NoteDealing()
    {
        GameState.AddCounter(DealingsKey);
    }

    /// <summary>A shop calls this after a sale. Only shops with a discount (the Sterling tower) count.</summary>
    public static void NoteSale(float priceMultiplier)
    {
        if (priceMultiplier < 0.999f) NoteDealing();
    }

    /// <summary>Victor calls this when the player talks to him. Counts once per game day, so chatting all day does not flood the farm.</summary>
    public static void NoteTalk()
    {
        int today = TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 1;
        if (GameState.GetCounter(LastTalkDayKey) == today) return;
        GameState.SetCounter(LastTalkDayKey, today);
        NoteDealing();
    }
}
