using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays NPC storylines (NpcStoryArc assets). ScheduledNpc asks this class first whenever you talk to someone:
/// if their next story part is ready, it is told instead of an everyday chat line.
///
/// Nothing to put in a scene: the stories are loaded from Assets/Resources/Stories and found by npc id.
/// All progress is stored in GameState counters and flags (see NpcStoryArc for the names), so it is saved with the game.
/// </summary>
public static class StoryArcs
{
    private const string Folder = "Stories";      // Assets/Resources/Stories
    private static readonly string[] QualityNames = { "Bronze", "Silver", "Gold", "Platinum" };

    private static Dictionary<string, NpcStoryArc> byId;

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() { byId = null; }

    // ------------------------------------------------------------------ finding stories

    /// <summary>The story for this NPC id, or null if they have none.</summary>
    public static NpcStoryArc For(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;
        if (byId == null)
        {
            byId = new Dictionary<string, NpcStoryArc>();
            foreach (NpcStoryArc arc in Resources.LoadAll<NpcStoryArc>(Folder))
            {
                if (arc == null || string.IsNullOrEmpty(arc.npcId)) continue;
                if (byId.ContainsKey(arc.npcId)) { Debug.LogWarning("Two story arcs are for '" + arc.npcId + "'. Only one is used: " + byId[arc.npcId].name); continue; }
                byId[arc.npcId] = arc;
            }
        }
        NpcStoryArc found;
        return byId.TryGetValue(npcId, out found) ? found : null;
    }

    // ------------------------------------------------------------------ progress (all in GameState)

    /// <summary>The number of the last finished part (0 = not started).</summary>
    public static int Progress(string npcId) { return GameState.GetCounter("story_" + npcId); }

    /// <summary>0 = no choice made yet, 1 = path A, 2 = path B.</summary>
    public static int ChosenPath(string npcId) { return GameState.GetCounter("story_path_" + npcId); }

    private static string AskedFlag(string npcId) { return "story_" + npcId + "_asked"; }
    private static int Today { get { return TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 0; } }
    private static bool FinishedOneToday(string npcId) { return GameState.GetCounter("story_day_" + npcId) == Today + 1; }

    /// <summary>The next part to tell (whether or not it is ready yet), or null when the story is over.</summary>
    public static NpcStoryArc.Part NextPart(NpcStoryArc arc)
    {
        if (arc == null || arc.parts == null) return null;
        int want = Progress(arc.npcId) + 1;
        int path = ChosenPath(arc.npcId);
        foreach (NpcStoryArc.Part p in arc.parts)
        {
            if (p == null || p.number != want) continue;
            if (p.path == NpcStoryArc.Path.OnlyA && path != 1) continue;
            if (p.path == NpcStoryArc.Path.OnlyB && path != 2) continue;
            return p;
        }
        return null;
    }

    /// <summary>True when this part may start now (hearts, conditions, and one part per day).</summary>
    private static bool Ready(NpcStoryArc arc, NpcStoryArc.Part part)
    {
        if (Friendship.Hearts(arc.npcId) < part.heartsNeeded) return false;
        if (part.conditions != null && !part.conditions.Matches()) return false;
        if (FinishedOneToday(arc.npcId)) return false;
        return true;
    }

    // ------------------------------------------------------------------ talking

