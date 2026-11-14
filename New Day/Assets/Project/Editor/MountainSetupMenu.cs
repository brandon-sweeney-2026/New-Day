using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Farm > Mountain > Set Up Mountain Trail      (run once, in your mountain scene)
/// Farm > Mountain > Drop Test Set Near Player  (optional: a bug, a bat and a few rocks to try things)
///
/// Set Up Mountain Trail:
///  1. imports the art (copy the "Mountain" folder into Assets first)
///  2. creates the items: Stone, Coal, Copper/Iron/Gold/Platinum Ore, Diamond, Ruby, Emerald, Chitin Shard, Bat Wing
///  3. builds prefabs next to the art (in a "Prefabs" folder): 12 rocks, the Bug, the Bat and the Mine Entrance
///     (rocks and the entrance are labelled so they show in the Level Editor's Objects palette)
///  4. adds a "MountainSpawners" object to the open scene with a RockSpawner and an EnemySpawner already filled in
/// Safe to run again.
/// </summary>
public static class MountainSetupMenu
{
    private const string ItemsFolder = "Assets/Farm/Data/Items";
    private static string artFolder;

    private class RockDef
    {
        public string name, art, rockName; public int hits, stoneMin, stoneMax; public float trailWeight;
        public string bonusItem; public int bonusMin, bonusMax; public float bonusChance;
    }

    private static List<RockDef> Rocks()
    {
        var l = new List<RockDef>();
        // ---- trail rocks (these are what spawns on the mountain trail) ----
        l.Add(new RockDef { name = "Rock_Brown_Small", art = "rock_brown_a", rockName = "Rock", hits = 2, stoneMin = 1, stoneMax = 2, trailWeight = 20 });
        l.Add(new RockDef { name = "Rock_Brown_Large", art = "rock_brown_b", rockName = "Rock", hits = 3, stoneMin = 2, stoneMax = 3, trailWeight = 18 });
        l.Add(new RockDef { name = "Rock_Grey_Small", art = "rock_grey_a", rockName = "Rock", hits = 2, stoneMin = 1, stoneMax = 2, trailWeight = 17 });
        l.Add(new RockDef { name = "Rock_Grey_Large", art = "rock_grey_b", rockName = "Rock", hits = 3, stoneMin = 2, stoneMax = 3, trailWeight = 15 });
        l.Add(new RockDef { name = "Ore_Coal", art = "ore_coal", rockName = "Coal", hits = 4, stoneMin = 1, stoneMax = 2, trailWeight = 9, bonusItem = "Coal", bonusMin = 1, bonusMax = 3, bonusChance = 1f });
        l.Add(new RockDef { name = "Ore_Copper", art = "ore_copper", rockName = "Copper", hits = 5, stoneMin = 1, stoneMax = 2, trailWeight = 6, bonusItem = "CopperOre", bonusMin = 1, bonusMax = 2, bonusChance = 1f });
        // ---- mine rocks (trailWeight 0: they are for the mine, but you can paint them anywhere in the Level Editor) ----
        l.Add(new RockDef { name = "Ore_Iron", art = "ore_iron", rockName = "Iron", hits = 6, stoneMin = 1, stoneMax = 2, bonusItem = "IronOre", bonusMin = 1, bonusMax = 3, bonusChance = 1f });
        l.Add(new RockDef { name = "Ore_Gold", art = "ore_gold", rockName = "Gold", hits = 7, stoneMin = 1, stoneMax = 2, bonusItem = "GoldOre", bonusMin = 1, bonusMax = 2, bonusChance = 1f });
        l.Add(new RockDef { name = "Ore_Platinum", art = "ore_platinum", rockName = "Platinum", hits = 8, stoneMin = 1, stoneMax = 2, bonusItem = "PlatinumOre", bonusMin = 1, bonusMax = 2, bonusChance = 1f });
        l.Add(new RockDef { name = "Gem_Diamond", art = "gem_diamond", rockName = "Diamond", hits = 9, stoneMin = 1, stoneMax = 2, bonusItem = "Diamond", bonusMin = 1, bonusMax = 1, bonusChance = 1f });
        l.Add(new RockDef { name = "Gem_Ruby", art = "gem_ruby", rockName = "Ruby", hits = 9, stoneMin = 1, stoneMax = 2, bonusItem = "Ruby", bonusMin = 1, bonusMax = 1, bonusChance = 1f });
        l.Add(new RockDef { name = "Gem_Emerald", art = "gem_emerald", rockName = "Emerald", hits = 9, stoneMin = 1, stoneMax = 2, bonusItem = "Emerald", bonusMin = 1, bonusMax = 1, bonusChance = 1f });
        return l;
    }

