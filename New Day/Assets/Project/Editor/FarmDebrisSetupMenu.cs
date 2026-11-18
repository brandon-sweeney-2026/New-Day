using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Farm Debris: everything the random farm clutter needs.
///   1. Imports the art drawn by Tools/make_farm_debris_art.ps1 (Assets/Project/Art/FarmDebris).
///   2. Makes the items in Assets/Farm/Data/FarmDebris: Fiber (from weeds) and four waste items (Crushed Can, Debris, Tar Lump, Toxic Goo).
///   3. Makes the prefabs in Assets/Farm/FarmDebris/Prefabs: four litter pieces, three weeds and a fallen tree.
///      Stones reuse the mountain rocks (Assets/Project/Art/Mountain/Prefabs), with coal and copper now and then.
///   4. Makes Assets/Resources/FarmDebrisSettings.asset with the starting numbers in the tables below.
///
/// Safe to run again: items, prefabs and the settings asset that already exist are KEPT (so your Inspector changes stay).
/// Delete one to get the starting version back. Kinds or groups added to the tables below are added to an existing settings asset.
/// No scene changes are needed: the spawner creates itself in the Farm scene when you press Play.
/// </summary>
public static class FarmDebrisSetupMenu
{
    private const string ArtFolder = "Assets/Project/Art/FarmDebris";
    private const string ItemFolder = "Assets/Farm/Data/FarmDebris";
    private const string PrefabFolder = "Assets/Farm/FarmDebris/Prefabs";
    private const string MountainPrefabs = "Assets/Project/Art/Mountain/Prefabs";
    private const string SettingsPath = "Assets/Resources/FarmDebrisSettings.asset";
    private const string LitMaterialPath = "Assets/Resources/Lighting/SpriteLit.mat";

    // ---------------------------------------------------------------- starting tables (edit these, then delete the asset and run again)

    // items: asset name, name in game, description, sell price, is waste, icon picture
    private static readonly object[][] Items =
    {
        new object[] { "Fiber",       "Fiber",       "Tough plant strands from cut weeds.",                                         2,  false, "icon_fiber" },
        new object[] { "CrushedCan",  "Crushed Can", "Somebody's empty can. It belongs at the treatment plant.",                    5,  true,  "litter_can" },
        new object[] { "Debris",      "Debris",      "Splintered wood and bent metal. It belongs at the treatment plant.",          8,  true,  "litter_debris" },
        new object[] { "TarLump",     "Tar Lump",    "A sticky black lump. It stains the soil it lies on.",                         12, true,  "litter_tar" },
        new object[] { "ToxicGoo",    "Toxic Goo",   "A purple lump that glows a little. It poisons the soil it lies on.",          20, true,  "litter_goo" },
    };

    // litter: prefab name, picture, item it gives, how much it pollutes its tile (0-5), kind id, weight
    private static readonly object[][] Litter =
    {
        new object[] { "Litter_Can",    "litter_can",    "CrushedCan", 0, "can",    4f },
        new object[] { "Litter_Debris", "litter_debris", "Debris",     0, "debris", 3f },
        new object[] { "Litter_TarLump","litter_tar",    "TarLump",    1, "tar",    2f },
        new object[] { "Litter_ToxicGoo","litter_goo",   "ToxicGoo",   2, "goo",    1f },
    };

    // weeds: prefab name, picture, kind id, weight
    private static readonly object[][] Weeds =
    {
        new object[] { "Weed_Tuft",      "weed_a", "weed_tuft",      5f },
        new object[] { "Weed_Grass",     "weed_b", "weed_grass",     3f },
        new object[] { "Weed_Dandelion", "weed_c", "weed_dandelion", 2f },
    };

    // stones from the mountain trail: prefab name, kind id, weight (ore is rare)
    private static readonly object[][] Stones =
    {
        new object[] { "Rock_Grey_Small",  "rock_grey",  5f },
        new object[] { "Rock_Brown_Small", "rock_brown", 5f },
        new object[] { "Ore_Coal",         "ore_coal",   0.6f },
        new object[] { "Ore_Copper",       "ore_copper", 0.4f },
    };

    // group: start count, per day, most on the farm, spring, summer, autumn, winter
    private static readonly object[][] Rules =
    {
        new object[] { FarmDebrisSettings.Group.Pollution,  1,  0.15f, 12, true, true, true, true  },
        new object[] { FarmDebrisSettings.Group.Weed,       12, 2.5f,  40, true, true, true, false },
        new object[] { FarmDebrisSettings.Group.Stone,      8,  1f,    20, true, true, true, true  },
        new object[] { FarmDebrisSettings.Group.FallenTree, 2,  0.2f,  5,  true, true, true, true  },
    };

    // fallen tree: hits with the axe, logs dropped (least, most)
    private const int TreeHits = 4, TreeLogsMin = 3, TreeLogsMax = 5;

    // ---------------------------------------------------------------- menu
    [MenuItem("Farm/Set Up/Farm Debris", false, 126)]
    private static void Menu()
    {
        string result = Run();
        EditorUtility.DisplayDialog("Farm Debris", result, "OK");
    }

