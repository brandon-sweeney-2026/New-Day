using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > City Research and Items
///
/// Makes the things the City Block needs that are not part of the scene itself:
///   - items in Assets/Farm/Data/City: hot dogs and other street food, sprinklers, moisture gels, fertilizers, soil purifier,
///     growth tonic, metal bars (what the smelter makes) and the waste items (trash bag, scrap, sludge, old boot);
///   - Assets/Resources/SoilCareSettings.asset: what each soil item does, the crop quality colours and prices, the river numbers;
///   - Assets/Farm/City/ResearchCatalog.asset: what the scientist can research and what each costs.
///
/// Farm > Set Up > City Scene runs this for you. Safe to run again: an item or asset that already exists is KEPT with your numbers
/// (only a missing icon is filled in). Delete an asset to get the starting version back.
/// The starting numbers are the tables at the top of this file. Must live in a folder named "Editor".
/// </summary>
public static class CityAssetsMenu
{
    public const string ItemDir = "Assets/Farm/Data/City";
    public const string DataDir = "Assets/Farm/City";
    public const string SettingsPath = "Assets/Resources/SoilCareSettings.asset";
    public const string CatalogPath = DataDir + "/ResearchCatalog.asset";

    /// <summary>One item to make: file name, shown name, icon picture, type, prices, what it restores, description.</summary>
    private class Def
    {
        public string file, name, icon, text;
        public ItemData.ItemType type = ItemData.ItemType.Resource;
        public int buy, sell, energy, magic;
        public bool waste;
    }

    private static Def Food(string file, string name, string icon, int buy, int energy, int magic, string text)
    { return new Def { file = file, name = name, icon = icon, type = ItemData.ItemType.Food, buy = buy, sell = buy / 4, energy = energy, magic = magic, text = text }; }
    private static Def Thing(string file, string name, string icon, int buy, int sell, string text)
    { return new Def { file = file, name = name, icon = icon, buy = buy, sell = sell, text = text }; }
    private static Def Waste(string file, string name, string icon, int value, string text)
    { return new Def { file = file, name = name, icon = icon, sell = value, waste = true, text = text }; }

    // ------------------------------------------------------------------ the items (change the numbers here, or on the assets afterwards)
    private static readonly Def[] Items =
    {
        // street food from the hot dog cart: (buy price, health restored, magic restored)
        Food("HotDog", "Hot Dog", "icon_hotdog", 25, 30, 0, "A hot dog with mustard. Restores energy."),
        Food("ChiliDog", "Chili Dog", "icon_chili_dog", 40, 50, 0, "A hot dog buried in chili. Restores a lot of energy."),
        Food("Lemonade", "Lemonade", "icon_lemonade", 15, 10, 15, "Cold and sharp. Restores a little energy and magic."),
        Food("Pretzel", "Soft Pretzel", "icon_pretzel", 18, 20, 0, "Warm, salty and bigger than your hand."),

        // researched at the university: (buy price, sell price)
        Thing("Sprinkler1", "Copper Sprinkler", "icon_sprinkler_1", 150, 40, "Put it on a tile. Every morning it waters the 4 tiles next to it."),
        Thing("Sprinkler2", "Steel Sprinkler", "icon_sprinkler_2", 400, 100, "Put it on a tile. Every morning it waters the 8 tiles around it."),
        Thing("Sprinkler3", "Gold Sprinkler", "icon_sprinkler_3", 900, 220, "Put it on a tile. Every morning it waters a 5 by 5 square."),
        Thing("MoistureGel1", "Moisture Gel (1 day)", "icon_gel_1", 15, 3, "Work it into tilled soil. The tile waters itself tomorrow morning."),
        Thing("MoistureGel2", "Moisture Gel (2 days)", "icon_gel_2", 28, 6, "Work it into tilled soil. The tile waters itself for 2 mornings."),
        Thing("MoistureGel3", "Moisture Gel (3 days)", "icon_gel_3", 40, 9, "Work it into tilled soil. The tile waters itself for 3 mornings."),
        Thing("MoistureGel4", "Moisture Gel (4 days)", "icon_gel_4", 50, 12, "Work it into tilled soil. The tile waters itself for 4 mornings."),
        Thing("FertilizerSilver", "Silver Fertilizer", "icon_fert_silver", 20, 4, "Work it into tilled soil. The next crop harvested there is Silver quality."),
        Thing("FertilizerGold", "Gold Fertilizer", "icon_fert_gold", 45, 10, "Work it into tilled soil. The next crop harvested there is Gold quality."),
        Thing("FertilizerPlatinum", "Platinum Fertilizer", "icon_fert_platinum", 90, 20, "Work it into tilled soil. The next crop harvested there is Platinum quality."),
        Thing("SoilPurifier", "Soil Purifier", "icon_purifier", 60, 12, "Cleans the pollution out of the soil around where you use it."),
        Thing("GrowthTonic", "Growth Tonic", "icon_tonic", 80, 15, "Pour it on a growing crop and it jumps a day ahead."),

        // what the smelter in the tower makes: (not sold, sell price)
        Thing("CopperBar", "Copper Bar", "icon_bar_copper", 0, 60, "A bar of smelted copper."),
        Thing("IronBar", "Iron Bar", "icon_bar_iron", 0, 110, "A bar of smelted iron."),
        Thing("GoldBar", "Gold Bar", "icon_bar_gold", 0, 220, "A bar of smelted gold."),
        Thing("PlatinumBar", "Platinum Bar", "icon_bar_platinum", 0, 400, "A bar of smelted platinum."),

        // waste: (what it is "worth"; the treatment plant charges a part of this to take it)
        Waste("TrashBag", "Trash Bag", "icon_trash_bag", 10, "A bag of somebody's rubbish. It belongs at the treatment plant."),
        Waste("Scrap", "Scrap Metal", "icon_scrap", 20, "Bent, rusty bits. It belongs at the treatment plant."),
        Waste("SludgeBarrel", "Sludge Barrel", "icon_sludge", 40, "It glows a little. It should NOT go in the river."),
        Waste("OldBoot", "Old Boot", "icon_old_boot", 5, "One boot. The other one is still out there."),
    };