    [MenuItem("Farm/Mountain/Set Up Mountain Trail")]
    public static void Run()
    {
        artFolder = FindFolder("rock_brown_a");
        if (artFolder == null)
        {
            EditorUtility.DisplayDialog("Mountain art not found",
                "Couldn't find rock_brown_a.png in your project.\nCopy the 'Mountain' folder into Assets (for example Assets/Art/Mountain) and run this again.", "OK");
            return;
        }

        // ---- import art ----
        foreach (string guid in AssetDatabase.FindAssets("icon_ t:Texture2D", new[] { artFolder }))
            ImportSingle(AssetDatabase.GUIDToAssetPath(guid), SpriteAlignment.Center);
        foreach (RockDef r in Rocks()) ImportSingle(artFolder + "/" + r.art + ".png", SpriteAlignment.BottomCenter);
        ImportSingle(artFolder + "/mine_entrance.png", SpriteAlignment.BottomCenter);
        Dictionary<string, Sprite> bugSheet = SliceSheet(artFolder + "/bug_sheet.png", 16, 16, SpriteAlignment.BottomCenter);
        Dictionary<string, Sprite> batSheet = SliceSheet(artFolder + "/bat_sheet.png", 24, 16, SpriteAlignment.Center);
        AssetDatabase.Refresh();

        // ---- items ----
        EnsureFolder(ItemsFolder);
        var items = new Dictionary<string, ItemData>();
        items["Stone"] = MakeItem("Stone", "Stone", "A plain chunk of mountain stone.", 5, "icon_stone");
        items["Coal"] = MakeItem("Coal", "Coal", "Black and dusty. Burns hot.", 14, "icon_coal");
        items["CopperOre"] = MakeItem("CopperOre", "Copper Ore", "Rock streaked with orange copper.", 24, "icon_copper_ore");
        items["IronOre"] = MakeItem("IronOre", "Iron Ore", "Heavy ore with rusty flecks.", 40, "icon_iron_ore");
        items["GoldOre"] = MakeItem("GoldOre", "Gold Ore", "Glittering gold in grey rock.", 90, "icon_gold_ore");
        items["PlatinumOre"] = MakeItem("PlatinumOre", "Platinum Ore", "Rare, pale and very valuable.", 180, "icon_platinum_ore");
        items["Diamond"] = MakeItem("Diamond", "Diamond", "A flawless, icy gem.", 420, "icon_diamond");
        items["Ruby"] = MakeItem("Ruby", "Ruby", "A deep red gem.", 320, "icon_ruby");
        items["Emerald"] = MakeItem("Emerald", "Emerald", "A bright green gem.", 260, "icon_emerald");
        items["Chitin"] = MakeItem("Chitin", "Chitin Shard", "A hard grey shell fragment from a mountain bug.", 12, "icon_chitin");
        items["BatWing"] = MakeItem("BatWing", "Bat Wing", "Leathery and light.", 10, "icon_bat_wing");
        AssetDatabase.SaveAssets();

        // ---- prefabs ----
        string prefabDir = artFolder + "/Prefabs";
        EnsureFolder(prefabDir);
        Material lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Lighting/SpriteLit.mat");   // from the Lighting pack, if you ran it

        var trailRocks = new List<KeyValuePair<GameObject, float>>();
        foreach (RockDef d in Rocks())
        {
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(artFolder + "/" + d.art + ".png");
            var go = new GameObject(d.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr; sr.sortingOrder = 10;
            if (lit != null) sr.sharedMaterial = lit;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.85f, 0.5f); box.offset = new Vector2(0f, 0.35f);
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.1f;

            var rock = go.AddComponent<MineableRock>();
            rock.rockName = d.rockName; rock.hits = d.hits;
            rock.stone = items["Stone"]; rock.stoneMin = d.stoneMin; rock.stoneMax = d.stoneMax;
            if (!string.IsNullOrEmpty(d.bonusItem))
                rock.bonusDrops = new[] { new RockDrop { item = items[d.bonusItem], min = d.bonusMin, max = d.bonusMax, chance = d.bonusChance } };

            GameObject prefab = SavePrefab(go, prefabDir + "/" + d.name + ".prefab");
            AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
            if (d.trailWeight > 0f) trailRocks.Add(new KeyValuePair<GameObject, float>(prefab, d.trailWeight));
        }

        GameObject bugPrefab = BuildBug(prefabDir, bugSheet, items, lit);
        GameObject batPrefab = BuildBat(prefabDir, batSheet, items, lit);
        BuildEntrance(prefabDir, lit);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- scene: spawners ----
        GameObject root = GameObject.Find("MountainSpawners");
        if (root == null)
        {
            root = new GameObject("MountainSpawners");
            Undo.RegisterCreatedObjectUndo(root, "Create MountainSpawners");
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null) root.transform.position = player.transform.position;
        }

