using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > Forest Scene
///
/// Builds the Healing Forest (Assets/Project/Scenes/Forest.unity), which lies to the LEFT of the farm:
///   1. sets up the pictures in Assets/Project/Art/ForestHeal (drawn by Tools/make_forest_heal_art.ps1): 16 pixels per unit,
///      Point filter, animal sheets cut into 24 x 24 frames with STABLE sprite IDs;
///   2. makes the floor tiles, Assets/Resources/ForestHealSettings.asset (tile pairs and animals) and the shadow animal prefabs
///      in Assets/Farm/ForestHeal (the settings and prefabs are KEPT on a rerun; only their pictures are refreshed);
///   3. starts from a copy of the Mine scene (Player, camera, HUD, TimeManager, lighting), emptied;
///   4. paints the forest from the text MAP below: six zones, the creek, the grove and the old woods, walls of trees between
///      the zones, and the trail that winds from the farm to the grove;
///   5. places each zone's heart stump, its blocker (bramble, bridge, goo pool, log pile, thorn arch, fog bank), a sign, and
///      scatters trees, bushes, rocks... that turn healthy when the zone heals;
///   6. adds the sick fairy, her sister in the grove, the pair of fairy doors, the way back to the farm and the fairy's guide.
///
/// Running it again REPLACES Forest.unity.
/// Layout changes belong in the constants below, not in hand edits to the scene.
/// Command line:  -executeMethod ForestSceneBuilder.BuildBatch   (rebuilds and saves a picture to Logs/forest_preview.png)
///                -executeMethod ForestSceneBuilder.PreviewBatch (only the picture).  Leave out -nographics.
/// Must live in a folder named "Editor".
/// </summary>
public static class ForestSceneBuilder
{
    // ------------------------------------------------------------------ where things live
    private const string ScenePath = "Assets/Project/Scenes/Forest.unity";
    private const string TemplateScene = "Assets/Project/Scenes/Mine.unity";
    private const string DataDir = "Assets/Farm/ForestHeal";
    private const string TileDir = DataDir + "/Tiles";
    private const string PrefabDir = DataDir + "/Prefabs";
    private const string SettingsPath = "Assets/Resources/ForestHealSettings.asset";
    private const string ProgressionPath = "Assets/Farm/Data/Forest/ForestProgression.asset";
    private const string LitMaterialPath = "Assets/Resources/Lighting/SpriteLit.mat";

    // ------------------------------------------------------------------ the map
    // One letter = a square of 4 x 4 tiles. The top line is the top (north) of the forest; the farm is off the RIGHT edge.
    //   1-6  the six zones, healed in that order      ~  the creek (it belongs to zone 2)
    //   G    the grove (always healthy)               #  the old woods: dense trees nobody can walk into
    // A wall of trees is grown wherever two different areas meet, except at the gaps listed in Zones / OpenGaps.
    private static readonly string[] Map =
    {
        "#########444444444333333333",
        "#########444444444333333333",
        "#########444444444333333333",
        "#########444444444333333333",
        "#########444444444333333333",
        "#########444444444333333333",
        "#########444444444333333333",
        "#########444444444~~~~~~~~~",
        "#########555555555222222222",
        "#########555555555222222222",
        "#########555555555222222222",
        "GGGGGGGGG555555555222222222",
        "GGGGGGGGG555555555222222222",
        "GGGGGGGGG555555555222222222",
        "GGGGGGGGG555555555222222222",
        "GGGGGGGGG666666666111111111",
        "GGGGGGGGG666666666111111111",
        "GGGGGGGGG666666666111111111",
        "GGGGGGGGG666666666111111111",
        "GGGGGGGGG666666666111111111",
        "GGGGGGGGG666666666111111111",
        "GGGGGGGGG666666666111111111",
    };
    private const int CellTiles = 4;
    private const int W = 27 * CellTiles, H = 22 * CellTiles;     // 108 x 88 tiles
    private const char CreekZone = '2';
    private const int WaterBottom = 57, WaterTop = 58;           // the two rows of water inside the creek band
    private const float GapHalf = 1.5f;                          // every gap in a wall is 3 tiles wide
    private const int Seed = 20261116;                           // change this for a different scatter of trees
    private const int FlatOrder = 5;                             // things lying on the ground (puddles, the bridge): just above every ground tilemap (0-4)
    private const int StandOrder = 8;                            // things standing up (trees, rocks, stumps)
    private const float SceneryDensity = 2.5f;                   // multiplies every zone's Scenery number (more = thicker forest)
    private const int GroundCoverPerZone = 140;                  // grass tufts and little mushrooms sprinkled everywhere off the trail

    // the trail from the farm to the grove: x, y pairs in tiles (it is painted 2 tiles wide)
    private static readonly float[] Trail =
    {
        108,12, 100,12, 94,16, 90,24, 90,31, 88,38, 80,44, 84,52, 86,55, 86,61, 88,66, 96,72, 88,78, 74,76, 64,76, 54,72,
        50,64, 52,58, 52,53, 50,46, 58,40, 56,32, 54,30, 54,25, 52,18, 46,12, 40,13, 32,14, 24,16, 18,21,
    };

    // gaps in a wall with nothing in them: x, y of the gap, and whether the wall runs left-right (h) or up-down (v)
    private static readonly Gap[] OpenGaps = { new Gap(86f, 60, true) };    // the trail climbing off the bridge into zone 3

    // the way back to the farm (right edge), where you arrive, and the fairies
    private static readonly Vector2 FarmSpawn = new Vector2(105f, 12f);
    private static readonly Vector2 SickFairySpot = new Vector2(101f, 7f);
    private static readonly Vector2 TrailDoorSpot = new Vector2(102f, 21f);    // the fairy door at the trailhead
    private static readonly Vector2 GroveDoorSpot = new Vector2(8f, 6f);       // ...and its partner in the grove
    private static readonly Vector2 FairyRing = new Vector2(18f, 20f);

    /// <summary>One zone: who it is, where its stump, sign and blocker go, and what lives and grows there.</summary>
    private class ZoneDef
    {
        public char letter; public string id, name;
        public ZoneBlocker.Kind blocker; public Gap gap;               // the blocker sits in this wall gap and leads on to the next zone
        public Vector2 stump, sign;
        public string[] shadows, critters;                             // shadow animal keys and friendly animal ids
        public int minShadows, maxShadows;
        public float broad, pine, oak, bush, tuft, rock, mushroom, log, puddle;   // how much of each scenery kind (relative)
        public int scenery;                                            // how many scenery pieces to scatter
        public string signText, whisper;
    }

