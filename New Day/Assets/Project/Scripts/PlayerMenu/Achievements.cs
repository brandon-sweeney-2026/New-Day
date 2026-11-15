using UnityEngine;

/// <summary>
/// Watches the game's counters, flags and gold, and hands out achievements when their goal is reached.
/// The list of achievements is in the Player Menu Settings asset (Achievements), so you add new ones in the Inspector.
///
/// An earned achievement is remembered as the flag "ach_done_<id>", so it saves with the rest of GameState.
/// This is a plain static class, so there is nothing to put in the scene.
/// </summary>
public static class Achievements
{
    private static bool checking;       // stops Check() from calling itself when it hands out a reward

    // GameState clears its events when Play is pressed, so subscribe again each time (just before the first scene loads).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartListening()
    {
        checking = false;
        GameState.OnStateChanged += OnStateChanged;
        GameState.OnGoldChanged += OnGoldChanged;
    }

    private static void OnStateChanged(string key) { Check(); }
    private static void OnGoldChanged(int gold) { Check(); }

    private static string DoneFlag(PlayerMenuSettings.Achievement a) { return "ach_done_" + a.id; }

    public static bool IsDone(PlayerMenuSettings.Achievement a)
    {
        return a != null && GameState.HasFlag(DoneFlag(a));
    }

    /// <summary>How far along an achievement is: 'have' out of 'need'.</summary>
    public static void Progress(PlayerMenuSettings.Achievement a, out int have, out int need)
    {
        need = Mathf.Max(1, a.target);
        switch (a.kind)
        {
            case PlayerMenuSettings.GoalKind.Flag:
                need = 1;
                have = GameState.HasFlag(a.key) ? 1 : 0;
                break;
            case PlayerMenuSettings.GoalKind.Gold:
                have = GameState.Gold;
                break;
            default:
                have = GameState.GetCounter(a.key);
                break;
        }
        if (IsDone(a)) have = need;      // gold can be spent again; a finished achievement stays full
        have = Mathf.Clamp(have, 0, need);
    }

    public static int DoneCount
    {
        get
        {
            int n = 0;
            foreach (PlayerMenuSettings.Achievement a in PlayerMenuSettings.Current.achievements) if (IsDone(a)) n++;
            return n;
        }
    }

    /// <summary>Looks at every unfinished achievement and awards the ones whose goal has been reached.</summary>
    public static void Check()
    {
        if (checking) return;
        checking = true;

        foreach (PlayerMenuSettings.Achievement a in PlayerMenuSettings.Current.achievements)
        {
            if (a == null || string.IsNullOrEmpty(a.id) || IsDone(a)) continue;

            int have, need;
            Progress(a, out have, out need);
            if (have < need) continue;

            GameState.SetFlag(DoneFlag(a));
            if (a.rewardGold > 0) GameState.AddGold(a.rewardGold);
            GameEvents.ShowToast("Achievement: " + a.title + (a.rewardGold > 0 ? "  (+" + a.rewardGold + "g)" : ""));
        }

        checking = false;
    }
}
