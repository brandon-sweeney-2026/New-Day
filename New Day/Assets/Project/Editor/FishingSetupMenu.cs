using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Fishing > Set Up Fishing         (run once)
/// Farm > Fishing > Give Starter Fishing Kit   (test: a rod, baits and some fishing XP in the inventory)
/// Farm > Fishing > Reset Fishing Progress     (test: sets XP and talents back to zero)
///
/// Set Up Fishing:
///  1. imports the art (copy the "Fishing" folder into Assets first)
///  2. creates 13 fish (FishData + their items), 4 rods, 5 baits (assets in Assets/Farm/Data/Fishing and Assets/Farm/Data/Items)
///  3. adds a FishingManager to the open scene, with the rod and bait lists filled in
///  4. builds Water_Pond, Water_River and Water_MountainLake prefabs (each with its own fish) that show in the Level Editor's Objects palette
/// Safe to run again (existing assets are updated, not duplicated).
/// </summary>
public static class FishingSetupMenu
{
    private const string ItemsFolder = "Assets/Farm/Data/Items";
    private const string FishFolder = "Assets/Farm/Data/Fishing";
    private static string artFolder;

    private class FishDef
    {
        public string key, name, desc; public int stars, notes, buttons, price, xp; public float speed, nps; public float from = 0f, to = 24f;
    }

    private static readonly FishDef[] Fish =
    {
        new FishDef { key="minnow",      name="Minnow",       stars=1, notes=10,  speed=380, nps=1.0f, buttons=2, price=8,   xp=3,  desc="Tiny and quick to catch. A good first fish." },
        new FishDef { key="sunfish",     name="Sunfish",      stars=1, notes=14,  speed=400, nps=1.1f, buttons=2, price=12,  xp=4,  desc="Bright orange with a blue cheek." },
        new FishDef { key="perch",       name="Perch",        stars=1, notes=20,  speed=430, nps=1.2f, buttons=3, price=18,  xp=5,  desc="Striped and curious." },
        new FishDef { key="bluegill",    name="Bluegill",     stars=2, notes=25,  speed=450, nps=1.3f, buttons=4, price=24,  xp=6,  desc="A fighter for its size." },
        new FishDef { key="trout",       name="Trout",        stars=2, notes=32,  speed=480, nps=1.5f, buttons=4, price=40,  xp=8,  desc="Likes cold, clear water." },
        new FishDef { key="carp",        name="Carp",         stars=2, notes=40,  speed=500, nps=1.6f, buttons=4, price=55,  xp=10, desc="Old, wise and stubborn." },
        new FishDef { key="bass",        name="Bass",         stars=3, notes=48,  speed=540, nps=1.8f, buttons=5, price=75,  xp=12, desc="Big mouth, big attitude." },
        new FishDef { key="catfish",     name="Catfish",      stars=3, notes=56,  speed=560, nps=1.9f, buttons=5, price=95,  xp=14, from=17f, to=7f,  desc="Comes out in the evening." },
        new FishDef { key="pike",        name="Pike",         stars=3, notes=65,  speed=620, nps=2.1f, buttons=5, price=130, xp=18, desc="A long, fast hunter." },
        new FishDef { key="salmon",      name="Salmon",       stars=4, notes=72,  speed=660, nps=2.3f, buttons=6, price=170, xp=22, desc="Strong swimmer. Hard to tire out." },
        new FishDef { key="eel",         name="Eel",          stars=4, notes=82,  speed=700, nps=2.5f, buttons=6, price=220, xp=28, from=18f, to=6f,  desc="Slippery. Only bites at night." },
        new FishDef { key="golden_koi",  name="Golden Koi",   stars=5, notes=92,  speed=760, nps=2.8f, buttons=6, price=400, xp=40, desc="Said to bring good luck. Very rare." },
        new FishDef { key="moonlit_koi", name="Moonlit Koi",  stars=5, notes=100, speed=820, nps=3.0f, buttons=6, price=600, xp=60, from=20f, to=5f, desc="Glows faintly. Appears only in deep night." },
    };

    private class RodDef { public string key, name, desc; public int price; public float slow, window, stamina, luck, bite; public int misses; }
    private static readonly RodDef[] Rods =
    {
        new RodDef { key="wood",       name="Wooden Rod",     price=150,  desc="A simple rod. Gets the job done." },
        new RodDef { key="fiberglass", name="Fiberglass Rod", price=600,  slow=0.10f, misses=1, window=0.015f, stamina=0.10f, luck=0.05f, desc="Light and springy. Notes slow a little; one free miss." },
        new RodDef { key="carbon",     name="Carbon Rod",     price=2000, slow=0.20f, misses=2, window=0.030f, stamina=0.20f, luck=0.15f, bite=0.20f, desc="Strong and sensitive. Bites come sooner." },
        new RodDef { key="golden",     name="Golden Rod",     price=6000, slow=0.30f, misses=3, window=0.050f, stamina=0.30f, luck=0.30f, bite=0.35f, desc="A master's rod. Fish seem to want to be caught." },
    };

