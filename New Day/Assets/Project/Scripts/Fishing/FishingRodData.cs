using UnityEngine;

/// <summary>
/// The fishing bonuses of a rod. Make one per rod, point "Rod Item" at the rod's ItemData (Type Tool, Tool Action Fishing Rod),
/// and add it to the FishingManager's list. Better rods = bigger numbers. Talents add on top of these.
/// </summary>
[CreateAssetMenu(fileName = "NewRod", menuName = "Farm/Fishing Rod")]
public class FishingRodData : ScriptableObject
{
    public ItemData rodItem;
    [Range(0f, 0.6f)] [Tooltip("Slows the notes by this fraction (0.2 = 20% slower).")] public float slowTime = 0f;
    [Min(0)] [Tooltip("Misses that cost nothing each fish.")] public int extraMisses = 0;
    [Range(0f, 0.15f)] [Tooltip("Seconds added to the hit window (makes timing easier).")] public float hitWindowBonus = 0f;
    [Range(0f, 1f)] [Tooltip("Each hit fills the stamina meter this much more (0.2 = 20% more).")] public float staminaGain = 0f;
    [Range(0f, 1f)] [Tooltip("Makes rare (4-5 star) fish more likely.")] public float luck = 0f;
    [Range(0f, 0.6f)] [Tooltip("Fish bite sooner.")] public float biteSpeed = 0f;
}
