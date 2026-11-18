using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > City Scene
///
/// Builds the whole City Block scene (Assets/Project/Scenes/City.unity), which lies to the RIGHT of the suburbs:
///   1. sets up the city pictures (Assets/Project/Art/City, drawn by Tools/make_city_art.ps1) and runs
///      Farm > Set Up > City Research and Items (hot dogs, sprinklers, gels, fertilizers, bars, waste items, research list);
///   2. starts from a copy of the Mine scene (Player, camera, HUD, TimeManager, lighting), emptied;
///   3. paints grass, the river along the top, two avenues, two cross streets and the river walk;
///   4. builds four buildings you can enter:
///        the University      - Dr. Hale researches farm improvements for gold and sells what she has worked out;
///        the Sterling tower  - seeds, smelting and house upgrades 5% cheaper than the suburbs; Victor Sterling paces the lobby;
///                              the back room is where the barrels go into the river;
///        City Hall           - the clerk sells city improvements and hands out jobs;
///        the treatment plant - takes waste for a fee;
///      plus nine background buildings, a hot dog cart, litter to pick up and a spot on the bank to throw waste in the river;
///   5. lays the walking network and the named spots, then puts a "visitor" copy of every neighbour who works or studies in the
///      city at their place of work (and points the City stops of their schedules at those places);
///   6. adds the street traffic: strangers on foot, cyclists, cars and scooters (the CityTraffic object);
///   7. joins the road to the suburbs at both ends (the Suburbs scene's 'CityRoad' becomes a doorway).
///
/// The layout numbers are the constants just below: change them and run the menu again.
/// Running it again REPLACES City.unity (it asks first). Assets in Assets/Farm/City and Assets/Farm/Data/City are kept.
/// Command line:  -executeMethod CitySceneBuilder.BuildBatch   (rebuilds and saves a picture to Logs/city_preview.png)
///                -executeMethod CitySceneBuilder.PreviewBatch (only the picture).  Leave out -nographics.
/// Must live in a folder named "Editor".
/// </summary>
public static class CitySceneBuilder
{
    // ------------------------------------------------------------------ where things live
    private const string ScenePath = "Assets/Project/Scenes/City.unity";
    private const string TemplateScene = "Assets/Project/Scenes/Mine.unity";
    private const string SuburbScene = "Assets/Project/Scenes/Suburbs.unity";
    private const string DataDir = "Assets/Farm/City";
    private const string TileDir = DataDir + "/Tiles";
    private const string PrefabDir = DataDir + "/Prefabs";
    private const string LitMaterialPath = "Assets/Resources/Lighting/SpriteLit.mat";
    private const string SuburbPrefabs = "Assets/Farm/Suburb/Prefabs";

    // ------------------------------------------------------------------ the layout (all in tiles; 0,0 is the bottom-left corner)
    private const int W = 100, H = 64;                // size of the map
    private const int RiverY = 58;                    // the river fills every row from here to the top
    private const int WalkY = 55;                     // the river walk: 3 rows of paving below the river
    private const int MainY = 32;                     // Main Avenue: the bottom row of its 3 rows (it continues the suburbs' Main Street)
    private const int CivicY = 12;                    // Civic Street: the bottom row of its 3 rows
    private const int StreetAX = 28, StreetBX = 70;   // the two cross streets: the left column of their 3 columns
    private const int NorthBaseY = 38;                // the university, the tower and the plant stand on this row
    private const int SouthBaseY = 18;                // city hall and its neighbours stand on this row
    private const int Seed = 20261115;                // change this number for a different scatter of trees
    private const float Discount = 0.95f;             // the tower sells everything at 95% of the normal price

    // where each building's front door is, along its street
    private const float UniX = 11f, BooksX = 22f, OfficeAX = 39f, TowerX = 50f, OfficeBX = 61f, PlantX = 87f;      // north row
    private const float CafeX = 7f, MuseumX = 18f, ClinicX = 38f, HallX = 50f, FlatsAX = 62f, MarketX = 80f, FlatsBX = 91f;   // south row

    private const float PlazaY = MainY + 4.5f;        // the middle of the wide pavement north of Main Avenue (where people walk)
    private const float CivicWalkY = CivicY + 4.5f;   // the middle of the pavement north of Civic Street

    private static string art;                         // folder the city pictures are in
    private static Material lit;
    private static bool[,] used;                       // tiles that are already taken
    private static System.Random rng;
    private static Transform objectsParent, buildingsParent, streetRoot, spotRoot;
    private static Dictionary<string, PathNode> nodes;
    private static readonly Dictionary<string, GameObject> propPrefabs = new Dictionary<string, GameObject>();

