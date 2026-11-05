using UnityEngine;

/// <summary>
/// Defines one kind of crop: how long it takes to grow and what it gives you.
/// Create instances in the editor: right-click in Project window > Create > Farm > Crop.
/// </summary>
[CreateAssetMenu(fileName = "NewCrop", menuName = "Farm/Crop")]
public class CropData : ScriptableObject
{
    [Header("Basics")]
    public string cropName = "New Crop";

    [Header("Items")]
    [Tooltip("The seed item the player plants.")]
    public ItemData seedItem;
    [Tooltip("The item the player receives when harvesting.")]
    public ItemData harvestItem;
    [Min(1)] public int harvestAmount = 1;

    [Header("Growth")]
    [Tooltip("One sprite per growth stage, in order. The last sprite is the fully grown crop.")]
    public Sprite[] growthStageSprites;
    [Tooltip("In-game days spent in each stage before advancing to the next.")]
    [Min(1)] public int daysPerStage = 1;
    [Tooltip("If true, the crop must be watered each day to grow.")]
    public bool needsWater = true;

    /// <summary>Total days from planting until the crop can be harvested.</summary>
    public int DaysToMature => Mathf.Max(0, growthStageSprites.Length - 1) * daysPerStage;

    public bool IsFullyGrown(int daysGrown) => daysGrown >= DaysToMature;

    /// <summary>Returns the sprite to display for a crop that has grown for this many days.</summary>
    public Sprite GetSpriteForDays(int daysGrown)
    {
        if (growthStageSprites == null || growthStageSprites.Length == 0) return null;
        int stage = Mathf.Min(daysGrown / daysPerStage, growthStageSprites.Length - 1);
        return growthStageSprites[stage];
    }
}