    /// <summary>Does the whole set up with no pop-ups. Returns a sentence saying what happened.</summary>
    public static string Run()
    {
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder(ArtFolder) || AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/weed_a.png") == null)
            return "No art found in " + ArtFolder + ". Run Tools/make_farm_debris_art.ps1 first, then this menu again.";

        HardwareEditorUtil.EnsureFolder(ItemFolder);
        HardwareEditorUtil.EnsureFolder(PrefabFolder);
        HardwareEditorUtil.EnsureFolder("Assets/Resources");

        // 1. art
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool icon = path.Contains("/icon_");
            HardwareEditorUtil.ImportSingle(path, icon ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        }

        // 2. items
        var items = new Dictionary<string, ItemData>();
        int madeItems = 0;
        foreach (object[] row in Items)
        {
            string assetName = (string)row[0];
            string path = ItemFolder + "/" + assetName + ".asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<ItemData>(path) == null;
            ItemData item = HardwareEditorUtil.LoadOrCreate<ItemData>(path);
            if (isNew)
            {
                item.itemName = (string)row[1];
                item.description = (string)row[2];
                item.sellPrice = (int)row[3];
                item.isWaste = (bool)row[4];
                item.type = ItemData.ItemType.Resource;
                item.maxStack = 99;
                madeItems++;
            }
            if (item.icon == null) item.icon = HardwareEditorUtil.Spr(ArtFolder, (string)row[5]);
            EditorUtility.SetDirty(item);
            items[assetName] = item;
        }

        ItemData log = HardwareEditorUtil.FindItem("Log");
        if (log == null) Debug.LogWarning("Farm Debris: no 'Log' item found, so fallen trees drop nothing. Set the drop on the FallenTree prefab.");

        // 3. prefabs
        var kinds = new List<FarmDebrisSettings.Kind>();
        int madePrefabs = 0;

        foreach (object[] row in Litter)
        {
            string name = (string)row[0];
            ItemData gives = items[(string)row[2]];
            int pollutes = (int)row[3];
            GameObject prefab = MakePrefab(name, (string)row[1], 4, ref madePrefabs, go =>
            {
                var box = go.AddComponent<BoxCollider2D>();          // the collider must come first: LitterPickup requires one
                box.isTrigger = true; box.size = new Vector2(0.8f, 0.5f); box.offset = new Vector2(0f, 0.25f);
                var pick = go.AddComponent<LitterPickup>();
                var so = new SerializedObject(pick);
                so.FindProperty("item").objectReferenceValue = gives;
                so.FindProperty("pollutesTile").intValue = pollutes;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            kinds.Add(new FarmDebrisSettings.Kind { id = (string)row[4], group = FarmDebrisSettings.Group.Pollution, prefab = prefab, weight = (float)row[5] });
        }

        foreach (object[] row in Weeds)
        {
            GameObject prefab = MakePrefab((string)row[0], (string)row[1], 6, ref madePrefabs, go =>
            {
                var circle = go.AddComponent<CircleCollider2D>();    // a trigger: you walk through weeds
                circle.isTrigger = true; circle.radius = 0.35f; circle.offset = new Vector2(0f, 0.35f);
                SetupBreakable(go.AddComponent<Breakable>(), ItemData.ToolAction.Sickle, 1, items["Fiber"], 0, 1);
            });
            kinds.Add(new FarmDebrisSettings.Kind { id = (string)row[2], group = FarmDebrisSettings.Group.Weed, prefab = prefab, weight = (float)row[3] });
        }

        GameObject tree = MakePrefab("FallenTree", "fallen_tree", 9, ref madePrefabs, go =>
        {
            var box = go.AddComponent<BoxCollider2D>();              // solid: you walk around a fallen tree
            box.size = new Vector2(1.75f, 0.4f); box.offset = new Vector2(0f, 0.3f);
            SetupBreakable(go.AddComponent<Breakable>(), ItemData.ToolAction.Axe, TreeHits, log, TreeLogsMin, TreeLogsMax);
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.3f;
        });
        kinds.Add(new FarmDebrisSettings.Kind { id = "fallen_tree", group = FarmDebrisSettings.Group.FallenTree, prefab = tree, weight = 1f });

        int stonesFound = 0;
        foreach (object[] row in Stones)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPrefabs + "/" + (string)row[0] + ".prefab");
            if (prefab == null) { Debug.LogWarning("Farm Debris: stone prefab " + row[0] + " not found in " + MountainPrefabs + " (run Farm > Set Up > Mountain Trail)."); continue; }
            stonesFound++;
            kinds.Add(new FarmDebrisSettings.Kind { id = (string)row[1], group = FarmDebrisSettings.Group.Stone, prefab = prefab, weight = (float)row[2] });
        }

        // 4. settings (kept if it exists; only missing kinds and groups are added)
        bool newSettings = AssetDatabase.LoadAssetAtPath<FarmDebrisSettings>(SettingsPath) == null;
        var settings = HardwareEditorUtil.LoadOrCreate<FarmDebrisSettings>(SettingsPath);
        int addedKinds = 0;
        foreach (FarmDebrisSettings.Kind k in kinds)
        {
            FarmDebrisSettings.Kind have = settings.KindById(k.id);
            if (have == null) { settings.kinds.Add(k); addedKinds++; }
            else if (have.prefab == null) have.prefab = k.prefab;
        }
        foreach (object[] row in Rules)
        {
            var g = (FarmDebrisSettings.Group)row[0];
            if (settings.RuleFor(g) != null) continue;
            settings.rules.Add(new FarmDebrisSettings.GroupRule
            {
                group = g, startCount = (int)row[1], perDay = (float)row[2], maxOnFarm = (int)row[3],
                spring = (bool)row[4], summer = (bool)row[5], autumn = (bool)row[6], winter = (bool)row[7]
            });
        }
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        return "Made " + madeItems + " new items and " + madePrefabs + " new prefabs (" + stonesFound + " stone kinds from the mountain), "
             + (newSettings ? "created " : "kept ") + SettingsPath + (addedKinds > 0 && !newSettings ? " and added " + addedKinds + " new kinds to it" : "")
             + ". Press Play in the Farm scene: weeds, stones, fallen trees and a little litter appear by themselves.";
    }

