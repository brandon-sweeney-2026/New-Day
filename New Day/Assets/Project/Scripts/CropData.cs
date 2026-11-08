using UnityEngine;

/// <summary>
/// Defines one kind of crop: how long it takes to grow and what it gives you.
/// Create instances in the editor: right-click in Project window > Create > Farm > Crop.
///
/// UPDATED: added pollutionSensitivity.
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

    [Header("Pollution")]
    [Tooltip("How strongly this crop reacts to pollution. 0 = immune, 1 = normal, 2 = very sensitive. " +
             "The tile's pollution level (0-5) is multiplied by this before the effects are applied.")]
    [Range(0f, 2f)] public float pollutionSensitivity = 1f;

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

    /// <summary>Converts the pollution level on the ground (0-5) into the level this crop actually feels.</summary>
    public int EffectiveLevel(float tileLevel)
    {
        return Mathf.Clamp(Mathf.RoundToInt(tileLevel * pollutionSensitivity), 0, PollutionSettings.MaxLevel);
    }
}
