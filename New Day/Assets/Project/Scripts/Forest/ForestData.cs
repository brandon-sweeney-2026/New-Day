using System.Collections.Generic;
using UnityEngine;

/// <summary>"Bring me 5 Carrots." Used by forest doors (what the stump wants) and by the Fairy's quests.</summary>
[System.Serializable]
public class ForestRequirement
{
    public ItemData item;
    [Min(1)] public int count = 1;
}

/// <summary>
/// The look of a forest room: floor, walls, gate pictures, scenery. Make one with Farm > Forest > Set Up Forest
/// (a polluted style for the maze and a clean one for the grove). Change the pictures here to restyle every room that uses it.
/// </summary>
[CreateAssetMenu(fileName = "ForestStyle", menuName = "Farm/Forest Style")]
public class ForestStyle : ScriptableObject
{
    [Header("Ground and walls")]
    [Tooltip("One of these is tiled over the whole room. Use a single picture for a plain floor.")]
    public Sprite[] floorTiles;
    public Sprite wallTile;
    [Tooltip("Extra scenery scattered around inside the room (dead trees, puddles, roots...).")]
    public Sprite[] scenery;
    public int sceneryCount = 8;
    [Tooltip("The most scenery items any one room can have, however big the room is.")]
    [Min(0)] public int maxScenery = 7;
    [Tooltip("How much bigger the dead trees are drawn (2 = twice as big).")]
    [Min(0.5f)] public float deadTreeScale = 2f;
    [Tooltip("Scenery casts a shadow from the sun and from lamps.")]
    public bool sceneryShadows = true;
    [Tooltip("Dead tree shadows are this many times longer than normal (they're tall).")]
    public float treeShadowLength = 1.3f;
    [Tooltip("Scenery pictures drawn flat on the ground (puddles) - tick for those in the list above that have no height.")]
    public bool sceneryFlat = false;

    [Header("Gates (at the door gaps)")]
    public Sprite gateAcrossLocked;   // 32x16, blocks a gap in a top/bottom wall
    public Sprite gateAcrossOpen;
    public Sprite gateUpDownLocked;   // 16x32, blocks a gap in a left/right wall
    public Sprite gateUpDownOpen;
    [Tooltip("The same gates in green. They fade in over the purple ones when a gate unlocks. Farm > Forest > Add Green Gates fills these in.")]
    public Sprite gateAcrossGreen;
    public Sprite gateUpDownGreen;

    [Header("Mood")]
    [Tooltip("Drifting purple fog over the room (the pollution). Off in the grove.")]
    public bool fog = true;
    public Color fogColor = new Color(0.45f, 0.2f, 0.65f, 0.16f);
    [Tooltip("How much fog: 0 = none, 1 = the normal amount, 2 = very thick. More fog means more drifting patches AND stronger colour. (Takes effect when the scene starts.)")]
    [Range(0f, 2f)] public float fogAmount = 1f;
}

/// <summary>Remembers which forest doors are open. Open doors stay open for good (saved with the game).</summary>
public static class ForestProgress
{
    public static string Key(string doorId) { return "forest_door_" + doorId; }
    public static bool IsUnlocked(string doorId) { return GameState.HasFlag(Key(doorId)); }
    public static void Unlock(string doorId) { GameState.SetFlag(Key(doorId), true); }

    /// <summary>How many of a requirement the player has already put on a stump (stumps keep your offerings between visits).</summary>
    public static int Given(string doorId, int index) { return GameState.GetCounter("forest_give_" + doorId + "_" + index); }
    public static void AddGiven(string doorId, int index, int n) { GameState.AddCounter("forest_give_" + doorId + "_" + index, n); }
}

/// <summary>
/// Which items the maze asks for, from easy to hard. The maze generator uses it to fill in every door.
/// Drag your FARM items (crops, eggs...) and QUARRY items (stone, coal, copper...) in, easiest first.
/// </summary>
[CreateAssetMenu(fileName = "ForestProgression", menuName = "Farm/Forest Progression")]
public class ForestProgression : ScriptableObject
{
    /// <summary>What ONE stump asks for. Pick the items from the drop-down menus.</summary>
    [System.Serializable]
    public class StumpRequest
    {
        [Tooltip("Just a note for you, e.g. 'Room 3 - first quarry item'.")]
        public string note;
        [Tooltip("What this stump asks for. Leave empty to let the automatic lists below choose.")]
        public ForestRequirement[] requirements = new ForestRequirement[0];
    }

    [Header("Choose each stump yourself (optional)")]
    [Tooltip("Stump 1 is the first room on the main path, stump 2 the next, and so on. Any stump left empty (or not listed) uses the automatic lists below.")]
    public StumpRequest[] stumps = new StumpRequest[0];

    [Header("Automatic lists (used for stumps you did not set above)")]
    public ItemData[] farmItems;
    public ItemData[] quarryItems;
    [Min(1)] public int firstCount = 2;
    [Tooltip("Each door further along asks for this many more (rounded).")] public float extraPerDoor = 0.6f;
    [Tooltip("Never ask for more than this many of one item.")] public int maxCount = 20;
    [Tooltip("Every Nth door asks for BOTH a farm and a quarry item.")] public int bothEvery = 4;

    /// <summary>The requirements for door number 'i' out of 'total' (0 = first door).</summary>
    public List<ForestRequirement> For(int i, int total)
    {
        var list = new List<ForestRequirement>();
        if (stumps != null && i >= 0 && i < stumps.Length && stumps[i] != null && stumps[i].requirements != null)
        {
            foreach (ForestRequirement r in stumps[i].requirements)
                if (r != null && r.item != null) list.Add(new ForestRequirement { item = r.item, count = Mathf.Max(1, r.count) });
            if (list.Count > 0) return list;
        }
        int count = Mathf.Min(maxCount, firstCount + Mathf.RoundToInt(i * extraPerDoor));
        bool both = bothEvery > 0 && (i + 1) % bothEvery == 0;
        bool farmTurn = i % 2 == 0;
        if (both || farmTurn) Add(list, farmItems, i, total, count);
        if (both || !farmTurn) Add(list, quarryItems, i, total, both ? Mathf.Max(1, count / 2) : count);
        if (list.Count == 0) Add(list, farmTurn ? quarryItems : farmItems, i, total, count);
        return list;
    }

    /// <summary>Right-click the asset's header > this, to copy the automatic choices into the stump list so you can change them with the drop-downs.</summary>
    [ContextMenu("Fill Stumps From Automatic Lists (20 stumps)")]
    private void FillStumps()
    {
        const int total = 20;
        stumps = new StumpRequest[total];
        for (int i = 0; i < total; i++)
        {
            var saved = stumps;
            stumps = new StumpRequest[0];                       // so For() uses the automatic lists
            List<ForestRequirement> auto = For(i, total);
            stumps = saved;
            stumps[i] = new StumpRequest { note = "Stump " + (i + 1), requirements = auto.ToArray() };
        }
    }

    private static void Add(List<ForestRequirement> list, ItemData[] pool, int i, int total, int count)
    {
        if (pool == null || pool.Length == 0) return;
        int idx = Mathf.Clamp(Mathf.FloorToInt((float)i / Mathf.Max(1, total) * pool.Length), 0, pool.Length - 1);
        if (pool[idx] == null) return;
        list.Add(new ForestRequirement { item = pool[idx], count = count });
    }
}