    // ------------------------------------------------------------------ entry points
    [MenuItem("Farm/Set Up/City Scene", false, 154)]
    public static void BuildFromMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Rebuild the city?", "City.unity already exists. Building again REPLACES it (anything you placed by hand in it is lost).\n\nYour items, research prices and settings are kept.", "Replace it", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string problem = Build(false);
        EditorUtility.DisplayDialog(problem == null ? "City ready" : "Couldn't build the city",
            problem ?? ("Built " + ScenePath + ".\n\nPress Play to walk around. The road on the left edge leads to the suburbs, and the suburbs' road on the right edge now leads here." +
                        "\n\nResearch prices: " + CityAssetsMenu.CatalogPath + "\nSoil items, crop quality and river numbers: " + CityAssetsMenu.SettingsPath +
                        "\nStreet traffic: the 'CityTraffic' object in the scene."), "OK");
    }

    /// <summary>Command line: no questions asked. Look for "CITY BUILD OK" in the log.</summary>
    public static void BuildBatch()
    {
        string problem = Build(true);
        Debug.Log(problem == null ? "CITY BUILD OK" : "CITY BUILD FAILED: " + problem);
    }

    /// <summary>Command line only: opens the saved City scene and takes the overhead picture without rebuilding.</summary>
    public static void PreviewBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SortingGroup.UpdateAllSortingGroups();
        SavePreview();
    }

    // ------------------------------------------------------------------ the build
    private static string Build(bool preview)
    {
        art = HardwareEditorUtil.FindFolder("city_tower");
        if (art == null) return "The city pictures were not found (city_tower.png and friends belong in Assets/Project/Art/City). Run Tools/make_city_art.ps1 first.";
        if (!File.Exists(TemplateScene)) return "The Mine scene (" + TemplateScene + ") is used as the starting point, and it is missing.";

        lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        rng = new System.Random(Seed);
        used = new bool[W, H];
        propPrefabs.Clear();

        ImportArt();
        HardwareEditorUtil.EnsureFolder(TileDir);
        HardwareEditorUtil.EnsureFolder(PrefabDir);
        string items = CityAssetsMenu.Build();
        Debug.Log("City: " + items);
        if (!items.StartsWith("Made")) return items;
        AssetDatabase.SaveAssets();

        // ---- start from a copy of the Mine scene (Player, camera, HUD, TimeManager, lighting all come with it) ----
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(TemplateScene, ScenePath)) return "Could not copy the Mine scene to " + ScenePath + ".";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CleanTemplate(scene);
        lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);     // (opening a scene can unload assets, so load it again)

        Tilemap ground = FindMap("Ground"), paths = FindMap("Paths"), decor = FindMap("PathDecor");
        if (ground == null || paths == null) return "The copied scene has no 'Ground' or 'Paths' tilemap under its Grid.";
        if (decor == null) decor = MakeMap(ground.transform.parent, "PathDecor", 3);

        GameObject objects = GameObject.Find("Objects");
        if (objects == null) objects = new GameObject("Objects");
        objectsParent = objects.transform;
        buildingsParent = new GameObject("Buildings").transform;

        PaintGround(ground);
        PaintStreets(paths, decor);
        LayStreetPaths();                 // first: the buildings link their doors to these nodes
        BuildUniversity();
        BuildTower();
        BuildCityHall();
        BuildPlant();
        BuildBackground();
        PlaceRiverThings();
        PlaceStreetThings();
        PlaceImprovements();
        Scatter();
        MakeEdges();
        MakeEntrances();
        MakeTraffic();
        MakeHouseManager();
        string visitors = PlaceVisitors();
        Debug.Log("City: " + visitors);
        SetUpPlayerAndCamera(ground);
        PathNode.MarkDirty();

        paths.RefreshAllTiles();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        if (preview) SavePreview();

        Debug.Log("City: " + ConnectSuburbs());
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return null;
    }

    // ------------------------------------------------------------------ art
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            // sheets are cut into cells with STABLE sprite IDs (HardwareEditorUtil.SliceSheet uses StableSpriteId)
            if (n.StartsWith("npc_") || n == "ceo_acts") HardwareEditorUtil.SliceSheet(p, 16, 28, SpriteAlignment.BottomCenter);
            else if (n.StartsWith("portraits_")) HardwareEditorUtil.SliceSheet(p, 64, 64, SpriteAlignment.Center);
            else
            {
                // tiles, icons and things that hang on a wall turn around their middle; everything that stands up has its pivot at its base
                bool centre = n.StartsWith("floor_") || n.StartsWith("wall_") || n.StartsWith("partition_") || n.StartsWith("tile_") || n.StartsWith("icon_")
                              || n.StartsWith("river_ripple") || n == "uni_chalkboard" || n == "tower_elevator" || n == "tower_logo" || n == "tower_staff_sign"
                              || n == "hall_notice_board" || n == "plant_pipes" || n == "river_boom";
                HardwareEditorUtil.ImportSingle(p, centre ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            }
        }
        AssetDatabase.Refresh();
    }

    private static Sprite Spr(string name) { return HardwareEditorUtil.Spr(art, name); }

    private static Tile MakeTile(string name)
    {
        var tile = HardwareEditorUtil.LoadOrCreate<Tile>(TileDir + "/" + name + ".asset");
        tile.sprite = Spr(name);
        EditorUtility.SetDirty(tile);
        return tile;
    }

    // ------------------------------------------------------------------ template clean-up (the same as the suburbs)
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
            foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true)) { map.ClearAllTiles(); map.CompressBounds(); }
    }

    private static Tilemap FindMap(string name)
    {
        GameObject grid = GameObject.Find("Grid");
        if (grid == null) return null;
        foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true)) if (map.name == name) return map;
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

    // ------------------------------------------------------------------ ground, river and streets
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
            var t = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Farm/Kiln/Tiles/Spring/Grass.asset");
            if (t != null) grass.Add(t);
        }
        if (grass.Count == 0) Debug.LogWarning("City: no grass tile found, the ground was left empty.");

        Tile water = MakeTile("tile_water"), waterB = MakeTile("tile_water_b"), bank = MakeTile("tile_bank");
        Tile sludge = MakeTile("tile_sludge"), sludgeB = MakeTile("tile_sludge_b");

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                TileBase tile;
                if (y == RiverY) tile = bank;                                   // the stone edge of the river
                else if (y > RiverY)
                {
                    // the river flows WEST (towards the suburbs and the farm): the water downstream of the tower's pipe is dirty
                    bool dirty = x < TowerX + 3 && y <= RiverY + 3 && rng.NextDouble() < 0.25 + 0.5 * (x / (TowerX + 3f));
                    tile = dirty ? (rng.Next(2) == 0 ? sludge : sludgeB) : (rng.Next(3) == 0 ? waterB : water);
                }
                else tile = grass.Count > 0 ? grass[rng.Next(grass.Count)] : null;
                ground.SetTile(new Vector3Int(x, y, 0), tile);
                if (y >= RiverY) used[x, y] = true;
            }
    }

    private static void PaintStreets(Tilemap paths, Tilemap decor)
    {
        var roadPreset = AssetDatabase.LoadAssetAtPath<GeographyPreset>("Assets/Farm/Data/Geography/Road.asset");
        var walkPreset = AssetDatabase.LoadAssetAtPath<GeographyPreset>("Assets/Farm/Data/Geography/Sidewalk.asset");
        if (roadPreset == null || walkPreset == null || Piece(roadPreset.wideTiles, 4) == null || Piece(walkPreset.wideTiles, 4) == null)
            Debug.LogWarning("City: the Road / Sidewalk geography presets have no tiles. Run Farm > Build Art > Path Palettes and Farm > Build Art > Geography Presets, then build the city again.");

        var road = new HashSet<Vector2Int>();
        var walk = new HashSet<Vector2Int>();

        // roads (each is 3 tiles wide)
        Add(road, 0, MainY, W - 1, MainY + 2);                                // Main Avenue, edge to edge
        Add(road, 0, CivicY, W - 1, CivicY + 2);                              // Civic Street, edge to edge
        Add(road, StreetAX, CivicY, StreetAX + 2, WalkY - 1);                 // the two cross streets, up to the river walk
        Add(road, StreetBX, CivicY, StreetBX + 2, WalkY - 1);

        // pavements
        Add(walk, 0, MainY + 3, W - 1, NorthBaseY - 1);                       // the wide pavement in front of the university, the tower and the plant
        Add(walk, 0, MainY - 2, W - 1, MainY - 1);
        Add(walk, 0, CivicY + 3, W - 1, SouthBaseY - 1);                      // in front of city hall
        Add(walk, 0, CivicY - 2, W - 1, CivicY - 1);
        Add(walk, 0, WalkY, W - 1, RiverY - 1);                               // the river walk
        foreach (int sx in new[] { StreetAX, StreetBX })
        {
            Add(walk, sx - 2, CivicY - 2, sx - 1, RiverY - 1);
            Add(walk, sx + 3, CivicY - 2, sx + 4, RiverY - 1);
        }
        foreach (Vector2Int c in road) walk.Remove(c);

        foreach (Vector2Int c in walk) { paths.SetTile(new Vector3Int(c.x, c.y, 0), StreetTile(walkPreset, walk, c)); used[c.x, c.y] = true; }
        foreach (Vector2Int c in road) { paths.SetTile(new Vector3Int(c.x, c.y, 0), StreetTile(roadPreset, road, c)); used[c.x, c.y] = true; }

        // dashed centre lines (2 on, 2 off), left out where streets cross
        if (roadPreset != null)
        {
            foreach (int ry in new[] { MainY + 1, CivicY + 1 })
                for (int x = 0; x < W; x++)
                    if (x % 4 < 2 && !NearCross(x)) Dash(decor, x, ry, roadPreset.centerHorizontal);
            foreach (int sx in new[] { StreetAX + 1, StreetBX + 1 })
                for (int y = CivicY + 4; y < WalkY - 1; y++)
                    if (y % 4 < 2 && (y < MainY - 2 || y > MainY + 4)) Dash(decor, sx, y, roadPreset.centerVertical);
        }
    }

    private static bool NearCross(int x)
    {
        return (x >= StreetAX - 2 && x <= StreetAX + 4) || (x >= StreetBX - 2 && x <= StreetBX + 4);
    }

    /// <summary>Which tile of a road or pavement belongs on one cell (edge, corner or middle). The same rules as SuburbSceneBuilder and the Geography Brush.</summary>
    private static TileBase StreetTile(GeographyPreset preset, HashSet<Vector2Int> street, Vector2Int c)
    {
        if (preset == null) return null;
        bool n = In(street, c.x, c.y + 1), s = In(street, c.x, c.y - 1), e = In(street, c.x + 1, c.y), w = In(street, c.x - 1, c.y);
        TileBase middle = Piece(preset.wideTiles, 4);
        TileBase fill = RandomPiece(preset.fillTiles);
        if (!preset.useEdgeTiles) return fill != null ? fill : middle;
        if ((!n && !s) || (!e && !w)) return preset.autoTile != null ? preset.autoTile : (middle != null ? middle : fill);

        TileBase t;
        if (!n) t = !w ? Piece(preset.wideTiles, 0) : (!e ? Piece(preset.wideTiles, 2) : Piece(preset.wideTiles, 1));
        else if (!s) t = !w ? Piece(preset.wideTiles, 6) : (!e ? Piece(preset.wideTiles, 8) : Piece(preset.wideTiles, 7));
        else if (!w) t = Piece(preset.wideTiles, 3);
        else if (!e) t = Piece(preset.wideTiles, 5);
        else
        {
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

    /// <summary>Is this cell part of the street? Cells past the left and right edge count as street, so roads run off the map cleanly.</summary>
    private static bool In(HashSet<Vector2Int> street, int x, int y)
    {
        if (x < 0 || x >= W) return street.Contains(new Vector2Int(Mathf.Clamp(x, 0, W - 1), y));
        return street.Contains(new Vector2Int(x, y));
    }

    private static TileBase Piece(TileBase[] tiles, int i) { return tiles != null && i < tiles.Length ? tiles[i] : null; }

    private static TileBase RandomPiece(TileBase[] tiles)
    {
        if (tiles == null || tiles.Length == 0) return null;
        for (int tries = 0; tries < 8; tries++) { TileBase t = tiles[rng.Next(tiles.Length)]; if (t != null) return t; }
        return null;
    }

    private static void Dash(Tilemap decor, int x, int y, TileBase tile) { if (decor != null && tile != null) decor.SetTile(new Vector3Int(x, y, 0), tile); }

    // ------------------------------------------------------------------ where NPCs can walk
    /// <summary>
    /// The outdoor walking network for the neighbours who come to work here: along both wide pavements and down the two cross streets.
    /// 'SuburbsExit' on the left edge is where they arrive from (and leave for) the suburbs. Each building adds its own door node.
    /// Select 'PathNetwork_City' in the Hierarchy to see it drawn in the Scene view.
    /// </summary>
    private static void LayStreetPaths()
    {
        streetRoot = new GameObject("PathNetwork_City").transform;
        spotRoot = new GameObject("City Spots").transform;
        nodes = new Dictionary<string, PathNode>();

        PathNode exit = Node("SuburbsExit", 0.5f, PlazaY);
        exit.leadsToScene = "Suburbs";
        float aw = StreetAX - 1f, ae = StreetAX + 4f, bw = StreetBX - 1f, be = StreetBX + 4f;     // the middle of each cross street's pavements

        // the north pavement, past the university, the tower and the plant
        Node("N_Uni", UniX, PlazaY); Node("N_AW", aw, PlazaY); Node("N_AE", ae, PlazaY); Node("N_Tower", TowerX, PlazaY);
        Node("N_BW", bw, PlazaY); Node("N_BE", be, PlazaY); Node("N_Plant", PlantX, PlazaY);
        Link("SuburbsExit", "N_Uni", "N_AW", "N_AE", "N_Tower", "N_BW", "N_BE", "N_Plant");

        // the pavement in front of city hall
        Node("C_Cafe", CafeX, CivicWalkY); Node("C_Museum", MuseumX, CivicWalkY); Node("C_AW", aw, CivicWalkY); Node("C_AE", ae, CivicWalkY);
        Node("C_Clinic", ClinicX, CivicWalkY); Node("C_Hall", HallX, CivicWalkY); Node("C_BW", bw, CivicWalkY); Node("C_BE", be, CivicWalkY); Node("C_Market", MarketX, CivicWalkY);
        Link("C_Cafe", "C_Museum", "C_AW", "C_AE", "C_Clinic", "C_Hall", "C_BW", "C_BE", "C_Market");

        // down the cross streets (both pavements of each)
        Link("N_AW", "C_AW"); Link("N_AE", "C_AE"); Link("N_BW", "C_BW"); Link("N_BE", "C_BE");
    }

    private static PathNode Node(string id, float x, float y)
    {
        PathNode n = PathToolsMenu.MakeNode(id, new Vector2(x, y), streetRoot);
        nodes[id] = n;
        return n;
    }

    private static void Link(params string[] ids)
    {
        for (int i = 0; i + 1 < ids.Length; i++)
        {
            PathNode a, b;
            if (!nodes.TryGetValue(ids[i], out a) || !nodes.TryGetValue(ids[i + 1], out b))
            {
                Debug.LogWarning("City: can't link path nodes '" + ids[i] + "' and '" + ids[i + 1] + "': one of them does not exist.");
                continue;
            }
            PathToolsMenu.Link(a, b);
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

    /// <summary>A door for NPCs: stepping onto 'outside' brings them out of 'inside', and the other way round.</summary>
    private static void Portal(string outside, string inside)
    {
        PathNode a = nodes[outside], b = nodes[inside];
        a.portalTo = b; b.portalTo = a;
        EditorUtility.SetDirty(a); EditorUtility.SetDirty(b);
    }

    // ------------------------------------------------------------------ building blocks
    /// <summary>The outside of a building: one picture, solid up to 'solid' tiles, drawn in front of or behind the player as needed.</summary>
    private static GameObject Building(string name, string picture, float x, int baseY, int widthTiles, int heightTiles)
    {
        var go = new GameObject(name);
        go.transform.SetParent(buildingsParent, false);
        go.transform.position = new Vector3(x, baseY, 0f);
        go.AddComponent<SortingGroup>().sortingOrder = 10;
        go.AddComponent<BehindPlayerSorter>().thresholdOffset = 3f;
        var pic = new GameObject("Building");
        pic.transform.SetParent(go.transform, false);
        var sr = pic.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(picture);
        if (lit != null) sr.sharedMaterial = lit;
        if (sr.sprite == null) Debug.LogWarning("City: picture '" + picture + ".png' was not found.");
        // solid except for the top two tiles (the roof edge), which you can walk behind
        float solid = Mathf.Max(3f, heightTiles - 2f);
        var body = go.AddComponent<BoxCollider2D>();
        body.size = new Vector2(widthTiles - 0.2f, solid); body.offset = new Vector2(0f, solid * 0.5f);

        int left = Mathf.FloorToInt(x - widthTiles * 0.5f);
        Take(left - 1, baseY - 1, left + widthTiles, baseY + heightTiles);
        return go;
    }

    /// <summary>
    /// The inside of a building: a room far away from the streets, like the shops in the suburbs. The floor and walls are tilemaps
    /// painted by an InteriorStyle, so Farm > Set Up > Rebuild Selected Interior works on it too.
    /// </summary>
    private static GameObject Room(string name, int index, int width, int floorRows, string floor, string wallUpper, string wallBase,
                                   FloorZone[] zones, PartitionRect[] partitions, out HouseInterior interior)
    {
        const int wallRows = 3;
        var room = new GameObject(name);
        room.transform.position = new Vector3(600f + index * 100f, 0f, 0f);
        room.AddComponent<Grid>();
        interior = room.AddComponent<HouseInterior>();

        var back = Child(room, "Backdrop", new Vector3(width * 0.5f, (floorRows + wallRows) * 0.5f, 0f));
        string backdropFolder = HardwareEditorUtil.FindFolder("backdrop_dark");      // shared with the general store
        var bsr = back.AddComponent<SpriteRenderer>();
        bsr.sprite = backdropFolder != null ? HardwareEditorUtil.Spr(backdropFolder, "backdrop_dark") : null; bsr.sortingOrder = -5;
        back.transform.localScale = new Vector3(120f, 100f, 1f);

        var floorGo = Child(room, "Floor", Vector3.zero);
        var floorMap = floorGo.AddComponent<Tilemap>(); floorGo.AddComponent<TilemapRenderer>().sortingOrder = 0;
        var wallGo = Child(room, "Wall", Vector3.zero);
        var wallMap = wallGo.AddComponent<Tilemap>(); wallGo.AddComponent<TilemapRenderer>().sortingOrder = 1;
        var partGo = Child(room, "Partitions", Vector3.zero);
        var partMap = partGo.AddComponent<Tilemap>(); partGo.AddComponent<TilemapRenderer>().sortingOrder = 4;
        partGo.AddComponent<TilemapCollider2D>();

        var edges = Child(room, "Edges", Vector3.zero);
        Box(edges, new Vector2(-0.5f, (floorRows + wallRows) * 0.5f), new Vector2(1f, floorRows + wallRows + 2f), false);
        Box(edges, new Vector2(width + 0.5f, (floorRows + wallRows) * 0.5f), new Vector2(1f, floorRows + wallRows + 2f), false);
        Box(edges, new Vector2(width * 0.5f, -0.5f), new Vector2(width + 2f, 1f), false);
        Box(edges, new Vector2(width * 0.5f, floorRows + wallRows * 0.5f), new Vector2(width, wallRows), false);

        var so = new SerializedObject(interior);
        so.FindProperty("width").intValue = width;
        so.FindProperty("floorRows").intValue = floorRows;
        so.FindProperty("wallRows").intValue = wallRows;
        so.FindProperty("floorTilemap").objectReferenceValue = floorMap;
        so.FindProperty("wallTilemap").objectReferenceValue = wallMap;
        so.ApplyModifiedPropertiesWithoutUndo();

        var style = room.AddComponent<InteriorStyle>();
        style.floorMap = floorMap; style.wallMap = wallMap; style.partitionMap = partMap;
        style.floorTile = MakeTile(floor); style.wallUpper = MakeTile(wallUpper); style.wallBase = MakeTile(wallBase); style.partitionTile = MakeTile("partition_city");
        style.zones = zones ?? new FloorZone[0];
        style.partitions = partitions ?? new PartitionRect[0];
        style.Apply();
        return room;
    }

    /// <summary>
    /// Joins a building to its room: the front door (press the action button to go in), the way back out, and the path nodes
    /// that let NPCs use the same door. 'streetNode' is the node on the pavement in front of the door; 'key' names the new nodes.
    /// </summary>
    private static void Doors(GameObject building, GameObject room, HouseInterior interior, float roomDoorX, string prompt, string key, string streetNode)
    {
        var frontDoor = Child(building, "FrontDoor", new Vector3(0f, 0.3f, 0f));
        Box(frontDoor, Vector2.zero, new Vector2(2.4f, 1.8f), true);
        var exitSpot = Child(building, "ExitSpot", new Vector3(0f, -0.9f, 0f));
        var entry = Child(room, "EntrySpawn", new Vector3(roomDoorX, 1.4f, 0f));
        var exitDoorGo = Child(room, "ExitDoor", new Vector3(roomDoorX, 0f, 0f));
        Box(exitDoorGo, Vector2.zero, new Vector2(2.4f, 1.2f), true);

        var inDoor = frontDoor.AddComponent<HouseDoor>();
        HardwareEditorUtil.SetRef(inDoor, "destination", entry.transform);
        HardwareEditorUtil.SetRef(inDoor, "enterInterior", interior);
        HardwareEditorUtil.SetString(inDoor, "prompt", prompt);
        var outDoor = exitDoorGo.AddComponent<HouseDoor>();
        HardwareEditorUtil.SetRef(outDoor, "destination", exitSpot.transform);
        HardwareEditorUtil.SetString(outDoor, "prompt", "Go outside");

        Vector2 b = building.transform.position, r = room.transform.position;
        Node(key + "_Door", b.x, b.y - 0.6f);
        Node(key + "_In", r.x + roomDoorX, r.y + 1.5f);
        Node(key + "_Mid", r.x + roomDoorX, r.y + 4.2f);
        Link(streetNode, key + "_Door");
        Link(key + "_In", key + "_Mid");
        Portal(key + "_Door", key + "_In");
    }

    /// <summary>
    /// Puts one copy of a picture in the scene as a prop. The first time a picture is used a prefab is made for it in
    /// Assets/Farm/City/Prefabs (labelled for the Level Editor's Objects palette). colW / colH = the solid base in tiles (0 = not solid).
    /// </summary>
    private static GameObject Prop(Transform parent, string picture, float x, float y, float colW = 0f, float colH = 0f, int order = 5, bool outdoor = false)
    {
        GameObject prefab;
        if (!propPrefabs.TryGetValue(picture, out prefab) || prefab == null)
        {
            var src = new GameObject("City_" + picture);
            var sr = src.AddComponent<SpriteRenderer>();
            sr.sprite = Spr(picture);
            sr.sortingOrder = order;
            if (outdoor && lit != null) sr.sharedMaterial = lit;
            if (sr.sprite == null) Debug.LogWarning("City: picture '" + picture + ".png' was not found.");
            if (colW > 0f)
            {
                var bc = src.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(colW, colH); bc.offset = new Vector2(0f, colH * 0.5f);
                src.AddComponent<BehindPlayerSorter>().thresholdOffset = colH;
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(src, PrefabDir + "/" + src.name + ".prefab");
            Object.DestroyImmediate(src);
            AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
            propPrefabs[picture] = prefab;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        return go;
    }

    /// <summary>Makes a prop play a short loop of pictures (the smelter glows, the chute swallows barrels...).</summary>
    private static void Flip(GameObject go, float fps, params string[] pictures)
    {
        if (go == null) return;
        var book = go.AddComponent<SpriteFlipbook>();
        var frames = new List<Sprite>();
        foreach (string p in pictures) { Sprite s = Spr(p); if (s != null) frames.Add(s); }
        book.frames = frames.ToArray();
        book.framesPerSecond = fps;
    }

    /// <summary>Something you can look at: it says one of the lines. (Adds a trigger area if the object has no collider yet.)</summary>
    private static void Look(GameObject go, string prompt, params string[] lines)
    {
        if (go == null) return;
        if (go.GetComponent<Collider2D>() == null) Box(go, new Vector2(0f, 0.5f), new Vector2(1.2f, 1.2f), true);     // (the collider goes on first: StoreFlavor needs one)
        var f = go.AddComponent<StoreFlavor>();
        f.prompt = prompt; f.lines = lines;
    }

    /// <summary>Somebody who stands in one place and talks. 'sheet' is the name after npc_ / portraits_ in the City art folder.</summary>
    private static CityPerson Person(Transform parent, string name, string sheet, float x, float y, Color color, string[] hello, string[] lines, bool oneLine = false)
    {
        var go = new GameObject("Npc_" + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = HardwareEditorUtil.Frame(art, "npc_" + sheet, 0, 0);
        if (sr.sprite == null) Debug.LogWarning("City: no walk pictures found for " + name + " (npc_" + sheet + ".png).");
        var p = go.AddComponent<CityPerson>();
        p.displayName = name; p.personId = sheet + "_" + name.ToLowerInvariant().Replace(' ', '_'); p.nameColor = color;
        p.firstMeeting = hello ?? new string[0]; p.lines = lines; p.oneLineOnly = oneLine;
        p.portraitIdle = HardwareEditorUtil.Frame(art, "portraits_" + sheet, 0, 0); p.portraitTalk = HardwareEditorUtil.Frame(art, "portraits_" + sheet, 1, 0);
        p.portraitHappy = HardwareEditorUtil.Frame(art, "portraits_" + sheet, 2, 0); p.portraitBlink = HardwareEditorUtil.Frame(art, "portraits_" + sheet, 3, 0);
        p.dialoguePanel = HardwareEditorUtil.FindSprite("ui_dialog_panel"); p.portraitFrame = HardwareEditorUtil.FindSprite("ui_dialog_frame");
        return p;
    }

    private static FloorZone Zone(string label, int x, int y, int w, int h, string tile)
    {
        return new FloorZone { label = label, area = new RectInt(x, y, w, h), tile = MakeTile(tile) };
    }

    private static PartitionRect Part(string label, int x, int y, int w, int h)
    {
        return new PartitionRect { label = label, area = new RectInt(x, y, w, h) };
    }

    // ------------------------------------------------------------------ the university
    private static void BuildUniversity()
    {
        GameObject ext = Building("University", "city_university", UniX, NorthBaseY, 14, 12);
        HouseInterior interior;
        GameObject room = Room("UniversityInterior", 0, 22, 12, "floor_uni", "wall_uni_upper", "wall_uni_base", null, null, out interior);
        Transform t = room.transform;
        Doors(ext, room, interior, 11f, "Enter the university", "Uni", "N_Uni");

        // the lecture end of the room (top), student desks in the middle, the research lab on the right
        Prop(t, "uni_chalkboard", 11f, 13.4f, 0f, 0f, 3);
        Prop(t, "uni_lectern", 11f, 10.6f, 0.8f, 0.5f);
        Prop(t, "uni_bookshelf", 2f, 11.1f, 1.9f, 0.8f); Prop(t, "uni_bookshelf", 4.1f, 11.1f, 1.9f, 0.8f); Prop(t, "uni_bookshelf", 19.9f, 11.1f, 1.9f, 0.8f);
        foreach (float dx in new[] { 5f, 8f }) foreach (float dy in new[] { 7.6f, 5.4f }) Prop(t, "uni_student_desk", dx, dy, 1.4f, 0.5f);
        GameObject bench = Prop(t, "uni_lab_bench", 17.5f, 8f, 2.9f, 0.8f);
        Look(bench, "Look", "Beakers, a microscope and something green that is definitely alive.");
        GameObject trial = Prop(t, "uni_plant_trial", 19.5f, 5.4f, 1.9f, 0.7f);
        Look(trial, "Look", "Six seedlings, each with a neat label. Number four is winning.");
        Prop(t, "city_potted_palm", 1.2f, 1f, 0.6f, 0.4f); Prop(t, "city_potted_palm", 20.8f, 1f, 0.6f, 0.4f);

        // Dr. Hale and her desk: talk to her, or use the desk, to fund research and buy what she has worked out
        GameObject desk = Prop(t, "uni_research_desk", 15.5f, 2.6f, 2.9f, 0.9f);
        var research = desk.AddComponent<ResearchDesk>();
        HardwareEditorUtil.SetRef(research, "catalog", AssetDatabase.LoadAssetAtPath<ResearchCatalog>(CityAssetsMenu.CatalogPath));
        CityPerson hale = Person(t, "Dr. Hale", "scientist", 18.2f, 2.9f, new Color(0.2f, 0.55f, 0.55f),
            new[] { "Oh! A visitor. And those are farmer's hands, if I ever saw them. Dr. Miriam Hale, agricultural science.",
                    "Twenty years I have been trying to get a real farmer in here. I have ideas: sprinklers, gels that hold water in the soil, fertilizers that make a crop shine.",
                    "Ideas cost money to test, I'm afraid. Fund the work and I will sell you whatever comes out of it at cost." },
            new[] { "Water, light, soil. Everything else is just paperwork.",
                    "A Platinum turnip is still a turnip. It is simply a turnip that knows its worth.",
                    "The river samples have been strange lately. Purple is not a colour water should be.",
                    "My students think food comes from a shop. Please, tell them where it comes from. Loudly." });
        hale.service = research; hale.serviceLabel = "Talk about research";
        EditorUtility.SetDirty(hale);

        // where the neighbours from the suburbs stand: Grace lectures, Bea is in the lab, Dev and Nia are at school desks, Soo-ah studies
        Vector2 r = room.transform.position;
        Spot("UniLectern", r.x + 12.4f, r.y + 10.2f, Vector2.down, "Uni_Mid");
        Spot("UniLab", r.x + 17.5f, r.y + 7.1f, Vector2.up, "Uni_Mid");
        Spot("UniDesk1", r.x + 5f, r.y + 6.7f, Vector2.up, "Uni_Mid");
        Spot("UniDesk2", r.x + 8f, r.y + 6.7f, Vector2.up, "Uni_Mid");
        Spot("UniStudy", r.x + 5f, r.y + 4.5f, Vector2.up, "Uni_Mid");

        // two students who are always here
        Person(t, "Student", "student_a", 8f, 4.5f, new Color(0.6f, 0.3f, 0.3f), null, new[] { "Shh. Finals.", "Dr. Hale says soil is alive. I haven't slept since.", "Is it true you grow your OWN food? Like, on purpose?" }, true);
        Person(t, "Student", "student_b", 3f, 9.2f, new Color(0.6f, 0.5f, 0.2f), null, new[] { "I'm looking for a book on beetles. For a nine-year-old. She has very specific questions.", "The library closes at nine. I live here until nine." }, true);
    }

    // ------------------------------------------------------------------ the Sterling tower
    private static void BuildTower()
    {
        GameObject ext = Building("SterlingTower", "city_tower", TowerX, NorthBaseY, 12, 18);
        HouseInterior interior;
        // 26 wide, 16 deep. The top five rows are the back room, behind a wall with one doorway (x 21-22).
        GameObject room = Room("TowerInterior", 1, 26, 16, "floor_tower", "wall_tower_upper", "wall_tower_base",
            new[] { Zone("Red carpet", 12, 0, 2, 7, "floor_tower_gold"), Zone("Back room", 0, 11, 26, 5, "floor_tower_back") },
            new[] { Part("Back wall (left)", 0, 10, 21, 1), Part("Back wall (right)", 23, 10, 3, 1) }, out interior);
        Transform t = room.transform;
        Doors(ext, room, interior, 13f, "Enter the Sterling tower", "Tower", "N_Tower");

        // ---- the lobby
        Prop(t, "tower_logo", 13f, 11.6f, 0f, 0f, 6);
        Prop(t, "tower_elevator", 7f, 11.5f, 0f, 0f, 6); Prop(t, "tower_elevator", 17f, 11.5f, 0f, 0f, 6);
        Prop(t, "tower_staff_sign", 19.5f, 11.2f, 0f, 0f, 6);
        Prop(t, "tower_statue", 9.5f, 1.2f, 1.2f, 0.6f); Prop(t, "tower_statue", 16.5f, 1.2f, 1.2f, 0.6f);
        Prop(t, "city_potted_palm", 1.2f, 9f, 0.6f, 0.4f); Prop(t, "city_potted_palm", 24.8f, 1f, 0.6f, 0.4f);
        GameObject reception = Prop(t, "tower_reception", 13f, 7.4f, 3.9f, 0.9f);
        Look(reception, "Look", "The desk is solid marble. The pen is chained to it.");

        // seeds: every seed in the game that has a Buy Price, 5% cheaper
        GameObject seeds = Prop(t, "tower_seed_kiosk", 4f, 6.4f, 2.9f, 0.9f);
        var seedShop = seeds.AddComponent<ShopCounter>();
        HardwareEditorUtil.SetString(seedShop, "shopName", "Sterling Seeds (5% off)");
        HardwareEditorUtil.SetObjects(seedShop, "stock", ItemsOfType(ItemData.ItemType.Seed));
        SetFloat(seedShop, "priceMultiplier", Discount);

        // house upgrades and building materials, 5% cheaper
        GameObject home = Prop(t, "tower_upgrade_kiosk", 4f, 2.4f, 2.9f, 0.9f);
        var homeShop = home.AddComponent<HardwareDisplay>();
        HardwareEditorUtil.SetString(homeShop, "title", "Sterling Home (5% off)");
        HardwareEditorUtil.SetString(homeShop, "prompt", "Browse house upgrades");
        var so = new SerializedObject(homeShop);
        var cats = (HouseCategory[])System.Enum.GetValues(typeof(HouseCategory));
        SerializedProperty up = so.FindProperty("upgrades");
        up.arraySize = cats.Length;
        for (int i = 0; i < cats.Length; i++) up.GetArrayElementAtIndex(i).enumValueIndex = i;
        so.ApplyModifiedPropertiesWithoutUndo();
        var mats = new List<Object>();
        foreach (string m in new[] { "WoodPlank", "PaintBucket", "StoneBrick", "FabricRoll", "GlassPane" }) { ItemData it = HardwareEditorUtil.FindItem(m); if (it != null) mats.Add(it); }
        HardwareEditorUtil.SetObjects(homeShop, "materials", mats);
        SetFloat(homeShop, "priceMultiplier", Discount);

        // the smelter: 3 ore + 1 coal + a fee = 1 bar
        GameObject furnace = Prop(t, "tower_smelter_0", 22f, 5f, 2.9f, 1.2f);
        Flip(furnace, 3f, "tower_smelter_0", "tower_smelter_1");
        var smelter = furnace.AddComponent<Smelter>();
        ItemData coal = HardwareEditorUtil.FindItem("Coal");
        foreach (string[] m in new[] { new[] { "CopperOre", "CopperBar", "10" }, new[] { "IronOre", "IronBar", "20" }, new[] { "GoldOre", "GoldBar", "40" }, new[] { "PlatinumOre", "PlatinumBar", "80" } })
        {
            ItemData ore = HardwareEditorUtil.FindItem(m[0]), bar = CityAssetsMenu.Item(m[1]);
            if (ore == null || bar == null) { Debug.LogWarning("City: the smelter can't make " + m[1] + " (" + m[0] + " or the bar item is missing)."); continue; }
            smelter.melts.Add(new Smelter.Melt { ore = ore, oreCount = 3, fuel = coal, fuelCount = coal != null ? 1 : 0, bar = bar, barCount = 1, fee = int.Parse(m[2]) });
        }
        SetFloat(smelter, "priceMultiplier", Discount);
        EditorUtility.SetDirty(smelter);

        // the staff: one line each, and that is all you will get out of them
        Color grey = new Color(0.4f, 0.42f, 0.5f);
        Person(t, "Receptionist", "worker_b", 13f, 8.6f, grey, null, new[] { "Welcome to Sterling. Do you have an appointment? Nobody does." }, true);
        Person(t, "Sales Associate", "worker_a", 6.3f, 6.6f, grey, null, new[] { "Seeds are five percent off. I don't know what any of them grow into." }, true);
        Person(t, "Sales Associate", "worker_d", 6.3f, 2.6f, grey, null, new[] { "Doors, floors, windows. All flat-packed. Assembly is your problem." }, true);
        Person(t, "Technician", "worker_c", 19.7f, 5.4f, grey, null, new[] { "Three ore, one coal, one fee. Stand behind the line, please." }, true);
        Person(t, "Security", "guard", 23.6f, 9f, new Color(0.2f, 0.25f, 0.4f), null, new[] { "Staff only past this door. ...You're going in anyway, aren't you." }, true);

        // ---- Victor Sterling himself
        Prop(t, "tower_ceo_desk", 20f, 8.2f, 3.9f, 0.9f);
        var ceoGo = new GameObject("Npc_VictorSterling");
        ceoGo.transform.SetParent(t, false);
        ceoGo.transform.localPosition = new Vector3(13f, 4.6f, 0f);
        ceoGo.AddComponent<SpriteRenderer>().sprite = HardwareEditorUtil.Frame(art, "npc_ceo", 0, 0);
        var ceo = ceoGo.AddComponent<CeoNpc>();
        ceo.down = HardwareEditorUtil.Row(art, "npc_ceo", 0); ceo.up = HardwareEditorUtil.Row(art, "npc_ceo", 1); ceo.side = HardwareEditorUtil.Row(art, "npc_ceo", 2);
        ceo.actCoin = HardwareEditorUtil.Row(art, "ceo_acts", 0); ceo.actLaugh = HardwareEditorUtil.Row(art, "ceo_acts", 1);
        ceo.actWatch = HardwareEditorUtil.Row(art, "ceo_acts", 2); ceo.actTie = HardwareEditorUtil.Row(art, "ceo_acts", 3);
        ceo.portraitIdle = HardwareEditorUtil.Frame(art, "portraits_ceo", 0, 0); ceo.portraitTalk = HardwareEditorUtil.Frame(art, "portraits_ceo", 1, 0);
        ceo.portraitHappy = HardwareEditorUtil.Frame(art, "portraits_ceo", 2, 0); ceo.portraitBlink = HardwareEditorUtil.Frame(art, "portraits_ceo", 3, 0);
        ceo.portraitSmug = HardwareEditorUtil.Frame(art, "portraits_ceo", 0, 1); ceo.portraitAngry = HardwareEditorUtil.Frame(art, "portraits_ceo", 1, 1);
        ceo.portraitLaugh = HardwareEditorUtil.Frame(art, "portraits_ceo", 2, 1); ceo.portraitScheming = HardwareEditorUtil.Frame(art, "portraits_ceo", 3, 1);
        ceo.dialoguePanel = HardwareEditorUtil.FindSprite("ui_dialog_panel"); ceo.portraitFrame = HardwareEditorUtil.FindSprite("ui_dialog_frame");
        var pace = new List<Transform>();
        foreach (Vector2 p in new[] { new Vector2(13f, 4.6f), new Vector2(9f, 4.2f), new Vector2(9f, 8.8f), new Vector2(17f, 8.8f), new Vector2(17f, 4.2f) })
            pace.Add(Child(room, "CeoPacePoint", p).transform);
        ceo.pacePoints = pace.ToArray();
        EditorUtility.SetDirty(ceo);

        // ---- the back room: barrels on a belt, down a chute, into the river
        GameObject belt1 = Prop(t, "tower_conveyor_0", 5f, 12.4f, 3.9f, 0.5f), belt2 = Prop(t, "tower_conveyor_0", 9f, 12.4f, 3.9f, 0.5f);
        Flip(belt1, 5f, "tower_conveyor_0", "tower_conveyor_1"); Flip(belt2, 5f, "tower_conveyor_0", "tower_conveyor_1");
        GameObject chute = Prop(t, "tower_chute_0", 13.5f, 12.2f, 2.9f, 1f);
        Flip(chute, 3f, "tower_chute_0", "tower_chute_1", "tower_chute_2");
        Look(chute, "Look", "A chute, straight down. You can hear running water at the bottom. That is the river.", "Barrel after barrel after barrel. All of it into the river.");
        foreach (Vector2 p in new[] { new Vector2(1.2f, 12f), new Vector2(2.2f, 12.3f), new Vector2(1.7f, 13.4f), new Vector2(17f, 14f), new Vector2(18f, 14.2f), new Vector2(24.5f, 14f) })
            Prop(t, "tower_barrel", p.x, p.y, 0.7f, 0.4f);
        GameObject leak = Prop(t, "tower_barrel_leak", 19.2f, 13.6f, 0.7f, 0.4f);
        Look(leak, "Look", "It is leaking something purple. The floor under it has gone soft.");
        Person(t, "Worker", "dumper_a", 7f, 13.6f, grey, null, new[] { "Don't look at me. I just push the barrels." }, true);
        Person(t, "Worker", "dumper_b", 15.8f, 13.4f, grey, null, new[] { "Boss says the river 'takes it away'. Away where? Not my department." }, true);

        // walking in here is remembered: it unlocks a new question for Sterling and adds a quest to the journal
        var seen = Child(room, "BackRoomSeen", new Vector3(13f, 13.5f, 0f));
        var area = seen.AddComponent<BoxCollider2D>();
        area.size = new Vector2(25f, 4.5f); area.isTrigger = true;
        var trigger = seen.AddComponent<StoryTrigger>();
        trigger.flag = CeoNpc.SawDumpingFlag;
        trigger.message = "So THIS is where the tower's waste goes: down a chute and straight into the river.";
        trigger.questId = "sterling_river"; trigger.questTitle = "Something in the Water"; trigger.questGiver = "You";
        trigger.questText = "The Sterling tower pours its waste into the river that runs past the suburbs, your farm and the forest. Ask Victor Sterling about it. Ask City Hall what can be done.";

        // Marcus from the suburbs has a desk by the wall
        Prop(t, "uni_student_desk", 23.5f, 2f, 1.4f, 0.5f);
        Vector2 r = room.transform.position;
        Spot("TowerOffice", r.x + 23.5f, r.y + 3f, Vector2.down, "Tower_Mid");
    }

    /// <summary>Every item of one type that can be bought (it has a Buy Price), in name order.</summary>
    private static List<Object> ItemsOfType(ItemData.ItemType type)
    {
        var list = new List<ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it != null && it.type == type && it.buyPrice > 0 && !list.Contains(it)) list.Add(it);
        }
        list.Sort((a, b) => string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase));
        if (list.Count == 0) Debug.LogWarning("City: no " + type + " items with a Buy Price were found, so that shop has nothing to sell.");
        return list.ConvertAll(i => (Object)i);
    }

    // ------------------------------------------------------------------ city hall
    private static void BuildCityHall()
    {
        GameObject ext = Building("CityHall", "city_hall", HallX, SouthBaseY, 14, 11);
        HouseInterior interior;
        GameObject room = Room("CityHallInterior", 2, 22, 12, "floor_hall", "wall_hall_upper", "wall_hall_base", null, null, out interior);
        Transform t = room.transform;
        Doors(ext, room, interior, 11f, "Enter City Hall", "Hall", "C_Hall");

        Prop(t, "hall_notice_board", 6f, 13.4f, 0f, 0f, 3);
        Prop(t, "hall_flag", 2f, 10.9f, 0.5f, 0.3f); Prop(t, "hall_flag", 20f, 10.9f, 0.5f, 0.3f);
        Prop(t, "hall_cabinet", 15f, 11.1f, 0.9f, 0.7f); Prop(t, "hall_cabinet", 16f, 11.1f, 0.9f, 0.7f); Prop(t, "hall_cabinet", 17f, 11.1f, 0.9f, 0.7f);
        Prop(t, "hall_bench", 4f, 2.6f, 1.9f, 0.5f); Prop(t, "hall_bench", 18f, 2.6f, 1.9f, 0.5f);
        Prop(t, "city_potted_palm", 1.2f, 1f, 0.6f, 0.4f); Prop(t, "city_potted_palm", 20.8f, 1f, 0.6f, 0.4f);
        GameObject model = Prop(t, "hall_model", 17.5f, 5.6f, 2.9f, 0.8f);
        Look(model, "Look", "A model of the city. The little tower is suspiciously bigger than the real one.", "Somebody has drawn the river on the model in blue. Then gone over part of it in purple.");

        GameObject counter = Prop(t, "hall_counter", 11f, 6.8f, 3.9f, 0.9f);
        var desk = counter.AddComponent<CityHallDesk>();
        desk.improvements = new List<CityHallDesk.Improvement>
        {
            Improve("trees", "Street trees", 400, "", false, "Young trees in planters along Main Avenue."),
            Improve("flowers", "Flower beds", 300, "", false, "Flower beds on the pavements. The bees will find them."),
            Improve("lamps", "Solar street lamps", 600, "", false, "Lamps that charge in the day and light the river walk at night."),
            Improve("bins", "Recycling bins", 350, "", false, "Bins on every corner, so less ends up on the street."),
            Improve("fountain", "Park fountain", 1200, "", false, "A fountain for the little park south of Civic Street."),
            Improve("bus", "Bus stop", 900, "", false, "A proper shelter on Main Avenue for the bus from the suburbs."),
            Improve("river_boom", "River boom", 1500, "", true, "A floating barrier across the river that catches what comes out of the pipe. Cleans the river a great deal."),
            Improve("river_reeds", "Reed beds", 2500, "river_boom", true, "Reeds planted along the bank filter the water as it flows west. Cleans the river even more."),
        };
        desk.jobs = new List<CityHallDesk.Job>
        {
            Work("litter", "Litter pick", "Pick up 10 pieces of litter from the city streets.", null, 10, "litter_collected", 150),
            Work("treat", "Do it properly", "Take 15 pieces of waste to the treatment plant instead of the river.", null, 15, RiverPollution.TreatedKey, 300),
            Work("market", "Feed the city", "The market is short of fresh food. Sell 30 crops (through your mailbox at home).", null, 30, "crops_sold", 250),
            Work("science", "Science fair", "The university wants results to show off. Fund 3 research projects.", null, 3, "research_done", 400),
            Work("bridge", "Steel for the footbridge", "The river footbridge needs iron. Bring 5 Iron Bars (the tower's smelter makes them).", CityAssetsMenu.Item("IronBar"), 5, "", 600),
        };
        EditorUtility.SetDirty(desk);

        CityPerson clerk = Person(t, "Mr. Okafor", "clerk", 11f, 8.1f, new Color(0.35f, 0.5f, 0.35f),
            new[] { "Next! Oh. You're not in the queue. There is no queue. Forgive me, it has been that sort of decade.",
                    "Desmond Okafor, city clerk. Permits, plans and complaints. Mostly complaints.",
                    "If you want something BUILT in this city, you pay for it here. If you want something DONE, check the notice board." },
            new[] { "Form 12-B is for trees. Form 12-C is for complaints about trees. They are the same form.",
                    "The tower pays its fines on time, every month. That is the problem: to them a fine is just a price.",
                    "A boom across the river would catch most of it. The council voted for one. Then the money... went somewhere.",
                    "Miss Reyes from the suburbs works mornings here. The filing has never been so fast, or so loud." });
        clerk.service = desk; clerk.serviceLabel = "City services";
        EditorUtility.SetDirty(clerk);

        Vector2 r = room.transform.position;
        Spot("HallDesk", r.x + 8f, r.y + 8.1f, Vector2.down, "Hall_Mid");       // Lucia from the suburbs works here in the mornings
    }

    private static CityHallDesk.Improvement Improve(string id, string title, int gold, string requires, bool river, string text)
    {
        return new CityHallDesk.Improvement { id = id, title = title, goldCost = gold, requires = requires, cleansRiver = river, description = text };
    }

    private static CityHallDesk.Job Work(string id, string title, string text, ItemData item, int count, string counter, int reward)
    {
        return new CityHallDesk.Job { id = id, title = title, description = text, item = item, count = count, counterKey = counter, countFromAccept = true, rewardGold = reward };
    }

    // ------------------------------------------------------------------ the waste treatment plant
    private static List<Object> WasteItems()
    {
        var list = new List<Object>();
        foreach (string w in new[] { "TrashBag", "Scrap", "SludgeBarrel", "OldBoot" }) { ItemData it = CityAssetsMenu.Item(w); if (it != null) list.Add(it); }
        return list;
    }

    private static void BuildPlant()
    {
        GameObject ext = Building("TreatmentPlant", "city_plant", PlantX, NorthBaseY, 14, 10);
        HouseInterior interior;
        GameObject room = Room("PlantInterior", 3, 22, 12, "floor_plant", "wall_plant_upper", "wall_plant_base", null, null, out interior);
        Transform t = room.transform;
        Doors(ext, room, interior, 11f, "Enter the treatment plant", "Plant", "N_Plant");

        Prop(t, "plant_pipes", 11f, 13.4f, 0f, 0f, 3);
        Prop(t, "plant_tank", 2.5f, 10.6f, 1.8f, 0.8f); Prop(t, "plant_tank", 5f, 10.6f, 1.8f, 0.8f); Prop(t, "plant_tank", 19.5f, 10.6f, 1.8f, 0.8f);
        GameObject panel = Prop(t, "plant_panel", 16.5f, 10.9f, 1.9f, 0.6f);
        Look(panel, "Look", "Every needle is in the green. Somebody here cares.");
        Look(Prop(t, "plant_bin_trash", 3f, 2.4f, 0.8f, 0.5f), "Look", "General waste.");
        Look(Prop(t, "plant_bin_recycle", 4.2f, 2.4f, 0.8f, 0.5f), "Look", "Recycling. Rinsed, please.");
        Look(Prop(t, "plant_bin_compost", 5.4f, 2.4f, 0.8f, 0.5f), "Look", "Compost. This one smells the best, honestly.");

        // the hopper takes the waste; the foreman does the talking
        GameObject hopper = Prop(t, "plant_hopper", 11f, 7.2f, 2.9f, 1f);
        var waste = hopper.AddComponent<WasteDesk>();
        HardwareEditorUtil.SetString(waste, "title", "Waste Treatment");
        HardwareEditorUtil.SetString(waste, "prompt", "Hand over waste");
        HardwareEditorUtil.SetObjects(waste, "trashItems", WasteItems());
        CityPerson boss = Person(t, "Foreman Ruiz", "plant_boss", 14f, 6.6f, new Color(0.75f, 0.5f, 0.1f),
            new[] { "Mind the grating. Carmen Ruiz, I run the plant.",
                    "Trash, scrap, sludge, crops that came up wrong from dirty soil: bring it here and it is dealt with properly.",
                    "It costs a fifth of what the stuff is worth. Bring a mixed load and I'll charge you the cheap rate for all of it.",
                    "Or you can do what the tower does and tip it in the river for nothing. I can't stop you. The forest will send you the bill." },
            new[] { "We could clean the whole river in a season if the tower stopped filling it.",
                    "Every barrel that comes through that door is one that isn't going past your farm.",
                    "Sam from the suburbs does three mornings a week here. Quiet. Writes on the back of the delivery notes.",
                    "People say this job stinks. They're right. Somebody still has to do it." });
        boss.service = waste; boss.serviceLabel = "Hand over waste";
        EditorUtility.SetDirty(boss);

        Vector2 r = room.transform.position;
        Spot("PlantFloor", r.x + 7f, r.y + 6f, Vector2.right, "Plant_Mid");     // Sam from the suburbs works here three mornings a week
    }

    // ------------------------------------------------------------------ the buildings you can't go into
    private static void BuildBackground()
    {
        // north row
        Front("Bookshop", "bg_books", BooksX, NorthBaseY, 8, 10, "A bookshop. The sign says: BACK IN FIVE MINUTES. It has said that for a year.");
        Front("OfficesA", "bg_office_a", OfficeAX, NorthBaseY, 10, 12, "Offices. Everybody inside is on the phone.");
        Front("Bank", "bg_office_b", OfficeBX, NorthBaseY, 8, 11, "The bank. Most of it belongs to Sterling.");
        // south row
        GameObject cafe = Front("Cafe", "bg_cafe", CafeX, SouthBaseY, 8, 10, "The Blue Note Cafe. Live music on Friday and Saturday nights.");
        GameObject museum = Front("Museum", "bg_museum", MuseumX, SouthBaseY, 10, 11, "The city museum. Closed on Mondays.");
        GameObject clinic = Front("Clinic", "bg_clinic", ClinicX, SouthBaseY, 8, 10, "The clinic. The waiting room is full.");
        Front("FlatsA", "bg_apartments_a", FlatsAX, SouthBaseY, 10, 11, "Flats. Somebody on the third floor is learning the trumpet.");
        GameObject market = Front("Market", "bg_market", MarketX, SouthBaseY, 8, 10, "The wholesale market. Shopkeepers only before noon.");
        Front("FlatsB", "bg_apartments_b", FlatsBX, SouthBaseY, 10, 11, "Lofts. Very expensive. Very small.");

        // Neighbours who work in these go in through the door and are out of sight (a hidden spot far off the map).
        Hidden("Cafe", cafe, "C_Cafe", 0);
        Hidden("Museum", museum, "C_Museum", 1);
        Hidden("Clinic", clinic, "C_Clinic", 2);
        Hidden("Market", market, "C_Market", 3);
    }

    private static GameObject Front(string name, string picture, float x, int baseY, int w, int h, string line)
    {
        GameObject b = Building(name, picture, x, baseY, w, h);
        var door = Child(b, "Door", new Vector3(0f, 0.3f, 0f));
        Box(door, Vector2.zero, new Vector2(2f, 1.6f), true);
        var f = door.AddComponent<StoreFlavor>();
        f.prompt = "Look"; f.lines = new[] { line };
        return b;
    }

    /// <summary>A door node for a background building, and the out-of-sight spot "<key>Inside" behind it.</summary>
    private static void Hidden(string key, GameObject building, string streetNode, int index)
    {
        Vector2 b = building.transform.position;
        Node(key + "_Door", b.x, b.y - 0.6f);
        Node(key + "_Hidden", -300f - index * 20f, 0f);
        Link(streetNode, key + "_Door");
        Portal(key + "_Door", key + "_Hidden");
        Spot(key + "Inside", -300f - index * 20f, 2f, Vector2.down, key + "_Hidden");
    }

    // ------------------------------------------------------------------ the river
    private static void PlaceRiverThings()
    {
        // the pipe behind the tower (flipped so the sludge pours UP the screen, into the river)
        GameObject pipe = Prop(objectsParent, "river_outflow_0", TowerX + 2f, RiverY + 1.6f, 0f, 0f, 3, true);
        pipe.GetComponent<SpriteRenderer>().flipY = true;
        Flip(pipe, 4f, "river_outflow_0", "river_outflow_1", "river_outflow_2");
        var pipeLook = Child(objectsParent.gameObject, "PipeLook", new Vector3(TowerX + 2f, RiverY - 0.6f, 0f));
        Box(pipeLook, Vector2.zero, new Vector2(2f, 1.2f), true);
        var pl = pipeLook.AddComponent<StoreFlavor>();
        pl.prompt = "Look"; pl.lines = new[] { "A pipe from the back of the Sterling tower. What comes out of it is not water.", "The river is clear upstream of this pipe. Downstream it is not." };

        // ripples on the clean water
        for (int i = 0; i < 16; i++)
        {
            GameObject ripple = Prop(objectsParent, "river_ripple_0", TowerX + 6f + (float)rng.NextDouble() * (W - TowerX - 8f), RiverY + 1.5f + (float)rng.NextDouble() * 4f, 0f, 0f, 2, true);
            Flip(ripple, 1.5f, "river_ripple_0", "river_ripple_1");
        }

        // the place on the bank where waste can be thrown in: free, and you will be sorry
        GameObject sign = Prop(objectsParent, "river_dump_sign", PlantX - 9f, RiverY - 1.1f, 0.4f, 0.3f, 10, true);
        var spot = Child(objectsParent.gameObject, "RiverDumpSpot", new Vector3(PlantX - 7.5f, RiverY - 0.7f, 0f));
        Box(spot, Vector2.zero, new Vector2(2.2f, 1.4f), true);
        var dump = spot.AddComponent<WasteDesk>();
        HardwareEditorUtil.SetString(dump, "title", "The River");
        HardwareEditorUtil.SetString(dump, "prompt", "Throw waste in the river");
        HardwareEditorUtil.SetObjects(dump, "trashItems", WasteItems());
        SetBool(dump, "intoRiver", true);
        Look(sign, "Read", "NO DUMPING. By order of the city. (Somebody has scratched out the NO.)");
    }

    // ------------------------------------------------------------------ things on the street
    private static void PlaceStreetThings()
    {
        // the hot dog cart on the pavement outside the tower: the cart is the shop, the vendor does the talking
        GameObject cart = Prop(objectsParent, "hotdog_cart", TowerX - 8f, MainY + 3.4f, 2.6f, 0.7f, 10, true);
        var shop = cart.AddComponent<ShopCounter>();
        HardwareEditorUtil.SetString(shop, "shopName", "Hot Dogs");
        var food = new List<Object>();
        foreach (string f in new[] { "HotDog", "ChiliDog", "Pretzel", "Lemonade" }) { ItemData it = CityAssetsMenu.Item(f); if (it != null) food.Add(it); }
        HardwareEditorUtil.SetObjects(shop, "stock", food);
        CityPerson vendor = Person(objectsParent, "Big Sal", "vendor", TowerX - 5.8f, MainY + 3.5f, new Color(0.8f, 0.25f, 0.2f),
            new[] { "Hot dogs! Get your hot dogs! ...Hey, a new face. Big Sal. Best dogs in the city, thirty years on this corner.", "You look like you've walked a long way. Eat something. You'll work twice as hard after." },
            new[] { "One with everything? It fills you right back up.", "The tower tried to buy my corner. I said: you can't afford it. They could. I still said no.",
                    "Farmer, eh? If you ever grow a decent onion, we should talk.", "Lunch rush is at twelve thirty. Come before it or bring a book." });
        vendor.GetComponent<SpriteRenderer>().sharedMaterial = lit;
        vendor.service = shop; vendor.serviceLabel = "I'll take a hot dog";
        EditorUtility.SetDirty(vendor);
        Take(Mathf.FloorToInt(TowerX - 10f), MainY + 3, Mathf.FloorToInt(TowerX - 5f), MainY + 4);

        // street lights and benches (pieces the project already has)
        var lamp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Art/Lights/Prefabs/LightPost.prefab");
        var bench = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Farm/Kiln/Prefabs/Props/BenchPark.prefab");
        for (float x = 4f; x < W; x += 12f)
        {
            if (NearCross(Mathf.RoundToInt(x))) continue;
            PutPrefab(lamp, x, MainY + 3.2f); PutPrefab(lamp, x + 6f, MainY - 1.6f);
            PutPrefab(lamp, x, CivicY + 3.2f); PutPrefab(lamp, x + 6f, WalkY + 0.3f);
        }
        foreach (float x in new[] { 16f, 44f, 58f, 82f, 94f }) PutPrefab(bench, x, WalkY + 1.9f);       // benches facing the river
        Prop(objectsParent, "city_bike_rack", UniX + 5.5f, NorthBaseY - 0.5f, 2.1f, 0.4f, 10, true);

        // litter: pick it up, then decide where it goes
        var waste = new List<ItemData>();
        foreach (Object o in WasteItems()) waste.Add((ItemData)o);
        Vector2[] piles =
        {
            new Vector2(24f, MainY + 5.3f), new Vector2(57f, MainY + 5.4f), new Vector2(76.5f, MainY + 5.2f), new Vector2(35f, MainY - 1.4f), new Vector2(66f, MainY - 1.5f),
            new Vector2(12f, CivicY + 5.2f), new Vector2(44f, CivicY - 1.4f), new Vector2(86f, CivicY + 5.3f), new Vector2(40f, WalkY + 1.2f), new Vector2(62f, WalkY + 0.6f),
        };
        for (int i = 0; i < piles.Length; i++)
        {
            GameObject pile = Prop(objectsParent, "city_trash_pile", piles[i].x, piles[i].y, 0f, 0f, 9, true);
            Box(pile, new Vector2(0f, 0.4f), new Vector2(1.2f, 0.8f), true);
            var lp = pile.AddComponent<LitterPile>();
            lp.pileId = "city_" + (i + 1);
            lp.items = waste.ToArray();
        }
    }

    private static void PutPrefab(GameObject prefab, float x, float y)
    {
        if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(objectsParent, false);
        go.transform.position = new Vector3(x, y, 0f);
        Take(Mathf.FloorToInt(x), Mathf.FloorToInt(y), Mathf.FloorToInt(x), Mathf.FloorToInt(y));
    }

    // ------------------------------------------------------------------ what City Hall can build
    /// <summary>
    /// Each improvement is an empty parent with a CityImprovement on it; the pieces are its children. They are in the scene from the
    /// start (so you can move them), and the game hides them until the improvement is bought.
    /// </summary>
    private static void PlaceImprovements()
    {
        Transform trees = Improvement("trees");
        for (float x = 6f; x < W; x += 8f) if (!NearCross(Mathf.RoundToInt(x)) && Mathf.Abs(x - (TowerX - 7f)) > 3f) Prop(trees, "city_tree_planter", x, MainY + 3.1f, 0.9f, 0.5f, 10, true);

        Transform flowers = Improvement("flowers");
        for (float x = 10f; x < W; x += 10f) if (!NearCross(Mathf.RoundToInt(x))) { Prop(flowers, "city_flower_bed", x, CivicY + 3.1f, 1.9f, 0.4f, 10, true); Prop(flowers, "city_flower_bed", x + 3f, MainY - 1.9f, 1.9f, 0.4f, 10, true); }

        Transform lamps = Improvement("lamps");
        for (float x = 10f; x < W; x += 12f) Prop(lamps, "city_solar_lamp", x, WalkY + 2.2f, 0.4f, 0.3f, 10, true);

        Transform bins = Improvement("bins");
        foreach (float x in new[] { 25f, 33.5f, 67f, 75.5f })
        {
            Prop(bins, "plant_bin_recycle", x, MainY + 5.4f, 0.8f, 0.5f, 10, true);
            Prop(bins, "plant_bin_compost", x, CivicY + 5.4f, 0.8f, 0.5f, 10, true);
        }

        Transform fountain = Improvement("fountain");
        Prop(fountain, "city_fountain", 50f, 4f, 2.8f, 1f, 10, true);
        Take(47, 3, 53, 6);

        Transform bus = Improvement("bus");
        Prop(bus, "city_bus_stop", 16f, MainY + 3.1f, 2.1f, 0.5f, 10, true);

        Transform boom = Improvement("river_boom");
        for (int i = 0; i < 2; i++) Prop(boom, "river_boom", TowerX - 6f, RiverY + 2f + i * 2.4f, 0f, 0f, 3, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        Transform reeds = Improvement("river_reeds");
        for (float x = 3f; x < TowerX - 8f; x += 3.5f) Prop(reeds, "river_reeds", x, RiverY + 0.5f, 0f, 0f, 3, true);
    }

    private static Transform Improvement(string id)
    {
        var go = new GameObject("Improvement_" + id);
        go.AddComponent<CityImprovement>().improvementId = id;
        return go.transform;
    }

    // ------------------------------------------------------------------ trees and bushes on the free grass
    private static void Scatter()
    {
        var trees = new List<GameObject>();
        foreach (string n in new[] { "Tree_Green", "Tree_Green", "Tree_Dark", "Tree_Blossom" }) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(SuburbPrefabs + "/" + n + ".prefab"); if (p != null) trees.Add(p); }
        var bushes = new List<GameObject>();
        foreach (string n in new[] { "Bush_Green", "Bush_Flowering", "Bush_Rose" }) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(SuburbPrefabs + "/" + n + ".prefab"); if (p != null) bushes.Add(p); }
        if (trees.Count == 0) { Debug.LogWarning("City: the suburb tree prefabs were not found (build the Suburbs scene first), so the park has no trees."); return; }

        for (int placed = 0, tries = 0; placed < 46 && tries < 6000; tries++)
        {
            int x = rng.Next(1, W - 1), y = rng.Next(1, RiverY - 2);
            if (!Free(x - 1, y, x + 1, y + 1)) continue;
            PutPrefab(trees[rng.Next(trees.Count)], x + 0.5f, y + 0.25f);
            Take(x - 2, y - 1, x + 2, y + 2);
            placed++;
        }
        for (int placed = 0, tries = 0; bushes.Count > 0 && placed < 30 && tries < 6000; tries++)
        {
            int x = rng.Next(1, W - 2), y = rng.Next(1, RiverY - 2);
            if (!Free(x, y, x + 1, y)) continue;
            PutPrefab(bushes[rng.Next(bushes.Count)], x + 1f, y + 0.2f);
            Take(x, y, x + 1, y);
            placed++;
        }
    }

    // ------------------------------------------------------------------ edges, doorways, traffic
    private static void MakeEdges()
    {
        var go = new GameObject("MapEdges");
        AddBox(go, new Vector2(-0.5f, H * 0.5f), new Vector2(1f, H + 2f));
        AddBox(go, new Vector2(W + 0.5f, H * 0.5f), new Vector2(1f, H + 2f));
        AddBox(go, new Vector2(W * 0.5f, -0.5f), new Vector2(W + 2f, 1f));
        AddBox(go, new Vector2(W * 0.5f, (RiverY + H) * 0.5f + 0.2f), new Vector2(W + 2f, H - RiverY));      // nobody walks on the river
    }

    private static void AddBox(GameObject go, Vector2 offset, Vector2 size)
    {
        var c = go.AddComponent<BoxCollider2D>();
        c.offset = offset; c.size = size;
    }

    private static void MakeEntrances()
    {
        float midY = MainY + 1.5f;

        var exit = new GameObject("ExitToSuburbs");
        exit.transform.position = new Vector3(0.75f, midY + 0.5f, 0f);
        exit.transform.localScale = new Vector3(1.5f, 9f, 1f);            // covers the road and both pavements
        var se = exit.AddComponent<SceneExit>();
        exit.GetComponent<BoxCollider2D>().isTrigger = true;
        Set(se, "sceneName", "Suburbs"); Set(se, "arriveAt", "FromCity"); Set(se, "promptText", "To the suburbs");
        SetBool(se, "requireInteract", true);

        // you arrive on the pavement, not in the road (there is traffic here)
        var spawn = new GameObject("FromSuburbs");
        spawn.transform.position = new Vector3(3.5f, PlazaY, 0f);
        var sp = spawn.AddComponent<SceneSpawnPoint>();
        Set(sp, "spawnId", "FromSuburbs"); SetBool(sp, "isDefault", true);
        var so = new SerializedObject(sp);
        so.FindProperty("faceDirection").vector2Value = Vector2.right;
        so.ApplyModifiedPropertiesWithoutUndo();

        var east = new GameObject("HighwayEast");
        east.transform.position = new Vector3(W - 0.75f, midY + 0.5f, 0f);
        east.transform.localScale = new Vector3(1.5f, 9f, 1f);
        east.AddComponent<BoxCollider2D>().isTrigger = true;
        var sign = east.AddComponent<StoreFlavor>();
        sign.prompt = "The highway";
        sign.lines = new[] { "The highway out of the city. It goes a very long way, and you have a farm to run." };
    }

    private static CityTraffic.Route Route(string label, CityTraffic.Kind kind, bool bothWays, float weight, params float[] xy)
    {
        var pts = new Vector2[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        return new CityTraffic.Route { label = label, kind = kind, bothWays = bothWays, weight = weight, points = pts };
    }

    /// <summary>The strangers: where they travel and what they can look like. Everything else is tuned on the CityTraffic object.</summary>
    private static void MakeTraffic()
    {
        var go = new GameObject("CityTraffic");
        var traffic = go.AddComponent<CityTraffic>();
        const float L = -3f, R = W + 3f;                                     // just off the left and right edges of the map
        float aw = StreetAX - 1f, ae = StreetAX + 4f, bw = StreetBX - 1f, be = StreetBX + 4f;
        float river = WalkY + 1.5f, southMain = MainY - 1f, southCivic = CivicY - 1f;
        var P = CityTraffic.Kind.Pedestrian; var B = CityTraffic.Kind.Cyclist; var C = CityTraffic.Kind.Car; var S = CityTraffic.Kind.Scooter;

        traffic.routes = new[]
        {
            // on foot: along every pavement, and a few longer walks round the corners
            Route("Main Avenue, north pavement", P, true, 3f, L, PlazaY - 0.6f, R, PlazaY - 0.6f),
            Route("Main Avenue, by the buildings", P, true, 2f, L, PlazaY + 0.6f, R, PlazaY + 0.6f),
            Route("Main Avenue, south pavement", P, true, 2f, L, southMain, R, southMain),
            Route("Civic Street, north pavement", P, true, 2f, L, CivicWalkY, R, CivicWalkY),
            Route("Civic Street, south pavement", P, true, 1f, L, southCivic, R, southCivic),
            Route("River walk", P, true, 1.5f, L, river, R, river),
            Route("Round the west corner", P, true, 1f, L, PlazaY, aw, PlazaY, aw, CivicWalkY, L, CivicWalkY),
            Route("Tower to city hall", P, true, 1.5f, R, PlazaY, ae, PlazaY, ae, CivicWalkY, R, CivicWalkY),
            Route("Down to the river", P, true, 1f, L, southMain, bw, southMain, bw, river, L, river),
            Route("East side stroll", P, true, 1f, R, river, be, river, be, southCivic, R, southCivic),
            // bicycles: at the kerb of Main Avenue (each way), and along the river
            Route("Bikes east", B, false, 1f, L, MainY + 0.15f, R, MainY + 0.15f),
            Route("Bikes west", B, false, 1f, R, MainY + 2.45f, L, MainY + 2.45f),
            Route("Bikes by the river", B, true, 1f, L, WalkY + 0.7f, R, WalkY + 0.7f),
            // cars keep to their side: east on the lower lane, west on the upper lane
            Route("Main Avenue east", C, false, 2f, L, MainY + 0.35f, R, MainY + 0.35f),
            Route("Main Avenue west", C, false, 2f, R, MainY + 1.85f, L, MainY + 1.85f),
            Route("Civic Street east", C, false, 1f, L, CivicY + 0.35f, R, CivicY + 0.35f),
            Route("Civic Street west", C, false, 1f, R, CivicY + 1.85f, L, CivicY + 1.85f),
            // scooters use the same lanes
            Route("Scooters east", S, false, 1f, L, MainY + 0.6f, R, MainY + 0.6f),
            Route("Scooters west", S, false, 1f, R, MainY + 2.1f, L, MainY + 2.1f),
            Route("Scooters on Civic", S, false, 0.6f, L, CivicY + 0.6f, R, CivicY + 0.6f),
        };

        var walkers = new List<CityTraffic.WalkerLook>();
        var sheets = new List<string>();
        for (int i = 1; i <= 12; i++) sheets.Add("ped_" + i);
        sheets.Add("student_a"); sheets.Add("student_b");
        foreach (string s in sheets)
        {
            var look = new CityTraffic.WalkerLook
            {
                label = s,
                down = HardwareEditorUtil.Row(art, "npc_" + s, 0), up = HardwareEditorUtil.Row(art, "npc_" + s, 1), side = HardwareEditorUtil.Row(art, "npc_" + s, 2),
                portraitIdle = HardwareEditorUtil.Frame(art, "portraits_" + s, 0, 0), portraitTalk = HardwareEditorUtil.Frame(art, "portraits_" + s, 1, 0),
                portraitHappy = HardwareEditorUtil.Frame(art, "portraits_" + s, 2, 0), portraitBlink = HardwareEditorUtil.Frame(art, "portraits_" + s, 3, 0),
            };
            if (look.down.Length > 0) walkers.Add(look);
        }
        traffic.walkerLooks = walkers.ToArray();
        traffic.bikeLooks = Wheels(1f, new[] { "bike_a_0", "bike_a_1" }, new[] { "bike_b_0", "bike_b_1" }, new[] { "bike_c_0", "bike_c_1" }, new[] { "bike_d_0", "bike_d_1" });
        traffic.scooterLooks = Wheels(1f, new[] { "vespa_mint" }, new[] { "vespa_red" }, new[] { "vespa_cream" }, new[] { "vespa_blue" });
        var cars = new List<CityTraffic.WheelLook>(Wheels(1f, new[] { "car_red" }, new[] { "car_blue" }, new[] { "car_white" }, new[] { "car_green" }, new[] { "car_taxi" }, new[] { "car_black" }));
        cars.AddRange(Wheels(0.85f, new[] { "car_van" }));
        cars.AddRange(Wheels(0.7f, new[] { "car_bus" }));
        traffic.carLooks = cars.ToArray();
        traffic.dialoguePanel = HardwareEditorUtil.FindSprite("ui_dialog_panel"); traffic.portraitFrame = HardwareEditorUtil.FindSprite("ui_dialog_frame");
        traffic.pedestrianLines = new[]
        {
            "Sorry, can't stop, I'm late!", "Lovely day for it.", "Have you tried Big Sal's hot dogs? Life-changing.", "Is it me, or does the river smell odd lately?",
            "The Sterling tower gets taller every year. Or I'm getting shorter.", "Excuse me. Coming through.", "You're not from the city, are you? It's the boots.",
            "My cousin lives out in the suburbs. Says the bread there is amazing.", "I work for Sterling. Everybody works for Sterling. Even the people who don't.",
            "They say a scientist at the university can make it rain on one field at a time.", "City Hall has had my form for six weeks. SIX.",
            "I used to fish in that river. Nobody fishes in that river now.", "If you see a red scooter, that's my brother. Tell him he owes me money.",
            "Don't cross on Main Avenue without looking. The taxis certainly won't.", "The treatment plant woman is the only honest person in this city. And she smells of bins.",
            "Mind the litter. Or pick it up, the city pays for that now.", "First time in the city? The museum is free on Sundays.", "I'm not lost. I'm exploring. Slowly. In circles.",
        };
        EditorUtility.SetDirty(traffic);
    }

    private static CityTraffic.WheelLook[] Wheels(float speed, params string[][] sets)
    {
        var list = new List<CityTraffic.WheelLook>();
        foreach (string[] set in sets)
        {
            var frames = new List<Sprite>();
            foreach (string s in set) { Sprite sp = Spr(s); if (sp != null) frames.Add(sp); }
            if (frames.Count > 0) list.Add(new CityTraffic.WheelLook { label = set[0], frames = frames.ToArray(), speedMultiplier = speed });
        }
        return list.ToArray();
    }

    /// <summary>The tower sells house upgrades, and buying one needs a HouseManager. Like the one in the suburbs, this one only holds the catalog.</summary>
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
    }

    // ------------------------------------------------------------------ visitors: the neighbours who work or study here
    /// <summary>One neighbour who comes to the city: whose schedule it is, the pictures, and where they go.</summary>
    private class Visitor
    {
        public string id, name, schedule, artFile, place, hello;
        public Color color;
        public string[] lines;
    }

    private const string Residents = "Assets/Farm/Suburb/Residents/Schedules/Schedule_";
    private const string Family = "Assets/Farm/Suburb/Family/Schedules/Schedule_";

    private static Visitor V(string id, string name, string folder, string place, Color color, string hello, params string[] lines)
    {
        return new Visitor { id = id, name = name, schedule = folder + id + ".asset", artFile = "npc_" + id, place = place, color = color, hello = hello, lines = lines };
    }

    /// <summary>
    /// WHO WORKS WHERE. The place is an NpcSpot in this scene; every "City" stop in that neighbour's schedule is pointed at it.
    /// To send somebody somewhere else, change the place here and build again.
    /// </summary>
    private static Visitor[] Visitors()
    {
        return new[]
        {
            // the university: Grace lectures; Bea, Dev, Nia and Soo-ah study
            V("grace", "Grace", Residents, "UniLectern", new Color(0.7f, 0.3f, 0.3f), "You, here? Good. Sit at the back and learn something.", "Forty students and not one of them has read the chapter.", "Dr. Hale down the hall is doing remarkable things with soil. Do fund her."),
            V("bea", "Bea", Residents, "UniLab", new Color(0.4f, 0.55f, 0.35f), "Oh! You came to the lab! Don't touch the green one.", "Dr. Hale lets me label the seedlings. It's the best job in the building.", "The river samples came back purple again. PURPLE."),
            V("dev", "Dev", Residents, "UniDesk1", new Color(0.8f, 0.3f, 0.25f), "Whoa. What are you doing at my school?", "The day school is in the university building. The big kids think we're mascots.", "Double maths. Send help."),
            V("nia", "Nia", Residents, "UniDesk2", new Color(0.85f, 0.5f, 0.25f), "Hi! This is my school! Do you want to see my beetle chart?", "There's a lady here with a microscope. She let me look at a leg. A BEETLE leg.", "I'm supposed to be doing spelling."),
            V("daughter", "Soo-ah", Family, "UniStudy", new Color(0.55f, 0.4f, 0.6f), "Oh, hi! Yes, this is where I disappear to three days a week.", "Business studies. I'm going to make the shop so efficient that Dad cries.", "The tower offered me a job. I haven't told my parents. I'm not taking it."),
            // the tower: Marcus
            V("marcus", "Marcus", Residents, "TowerOffice", new Color(0.35f, 0.4f, 0.5f), "Oh no. You've seen me at work. Please don't tell Frank.", "Forty floors and they put my desk next to the smelter.", "Don't go in the back room. Seriously. I did once and I haven't slept right since."),
            // city hall: Lucia
            V("lucia", "Lucia", Residents, "HallDesk", new Color(0.85f, 0.55f, 0.15f), "Hey! Welcome to city hall! Take a number. Kidding. There are no numbers.", "Mr. Okafor files. I stamp. We are a machine.", "If you want the river fixed, buy the boom. I'll stamp it myself."),
            // the treatment plant: Sam
            V("sam", "Sam", Residents, "PlantFloor", new Color(0.6f, 0.45f, 0.25f), "Oh. Um. Hello. Yes, I work here. Three mornings.", "You learn a lot about people from what they throw away. It's all going in the book.", "Foreman Ruiz says I sort faster than anyone. I've never been best at anything before."),
            // out of sight in the background buildings
            V("yuki", "Yuki", Residents, "MuseumInside", new Color(0.3f, 0.35f, 0.55f), "Good day.", "The museum is this way."),
            V("priya", "Priya", Residents, "ClinicInside", new Color(0.25f, 0.6f, 0.58f), "Can't stop, shift starts in two minutes.", "Early shift. Coffee first."),
            V("tariq", "Tariq", Residents, "CafeInside", new Color(0.45f, 0.35f, 0.6f), "Gig night! Come and listen.", "Sound check in ten."),
            V("dad", "Jae-hoon", Family, "MarketInside", new Color(0.25f, 0.35f, 0.55f), "Ah! You caught me buying stock.", "The wholesale market has the best prices before noon."),
            V("mom", "Mi-sook", Family, "CafeInside", new Color(0.65f, 0.3f, 0.4f), "Oh, hello dear! I'm meeting my friends at the cafe.", "Saturday is my day in the city."),
        };
    }

    private static string PlaceVisitors()
    {
        var parent = new GameObject("Visitors");
        int placed = 0;
        var missing = new List<string>();
        foreach (Visitor v in Visitors())
        {
            var schedule = AssetDatabase.LoadAssetAtPath<NpcScheduleData>(v.schedule);
            string folder = HardwareEditorUtil.FindFolder(v.artFile);
            if (schedule == null || folder == null) { missing.Add(v.name); continue; }

            if (v.id == "sam") GiveSamHisShift(schedule);
            PointCityStops(schedule, v.place);

            var go = new GameObject("Visitor_" + v.name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(0.5f, PlazaY, 0f);
            go.AddComponent<SpriteRenderer>().sortingOrder = 0;
            var npc = go.AddComponent<ScheduledNpc>();
            npc.displayName = v.name;
            npc.schedule = schedule;
            npc.homeScene = "Suburbs";                       // this copy only appears for the stops that are in the City scene
            npc.nameColor = v.color;
            npc.firstMeeting = new[] { v.hello };
            var chat = new List<NpcChatLine>();
            foreach (string line in v.lines) chat.Add(new NpcChatLine { label = "At work", priority = 0, conditions = new NpcCondition(), pages = new[] { line } });
            npc.chatLines = chat.ToArray();
            HardwareEditorUtil.GiveNpcArt(npc, folder, v.artFile, "portraits_" + v.id);
            if (npc.down == null || npc.down.Length == 0) Debug.LogWarning("City: no walk pictures found for " + v.name + " (build the Suburbs scene once so the sheets are cut up).");
            EditorUtility.SetDirty(npc);
            placed++;
        }
        AssetDatabase.SaveAssets();
        return "Placed " + placed + " visiting neighbours under 'Visitors'." +
               (missing.Count > 0 ? " Left out (no schedule asset or art yet; build the Suburbs scene first, then this one again): " + string.Join(", ", missing) + "." : "");
    }

    /// <summary>Every stop a schedule has in the City scene is sent to this neighbour's place of work.</summary>
    private static void PointCityStops(NpcScheduleData schedule, string place)
    {
        bool changed = false;
        if (schedule.routes == null) return;
        foreach (ScheduleRoute route in schedule.routes)
        {
            if (route == null || route.stops == null) continue;
            foreach (ScheduleStop stop in route.stops)
                if (stop != null && stop.scene == "City" && stop.place != place) { stop.place = place; changed = true; }
        }
        if (changed) EditorUtility.SetDirty(schedule);
    }

    /// <summary>Sam's schedule asset was made before the city existed: add his mornings at the treatment plant if they are not there yet.</summary>
    private static void GiveSamHisShift(NpcScheduleData schedule)
    {
        if (schedule.routes == null) schedule.routes = new ScheduleRoute[0];
        foreach (ScheduleRoute route in schedule.routes)
            if (route != null && route.stops != null)
                foreach (ScheduleStop stop in route.stops)
                    if (stop != null && stop.scene == "City") return;
        if (schedule.routes.Length >= NpcScheduleData.MaxRoutes) return;

        var list = new List<ScheduleRoute>(schedule.routes);
        list.Insert(0, new ScheduleRoute
        {
            routeName = "Shift at the treatment plant", startHour = 7.5f, priority = 40,
            conditions = new NpcCondition { days = DayMask.Tue | DayMask.Thu | DayMask.Sat },
            stops = new[] { new ScheduleStop { place = "PlantFloor", scene = "City", waitMinutes = 200f, transitMinutes = 40f, activity = "Sorting waste at the treatment plant" } },
            whenFinished = RouteEnd.ReturnToIdleSpot
        });
        schedule.routes = list.ToArray();
        EditorUtility.SetDirty(schedule);
    }

    // ------------------------------------------------------------------ player, camera, the suburbs end of the road
    private static void SetUpPlayerAndCamera(Tilemap ground)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = new Vector3(3.5f, PlazaY, 0f);
        else Debug.LogWarning("City: no Player came over from the Mine scene. Add one, then run Farm > Set Up > Game Systems In Scene.");

        var cam = Object.FindAnyObjectByType<CameraFollow2D>();
        if (cam != null)
        {
            var so = new SerializedObject(cam);
            so.FindProperty("limitToMap").boolValue = true;
            so.FindProperty("boundsTilemap").objectReferenceValue = ground;
            so.ApplyModifiedPropertiesWithoutUndo();
            cam.transform.position = new Vector3(8f, PlazaY, cam.transform.position.z);
        }
    }

    /// <summary>Turns the suburbs' "road to the city" into a real doorway, and adds the spot you arrive at coming back.</summary>
    private static string ConnectSuburbs()
    {
        if (!File.Exists(SuburbScene)) return "Suburbs scene not found, so no doorway was added there. Build it (Farm > Set Up > Suburbs Scene); it now makes the doorway itself.";
        Scene suburbs = EditorSceneManager.OpenScene(SuburbScene, OpenSceneMode.Single);
        GameObject road = GameObject.Find("CityRoad");
        if (road == null) return "The Suburbs scene has no 'CityRoad' object. Rebuild it with Farm > Set Up > Suburbs Scene to get the doorway to the city.";
        bool changed = false;

        var flavor = road.GetComponent<StoreFlavor>();
        if (flavor != null) { Object.DestroyImmediate(flavor); changed = true; }
        var se = road.GetComponent<SceneExit>();
        if (se == null)
        {
            se = road.AddComponent<SceneExit>();
            road.GetComponent<BoxCollider2D>().isTrigger = true;
            changed = true;
        }
        Set(se, "sceneName", "City"); Set(se, "arriveAt", "FromSuburbs"); Set(se, "promptText", "To the city");
        SetBool(se, "requireInteract", true);

        if (GameObject.Find("FromCity") == null)
        {
            var spawn = new GameObject("FromCity");
            spawn.transform.position = road.transform.position + new Vector3(-2.75f, 0f, 0f);
            var sp = spawn.AddComponent<SceneSpawnPoint>();
            Set(sp, "spawnId", "FromCity");
            var so = new SerializedObject(sp);
            so.FindProperty("faceDirection").vector2Value = Vector2.left;
            so.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }
        if (!changed) return "The suburbs already had their doorway to the city.";
        EditorSceneManager.MarkSceneDirty(suburbs);
        EditorSceneManager.SaveScene(suburbs);
        return "The Suburbs scene's 'CityRoad' (right edge of Main Street) now leads to the city, and 'FromCity' is where you arrive coming back.";
    }

    // ------------------------------------------------------------------ small helpers
    private static GameObject Child(GameObject parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        return go;
    }

    private static void Box(GameObject go, Vector2 offset, Vector2 size, bool trigger)
    {
        var c = go.AddComponent<BoxCollider2D>();
        c.offset = offset; c.size = size; c.isTrigger = trigger;
    }

    private static void Add(HashSet<Vector2Int> set, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (x >= 0 && y >= 0 && x < W && y < H) set.Add(new Vector2Int(x, y));
    }

    /// <summary>Marks tiles as taken, so no tree is planted there.</summary>
    private static void Take(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (x >= 0 && y >= 0 && x < W && y < H) used[x, y] = true;
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
        if (p == null) { Debug.LogWarning("City: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("City: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("City: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in scenes) if (s.path == path) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    /// <summary>Takes a picture of the whole map from above and saves it to Logs/city_preview.png (only when built from the command line).</summary>
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
            File.WriteAllBytes("Logs/city_preview.png", tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("City: preview picture saved to Logs/city_preview.png");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("City: could not take the preview picture (" + e.Message + "). The scene itself is fine.");
        }
    }
}