        var rs = root.GetComponent<RockSpawner>();
        if (rs == null) rs = root.AddComponent<RockSpawner>();
        rs.rockTypes = new RockSpawner.RockType[trailRocks.Count];
        for (int i = 0; i < trailRocks.Count; i++) rs.rockTypes[i] = new RockSpawner.RockType { prefab = trailRocks[i].Key, weight = trailRocks[i].Value };
        rs.targetCount = 28; rs.respawnPerDay = 8;

        var es = root.GetComponent<EnemySpawner>();
        if (es == null) es = root.AddComponent<EnemySpawner>();
        es.enemyTypes = new[]
        {
            new EnemySpawner.EnemyType { prefab = bugPrefab, weight = 3f, maxAlive = 5 },
            new EnemySpawner.EnemyType { prefab = batPrefab, weight = 1.5f, maxAlive = 2 },
        };
        es.maxAlive = 6; es.startCount = 4;
        es.spawnOffset = Vector2.zero;
        EditorUtility.SetDirty(root);

        bool pieces = BuildDesignPieces();

        EditorUtility.DisplayDialog("Mountain trail ready",
            "Done!\n\n- 11 items created in " + ItemsFolder + "\n- prefabs created in " + prefabDir +
            "\n  (12 rocks, Bug, Bat, MineEntrance)\n- 'MountainSpawners' added to the scene with rocks and enemies set up.\n" +
            (pieces ? "- Level Editor pieces added (zones, keep-clear marker, Bug, Bat).\n" : "- Level Editor zone pieces skipped: copy the 'Zones' art folder in, then run Farm > Level Editors > Add Mountain Editor Pieces.\n") + "\n" +
            "Select MountainSpawners: if your ground tilemap isn't called 'Ground', drag it into 'Spawn On Tilemap' on both spawner components.", "OK");
    }

    // ------------------------------------------------------------------
    // Prefabs
    // ------------------------------------------------------------------
    private static GameObject BuildBug(string dir, Dictionary<string, Sprite> sheet, Dictionary<string, ItemData> items, Material lit)
    {
        var go = new GameObject("Bug");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        if (lit != null) sr.sharedMaterial = lit;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.28f; col.offset = new Vector2(0f, 0.3f);
        go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;

        var bug = go.AddComponent<BugEnemy>();
        bug.enemyName = "Bug"; bug.maxHealth = 12; bug.contactDamage = 3;
        bug.sideFrames = Cells(sheet, 0, 0, 4); bug.downFrames = Cells(sheet, 0, 1, 4); bug.upFrames = Cells(sheet, 0, 2, 4);
        if (bug.sideFrames.Length > 0) sr.sprite = bug.sideFrames[0];
        bug.drops = new[] { new EnemyDrop { item = items["Chitin"], min = 1, max = 2, chance = 0.7f } };
        bug.goldMin = 1; bug.goldMax = 3;
        bug.shadowSize = new Vector2(0.85f, 0.32f); bug.shadowOffset = new Vector2(0f, 0.12f);
        return SavePrefab(go, dir + "/Bug.prefab");
    }

    private static GameObject BuildBat(string dir, Dictionary<string, Sprite> sheet, Dictionary<string, ItemData> items, Material lit)
    {
        var go = new GameObject("Bat");
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f; col.offset = new Vector2(0f, 0.25f); col.isTrigger = true;

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(go.transform, false);
        var sr = bodyGo.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 24;
        if (lit != null) sr.sharedMaterial = lit;

        var bat = go.AddComponent<BatEnemy>();
        bat.enemyName = "Bat"; bat.maxHealth = 8; bat.contactDamage = 4;
        bat.body = bodyGo.transform;
        bat.flapFrames = Cells(sheet, 0, 0, 4); bat.diveFrames = Cells(sheet, 4, 0, 2);
        if (bat.flapFrames.Length > 0) sr.sprite = bat.flapFrames[0];
        bat.drops = new[] { new EnemyDrop { item = items["BatWing"], min = 1, max = 1, chance = 0.6f } };
        bat.goldMin = 1; bat.goldMax = 4;
        bat.shadowSize = new Vector2(0.9f, 0.3f); bat.shadowOffset = new Vector2(0f, 0.05f);
        return SavePrefab(go, dir + "/Bat.prefab");
    }

    private static void BuildEntrance(string dir, Material lit)
    {
        var go = new GameObject("MineEntrance");
        go.AddComponent<UnityEngine.Rendering.SortingGroup>();            // so lanterns and the building sort as one piece
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artFolder + "/mine_entrance.png");
        sr.sortingOrder = 5;
        if (lit != null) sr.sharedMaterial = lit;

        // solid walls either side of the doorway
        var left = go.AddComponent<BoxCollider2D>(); left.offset = new Vector2(-2.75f, 0.9f); left.size = new Vector2(2.5f, 1.0f);
        var right = go.AddComponent<BoxCollider2D>(); right.offset = new Vector2(2.75f, 0.9f); right.size = new Vector2(2.5f, 1.0f);
        go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.9f;

        // the doorway: a trigger the player presses the action button in
        var door = new GameObject("Doorway");
        door.transform.SetParent(go.transform, false);
        door.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var dc = door.AddComponent<BoxCollider2D>(); dc.isTrigger = true; dc.size = new Vector2(2.4f, 1.1f);
        door.AddComponent<MineEntrance>();

        // two lanterns on the brackets (needs the lantern art from the Lighting pack; skipped if you don't have it)
        string lanternPath = FindAssetPath("lantern", "Texture2D");
        if (lanternPath != null)
        {
            Sprite lanternSprite = AssetDatabase.LoadAssetAtPath<Sprite>(lanternPath);
            string glowPath = lanternPath.Replace("lantern.png", "lantern_glow.png");
            var glow = new List<Sprite>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(glowPath)) { var s = o as Sprite; if (s != null) glow.Add(s); }
            glow.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (float x in new[] { -2.35f, 2.35f })
            {
                var l = new GameObject("Lantern");
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(x, 3.2f, 0f);
                var lsr = l.AddComponent<SpriteRenderer>(); lsr.sprite = lanternSprite; lsr.sortingOrder = 6;
                if (lit != null) lsr.sharedMaterial = lit;
                var ls = l.AddComponent<LightSource>();
                ls.lightColor = new Color(1f, 0.8f, 0.45f); ls.intensity = 1.3f; ls.outerRadius = 5f; ls.innerRadius = 0.4f;
                ls.lightOffset = new Vector2(0f, 0.65f); ls.behaviour = LightBehaviour.DuskToDawn;
                ls.flickerAmount = 0.08f; ls.flickerSpeed = 6f;
                ls.glowFrames = glow.ToArray(); ls.glowFramesPerSecond = 3f;
            }
        }

        GameObject prefab = SavePrefab(go, dir + "/MineEntrance.prefab");
        AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------
    // Level Editor pieces
    // ------------------------------------------------------------------
    [MenuItem("Farm/Level Editors/Add Mountain Editor Pieces", false, 31)]
    public static void AddPiecesMenu()
    {
        if (!BuildDesignPieces())
            EditorUtility.DisplayDialog("Couldn't add the pieces",
                "Needs two things:\n1. Run Farm > Mountain > Set Up Mountain Trail first (it makes the rocks, Bug and Bat).\n2. Copy the 'Zones' art folder into Assets.", "OK");
        else
            EditorUtility.DisplayDialog("Level Editor pieces ready",
                "Open Farm > Level Editors > Farm Level Editor, choose the Objects layer, open 'Edit palette list' and press 'Reload this palette from labelled assets'.\nYou'll find Zone_Rocks, Zone_Enemies, Marker_KeepClear, Bug, Bat, the rocks and MineEntrance.", "OK");
    }

    /// <summary>Makes zone / marker prefabs and labels everything the Level Editor's Objects palette should list. Returns false if something is missing.</summary>
    private static bool BuildDesignPieces()
    {
        string zoneArt = FindFolder("zone_rock_spawner");
        GameObject bug = FindPrefab("Bug"), bat = FindPrefab("Bat");
        if (zoneArt == null || bug == null || bat == null) return false;

        foreach (string n in new[] { "zone_rock_spawner", "zone_enemy_spawner", "marker_keep_clear" })
            ImportSingle(zoneArt + "/" + n + ".png", SpriteAlignment.Center);
        AssetDatabase.Refresh();

        string dir = zoneArt + "/Prefabs";
        EnsureFolder(dir);

        // the trail rocks, with the same weights the MountainSpawners object uses
        var rockTypes = new List<RockSpawner.RockType>();
        foreach (RockDef d in Rocks())
        {
            if (d.trailWeight <= 0f) continue;
            GameObject p = FindPrefab(d.name);
            if (p != null) rockTypes.Add(new RockSpawner.RockType { prefab = p, weight = d.trailWeight });
        }

        // Zone_Rocks
        var z1 = new GameObject("Zone_Rocks");
        AddIcon(z1, zoneArt + "/zone_rock_spawner.png");
        var rs = z1.AddComponent<RockSpawner>();
        rs.rockTypes = rockTypes.ToArray();
        rs.limitToBox = true; rs.boxSize = new Vector2(24f, 16f);
        rs.targetCount = 12; rs.respawnPerDay = 4;
        GameObject zp1 = SavePrefab(z1, dir + "/Zone_Rocks.prefab");

        // Zone_Enemies
        var z2 = new GameObject("Zone_Enemies");
        AddIcon(z2, zoneArt + "/zone_enemy_spawner.png");
        var es = z2.AddComponent<EnemySpawner>();
        es.enemyTypes = new[]
        {
            new EnemySpawner.EnemyType { prefab = bug, weight = 3f, maxAlive = 3 },
            new EnemySpawner.EnemyType { prefab = bat, weight = 1.5f, maxAlive = 1 },
        };
        es.limitToBox = true; es.boxSize = new Vector2(24f, 16f);
        es.maxAlive = 3; es.startCount = 2; es.spawnOffset = Vector2.zero;
        GameObject zp2 = SavePrefab(z2, dir + "/Zone_Enemies.prefab");

        // Marker_KeepClear
        var m = new GameObject("Marker_KeepClear");
        AddIcon(m, zoneArt + "/marker_keep_clear.png");
        m.AddComponent<KeepClearMarker>();
        GameObject zp3 = SavePrefab(m, dir + "/Marker_KeepClear.prefab");

        foreach (GameObject g in new[] { zp1, zp2, zp3, bug, bat })
            AssetDatabase.SetLabels(g, new[] { FarmEditorLabels.Objects });
        AssetDatabase.SaveAssets();
        return true;
    }

    private static void AddIcon(GameObject go, string spritePath)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        sr.sortingOrder = 100;
    }

    private static GameObject FindPrefab(string name)
    {
        string p = FindAssetPath(name, "Prefab");
        return p == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(p);
    }

    // ------------------------------------------------------------------
    // Test set
    // ------------------------------------------------------------------
    [MenuItem("Farm/Mountain/Drop Test Set Near Player")]
    public static void DropTestSet()
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        Vector3 start = player != null ? player.transform.position + new Vector3(3f, 2f, 0f) : Vector3.zero;
        string[] names = { "Rock_Brown_Small", "Rock_Grey_Large", "Ore_Coal", "Ore_Copper", "Ore_Iron", "Ore_Gold", "Bug", "Bat" };
        int i = 0;
        foreach (string n in names)
        {
            foreach (string guid in AssetDatabase.FindAssets(n + " t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) != n) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p));
                Undo.RegisterCreatedObjectUndo(inst, "Drop test set");
                inst.transform.position = start + new Vector3((i % 4) * 2f, -(i / 4) * 2.5f, 0f);
                i++;
                break;
            }
        }
        if (i == 0) EditorUtility.DisplayDialog("Nothing to drop", "Run Farm > Mountain > Set Up Mountain Trail first.", "OK");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private static ItemData MakeItem(string asset, string name, string desc, int sell, string icon)
    {
        string path = ItemsFolder + "/" + asset + ".asset";
        var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (it == null) { it = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(it, path); }
        it.itemName = name; it.description = desc; it.type = ItemData.ItemType.Resource;
        it.buyPrice = 0; it.sellPrice = sell; it.maxStack = 99;
        it.icon = AssetDatabase.LoadAssetAtPath<Sprite>(artFolder + "/" + icon + ".png");
        EditorUtility.SetDirty(it);
        return it;
    }

    private static Sprite[] Cells(Dictionary<string, Sprite> sheet, int firstCol, int row, int count)
    {
        var list = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            Sprite s;
            if (sheet.TryGetValue((firstCol + i) + "_" + row, out s)) list.Add(s);
        }
        return list.ToArray();
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
        string p = FindAssetPath(fileName, "Texture2D");
        return p == null ? null : Path.GetDirectoryName(p).Replace('\\', '/');
    }

    private static string FindAssetPath(string fileName, string type)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:" + type))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return p;
        }
        return null;
    }

    private static void ImportSingle(string path, SpriteAlignment align)
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
        settings.spriteAlignment = (int)align;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    /// <summary>Cuts a sheet into equal cells. Returns "column_row" (row from the top) -> sprite.</summary>
    private static Dictionary<string, Sprite> SliceSheet(string path, int cellW, int cellH, SpriteAlignment align)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cols = tex.width / cellW, rows = tex.height / cellH;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        UnityEditor.U2D.Sprites.ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var rect = new SpriteRect();
                rect.name = baseName + "_" + c + "_" + r;
                rect.rect = new Rect(c * cellW, tex.height - (r + 1) * cellH, cellW, cellH);
                rect.alignment = align;
                rect.pivot = align == SpriteAlignment.BottomCenter ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
                rect.spriteID = EnemyArtRepair.StableId(baseName, rect.name);     // stable: re-running setup keeps the same sprites, so nothing loses its art
                rects.Add(rect);
            }
        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect sr in rects) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        idProvider.SetNameFileIdPairs(pairs);
        provider.Apply();
        imp.SaveAndReimport();

        var result = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = o as Sprite;
            if (s != null && s.name.StartsWith(baseName + "_")) result[s.name.Substring(baseName.Length + 1)] = s;
        }
        return result;
    }
}
