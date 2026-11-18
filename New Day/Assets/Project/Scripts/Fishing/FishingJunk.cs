using UnityEngine;

/// <summary>
/// One piece of junk that can be fished out of the water instead of a fish, and how common it is.
/// The list lives on the FishingManager ("Junk"). Junk is caught at once: there is no fight.
/// </summary>
[System.Serializable]
public class FishingJunk
{
    [Tooltip("The item put in the backpack (an old boot, a can, a trash bag...). Waste items can go to the treatment plant in the city.")]
    public ItemData item;
    [Min(0f)] [Tooltip("Bigger = this junk comes up more often than the others.")]
    public float weight = 10f;
}