    // ---------------------------------------------------------------- helpers
    /// <summary>Makes a prefab with a lit SpriteRenderer, unless one with that name already exists (then it is kept as it is).</summary>
    private static GameObject MakePrefab(string name, string picture, int sortingOrder, ref int made, System.Action<GameObject> addParts)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = HardwareEditorUtil.Spr(ArtFolder, picture);
        sr.sortingOrder = sortingOrder;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        if (lit != null) sr.sharedMaterial = lit;          // goes dark at night like everything else

        addParts(go);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        made++;
        return prefab;
    }

    /// <summary>Breakable's fields are private, so they are set the way the Inspector would set them.</summary>
    private static void SetupBreakable(Breakable b, ItemData.ToolAction tool, int hits, ItemData drop, int dropMin, int dropMax)
    {
        var so = new SerializedObject(b);
        so.FindProperty("requiredTool").intValue = (int)tool;
        so.FindProperty("hits").intValue = hits;
        so.FindProperty("drop").objectReferenceValue = drop;
        so.FindProperty("dropMin").intValue = dropMin;
        so.FindProperty("dropMax").intValue = dropMax;
        so.FindProperty("destroyWhenBroken").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- test helpers (Play mode, Farm scene)
    [MenuItem("Farm/Test and Debug/Farm Debris - Drop Litter Near Player", false, 343)]
    private static void DropLitter() { Drop(FarmDebrisSettings.Group.Pollution); }

    [MenuItem("Farm/Test and Debug/Farm Debris - Drop Weeds Near Player", false, 344)]
    private static void DropWeeds() { for (int i = 0; i < 5; i++) Drop(FarmDebrisSettings.Group.Weed); }

    [MenuItem("Farm/Test and Debug/Farm Debris - Drop Litter Near Player", true)]
    [MenuItem("Farm/Test and Debug/Farm Debris - Drop Weeds Near Player", true)]
    private static bool CanDrop() { return EditorApplication.isPlaying && FarmDebrisSpawner.Instance != null; }

    private static void Drop(FarmDebrisSettings.Group g)
    {
        if (!FarmDebrisSpawner.Instance.SpawnNearPlayer(g)) Debug.Log("Farm Debris: no free spot near the player (or no " + g + " kinds in the settings).");
    }

    [MenuItem("Farm/Test and Debug/Farm Debris - Clear and Scatter a New Farm", false, 346)]
    private static void Rescatter()
    {
        FarmDebrisSpawner.Instance.Rescatter();
        Debug.Log("Farm Debris: scattered a new farm. Weeds " + FarmDebrisSpawner.Count(FarmDebrisSettings.Group.Weed)
                  + ", stones " + FarmDebrisSpawner.Count(FarmDebrisSettings.Group.Stone)
                  + ", fallen trees " + FarmDebrisSpawner.Count(FarmDebrisSettings.Group.FallenTree)
                  + ", litter " + FarmDebrisSpawner.Count(FarmDebrisSettings.Group.Pollution) + ".");
    }

    [MenuItem("Farm/Test and Debug/Farm Debris - Clear and Scatter a New Farm", true)]
    private static bool CanRescatter() { return CanDrop(); }

    [MenuItem("Farm/Test and Debug/Farm Debris - Add 10 Sterling Dealings", false, 345)]
    private static void AddDealings()
    {
        GameState.AddCounter(SterlingInfluence.DealingsKey, 10);
        FarmDebrisSettings s = FarmDebrisSettings.Current;
        string perDay = s != null ? s.PerDay(s.RuleFor(FarmDebrisSettings.Group.Pollution)).ToString("0.00") : "?";
        Debug.Log("Farm Debris: Sterling dealings are now " + SterlingInfluence.Dealings + "; litter per day is now " + perDay + ".");
    }

    [MenuItem("Farm/Test and Debug/Farm Debris - Add 10 Sterling Dealings", true)]
    private static bool CanAddDealings() { return EditorApplication.isPlaying; }
}
