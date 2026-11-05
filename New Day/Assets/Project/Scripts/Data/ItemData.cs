using UnityEngine;

/// <summary>
/// Defines one kind of item (seeds, crops, tools, resources).
/// Create instances in the editor: right-click in Project window > Create > Farm > Item.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Farm/Item")]
public class ItemData : ScriptableObject
{
    public enum ItemType { Seed, Crop, Tool, Resource, Food }

    [Header("Basics")]
    public string itemName = "New Item";
    [TextArea] public string description;
    public Sprite icon;
    public ItemType type = ItemType.Resource;

    [Header("Inventory")]
    [Min(1)] public int maxStack = 99;

    [Header("Economy")]
    [Min(0)] public int sellPrice = 10;
    [Min(0)] public int buyPrice = 0; // 0 = not sold in the shop

    [Header("Food (only used when type is Food)")]
    [Min(0)] public int energyRestored = 0;
}
