using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>What part of the house an upgrade changes.</summary>
public enum HouseCategory
{
    Floor, Wall,                       // inside
    Siding, Roof,                      // outside colours
    Bed, Kitchen, Table, Rug, Bookshelf, Plant, Lamp, WallArt,  // furniture   (Kitchen = the stove / range)
    // ---- added by Suburb Pack C (Frank's Home & Hardware). New entries go at the END so your saved assets keep working. ----
    Door,            // outside: front door colour
    Shutters,        // outside: shutters beside the big windows
    Cabinets,        // inside: the kitchen cabinets on the wall
    WindowStyle,     // inside: what the windows look like
    WindowLayout     // inside: where the windows are on the back wall
}

/// <summary>One window position on the back wall. Used by WindowLayout options.</summary>
[Serializable]
public class WindowSpot
{
    [Tooltip("Where along the back wall: 0 = far left, 1 = far right.")]
    [Range(0f, 1f)] public float along = 0.5f;
    [Tooltip("Move the window up (+) or down (-), in tiles.")]
    public float lift = 0f;
    [Tooltip("Mirror the picture.")]
    public bool flip = false;
}

[Serializable]
public class MaterialCost
{
    public ItemData item;
    [Min(1)] public int count = 1;
}

/// <summary>
/// One purchasable house option (a floor, a wallpaper, a roof colour, a piece of furniture).
/// Create more in the Project window: right-click > Create > Farm > House Upgrade, then add it to the
/// HouseManager's Catalog list. Everything - price, materials, look - is editable here.
/// </summary>
[CreateAssetMenu(fileName = "NewHouseUpgrade", menuName = "Farm/House Upgrade")]
public class HouseUpgradeData : ScriptableObject
{
    [Header("Basics")]
    public string displayName = "New Option";
    [TextArea] public string description;
    public HouseCategory category = HouseCategory.Rug;
    [Tooltip("The option the house starts with (free and already owned). Exactly one per category.")]
    public bool isDefault;

    [Header("Price")]
    [Min(0)] public int goldCost = 100;
    [Tooltip("Materials used up when buying. Buy them at the General Store or collect them.")]
    public List<MaterialCost> materials = new List<MaterialCost>();

    [Header("Look")]
    [Tooltip("Furniture: the picture. Leave empty for 'nothing here'.")]
    public Sprite sprite;
    [Tooltip("Floor / Wall: the tile. Wall: 'Tile' is the upper row, 'Tile Base' the row with the skirting board.")]
    public TileBase tile;
    public TileBase tileBase;
    [Tooltip("Siding / Roof: the colour the outside is painted.")]
    public Color tint = Color.white;

    [Header("Windows (WindowLayout options only)")]
    [Tooltip("Where the windows go. Add as many as you like. The picture used comes from the WindowStyle you own.")]
    public WindowSpot[] windowSpots = new WindowSpot[0];

    [Header("Effects")]
    [Tooltip("Kitchen only: chance (0-1) that cooking makes one extra serving.")]
    [Range(0f, 1f)] public float foodBonus = 0f;

    /// <summary>"150g + 8 Wood Plank" style text.</summary>
    public string CostText()
    {
        var parts = new List<string>();
        if (goldCost > 0) parts.Add(goldCost + "g");
        foreach (MaterialCost m in materials)
            if (m != null && m.item != null) parts.Add(m.count + " " + m.item.itemName);
        return parts.Count == 0 ? "Free" : string.Join(" + ", parts);
    }
}
