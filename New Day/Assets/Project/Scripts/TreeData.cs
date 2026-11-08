using System;
using UnityEngine;

/// <summary>
/// One kind of tree (Oak, Apple, Pine, Maple...). Create more: right-click > Create > Farm > Tree.
///
/// A tree grows through 4 stages (sapling, young, juvenile, mature) and looks different in each of the 4 seasons,
/// so it needs 16 pictures. Mature trees drop items (fruit, nuts...) depending on the season, and can be chopped with an axe.
/// </summary>
[CreateAssetMenu(fileName = "NewTree", menuName = "Farm/Tree")]
public class TreeData : ScriptableObject
{
    [Serializable]
    public class SeasonDrop
    {
        [Tooltip("What falls from the tree in this season. Leave empty for nothing.")]
        public ItemData item;
        [Min(1)] public int minAmount = 1;
        [Min(1)] public int maxAmount = 2;
        [Tooltip("Chance (0-1) each day that a mature tree drops this.")]
        [Range(0f, 1f)] public float chancePerDay = 0.3f;
    }

    [Header("Basics")]
    public string treeName = "Tree";
    [Tooltip("How likely this tree is to be picked when a new one sprouts (relative to the others).")]
    [Min(0f)] public float spawnWeight = 1f;

    [Header("Random sprouting (wild trees)")]
    [Tooltip("Chance (0-1) that a sprout attempt of this tree actually happens. Lower = rarer tree. Multiplies with the TreeManager's Sprout Chance.")]
    [Range(0f, 1f)] public float sproutChance = 1f;
    [Tooltip("Seasons in which this tree may sprout by itself: Spring, Summer, Autumn, Winter.")]
    public bool[] sproutSeasons = { true, true, true, true };

    [Header("Seed drop (same kind of tree)")]
    [Tooltip("Item that plants this tree (create an item of type Seed). Leave empty for no seeds.")]
    public ItemData seedItem;
    [Tooltip("Chance (0-1) each day that a fully grown tree drops one seed, on top of its seasonal drops.")]
    [Range(0f, 1f)] public float seedDropChancePerDay = 0.15f;
    [Tooltip("Chance (0-1) of getting a seed when the tree is chopped down (mature trees only).")]
    [Range(0f, 1f)] public float seedDropChanceOnChop = 0.5f;

    [Header("Pictures: 16 sprites, season by season")]
    [Tooltip("Order: Spring stage 1,2,3,4, then Summer 1-4, then Autumn 1-4, then Winter 1-4. (The setup menu fills this in.)")]
    public Sprite[] sprites = new Sprite[16];

    [Header("Growth")]
    [Tooltip("Days spent in stage 1, 2 and 3 before growing up. Stage 4 is fully grown.")]
    public int[] daysInStage = { 2, 3, 4 };
    [Tooltip("Polluted ground slows growth (uses the Growth Chance in your Pollution Settings).")]
    public bool slowedByPollution = true;

    [Header("Chopping (axe)")]
    [Tooltip("Axe hits needed to cut it down at stage 1, 2, 3 and 4.")]
    public int[] hitsByStage = { 1, 2, 4, 6 };
    public ItemData logItem;
    [Tooltip("Logs dropped when cut down at stage 1, 2, 3 and 4.")]
    public int[] logsByStage = { 0, 1, 2, 4 };

    [Header("Drops from a mature tree (Spring, Summer, Autumn, Winter)")]
    public SeasonDrop[] seasonDrops = new SeasonDrop[4];

    [Header("Collision")]
    [Tooltip("Size of the trunk's solid area (world units), at stage 2 and above.")]
    public Vector2 trunkSize = new Vector2(0.5f, 0.35f);

    public bool CanSproutIn(int season)
    {
        return sproutSeasons == null || season < 0 || season >= sproutSeasons.Length || sproutSeasons[season];
    }

    /// <summary>Picture for a season (0-3) and stage (0-3).</summary>
    public Sprite GetSprite(int season, int stage)
    {
        int index = Mathf.Clamp(season, 0, 3) * 4 + Mathf.Clamp(stage, 0, 3);
        return sprites != null && index < sprites.Length ? sprites[index] : null;
    }

    public int DaysInStage(int stage)
    {
        if (daysInStage == null || stage < 0 || stage >= daysInStage.Length) return 1;
        return Mathf.Max(1, daysInStage[stage]);
    }

    public int Hits(int stage)
    {
        if (hitsByStage == null || hitsByStage.Length == 0) return 1;
        return Mathf.Max(1, hitsByStage[Mathf.Clamp(stage, 0, hitsByStage.Length - 1)]);
    }

    public int Logs(int stage)
    {
        if (logsByStage == null || logsByStage.Length == 0) return 0;
        return Mathf.Max(0, logsByStage[Mathf.Clamp(stage, 0, logsByStage.Length - 1)]);
    }
}
