using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The list of every decor piece the Interior Decorator offers (Assets/Farm/Interiors/DecorCatalog.asset).
/// Farm > Set Up > Interior Decor fills it; add your own DecorItem assets to 'Items' to see them in the decorator too.
/// </summary>
[CreateAssetMenu(fileName = "DecorCatalog", menuName = "Farm/Decor Catalog")]
public class DecorCatalog : ScriptableObject
{
    public static readonly string[] Themes = { "cottage", "modern", "rustic", "retro", "coastal" };

    public List<DecorItem> items = new List<DecorItem>();

    /// <summary>Every item in one category (in list order).</summary>
    public List<DecorItem> InCategory(DecorCategory category)
    {
        var list = new List<DecorItem>();
        foreach (DecorItem i in items) if (i != null && i.category == category) list.Add(i);
        return list;
    }

    /// <summary>The item with this id, or null.</summary>
    public DecorItem ById(string id)
    {
        foreach (DecorItem i in items) if (i != null && i.id == id) return i;
        return null;
    }

    /// <summary>The piece of a category in a theme (falls back to an "any" piece, then the first one).</summary>
    public DecorItem Pick(DecorCategory category, string theme)
    {
        DecorItem any = null, first = null;
        foreach (DecorItem i in items)
        {
            if (i == null || i.category != category) continue;
            if (i.theme == theme) return i;
            if (first == null) first = i;
            if (any == null && i.theme == "any") any = i;
        }
        return any != null ? any : first;
    }
}
