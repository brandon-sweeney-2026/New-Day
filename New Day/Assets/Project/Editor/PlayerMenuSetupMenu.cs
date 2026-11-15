using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Player Menu
///
/// The player menu itself needs no setup (it creates itself when the game starts). This menu makes the
/// things you will want to EDIT:
///   1. Assets/Resources/PlayerMenuSettings.asset - keys, colours, XP rules, perks, achievements, friendship numbers.
///   2. Starter equipment items in Assets/Farm/Data/Equipment (a hat, a vest, gloves, boots, two rings, a necklace).
///   3. Starter crafting recipes for them in Assets/Farm/Data/Crafting, and adds every recipe in that folder
///      to the Crafting tab.
///
/// It is safe to run again: anything that already exists is left exactly as it is, so your own changes are kept.
/// To get a starter item or recipe back with its default numbers, delete the asset and run this again.
/// </summary>
public static class PlayerMenuSetupMenu
{
    private const string SettingsPath = "Assets/Resources/PlayerMenuSettings.asset";
    private const string EquipmentFolder = "Assets/Farm/Data/Equipment";
    private const string CraftingFolder = "Assets/Farm/Data/Crafting";

    // ------------------------------------------------------------------
    // The starter equipment. Change the numbers here, or (easier) on the assets in the Inspector afterwards.
    // ------------------------------------------------------------------
    private class Gear
    {
        public string file, itemName, description;
        public ItemData.EquipSlot slot;
        public int defense, health, buyPrice;
        public float magic, speedPercent, damagePercent;
    }

    private static readonly Gear[] StarterGear =
    {
        new Gear { file = "StrawHat", itemName = "Straw Hat", description = "Keeps the sun off while you work.", slot = ItemData.EquipSlot.Hat, health = 5, buyPrice = 80 },
        new Gear { file = "LeatherVest", itemName = "Leather Vest", description = "Tough enough to turn a bite.", slot = ItemData.EquipSlot.Armor, defense = 2, buyPrice = 200 },
        new Gear { file = "WorkGloves", itemName = "Work Gloves", description = "Thick gloves for rough jobs.", slot = ItemData.EquipSlot.Gloves, defense = 1, buyPrice = 90 },
        new Gear { file = "SturdyBoots", itemName = "Sturdy Boots", description = "Good soles. You walk a little faster.", slot = ItemData.EquipSlot.Boots, speedPercent = 5f, buyPrice = 150 },
        new Gear { file = "CopperRing", itemName = "Copper Ring", description = "A plain band that hums faintly.", slot = ItemData.EquipSlot.Ring, magic = 5f, buyPrice = 120 },
        new Gear { file = "IronRing", itemName = "Iron Ring", description = "Heavy on the finger. Your swings land harder.", slot = ItemData.EquipSlot.Ring, damagePercent = 5f, buyPrice = 180 },
        new Gear { file = "EmeraldNecklace", itemName = "Emerald Necklace", description = "The stone is warm to the touch.", slot = ItemData.EquipSlot.Necklace, health = 15, buyPrice = 400 },
    };

    // ------------------------------------------------------------------
    // The starter recipes: what each piece of equipment is made from.
    // Ingredient names are the FILE names of items in Assets/Farm/Data/Items. A recipe is skipped if an ingredient is missing.
    // ------------------------------------------------------------------
    private class Craft
    {
        public string gearFile;
        public int minutes;
        public string[] ingredients;
        public int[] counts;
    }

    private static readonly Craft[] StarterCrafts =
    {
        new Craft { gearFile = "StrawHat", minutes = 20, ingredients = new[] { "FabricRoll" }, counts = new[] { 2 } },
        new Craft { gearFile = "LeatherVest", minutes = 60, ingredients = new[] { "FabricRoll", "Chitin" }, counts = new[] { 2, 4 } },
        new Craft { gearFile = "WorkGloves", minutes = 20, ingredients = new[] { "FabricRoll", "Chitin" }, counts = new[] { 1, 1 } },
        new Craft { gearFile = "SturdyBoots", minutes = 40, ingredients = new[] { "FabricRoll", "Chitin" }, counts = new[] { 1, 3 } },
        new Craft { gearFile = "CopperRing", minutes = 30, ingredients = new[] { "CopperOre", "Coal" }, counts = new[] { 3, 1 } },
        new Craft { gearFile = "IronRing", minutes = 30, ingredients = new[] { "IronOre", "Coal" }, counts = new[] { 3, 2 } },
        new Craft { gearFile = "EmeraldNecklace", minutes = 60, ingredients = new[] { "Emerald", "GoldOre" }, counts = new[] { 1, 2 } },
    };