    /// <summary>What the soil items do: item file name, what kind, how strong.</summary>
    private static readonly object[][] SoilItems =
    {
        new object[] { "Sprinkler1", SoilItemKind.Sprinkler, 1 }, new object[] { "Sprinkler2", SoilItemKind.Sprinkler, 2 }, new object[] { "Sprinkler3", SoilItemKind.Sprinkler, 3 },
        new object[] { "MoistureGel1", SoilItemKind.MoistureGel, 1 }, new object[] { "MoistureGel2", SoilItemKind.MoistureGel, 2 },
        new object[] { "MoistureGel3", SoilItemKind.MoistureGel, 3 }, new object[] { "MoistureGel4", SoilItemKind.MoistureGel, 4 },
        new object[] { "FertilizerSilver", SoilItemKind.Fertilizer, 1 }, new object[] { "FertilizerGold", SoilItemKind.Fertilizer, 2 }, new object[] { "FertilizerPlatinum", SoilItemKind.Fertilizer, 3 },
        new object[] { "SoilPurifier", SoilItemKind.Purifier, 2 }, new object[] { "GrowthTonic", SoilItemKind.GrowthTonic, 1 },
    };

    /// <summary>The research list: id, title, gold, the id it needs first, the item it unlocks, description.</summary>
    private static readonly string[][] Research =
    {
        new[] { "sprinkler_1", "Copper Sprinkler", "600", "", "Sprinkler1", "A simple sprinkler that waters the 4 tiles next to it every morning." },
        new[] { "sprinkler_2", "Steel Sprinkler", "1500", "sprinkler_1", "Sprinkler2", "A wider spray: the 8 tiles around it." },
        new[] { "sprinkler_3", "Gold Sprinkler", "3500", "sprinkler_2", "Sprinkler3", "The best there is: a whole 5 by 5 square." },
        new[] { "gel_1", "Moisture Gel I", "300", "", "MoistureGel1", "A gel that holds water in the soil for one extra morning." },
        new[] { "gel_2", "Moisture Gel II", "600", "gel_1", "MoistureGel2", "The soil stays watered for two mornings." },
        new[] { "gel_3", "Moisture Gel III", "1000", "gel_2", "MoistureGel3", "The soil stays watered for three mornings." },
        new[] { "gel_4", "Moisture Gel IV", "1600", "gel_3", "MoistureGel4", "The soil stays watered for four mornings." },
        new[] { "fert_silver", "Silver Fertilizer", "500", "", "FertilizerSilver", "Fertilizer that lifts a crop from Bronze to Silver quality. Silver crops sell for more." },
        new[] { "fert_gold", "Gold Fertilizer", "1400", "fert_silver", "FertilizerGold", "Gold quality crops." },
        new[] { "fert_platinum", "Platinum Fertilizer", "3200", "fert_gold", "FertilizerPlatinum", "Platinum quality crops: the best money can grow." },
        new[] { "purifier", "Soil Purifier", "900", "", "SoilPurifier", "A powder that pulls pollution out of the soil. Useful when the river is dirty." },
        new[] { "tonic", "Growth Tonic", "1200", "fert_silver", "GrowthTonic", "A tonic that makes a crop jump a day ahead." },
    };

