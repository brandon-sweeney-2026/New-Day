using UnityEngine;

/// <summary>
/// A sprinkler standing on a farm tile. SoilCare makes these; press the action button in front of one to pick it back up.
/// </summary>
public class PlacedSprinkler : MonoBehaviour, IInteractable
{
    [HideInInspector] public Vector3Int cell;
    [HideInInspector] public string itemName = "sprinkler";

    public string InteractPrompt { get { return "Pick up " + itemName; } }

    public void Interact(PlayerActions player)
    {
        if (SoilCare.Instance != null && player != null) SoilCare.Instance.PickUpSprinkler(cell, player.Inventory);
    }
}
