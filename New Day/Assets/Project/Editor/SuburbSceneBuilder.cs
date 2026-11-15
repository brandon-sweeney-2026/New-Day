using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > Suburbs Scene
///
/// Builds the whole Suburbs scene (Assets/Project/Scenes/Suburbs.unity) in one go:
///   1. sets up the suburb pictures (Assets/Project/Art/Suburb) and makes prop prefabs in Assets/Farm/Suburb/Prefabs
///      (houses, trees, bushes, flowers, pots, playground pieces... all labelled for the Level Editor's Objects palette);
///   2. starts from a copy of the Mine scene, so the Player, camera, HUD, TimeManager, lighting and pollution map are already
///      set up exactly like your other scenes, then removes everything that belongs to the mine;
///   3. paints grass, roads, sidewalks and the dashed centre lines;
///   4. builds the General Store and Frank's Home and Hardware along the top (both can be entered), ten houses you can enter,
///      the park with a soccer field and playground, street lights, benches, mailboxes, trees, bushes, flower beds and pots;
///   5. lays the walking network for NPCs (path nodes along every sidewalk, into the park and playground, and a node on the
///      right edge that leads to the future City scene) and the named spots they stand at (benches, the pitch, the swings...);
///   6. moves everybody in: the Kim family above the General Store, Frank in his store, and the 18 residents of the ten houses;
///   7. adds the doorway to the farm on the left edge, and the matching doorway on the farm's right edge.
///
/// It does this by running the other setup menus for you, in order: General Store, Store Paths, Frank's Home and Hardware,
/// Suburb Houses, Kim Family, Suburb Residents (all under Farm > Set Up), then Farmhouse Hardware Upgrades in the Farm scene.
///
/// The layout numbers are the constants just below: change them and run the menu again to get a different suburb.
/// Who lives where is in the 'Homes' table below; who they are and what they do all day is in SuburbResidentsMenu.cs.
/// Trees here are decoration only: they have no Breakable script, so they cannot be chopped and drop nothing.
/// Running it again REPLACES the Suburbs scene (it asks first). Must live in a folder named "Editor".
/// </summary>
public static class SuburbSceneBuilder
{
    // ------------------------------------------------------------------ where things live
    private const string ScenePath = "Assets/Project/Scenes/Suburbs.unity";
    private const string TemplateScene = "Assets/Project/Scenes/Mine.unity";
    private const string FarmScene = "Assets/Project/Scenes/Farm.unity";
    private const string PrefabDir = "Assets/Farm/Suburb/Prefabs";
    private const string LitMaterialPath = "Assets/Resources/Lighting/SpriteLit.mat";

    // ------------------------------------------------------------------ the layout (all in tiles; 0,0 is the bottom-left corner)
    private const int W = 72, H = 63;                 // size of the map (63 tall so the roof of Frank's store, 15 tiles high, fits on it)
    private const int MainY = 23;                     // Main Street: the bottom row of its 3 rows (it runs the whole width)
    private const int CrossX = 34;                    // Cross Street: the left column of its 3 columns
    private const int ShopY = 41;                     // Shop Street (top)
    private const int MapleY = 6;                     // Maple Lane (bottom-left)
    private const int ShopBaseY = 47;                 // the shops stand on this row
    private const int RowABaseY = 30;                 // six houses north of Main Street
    private const int RowBBaseY = 12;                 // four houses north of Maple Lane
    private const int Seed = 20261114;                // change this number for a different scatter of trees and flowers
    private const int TreeCount = 40, BushCount = 44, FlowerBedCount = 44;

    private const float StoreX = 20f, FranksX = 50f;  // where the two shops' front doors are along Shop Street

    /// <summary>One home: where it stands, which picture and inside layout it uses, and what its door calls it.</summary>
    private class Home
    {
        public float x; public int baseY;             // the middle of the front wall (whole numbers keep the house on the tile grid)
        public int picture;                           // sub_house_1 ... sub_house_10 (Assets/Project/Art/Houses)
        public int kind;                              // inside layout: 0 Cosy, 1 Family, 2 Cosy mirrored, 3 Loft
        public string name;                           // the door says "Enter <name>"
        public int sidewalkY;                         // the top row of the sidewalk its front path joins
        public Home(float x, int baseY, int picture, int kind, string name, int sidewalkY)
        { this.x = x; this.baseY = baseY; this.picture = picture; this.kind = kind; this.name = name; this.sidewalkY = sidewalkY; }
        /// <summary>Where the front door is (not every picture has it in the middle).</summary>
        public float DoorX { get { return x + SuburbHousesMenu.DoorOffset(picture); } }
    }

    /// <summary>
    /// The ten homes, in house-number order (House 1 gets the spots H1Bed, H1Kitchen ...; SuburbResidentsMenu says who lives in each).
    /// Six stand north of Main Street, three north of Maple Lane, and one on Shop Street next to Frank's.
    /// Each house picture is 8 tiles wide and 9 tall, so keep at least 9 tiles between two houses in a row.
    /// </summary>
    private static readonly Home[] Homes =
    {
        new Home(8f,  RowABaseY, 1, 1, "the Abaras' house",        MainY + 4),
        new Home(17f, RowABaseY, 3, 0, "the Delgados' house",      MainY + 4),
        new Home(26f, RowABaseY, 5, 1, "the Nairs' house",         MainY + 4),
        new Home(44f, RowABaseY, 7, 3, "Bea and Tariq's house",    MainY + 4),
        new Home(53f, RowABaseY, 2, 1, "the Tanakas' house",       MainY + 4),
        new Home(62f, RowABaseY, 4, 2, "the Bells' house",         MainY + 4),
        new Home(6f,  RowBBaseY, 6, 2, "Sam's house",              MapleY + 4),
        new Home(15f, RowBBaseY, 8, 3, "the Reyes' house",         MapleY + 4),
        new Home(24f, RowBBaseY, 9, 0, "Ada's house",              MapleY + 4),
        new Home(62f, ShopBaseY, 10, 3, "Gus's house",             ShopBaseY - 1),
    };

    private static string art;                         // folder the suburb pictures are in
    private static Material lit;
    private static bool[,] used;                       // tiles that are already taken
    private static char[,] sketch;                     // a text drawing of the map, written to the Console
    private static System.Random rng;
    private static Transform objectsParent;
    private static Dictionary<string, GameObject> props;
    private static Transform streetRoot, spotRoot;     // parents of the street path nodes and the outdoor NpcSpots
    private static Dictionary<string, PathNode> streetNodes;