    private static readonly ZoneDef[] Zones =
    {
        new ZoneDef { letter = '1', id = "trailhead", name = "The Trailhead", blocker = ZoneBlocker.Kind.Bramble, gap = new Gap(90f, 28, true),
            stump = new Vector2(96f, 20f), sign = new Vector2(103.5f, 15f), shadows = new[] { "rabbit" }, critters = new[] { "rabbit", "bird" },
            minShadows = 1, maxShadows = 2, broad = 3, pine = 1, oak = 0.5f, bush = 2, tuft = 3, rock = 1, mushroom = 0.5f, log = 0.5f, puddle = 0.5f, scenery = 34,
            signText = "THE TRAILHEAD\nThe old forest path. Something has made the trees sick.",
            whisper = "The old stump hums. This is where the forest's sickness began to spread.\nHeal its heart and the brambles will let you pass." },
        new ZoneDef { letter = '2', id = "creek_bend", name = "Creek Bend", blocker = ZoneBlocker.Kind.Bridge, gap = new Gap(86f, 58, true),
            stump = new Vector2(78f, 48f), sign = new Vector2(93f, 33f), shadows = new[] { "rabbit", "bird" }, critters = new[] { "bird", "turtle", "rabbit" },
            minShadows = 1, maxShadows = 3, broad = 2, pine = 1, oak = 0.5f, bush = 1.5f, tuft = 3, rock = 2, mushroom = 0.5f, log = 0.5f, puddle = 2, scenery = 34,
            signText = "CREEK BEND\nThe creek used to sing here. The old bridge has rotted through.",
            whisper = "The stump is damp and cold. The creek's sickness has soaked into its roots." },
        new ZoneDef { letter = '3', id = "mushroom_hollow", name = "Mushroom Hollow", blocker = ZoneBlocker.Kind.GooPool, gap = new Gap(72f, 76, false),
            stump = new Vector2(98f, 76f), sign = new Vector2(83.5f, 64f), shadows = new[] { "squirrel", "hedgehog" }, critters = new[] { "hedgehog", "squirrel" },
            minShadows = 2, maxShadows = 3, broad = 1.5f, pine = 0.5f, oak = 1.5f, bush = 1, tuft = 2, rock = 1, mushroom = 4, log = 1, puddle = 1, scenery = 38,
            signText = "MUSHROOM HOLLOW\nA quiet hollow where mushrooms grow in rings. Mind the goo.",
            whisper = "Spores drift around the stump like dust. It wants to wake up." },
        new ZoneDef { letter = '4', id = "logging_clearing", name = "Old Logging Clearing", blocker = ZoneBlocker.Kind.LogPile, gap = new Gap(52f, 56, true),
            stump = new Vector2(46f, 74f), sign = new Vector2(66f, 79f), shadows = new[] { "hedgehog", "rabbit", "turtle" }, critters = new[] { "rabbit", "hedgehog", "squirrel" },
            minShadows = 2, maxShadows = 3, broad = 1.5f, pine = 1, oak = 0.5f, bush = 1, tuft = 4, rock = 1, mushroom = 1, log = 3, puddle = 0.5f, scenery = 32,
            signText = "OLD LOGGING CLEARING\nSomeone cut trees here long ago and never came back.",
            whisper = "This stump remembers the saws. Help it grow again." },
        new ZoneDef { letter = '5', id = "bramble_ridge", name = "Bramble Ridge", blocker = ZoneBlocker.Kind.Arch, gap = new Gap(54f, 28, true),
            stump = new Vector2(62f, 36f), sign = new Vector2(48.5f, 50f), shadows = new[] { "squirrel", "bird", "turtle" }, critters = new[] { "squirrel", "bird", "turtle" },
            minShadows = 2, maxShadows = 4, broad = 1, pine = 3, oak = 0.5f, bush = 2, tuft = 2, rock = 2.5f, mushroom = 0.5f, log = 0.5f, puddle = 0.3f, scenery = 36,
            signText = "BRAMBLE RIDGE\nPines and stones. The old arch leads down to the Mire.",
            whisper = "Thorns have grown right through this stump. It aches." },
        new ZoneDef { letter = '6', id = "the_mire", name = "The Mire", blocker = ZoneBlocker.Kind.Fog, gap = new Gap(36f, 14, false),
            stump = new Vector2(44f, 8f), sign = new Vector2(57f, 22f), shadows = new[] { "rabbit", "squirrel", "hedgehog", "bird", "turtle" }, critters = new[] { "turtle", "bird", "rabbit", "hedgehog" },
            minShadows = 3, maxShadows = 4, broad = 2, pine = 0.5f, oak = 1, bush = 1, tuft = 3, rock = 0.5f, mushroom = 1.5f, log = 1, puddle = 3, scenery = 36,
            signText = "THE MIRE\nThe sickest part of the forest. The grove lies beyond the fog.",
            whisper = "The last stump. Beyond the fog, the grove is waiting." },
    };

    /// <summary>The ids of the six zones, in healing order (the test menus use these to reset progress from any scene).</summary>
    public static string[] ZoneIds()
    {
        var ids = new string[Zones.Length];
        for (int i = 0; i < Zones.Length; i++) ids[i] = Zones[i].id;
        return ids;
    }

    /// <summary>A gap in a wall: its middle (x, y in tiles) and whether the wall there runs left-right.</summary>
    private struct Gap
    {
        public float x; public float y; public bool horizontal;
        public Gap(float x, float y, bool horizontal) { this.x = x; this.y = y; this.horizontal = horizontal; }
    }

    // ------------------------------------------------------------------ working state
    private static string art, oldArt;                 // the new pictures, and the old forest folder (fairies, grove pieces)
    private static Material lit;
    private static System.Random rng;
    private static bool[,] used;                       // tiles already taken by the trail, a wall or something placed
    private static bool[,] trail;                      // tiles painted with the trail (ground cover never goes on these)
    private static Transform root, sceneryRoot, wallRoot;
    private static readonly Dictionary<char, ForestZone> zoneObjects = new Dictionary<char, ForestZone>();
    private static readonly Dictionary<string, GameObject> shadowPrefabs = new Dictionary<string, GameObject>();

    // ------------------------------------------------------------------ entry points
    [MenuItem("Farm/Set Up/Forest Scene", false, 139)]
    public static void BuildFromMenu()
    {
        const string note = "Building again REPLACES Forest.unity (anything placed by hand in it is lost).";
        if (!EditorUtility.DisplayDialog("Build the Healing Forest?", note + "\n\nThe settings and prefabs in " + DataDir + " and Assets/Resources/ForestHealSettings.asset are kept.", "Build it", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string problem = Build(false);
        EditorUtility.DisplayDialog(problem == null ? "Forest ready" : "Couldn't build the forest",
            problem ?? ("Built " + ScenePath + ".\n\nPress Play on the Farm and take the path on its left edge. The six zones heal in order: Trailhead, Creek Bend, " +
                        "Mushroom Hollow, Old Logging Clearing, Bramble Ridge, The Mire, then the grove.\n\nWhat each stump asks for: " + ProgressionPath +
                        "\nTiles, wave and animals: " + SettingsPath), "OK");
    }

    /// <summary>Command line: no questions asked. Look for "FOREST BUILD OK" in the log.</summary>
    public static void BuildBatch()
    {
        string problem = Build(true);
        Debug.Log(problem == null ? "FOREST BUILD OK" : "FOREST BUILD FAILED: " + problem);
    }

    /// <summary>Command line only: opens the saved Forest scene and takes the overhead picture without rebuilding.</summary>
    public static void PreviewBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SavePreview();
    }

    // ------------------------------------------------------------------ the build
    private static string Build(bool preview)
    {
        art = HardwareEditorUtil.FindFolder("ground_0_sick");
        if (art == null) return "The Healing Forest pictures were not found (ground_0_sick.png and friends belong in Assets/Project/Art/ForestHeal). Run Tools/make_forest_heal_art.ps1 first.";
        oldArt = HardwareEditorUtil.FindFolder("fairy_sick");
        if (!File.Exists(TemplateScene)) return "The Mine scene (" + TemplateScene + ") is used as the starting point, and it is missing.";
        foreach (string line in Map) if (line.Length != W / CellTiles) return "Every line of the MAP must be " + (W / CellTiles) + " letters long.";

        rng = new System.Random(Seed);
        used = new bool[W, H];
        trail = new bool[W, H];
        zoneObjects.Clear();
        shadowPrefabs.Clear();

        // ---- pictures, tiles, settings, prefabs ----
        ImportArt();
        HardwareEditorUtil.EnsureFolder(TileDir);
        HardwareEditorUtil.EnsureFolder(PrefabDir);
        HardwareEditorUtil.EnsureFolder("Assets/Resources");
        ForestHealSettings settings = MakeSettings();
        MakeShadowPrefabs();
        AssetDatabase.SaveAssets();

        // ---- start from a copy of the Mine scene ----
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(TemplateScene, ScenePath)) return "Could not copy the Mine scene to " + ScenePath + ".";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CleanTemplate(scene);
        lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);     // (opening a scene can unload assets, so load it again)
        settings = AssetDatabase.LoadAssetAtPath<ForestHealSettings>(SettingsPath);

