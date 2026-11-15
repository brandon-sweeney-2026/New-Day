using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Farm > Set Up > Crop Seasons
///   1. gives every crop's SEED its growing seasons (the table below). Seeds whose season boxes you already changed are left alone;
///   2. removes the old shop stall ("GeneralStore") from the open scene, because seeds are now bought at the General Store in the suburbs.
/// Farm > Test and Debug > Reset Crop Seasons To Defaults puts every seed back to the table.
///
/// The seasons live on the seed ITEM (Plant In Spring / Summer / Autumn / Winter), not on the crop, so the shop,
/// planting and growing all read the same boxes. Nothing grows in winter unless you tick Plant In Winter on a seed.
/// To change a crop's seasons, select its seed item and tick the boxes - or edit the table here and use the reset menu.
/// Must live in a folder named "Editor".
/// </summary>
public static class CropLoopSetupMenu
{
    /// <summary>crop name (lower case) -> spring, summer, autumn</summary>
    private static readonly Dictionary<string, bool[]> Seasons = new Dictionary<string, bool[]>
    {
        { "turnip",     new[] { true,  false, false } },
        { "potato",     new[] { true,  false, false } },
        { "strawberry", new[] { true,  true,  false } },
        { "carrot",     new[] { true,  false, true  } },
        { "tomato",     new[] { false, true,  false } },
        { "corn",       new[] { false, true,  true  } },
        { "wheat",      new[] { false, true,  true  } },
        { "pumpkin",    new[] { false, false, true  } },
    };

    [MenuItem("Farm/Set Up/Crop Seasons", false, 106)]
    public static void Run()
    {
        var log = new List<string>();
        ApplySeasons(false, log);
        RemoveFarmShop(true, log);
        EditorUtility.DisplayDialog("Crop seasons", string.Join("\n", log.ToArray()) +
            "\n\nSeeds are now sold only in the seasons they grow in, at the General Store's seed rack and checkout." +
            "\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    [MenuItem("Farm/Test and Debug/Reset Crop Seasons To Defaults", false, 326)]
    public static void ResetSeasons()
    {
        if (!EditorUtility.DisplayDialog("Reset crop seasons", "Put every crop seed's seasons back to the defaults (turnip and potato in spring, tomato in summer, pumpkin in autumn, and so on)?", "Reset", "Cancel")) return;
        var log = new List<string>();
        ApplySeasons(true, log);
        EditorUtility.DisplayDialog("Crop seasons", string.Join("\n", log.ToArray()), "OK");
    }

    /// <summary>Command line only: applies the seasons, then opens the Farm scene, removes the stall and saves.</summary>
    public static void RunBatch()
    {
        var log = new List<string>();
        ApplySeasons(false, log);
        Scene farm = EditorSceneManager.OpenScene("Assets/Project/Scenes/Farm.unity", OpenSceneMode.Single);
        if (RemoveFarmShop(false, log)) EditorSceneManager.SaveScene(farm);
        Debug.Log("CROP LOOP SETUP: " + string.Join(" | ", log.ToArray()));
    }

    private static void ApplySeasons(bool overwrite, List<string> log)
    {
        int set = 0, kept = 0, unknown = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (crop == null || crop.seedItem == null) continue;
            ItemData seed = crop.seedItem;

            bool[] s;
            if (!Seasons.TryGetValue(crop.cropName.Trim().ToLowerInvariant(), out s)) { unknown++; continue; }

            // "untouched" = still the way a new item starts: spring, summer and autumn ticked, winter not
            bool untouched = seed.plantInSpring && seed.plantInSummer && seed.plantInAutumn && !seed.plantInWinter;
            if (!overwrite && !untouched) { kept++; continue; }

            seed.plantInSpring = s[0]; seed.plantInSummer = s[1]; seed.plantInAutumn = s[2]; seed.plantInWinter = false;
            EditorUtility.SetDirty(seed);
            set++;
        }
        AssetDatabase.SaveAssets();
        log.Add("Seasons set on " + set + " seed(s)." + (kept > 0 ? " " + kept + " you had changed were left alone." : "") +
                (unknown > 0 ? " " + unknown + " crop(s) are not in the table, so they still grow spring to autumn." : ""));
    }

    /// <summary>Deletes the farm's shop stall from the open scene. Returns true if it removed one.</summary>
    private static bool RemoveFarmShop(bool withUndo, List<string> log)
    {
        GameObject stall = GameObject.Find("GeneralStore");
        if (stall == null || stall.GetComponent<ShopCounter>() == null)
        {
            log.Add("No shop stall ('GeneralStore') in this scene, so nothing was removed. (Open the Farm scene and run this again if it is still there.)");
            return false;
        }
        Scene scene = stall.scene;
        if (withUndo) Undo.DestroyObjectImmediate(stall); else Object.DestroyImmediate(stall);
        EditorSceneManager.MarkSceneDirty(scene);
        log.Add("Removed the shop stall ('GeneralStore') from the " + scene.name + " scene. Save the scene (Ctrl+S).");
        return true;
    }
}