    // ------------------------------------------------------------------ menu
    [MenuItem("Farm/Set Up/City Research and Items", false, 155)]
    public static void RunFromMenu()
    {
        string result = Build();
        EditorUtility.DisplayDialog(result.StartsWith("Made") ? "City items ready" : "City research and items", result, "OK");
    }

    /// <summary>Makes the items and the two settings assets. Returns a sentence saying what happened (it starts with "Made" when it worked). No pop-ups.</summary>
    public static string Build()
    {
        string art = HardwareEditorUtil.FindFolder("icon_hotdog");
        if (art == null) return "The city art was not found (icon_hotdog.png and friends belong in Assets/Project/Art/City). Run Tools/make_city_art.ps1 first.";

        foreach (string guid in AssetDatabase.FindAssets("icon_ t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p).StartsWith("icon_")) HardwareEditorUtil.ImportSingle(p, SpriteAlignment.Center);
        }
        AssetDatabase.Refresh();

        HardwareEditorUtil.EnsureFolder(ItemDir);
        HardwareEditorUtil.EnsureFolder(DataDir);
        HardwareEditorUtil.EnsureFolder("Assets/Resources");

        int made = 0, kept = 0;
        foreach (Def d in Items)
        {
            string path = ItemDir + "/" + d.file + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                // keep the developer's numbers; only fill in a missing picture
                if (item.icon == null) { item.icon = HardwareEditorUtil.Spr(art, d.icon); EditorUtility.SetDirty(item); }
                kept++;
                continue;
            }
            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = d.name; item.description = d.text; item.type = d.type;
            item.icon = HardwareEditorUtil.Spr(art, d.icon);
            item.buyPrice = d.buy; item.sellPrice = d.sell;
            item.energyRestored = d.energy; item.magicRestored = d.magic;
            item.isWaste = d.waste;
            item.maxStack = 99;
            AssetDatabase.CreateAsset(item, path);
            made++;
        }

        bool newSettings = MakeSettings();
        bool newCatalog = MakeCatalog();
        AssetDatabase.SaveAssets();

        return "Made " + made + " new city items in " + ItemDir + " (" + kept + " were already there and were kept)." +
               "\n\n" + (newSettings ? "Made " : "Kept ") + SettingsPath + " (what soil items do, crop quality, river numbers)." +
               "\n" + (newCatalog ? "Made " : "Kept ") + CatalogPath + " (what the scientist researches and what it costs).";
    }

    /// <summary>Loads one of the city items by its file name (HotDog, Sprinkler1, IronBar, TrashBag...).</summary>
    public static ItemData Item(string file)
    {
        return AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/" + file + ".asset");
    }

    private static bool MakeSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<SoilCareSettings>(SettingsPath);
        bool isNew = settings == null;
        if (isNew)
        {
            settings = ScriptableObject.CreateInstance<SoilCareSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        // the list of soil items is filled in when it is empty (a new asset, or one whose list was cleared)
        if (settings.items == null || settings.items.Length == 0)
        {
            var list = new List<SoilItem>();
            foreach (object[] row in SoilItems)
            {
                ItemData item = Item((string)row[0]);
                if (item != null) list.Add(new SoilItem { item = item, kind = (SoilItemKind)row[1], power = (int)row[2], placedSprite = item.icon });
            }
            settings.items = list.ToArray();
            EditorUtility.SetDirty(settings);
        }
        return isNew;
    }

    private static bool MakeCatalog()
    {
        if (AssetDatabase.LoadAssetAtPath<ResearchCatalog>(CatalogPath) != null) return false;      // keep the developer's prices
        var catalog = ScriptableObject.CreateInstance<ResearchCatalog>();
        var list = new List<ResearchProject>();
        foreach (string[] row in Research)
        {
            ItemData item = Item(row[4]);
            list.Add(new ResearchProject
            {
                id = row[0], title = row[1], goldCost = int.Parse(row[2]), requires = row[3], description = row[5],
                unlocks = item != null ? new[] { item } : new ItemData[0]
            });
        }
        catalog.projects = list.ToArray();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return true;
    }
}
