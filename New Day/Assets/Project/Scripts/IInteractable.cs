/// <summary>
/// Anything the player can use with the action button when standing in front of it:
/// a bed, a shipping bin, an NPC, a door, a chest...
/// The object also needs a Collider2D (a trigger is fine) so the player can "see" it.
/// </summary>
public interface IInteractable
{
    /// <summary>Text shown on screen, e.g. "Sleep" or "Talk to Mira". Return "" to show nothing.</summary>
    string InteractPrompt { get; }

    /// <summary>Called when the player presses the action button in front of this object.</summary>
    void Interact(PlayerActions player);
}

/// <summary>
/// Something the player can hit with an Axe or Pickaxe (trees, rocks, stumps). See Breakable.
/// </summary>
public interface IToolTarget
{
    /// <summary>Returns true if the tool did something (so the swing counts as used).</summary>
    bool OnToolUsed(ItemData.ToolAction tool, PlayerActions user);
}