    private class BaitDef { public string key, name, desc; public int price; public string[] fish; public float boost = 3f; }
    private static readonly BaitDef[] Baits =
    {
        new BaitDef { key="worm",    name="Worm",        price=5,  fish=new[]{"minnow","sunfish","perch","bluegill"}, desc="Small fish love worms." },
        new BaitDef { key="cricket", name="Cricket",     price=8,  fish=new[]{"perch","trout","bass"},                 desc="Trout and bass jump at crickets." },
        new BaitDef { key="shrimp",  name="Shrimp",      price=12, fish=new[]{"carp","catfish","eel"},                 desc="Smelly. Bottom feeders adore it." },
        new BaitDef { key="grub",    name="Glow Grub",   price=20, fish=new[]{"pike","eel","moonlit_koi"},             desc="Glows in the dark water." },
        new BaitDef { key="lure",    name="Golden Lure", price=60, fish=new[]{"salmon","golden_koi","moonlit_koi"}, boost=4f, desc="Shiny. The rare ones can't resist." },
    };

    [MenuItem("Farm/Fishing/Set Up Fishing")]
    public static void Run()
    {
        artFolder = FindFolder("fish_minnow");
        if (artFolder == null)
        {
            EditorUtility.DisplayDialog("Fishing art not found",
                "Couldn't find fish_minnow.png in your project.\nCopy the 'Fishing' folder into Assets (for example Assets/Art/Fishing) and run this again.", "OK");
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { artFolder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n.StartsWith("fish_") || n.StartsWith("icon_rod_") || n.StartsWith("icon_bait_") || n.StartsWith("water_")) ImportSingle(p);
        }
        AssetDatabase.Refresh();

        EnsureFolder(ItemsFolder);
        EnsureFolder(FishFolder);

        // ---- fish ----
        var fishData = new Dictionary<string, FishData>();
        foreach (FishDef d in Fish)
        {
            ItemData item = MakeItem("Fish_" + d.key, d.name, d.desc, ItemData.ItemType.Resource, d.price, 0, "fish_" + d.key, false);
            FishData f = LoadOrCreate<FishData>(FishFolder + "/Fish_" + d.key + ".asset");
            f.fishName = d.name; f.item = item; f.stars = d.stars; f.description = d.desc;
            f.noteCount = d.notes; f.scrollSpeed = d.speed; f.notesPerSecond = d.nps; f.buttonsUsed = d.buttons;
            f.startStamina = d.stars <= 2 ? 35f : 30f;
            f.requiredHitRatio = new[] { 0.70f, 0.70f, 0.75f, 0.78f, 0.82f, 0.86f }[d.stars];
            f.missPenalty = 1f;
            f.defaultWeight = 10f; f.availableFromHour = d.from; f.availableToHour = d.to; f.xp = d.xp;
            EditorUtility.SetDirty(f);
            fishData[d.key] = f;
        }

        // ---- rods ----
        var rodData = new List<FishingRodData>();
        foreach (RodDef d in Rods)
        {
            ItemData item = MakeItem("Rod_" + d.key, d.name, d.desc, ItemData.ItemType.Tool, d.price / 4, d.price, "icon_rod_" + (d.key), true);
            FishingRodData r = LoadOrCreate<FishingRodData>(FishFolder + "/Rod_" + d.key + ".asset");
            r.rodItem = item; r.slowTime = d.slow; r.extraMisses = d.misses; r.hitWindowBonus = d.window;
            r.staminaGain = d.stamina; r.luck = d.luck; r.biteSpeed = d.bite;
            EditorUtility.SetDirty(r);
            rodData.Add(r);
        }

        // ---- baits ----
        var baitData = new List<BaitData>();
        foreach (BaitDef d in Baits)
        {
            ItemData item = MakeItem("Bait_" + d.key, d.name, d.desc, ItemData.ItemType.Resource, Mathf.Max(1, d.price / 3), d.price, "icon_bait_" + d.key, false);
            BaitData b = LoadOrCreate<BaitData>(FishFolder + "/Bait_" + d.key + ".asset");
            b.baitItem = item; b.boost = d.boost;
            var list = new List<FishData>();
            foreach (string k in d.fish) list.Add(fishData[k]);
            b.attracts = list.ToArray();
            EditorUtility.SetDirty(b);
            baitData.Add(b);
        }
        AssetDatabase.SaveAssets();

        // ---- manager in the scene ----
        var mgr = Object.FindAnyObjectByType<FishingManager>();
        if (mgr == null)
        {
            var go = new GameObject("FishingManager");
            Undo.RegisterCreatedObjectUndo(go, "Create FishingManager");
            mgr = go.AddComponent<FishingManager>();
        }
        mgr.rods = rodData.ToArray();
        mgr.baits = baitData.ToArray();
        EditorUtility.SetDirty(mgr);

        // ---- water prefabs ----
        string prefabDir = artFolder + "/Prefabs";
        EnsureFolder(prefabDir);
        BuildWater(prefabDir, "Water_Pond", "Pond", "water_pond", new Vector2(10f, 6f), fishData, new Dictionary<string, float>
            { {"minnow",30}, {"sunfish",25}, {"perch",20}, {"bluegill",20}, {"carp",8}, {"catfish",5}, {"golden_koi",1} });
        BuildWater(prefabDir, "Water_River", "River", "water_river", new Vector2(14f, 5f), fishData, new Dictionary<string, float>
            { {"perch",15}, {"trout",25}, {"bass",18}, {"salmon",12}, {"catfish",8}, {"pike",8}, {"eel",6}, {"moonlit_koi",0.5f} });
        BuildWater(prefabDir, "Water_MountainLake", "Mountain Lake", "water_lake", new Vector2(12f, 8f), fishData, new Dictionary<string, float>
            { {"minnow",10}, {"trout",25}, {"pike",12}, {"salmon",14}, {"eel",8}, {"golden_koi",1.5f}, {"moonlit_koi",1.5f} });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Fishing ready",
            "Done!\n\n- 13 fish, 4 rods and 5 baits created (data in " + FishFolder + ", items in " + ItemsFolder + ")\n" +
            "- FishingManager added to the scene.\n- Water_Pond, Water_River and Water_MountainLake prefabs in " + prefabDir +
            ".\n  In the Level Editor open the Objects palette > 'Edit palette list' > 'Reload this palette from labelled assets', then paint a water piece over each pond and drag its box handles to fit.\n\n" +
            "To try it: Farm > Fishing > Give Starter Fishing Kit, then equip the rod, face the water and press Space.\n" +
            "To sell rods/baits, add them to a ShopCounter's stock (they have Buy Prices).", "OK");
    }

    private static void BuildWater(string dir, string prefabName, string areaName, string art, Vector2 size,
                                   Dictionary<string, FishData> fish, Dictionary<string, float> table)
    {
        var go = new GameObject(prefabName);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artFolder + "/" + art + ".png");
        sr.sortingOrder = 100;                       // design picture only; hidden when the game runs
        var w = go.AddComponent<FishingWater>();
        w.areaName = areaName; w.boxSize = size;
        w.fishHere = new List<FishChance>();
        foreach (var kv in table) w.fishHere.Add(new FishChance { fish = fish[kv.Key], weight = kv.Value });

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + prefabName + ".prefab");
        Object.DestroyImmediate(go);
        AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
    }

    // ------------------------------------------------------------------ test helpers
    [MenuItem("Farm/Fishing/Give Starter Fishing Kit")]
    public static void GiveKit()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Play mode needed", "Press Play first, then use this to fill the inventory.", "OK"); return; }
        var inv = Object.FindAnyObjectByType<Inventory>();
        var mgr = Object.FindAnyObjectByType<FishingManager>();
        if (inv == null || mgr == null || mgr.rods == null) { EditorUtility.DisplayDialog("Not ready", "Run Farm > Fishing > Set Up Fishing first.", "OK"); return; }
        if (mgr.rods.Length > 0 && mgr.rods[0].rodItem != null) inv.Add(mgr.rods[0].rodItem, 1);
        foreach (BaitData b in mgr.baits) if (b != null && b.baitItem != null) inv.Add(b.baitItem, 5);
        FishingTalents.AddXp(300);
        Debug.Log("Fishing kit added: a Wooden Rod, 5 of each bait and 300 fishing XP (level " + FishingTalents.FishingLevel + ").");
    }

    [MenuItem("Farm/Fishing/Reset Fishing Progress")]
    public static void ResetProgress()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Play mode needed", "Fishing progress is part of the running game. Press Play first.", "OK"); return; }
        GameState.SetCounter("fishing_xp", 0);
        FishingTalents.Reset();
        Debug.Log("Fishing XP and talents reset.");
    }

    // ------------------------------------------------------------------ helpers
    private static ItemData MakeItem(string asset, string name, string desc, ItemData.ItemType type, int sell, int buy, string icon, bool isRod)
    {
        var it = LoadOrCreate<ItemData>(ItemsFolder + "/" + asset + ".asset");
        it.itemName = name; it.description = desc; it.type = type;
        it.sellPrice = sell; it.buyPrice = buy; it.maxStack = type == ItemData.ItemType.Tool ? 1 : 99;
        if (isRod) it.toolAction = ItemData.ToolAction.FishingRod;
        it.icon = AssetDatabase.LoadAssetAtPath<Sprite>(artFolder + "/" + icon + ".png");
        EditorUtility.SetDirty(it);
        return it;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string FindFolder(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return null;
    }

    private static void ImportSingle(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