    // ------------------------------------------------------------------ entry points
    [MenuItem("Farm/Set Up/Suburbs Scene", false, 146)]
    public static void BuildFromMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Rebuild the suburb?", "Suburbs.unity already exists. Building again REPLACES it (anything you placed by hand in it is lost).", "Replace it", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string problem = Build(false);
        EditorUtility.DisplayDialog(problem == null ? "Suburbs ready" : "Couldn't build the suburb",
            problem ?? ("Built " + ScenePath + ".\n\nPress Play to walk around. The road on the left edge leads to the farm, and the farm's road on its right edge now leads here." +
                        "\n\nThe Kims, Frank and the neighbours are in the scene with their schedules. Farm > Fix and Diagnose > Check NPC Schedules prints everybody's week in the Console."), "OK");
    }

    /// <summary>Used from the command line (Unity -batchmode -executeMethod SuburbSceneBuilder.BuildBatch): no questions asked.</summary>
    public static void BuildBatch()
    {
        string problem = Build(true);
        Debug.Log(problem == null ? "SUBURB BUILD OK" : "SUBURB BUILD FAILED: " + problem);
    }

    /// <summary>Command line only: opens the saved Suburbs scene and takes the overhead picture (Logs/suburb_preview.png) without rebuilding.</summary>
    public static void PreviewBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SortingGroup.UpdateAllSortingGroups();
        SavePreview();
    }

    // ------------------------------------------------------------------ the build
    private static string Build(bool preview)
    {
        art = FindFolder("soccer_field");
        if (art == null) return "The suburb pictures were not found (soccer_field.png and friends belong in Assets/Project/Art/Suburb).";
        if (!File.Exists(TemplateScene)) return "The Mine scene (" + TemplateScene + ") is used as the starting point, and it is missing.";

        lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        rng = new System.Random(Seed);
        used = new bool[W, H];
        sketch = new char[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) sketch[x, y] = '.';

        ImportArt();
        EnsureFolder(PrefabDir);
        props = MakePrefabs();
        // the house upgrades Frank sells (front doors, shutters, cabinets, windows, stoves): only the assets here, the farmhouse is wired up in ConnectFarm
        if (HouseHardwareUpgradesMenu.MakeAssets() == null) Debug.LogWarning("Suburb: the Hardware art was not found, so Frank has no door / shutter / cabinet / window upgrades to sell.");
        AssetDatabase.SaveAssets();

        // ---- start from a copy of the Mine scene (Player, camera, HUD, TimeManager, lighting all come with it) ----
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(TemplateScene, ScenePath)) return "Could not copy the Mine scene to " + ScenePath + ".";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CleanTemplate(scene);

        Tilemap ground = FindMap("Ground"), paths = FindMap("Paths"), decor = FindMap("PathDecor");
        if (ground == null || paths == null) return "The copied scene has no 'Ground' or 'Paths' tilemap under its Grid.";
        if (decor == null) decor = MakeMap(ground.transform.parent, "PathDecor", 3);

        GameObject objects = GameObject.Find("Objects");
        if (objects == null) objects = new GameObject("Objects");
        objectsParent = objects.transform;

        PaintGround(ground);
        PaintStreets(paths, decor);
        PlaceShops();
        PlaceHouses();
        PlacePark();
        PlaceStreetFurniture();
        LayStreetPaths();           // before Scatter, so no tree or bush is planted on a walking route
        Scatter();
        MakeEdges();
        MakeEntrance();
        MakeCityRoad();
        MakeAmbientLife();
        MakeHouseManager();
        MoveEverybodyIn();
        SetUpPlayerAndCamera(ground);

        paths.RefreshAllTiles();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(ScenePath);
        LogSketch();
        int scheduleProblems;
        Debug.Log(NpcScheduleCheckMenu.Check(out scheduleProblems));      // everybody's week, and anything wrong with it
        if (scheduleProblems > 0) Debug.LogWarning("Suburb: " + scheduleProblems + " schedule problem(s). Look for the lines marked '!!' in the Console (or run Farm > Fix and Diagnose > Check NPC Schedules).");
        if (preview) SavePreview();

        string farmNote = ConnectFarm();
        Debug.Log("Suburb: " + farmNote);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return null;
    }

    // ------------------------------------------------------------------ template clean-up
    /// <summary>Removes everything that belongs to the mine and empties the tilemaps, keeping the Player, camera, HUD and managers.</summary>
    private static void CleanTemplate(Scene scene)
    {
        var keep = new HashSet<string> { "Player", "Main Camera", "Objects", "PollutionMap", "TimeManager", "GameHUD", "Grid", "LightingManager" };
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            bool isUi = root.GetComponentInChildren<Canvas>(true) != null || root.GetComponentInChildren<Camera>(true) != null;
            if (!keep.Contains(root.name) && !isUi) Object.DestroyImmediate(root);
        }

        GameObject objects = GameObject.Find("Objects");
        if (objects != null)
            for (int i = objects.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(objects.transform.GetChild(i).gameObject);

        GameObject grid = GameObject.Find("Grid");
        if (grid != null)
            foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true))
            {
                map.ClearAllTiles();
                map.CompressBounds();
            }
    }

    private static Tilemap FindMap(string name)
    {
        GameObject grid = GameObject.Find("Grid");
        if (grid == null) return null;
        foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true))
            if (map.name == name) return map;
        return null;
    }

    private static Tilemap MakeMap(Transform grid, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grid, false);
        var map = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = order;
        return map;
    }

    // ------------------------------------------------------------------ ground and streets
    private static void PaintGround(Tilemap ground)
    {
        var grass = new List<TileBase>();
        for (int i = 1; i <= 4; i++)
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Farm/Biomes/Tiles/City/City_ParkGrass_" + i + ".asset");
            if (t != null) grass.Add(t);
        }
        if (grass.Count == 0)
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Farm/Kiln/Tiles/Spring/Grass.asset");   // fall back to the farm's grass
            if (t != null) grass.Add(t);
        }
        if (grass.Count == 0) { Debug.LogWarning("Suburb: no grass tile found, the ground was left empty."); return; }

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                ground.SetTile(new Vector3Int(x, y, 0), grass[rng.Next(grass.Count)]);
    }

    private static void PaintStreets(Tilemap paths, Tilemap decor)
    {
        var roadPreset = AssetDatabase.LoadAssetAtPath<GeographyPreset>("Assets/Farm/Data/Geography/Road.asset");
        var walkPreset = AssetDatabase.LoadAssetAtPath<GeographyPreset>("Assets/Farm/Data/Geography/Sidewalk.asset");
        if (roadPreset == null || walkPreset == null || Piece(roadPreset.wideTiles, 4) == null || Piece(walkPreset.wideTiles, 4) == null)
            Debug.LogWarning("Suburb: the Road / Sidewalk geography presets have no tiles. Run Farm > Build Art > Path Palettes and Farm > Build Art > Geography Presets, then build the suburb again.");

        var road = new HashSet<Vector2Int>();
        var walk = new HashSet<Vector2Int>();

        // roads (each is 3 tiles wide)
        Add(road, 0, MainY, W - 1, MainY + 2);                       // Main Street, edge to edge
        Add(road, CrossX, MapleY, CrossX + 2, ShopY + 2);            // Cross Street, from Maple Lane up to Shop Street
        Add(road, 6, ShopY, 65, ShopY + 2);                          // Shop Street
        Add(road, 6, MapleY, CrossX + 2, MapleY + 2);                // Maple Lane

        // sidewalks (2 tiles wide) along both sides of every road
        Add(walk, 0, MainY - 2, W - 1, MainY - 1); Add(walk, 0, MainY + 3, W - 1, MainY + 4);
        Add(walk, CrossX - 2, MapleY - 2, CrossX - 1, ShopY + 4); Add(walk, CrossX + 3, MapleY - 2, CrossX + 4, ShopY + 4);
        Add(walk, 4, ShopY - 2, 67, ShopY - 1);
        Add(walk, 4, ShopY + 3, 67, ShopBaseY - 1);                  // the plaza in front of the shops (3 deep)
        Add(walk, 4, ShopY, 5, ShopY + 2); Add(walk, 66, ShopY, 67, ShopY + 2);      // caps at the ends of Shop Street
        Add(walk, 4, MapleY - 2, CrossX + 4, MapleY - 1); Add(walk, 4, MapleY + 3, CrossX - 1, MapleY + 4);
        Add(walk, 4, MapleY, 5, MapleY + 2);                         // cap at the end of Maple Lane
        Add(walk, 46, 16, 47, MainY - 3);                            // a path from Main Street down into the park

        // the front path of every house (2 tiles wide, from the sidewalk up to the door)
        foreach (Home h in Homes) { int x0 = WalkwayX(h); Add(walk, x0, h.sidewalkY + 1, x0 + 1, h.baseY - 1); }

        foreach (Vector2Int c in road) walk.Remove(c);

        // Pick the right piece for every tile (edge, corner, inside corner or plain middle), the same way the Geography Brush does.
        foreach (Vector2Int c in walk) { paths.SetTile(new Vector3Int(c.x, c.y, 0), StreetTile(walkPreset, walk, c)); Take(c.x, c.y, '-'); }
        foreach (Vector2Int c in road) { paths.SetTile(new Vector3Int(c.x, c.y, 0), StreetTile(roadPreset, road, c)); Take(c.x, c.y, '='); }

        // dashed centre lines (2 on, 2 off), left out where streets cross
        if (roadPreset != null)
        {
            for (int x = 0; x < W; x++) if (x % 4 < 2 && (x < CrossX - 2 || x > CrossX + 4)) Dash(decor, x, MainY + 1, roadPreset.centerHorizontal);
            for (int x = 7; x <= 64; x++) if (x % 4 < 2 && (x < CrossX - 2 || x > CrossX + 4)) Dash(decor, x, ShopY + 1, roadPreset.centerHorizontal);
            for (int x = 7; x < CrossX - 2; x++) if (x % 4 < 2) Dash(decor, x, MapleY + 1, roadPreset.centerHorizontal);
            for (int y = MapleY + 3; y < ShopY - 1; y++) if (y % 4 < 2 && (y < MainY - 2 || y > MainY + 4)) Dash(decor, CrossX + 1, y, roadPreset.centerVertical);
        }
    }

    /// <summary>The left tile of a house's 2-wide front path (it lines up with the door on the house picture).</summary>
    private static int WalkwayX(Home h)
    {
        return Mathf.FloorToInt(h.DoorX - 0.5f);
    }

    /// <summary>
    /// Which tile of a geography (road or sidewalk) belongs on one cell, judged by which neighbours are part of the same street.
    /// A copy of the rules in GeographyBrushWindow, so streets built here look the same as ones you paint by hand.
    /// Wide Tiles are a 3 x 3 block: 0 1 2 = top-left, top, top-right; 3 4 5 = left, middle, right; 6 7 8 = bottom row.
    /// </summary>
    private static TileBase StreetTile(GeographyPreset preset, HashSet<Vector2Int> street, Vector2Int c)
    {
        if (preset == null) return null;
        bool n = In(street, c.x, c.y + 1), s = In(street, c.x, c.y - 1), e = In(street, c.x + 1, c.y), w = In(street, c.x - 1, c.y);
        TileBase middle = Piece(preset.wideTiles, 4);
        TileBase fill = RandomPiece(preset.fillTiles);

        if (!preset.useEdgeTiles) return fill != null ? fill : middle;

        // one tile wide: the self-connecting path tile
        if ((!n && !s) || (!e && !w)) return preset.autoTile != null ? preset.autoTile : (middle != null ? middle : fill);

        TileBase t;
        if (!n) t = !w ? Piece(preset.wideTiles, 0) : (!e ? Piece(preset.wideTiles, 2) : Piece(preset.wideTiles, 1));
        else if (!s) t = !w ? Piece(preset.wideTiles, 6) : (!e ? Piece(preset.wideTiles, 8) : Piece(preset.wideTiles, 7));
        else if (!w) t = Piece(preset.wideTiles, 3);
        else if (!e) t = Piece(preset.wideTiles, 5);
        else
        {
            // street on all four sides: it may still be an inside corner, where one diagonal neighbour is missing
            int corner = -1;
            if (!In(street, c.x - 1, c.y + 1)) corner = 0;
            else if (!In(street, c.x + 1, c.y + 1)) corner = 1;
            else if (!In(street, c.x - 1, c.y - 1)) corner = 2;
            else if (!In(street, c.x + 1, c.y - 1)) corner = 3;
            if (corner >= 0 && preset.swapInnerCorners) corner = 3 - corner;
            t = corner >= 0 ? Piece(preset.innerTiles, corner) : null;
            if (t == null) t = fill;
        }
        return t != null ? t : (middle != null ? middle : fill);
    }

    /// <summary>Is this cell part of the street? Cells past the left and right edge of the map count as street, so roads run off the map cleanly.</summary>
    private static bool In(HashSet<Vector2Int> street, int x, int y)
    {
        if (x < 0 || x >= W) return street.Contains(new Vector2Int(Mathf.Clamp(x, 0, W - 1), y));
        return street.Contains(new Vector2Int(x, y));
    }

    private static TileBase Piece(TileBase[] tiles, int i) { return tiles != null && i < tiles.Length ? tiles[i] : null; }

    private static TileBase RandomPiece(TileBase[] tiles)
    {
        if (tiles == null || tiles.Length == 0) return null;
        for (int tries = 0; tries < 8; tries++)
        {
            TileBase t = tiles[rng.Next(tiles.Length)];
            if (t != null) return t;
        }
        return null;
    }

    private static void Dash(Tilemap decor, int x, int y, TileBase tile)
    {
        if (decor != null && tile != null) decor.SetTile(new Vector3Int(x, y, 0), tile);
    }

    // ------------------------------------------------------------------ buildings
    private static void PlaceShops()
    {
        // General Store: built by the store pack's own menu (it also builds the inside of the store and the apartment far away).
        StoreSetupMenu.Run();
        GameObject store = GameObject.Find("SuburbStore");
        if (store != null) store.transform.position = new Vector3(StoreX, ShopBaseY, 0f);
        else Debug.LogWarning("Suburb: the General Store was not built (see the messages above). Run Farm > Set Up > General Store in the Suburbs scene.");
        Take(16, ShopBaseY - 1, 24, H - 1, 'S');
        // the walking network inside the store and the apartment (after the store is in its place: the street node goes in front of its door)
        Debug.Log("Suburb: " + PathToolsMenu.BuildStorePaths());

        // Frank's Home and Hardware: the building is 10 tiles wide and 15 tall, the inside is a room far away like the store's
        Debug.Log("Suburb: " + FrankStoreSetupMenu.Build(new Vector3(FranksX, ShopBaseY, 0f)));
        Take(45, ShopBaseY - 1, 54, H - 1, 'S');
        // the old front-only hardware shop prefab is replaced by Frank's store: remove it so it can't be placed by mistake
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/HardwareStore.prefab") != null) AssetDatabase.DeleteAsset(PrefabDir + "/HardwareStore.prefab");

        // pots beside both shop doors (Frank's double doors are wider)
        Put("Pot_Flowers", StoreX - 1.7f, ShopBaseY - 0.25f); Put("Pot_Shrub", StoreX + 1.7f, ShopBaseY - 0.25f);
        Put("Pot_Shrub", FranksX - 4.2f, ShopBaseY - 0.25f); Put("Pot_Flowers", FranksX + 4.2f, ShopBaseY - 0.25f);
    }

    private static void PlaceHouses()
    {
        // the houses themselves (outside, inside, door, spots and path nodes) are built by the houses menu, one per entry in 'Homes'
        var sites = new List<SuburbHousesMenu.Site>();
        foreach (Home h in Homes)
            sites.Add(new SuburbHousesMenu.Site { position = new Vector2(h.x, h.baseY), picture = h.picture, kind = h.kind, name = h.name });
        string result = SuburbHousesMenu.Build(sites);
        Debug.Log("Suburb: " + result);
        if (!result.StartsWith("Built")) Debug.LogWarning("Suburb: the houses were not built (see the message above), so nobody can move in.");

        foreach (Home h in Homes) Garden(h);
    }

    /// <summary>A house's front garden: mailbox, a bush and a row of flowers under the windows on each side, a pot by the door. (Its front path is painted with the streets.)</summary>
    private static void Garden(Home h)
    {
        int left = Mathf.FloorToInt(h.x - 4f);
        Take(left, h.baseY - 2, left + 7, h.baseY + 8, 'H');             // the house is 8 wide and 9 tall

        Put("Mailbox", WalkwayX(h) + 2.6f, h.sidewalkY + 1.35f);

        string[] bushes = { "Bush_Green", "Bush_Flowering", "Bush_Rose" };
        string[] flowers = { "Flower_Red", "Flower_Yellow", "Flower_Pink", "Flower_Blue", "Flower_White" };
        string colourA = flowers[rng.Next(flowers.Length)], colourB = flowers[rng.Next(flowers.Length)];
        foreach (float side in new[] { -2.6f, 2.6f })
        {
            if (Mathf.Abs(h.x + side - h.DoorX) < 1.8f) continue;          // never in front of the door
            Put(bushes[rng.Next(bushes.Length)], h.x + side, h.baseY - 0.3f);
            for (int i = 0; i < 4; i++)
                Put(i % 2 == 0 ? colourA : colourB, h.x + side - 0.6f + i * 0.4f, h.baseY - 0.95f - (float)rng.NextDouble() * 0.15f);
        }
        Put(rng.Next(2) == 0 ? "Pot_Flowers" : "Pot_Shrub", h.DoorX + 1.25f, h.baseY - 0.2f);
    }

    // ------------------------------------------------------------------ the park
    private static void PlacePark()
    {
        Put("SoccerField", 58f, 9f);
        Take(48, 3, 68, 15, 'F');
        Put("SoccerGoal", 49.3f, 7.4f);
        GameObject rightGoal = Put("SoccerGoal", 66.7f, 7.4f);
        if (rightGoal != null) rightGoal.GetComponent<SpriteRenderer>().flipX = true;
        Put("SoccerBall", 58f, 9f);

        Put("Play_Sandbox", 43.5f, 5.5f);
        Put("Play_Seesaw", 43.5f, 9.2f);
        Put("Play_Swings", 43.5f, 12.2f);
        Put("Play_Slide", 42.5f, 16f);
        Take(40, 3, 46, 19, 'p');

        Put("BenchPark", 52f, 15.7f); Put("BenchPark", 64f, 15.7f); Put("BenchPark", 58f, 1.8f); Put("BenchPark", 45f, 18.2f);
        Take(51, 15, 53, 16, 'n'); Take(63, 15, 65, 16, 'n'); Take(57, 1, 59, 2, 'n');
    }

    // ------------------------------------------------------------------ lights, benches, planters
    private static void PlaceStreetFurniture()
    {
        // street lights: north side of Main Street (between the houses), south side, the plaza, Maple Lane, Cross Street, the park
        float[][] lights =
        {
            new[] { 13f, 28.2f }, new[] { 22f, 28.2f }, new[] { 40f, 28.2f }, new[] { 49f, 28.2f }, new[] { 58f, 28.2f }, new[] { 67f, 28.2f },
            new[] { 10f, 20.2f }, new[] { 24f, 20.2f }, new[] { 44f, 20.2f }, new[] { 60f, 20.2f },
            new[] { 13f, 46.6f }, new[] { 27f, 46.6f }, new[] { 43f, 46.6f }, new[] { 57f, 46.6f },
            new[] { 9.5f, 11.3f }, new[] { 23.5f, 11.3f },
            new[] { 31.3f, 15f }, new[] { 39.3f, 33f }, new[] { 31.3f, 34f },
            new[] { 48.6f, 18.5f }, new[] { 69f, 18.5f }, new[] { 69f, 2.5f }, new[] { 41f, 2.2f },
        };
        foreach (float[] p in lights) { Put("LightPost", p[0], p[1]); Take(Mathf.FloorToInt(p[0]), Mathf.FloorToInt(p[1]), 'L'); }

        // the plaza: planter boxes full of flowers, with benches between them
        string[] flowers = { "Flower_Red", "Flower_Yellow", "Flower_Pink", "Flower_Blue", "Flower_White" };
        foreach (float x in new[] { 10f, 30f, 40f, 60f })
        {
            Put("PlanterBox", x, ShopBaseY - 0.5f);
            for (int i = 0; i < 4; i++) Put(flowers[rng.Next(flowers.Length)], x - 0.75f + i * 0.5f, ShopBaseY - 0.1f);
        }
        Put("BenchPark", 33f, ShopBaseY - 0.6f); Put("BenchPark", 38f, ShopBaseY - 0.6f);

        // a signpost by the road to the farm
        Put("SignpostArrows", 2.5f, MainY + 5.3f);
        Take(2, MainY + 5, 'n');
    }

    // ------------------------------------------------------------------ where NPCs can walk and stand
    /// <summary>
    /// Lays the outdoor walking network. NPCs only walk from node to node along links, so this decides where they can go:
    /// along the middle of every sidewalk, across the streets at the corners, up each front path to a door, into the park and
    /// the playground, and to the right edge of the map ("CityExit": the road to the future City scene).
    /// It also makes the named NpcSpots that schedules use as places (ParkBenchA, FieldMid, PlaySwings, PlazaBench1 ...).
    /// The shops and houses made their own nodes (Street_StoreFront, Street_Hardware, H1_Street ...); this joins them up.
    /// Select 'PathNetwork_Streets' in the Hierarchy to see the network drawn in the Scene view.
    /// </summary>
    private static void LayStreetPaths()
    {
        streetRoot = new GameObject("PathNetwork_Streets").transform;
        spotRoot = new GameObject("Amenity Spots").transform;
        streetNodes = new Dictionary<string, PathNode>();
        // nodes the shops and houses already made (looked up by name when linking)
        foreach (PathNode n in Object.FindObjectsByType<PathNode>()) streetNodes[n.Id] = n;

        float north = MainY + 4f, south = MainY - 1f;          // the middle of Main Street's two sidewalks
        float west = CrossX - 1f, east = CrossX + 4f;          // the middle of Cross Street's two sidewalks
        float plaza = ShopBaseY - 2f, shopSouth = ShopY - 1f;  // the plaza in front of the shops; the sidewalk south of Shop Street
        float mapleNorth = MapleY + 4f, mapleSouth = MapleY - 1f;

        // ---- Main Street: both sidewalks, edge to edge. The right-hand end of the north sidewalk is the way to the city.
        Node("MN_West", 1.5f, north); Node("MN_CrossW", west, north); Node("MN_CrossE", east, north); Node("MN_East", 70f, north);
        PathNode city = Node("CityExit", W - 0.5f, north);
        city.leadsToScene = "City";
        Node("MS_West", 1.5f, south); Node("MS_CrossW", west, south); Node("MS_CrossE", east, south); Node("MS_Park", 47f, south); Node("MS_East", 70f, south);

        // one node on the sidewalk in front of every house, joined to the node at its door
        var mainNorth = new List<string> { "MN_West" };
        var maple = new List<string>();
        for (int i = 0; i < Homes.Length; i++)
        {
            Home h = Homes[i];
            string id = "Walk_H" + (i + 1);
            Node(id, h.DoorX, h.baseY == ShopBaseY ? plaza : h.sidewalkY);
            Link(id, "H" + (i + 1) + "_Street");
            if (h.baseY == RowABaseY) { if (h.x > CrossX && !mainNorth.Contains("MN_CrossW")) { mainNorth.Add("MN_CrossW"); mainNorth.Add("MN_CrossE"); } mainNorth.Add(id); }
            else if (h.baseY == RowBBaseY) maple.Add(id);
        }
        mainNorth.Add("MN_East"); mainNorth.Add("CityExit");
        Link(mainNorth.ToArray());
        Link("MS_West", "MS_CrossW", "MS_CrossE", "MS_Park", "MS_East");
        Link("MN_West", "MS_West"); Link("MN_East", "MS_East");                // crossing Main Street at both ends

        // ---- Cross Street: both sidewalks, from Maple Lane up to the plaza (they cross Main Street and Shop Street at the corners)
        Node("CW_MapleS", west, mapleSouth); Node("CW_Maple", west, mapleNorth); Node("CW_Shop", west, shopSouth); Node("CW_Plaza", west, plaza);
        Node("CE_Maple", east, mapleSouth); Node("CE_Play", east, 14f); Node("CE_Shop", east, shopSouth); Node("CE_Plaza", east, plaza);
        Link("CW_MapleS", "CW_Maple", "MS_CrossW", "MN_CrossW", "CW_Shop", "CW_Plaza");
        Link("CE_Maple", "CE_Play", "MS_CrossE", "MN_CrossE", "CE_Shop", "CE_Plaza");
        Link("CW_MapleS", "CE_Maple"); Link("CW_Shop", "CE_Shop");

        // ---- Maple Lane: the sidewalk in front of the three houses
        maple.Add("CW_Maple");
        Link(maple.ToArray());

        // ---- the plaza: past both shop doors and Gus's house
        Node("Plaza_West", 8f, plaza); Node("Plaza_East", 66f, plaza);
        Link("Plaza_West", "Street_StoreFront", "CW_Plaza", "CE_Plaza", "Street_Hardware", "Walk_H10", "Plaza_East");
        Spot("PlazaBench1", 33f, ShopBaseY - 1.2f, Vector2.down, "CW_Plaza");           // the two benches between the shops
        Spot("PlazaBench2", 38f, ShopBaseY - 1.2f, Vector2.down, "CE_Plaza");
        Spot("PlazaPlanterW", 10f, ShopBaseY - 1.1f, Vector2.up, "Plaza_West");         // the four planter boxes
        Spot("PlazaPlanterMidW", 30f, ShopBaseY - 1.1f, Vector2.up, "CW_Plaza");
        Spot("PlazaPlanterMidE", 40f, ShopBaseY - 1.1f, Vector2.up, "CE_Plaza");
        Spot("PlazaPlanterE", 60f, ShopBaseY - 1.1f, Vector2.up, "Walk_H10");
        Spot("FarmRoad", 3f, north, Vector2.left, "MN_West");                           // looking down the road to the farm

        // ---- the park: in from Main Street down the path, past the benches, onto the pitch
        Node("Park_Top", 47f, 16.6f);
        Link("MS_Park", "Park_Top");
        Spot("ParkBenchA", 52f, 15f, Vector2.down, "Park_Top");                         // the two benches north of the pitch
        Spot("FieldSide", 58f, 14.6f, Vector2.down, "At_ParkBenchA");                   // the touchline
        Spot("ParkBenchB", 64f, 15f, Vector2.down, "At_FieldSide");
        Spot("FieldMid", 58f, 9.7f, Vector2.down, "At_FieldSide");                      // the centre circle (just above the ball)
        Spot("FieldLeft", 53f, 9f, Vector2.right, "At_FieldMid");
        Spot("FieldRight", 63f, 9f, Vector2.left, "At_FieldMid");
        Spot("ParkBenchC", 58f, 2.9f, Vector2.up, "At_FieldMid");                       // the bench south of the pitch
        Link("At_FieldLeft", "At_ParkBenchA"); Link("At_FieldRight", "At_ParkBenchB");
        Link("At_ParkBenchB", "MS_East");                                               // a second way out, at the east end

        // ---- the playground: in from Cross Street, down its west side, across to each piece
        Node("Play_W14", 41.2f, 14f); Node("Play_W11", 41.2f, 11.6f); Node("Play_W8", 41.2f, 8.6f); Node("Play_W5", 41.2f, 5.5f); Node("Play_E", 44.6f, 14.6f);
        Link("CE_Play", "Play_W14", "Play_W11", "Play_W8", "Play_W5");
        Link("Play_W14", "Play_E", "Park_Top");                                         // and through to the park
        Spot("PlaySlide", 42.5f, 15.3f, Vector2.down, "Play_W14");
        Spot("PlaySwings", 43.5f, 11.6f, Vector2.down, "Play_W11");
        Spot("PlaySeesaw", 43.5f, 8.6f, Vector2.down, "Play_W8");
        Spot("PlaySandbox", 43.5f, 5.5f, Vector2.down, "Play_W5");
        Spot("ParkBenchD", 45f, 17.5f, Vector2.down, "Play_E");                         // the bench that looks over the playground

        PathNode.MarkDirty();
    }

    /// <summary>A new street node. The tile under it is marked as taken, so nothing is planted there.</summary>
    private static PathNode Node(string id, float x, float y)
    {
        PathNode n = PathToolsMenu.MakeNode(id, new Vector2(x, y), streetRoot);
        streetNodes[id] = n;
        Take(Mathf.FloorToInt(x), Mathf.FloorToInt(y), ' ');
        Sketch(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 'o');
        return n;
    }

    /// <summary>Links the named nodes in a chain (a to b, b to c ...), and keeps the ground along each link clear of trees and bushes.</summary>
    private static void Link(params string[] ids)
    {
        for (int i = 0; i + 1 < ids.Length; i++)
        {
            PathNode a, b;
            if (!streetNodes.TryGetValue(ids[i], out a) || !streetNodes.TryGetValue(ids[i + 1], out b))
            {
                Debug.LogWarning("Suburb: can't link path nodes '" + ids[i] + "' and '" + ids[i + 1] + "': one of them does not exist.");
                continue;
            }
            PathToolsMenu.Link(a, b);
            Vector2 p = a.transform.position, q = b.transform.position;
            if (p.x > W || q.x > W) continue;                               // (a node inside a building's far-away room)
            int steps = Mathf.CeilToInt(Vector2.Distance(p, q) * 2f);
            for (int s = 0; s <= steps; s++)
            {
                Vector2 c = Vector2.Lerp(p, q, steps == 0 ? 0f : (float)s / steps);
                Take(Mathf.FloorToInt(c.x), Mathf.FloorToInt(c.y), ' ');
            }
        }
    }

    /// <summary>A named place NPCs can be sent to, with its own path node ("At_" + the name) linked to a node that is already there.</summary>
    private static void Spot(string id, float x, float y, Vector2 facing, string linkTo)
    {
        var go = new GameObject("Spot_" + id);
        go.transform.SetParent(spotRoot, false);
        go.transform.position = new Vector3(x, y, 0f);
        var spot = go.AddComponent<NpcSpot>();
        spot.spotId = id; spot.facing = facing;
        Node("At_" + id, x, y);
        Link("At_" + id, linkTo);
    }

    /// <summary>The right-hand end of Main Street: the road to the city. The City scene does not exist yet, so for now it only says so.</summary>
    private static void MakeCityRoad()
    {
        var go = new GameObject("CityRoad");
        go.transform.position = new Vector3(W - 0.75f, MainY + 1.5f, 0f);
        go.transform.localScale = new Vector3(1.5f, 7f, 1f);                // covers the road and both sidewalks
        go.AddComponent<BoxCollider2D>().isTrigger = true;                  // (the collider goes on first: StoreFlavor needs one)
        var sign = go.AddComponent<StoreFlavor>();
        sign.prompt = "Road to the city";
        sign.lines = new[] { "The road to the city. It is a long walk, and you have a farm to run.", "The neighbours walk this way to work. Maybe one day you'll follow them." };
    }

    /// <summary>
    /// Frank's displays sell house upgrades, and buying one needs a HouseManager. The farmhouse is in the Farm scene, so this one
    /// only holds the catalog (every HouseUpgradeData in the project): what you buy here is remembered and shows on the farmhouse at home.
    /// </summary>
    private static void MakeHouseManager()
    {
        var go = new GameObject("HouseManager");
        var mgr = go.AddComponent<HouseManager>();
        var so = new SerializedObject(mgr);
        SerializedProperty catalog = so.FindProperty("catalog");
        string[] guids = AssetDatabase.FindAssets("t:HouseUpgradeData");
        catalog.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
            catalog.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<HouseUpgradeData>(AssetDatabase.GUIDToAssetPath(guids[i]));
        so.ApplyModifiedPropertiesWithoutUndo();
        if (guids.Length == 0) Debug.LogWarning("Suburb: no house upgrades found, so Frank's displays have nothing to sell. Run Farm > Set Up > Build Farmhouse in the Farm scene first.");
    }

    /// <summary>The Kim family (above the General Store) and the residents of the ten houses. Frank was placed with his store.</summary>
    private static void MoveEverybodyIn()
    {
        Debug.Log("Suburb: " + NpcFamilySetupMenu.Build());
        Debug.Log("Suburb: " + SuburbResidentsMenu.Build());
    }

    // ------------------------------------------------------------------ trees, bushes and flower beds scattered over the free grass
    private static void Scatter()
    {
        string[] trees = { "Tree_Green", "Tree_Green", "Tree_Dark", "Tree_Blossom" };
        string[] bushes = { "Bush_Green", "Bush_Green", "Bush_Flowering", "Bush_Rose" };
        string[] flowers = { "Flower_Red", "Flower_Yellow", "Flower_Pink", "Flower_Blue", "Flower_White" };

        // trees: need a clear 3 x 2 patch, and keep a ring of space around them so they don't crowd
        for (int placed = 0, tries = 0; placed < TreeCount && tries < 6000; tries++)
        {
            int x = rng.Next(1, W - 1), y = rng.Next(1, H - 2);
            if (!Free(x - 1, y, x + 1, y + 1)) continue;
            Put(trees[rng.Next(trees.Length)], x + 0.5f, y + 0.25f);
            Take(x - 2, y - 1, x + 2, y + 2, ' ');
            Sketch(x, y, 'T');
            placed++;
        }
        for (int placed = 0, tries = 0; placed < BushCount && tries < 6000; tries++)
        {
            int x = rng.Next(1, W - 2), y = rng.Next(1, H - 1);
            if (!Free(x, y, x + 1, y)) continue;
            Put(bushes[rng.Next(bushes.Length)], x + 1f, y + 0.2f);
            Take(x, y, x + 1, y, 'b');
            placed++;
        }
        // flower beds: a little cluster of 3 to 5 flowers in one or two colours
        for (int placed = 0, tries = 0; placed < FlowerBedCount && tries < 6000; tries++)
        {
            int x = rng.Next(1, W - 1), y = rng.Next(1, H - 1);
            if (!Free(x, y, x, y)) continue;
            string a = flowers[rng.Next(flowers.Length)], b = flowers[rng.Next(flowers.Length)];
            int count = rng.Next(3, 6);
            for (int i = 0; i < count; i++)
                Put(i % 2 == 0 ? a : b, x + 0.15f + (float)rng.NextDouble() * 0.7f, y + 0.1f + (float)rng.NextDouble() * 0.7f);
            Take(x, y, 'f');
            placed++;
        }
    }

    // ------------------------------------------------------------------ edges, entrance, life, player
    private static void MakeEdges()
    {
        var go = new GameObject("MapEdges");
        AddBox(go, new Vector2(-0.5f, H * 0.5f), new Vector2(1f, H + 2f));
        AddBox(go, new Vector2(W + 0.5f, H * 0.5f), new Vector2(1f, H + 2f));
        AddBox(go, new Vector2(W * 0.5f, -0.5f), new Vector2(W + 2f, 1f));
        AddBox(go, new Vector2(W * 0.5f, H + 0.5f), new Vector2(W + 2f, 1f));
    }

    private static void AddBox(GameObject go, Vector2 offset, Vector2 size)
    {
        var c = go.AddComponent<BoxCollider2D>();
        c.offset = offset; c.size = size;
    }

    private static void MakeEntrance()
    {
        float midY = MainY + 1.5f;                                   // the middle of Main Street

        var exit = new GameObject("ExitToFarm");
        exit.transform.position = new Vector3(0.75f, midY, 0f);
        exit.transform.localScale = new Vector3(1.5f, 7f, 1f);       // covers the road and both sidewalks
        var se = exit.AddComponent<SceneExit>();                     // (adds its BoxCollider2D by itself)
        exit.GetComponent<BoxCollider2D>().isTrigger = true;
        Set(se, "sceneName", "Farm"); Set(se, "arriveAt", "SuburbFarmEntry"); Set(se, "promptText", "To the farm");
        SetBool(se, "requireInteract", true);

        var spawn = new GameObject("FromFarm");
        spawn.transform.position = new Vector3(3.5f, midY, 0f);
        var sp = spawn.AddComponent<SceneSpawnPoint>();
        Set(sp, "spawnId", "FromFarm"); SetBool(sp, "isDefault", true);
        var so = new SerializedObject(sp);
        so.FindProperty("faceDirection").vector2Value = Vector2.right;
        so.ApplyModifiedProperties();
        Take(0, MainY - 2, 1, MainY + 4, 'E');
    }

    private static void MakeAmbientLife()
    {
        var go = new GameObject("AmbientLife");
        var spawner = go.AddComponent<PollinatorSpawner>();
        spawner.kinds = new[]
        {
            Insect("Butterfly (orange)", "butterfly_orange", 1f, 1.5f, 0.35f, 8f, new Vector2(2.5f, 6f)),
            Insect("Butterfly (blue)", "butterfly_blue", 1f, 1.5f, 0.35f, 8f, new Vector2(2.5f, 6f)),
            Insect("Butterfly (yellow)", "butterfly_yellow", 1f, 1.5f, 0.35f, 8f, new Vector2(2.5f, 6f)),
            Insect("Bee", "bee", 1.5f, 2.4f, 0.12f, 22f, new Vector2(1.5f, 4f)),
        };
    }

    private static InsectKind Insect(string name, string picture, float weight, float speed, float wobble, float flaps, Vector2 rest)
    {
        return new InsectKind
        {
            name = name, weight = weight, speed = speed, wobble = wobble, flapsPerSecond = flaps, restSeconds = rest,
            frames = new[] { Spr(picture + "_0"), Spr(picture + "_1") }
        };
    }

    private static void SetUpPlayerAndCamera(Tilemap ground)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = new Vector3(3.5f, MainY + 1.5f, 0f);
        else Debug.LogWarning("Suburb: no Player came over from the Mine scene. Add one, then run Farm > Set Up > Game Systems In Scene.");

        var cam = Object.FindAnyObjectByType<CameraFollow2D>();
        if (cam != null)
        {
            var so = new SerializedObject(cam);
            so.FindProperty("limitToMap").boolValue = true;                    // never show past the edge of the map
            so.FindProperty("boundsTilemap").objectReferenceValue = ground;
            so.ApplyModifiedProperties();
            cam.transform.position = new Vector3(8f, MainY + 1.5f, cam.transform.position.z);
        }
    }

    /// <summary>Adds the doorway to the suburbs on the farm's right edge (where its road already runs off the map), and the hardware upgrades to the farmhouse.</summary>
    private static string ConnectFarm()
    {
        if (!File.Exists(FarmScene)) return "Farm scene not found, so no doorway was added there.";
        Scene farm = EditorSceneManager.OpenScene(FarmScene, OpenSceneMode.Single);
        bool changed = false;

        if (GameObject.Find("ExitToSuburbs") == null)
        {
            var exit = new GameObject("ExitToSuburbs");
            exit.transform.position = new Vector3(33.5f, 2.5f, 0f);            // the farm road's last column, rows -1 to 5
            exit.transform.localScale = new Vector3(1.5f, 7f, 1f);
            var se = exit.AddComponent<SceneExit>();
            exit.GetComponent<BoxCollider2D>().isTrigger = true;
            Set(se, "sceneName", "Suburbs"); Set(se, "arriveAt", "FromFarm"); Set(se, "promptText", "To the suburbs");
            SetBool(se, "requireInteract", true);
            changed = true;
        }
        if (GameObject.Find("SuburbFarmEntry") == null)
        {
            var spawn = new GameObject("SuburbFarmEntry");
            spawn.transform.position = new Vector3(31f, 2.5f, 0f);
            var sp = spawn.AddComponent<SceneSpawnPoint>();
            Set(sp, "spawnId", "SuburbFarmEntry");
            var so = new SerializedObject(sp);
            so.FindProperty("faceDirection").vector2Value = Vector2.left;
            so.ApplyModifiedProperties();
            changed = true;
        }
        // the farmhouse gets the upgrades Frank sells (front doors, shutters, cabinets, windows, stoves). Safe to run again: nothing is added twice.
        string upgrades = HouseHardwareUpgradesMenu.Run();
        Debug.Log("Suburb: " + upgrades);
        if (upgrades.StartsWith("Added")) changed = true;

        if (!changed) return "The farm already had its doorway to the suburbs.";
        EditorSceneManager.MarkSceneDirty(farm);
        EditorSceneManager.SaveScene(farm);
        return "The Farm scene has 'ExitToSuburbs' and 'SuburbFarmEntry' (right edge, on the road), and the farmhouse knows the upgrades Frank sells.";
    }

    // ------------------------------------------------------------------ prefabs
    private static Dictionary<string, GameObject> MakePrefabs()
    {
        var d = new Dictionary<string, GameObject>();

        // trees: a trunk and a leafy top that sways. No Breakable script, so they can't be chopped and drop nothing.
        Tree(d, "Tree_Green", "tree_canopy_green");
        Tree(d, "Tree_Dark", "tree_canopy_dark");
        Tree(d, "Tree_Blossom", "tree_canopy_blossom");

        foreach (string[] b in new[] { new[] { "Bush_Green", "bush_green" }, new[] { "Bush_Flowering", "bush_flowering" }, new[] { "Bush_Rose", "bush_rose" } })
        {
            var go = MakeSprite(b[0], b[1], 10);
            AddCollider(go, new Vector2(1.3f, 0.45f), new Vector2(0f, 0.25f));
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.2f;
            Sway(go, 1.3f, 1.3f, 0.02f);
            d[go.name] = Save(go);
        }

        foreach (string c in new[] { "Red", "Yellow", "Pink", "Blue", "White" })
        {
            var go = MakeSprite("Flower_" + c, "flower_" + c.ToLowerInvariant(), 10);
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;      // you walk in front of or behind it, never bump into it
            Sway(go, 5f, 1.7f, 0f);
            go.AddComponent<FlowerSpot>().landingOffset = new Vector2(0f, 0.62f);
            d[go.name] = Save(go);
        }

        {
            var go = MakeSprite("Pot_Flowers", "pot_flowers", 10);
            AddCollider(go, new Vector2(0.6f, 0.35f), new Vector2(0f, 0.2f));
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.2f;
            go.AddComponent<FlowerSpot>().landingOffset = new Vector2(0f, 1.1f);
            d[go.name] = Save(go);

            go = MakeSprite("Pot_Shrub", "pot_shrub", 10);
            AddCollider(go, new Vector2(0.6f, 0.35f), new Vector2(0f, 0.2f));
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.2f;
            d[go.name] = Save(go);

            go = MakeSprite("PlanterBox", "planter_box", 10);
            AddCollider(go, new Vector2(2f, 0.5f), new Vector2(0f, 0.3f));
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.3f;
            d[go.name] = Save(go);
        }

        // the park
        {
            var go = MakeSprite("SoccerField", "soccer_field", 1);              // flat on the ground, under everything that stands up
            d[go.name] = Save(go);

            go = MakeSprite("Play_Sandbox", "play_sandbox", 2);
            d[go.name] = Save(go);

            go = MakeSprite("SoccerGoal", "soccer_goal", 10);
            AddCollider(go, new Vector2(0.8f, 0.5f), new Vector2(0f, 0.25f));
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.3f;
            d[go.name] = Save(go);

            // a ball you can kick by walking into it
            go = MakeSprite("SoccerBall", "soccer_ball", 10);
            go.AddComponent<CircleCollider2D>().radius = 0.2f;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f; rb.linearDamping = 2.5f; rb.angularDamping = 1f; rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;
            d[go.name] = Save(go);

            foreach (object[] p in new[]
            {
                new object[] { "Play_Swings", "play_swings", new Vector2(2.8f, 0.5f) },
                new object[] { "Play_Slide", "play_slide", new Vector2(2.2f, 0.6f) },
                new object[] { "Play_Seesaw", "play_seesaw", new Vector2(2.4f, 0.4f) },
            })
            {
                go = MakeSprite((string)p[0], (string)p[1], 10);
                Vector2 size = (Vector2)p[2];
                AddCollider(go, size, new Vector2(0f, size.y * 0.5f));
                go.AddComponent<BehindPlayerSorter>().thresholdOffset = size.y;
                d[go.name] = Save(go);
            }
        }

        // things that already exist elsewhere in the project
        Reuse(d, "LightPost", "Assets/Project/Art/Lights/Prefabs/LightPost.prefab");
        Reuse(d, "BenchPark", "Assets/Farm/Kiln/Prefabs/Props/BenchPark.prefab");
        Reuse(d, "Mailbox", "Assets/Farm/Kiln/Prefabs/Props/Mailbox.prefab");
        Reuse(d, "SignpostArrows", "Assets/Farm/Kiln/Prefabs/Props/SignpostArrows.prefab");
        return d;
    }

    private static void Tree(Dictionary<string, GameObject> d, string name, string canopyPicture)
    {
        var go = new GameObject(name);
        go.AddComponent<SortingGroup>().sortingOrder = 10;                   // trunk and leaves sort as one thing
        go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.3f;
        AddCollider(go, new Vector2(0.6f, 0.4f), new Vector2(0f, 0.2f));     // only the trunk is solid

        var trunk = new GameObject("Trunk");
        trunk.transform.SetParent(go.transform, false);
        var tsr = trunk.AddComponent<SpriteRenderer>();
        tsr.sprite = Spr("tree_trunk"); tsr.sortingOrder = 0;
        if (lit != null) tsr.sharedMaterial = lit;

        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(go.transform, false);
        canopy.transform.localPosition = new Vector3(0f, 1.05f, 0f);         // sits on top of the trunk
        var csr = canopy.AddComponent<SpriteRenderer>();
        csr.sprite = Spr(canopyPicture); csr.sortingOrder = 1;
        if (lit != null) csr.sharedMaterial = lit;
        Sway(canopy, 1.6f, 1.1f, 0.012f);

        d[name] = Save(go);
    }

    /// <summary>A new object showing one picture (the starting point for every prop prefab).</summary>
    private static GameObject MakeSprite(string name, string picture, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(picture);
        sr.sortingOrder = order;
        if (lit != null) sr.sharedMaterial = lit;
        if (sr.sprite == null) Debug.LogWarning("Suburb: picture '" + picture + ".png' was not found.");
        return go;
    }

    private static void AddCollider(GameObject go, Vector2 size, Vector2 offset)
    {
        var c = go.AddComponent<BoxCollider2D>();
        c.size = size; c.offset = offset;
    }

    private static void Sway(GameObject go, float angle, float speed, float stretch)
    {
        var w = go.AddComponent<WindSway>();
        w.maxAngle = angle; w.speed = speed; w.stretch = stretch;
    }

    private static GameObject Save(GameObject go)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + go.name + ".prefab");
        Object.DestroyImmediate(go);
        AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
        return prefab;
    }

    private static void Reuse(Dictionary<string, GameObject> d, string name, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null) d[name] = prefab;
        else Debug.LogWarning("Suburb: '" + path + "' was not found, so no " + name + " was placed.");
    }

    /// <summary>Puts one copy of a prefab in the scene, under the 'Objects' parent.</summary>
    private static GameObject Put(string prop, float x, float y)
    {
        GameObject prefab;
        if (!props.TryGetValue(prop, out prefab) || prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(objectsParent, false);
        go.transform.position = new Vector3(x, y, 0f);
        return go;
    }

    // ------------------------------------------------------------------ art import
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            // flat things and insects turn around their middle; everything that stands up has its pivot at its base
            bool centre = n == "soccer_field" || n == "play_sandbox" || n == "soccer_ball" || n.StartsWith("butterfly_") || n.StartsWith("bee_");
            ImportSingle(p, centre ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        }
        AssetDatabase.Refresh();
    }

    private static void ImportSingle(string path, SpriteAlignment align)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;        // one sprite per picture, so there are no sprite IDs to keep stable
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

    private static Sprite Spr(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(art + "/" + name + ".png"); }

    // ------------------------------------------------------------------ small helpers
    private static void Add(HashSet<Vector2Int> set, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (x >= 0 && y >= 0 && x < W && y < H) set.Add(new Vector2Int(x, y));
    }

    /// <summary>Marks tiles as taken (and draws them in the text sketch; a space leaves the sketch alone).</summary>
    private static void Take(int x0, int y0, int x1, int y1, char mark)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Take(x, y, mark);
    }

    private static void Take(int x, int y, char mark)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        used[x, y] = true;
        if (mark != ' ') sketch[x, y] = mark;
    }

    private static void Sketch(int x, int y, char mark)
    {
        if (x >= 0 && y >= 0 && x < W && y < H) sketch[x, y] = mark;
    }

    private static bool Free(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (x < 0 || y < 0 || x >= W || y >= H || used[x, y]) return false;
        return true;
    }

    private static void Set(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Suburb: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Suburb: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in scenes) if (s.path == path) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
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

    // ------------------------------------------------------------------ checking the result
    /// <summary>Writes a text drawing of the map to the Console: = road, - sidewalk, H house, S shop, F field, p playground, T tree, b bush, f flowers, L light, E exit, o path node.</summary>
    private static void LogSketch()
    {
        var sb = new System.Text.StringBuilder("SUBURB MAP (top of the map first)\n");
        for (int y = H - 1; y >= 0; y--)
        {
            sb.Append("MAP|");
            for (int x = 0; x < W; x++) sb.Append(sketch[x, y]);
            sb.Append('\n');
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>Takes a picture of the whole map from above and saves it to Logs/suburb_preview.png (only when built from the command line).</summary>
    private static void SavePreview()
    {
        try
        {
            SortingGroup.UpdateAllSortingGroups();      // objects made a moment ago are not sorted yet otherwise
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
            File.WriteAllBytes("Logs/suburb_preview.png", tex.EncodeToPNG());      // (Unity empties the Temp folder when it closes)

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("Suburb: preview picture saved to Logs/suburb_preview.png");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Suburb: could not take the preview picture (" + e.Message + "). The scene itself is fine.");
        }
    }
}