    /// <summary>
    /// Called by ScheduledNpc when you talk to someone. Tells the next story part if it is ready (or the reminder for a part
    /// that is waiting for items) and returns true. Returns false when there is nothing to tell, so the NPC says an ordinary line.
    /// 'normalOptions' makes the NPC's usual choices (Buy, Give a gift, Goodbye...), shown after the story.
    /// </summary>
    public static bool TryTalk(string npcId, DialogueSpeaker speaker, PlayerActions player, Func<List<DialogueOption>> normalOptions)
    {
        NpcStoryArc arc = For(npcId);
        NpcStoryArc.Part part = NextPart(arc);
        if (part == null) return false;
        Inventory inv = player != null ? player.Inventory : null;

        // 1) a part that already asked for items: remind, and offer to hand them over
        if (part.ask.Active && GameState.HasFlag(AskedFlag(npcId)))
        {
            AddQuest(arc, part);      // (puts the progress text back after loading a save)
            List<DialogueOption> o = normalOptions();
            if (CountUsable(inv, part.ask) >= part.ask.amount) o.Insert(0, HandOverOption(arc, part, speaker, player, normalOptions));
            string[] remind = part.ask.reminder != null && part.ask.reminder.Length > 0 ? part.ask.reminder
                            : new[] { "Did you manage to find " + AskText(part.ask) + "?" };
            DialogueBox.Show(speaker, remind, o, null);
            return true;
        }

        if (!Ready(arc, part)) return false;

        // 2) a part that asks for items: tell it, put the quest in the journal, and wait
        if (part.ask.Active)
        {
            GameState.SetFlag(AskedFlag(npcId));
            AddQuest(arc, part);
            List<DialogueOption> o = normalOptions();
            if (CountUsable(inv, part.ask) >= part.ask.amount) o.Insert(0, HandOverOption(arc, part, speaker, player, normalOptions));
            DialogueBox.Show(speaker, Pages(part), o, null, part.happy);
            return true;
        }

        // 3) the fork: two answers; the part is finished when one is picked (closing the box without picking = ask again later)
        if (part.choice.enabled)
        {
            NpcStoryArc.Choice c = part.choice;
            var o = new List<DialogueOption>
            {
                new DialogueOption(string.IsNullOrEmpty(c.optionA) ? "(A)" : c.optionA, () => Choose(arc, part, 1, c.replyA, c.flagsA, speaker, player, normalOptions)),
                new DialogueOption(string.IsNullOrEmpty(c.optionB) ? "(B)" : c.optionB, () => Choose(arc, part, 2, c.replyB, c.flagsB, speaker, player, normalOptions)),
            };
            DialogueBox.Show(speaker, Pages(part), o, null, part.happy);
            return true;
        }

        // 4) an ordinary part: it counts as finished as soon as it is told
        Finish(arc, part, player);
        DialogueBox.Show(speaker, Pages(part), normalOptions(), null, part.happy);
        return true;
    }

    private static void Choose(NpcStoryArc arc, NpcStoryArc.Part part, int path, string[] reply, string[] flags,
                               DialogueSpeaker speaker, PlayerActions player, Func<List<DialogueOption>> normalOptions)
    {
        GameState.SetCounter("story_path_" + arc.npcId, path);
        GameState.SetFlag("story_" + arc.npcId + (path == 1 ? "_chose_a" : "_chose_b"));
        SetFlags(flags);
        Finish(arc, part, player);
        string[] pages = reply != null && reply.Length > 0 ? reply : new[] { "..." };
        DialogueBox.Show(speaker, pages, normalOptions(), null, true);
    }

    private static DialogueOption HandOverOption(NpcStoryArc arc, NpcStoryArc.Part part, DialogueSpeaker speaker, PlayerActions player, Func<List<DialogueOption>> normalOptions)
    {
        return new DialogueOption("Give " + AskText(part.ask), () =>
        {
            Inventory inv = player != null ? player.Inventory : null;
            if (!TakeUsable(inv, part.ask)) { DialogueBox.Close(); return; }
            Finish(arc, part, player);
            string[] thanks = part.ask.thanks != null && part.ask.thanks.Length > 0 ? part.ask.thanks : new[] { "Thank you! This is perfect." };
            DialogueBox.Show(speaker, thanks, normalOptions(), null, true);
        });
    }

    /// <summary>Marks a part as finished: progress, flags, reward, journal, and "one part per day".</summary>
    private static void Finish(NpcStoryArc arc, NpcStoryArc.Part part, PlayerActions player)
    {
        string id = arc.npcId;
        GameState.SetCounter("story_" + id, part.number);
        GameState.SetFlag("story_" + id + "_" + part.number);
        GameState.SetCounter("story_day_" + id, Today + 1);
        GameState.SetFlag(AskedFlag(id), false);
        if (part.ask.Active) QuestLog.Complete(QuestId(arc, part));
        SetFlags(part.setFlags);
        if (NextPart(arc) == null) GameState.SetFlag("story_" + id + "_done");

        NpcStoryArc.Reward r = part.reward;
        if (r == null) return;
        if (r.gold > 0) GameState.AddGold(r.gold);
        if (r.friendship > 0) Friendship.AddPoints(id, r.friendship);
        if (r.item != null && r.amount > 0 && player != null && player.Inventory != null)
        {
            int left = player.Inventory.Add(r.item, r.amount);
            if (left > 0) Debug.LogWarning("Story " + id + ": the backpack was full, so " + left + " " + r.item.itemName + " could not be given.");
        }
    }

