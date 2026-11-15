using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Tools
///   1. finds the tool pictures (hoe_stone.png ... watering_can_platinum.png, in Assets/Project/Art/Tools) and sets them up as sprites
///      (16 pixels per unit, Point filter, one sprite each);
///   2. makes 25 tool items (Hoe, Watering Can, Scythe, Axe, Pickaxe  x  Stone, Copper, Iron, Gold, Platinum) in Assets/Farm/Data/Tools;
///   3. swaps the old "Basic" tools in the Player's starting inventory (open scene) for the Stone ones.
/// Running it again only refreshes the pictures and keeps numbers you changed.
/// Farm > Test and Debug > Reset Tool Stats To Defaults puts every tool's numbers back to the defaults below.
///
/// What each upgrade does (change the tables below, or the numbers on each item in the Inspector):
///   Hoe          - tills more tiles in a row        (Tool Reach)
///   Watering Can - waters more tiles in a row       (Tool Reach)
///   Scythe       - harvests more tiles in a row     (Tool Reach)
///   Axe          - each swing counts as more hits   (Tool Power)
///   Pickaxe      - each swing counts as more hits   (Tool Power)
/// Must live in a folder named "Editor".
/// </summary>
public static class ToolSetupMenu
{
    private const string Folder = "Assets/Farm/Data/Tools";

    private static readonly string[] Tiers = { "stone", "copper", "iron", "gold", "platinum" };
    private static readonly string[] TierNames = { "Stone", "Copper", "Iron", "Gold", "Platinum" };

    /// <summary>One kind of tool: its picture name, its item name, what it does, and its numbers for the five tiers.</summary>
    private class ToolDef
    {
        public string art;                      // picture file name without the tier, e.g. "hoe" for hoe_stone.png
        public string name;                     // shown in the game, e.g. "Hoe"
        public ItemData.ToolAction action;
        public int[] reach;                     // tiles in a row, one number per tier (stone ... platinum)
        public int[] power;                     // hits per swing, one number per tier
        public bool gripPivot;                  // true = the picture turns around the end of its handle; false = around its middle
    }

    // ------------------------------------------------------------------ the numbers (one value per tier: stone ... platinum)
    private static readonly int[] One = { 1, 1, 1, 1, 1 };
    private static readonly int[] HoeReach = { 1, 2, 3, 4, 5 };
    private static readonly int[] CanReach = { 1, 2, 3, 4, 5 };
    private static readonly int[] ScytheReach = { 1, 2, 3, 4, 5 };
    private static readonly int[] AxePower = { 1, 2, 3, 4, 5 };
    private static readonly int[] PickaxePower = { 1, 2, 3, 4, 5 };
    private static readonly int[] BuyPrices = { 50, 250, 600, 1500, 3500 };

    private static ToolDef[] Defs()
    {
        return new[]
        {
            new ToolDef { art = "hoe", name = "Hoe", action = ItemData.ToolAction.Hoe, reach = HoeReach, power = One, gripPivot = true },
            new ToolDef { art = "watering_can", name = "Watering Can", action = ItemData.ToolAction.WateringCan, reach = CanReach, power = One, gripPivot = false },
            new ToolDef { art = "scythe", name = "Scythe", action = ItemData.ToolAction.Sickle, reach = ScytheReach, power = One, gripPivot = true },
            new ToolDef { art = "axe", name = "Axe", action = ItemData.ToolAction.Axe, reach = One, power = AxePower, gripPivot = true },
            new ToolDef { art = "pickaxe", name = "Pickaxe", action = ItemData.ToolAction.Pickaxe, reach = One, power = PickaxePower, gripPivot = true },
        };
    }

    [MenuItem("Farm/Set Up/Tools", false, 105)]
    public static void SetUp()
    {
        Run(false);
    }

    [MenuItem("Farm/Test and Debug/Reset Tool Stats To Defaults", false, 325)]
    public static void ResetStats()
    {
        if (!EditorUtility.DisplayDialog("Reset tool stats", "Put the reach, power, prices and descriptions of all 25 tools back to the defaults? Pictures stay.", "Reset", "Cancel")) return;
        Run(true);
    }

