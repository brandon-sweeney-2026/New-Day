using System;
using UnityEngine;

/// <summary>
/// One NPC's storyline: ten parts that unlock one at a time as you become friends (Create > Farm > NPC Story Arc).
///
/// HOW A STORY PLAYS
///   - When you talk to the NPC and their next part is ready, they tell you that part instead of an everyday chat line.
///   - A part is ready when: the part before it is finished, you have at least 'Hearts Needed' hearts with them,
///     its Conditions are true (season, weather, flags, counters...), and no other part of THIS story was finished today.
///   - A part can ASK for items (a small quest in the journal). You hand them over the next time you talk while carrying them.
///   - A part can offer a CHOICE (usually Part 8). Picking one sets the story's path to A or B for good. Parts after that
///     can be marked "Only A" or "Only B", so Parts 9 and 10 usually exist twice: one version for each path.
///
/// WHERE PROGRESS IS KEPT (GameState, so it is saved with the game)
///   counter story_NPCID       = the number of the last finished part (0 = not started)
///   counter story_path_NPCID  = 0 no choice yet, 1 path A, 2 path B
///   flag    story_NPCID_N     = part N is finished (handy in another NPC's Conditions: "after Rosa's Part 7")
///   flag    story_NPCID_done  = the whole story is finished
///
/// Story assets live in Assets/Resources/Stories so every NPC can find theirs by npc id, in any scene.
/// Farm > Set Up > Story Arcs makes the starter stories. Farm > Fix and Diagnose > Check Story Arcs looks for mistakes.
/// </summary>
[CreateAssetMenu(menuName = "Farm/NPC Story Arc", fileName = "Story_")]
public class NpcStoryArc : ScriptableObject
{
    /// <summary>Which parts a part belongs to once the story's choice has been made.</summary>
    public enum Path { Both, OnlyA, OnlyB }

    [Header("Who")]
    [Tooltip("The NPC's id: the same as 'Npc Id' on their schedule asset (rosa, sam, marcus, frank...).")]
    public string npcId = "";
    [Tooltip("The story's name, shown in the People tab of the player menu.")]
    public string title = "A Story";
    [TextArea(2, 6)] [Tooltip("For you, the designer: what this story is about. Not shown in the game.")]
    public string summary = "";

    [Header("The two endings (shown in the People tab after the choice)")]
    public string pathAName = "Path A";
    public string pathBName = "Path B";

    [Header("The parts (in order; Parts 9 and 10 usually appear twice, once per path)")]
    public Part[] parts = new Part[0];

    /// <summary>One part of the story.</summary>
    [Serializable]
    public class Part
    {
        [Tooltip("A short name for you and for the People tab, e.g. 'The soup on the step'.")]
        public string title = "Part";
        [Range(1, 20)] [Tooltip("Which part this is. Two versions of the same part (one Only A, one Only B) share a number.")]
        public int number = 1;
        [Range(0, 20)] [Tooltip("Friendship hearts needed before this part can play. Usually the same as the part number.")]
        public int heartsNeeded = 1;
        [Tooltip("Both = everyone sees this part. Only A / Only B = only after that choice was picked.")]
        public Path path = Path.Both;
        [Tooltip("Anything else that must be true (season, weather, flags, counters). Leave empty for 'any time'.")]
        public NpcCondition conditions = new NpcCondition();

        [Header("What they say")]
        [TextArea(2, 5)] public string[] pages = { "..." };
        [Tooltip("Show the happy portrait while this part is told.")]
        public bool happy = false;

        [Header("Optional: ask for items (a quest)")]
        public Ask ask = new Ask();

        [Header("Optional: a choice (the story's fork)")]
        public Choice choice = new Choice();

        [Header("When the part is finished")]
        [Tooltip("Extra GameState flags to set, e.g. saw_tower_dumping or market_open.")]
        public string[] setFlags = new string[0];
        public Reward reward = new Reward();
    }

    /// <summary>"Bring me 10 Wheat." The part is finished when the items are handed over.</summary>
    [Serializable]
    public class Ask
    {
        [Tooltip("Leave empty for a part that asks for nothing.")]
        public ItemData item;
        [Min(0)] public int amount = 0;
        [Range(0, 3)] [Tooltip("Lowest crop quality accepted: 0 Bronze (anything), 1 Silver, 2 Gold, 3 Platinum.")]
        public int minQuality = 0;
        [Tooltip("The quest's name in the Quests tab.")]
        public string questTitle = "";
        [TextArea(2, 4)] public string questText = "";
        [TextArea(2, 4)] [Tooltip("What they say if you talk to them before you have everything.")]
        public string[] reminder = new string[0];
        [TextArea(2, 4)] [Tooltip("What they say when you hand the items over.")]
        public string[] thanks = new string[0];

        /// <summary>True when this part really asks for something.</summary>
        public bool Active { get { return item != null && amount > 0; } }
    }

    /// <summary>Two answers. Picking one decides the story's path (A or B) for good.</summary>
    [Serializable]
    public class Choice
    {
        [Tooltip("Tick to show the two answers after this part's pages.")]
        public bool enabled = false;
        public string optionA = "";
        [TextArea(2, 4)] public string[] replyA = new string[0];
        public string[] flagsA = new string[0];
        public string optionB = "";
        [TextArea(2, 4)] public string[] replyB = new string[0];
        public string[] flagsB = new string[0];
    }

    /// <summary>A thank-you when the part is finished.</summary>
    [Serializable]
    public class Reward
    {
        [Min(0)] public int gold = 0;
        public ItemData item;
        [Min(0)] public int amount = 0;
        [Min(0)] [Tooltip("Bonus friendship points (100 = one heart, unless Player Menu Settings says otherwise).")]
        public int friendship = 0;
    }

    /// <summary>The highest part number in this story (normally 10).</summary>
    public int LastNumber
    {
        get
        {
            int n = 0;
            if (parts != null) foreach (Part p in parts) if (p != null && p.number > n) n = p.number;
            return n;
        }
    }
}
