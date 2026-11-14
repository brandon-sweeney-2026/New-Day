using UnityEngine;

/// <summary>
/// A bait. Point "Bait Item" at its ItemData (a Resource), list the fish it attracts, and add it to the FishingManager's list.
/// One bait is used up each cast and makes the listed fish more likely to bite in water where they live.
/// </summary>
[CreateAssetMenu(fileName = "NewBait", menuName = "Farm/Bait")]
public class BaitData : ScriptableObject
{
    public ItemData baitItem;
    public FishData[] attracts;
    [Min(1f)] [Tooltip("How many times more likely the listed fish become.")] public float boost = 3f;
}
