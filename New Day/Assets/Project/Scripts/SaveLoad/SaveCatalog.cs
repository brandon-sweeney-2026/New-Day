using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A list of every item in the game, so a save file can find an item again from its name.
///
/// A save file is only text, so it stores "Turnip" rather than a link to the Turnip asset.
/// When a save is loaded this list turns the name back into the real item.
///
/// One asset: Assets/Resources/SaveCatalog.asset. You never fill it in by hand: it is rebuilt
/// every time you press Play and before every build (see SaveSetupMenu), and by Farm > Set Up > Save System.
/// </summary>
public class SaveCatalog : ScriptableObject
{
    [Tooltip("Every ItemData asset in the project. Filled in automatically.")]
    public ItemData[] items = new ItemData[0];

    private static SaveCatalog current;
    [System.NonSerialized] private Dictionary<string, ItemData> byName;     // built the first time an item is looked up

    // The list may have been refreshed by the editor; look it up again next time.
    private void OnEnable() { byName = null; }
    private void OnValidate() { byName = null; }

    /// <summary>The catalog in Resources, or an empty one if it has not been made yet.</summary>
    public static SaveCatalog Current
    {
        get
        {
            if (current == null) current = Resources.Load<SaveCatalog>("SaveCatalog");
            if (current == null)
            {
                current = CreateInstance<SaveCatalog>();
                current.hideFlags = HideFlags.DontSave;
            }
            return current;
        }
    }

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so forget the old catalog.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        if (current != null) current.byName = null;
        current = null;
    }

    /// <summary>The item whose asset is called 'assetName', or null if there is no such item any more.</summary>
    public ItemData FindItem(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;

        if (byName == null)
        {
            byName = new Dictionary<string, ItemData>();
            foreach (ItemData item in items)
                if (item != null) byName[item.name] = item;
        }

        ItemData found;
        if (byName.TryGetValue(assetName, out found)) return found;

        // Not in the list (the catalog is out of date?). Look through the items Unity happens to have in memory.
        foreach (ItemData item in Resources.FindObjectsOfTypeAll<ItemData>())
        {
            if (item.name != assetName) continue;
            byName[assetName] = item;
            return item;
        }

        Debug.LogWarning("Save: could not find an item called '" + assetName + "'. It was skipped. " +
                         "If the item still exists, run Farm > Set Up > Save System to refresh the catalog.");
        return null;
    }
}
