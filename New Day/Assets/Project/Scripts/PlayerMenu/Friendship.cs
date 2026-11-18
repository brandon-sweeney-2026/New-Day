using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How much each villager likes the player, shown as hearts in the People tab of the player menu.
///
///  - The first chat with someone each day gives a few points.
///  - One gift per person per day gives more (or takes some away), depending on their NPC Profile:
///    Loves / Likes / Dislikes. Anything else counts as a plain, neutral gift.
///  - What you learn about someone's taste by giving gifts is remembered and listed in the menu.
///
/// The numbers (points per heart, points per gift...) are in the Player Menu Settings asset under Friendship.
/// Points are stored in GameState counters ("friend_<id>"), so they save with the rest of the game.
/// ScheduledNpc calls Meet / Talk / Gift; this is a plain static class, so there is nothing to put in the scene.
/// </summary>
public static class Friendship
{
    public enum Reaction { Disliked, Neutral, Liked, Loved }

    /// <summary>Somebody the player has met.</summary>
    public class Person
    {
        public string id, name;
        public Sprite portrait;
        public NpcProfile profile;      // may be null: then every gift is neutral
    }

    private static readonly List<Person> known = new List<Person>();

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        known.Clear();
    }

    /// <summary>Everyone met so far, in the order they were met.</summary>
    public static IReadOnlyList<Person> Known { get { return known; } }

    // ------------------------------------------------------------------
    // Meeting and talking
    // ------------------------------------------------------------------

    /// <summary>Adds someone to the People list the first time they are spoken to.</summary>
    public static void Meet(string id, string name, Sprite portrait, NpcProfile profile)
    {
        if (string.IsNullOrEmpty(id)) return;

        Person p = Find(id);
        if (p == null)
        {
            p = new Person { id = id };
            known.Add(p);
            GameState.AddCounter("people_met");
        }
        p.name = name;
        if (portrait != null) p.portrait = portrait;
        else if (profile != null && profile.portrait != null) p.portrait = profile.portrait;
        if (profile != null) p.profile = profile;
    }

    /// <summary>Forgets everyone, ready to be filled from a save file (used by SaveSystem).</summary>
    public static void ClearForLoad()
    {
        known.Clear();
    }

    /// <summary>
    /// Puts a saved person back in the People list. Their portrait and tastes come back the next time
    /// you talk to them (Meet fills those in); their hearts are already in the GameState counters.
    /// </summary>
    public static void Restore(string id, string name)
    {
        if (string.IsNullOrEmpty(id) || Find(id) != null) return;
        known.Add(new Person { id = id, name = name });
    }

    public static Person Find(string id)
    {
        foreach (Person p in known) if (p.id == id) return p;
        return null;
    }

    /// <summary>Call when the player chats with someone. Only the first chat of the day gives points.</summary>
    public static void Talk(string id)
    {
        if (TalkedToday(id)) return;
        GameState.SetCounter("friend_talk_" + id, Today + 1);
        AddPoints(id, PlayerMenuSettings.Current.talkPoints);
    }

    public static bool TalkedToday(string id) { return GameState.GetCounter("friend_talk_" + id) == Today + 1; }
    public static bool GiftedToday(string id) { return GameState.GetCounter("friend_gift_" + id) == Today + 1; }

    // The day number is stored +1 so that "never" (0) is different from "on day 0".
    private static int Today { get { return TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 0; } }

    // ------------------------------------------------------------------
    // Gifts
    // ------------------------------------------------------------------

    /// <summary>How this person feels about an item.</summary>
    public static Reaction ReactionTo(NpcProfile profile, ItemData item)
    {
        if (profile == null || item == null) return Reaction.Neutral;
        if (Contains(profile.loves, item)) return Reaction.Loved;
        if (Contains(profile.likes, item)) return Reaction.Liked;
        if (Contains(profile.dislikes, item)) return Reaction.Disliked;
        return Reaction.Neutral;
    }

    /// <summary>Gives a gift (the caller removes the item from the backpack). Returns how it was received.</summary>
    public static Reaction Gift(string id, ItemData item)
    {
        Person p = Find(id);
        Reaction reaction = ReactionTo(p != null ? p.profile : null, item);
        PlayerMenuSettings s = PlayerMenuSettings.Current;

        int points = reaction == Reaction.Loved ? s.lovedGiftPoints
                   : reaction == Reaction.Liked ? s.likedGiftPoints
                   : reaction == Reaction.Disliked ? s.dislikedGiftPoints
                   : s.neutralGiftPoints;

        GameState.SetCounter("friend_gift_" + id, Today + 1);
        GameState.AddCounter("gifts_given");
        if (item != null) GameState.SetFlag(TasteFlag(id, item));      // now the player knows what they think of it
        AddPoints(id, points);
        return reaction;
    }

    private static string TasteFlag(string id, ItemData item) { return "taste_" + id + "_" + item.name; }

    /// <summary>Has the player found out (by giving it) what this person thinks of this item?</summary>
    public static bool KnowsTaste(string id, ItemData item)
    {
        return item != null && GameState.HasFlag(TasteFlag(id, item));
    }

    // ------------------------------------------------------------------
    // Points and hearts
    // ------------------------------------------------------------------

    public static int Points(string id) { return GameState.GetCounter("friend_" + id); }

    public static int MaxPoints
    {
        get { PlayerMenuSettings s = PlayerMenuSettings.Current; return s.pointsPerHeart * s.maxHearts; }
    }

    public static int Hearts(string id)
    {
        PlayerMenuSettings s = PlayerMenuSettings.Current;
        return Mathf.Clamp(Points(id) / Mathf.Max(1, s.pointsPerHeart), 0, s.maxHearts);
    }

    /// <summary>How far through the current heart, 0-1.</summary>
    public static float HeartProgress(string id)
    {
        PlayerMenuSettings s = PlayerMenuSettings.Current;
        if (Hearts(id) >= s.maxHearts) return 1f;
        int per = Mathf.Max(1, s.pointsPerHeart);
        return (Points(id) % per) / (float)per;
    }

    public static void AddPoints(string id, int amount)
    {
        if (amount == 0 || string.IsNullOrEmpty(id)) return;

        int heartsBefore = Hearts(id);
        GameState.SetCounter("friend_" + id, Mathf.Clamp(Points(id) + amount, 0, MaxPoints));
        int heartsAfter = Hearts(id);

        if (heartsAfter > heartsBefore)
        {
            Person p = Find(id);
            GameEvents.ShowToast("Friendship with " + (p != null ? p.name : id) + ": " + heartsAfter + (heartsAfter == 1 ? " heart" : " hearts"));
        }
    }

    private static bool Contains(ItemData[] items, ItemData item)
    {
        if (items == null) return false;
        foreach (ItemData i in items) if (i == item) return true;
        return false;
    }
}
