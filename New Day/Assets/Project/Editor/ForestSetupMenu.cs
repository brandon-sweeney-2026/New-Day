using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Forest > Set Up Forest           (run once, after copying the 'Forest' art folder into Assets)
/// Farm > Forest > Test: Open All Doors    (play mode)
/// Farm > Forest > Test: Reset Forest      (play mode: re-locks doors, clears offerings and fairy quests)
///
/// Set Up Forest imports the art and makes: the Shadow Essence item, the polluted ForestStyle, the five shadow animals (prefabs), the forest room,
/// gates, the stump, the Fairy (with starter quests), a ForestProgression asset, and every grove decoration. All of them are labelled so they
/// show in the Level Editor's Objects palette. Then use Farm > Forest > Maze Generator to build the maze. Safe to run again.
/// </summary>
public static class ForestSetupMenu
{
    public const string DataFolder = "Assets/Farm/Data/Forest";
    private const string ItemsFolder = "Assets/Farm/Data/Items";
    public static string ArtFolder;

    [MenuItem("Farm/Forest/Set Up Forest")]
    public static void Run()
    {
        ArtFolder = FindFolder("floor_dark_0");
        if (ArtFolder == null)
        {
            EditorUtility.DisplayDialog("Forest art not found",
                "Couldn't find floor_dark_0.png in your project.\nCopy the 'Forest' art folder into Assets (for example Assets/Art/Forest) and run this again.", "OK");
            return;
        }

        ImportArt();
        EnsureFolder(DataFolder);
        EnsureFolder(ItemsFolder);
        string prefabDir = ArtFolder + "/Prefabs";
        EnsureFolder(prefabDir);

        ItemData essence = MakeItem("ShadowEssence", "Shadow Essence", "A drop of pollution given shape. It hums faintly.", 15, "icon_shadow_essence");

        ForestStyle polluted = LoadOrCreate<ForestStyle>(DataFolder + "/ForestStyle_Polluted.asset");
        polluted.floorTiles = new[] { Spr("floor_dark_0"), Spr("floor_dark_1"), Spr("floor_dark_2") };
        polluted.wallTile = Spr("wall_thorn");
        polluted.scenery = new[] { Spr("forest_dead_tree_a"), Spr("forest_dead_tree_b"), Spr("forest_dead_bush"), Spr("forest_dark_mushroom") };
        polluted.sceneryCount = 9;
        polluted.gateAcrossLocked = Spr("gate_across_locked"); polluted.gateAcrossOpen = Spr("gate_across_open");
        polluted.gateUpDownLocked = Spr("gate_updown_locked"); polluted.gateUpDownOpen = Spr("gate_updown_open");
        polluted.fog = true;
        EditorUtility.SetDirty(polluted);

        // ---- animals ----
        var animals = new List<GameObject>();
        animals.Add(BuildAnimal(prefabDir, essence, "rabbit", "Rabbit", ShadowCreature.Style.Hop, 8, 2, 1.2f, 5.5f, 0.35f, 0.55f, 0.30f, true, false, 0f));
        animals.Add(BuildAnimal(prefabDir, essence, "squirrel", "Squirrel", ShadowCreature.Style.Dart, 7, 2, 1.6f, 9f, 0.28f, 0.70f, 0.20f, true, true, 0f));
        animals.Add(BuildAnimal(prefabDir, essence, "hedgehog", "Hedgehog", ShadowCreature.Style.Roll, 12, 4, 0.9f, 7.5f, 0.90f, 1.00f, 0.55f, false, false, 0.3f));
        animals.Add(BuildAnimal(prefabDir, essence, "bird", "Bird", ShadowCreature.Style.Dash, 6, 3, 1.8f, 9.5f, 0.45f, 0.55f, 0.35f, true, false, 0f));
        animals.Add(BuildAnimal(prefabDir, essence, "turtle", "Turtle", ShadowCreature.Style.Plod, 26, 3, 0.5f, 1.4f, 0.35f, 0.5f, 0.2f, true, false, 0.8f));

        // ---- room, gates, stump, markers ----
        var room = new GameObject("ForestRoom");
        AddIcon(room, "marker_forest_room");
        var fr = room.AddComponent<ForestRoom>();
        fr.style = polluted;
        fr.enemyPrefabs = animals.ToArray();
        GameObject roomPrefab = SavePrefab(room, prefabDir + "/ForestRoom.prefab");

        GameObject gateA = BuildGate(prefabDir, "ForestGate_Across", polluted, false);
        GameObject gateU = BuildGate(prefabDir, "ForestGate_UpDown", polluted, true);

        var st = new GameObject("ForestStump");
        var ssr = st.AddComponent<SpriteRenderer>();
        ssr.sprite = Spr("forest_stump"); ssr.sortingOrder = 9;
        var sc = st.AddComponent<CircleCollider2D>(); sc.radius = 0.5f; sc.offset = new Vector2(0f, 0.3f);
        var stump = st.AddComponent<ForestStump>();
        stump.openSprite = Spr("forest_stump_awake");
        st.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;
        GameObject stumpPrefab = SavePrefab(st, prefabDir + "/ForestStump.prefab");

        // ---- progression asset (auto-fills what it can find) ----
        ForestProgression prog = LoadOrCreate<ForestProgression>(DataFolder + "/ForestProgression.asset");
        AutoFillProgression(prog);
        EditorUtility.SetDirty(prog);

        // ---- the fairy ----
        GameObject fairy = BuildFairy(prefabDir, prog);

        // ---- grove pieces ----
        var pieces = new List<GameObject>();
        pieces.Add(BuildGroveZone(prefabDir));
        pieces.Add(BuildPiece(prefabDir, "Grove_PondBig", "grove_pond_big", -10, true, 0.86f, false));
        pieces.Add(BuildPiece(prefabDir, "Grove_PondSmall", "grove_pond_small", -10, true, 0.8f, false));
        pieces.Add(BuildPiece(prefabDir, "Grove_FairyRing", "grove_fairy_ring", -15, false, 0f, false));
        pieces.Add(BuildPiece(prefabDir, "Grove_FlowersPink", "grove_flowers_pink", -14, false, 0f, false));
        pieces.Add(BuildPiece(prefabDir, "Grove_FlowersBlue", "grove_flowers_blue", -14, false, 0f, false));
        pieces.Add(BuildPiece(prefabDir, "Grove_FlowersYellow", "grove_flowers_yellow", -14, false, 0f, false));
        pieces.Add(BuildPiece(prefabDir, "Grove_MushroomRed", "grove_mushroom_red", 8, false, 0f, false));
        pieces.Add(BuildGlowMushroom(prefabDir, "Grove_MushroomBlue", "grove_mushroom_blue", new Color(0.4f, 0.65f, 1f)));
        pieces.Add(BuildGlowMushroom(prefabDir, "Grove_MushroomGlow", "grove_mushroom_glow", new Color(0.35f, 1f, 0.65f)));
        pieces.Add(BuildPiece(prefabDir, "Grove_Tree", "grove_tree", 8, false, 0f, true));
        pieces.Add(BuildPiece(prefabDir, "Grove_Bush", "grove_bush", 8, false, 0f, true));
        // polluted scenery too, for decorating the maze by hand
        pieces.Add(BuildPiece(prefabDir, "Forest_DeadTreeA", "forest_dead_tree_a", 8, false, 0f, true));
        pieces.Add(BuildPiece(prefabDir, "Forest_DeadTreeB", "forest_dead_tree_b", 8, false, 0f, true));
        pieces.Add(BuildPiece(prefabDir, "Forest_DeadBush", "forest_dead_bush", 8, false, 0f, true));
        pieces.Add(BuildPiece(prefabDir, "Forest_Puddle", "forest_puddle", -14, false, 0f, false));
        pieces.Add(BuildPiece(prefabDir, "Forest_DarkMushroom", "forest_dark_mushroom", 8, false, 0f, false));

        // ---- label everything the Level Editor's Objects palette should list ----
        var all = new List<GameObject>(pieces);
        all.AddRange(animals);
        all.Add(roomPrefab); all.Add(gateA); all.Add(gateU); all.Add(stumpPrefab); all.Add(fairy);
        foreach (GameObject g in all) if (g != null) AssetDatabase.SetLabels(g, new[] { FarmEditorLabels.Objects });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Forest ready",
            "Done! Made the Shadow Essence item, the forest style, 5 shadow animals, room, gates, stump, the Fairy and the grove pieces.\n\n" +
            "NEXT:\n1. Open " + DataFolder + "/ForestProgression and drag your FARM items and QUARRY items in (easiest first). I filled in what I could find.\n" +
            "2. Open the scene for the forest and run Farm > Forest > Maze Generator.\n" +
            "3. Level Editor > Objects palette > 'Reload this palette from labelled assets' to paint extras.\n\n" +
            "Remember: ground/lighting scripts treat the forest like any scene. Not compiled in Unity - tell me about any red errors.", "OK");
    }

    // ------------------------------------------------------------------ prefab builders
    private static GameObject BuildAnimal(string dir, ItemData essence, string key, string label, ShadowCreature.Style style, int hp, int dmg,
        float wander, float go, float goSec, float rest, float warn, bool aim, bool hitRun, float knockResist)
    {
        var go_ = new GameObject("Shadow" + label);
        var sr = go_.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        Material lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Lighting/SpriteLit.mat");
        if (lit != null) sr.sharedMaterial = lit;
        var rb = go_.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go_.AddComponent<CircleCollider2D>();
        col.radius = style == ShadowCreature.Style.Plod ? 0.4f : 0.28f; col.offset = new Vector2(0f, 0.3f);
        go_.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;

        var c = go_.AddComponent<ShadowCreature>();
        c.style = style; c.enemyName = "Shadow " + label;
        c.maxHealth = hp; c.contactDamage = dmg;
        c.wanderSpeed = wander; c.goSpeed = go; c.goSeconds = goSec; c.restSeconds = rest; c.warnSeconds = warn;
        c.aimBeforeEachBurst = aim; c.hitAndRun = hitRun; c.knockbackResist = knockResist;
        if (style == ShadowCreature.Style.Plod) { c.touchCooldown = 1.2f; c.aggroRange = 5.5f; }
        c.moveFrames = new[] { Cell("shadow_" + key, 0), Cell("shadow_" + key, 1) };
        c.warnFrame = key == "turtle" ? null : Cell("shadow_" + key, 2);
        if (c.moveFrames[0] != null) sr.sprite = c.moveFrames[0];
        c.drops = new[] { new EnemyDrop { item = essence, min = 1, max = style == ShadowCreature.Style.Plod ? 2 : 1, chance = style == ShadowCreature.Style.Plod ? 1f : 0.5f } };
        c.goldMin = 1; c.goldMax = 3;
        c.shadowSize = new Vector2(0.8f, 0.3f); c.shadowOffset = new Vector2(0f, 0.12f);
        return SavePrefab(go_, dir + "/Shadow" + label + ".prefab");
    }

    private static GameObject BuildGate(string dir, string name, ForestStyle style, bool upDown)
    {
        var g = new GameObject(name);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = upDown ? style.gateUpDownLocked : style.gateAcrossLocked;
        sr.sortingOrder = 7;
        var bc = g.AddComponent<BoxCollider2D>();
        bc.size = upDown ? new Vector2(1f, 2f) : new Vector2(2f, 1f);
        var d = g.AddComponent<ForestDoor>();
        d.style = style; d.upDown = upDown; d.id = "door_new";
        return SavePrefab(g, dir + "/" + name + ".prefab");
    }

    private static GameObject BuildFairy(string dir, ForestProgression prog)
    {
        var root = new GameObject("Fairy");
        var pic = new GameObject("Picture");
        pic.transform.SetParent(root.transform, false);
        pic.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var sr = pic.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 12;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 1.1f; col.isTrigger = true;

        var f = root.AddComponent<FairyNpc>();
        f.flyFrames = new[] { Cell("fairy_fly", 0), Cell("fairy_fly", 1) };
        if (f.flyFrames[0] != null) sr.sprite = f.flyFrames[0];
        f.portraitIdle = Cell("fairy_portraits", 0); f.portraitTalk = Cell("fairy_portraits", 1);
        f.portraitHappy = Cell("fairy_portraits", 2); f.portraitBlink = Cell("fairy_portraits", 3);
        f.dialoguePanel = FindSprite("ui_dialog_panel"); f.portraitFrame = FindSprite("ui_dialog_frame");

        ItemData farm = prog.farmItems != null && prog.farmItems.Length > 0 ? prog.farmItems[0] : null;
        ItemData quarry = prog.quarryItems != null && prog.quarryItems.Length > 0 ? prog.quarryItems[0] : null;
        var quests = new List<FairyQuest>();
        quests.Add(new FairyQuest
        {
            id = "shadows_1", title = "Quiet the Shadows", kind = FairyQuest.Kind.ReachCounter,
            counterKey = "shadows_banished", counterTarget = 8, counterLabel = "Shadows banished", rewardGold = 120,
            offerPages = new[] { "The shadow animals were my friends once. Rabbits, squirrels... all twisted by the dark.", "If you banish eight of them, the paths will be safer, and maybe the pollution will loosen its hold." },
            reminder = "Eight shadows, little farmer. They hide in the maze rooms.",
            completePages = new[] { "I can feel it. The forest is breathing a little deeper. Thank you!" },
        });
        quests.Add(new FairyQuest
        {
            id = "stone_1", title = "Cleansing Stones", kind = FairyQuest.Kind.GiveItems,
            items = quarry != null ? new[] { new ForestRequirement { item = quarry, count = 10 } } : new ForestRequirement[0],
            rewardGold = 150,
            offerPages = new[] { "Stone can hold a spell, if the stone is clean. The mountain has plenty.", "Bring me some, and I'll set them around the pond to keep the pollution out." },
            reminder = "Stones from the mountain, please. They must be clean.",
            completePages = new[] { "Perfect. I'll set these around the water tonight." },
        });
        quests.Add(new FairyQuest
        {
            id = "harvest_1", title = "Seeds of Hope", kind = FairyQuest.Kind.GiveItems,
            items = farm != null ? new[] { new ForestRequirement { item = farm, count = 8 } } : new ForestRequirement[0],
            rewardGold = 200,
            offerPages = new[] { "Things grow slowly here now. Fresh produce from the farm carries living magic.", "Could you bring me a basket? I'll plant the seeds where the shadows once stood." },
            reminder = "Fresh things from the farm, please.",
            completePages = new[] { "Oh, these are wonderful. Already the soil feels warmer." },
        });
        quests.Add(new FairyQuest
        {
            id = "shadows_2", title = "Hunters of the Dark", kind = FairyQuest.Kind.ReachCounter,
            counterKey = "shadows_banished", counterTarget = 25, counterLabel = "Shadows banished", rewardGold = 400,
            offerPages = new[] { "There are more. So many more. The pollution keeps making them.", "Twenty-five this time. Then I think we can look for where it all comes from." },
            reminder = "Keep going. Every shadow you free is a friend returned.",
            completePages = new[] { "You've done what I could not for years. Now... we need to find the source." },
        });
        quests.Add(new FairyQuest
        {
            id = "story_placeholder", title = "(Story placeholder) The Source", kind = FairyQuest.Kind.ReachCounter,
            counterKey = "fairy_quests_done", counterTarget = 4, counterLabel = "Quests finished", countFromAccept = false, setFlag = "forest_story_unlocked",
            offerPages = new[] { "[Placeholder] This is where the main story continues: where the pollution comes from, and how to stop it." },
            reminder = "[Placeholder] Rewrite this quest to match your story.",
            completePages = new[] { "[Placeholder] To be continued..." },
        });
        f.quests = quests.ToArray();
        return SavePrefab(root, dir + "/Fairy.prefab");
    }

    private static GameObject BuildGroveZone(string dir)
    {
        var g = new GameObject("Grove_Zone");
        AddIcon(g, "marker_grove");
        g.AddComponent<ForestGrove>();
        return SavePrefab(g, dir + "/Grove_Zone.prefab");
    }

    private static GameObject BuildPiece(string dir, string name, string art, int order, bool block, float blockFraction, bool trunkBlock)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(art); sr.sortingOrder = order;
        if (block && sr.sprite != null)
        {
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(sr.sprite.bounds.size.x * blockFraction, sr.sprite.bounds.size.y * blockFraction);
        }
        if (trunkBlock)
        {
            var bc = go.AddComponent<BoxCollider2D>();
            bool big = art == "grove_tree";
            bc.size = big ? new Vector2(1f, 0.7f) : new Vector2(0.8f, 0.5f);
            bc.offset = new Vector2(0f, big ? 0.35f : 0.25f);
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;
        }
        if (name.StartsWith("Forest_DeadTree")) ForestUpdateMenu.UpgradeDeadTree(go, 2f);       // twice as big, with a shadow
        return SavePrefab(go, dir + "/" + name + ".prefab");
    }

    private static GameObject BuildGlowMushroom(string dir, string name, string art, Color glow)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(art); sr.sortingOrder = 8;
        var l = go.AddComponent<LightSource>();
        l.lightColor = glow; l.intensity = 0.9f; l.outerRadius = 3.2f; l.innerRadius = 0.2f;
        l.lightOffset = new Vector2(0f, 0.5f);
        l.behaviour = LightBehaviour.DimmerByDay; l.daytimeStrength = 0.25f;
        l.flickerAmount = 0f; l.pulseAmount = 0.2f; l.pulseSpeed = 1.2f;
        return SavePrefab(go, dir + "/" + name + ".prefab");
    }

    // ------------------------------------------------------------------ items
    private static void AutoFillProgression(ForestProgression prog)
    {
        var farm = new List<ItemData>();
        var quarry = new List<ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it == null) continue;
            string n = it.itemName.ToLowerInvariant();
            if (n == "stone" || n == "coal" || n == "copper ore" || n == "iron ore") quarry.Add(it);
            else if (it.type == ItemData.ItemType.Crop) farm.Add(it);
        }
        // easiest first
        farm.Sort((a, b) => a.sellPrice.CompareTo(b.sellPrice));
        quarry.Sort((a, b) => a.sellPrice.CompareTo(b.sellPrice));
        if (farm.Count > 8) farm.RemoveRange(8, farm.Count - 8);
        if (prog.farmItems == null || prog.farmItems.Length == 0) prog.farmItems = farm.ToArray();
        if (prog.quarryItems == null || prog.quarryItems.Length == 0) prog.quarryItems = quarry.ToArray();
        if (prog.farmItems.Length == 0 || prog.quarryItems.Length == 0)
            Debug.LogWarning("Forest: couldn't find all the farm / quarry items by name. Drag them into " + DataFolder + "/ForestProgression yourself.");
    }

    private static ItemData MakeItem(string asset, string name, string desc, int sell, string icon)
    {
        string path = ItemsFolder + "/" + asset + ".asset";
        var it = LoadOrCreate<ItemData>(path);
        it.itemName = name; it.description = desc; it.type = ItemData.ItemType.Resource;
        it.buyPrice = 0; it.sellPrice = sell; it.maxStack = 99;
        it.icon = Spr(icon);
        EditorUtility.SetDirty(it);
        return it;
    }

    // ------------------------------------------------------------------ art import
    private static readonly string[] CentreArt =
    {
        "floor_", "wall_thorn", "gate_", "forest_puddle", "grove_pond", "grove_fairy_ring", "marker_", "icon_",
    };

    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n.StartsWith("shadow_")) { SliceSheet(p, 24, 24, SpriteAlignment.BottomCenter); continue; }
            if (n == "fairy_fly") { SliceSheet(p, 24, 24, SpriteAlignment.Center); continue; }
            if (n == "fairy_portraits") { SliceSheet(p, 64, 64, SpriteAlignment.Center); continue; }
            bool centre = false;
            foreach (string c in CentreArt) if (n.StartsWith(c)) { centre = true; break; }
            if (n.StartsWith("forest_") || n.StartsWith("grove_") || n.StartsWith("floor_") || n.StartsWith("wall_") || n.StartsWith("gate_") || n.StartsWith("marker_") || n.StartsWith("icon_"))
                ImportSingle(p, centre ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        }
        AssetDatabase.Refresh();
    }

    private static Sprite Spr(string name)
    {
        string p = ArtFolder + "/" + name + ".png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    /// <summary>One cell of a sliced sheet by column.</summary>
    private static Sprite Cell(string sheet, int col)
    {
        string p = ArtFolder + "/" + sheet + ".png";
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
        {
            var s = o as Sprite;
            if (s != null && s.name == sheet + "_" + col + "_0") return s;
        }
        return null;
    }

    private static Sprite FindSprite(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Sprite"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
        return null;
    }

    private static void AddIcon(GameObject go, string art)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(art);
        sr.sortingOrder = 100;
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static string FindFolder(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return Path.GetDirectoryName(p).Replace('\\', '/');
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

    private static void SliceSheet(string path, int cellW, int cellH, SpriteAlignment align)
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
                rect.spriteID = GUID.Generate();
                rects.Add(rect);
            }
        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect sr in rects) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        idProvider.SetNameFileIdPairs(pairs);
        provider.Apply();
        imp.SaveAndReimport();
    }

    // ------------------------------------------------------------------ test helpers
    [MenuItem("Farm/Forest/Test: Open All Doors")]
    public static void OpenAll()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Play mode needed", "Press Play first.", "OK"); return; }
        foreach (ForestDoor d in Object.FindObjectsByType<ForestDoor>()) d.Unlock();
        Debug.Log("All forest doors opened.");
    }

    [MenuItem("Farm/Forest/Test: Reset Forest")]
    public static void ResetForest()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Play mode needed", "Progress lives in the running game. Press Play first.", "OK"); return; }
        foreach (ForestDoor d in Object.FindObjectsByType<ForestDoor>())
        {
            d.Lock();                                   // plays the closing animation and forgets the door was open
            if (d.requirements != null) for (int i = 0; i < d.requirements.Length; i++) GameState.SetCounter("forest_give_" + d.id + "_" + i, 0);
        }
        var fairy = Object.FindAnyObjectByType<FairyNpc>();
        if (fairy != null && fairy.quests != null)
            foreach (FairyQuest q in fairy.quests) { GameState.SetCounter("fairy_q_" + q.id, 0); }
        GameState.SetFlag("fairy_met", false);
        GameState.SetFlag("sick_fairy_met", false);
        GameState.SetFlag("sick_fairy_promised", false);
        GameState.SetFlag("forest_grove_reached", false);
        Debug.Log("Forest reset. The doors closed again.");
    }
}