    private static void SetFlags(string[] flags)
    {
        if (flags == null) return;
        foreach (string f in flags) if (!string.IsNullOrEmpty(f)) GameState.SetFlag(f);
    }

    private static string[] Pages(NpcStoryArc.Part part)
    {
        return part.pages != null && part.pages.Length > 0 ? part.pages : new[] { "..." };
    }

    // ------------------------------------------------------------------ quests (items to hand over)

    private static string QuestId(NpcStoryArc arc, NpcStoryArc.Part part) { return "story_" + arc.npcId + "_" + part.number; }

    private static void AddQuest(NpcStoryArc arc, NpcStoryArc.Part part)
    {
        NpcStoryArc.Ask a = part.ask;
        string title = !string.IsNullOrEmpty(a.questTitle) ? a.questTitle : part.title;
        string giver = Friendship.Find(arc.npcId) != null ? Friendship.Find(arc.npcId).name : arc.npcId;
        string text = !string.IsNullOrEmpty(a.questText) ? a.questText : "Bring " + AskText(a) + ".";
        QuestLog.Add(QuestId(arc, part), title, giver, text, () =>
        {
            PlayerActions p = UnityEngine.Object.FindAnyObjectByType<PlayerActions>();
            int have = CountUsable(p != null ? p.Inventory : null, a);
            return a.item.itemName + ": " + Mathf.Min(have, a.amount) + " / " + a.amount;
        });
    }

    /// <summary>"10 Wheat" or "5 Gold-quality Tomato".</summary>
    private static string AskText(NpcStoryArc.Ask a)
    {
        string q = a.minQuality > 0 ? QualityNames[Mathf.Clamp(a.minQuality, 0, 3)] + "-quality " : "";
        return a.amount + " " + q + a.item.itemName;
    }

    /// <summary>How many of the asked item the backpack holds at the asked quality or better.</summary>
    private static int CountUsable(Inventory inv, NpcStoryArc.Ask a)
    {
        if (inv == null || a.item == null) return 0;
        int total = 0;
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            ItemStack s = inv.GetSlot(i);
            if (s != null && !s.IsEmpty && s.item == a.item && s.quality >= a.minQuality) total += s.count;
        }
        return total;
    }

    /// <summary>Removes the asked items (lowest acceptable quality first, so your best crops are kept). False if there are not enough.</summary>
    private static bool TakeUsable(Inventory inv, NpcStoryArc.Ask a)
    {
        if (CountUsable(inv, a) < a.amount) return false;
        int need = a.amount;
        for (int q = a.minQuality; q <= 3 && need > 0; q++)
            for (int i = 0; i < Inventory.SlotCount && need > 0; i++)
            {
                ItemStack s = inv.GetSlot(i);
                if (s == null || s.IsEmpty || s.item != a.item || s.quality != q) continue;
                need -= inv.RemoveFromSlot(i, need);
            }
        return true;
    }

    // ------------------------------------------------------------------ the People tab

    /// <summary>A few lines about this NPC's story for the player menu, or "" if they have no story.</summary>
    public static string MenuText(string npcId)
    {
        NpcStoryArc arc = For(npcId);
        if (arc == null) return "";
        int done = Progress(npcId), last = arc.LastNumber;
        string text = "Story: " + arc.title + " (part " + done + " of " + last + ")";
        int path = ChosenPath(npcId);
        if (path == 1 && !string.IsNullOrEmpty(arc.pathAName)) text += "\nYou chose: " + arc.pathAName;
        if (path == 2 && !string.IsNullOrEmpty(arc.pathBName)) text += "\nYou chose: " + arc.pathBName;

        NpcStoryArc.Part next = NextPart(arc);
        if (next == null) text += done > 0 ? "\nThis story is finished." : "";
        else if (next.ask.Active && GameState.HasFlag(AskedFlag(npcId))) text += "\nThey are waiting for " + AskText(next.ask) + ".";
        else if (Friendship.Hearts(npcId) < next.heartsNeeded) text += "\nThe next part unlocks at " + next.heartsNeeded + " hearts.";
        else if (FinishedOneToday(npcId)) text += "\nMore tomorrow.";
        else if (next.conditions != null && !next.conditions.Matches()) text += "\nThe next part is waiting for the right moment.";
        else text += "\nThey have something to tell you.";
        return text;
    }
}
