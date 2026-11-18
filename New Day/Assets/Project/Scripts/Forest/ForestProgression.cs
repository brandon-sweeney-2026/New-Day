using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which items the forest stumps ask for, from easy to hard: the Healing Forest's six heart stumps (Farm > Set Up > Forest Scene)
/// and the old maze's doors both read it. Drag your FARM items (crops, eggs...) and QUARRY items (stone, coal, copper...) in, easiest first.
/// (This must stay in a file called ForestProgression.cs: Unity can only save a settings asset whose script file has the same name as the class.)
/// </summary>
[CreateAssetMenu(fileName = "ForestProgression", menuName = "Farm/Forest Progression")]
public class ForestProgression : ScriptableObject
{
    /// <summary>What ONE stump asks for. Pick the items from the drop-down menus.</summary>
    [System.Serializable]
    public class StumpRequest
    {
        [Tooltip("Just a note for you, e.g. 'Zone 3 - first quarry item'.")]
        public string note;
        [Tooltip("What this stump asks for. Leave empty to let the automatic lists below choose.")]
        public ForestRequirement[] requirements = new ForestRequirement[0];
    }

    [Header("Choose each stump yourself (optional)")]
    [Tooltip("Stump 1 is the first zone's heart stump (the Trailhead), stump 2 the next, and so on. Any stump left empty (or not listed) uses the automatic lists below.")]
    public StumpRequest[] stumps = new StumpRequest[0];

    [Header("Automatic lists (used for stumps you did not set above)")]
    public ItemData[] farmItems;
    public ItemData[] quarryItems;
    [Min(1)] public int firstCount = 2;
    [Tooltip("Each stump further along asks for this many more (rounded).")] public float extraPerDoor = 0.6f;
    [Tooltip("Never ask for more than this many of one item.")] public int maxCount = 20;
    [Tooltip("Every Nth stump asks for BOTH a farm and a quarry item.")] public int bothEvery = 4;

    /// <summary>The requirements for stump number 'i' out of 'total' (0 = first stump).</summary>
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
        var filled = new StumpRequest[total];
        stumps = new StumpRequest[0];                           // so For() uses the automatic lists
        for (int i = 0; i < total; i++)
            filled[i] = new StumpRequest { note = "Stump " + (i + 1), requirements = For(i, total).ToArray() };
        stumps = filled;
    }

    private static void Add(List<ForestRequirement> list, ItemData[] pool, int i, int total, int count)
    {
        if (pool == null || pool.Length == 0) return;
        int idx = Mathf.Clamp(Mathf.FloorToInt((float)i / Mathf.Max(1, total) * pool.Length), 0, pool.Length - 1);
        if (pool[idx] == null) return;
        list.Add(new ForestRequirement { item = pool[idx], count = count });
    }
}