    [MenuItem("Farm/Set Up/Player Menu", false, 107)]
    private static void Run()
    {
        EditorUtility.DisplayDialog("Player Menu", Build(), "OK");
    }

    /// <summary>Does the work without a pop-up and returns a sentence saying what happened.</summary>
    public static string Build()
    {
        // ---- 1. the settings asset ----
        EnsureFolder("Assets/Resources");
        var settings = AssetDatabase.LoadAssetAtPath<PlayerMenuSettings>(SettingsPath);
        bool newSettings = settings == null;
        if (newSettings)
        {
            settings = ScriptableObject.CreateInstance<PlayerMenuSettings>();      // starts with the built-in defaults
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        // ---- 2. starter equipment ----
        EnsureFolder(EquipmentFolder);
        int madeGear = 0;
        foreach (Gear g in StarterGear)
        {
            string path = EquipmentFolder + "/" + g.file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null) continue;      // already there: keep the developer's version

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = g.itemName;
            item.description = g.description;
            item.type = ItemData.ItemType.Equipment;
            item.maxStack = 1;
            item.buyPrice = g.buyPrice;
            item.sellPrice = Mathf.Max(1, g.buyPrice / 4);
            item.equipSlot = g.slot;
            item.defense = g.defense;
            item.bonusMaxHealth = g.health;
            item.bonusMaxMagic = g.magic;
            item.bonusMoveSpeedPercent = g.speedPercent;
            item.bonusDamagePercent = g.damagePercent;
            AssetDatabase.CreateAsset(item, path);
            madeGear++;
        }

        // ---- 3. starter recipes ----
        EnsureFolder(CraftingFolder);
        int madeRecipes = 0;
        var skipped = new List<string>();
        foreach (Craft c in StarterCrafts)
        {
            string path = CraftingFolder + "/Craft_" + c.gearFile + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Recipe>(path) != null) continue;

            var result = AssetDatabase.LoadAssetAtPath<ItemData>(EquipmentFolder + "/" + c.gearFile + ".asset");
            if (result == null) continue;

            var ingredients = new List<Recipe.Ingredient>();
            bool missing = false;
            for (int i = 0; i < c.ingredients.Length; i++)
            {
                ItemData item = FindItem(c.ingredients[i]);
                if (item == null) { missing = true; break; }
                ingredients.Add(new Recipe.Ingredient { item = item, count = c.counts[i] });
            }
            if (missing) { skipped.Add(result.itemName); continue; }

            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.recipeName = result.itemName;
            recipe.description = result.description;
            recipe.ingredients = ingredients;
            recipe.result = result;
            recipe.resultCount = 1;
            recipe.cookMinutes = c.minutes;
            recipe.crafted = true;          // counts as crafting, not cooking
            AssetDatabase.CreateAsset(recipe, path);
            madeRecipes++;
        }

        // ---- 4. every recipe in the Crafting folder goes on the Crafting tab ----
        int added = 0;
        settings.craftingRecipes.RemoveAll(r => r == null);      // tidy up recipes that were deleted
        foreach (string guid in AssetDatabase.FindAssets("t:Recipe", new[] { CraftingFolder }))
        {
            var recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
            if (recipe == null || settings.craftingRecipes.Contains(recipe)) continue;
            settings.craftingRecipes.Add(recipe);
            added++;
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = "Made the player menu data: " + (newSettings ? "new settings asset" : "settings asset kept") + " (" + SettingsPath + "), "
                       + madeGear + " new equipment item(s), " + madeRecipes + " new recipe(s), " + added + " recipe(s) added to the Crafting tab.";
        if (skipped.Count > 0) message += "\n\nNo recipe was made for: " + string.Join(", ", skipped.ToArray()) + " (an ingredient item was not found).";
        message += "\n\nIn the game press Tab to open the menu. To try the equipment, hand it out with Farm > Test and Debug.";
        return message;
    }

    /// <summary>Finds an item by its file name: first in Assets/Farm/Data/Items, then anywhere in the project.</summary>
    private static ItemData FindItem(string fileName)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Farm/Data/Items/" + fileName + ".asset");
        if (item != null) return item;

        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:ItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == fileName) return AssetDatabase.LoadAssetAtPath<ItemData>(path);
        }
        return null;
    }

    /// <summary>Makes a folder (and the folders above it) if it is not there yet.</summary>
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