        Tilemap ground = FindMap("Ground"), paths = FindMap("Paths");
        if (ground == null || paths == null) return "The copied scene has no 'Ground' or 'Paths' tilemap under its Grid.";

        root = new GameObject("HealingForest").transform;
        sceneryRoot = new GameObject("Scenery").transform; sceneryRoot.SetParent(root, false);
        wallRoot = new GameObject("Walls").transform; wallRoot.SetParent(root, false);

        PaintGround(ground);
        PaintTrail(paths);
        ReserveSpots();                   // before anything is scattered: keep the fairies, doors and the arrival spot clear
        MakeZones();
        BuildWalls();
        BuildCreek();
        BuildOldWoods();
        PlaceZoneThings();
        BuildGrove();
        Scatter();
        MakeEdgesAndEntrance();
        AddFairies();
        new GameObject("ForestGuide").AddComponent<ForestGuide>();          // the sick fairy's directions (it reads the zones)
        SetUpPlayerAndCamera(ground);

        ground.RefreshAllTiles(); paths.RefreshAllTiles();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        if (preview) SavePreview();
        return null;
    }

    // ------------------------------------------------------------------ pictures
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            // the animal sheets are cut into 24 x 24 frames with STABLE sprite IDs (SliceSheet uses StableSpriteId)
            if (n.StartsWith("critter_")) HardwareEditorUtil.SliceSheet(p, 24, 24, SpriteAlignment.BottomCenter);
            // floor tiles turn around their middle; everything that stands up has its pivot at its base
            else HardwareEditorUtil.ImportSingle(p, IsTile(n) ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        }
        AssetDatabase.Refresh();
    }

    private static bool IsTile(string n) { return n.StartsWith("ground_") || n.StartsWith("path_") || n.StartsWith("water_"); }
    private static Sprite Spr(string name) { return HardwareEditorUtil.Spr(art, name); }

    private static Tile MakeTile(string name)
    {
        var tile = HardwareEditorUtil.LoadOrCreate<Tile>(TileDir + "/" + name + ".asset");
        tile.sprite = Spr(name);
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    /// <summary>The 6 frames of one row of an animal sheet (row 0 idle, 1 move, 2 special).</summary>
    private static Sprite[] Frames(string sheet, int row)
    {
        var list = new List<Sprite>();
        for (int c = 0; c < 6; c++) { Sprite s = HardwareEditorUtil.Frame(art, sheet, c, row); if (s != null) list.Add(s); }
        return list.ToArray();
    }

    // ------------------------------------------------------------------ settings and prefabs
    private static ForestHealSettings MakeSettings()
    {
        var s = HardwareEditorUtil.LoadOrCreate<ForestHealSettings>(SettingsPath);
        // the tile pairs are always refreshed (they only point at pictures)
        var pairs = new List<ForestHealSettings.TilePair>();
        for (int v = 0; v < 3; v++) pairs.Add(new ForestHealSettings.TilePair { sick = MakeTile("ground_" + v + "_sick"), healthy = MakeTile("ground_" + v + "_healthy") });
        for (int v = 0; v < 2; v++) pairs.Add(new ForestHealSettings.TilePair { sick = MakeTile("path_" + v + "_sick"), healthy = MakeTile("path_" + v + "_healthy") });
        for (int v = 0; v < 2; v++) pairs.Add(new ForestHealSettings.TilePair { sick = MakeTile("water_" + v + "_sick"), healthy = MakeTile("water_" + v + "_healthy") });
        s.tilePairs = pairs.ToArray();

        // the animals: made once with starting numbers; on a rerun only their pictures are refreshed (your numbers are kept)
        string[] ids = { "rabbit", "squirrel", "hedgehog", "bird", "turtle" };
        ShadowCreature.Style[] styles = { ShadowCreature.Style.Hop, ShadowCreature.Style.Dart, ShadowCreature.Style.Roll, ShadowCreature.Style.Dash, ShadowCreature.Style.Plod };
        float[] speed = { 1.4f, 1.8f, 0.8f, 1.2f, 0.5f }, fps = { 10f, 12f, 8f, 10f, 6f }, hop = { 0.12f, 0.08f, 0f, 0.1f, 0f };
        bool[] tame = { false, false, false, false, true };
        var list = new List<ForestHealSettings.CritterArt>(s.critters ?? new ForestHealSettings.CritterArt[0]);
        for (int i = 0; i < ids.Length; i++)
        {
            ForestHealSettings.CritterArt c = list.Find(a => a != null && a.id == ids[i]);
            if (c == null)
            {
                c = new ForestHealSettings.CritterArt { id = ids[i], shadowStyle = styles[i], walkSpeed = speed[i], framesPerSecond = fps[i], hopHeight = hop[i], tame = tame[i] };
                list.Add(c);
            }
            c.idleFrames = Frames("critter_" + ids[i], 0);
            c.moveFrames = Frames("critter_" + ids[i], 1);
            c.specialFrames = Frames("critter_" + ids[i], 2);
            if (c.idleFrames.Length == 0) Debug.LogWarning("Forest: the frames of critter_" + ids[i] + ".png were not found.");
        }
        s.critters = list.ToArray();
        EditorUtility.SetDirty(s);
        return s;
    }

    /// <summary>
    /// The shadow animals of the Healing Forest: copies of the old shadow animal prefabs (same health, speed and attacks)
    /// with the new pictures (critter_<animal>_shadow), so each one turns into its friendly twin frame for frame.
    /// A copy that already exists is kept (your changes to its numbers stay); only its pictures are refreshed.
    /// </summary>
    private static void MakeShadowPrefabs()
    {
        string[] keys = { "rabbit", "squirrel", "hedgehog", "bird", "turtle" };
        string[] labels = { "Rabbit", "Squirrel", "Hedgehog", "Bird", "Turtle" };
        for (int i = 0; i < keys.Length; i++)
        {
            string path = PrefabDir + "/HealShadow" + labels[i] + ".prefab";
            if (!File.Exists(path))
            {
                string old = oldArt != null ? oldArt + "/Prefabs/Shadow" + labels[i] + ".prefab" : null;
                if (old == null || !File.Exists(old)) { Debug.LogWarning("Forest: " + old + " not found (run Farm > Set Up > Forest once), so zones have no shadow " + keys[i] + "."); continue; }
                AssetDatabase.CopyAsset(old, path);
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            var sc = contents.GetComponent<ShadowCreature>();
            if (sc != null)
            {
                string sheet = "critter_" + keys[i] + "_shadow";
                sc.idleFrames = Frames(sheet, 0);
                sc.moveFrames = Frames(sheet, 1);
                Sprite[] special = Frames(sheet, 2);
                // the warning pose: the rabbit / squirrel crouch, the hedgehog curls up, the bird flaps; the turtle never warns
                sc.warnFrame = keys[i] == "turtle" || special.Length < 3 ? null : special[keys[i] == "hedgehog" ? 2 : 1];
                var sr = contents.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sc.idleFrames.Length > 0) sr.sprite = sc.idleFrames[0];
            }
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            if (saved != null) { shadowPrefabs[keys[i]] = saved; AssetDatabase.SetLabels(saved, new[] { FarmEditorLabels.Objects }); }
        }
    }

    // ------------------------------------------------------------------ template clean-up (as the city and suburbs do)
    private static void CleanTemplate(Scene scene)
    {
        var keep = new HashSet<string> { "Player", "Main Camera", "Objects", "PollutionMap", "TimeManager", "GameHUD", "Grid", "LightingManager" };
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            bool isUi = r.GetComponentInChildren<Canvas>(true) != null || r.GetComponentInChildren<Camera>(true) != null;
            if (!keep.Contains(r.name) && !isUi) Object.DestroyImmediate(r);
        }
        GameObject objects = GameObject.Find("Objects");
        if (objects != null)
            for (int i = objects.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(objects.transform.GetChild(i).gameObject);
        GameObject grid = GameObject.Find("Grid");
        if (grid != null)
            foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true)) { map.ClearAllTiles(); map.CompressBounds(); }
    }

    private static Tilemap FindMap(string name)
    {
        GameObject grid = GameObject.Find("Grid");
        if (grid == null) return null;
        foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true)) if (map.name == name) return map;
        return null;
    }

    // ------------------------------------------------------------------ reading the map
    /// <summary>The map letter at a tile (outside the map = '#').</summary>
    private static char At(int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return '#';
        return Map[(H - 1 - y) / CellTiles][x / CellTiles];
    }

    /// <summary>The area a tile belongs to for walls and zones: the creek counts as its zone.</summary>
    private static char Area(int x, int y) { char c = At(x, y); return c == '~' ? CreekZone : c; }
    private static bool IsZone(char c) { return c >= '1' && c <= '6'; }
    private static bool IsWater(int x, int y) { return At(x, y) == '~' && y >= WaterBottom && y <= WaterTop; }

    // ------------------------------------------------------------------ ground and trail
    private static void PaintGround(Tilemap ground)
    {
        var sick = new Tile[3]; var healthy = new Tile[3];
        for (int v = 0; v < 3; v++) { sick[v] = MakeTile("ground_" + v + "_sick"); healthy[v] = MakeTile("ground_" + v + "_healthy"); }
        Tile[] water = { MakeTile("water_0_sick"), MakeTile("water_1_sick") };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                char a = At(x, y);
                // mostly the plain tile, with the speckled ones here and there
                int v = rng.NextDouble() < 0.6 ? 0 : 1 + rng.Next(2);
                TileBase t = IsWater(x, y) ? water[rng.Next(2)] : (IsZone(Area(x, y)) ? sick[v] : healthy[v]);
                ground.SetTile(new Vector3Int(x, y, 0), t);
                if (IsWater(x, y) || a == '#') used[x, y] = true;
            }
    }

    private static void PaintTrail(Tilemap paths)
    {
        Tile[] sick = { MakeTile("path_0_sick"), MakeTile("path_1_sick") };
        Tile[] healthy = { MakeTile("path_0_healthy"), MakeTile("path_1_healthy") };
        for (int i = 0; i + 3 < Trail.Length; i += 2)
        {
            var a = new Vector2(Trail[i], Trail[i + 1]); var b = new Vector2(Trail[i + 2], Trail[i + 3]);
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 3f);
            for (int k = 0; k <= steps; k++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)k / steps);
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int x = Mathf.FloorToInt(p.x + 0.5f) + dx, y = Mathf.FloorToInt(p.y + 0.5f) + dy;
                        if (x < 0 || y < 0 || x >= W || y >= H || IsWater(x, y)) continue;
                        Tile[] set = IsZone(Area(x, y)) ? sick : healthy;
                        paths.SetTile(new Vector3Int(x, y, 0), set[(x * 7 + y * 3) % 2]);
                        trail[x, y] = true;
                    }
                // keep the trail and a tile either side clear of scenery
                Block(p.x - 2.5f, p.y - 2.5f, p.x + 1.5f, p.y + 1.5f);
            }
        }
    }

    /// <summary>Keeps the special spots free of trees: where you arrive, the fairies, both fairy doors, the fairy ring and the ponds.</summary>
    private static void ReserveSpots()
    {
        Block(FarmSpawn.x - 3f, FarmSpawn.y - 3f, W, FarmSpawn.y + 3f);
        Block(SickFairySpot.x - 2f, SickFairySpot.y - 2f, SickFairySpot.x + 2f, SickFairySpot.y + 2f);
        Block(TrailDoorSpot.x - 2f, TrailDoorSpot.y - 2f, TrailDoorSpot.x + 2f, TrailDoorSpot.y + 3f);
        Block(GroveDoorSpot.x - 2f, GroveDoorSpot.y - 2f, GroveDoorSpot.x + 2f, GroveDoorSpot.y + 3f);
        Block(FairyRing.x - 4f, FairyRing.y - 3f, FairyRing.x + 4f, FairyRing.y + 5f);
        Block(3f, 28f, 15f, 38f); Block(24f, 27f, 32f, 35f);          // the two grove ponds
    }

    // ------------------------------------------------------------------ zones
    private static void MakeZones()
    {
        ForestProgression prog = AssetDatabase.LoadAssetAtPath<ForestProgression>(ProgressionPath);
        if (prog == null) Debug.LogWarning("Forest: " + ProgressionPath + " not found, so the stumps ask for nothing. Run Farm > Set Up > Forest to make it.");
        var parent = new GameObject("Zones").transform;
        parent.SetParent(root, false);
        for (int i = 0; i < Zones.Length; i++)
        {
            ZoneDef d = Zones[i];
            RectInt box = BoundsOf(d.letter);
            var go = new GameObject("Zone" + (i + 1) + "_" + d.id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(box.x + box.width * 0.5f, box.y + box.height * 0.5f, 0f);
            var z = go.AddComponent<ForestZone>();
            z.id = d.id; z.displayName = d.name; z.order = i + 1;
            z.size = new Vector2(box.width, box.height);
            z.requirements = prog != null ? prog.For(i, Zones.Length).ToArray() : new ForestRequirement[0];
            var wants = new List<string>();
            foreach (ForestRequirement r in z.requirements) if (r != null && r.item != null) wants.Add(r.item.itemName + " x" + r.count);
            Debug.Log("FORESTZONE|" + (i + 1) + "|" + d.name + "|wants: " + (wants.Count > 0 ? string.Join(", ", wants) : "nothing"));
            var list = new List<GameObject>();
            foreach (string k in d.shadows) { GameObject p; if (shadowPrefabs.TryGetValue(k, out p) && p != null) list.Add(p); }
            z.shadowPrefabs = list.ToArray();
            z.minShadows = d.minShadows; z.maxShadows = d.maxShadows;
            z.critterIds = d.critters;
            z.minCritters = 2; z.maxCritters = 4;
            zoneObjects[d.letter] = z;
        }
    }

    /// <summary>The rectangle (in tiles) covering every tile of an area letter (the creek counts as zone 2).</summary>
    private static RectInt BoundsOf(char letter)
    {
        int x0 = W, y0 = H, x1 = -1, y1 = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (Area(x, y) == letter) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
        return x1 < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    private static ZoneDef DefOf(char letter) { foreach (ZoneDef d in Zones) if (d.letter == letter) return d; return null; }
    private static ForestZone ZoneOf(char letter) { ForestZone z; return zoneObjects.TryGetValue(letter, out z) ? z : null; }

    // ------------------------------------------------------------------ walls of trees between areas
    private static bool InGap(float x, float y, bool horizontal)
    {
        foreach (ZoneDef d in Zones) if (Near(d.gap, x, y, horizontal)) return true;
        foreach (Gap g in OpenGaps) if (Near(g, x, y, horizontal)) return true;
        return false;
    }
    private static bool Near(Gap g, float x, float y, bool horizontal)
    {
        if (g.horizontal != horizontal) return false;
        return horizontal ? Mathf.Approximately(g.y, y) && Mathf.Abs(x + 0.5f - g.x) < GapHalf + 0.01f
                          : Mathf.Approximately(g.x, x) && Mathf.Abs(y + 0.5f - g.y) < GapHalf + 0.01f;
    }

    /// <summary>The zone that owns a wall between areas a and b: the one healed first (the grove and the old woods own nothing).</summary>
    private static char WallOwner(char a, char b)
    {
        if (IsZone(a) && IsZone(b)) return a < b ? a : b;
        return IsZone(a) ? a : (IsZone(b) ? b : 'G');
    }

    private static void BuildWalls()
    {
        // left-right walls: between the tile row y-1 and the row y
        for (int y = 1; y < H; y++)
        {
            int runStart = -1; char owner = ' ';
            for (int x = 0; x <= W; x++)
            {
                bool wall = false; char o = ' ';
                if (x < W)
                {
                    char a = Area(x, y), b = Area(x, y - 1);
                    wall = a != b && a != '#' && b != '#' && !InGap(x, y, true);
                    o = WallOwner(a, b);
                }
                if (wall && runStart < 0) { runStart = x; owner = o; }
                if ((!wall || o != owner) && runStart >= 0) { WallRun(runStart, y, x - runStart, true, owner); runStart = wall ? x : -1; owner = o; }
            }
        }
        // up-down walls: between the tile column x-1 and the column x
        for (int x = 1; x < W; x++)
        {
            int runStart = -1; char owner = ' ';
            for (int y = 0; y <= H; y++)
            {
                bool wall = false; char o = ' ';
                if (y < H)
                {
                    char a = Area(x, y), b = Area(x - 1, y);
                    wall = a != b && a != '#' && b != '#' && !InGap(x, y, false);
                    o = WallOwner(a, b);
                }
                if (wall && runStart < 0) { runStart = y; owner = o; }
                if ((!wall || o != owner) && runStart >= 0) { WallRun(x, runStart, y - runStart, false, owner); runStart = wall ? y : -1; owner = o; }
            }
        }
    }

    /// <summary>One straight piece of wall: a collider strip plus a row of trees standing on it.</summary>
    private static void WallRun(int x, int y, int length, bool horizontal, char owner)
    {
        if (length <= 0) return;
        var go = new GameObject("Wall");
        go.transform.SetParent(wallRoot, false);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = horizontal ? new Vector2(length, 1.2f) : new Vector2(1.2f, length);
        box.offset = horizontal ? new Vector2(x + length * 0.5f, y) : new Vector2(x, y + length * 0.5f);

        // two staggered rows of trees, a little jumbled, so it looks like thick woods and not a fence
        for (int row = 0; row < 2; row++)
            for (float t = 0.4f + row * 0.7f; t < length; t += 1.3f)
            {
                float jitter = (float)(rng.NextDouble() - 0.5) * 0.5f;
                float across = row == 0 ? 0.5f : -0.6f;                       // one row each side of the line
                Vector2 p = horizontal ? new Vector2(x + t + jitter, y + across - 0.3f) : new Vector2(x + across + jitter, y + t - 0.6f);
                string kind = rng.NextDouble() < 0.5 ? "tree_pine" : (rng.NextDouble() < 0.8 ? "tree_broad" : "tree_oak");
                Scenery(wallRoot, kind, p, owner, false, rng.Next(2) == 0);
            }
        if (horizontal) Block(x - 1, y - 2, x + length, y + 1); else Block(x - 2, y - 1, x + 1, y + length);
    }

    // ------------------------------------------------------------------ the creek, the old woods, the map edges
    private static void BuildCreek()
    {
        var go = new GameObject("Creek");
        go.transform.SetParent(root, false);
        // solid water, except under the bridge (the bridge's own collider fills that gap until the bridge is mended)
        Gap bridge = Zones[1].gap;
        int x0 = W, x1 = -1;
        for (int x = 0; x < W; x++) if (IsWater(x, WaterBottom)) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); }
        if (x1 < 0) return;
        float h = WaterTop - WaterBottom + 1, cy = WaterBottom + h * 0.5f;
        float gapL = bridge.x - GapHalf, gapR = bridge.x + GapHalf;
        AddBox(go, new Vector2((x0 + gapL) * 0.5f, cy), new Vector2(gapL - x0, h));
        AddBox(go, new Vector2((gapR + x1 + 1) * 0.5f, cy), new Vector2(x1 + 1 - gapR, h));
        // a few reeds and stones on the banks
        for (int i = 0; i < 14; i++)
        {
            float x = x0 + (float)rng.NextDouble() * (x1 - x0);
            if (Mathf.Abs(x - bridge.x) < 3f) continue;
            float y = rng.Next(2) == 0 ? WaterBottom - 0.6f : WaterTop + 1.2f;
            Scenery(sceneryRoot, rng.Next(3) == 0 ? "rock" : "tuft", new Vector2(x, y), CreekZone, false, false);
        }
    }

    /// <summary>The '#' area: ancient, healthy woods, too thick to walk into.</summary>
    private static void BuildOldWoods()
    {
        RectInt box = BoundsOf('#');
        if (box.width == 0) return;
        var go = new GameObject("OldWoods");
        go.transform.SetParent(root, false);
        AddBox(go, box.center, new Vector2(box.width, box.height));
        for (float y = box.yMax - 1.5f; y >= box.y - 0.5f; y -= 2.2f)
            for (float x = box.x + 0.5f; x < box.xMax; x += 2.4f)
            {
                var p = new Vector2(x + (float)(rng.NextDouble() - 0.5), y + (float)(rng.NextDouble() - 0.5) * 0.8f);
                if (At(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)) != '#') continue;
                Scenery(go.transform, rng.NextDouble() < 0.6 ? "tree_pine" : "tree_oak", p, 'G', false, false);
            }
    }

    private static void MakeEdgesAndEntrance()
    {
        var edges = new GameObject("MapEdges");
        edges.transform.SetParent(root, false);
        AddBox(edges, new Vector2(-0.5f, H * 0.5f), new Vector2(1f, H + 2f));
        AddBox(edges, new Vector2(W * 0.5f, -0.5f), new Vector2(W + 2f, 1f));
        AddBox(edges, new Vector2(W * 0.5f, H + 0.5f), new Vector2(W + 2f, 1f));
        // the right edge is open only where the trail comes in from the farm
        float doorLow = FarmSpawn.y - 2.5f, doorHigh = FarmSpawn.y + 2.5f;
        AddBox(edges, new Vector2(W + 0.5f, doorLow * 0.5f), new Vector2(1f, doorLow));
        AddBox(edges, new Vector2(W + 0.5f, (doorHigh + H) * 0.5f), new Vector2(1f, H - doorHigh));

        // trees along the edges (not in the old woods: it is all trees already)
        for (float t = 1f; t < H; t += 1.8f)
        {
            if (Mathf.Abs(t - FarmSpawn.y) > 3.5f) EdgeTree(new Vector2(W - 0.6f, t));
            EdgeTree(new Vector2(0.6f, t));
        }
        for (float t = 1f; t < W; t += 1.8f) { EdgeTree(new Vector2(t, H - 1.2f)); EdgeTree(new Vector2(t, 0.1f)); }

        // the way back to the farm, and where you arrive from it
        var exit = new GameObject("ExitToFarm");
        exit.transform.SetParent(root, false);
        exit.transform.position = new Vector3(W - 0.75f, FarmSpawn.y, 0f);
        exit.transform.localScale = new Vector3(1.5f, 5f, 1f);
        var se = exit.AddComponent<SceneExit>();
        exit.GetComponent<BoxCollider2D>().isTrigger = true;
        Set(se, "sceneName", "Farm"); Set(se, "arriveAt", "ForestFarmEntry"); Set(se, "promptText", "Back to the farm");
        SetBool(se, "requireInteract", true);

        var spawn = new GameObject("FromFarm");
        spawn.transform.SetParent(root, false);
        spawn.transform.position = FarmSpawn;
        var sp = spawn.AddComponent<SceneSpawnPoint>();
        Set(sp, "spawnId", "FromFarm"); SetBool(sp, "isDefault", true);
        var so = new SerializedObject(sp);
        so.FindProperty("faceDirection").vector2Value = Vector2.left;
        so.ApplyModifiedPropertiesWithoutUndo();
        Block(FarmSpawn.x - 3f, FarmSpawn.y - 3f, W, FarmSpawn.y + 3f);
    }

    private static void EdgeTree(Vector2 p)
    {
        char a = Area(Mathf.Clamp(Mathf.FloorToInt(p.x), 0, W - 1), Mathf.Clamp(Mathf.FloorToInt(p.y), 0, H - 1));
        if (a == '#') return;
        Scenery(wallRoot, rng.NextDouble() < 0.5 ? "tree_pine" : "tree_broad", p + new Vector2((float)(rng.NextDouble() - 0.5) * 0.4f, 0f), a, false, false);
    }

    // ------------------------------------------------------------------ stumps, blockers, signs
    private static void PlaceZoneThings()
    {
        GameObject signPrefab = ForestToolsMenu.EnsureSignPrefab();
        foreach (ZoneDef d in Zones)
        {
            ForestZone zone = ZoneOf(d.letter);
            var parent = zone.transform;

            // the heart stump
            var st = Thing(parent, "HeartStump", "heart_stump_sick", d.stump, 8);
            var box = st.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 0.5f); box.offset = new Vector2(0f, 0.3f);
            var stump = st.AddComponent<ForestStump>();
            stump.zone = zone;
            stump.openSprite = Spr("heart_stump_healthy");
            stump.whisper = d.whisper;
            st.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;
            st.AddComponent<KeepClearMarker>().radius = 2.5f;
            Block(d.stump.x - 2f, d.stump.y - 1.5f, d.stump.x + 2f, d.stump.y + 2f);

            PlaceBlocker(d, zone);

            // a sign where you come into the zone
            if (signPrefab != null)
            {
                var sign = (GameObject)PrefabUtility.InstantiatePrefab(signPrefab);
                sign.transform.SetParent(parent, false);
                sign.transform.position = d.sign;
                var fs = sign.GetComponent<ForestSign>();
                if (fs != null)
                {
                    string[] lines = d.signText.Split('\n');
                    fs.signTitle = lines[0];
                    fs.pages = new[] { lines.Length > 1 ? lines[1] : d.name };
                }
                Block(d.sign.x - 1f, d.sign.y - 1f, d.sign.x + 1f, d.sign.y + 1.5f);
            }
        }
    }

    private static void PlaceBlocker(ZoneDef d, ForestZone zone)
    {
        string pic = "blocker_" + PictureOf(d.blocker);
        Gap g = d.gap;
        Vector2 pos; Vector2 colCentre; Vector2 colSize; int order = StandOrder;
        switch (d.blocker)
        {
            case ZoneBlocker.Kind.Bridge:                       // lies across the water, below the player
                pos = new Vector2(g.x, WaterBottom - 0.5f);
                colCentre = new Vector2(g.x, (WaterBottom + WaterTop + 1) * 0.5f); colSize = new Vector2(GapHalf * 2f, WaterTop - WaterBottom + 1);
                order = FlatOrder;
                break;
            case ZoneBlocker.Kind.GooPool:                      // flat on the ground in an up-down wall
                pos = new Vector2(g.x, g.y - 1f);
                colCentre = new Vector2(g.x, g.y); colSize = new Vector2(1.4f, GapHalf * 2f);
                order = FlatOrder;
                break;
            case ZoneBlocker.Kind.Fog:                          // a bank of fog hanging in an up-down wall, over everything
                pos = new Vector2(g.x, g.y - 1.2f);
                colCentre = new Vector2(g.x, g.y); colSize = new Vector2(1.4f, GapHalf * 2f);
                order = 45;
                break;
            default:                                            // bramble, log pile, arch: standing in a left-right wall
                pos = new Vector2(g.x, g.y - 0.7f);
                colCentre = new Vector2(g.x, g.y); colSize = new Vector2(GapHalf * 2f, 1.4f);
                break;
        }

        var go = Thing(zone.transform, "Blocker_" + d.blocker, pic + "_sick", pos, order);
        if (d.blocker == ZoneBlocker.Kind.Fog) { go.transform.localScale = new Vector3(1.4f, 1.4f, 1f); go.AddComponent<LightingExempt>(); }
        var col = go.AddComponent<BoxCollider2D>();
        Vector3 s = go.transform.localScale;
        col.size = new Vector2(colSize.x / s.x, colSize.y / s.y);
        col.offset = new Vector2((colCentre.x - pos.x) / s.x, (colCentre.y - pos.y) / s.y);
        var zb = go.AddComponent<ZoneBlocker>();
        zb.kind = d.blocker;
        zb.zone = zone;
        zb.closedSprite = Spr(pic + "_sick");
        zb.openSprite = d.blocker == ZoneBlocker.Kind.Fog ? null : Spr(pic + "_healthy");
        zb.blockCollider = col;
        if (order == StandOrder) go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.5f;

        // the arch's two legs always block (only the opening between them clears)
        if (d.blocker == ZoneBlocker.Kind.Arch)
        {
            col.size = new Vector2(1.6f, 1.4f);
            var legs = new GameObject("ArchLegs");
            legs.transform.SetParent(go.transform, false);
            AddBox(legs, new Vector2(-1.25f, 0.7f), new Vector2(0.7f, 1.4f));
            AddBox(legs, new Vector2(1.25f, 0.7f), new Vector2(0.7f, 1.4f));
        }
        Block(pos.x - 2.5f, pos.y - 2f, pos.x + 2.5f, pos.y + 3f);
    }

    private static string PictureOf(ZoneBlocker.Kind k)
    {
        switch (k)
        {
            case ZoneBlocker.Kind.Bridge: return "bridge";
            case ZoneBlocker.Kind.GooPool: return "goo";
            case ZoneBlocker.Kind.LogPile: return "logpile";
            case ZoneBlocker.Kind.Arch: return "arch";
            case ZoneBlocker.Kind.Fog: return "fog";
            default: return "bramble";
        }
    }

    // ------------------------------------------------------------------ the grove
    private static void BuildGrove()
    {
        RectInt box = BoundsOf('G');
        var grove = new GameObject("Grove");
        grove.transform.SetParent(root, false);
        grove.transform.position = new Vector3(box.center.x, box.center.y, 0f);
        var fg = grove.AddComponent<ForestGrove>();
        fg.boxSize = new Vector2(box.width, box.height);
        fg.groveName = "The Grove";

        string pf = oldArt != null ? oldArt + "/Prefabs/" : null;
        Put(grove.transform, pf, "Grove_FairyRing", FairyRing);
        Put(grove.transform, pf, "Grove_PondBig", new Vector2(9f, 33f));
        Put(grove.transform, pf, "Grove_PondSmall", new Vector2(28f, 31f));
        Block(FairyRing.x - 4f, FairyRing.y - 3f, FairyRing.x + 4f, FairyRing.y + 5f);
        Block(3f, 28f, 15f, 38f); Block(24f, 27f, 32f, 35f);

        string[] flowers = { "Grove_FlowersPink", "Grove_FlowersBlue", "Grove_FlowersYellow" };
        string[] glows = { "Grove_MushroomBlue", "Grove_MushroomGlow", "Grove_MushroomRed" };
        for (int i = 0, tries = 0; i < 40 && tries < 3000; tries++)
        {
            int x = rng.Next(box.x + 1, box.xMax - 1), y = rng.Next(box.y + 1, box.yMax - 1);
            if (!Free(x, y, x, y)) continue;
            Put(grove.transform, pf, i % 4 == 3 ? glows[rng.Next(glows.Length)] : flowers[rng.Next(flowers.Length)], new Vector2(x + 0.5f, y + 0.3f));
            used[x, y] = true; i++;
        }
        for (int i = 0, tries = 0; i < 14 && tries < 3000; tries++)
        {
            int x = rng.Next(box.x + 2, box.xMax - 2), y = rng.Next(box.y + 2, box.yMax - 3);
            if (!Free(x - 1, y, x + 1, y + 2)) continue;
            Put(grove.transform, pf, rng.Next(3) == 0 ? "Grove_Bush" : "Grove_Tree", new Vector2(x + 0.5f, y + 0.2f));
            Block(x - 2, y - 1, x + 2, y + 3); i++;
        }
    }

    // ------------------------------------------------------------------ scattering scenery in the zones
    private static void Scatter()
    {
        foreach (ZoneDef d in Zones)
        {
            RectInt box = BoundsOf(d.letter);
            string[] kinds = { "tree_broad", "tree_pine", "tree_oak", "bush", "tuft", "rock", "mushroom", "log", "puddle" };
            float[] weights = { d.broad, d.pine, d.oak, d.bush, d.tuft, d.rock, d.mushroom, d.log, d.puddle };
            float total = 0f; foreach (float w in weights) total += w;
            int want = Mathf.RoundToInt(d.scenery * SceneryDensity);
            for (int placed = 0, tries = 0; placed < want && tries < 8000; tries++)
            {
                // pick a kind by its weight
                float r = (float)rng.NextDouble() * total; int k = 0;
                while (k < kinds.Length - 1 && r > weights[k]) { r -= weights[k]; k++; }
                string kind = kinds[k];
                int fw = kind == "tree_oak" || kind == "log" ? 1 : 0;           // half-width its base needs
                int x = rng.Next(box.x + 2, box.xMax - 2), y = rng.Next(box.y + 1, box.yMax - 3);
                if (Area(x, y) != d.letter || At(x, y) == '~') continue;
                if (!Free(x - fw, y, x + fw, y)) continue;                       // only the base must be free: canopies may overlap, like real woods
                Scenery(sceneryRoot, kind, new Vector2(x + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.4f, y + 0.2f), d.letter, true, true);
                Block(x - fw, y, x + fw, y + (kind.StartsWith("tree") ? 1 : 0));
                placed++;
            }

            // ground cover: grass tufts and little mushrooms everywhere off the trail (no colliders, nothing to bump into)
            for (int placed = 0, tries = 0; placed < GroundCoverPerZone && tries < 6000; tries++)
            {
                int x = rng.Next(box.x + 1, box.xMax - 1), y = rng.Next(box.y + 1, box.yMax - 1);
                if (Area(x, y) != d.letter || At(x, y) == '~' || trail[x, y] || (used[x, y] && rng.Next(3) != 0)) continue;   // a little cover may still tuck in beside trees
                string kind = rng.NextDouble() < (d.mushroom > 2f ? 0.35 : 0.12) ? "mushroom" : "tuft";
                Scenery(sceneryRoot, kind, new Vector2(x + (float)rng.NextDouble(), y + (float)rng.NextDouble() * 0.6f), d.letter, false, true);
                placed++;
            }
        }
    }

    /// <summary>
    /// One piece of scenery with a sick and a healthy picture ("tree_broad" -> tree_broad_sick / tree_broad_healthy).
    /// In a zone it gets a HealSwap so it changes when that zone heals; in the grove / old woods it is simply healthy.
    /// </summary>
    private static GameObject Scenery(Transform parent, string kind, Vector2 pos, char area, bool collider, bool randomFlip)
    {
        bool zone = IsZone(area);
        bool flat = kind == "puddle";
        var go = Thing(parent, kind, kind + (zone ? "_sick" : "_healthy"), pos, flat ? FlatOrder : StandOrder);
        var sr = go.GetComponent<SpriteRenderer>();
        if (randomFlip && rng.Next(2) == 0) sr.flipX = true;
        if (zone)
        {
            var hs = go.AddComponent<HealSwap>();
            hs.zone = ZoneOf(area);
            hs.sickSprite = Spr(kind + "_sick");
            hs.healthySprite = Spr(kind + "_healthy");
            hs.swayWhenHealthy = kind.StartsWith("tree") || kind == "bush" || kind == "tuft";
        }
        if (flat || kind == "tuft" || kind == "mushroom") return go;

        bool tree = kind.StartsWith("tree");
        if (collider)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = kind == "tree_oak" ? new Vector2(1.2f, 0.6f) : kind == "log" ? new Vector2(1.7f, 0.5f) : new Vector2(0.8f, 0.5f);
            box.offset = new Vector2(0f, box.size.y * 0.5f);
        }
        go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;
        if (tree || kind == "rock") go.AddComponent<ShadowCaster>().lengthMultiplier = tree ? 1.3f : 1f;
        return go;
    }

    /// <summary>A plain object with a picture (lit by the day / night light), its feet at 'pos'.</summary>
    private static GameObject Thing(Transform parent, string name, string picture, Vector2 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(picture);
        sr.sortingOrder = order;
        if (lit != null) sr.sharedMaterial = lit;
        if (sr.sprite == null) Debug.LogWarning("Forest: picture '" + picture + ".png' was not found.");
        return go;
    }

    private static GameObject Put(Transform parent, string prefabFolder, string prefabName, Vector2 pos)
    {
        if (prefabFolder == null) return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + prefabName + ".prefab");
        if (prefab == null) { Debug.LogWarning("Forest: prefab " + prefabName + " not found in " + prefabFolder); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        // the old grove pieces were made for a floor drawn at order -20; here the floor is a tilemap (orders 0-4), so lift anything below it
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.sortingOrder < FlatOrder) sr.sortingOrder = FlatOrder;
        return go;
    }

    // ------------------------------------------------------------------ the fairies and the fairy doors
    private static void AddFairies()
    {
        string pf = oldArt != null ? oldArt + "/Prefabs/" : null;
        if (pf == null) { Debug.LogWarning("Forest: the old forest art (fairy_sick.png) was not found, so there are no fairies or fairy doors."); return; }

        GameObject sick = Put(root, pf, "SickFairy", SickFairySpot);
        if (sick != null)
        {
            Block(SickFairySpot.x - 2f, SickFairySpot.y - 2f, SickFairySpot.x + 2f, SickFairySpot.y + 2f);
            var sf = sick.GetComponent<SickFairyNpc>();
            if (sf != null)
            {
                SetStrings(sf, "firstMeeting", new[]
                {
                    "...Oh. Is someone there? Please, don't be afraid of me.",
                    "I tried to fly for help. But the sickness is in the air here... my wings won't hold me. I keep falling.",
                    "The whole forest is sick, part by part. My sister keeps the grove at the far end of the trail, and she cannot hold it alone.",
                    "Every part of the forest has an old heart stump. Feed the stump what it asks for and that part will heal. Will you help us?",
                });
                SetStrings(sf, "aboutSister", new[]
                {
                    "My sister is the keeper of the grove. She is taller than me, and much more serious, and she hums when she's worried.",
                    "The grove is the only place the sickness has not reached. It's at the very end of the trail, past the fog of the Mire. Look for the fireflies.",
                });
                SetStrings(sf, "noGuide", new[] { "I can feel the trail, but it's tangled... Follow it deeper. Every part of the forest has a heart stump that can heal it." });
            }
        }

        GameObject sister = Put(root, pf, "Fairy", FairyRing + new Vector2(0f, 3f));
        if (sister != null) sister.transform.SetParent(GameObject.Find("Grove") != null ? GameObject.Find("Grove").transform : root, true);

        GameObject trailDoor = Put(root, pf, "FairyDoor", TrailDoorSpot);
        GameObject groveDoor = Put(root, pf, "FairyDoor", GroveDoorSpot);
        if (trailDoor != null && groveDoor != null)
        {
            var a = trailDoor.GetComponent<ForestShortcutDoor>(); var b = groveDoor.GetComponent<ForestShortcutDoor>();
            if (a != null && b != null)
            {
                a.partner = b; b.partner = a;
                a.requireFlag = "forest_grove_reached"; b.requireFlag = "";
                a.travelToScene = ""; b.travelToScene = "";
                a.wakeOnInteract = false; b.wakeOnInteract = false;
                a.lockedHint = "The fairy door is sealed. Only someone who has walked the whole trail to the grove can wake it.";
                EditorUtility.SetDirty(a); EditorUtility.SetDirty(b);
            }
            Block(TrailDoorSpot.x - 2f, TrailDoorSpot.y - 2f, TrailDoorSpot.x + 2f, TrailDoorSpot.y + 3f);
            Block(GroveDoorSpot.x - 2f, GroveDoorSpot.y - 2f, GroveDoorSpot.x + 2f, GroveDoorSpot.y + 3f);
        }
    }

    // ------------------------------------------------------------------ player and camera
    private static void SetUpPlayerAndCamera(Tilemap ground)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = FarmSpawn;
        else Debug.LogWarning("Forest: no Player came over from the Mine scene. Add one, then run Farm > Set Up > Game Systems In Scene.");

        var cam = Object.FindAnyObjectByType<CameraFollow2D>();
        if (cam != null)
        {
            var so = new SerializedObject(cam);
            so.FindProperty("limitToMap").boolValue = true;
            so.FindProperty("boundsTilemap").objectReferenceValue = ground;
            so.ApplyModifiedPropertiesWithoutUndo();
            cam.transform.position = new Vector3(FarmSpawn.x - 6f, FarmSpawn.y, cam.transform.position.z);
        }
    }

    // ------------------------------------------------------------------ small helpers
    /// <summary>Marks tiles as taken, so nothing else is placed there.</summary>
    private static void Block(float x0, float y0, float x1, float y1)
    {
        int ax = Mathf.Max(0, Mathf.FloorToInt(x0)), ay = Mathf.Max(0, Mathf.FloorToInt(y0));
        int bx = Mathf.Min(W - 1, Mathf.FloorToInt(x1)), by = Mathf.Min(H - 1, Mathf.FloorToInt(y1));
        for (int y = ay; y <= by; y++) for (int x = ax; x <= bx; x++) used[x, y] = true;
    }

    private static bool Free(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (x < 0 || y < 0 || x >= W || y >= H || used[x, y]) return false;
        return true;
    }

    private static void AddBox(GameObject go, Vector2 offset, Vector2 size)
    {
        if (size.x <= 0f || size.y <= 0f) return;
        var c = go.AddComponent<BoxCollider2D>();
        c.offset = offset; c.size = size;
    }

    private static void Set(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Forest: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Forest: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStrings(Object target, string field, string[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null || !p.isArray) { Debug.LogWarning("Forest: list '" + field + "' not found on " + target.GetType().Name); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in scenes) if (s.path == path) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    /// <summary>Takes a picture of the whole forest from above and saves it to Logs/forest_preview.png (command line only).</summary>
    private static void SavePreview()
    {
        try
        {
            SortingGroup.UpdateAllSortingGroups();
            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = H * 0.5f;
            cam.aspect = (float)W / H;
            cam.transform.position = new Vector3(W * 0.5f, H * 0.5f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 1f, 0f);

            var rt = new RenderTexture(W * 16, H * 16, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/forest_preview.png", tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("Forest: preview picture saved to Logs/forest_preview.png");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Forest: could not take the preview picture (" + e.Message + "). The scene itself is fine.");
        }
    }
}