    private static void Run(bool resetStats)
    {
        ToolDef[] defs = Defs();

        // 1. pictures
        var missing = new List<string>();
        foreach (ToolDef d in defs)
            foreach (string t in Tiers)
                SetupSprite(d.art + "_" + t, d.gripPivot, missing);
        AssetDatabase.Refresh();

        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Tool pictures missing",
                "These pictures were not found anywhere in Assets (they belong in Assets/Project/Art/Tools):\n\n" +
                string.Join(", ", missing.GetRange(0, Mathf.Min(6, missing.Count)).ToArray()) + (missing.Count > 6 ? " and " + (missing.Count - 6) + " more" : ""), "OK");
            return;
        }

        // 2. items
        EnsureFolder(Folder);
        var stoneTools = new Dictionary<ItemData.ToolAction, ItemData>();   // the starter version of each tool
        var ours = new HashSet<ItemData>();                                 // every item this menu made
        foreach (ToolDef d in defs)
            for (int t = 0; t < Tiers.Length; t++)
            {
                string assetName = TierNames[t] + d.name.Replace(" ", "");   // e.g. StoneWateringCan
                bool isNew;
                ItemData it = LoadOrCreate(assetName, out isNew);

                // always: what the item IS
                it.itemName = TierNames[t] + " " + d.name;
                it.type = ItemData.ItemType.Tool;
                it.toolAction = d.action;
                it.tier = (ItemData.WeaponTier)t;
                it.maxStack = 1;
                it.icon = LoadSprite(d.art + "_" + Tiers[t]);

                // only for a new item (or when resetting): the numbers you are allowed to change
                if (isNew || resetStats)
                {
                    it.toolReach = d.reach[t];
                    it.toolPower = d.power[t];
                    it.buyPrice = BuyPrices[t];
                    it.sellPrice = BuyPrices[t] / 4;
                    it.description = Describe(d, t);
                }

                EditorUtility.SetDirty(it);
                ours.Add(it);
                if (t == 0) stoneTools[d.action] = it;
            }
        AssetDatabase.SaveAssets();

        // 3. the player's starting tools: swap the old basic ones for the stone ones
        string playerMsg = SwapStartingTools(stoneTools, ours);

        EditorUtility.DisplayDialog("Tools ready",
            "Made 25 tools in " + Folder + ".\n\n" + playerMsg +
            "\n\nTo try them: Play, open Farm > Test and Debug > Give Items and give yourself a better tool." +
            "\nTo sell upgrades in the shop: drag the tools you want into the 'Stock' list of your Shop Counter (the price is each tool's Buy Price)." +
            "\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    private static string Describe(ToolDef d, int t)
    {
        int reach = d.reach[t], power = d.power[t];
        string tiles = reach == 1 ? "the tile in front of you" : reach + " tiles in a row";
        string hits = power == 1 ? "" : " Each swing counts as " + power + " hits.";
        switch (d.action)
        {
            case ItemData.ToolAction.Hoe: return "Tills " + tiles + " so seeds can be planted.";
            case ItemData.ToolAction.WateringCan: return "Waters " + tiles + ".";
            case ItemData.ToolAction.Sickle: return "Harvests ripe crops on " + tiles + ".";
            case ItemData.ToolAction.Axe: return "Chops down trees." + hits;
            case ItemData.ToolAction.Pickaxe: return "Breaks rocks and ore." + hits;
            default: return "";
        }
    }

    /// <summary>In the open scene, replaces any older tool of the same kind in the Player's starting inventory with the Stone one.</summary>
    private static string SwapStartingTools(Dictionary<ItemData.ToolAction, ItemData> stoneTools, HashSet<ItemData> ours)
    {
        var inv = Object.FindAnyObjectByType<Inventory>();
        if (inv == null) return "No Player with an Inventory in the open scene, so the starting tools were left alone. Open your farm scene and run this again to swap them.";

        var so = new SerializedObject(inv);
        SerializedProperty list = so.FindProperty("startingItems");
        if (list == null) return "Could not read the Player's starting items, so they were left alone.";

        int swapped = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty itemProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("item");
            var old = itemProp.objectReferenceValue as ItemData;
            if (old == null || old.type != ItemData.ItemType.Tool || ours.Contains(old)) continue;   // not a tool, or already one of the new ones
            ItemData stone;
            if (!stoneTools.TryGetValue(old.toolAction, out stone)) continue;                        // shovel, fishing rod...: no upgrades, keep it
            itemProp.objectReferenceValue = stone;
            swapped++;
        }
        if (swapped == 0) return "The Player's starting tools did not need changing.";

        so.ApplyModifiedProperties();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(inv.gameObject.scene);
        return "Swapped " + swapped + " old tool(s) in the Player's starting inventory for the Stone versions. Save the scene (Ctrl+S).";
    }

    // ------------------------------------------------------------------ helpers
    private static void SetupSprite(string name, bool gripPivot, List<string> missing)
    {
        string path = FindTexture(name);
        if (path == null) { missing.Add(name); return; }
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;        // one sprite per picture, so there are no sprite IDs to keep stable
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(s);
        // The handle's end is 4 pixels in and 3.5 pixels up from the bottom-left corner of the 24x24 pictures.
        imp.spritePivot = gripPivot ? new Vector2(4f / 24f, 3.5f / 24f) : new Vector2(0.5f, 0.5f);
        imp.SaveAndReimport();
    }

    private static string FindTexture(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }

    private static Sprite LoadSprite(string name)
    {
        string p = FindTexture(name);
        return p == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    private static ItemData LoadOrCreate(string assetName, out bool isNew)
    {
        string path = Folder + "/" + assetName + ".asset";
        var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        isNew = it == null;
        if (it == null)
        {
            it = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(it, path);
        }
        return it;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
