using System;
using UnityEngine;

/// <summary>What a soil item does when the player uses it on a tile.</summary>
public enum SoilItemKind
{
    Sprinkler,      // stays on the tile and waters the tiles around it every morning
    MoistureGel,    // the tile waters itself for 'power' more mornings
    Fertilizer,     // the next crop harvested here is better: power 1 = Silver, 2 = Gold, 3 = Platinum
    Purifier,       // cleans pollution in a circle ('power' = how many tiles out)
    GrowthTonic     // the crop on this tile grows 'power' days at once
}

/// <summary>One usable soil item: which item it is, what it does and how strong it is.</summary>
[Serializable]
public class SoilItem
{
    public ItemData item;
    public SoilItemKind kind = SoilItemKind.MoistureGel;
    [Min(1), Tooltip("Sprinkler: 1 = the 4 tiles next to it, 2 = the 8 around it, 3 = a 5 x 5 square.\nMoisture gel: how many mornings it waters the tile.\n" +
                     "Fertilizer: 1 Silver, 2 Gold, 3 Platinum.\nPurifier: radius in tiles.\nGrowth tonic: days of growth.")]
    public int power = 1;
    [Tooltip("Sprinkler only: the picture that stands on the tile. Empty = the item's icon.")]
    public Sprite placedSprite;
}

/// <summary>
/// The numbers behind the things researched at the university (sprinklers, moisture gel, fertilizer...), crop quality,
/// and how dirty the river gets. One asset: Assets/Resources/SoilCareSettings.asset, made by Farm > Set Up > City Research and Items.
/// Without the asset the game still runs: soil items simply do nothing and the built-in numbers below are used.
/// </summary>
[CreateAssetMenu(fileName = "SoilCareSettings", menuName = "Farm/Soil Care Settings")]
public class SoilCareSettings : ScriptableObject
{
    [Header("Soil items (what each item does when used on a tile)")]
    public SoilItem[] items = new SoilItem[0];

    [Header("Crop quality: Bronze (ordinary), Silver, Gold, Platinum")]
    [Tooltip("The colour of the band around a crop's icon. Exactly 4: Bronze, Silver, Gold, Platinum.")]
    public Color[] qualityColors =
    {
        new Color(0.72f, 0.45f, 0.20f), new Color(0.80f, 0.84f, 0.88f), new Color(1.00f, 0.82f, 0.25f), new Color(0.55f, 0.95f, 1.00f)
    };
    [Tooltip("The sale price is multiplied by this. Exactly 4: Bronze, Silver, Gold, Platinum.")]
    public float[] qualityPriceMultipliers = { 1f, 1.25f, 1.5f, 2f };
    [Tooltip("Show the bronze band on ordinary crops too. Off = only Silver and better get a band.")]
    public bool showBronzeBand = true;

    [Header("River pollution (0 = clean ... 5 = filthy)")]
    [Min(1), Tooltip("How many 'dirt points' make one level of river pollution.")]
    public int pointsPerLevel = 12;
    [Min(0f), Tooltip("Dirt points for each item the PLAYER throws in the river.")]
    public float pointsPerItemDumped = 1f;
    [Min(0f), Tooltip("Dirt points the Sterling tower adds every day, all by itself.")]
    public float pointsPerDayFromTower = 0.5f;
    [Min(0f), Tooltip("Dirt points taken away for each item the player takes to the treatment plant.")]
    public float pointsCleanedPerItemTreated = 0.5f;
    [Min(0f), Tooltip("Dirt points taken away by each river improvement bought at City Hall (counter 'river_cleanups').")]
    public float pointsCleanedPerRiverImprovement = 18f;
    [Min(0), Tooltip("Farm: how many rows of ground nearest the river (the top of the map) get polluted.")]
    public int farmRowsReached = 3;
    [Min(0), Tooltip("Forest: how many polluted patches appear per pollution level.")]
    public int forestPatchesPerLevel = 4;

    private static SoilCareSettings current;

    /// <summary>The settings in use: the asset in Resources, or built-in defaults if there is none.</summary>
    public static SoilCareSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<SoilCareSettings>("SoilCareSettings");
            if (current == null)
            {
                current = CreateInstance<SoilCareSettings>();
                current.hideFlags = HideFlags.DontSave;
            }
            return current;
        }
    }

    /// <summary>What this item does on a tile, or null if it is not a soil item.</summary>
    public SoilItem Find(ItemData item)
    {
        if (item == null || items == null) return null;
        foreach (SoilItem s in items) if (s != null && s.item == item) return s;
        return null;
    }
}
