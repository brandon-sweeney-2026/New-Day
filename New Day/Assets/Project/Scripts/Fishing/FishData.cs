using UnityEngine;

/// <summary>
/// One kind of fish and how hard it is to land. The fishing minigame builds its note track from these numbers.
///
/// "Note Count" is the length of the fight: an easy fish has 10 notes, the hardest has 100.
/// Harder fish also scroll faster, have notes closer together and use more of the six buttons.
/// </summary>
[CreateAssetMenu(fileName = "NewFish", menuName = "Farm/Fish")]
public class FishData : ScriptableObject
{
    [Header("Basics")]
    public string fishName = "Fish";
    [Tooltip("What goes into the player's inventory when caught (its icon is also the fish picture in the minigame).")]
    public ItemData item;
    [Range(1, 5)] [Tooltip("Shown as stars. Rod luck and the Lucky Angler talent make 4-5 star fish more likely.")]
    public int stars = 1;
    [TextArea] public string description;

    [Header("The fight")]
    [Range(10, 100)] [Tooltip("How many notes (button presses) the fight has. Easy = 10, hard = up to 100.")]
    public int noteCount = 10;
    [Tooltip("How fast the notes travel across the screen (pixels per second). Higher = harder.")]
    public float scrollSpeed = 420f;
    [Tooltip("Average notes per second. Higher = harder.")]
    public float notesPerSecond = 1.3f;
    [Range(2, 6)] [Tooltip("How many buttons it uses. 2 = Left/Right, 4 = adds Up/Down, 6 = adds Action 1 and Action 2.")]
    public int buttonsUsed = 2;

    [Header("Stamina (the catch meter)")]
    [Range(0f, 90f)] [Tooltip("Where the meter starts. It fills to 100 to catch the fish and empties to 0 when the fish escapes.")]
    public float startStamina = 30f;
    [Range(0.4f, 1f)] [Tooltip("Fraction of the notes you need to hit to reach 100. 0.75 = you can miss about 1 in 4.")]
    public float requiredHitRatio = 0.75f;
    [Tooltip("A miss takes away this multiple of what a hit gives.")]
    public float missPenalty = 1f;

    [Header("Where and when")]
    [Tooltip("Rarity weight when a water area doesn't set its own. Bigger = more common.")]
    public float defaultWeight = 10f;
    [Range(0, 24)] public float availableFromHour = 0f;
    [Range(0, 24)] [Tooltip("Set From later than To for night fish (e.g. 20 to 6).")] public float availableToHour = 24f;

    [Header("Reward")]
    [Tooltip("Fishing experience for catching it (earns talent points).")] public int xp = 5;

    public bool AvailableAt(float hour)
    {
        if (availableFromHour <= availableToHour) return hour >= availableFromHour && hour < availableToHour;
        return hour >= availableFromHour || hour < availableToHour;     // wraps past midnight
    }
}
