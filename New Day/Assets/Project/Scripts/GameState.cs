using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global game facts that don't belong to one object: gold, story flags, counters and input locks.
///
///  - Gold: GameState.Gold, AddGold(), TrySpendGold().
///  - Flags/counters: remember anything by name. NPCs and events read these, e.g.
///        GameState.SetFlag("met_mira");   GameState.GetCounter("turnips_sold") >= 10
///  - Input lock: dialogue boxes and menus call LockInput() / UnlockInput() so the player stands still.
///
/// This is a plain static class, so there is nothing to put in the scene.
/// Everything resets when you press Play (and SaveData() / LoadData() are ready for a save system).
/// </summary>
public static class GameState
{
    private static int gold;
    private static readonly HashSet<string> flags = new HashSet<string>();
    private static readonly Dictionary<string, int> counters = new Dictionary<string, int>();
    private static int inputLocks;

    public static int StartingGold = 100;

    public static event Action<int> OnGoldChanged;
    /// <summary>Raised whenever a flag or counter changes: (key). Event systems listen to this to re-check conditions.</summary>
    public static event Action<string> OnStateChanged;

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        gold = StartingGold;
        flags.Clear();
        counters.Clear();
        inputLocks = 0;
        OnGoldChanged = null;
        OnStateChanged = null;
    }

    // ------------------------------------------------------------------
    // Gold
    // ------------------------------------------------------------------

    public static int Gold { get { return gold; } }

    public static void AddGold(int amount)
    {
        gold = Mathf.Max(0, gold + amount);
        if (OnGoldChanged != null) OnGoldChanged(gold);
    }

    public static bool TrySpendGold(int amount)
    {
        if (gold < amount) return false;
        AddGold(-amount);
        return true;
    }

    // ------------------------------------------------------------------
    // Flags and counters
    // ------------------------------------------------------------------

    public static bool HasFlag(string key) { return flags.Contains(key); }

    public static void SetFlag(string key, bool value = true)
    {
        bool changed = value ? flags.Add(key) : flags.Remove(key);
        if (changed && OnStateChanged != null) OnStateChanged(key);
    }

    public static int GetCounter(string key)
    {
        int v;
        return counters.TryGetValue(key, out v) ? v : 0;
    }

    public static void SetCounter(string key, int value)
    {
        counters[key] = value;
        if (OnStateChanged != null) OnStateChanged(key);
    }

    public static void AddCounter(string key, int delta = 1)
    {
        SetCounter(key, GetCounter(key) + delta);
    }

    // ------------------------------------------------------------------
    // Input lock (dialogue, menus)
    // ------------------------------------------------------------------

    public static bool InputLocked { get { return inputLocks > 0; } }
    public static void LockInput() { inputLocks++; }
    public static void UnlockInput() { inputLocks = Mathf.Max(0, inputLocks - 1); }

    // ------------------------------------------------------------------
    // Save / load hooks (simple text form; swap for JSON or your own save system later)
    // ------------------------------------------------------------------

    public static string SaveData()
    {
        var parts = new List<string> { "gold=" + gold };
        foreach (string f in flags) parts.Add("flag:" + f);
        foreach (var kv in counters) parts.Add("count:" + kv.Key + "=" + kv.Value);
        return string.Join("\n", parts);
    }

    public static void LoadData(string data)
    {
        flags.Clear();
        counters.Clear();
        if (string.IsNullOrEmpty(data)) return;

        foreach (string line in data.Split('\n'))
        {
            if (line.StartsWith("gold=")) int.TryParse(line.Substring(5), out gold);
            else if (line.StartsWith("flag:")) flags.Add(line.Substring(5));
            else if (line.StartsWith("count:"))
            {
                string body = line.Substring(6);
                int eq = body.LastIndexOf('=');
                int v;
                if (eq > 0 && int.TryParse(body.Substring(eq + 1), out v)) counters[body.Substring(0, eq)] = v;
            }
        }
        if (OnGoldChanged != null) OnGoldChanged(gold);
    }
}
