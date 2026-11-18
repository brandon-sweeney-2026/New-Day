using System;
using System.Collections.Generic;

/// <summary>
/// The player's quest journal (shown in the Quests tab of the player menu).
///
/// Anything that hands out a quest tells the journal about it:
///
///   QuestLog.Add("fairy_clean_water", "Clean Water", "Fairy", "Bring 5 jars of water.", () => "Water: 2 / 5");
///   QuestLog.Complete("fairy_clean_water");
///
/// The last argument of Add is a small function that returns the progress text; it is called each time the menu is drawn.
/// The journal is only a LIST for the player to read: the quest giver still decides when a quest is finished.
/// This is a plain static class, so there is nothing to put in the scene.
/// </summary>
public static class QuestLog
{
    /// <summary>One quest in the journal.</summary>
    public class Entry
    {
        public string id, title, giver, description;
        public Func<string> progress;       // may be null
        public bool done;

        public string ProgressText { get { return progress != null ? progress() : ""; } }
    }

    private static readonly List<Entry> entries = new List<Entry>();

    /// <summary>Raised when a quest is added or finished.</summary>
    public static event Action OnChanged;

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        entries.Clear();
        OnChanged = null;
    }

    /// <summary>Every quest, in the order they were accepted.</summary>
    public static IReadOnlyList<Entry> Entries { get { return entries; } }

    public static Entry Find(string id)
    {
        foreach (Entry e in entries) if (e.id == id) return e;
        return null;
    }

    /// <summary>Adds a quest to the journal (or updates it if that id is already there).</summary>
    public static void Add(string id, string title, string giver, string description, Func<string> progress = null)
    {
        Entry e = Find(id);
        if (e == null) { e = new Entry { id = id }; entries.Add(e); }
        e.title = title;
        e.giver = giver;
        e.description = description;
        e.progress = progress;
        if (OnChanged != null) OnChanged();
    }

    /// <summary>Marks a quest as finished. It stays in the journal under "Done".</summary>
    public static void Complete(string id)
    {
        Entry e = Find(id);
        if (e == null || e.done) return;
        e.done = true;
        e.progress = null;
        if (OnChanged != null) OnChanged();
    }

    /// <summary>
    /// Empties the journal and fills it from a save file (used by SaveSystem). Call Restore once per saved quest afterwards.
    /// A restored quest has no progress text until its quest giver is met again and calls Add.
    /// </summary>
    public static void ClearForLoad()
    {
        entries.Clear();
    }

    /// <summary>Puts one saved quest back in the journal without announcing it.</summary>
    public static void Restore(string id, string title, string giver, string description, bool done)
    {
        if (string.IsNullOrEmpty(id) || Find(id) != null) return;
        entries.Add(new Entry { id = id, title = title, giver = giver, description = description, done = done });
    }

    public static int ActiveCount
    {
        get { int n = 0; foreach (Entry e in entries) if (!e.done) n++; return n; }
    }
}
