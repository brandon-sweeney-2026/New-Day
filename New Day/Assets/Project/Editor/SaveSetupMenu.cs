using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Editor side of the save system.
///
///  - Keeps Assets/Resources/SaveCatalog.asset (the list of every item, so a save file can find items by name)
///    up to date. It refreshes itself every time you press Play and before every build, so you normally never think about it.
///  - Farm > Set Up > Save System: refreshes the catalog now and says how many items it found.
///  - Farm > Test and Debug > Open Save Folder / Delete All Saves / Skip Title Screen When Testing.
/// </summary>
[InitializeOnLoad]
public class SaveSetupMenu : IPreprocessBuildWithReport
{
    private const string CatalogPath = "Assets/Resources/SaveCatalog.asset";
    private const string SkipTitlePath = "Farm/Test and Debug/Skip Title Screen When Testing";

    static SaveSetupMenu()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        // Just before Play starts: make sure every item can be found by the save system.
        if (change == PlayModeStateChange.ExitingEditMode) RefreshCatalog();
    }

    // Before a build, for the same reason.
    public int callbackOrder { get { return 0; } }
    public void OnPreprocessBuild(BuildReport report) { RefreshCatalog(); }

    // ------------------------------------------------------------------
    // Menu items
    // ------------------------------------------------------------------

    [MenuItem("Farm/Set Up/Save System", false, 108)]
    private static void SetUpMenu()
    {
        EditorUtility.DisplayDialog("Save System", Run(), "OK");
    }

    /// <summary>Refreshes the catalog. Returns a sentence saying what was done (no pop-up).</summary>
    public static string Run()
    {
        int count = RefreshCatalog();
        return "Made the save catalog (" + CatalogPath + ") with " + count + " items.\n\n" +
               "The title screen appears when the game starts and the game saves itself at the end of every day. " +
               "Save files are in: " + Application.persistentDataPath;
    }

    [MenuItem("Farm/Test and Debug/Open Save Folder", false, 330)]
    private static void OpenSaveFolder()
    {
        string folder = Application.persistentDataPath;
        Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }

    [MenuItem("Farm/Test and Debug/Delete All Saves", false, 331)]
    private static void DeleteAllSaves()
    {
        if (!EditorUtility.DisplayDialog("Delete All Saves",
                "Delete all " + SaveSystem.SlotCount + " save slots? This can't be undone.", "Delete", "Cancel")) return;
        for (int i = 0; i < SaveSystem.SlotCount; i++) SaveSystem.DeleteSlot(i);
        Debug.Log("Save: deleted every save slot in " + Application.persistentDataPath);
    }

    [MenuItem(SkipTitlePath, false, 332)]
    private static void ToggleSkipTitle()
    {
        bool skip = PlayerPrefs.GetInt(TitleScreen.SkipInEditorKey, 0) == 1;
        PlayerPrefs.SetInt(TitleScreen.SkipInEditorKey, skip ? 0 : 1);
        PlayerPrefs.Save();
        Debug.Log(skip ? "The title screen will show when you press Play." : "The title screen is skipped when you press Play in the Editor (a built game always shows it).");
    }

    [MenuItem(SkipTitlePath, true)]
    private static bool ToggleSkipTitleCheck()
    {
        Menu.SetChecked(SkipTitlePath, PlayerPrefs.GetInt(TitleScreen.SkipInEditorKey, 0) == 1);
        return true;
    }

    // ------------------------------------------------------------------
    // The catalog
    // ------------------------------------------------------------------

    /// <summary>Puts every ItemData asset in the project into the catalog. Returns how many there are.</summary>
    public static int RefreshCatalog()
    {
        var items = new List<ItemData>();
        var seen = new Dictionary<string, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null) continue;

            // Saves find items by their asset name, so two items with the same file name would be mixed up.
            string other;
            if (seen.TryGetValue(item.name, out other))
                Debug.LogWarning("Save: two items are both called '" + item.name + "' (" + other + " and " + path + "). " +
                                 "A save file can't tell them apart; rename one of them.");
            else seen[item.name] = path;

            items.Add(item);
        }
        items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        SaveCatalog catalog = AssetDatabase.LoadAssetAtPath<SaveCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SaveCatalog>();
            catalog.items = items.ToArray();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return items.Count;
        }

        // Only touch the asset when something changed, so pressing Play does not mark it as modified every time.
        if (!SameItems(catalog.items, items))
        {
            catalog.items = items.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
        return items.Count;
    }

    private static bool SameItems(ItemData[] current, List<ItemData> wanted)
    {
        if (current == null || current.Length != wanted.Count) return false;
        for (int i = 0; i < current.Length; i++) if (current[i] != wanted[i]) return false;
        return true;
    }
}
