using System;
using UnityEngine;

/// <summary>One thing the scientist can research for gold.</summary>
[Serializable]
public class ResearchProject
{
    [Tooltip("Short unique name with no spaces, for example sprinkler_1. Remembered as the flag research_<id>.")]
    public string id = "project";
    public string title = "New research";
    [TextArea] public string description = "";
    [Min(0)] public int goldCost = 500;
    [Tooltip("The id of a project that must be finished first. Empty = can be researched straight away.")]
    public string requires = "";
    [Tooltip("Items the scientist sells once this is researched (each at the item's Buy Price).")]
    public ItemData[] unlocks = new ItemData[0];
}

/// <summary>
/// Everything the university can research. One asset: Assets/Farm/City/ResearchCatalog.asset
/// (made by Farm > Set Up > City Research and Items; it is KEPT when you run that again, so change prices here freely).
/// </summary>
[CreateAssetMenu(fileName = "ResearchCatalog", menuName = "Farm/Research Catalog")]
public class ResearchCatalog : ScriptableObject
{
    public ResearchProject[] projects = new ResearchProject[0];

    public static string FlagOf(string id) { return "research_" + id; }

    /// <summary>Has the player paid for this research?</summary>
    public static bool IsDone(string id) { return !string.IsNullOrEmpty(id) && GameState.HasFlag(FlagOf(id)); }

    public ResearchProject Find(string id)
    {
        if (projects == null) return null;
        foreach (ResearchProject p in projects) if (p != null && p.id == id) return p;
        return null;
    }
}
