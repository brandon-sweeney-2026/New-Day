using UnityEngine;

/// <summary>
/// What one villager likes. Create one per NPC: right-click in the Project window > Create > Farm > NPC Profile.
/// Meatball reads these (drag them into his "Known Npcs" list) and tells the player about ONE villager per day.
/// The NPC batch will use the same asset for gift reactions.
/// </summary>
[CreateAssetMenu(fileName = "NewNpcProfile", menuName = "Farm/NPC Profile")]
public class NpcProfile : ScriptableObject
{
    public string npcName = "New NPC";
    public Sprite portrait;

    [Header("Gift taste")]
    public ItemData[] loves;
    public ItemData[] likes;
    public ItemData[] dislikes;

    [Header("Extra")]
    [Tooltip("Optional: a fun fact Meatball adds, e.g. 'She is up before sunrise every day.'")]
    [TextArea] public string extraNote;

    /// <summary>What Meatball says about this person.</summary>
    public string Describe()
    {
        string s = "";
        if (loves != null && loves.Length > 0) s += npcName + " loves " + Join(loves) + ". ";
        if (likes != null && likes.Length > 0) s += "They also like " + Join(likes) + ". ";
        if (dislikes != null && dislikes.Length > 0) s += "But " + Join(dislikes) + "? Not so much. ";
        if (s.Length == 0) s = "I haven't figured out what " + npcName + " likes yet. ";
        if (!string.IsNullOrEmpty(extraNote)) s += extraNote;
        return s.Trim();
    }

    private static string Join(ItemData[] items)
    {
        string s = "";
        int n = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            if (n > 0) s += ", ";
            s += items[i].itemName;
            n++;
        }
        return s;
    }
}
