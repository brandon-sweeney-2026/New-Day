using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > Mine Shaft builds the whole mine shaft:
///   1. imports the art in Assets/Project/Art/MineShaft (drawn by Tools/make_mine_art.ps1) and makes the tiles;
///   2. makes the prefabs (stairs, ladder, mine cart, the new enemies and what they spit) in Assets/Farm/Mine/Prefabs;
///   3. makes the tuning asset Assets/Farm/Mine/MineShaftConfig.asset (only if it does not exist yet, so your numbers are kept);
///   4. builds the scene Assets/Project/Scenes/MineShaft.unity (a copy of the Mine scene, emptied, with the hub painted in it);
///   5. in the Mine scene (the mountain trail) points the MineEntrance at the new scene and adds the other fairy door by the dock.
///
/// The MineShaft scene is GENERATED: running this again replaces it, so change the hub in the code below, not by hand.
/// Enemy prefabs that already exist are kept (so numbers you tuned on them survive). Delete a prefab to get the starting one back.
///
/// Without pop-ups, for the command line:  -executeMethod MineShaftSetupMenu.BuildBatch / PreviewBatch / VerifyBatch
/// </summary>
public static class MineShaftSetupMenu
{
    private const string ScenePath = "Assets/Project/Scenes/MineShaft.unity";
    private const string SceneName = "MineShaft";
    private const string TrailScene = "Assets/Project/Scenes/Mine.unity";
    private const string ArtDir = "Assets/Project/Art/MineShaft";
    private const string DataDir = "Assets/Farm/Mine";
    private const string TileDir = DataDir + "/Tiles";
    private const string PrefabDir = DataDir + "/Prefabs";
    private const string ConfigPath = DataDir + "/MineShaftConfig.asset";
    private const string MountainPrefabs = "Assets/Project/Art/Mountain/Prefabs";
    private const string ItemDir = "Assets/Farm/Data/Items";
    private const string LampPrefab = "Assets/Project/Art/Lights/Prefabs/Lantern.prefab";
    private const string FairyDoorPrefab = "Assets/Project/Art/Forest/Prefabs/FairyDoor.prefab";
    private const string LitMaterialPath = "Assets/Resources/Lighting/SpriteLit.mat";
    private const string PreviewTempScene = "Assets/Project/Scenes/_MineShaftPreviewTemp.unity";

    // ---- the hub (floor 0): a room 24 tiles wide and 16 tall. Change these to move things, then run the menu again. ----
    private const int HubW = 24, HubH = 16;
    private static readonly RectInt HubRoom = new RectInt(2, 2, 20, 11);              // the open floor
    private static readonly RectInt HubCorridor = new RectInt(11, 0, 2, 2);           // the way out, at the bottom
    private static readonly Vector2 HubEntry = new Vector2(12f, 2.6f);                // where the player appears coming in from the trail
    private static readonly Vector2 HubFairyDoor = new Vector2(5f, 11.4f);            // against the top wall, on the left
    private static readonly Vector2 HubStairs = new Vector2(18.5f, 11f);              // top right
    private static readonly Vector2 HubCart = new Vector2(17.5f, 5f);                 // on the rails, lower right
    private static readonly Vector2[] HubLamps = { new Vector2(2.6f, 11.4f), new Vector2(21.4f, 11.4f), new Vector2(2.6f, 2.5f), new Vector2(21.4f, 2.5f), new Vector2(9.5f, 11.4f), new Vector2(14.5f, 11.4f) };

    // ---- the other fairy door, on the mountain trail: it is put on the free spot nearest to this point (by the rocks at the land end of the dock) ----
    private static readonly Vector2 TrailFairyDoorWanted = new Vector2(6.5f, -2.5f);

    private static Material lit;

    // ================================================================== menus

    [MenuItem("Farm/Set Up/Mine Shaft", false, 153)]
    public static void BuildFromMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Rebuild the mine shaft?", "MineShaft.unity already exists. Building again REPLACES it (anything you placed by hand in it is lost).\n\nYour numbers in MineShaftConfig and on the enemy prefabs are kept.", "Replace it", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string result = Build();
        EditorUtility.DisplayDialog(result.StartsWith("Built") ? "Mine shaft ready" : "Couldn't build the mine shaft", result, "OK");
    }

    /// <summary>Command line: builds everything, no questions asked. Look for "MINESHAFT BUILD OK" in the log.</summary>
    public static void BuildBatch()
    {
        string result = Build();
        Debug.Log(result.StartsWith("Built") ? "MINESHAFT BUILD OK: " + result : "MINESHAFT BUILD FAILED: " + result);
    }

    [MenuItem("Farm/Test and Debug/Mine Floor Previews (one per theme)", false, 342)]
    public static void PreviewFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string result = Preview(new[] { 5, 25, 45, 65, 85 });
        EditorUtility.DisplayDialog("Mine floor previews", result, "OK");
    }

    /// <summary>
    /// Command line: saves overhead pictures of the hub and of some floors to Logs/ (mine_hub.png, mine_floor_005.png ...).
    /// Add  -mineFloors 1,20,21,100  to choose the floors; without it, one floor of each theme is drawn. Do not use -nographics.
    /// </summary>
    public static void PreviewBatch()
    {
        var floors = new List<int>();
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-mineFloors")
                foreach (string part in args[i + 1].Split(','))
                {
                    int n;
                    if (int.TryParse(part, out n)) floors.Add(n);
                }
        if (floors.Count == 0) floors.AddRange(new[] { 5, 25, 45, 65, 85 });
        Debug.Log("MINESHAFT PREVIEW: " + Preview(floors.ToArray()));
    }

    /// <summary>Command line: generates all 100 floors one after the other and writes one MINEFLOOR| line each to the log (theme, style, rocks, ores, enemies).</summary>
    public static void VerifyBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var mine = Object.FindAnyObjectByType<MineShaftManager>();
        if (mine == null || mine.config == null) { Debug.Log("MINESHAFT VERIFY FAILED: no MineShaftManager with a Config in " + ScenePath); return; }
        var sb = new System.Text.StringBuilder("MINESHAFT VERIFY\n");
        for (int floor = 1; floor <= mine.config.floorCount; floor++) sb.AppendLine(mine.BuildFloor(floor, 7000 + floor));
        mine.BuildFloor(0, 0);
        Debug.Log(sb.ToString());
        Debug.Log("MINESHAFT VERIFY OK");
    }

    [MenuItem("Farm/Test and Debug/Unlock All Mine Cart Floors", false, 327)]
    public static void UnlockCartFloors()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Press Play first", "Progress only exists while the game is playing. Press Play, then choose this again.", "OK"); return; }
        GameState.SetCounter(MineShaftManager.DeepestFloorKey, 100);
        GameState.SetFlag(MineShaftManager.FairyDoorFlag, true);
        Debug.Log("Mine: every cart floor is unlocked and the fairy door is awake.");
    }

    [MenuItem("Farm/Test and Debug/Reset Mine Progress", false, 328)]
    public static void ResetProgress()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Press Play first", "Progress only exists while the game is playing. Press Play, then choose this again.", "OK"); return; }
        GameState.SetCounter(MineShaftManager.DeepestFloorKey, 0);
        GameState.SetFlag(MineShaftManager.FairyDoorFlag, false);
        Debug.Log("Mine: deepest floor set back to 0 and the fairy door is asleep again.");
    }

    // ================================================================== the build

    private static string Build()
    {
        if (!AssetDatabase.IsValidFolder(ArtDir)) return "The mine art is missing (" + ArtDir + "). Run Tools/make_mine_art.ps1 first.";
        if (!File.Exists(TrailScene)) return "The Mine scene (" + TrailScene + ") is used as the starting point, and it is missing.";

        lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        EnsureFolder(TileDir);
        EnsureFolder(PrefabDir);
        ImportArt();

        Dictionary<string, Tile> tiles = MakeTiles();
        Dictionary<string, GameObject> prefabs = MakePrefabs();
        MineShaftConfig config = MakeConfig(tiles, prefabs);
        AssetDatabase.SaveAssets();

        // ---- the scene: start from a copy of the Mine scene (Player, camera, HUD, TimeManager and lighting come with it) ----
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(TrailScene, ScenePath)) return "Could not copy the Mine scene to " + ScenePath + ".";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CleanTemplate(scene);

        GameObject grid = GameObject.Find("Grid");
        Tilemap ground = FindMap("Ground");
        if (grid == null || ground == null) return "The copied scene has no 'Grid' with a 'Ground' tilemap.";
        Material mapMaterial = ground.GetComponent<TilemapRenderer>().sharedMaterial;
        Tilemap walls = MakeMap(grid.transform, "MineWalls", 1, mapMaterial, true);
        Tilemap fill = MakeMap(grid.transform, "MineFill", 1, mapMaterial, false);

        // Opening a scene makes Unity drop assets nothing uses yet, so the tiles, prefabs and config are loaded again here
        tiles = MakeTiles();
        prefabs = MakePrefabs();
        config = AssetDatabase.LoadAssetAtPath<MineShaftConfig>(ConfigPath);
        GameObject hub = BuildHub(tiles, prefabs, mapMaterial, out Transform hubArrival);

        var managerGo = new GameObject("MineShaftManager");
        var mine = managerGo.AddComponent<MineShaftManager>();
        mine.config = config;
        mine.groundMap = ground; mine.wallMap = walls; mine.fillMap = fill;
        mine.hubRoot = hub; mine.hubArrival = hubArrival;
        mine.hubCameraBounds = new Rect(0f, 0f, HubW, HubH);
        mine.ladderUpPrefab = prefabs["LadderUp"]; mine.stairsDownPrefab = prefabs["StairsDown"];

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = HubEntry;
        var cam = Object.FindAnyObjectByType<CameraFollow2D>();
        if (cam != null)
        {
            var so = new SerializedObject(cam);
            so.FindProperty("limitToMap").boolValue = false;            // MineShaftManager tells the camera its limits for each floor
            so.FindProperty("boundsTilemap").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            cam.transform.position = new Vector3(HubEntry.x, HubEntry.y + 2f, cam.transform.position.z);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(ScenePath);

        string trailNote = ConnectTrail(prefabs);
        Debug.Log("MINESHAFT|" + trailNote);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return "Built " + ScenePath + ".\n\n" + trailNote +
               "\n\nTuning numbers: click " + ConfigPath + ".\nPictures of generated floors: Farm > Test and Debug > Mine Floor Previews.";
    }

    // ------------------------------------------------------------------ art
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith("_sheet")) continue;                                              // the three recoloured sheets are cut up below
            // tiles, rails and shots turn around their middle; everything else has its pivot at its feet
            bool centre = name.StartsWith("tile_") || name.StartsWith("shot_") || name == "mine_rails";
            ImportSingle(path, centre ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        }
        AssetDatabase.Refresh();
    }

    private static Sprite Spr(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + name + ".png"); }

    private static Sprite[] Sprs(string baseName, params int[] numbers)
    {
        var list = new List<Sprite>();
        foreach (int n in numbers) { Sprite s = Spr(baseName + "_" + n); if (s != null) list.Add(s); }
        return list.ToArray();
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

    /// <summary>Cuts a sheet into equal cells with STABLE sprite IDs. Returns "column_row" (row 0 at the top) -> sprite.</summary>
    private static Dictionary<string, Sprite> SliceSheet(string path, int cellW, int cellH, SpriteAlignment align)
    {
        var result = new Dictionary<string, Sprite>();
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return result;
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

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(imp);
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
                rect.spriteID = StableSpriteId.For(provider, baseName, rect.name);      // never a random ID (see CLAUDE.md rule 5)
                rects.Add(rect);
            }
        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect sr in rects) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        idProvider.SetNameFileIdPairs(pairs);
        provider.Apply();
        imp.SaveAndReimport();

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = o as Sprite;
            if (s != null && s.name.StartsWith(baseName + "_")) result[s.name.Substring(baseName.Length + 1)] = s;
        }
        return result;
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

    // ------------------------------------------------------------------ tiles
    private static readonly string[] ThemeKeys = { "ice", "rock", "cave", "desert", "lava" };

    private static Dictionary<string, Tile> MakeTiles()
    {
        var tiles = new Dictionary<string, Tile>();
        foreach (string key in ThemeKeys)
        {
            foreach (string part in new[] { "floor_a", "floor_b", "floor_c" }) tiles[key + "_" + part] = MakeTile(key + "_" + part, false);
            foreach (string part in new[] { "wall_face", "wall_top", "pool" }) tiles[key + "_" + part] = MakeTile(key + "_" + part, true);   // solid
        }
        return tiles;
    }

    private static Tile MakeTile(string name, bool solid)
    {
        string path = TileDir + "/Mine_" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }
        tile.sprite = Spr("tile_" + name);
        tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    // ------------------------------------------------------------------ prefabs
    private static Dictionary<string, GameObject> MakePrefabs()
    {
        var p = new Dictionary<string, GameObject>();

        // ---- stairs, ladder, cart (rebuilt every time: they have nothing to tune) ----
        var stairs = NewSprite("StairsDown", Spr("mine_stairs_down"), 2);
        AddTrigger(stairs, new Vector2(0.9f, 0.9f), new Vector2(0f, 0.5f));
        stairs.AddComponent<MineStairs>().direction = MineStairs.Direction.DownOneFloor;
        p["StairsDown"] = Save(stairs);

        var ladder = NewSprite("LadderUp", Spr("mine_ladder_up"), 9);
        AddTrigger(ladder, new Vector2(0.9f, 1.2f), new Vector2(0f, 0.6f));
        ladder.AddComponent<MineStairs>().direction = MineStairs.Direction.UpToHub;
        p["LadderUp"] = Save(ladder);

        var cart = NewSprite("MineCart", Spr("mine_cart"), 10);
        var cartBox = cart.AddComponent<BoxCollider2D>();
        cartBox.size = new Vector2(1.3f, 0.5f); cartBox.offset = new Vector2(0f, 0.3f);
        cart.AddComponent<BehindPlayerSorter>().thresholdOffset = 0.1f;
        cart.AddComponent<MineCart>();
        p["MineCart"] = Save(cart);

        // ---- what the spitters spit ----
        foreach (string v in new[] { "rock", "bubble", "cactus", "fire" })
        {
            var shot = new GameObject("Shot_" + v);
            var sr = shot.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("shot_" + v); sr.sortingOrder = 12;
            if (lit != null) sr.sharedMaterial = lit;
            shot.AddComponent<EnemyProjectile>();
            p["Shot_" + v] = Save(shot);
        }

        ItemData chitin = Item("Chitin"), wing = Item("BatWing"), stone = Item("Stone"), coal = Item("Coal");
        Dictionary<string, Sprite> caveBat = SliceSheet(ArtDir + "/cave_bat_sheet.png", 24, 16, SpriteAlignment.Center);
        Dictionary<string, Sprite> fireBat = SliceSheet(ArtDir + "/fire_bat_sheet.png", 24, 16, SpriteAlignment.Center);
        Dictionary<string, Sprite> scarab = SliceSheet(ArtDir + "/scarab_sheet.png", 16, 16, SpriteAlignment.BottomCenter);

        // ---- set 1, floors 1-20 (icy): the mountain Bug and Bat, plus the Frost Slime ----
        p["Bug"] = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPrefabs + "/Bug.prefab");
        p["Bat"] = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPrefabs + "/Bat.prefab");
        p["FrostSlime"] = Slime("FrostSlime", "Frost Slime", "frost", 10, 2, 3.6f, 1f, 1, 3, null);

        // ---- set 2, floors 21-40 (rocky) ----
        p["MudSlime"] = Slime("MudSlime", "Mud Slime", "mud", 22, 5, 4.4f, 1f, 3, 6, null);
        p["RockSpitter"] = Shooter("RockSpitter", "Rock Spitter", "rock", 18, 5, 1, 0f, 5f, 3, 6, Drop(stone, 1, 2, 0.5f));
        p["Mole"] = Burrower("Mole", "Mole", "mole", 24, 6, 3.2f, 3, 7);

        // ---- set 3, floors 41-60 (watery cave) ----
        p["CaveBat"] = Bat("CaveBat", "Cave Bat", caveBat, 26, 8, 5f, new Vector2(1.6f, 2.8f), 5, 10, Drop(wing, 1, 2, 0.6f));
        p["BubbleSpitter"] = Shooter("BubbleSpitter", "Bubble Spitter", "bubble", 30, 7, 3, 40f, 5.5f, 6, 11, null);
        p["StoneGolem"] = Golem("StoneGolem", "Stone Golem", "stone", 70, 11, 1.3f, 10, 18, Drop(stone, 2, 4, 1f));

        // ---- set 4, floors 61-80 (desert) ----
        p["Scarab"] = Bug("Scarab", "Scarab", scarab, 50, 11, 5.4f, 9f, 9, 15, Drop(chitin, 1, 3, 0.7f));
        p["SandWorm"] = Burrower("SandWorm", "Sand Worm", "worm", 55, 13, 4f, 10, 17);
        p["CactusSpitter"] = Shooter("CactusSpitter", "Cactus", "cactus", 48, 10, 8, 360f, 4.5f, 10, 16, null);

        // ---- set 5, floors 81-100 (lava): four kinds, and the Magma Slime bursts into two Ember Slimes ----
        GameObject ember = Slime("EmberSlime", "Ember Slime", "magma", 25, 9, 5.2f, 0.65f, 3, 6, null);
        p["EmberSlime"] = ember;
        p["MagmaSlime"] = Slime("MagmaSlime", "Magma Slime", "magma", 70, 15, 5f, 1.15f, 14, 22, ember);
        p["FireBat"] = Bat("FireBat", "Fire Bat", fireBat, 60, 16, 5.8f, new Vector2(1.3f, 2.3f), 14, 22, Drop(wing, 1, 2, 0.6f));
        p["FireSpitter"] = Shooter("FireSpitter", "Fire Spitter", "fire", 65, 14, 5, 70f, 6.5f, 15, 24, Drop(coal, 1, 2, 0.5f));
        p["MagmaGolem"] = Golem("MagmaGolem", "Magma Golem", "magma", 160, 22, 1.5f, 25, 40, Drop(coal, 2, 4, 1f));
        return p;
    }

    private static ItemData Item(string name) { return AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/" + name + ".asset"); }

    private static EnemyDrop[] Drop(ItemData item, int min, int max, float chance)
    {
        return item == null ? null : new[] { new EnemyDrop { item = item, min = min, max = max, chance = chance } };
    }

    /// <summary>A prop: one object with a picture on it (pivot at its feet).</summary>
    private static GameObject NewSprite(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.sortingOrder = order;
        if (lit != null) sr.sharedMaterial = lit;
        return go;
    }

    private static void AddTrigger(GameObject go, Vector2 size, Vector2 offset)
    {
        var box = go.AddComponent<BoxCollider2D>();          // the collider goes on first: scripts that need one refuse to be added without it
        box.isTrigger = true; box.size = size; box.offset = offset;
    }

    private static GameObject Save(GameObject go)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + go.name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>An enemy prefab that already exists is kept as it is (your tuning survives). Returns null when it must be built.</summary>
    private static GameObject Existing(string name) { return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + name + ".prefab"); }

    /// <summary>The part every ground enemy shares: picture, body, round collider.</summary>
    private static GameObject EnemyRoot(string name, Sprite sprite, float radius, float colliderY)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.sortingOrder = 10;
        if (lit != null) sr.sharedMaterial = lit;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = radius; col.offset = new Vector2(0f, colliderY);
        go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;
        return go;
    }

    private static void Stats(EnemyBase e, string shownName, int health, int damage, int goldMin, int goldMax, EnemyDrop[] drops)
    {
        e.enemyName = shownName; e.maxHealth = health; e.contactDamage = damage;
        e.goldMin = goldMin; e.goldMax = goldMax; e.drops = drops;
    }

    private static GameObject Slime(string name, string shown, string art, int health, int damage, float hopSpeed, float size, int goldMin, int goldMax, GameObject splitInto)
    {
        GameObject old = Existing(name);
        if (old != null) return old;
        Sprite[] frames = Sprs("slime_" + art, 0, 1, 2, 3);
        var go = EnemyRoot(name, frames.Length > 0 ? frames[0] : null, 0.3f, 0.3f);
        go.transform.localScale = new Vector3(size, size, 1f);
        var e = go.AddComponent<SlimeEnemy>();
        Stats(e, shown, health, damage, goldMin, goldMax, null);
        e.frames = frames; e.hopSpeed = hopSpeed;
        e.splitInto = splitInto; e.splitCount = splitInto != null ? 2 : 0;
        e.shadowSize = new Vector2(0.8f, 0.3f); e.shadowOffset = new Vector2(0f, 0.08f);
        return Save(go);
    }

    private static GameObject Shooter(string name, string shown, string art, int health, int damage, int shots, float spread, float shotSpeed, int goldMin, int goldMax, EnemyDrop[] drops)
    {
        GameObject old = Existing(name);
        if (old != null) return old;
        Sprite[] idle = Sprs("spitter_" + art, 0, 1);
        var go = EnemyRoot(name, idle.Length > 0 ? idle[0] : null, 0.3f, 0.3f);
        var e = go.AddComponent<ShooterEnemy>();
        Stats(e, shown, health, damage, goldMin, goldMax, drops);
        e.idleFrames = idle; e.aimFrames = Sprs("spitter_" + art, 2);
        e.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Shot_" + art + ".prefab");
        e.shotsPerVolley = shots; e.spreadDegrees = spread; e.projectileSpeed = shotSpeed;
        e.touchRadius = 0.5f;
        e.shadowSize = new Vector2(0.75f, 0.28f); e.shadowOffset = new Vector2(0f, 0.06f);
        return Save(go);
    }

    private static GameObject Burrower(string name, string shown, string art, int health, int damage, float speed, int goldMin, int goldMax)
    {
        GameObject old = Existing(name);
        if (old != null) return old;
        Sprite[] mound = Sprs("burrower_" + art + "_mound", 0, 1);
        var go = EnemyRoot(name, mound.Length > 0 ? mound[0] : null, 0.32f, 0.35f);
        var e = go.AddComponent<BurrowerEnemy>();
        Stats(e, shown, health, damage, goldMin, goldMax, null);
        e.moundFrames = mound; e.upFrames = Sprs("burrower_" + art + "_up", 0, 1);
        e.burrowSpeed = speed; e.touchRadius = 0.8f;
        e.shadowSize = new Vector2(0.9f, 0.25f); e.shadowOffset = new Vector2(0f, 0.05f);
        return Save(go);
    }

    private static GameObject Golem(string name, string shown, string art, int health, int damage, float speed, int goldMin, int goldMax, EnemyDrop[] drops)
    {
        GameObject old = Existing(name);
        if (old != null) return old;
        Sprite[] walk = Sprs("golem_" + art, 0, 1);
        var go = EnemyRoot(name, walk.Length > 0 ? walk[0] : null, 0.45f, 0.45f);
        var e = go.AddComponent<GolemEnemy>();
        Stats(e, shown, health, damage, goldMin, goldMax, drops);
        e.walkFrames = walk; e.windupFrame = Spr("golem_" + art + "_2"); e.slamFrame = Spr("golem_" + art + "_3");
        e.walkSpeed = speed; e.knockbackResist = 0.85f; e.contactKnockback = 9f; e.touchRadius = 0.8f;
        e.shadowSize = new Vector2(1.3f, 0.45f); e.shadowOffset = new Vector2(0f, 0.08f);
        return Save(go);
    }

    /// <summary>A recoloured Bug (same script and layout as the mountain Bug).</summary>
    private static GameObject Bug(string name, string shown, Dictionary<string, Sprite> sheet, int health, int damage, float chaseSpeed, float chargeSpeed, int goldMin, int goldMax, EnemyDrop[] drops)
    {
        GameObject old = Existing(name);
        if (old != null) return old;
        Sprite[] side = Cells(sheet, 0, 0, 4);
        var go = EnemyRoot(name, side.Length > 0 ? side[0] : null, 0.28f, 0.3f);
        var e = go.AddComponent<BugEnemy>();
        Stats(e, shown, health, damage, goldMin, goldMax, drops);
        e.sideFrames = side; e.downFrames = Cells(sheet, 0, 1, 4); e.upFrames = Cells(sheet, 0, 2, 4);
        e.chaseSpeed = chaseSpeed; e.chargeSpeed = chargeSpeed; e.turnRate = 110f;
        e.shadowSize = new Vector2(0.85f, 0.32f); e.shadowOffset = new Vector2(0f, 0.12f);
        return Save(go);
    }

    /// <summary>A recoloured Bat (same script and layout as the mountain Bat: the picture is on a child called Body).</summary>
    private static GameObject Bat(string name, string shown, Dictionary<string, Sprite> sheet, int health, int damage, float flySpeed, Vector2 betweenSwoops, int goldMin, int goldMax, EnemyDrop[] drops)
    {
        GameObject old = Existing(name);
        if (old != null) return old;
        var go = new GameObject(name);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f; col.offset = new Vector2(0f, 0.25f); col.isTrigger = true;

        var body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 24;
        if (lit != null) sr.sharedMaterial = lit;

        var e = go.AddComponent<BatEnemy>();
        Stats(e, shown, health, damage, goldMin, goldMax, drops);
        e.body = body.transform;
        e.flapFrames = Cells(sheet, 0, 0, 4); e.diveFrames = Cells(sheet, 4, 0, 2);
        if (e.flapFrames.Length > 0) sr.sprite = e.flapFrames[0];
        e.flySpeed = flySpeed; e.timeBetweenSwoops = betweenSwoops;
        e.shadowSize = new Vector2(0.9f, 0.3f); e.shadowOffset = new Vector2(0f, 0.05f);
        return Save(go);
    }

    // ------------------------------------------------------------------ the tuning asset
    private static MineShaftConfig MakeConfig(Dictionary<string, Tile> tiles, Dictionary<string, GameObject> p)
    {
        var config = AssetDatabase.LoadAssetAtPath<MineShaftConfig>(ConfigPath);
        if (config != null) return config;                       // already there: your numbers are kept

        config = ScriptableObject.CreateInstance<MineShaftConfig>();
        config.lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab);

        // The ore table: which ore on which floors. Coal and copper at the top, platinum and diamonds at the bottom.
        config.ores = new[]
        {
            Ore("Coal", "Ore_Coal", 1, 40, 10f),
            Ore("Copper", "Ore_Copper", 1, 45, 10f),
            Ore("Iron", "Ore_Iron", 15, 65, 10f),
            Ore("Gold", "Ore_Gold", 35, 85, 8f),
            Ore("Platinum", "Ore_Platinum", 55, 100, 8f),
            Ore("Emerald", "Gem_Emerald", 25, 100, 2f),
            Ore("Ruby", "Gem_Ruby", 45, 100, 2f),
            Ore("Diamond", "Gem_Diamond", 65, 100, 2f),
        };

        GameObject greyS = Rock("Rock_Grey_Small"), greyL = Rock("Rock_Grey_Large"), brownS = Rock("Rock_Brown_Small"), brownL = Rock("Rock_Brown_Large");
        config.themes = new[]
        {
            Theme("Icy caverns", 1, "ice", tiles, new Color(0.62f, 0.78f, 1f), 0.5f, new[] { greyS, greyL },
                Enemy(p, "Bug", 3f), Enemy(p, "Bat", 1.5f), Enemy(p, "FrostSlime", 3f)),
            Theme("Rocky depths", 21, "rock", tiles, new Color(0.92f, 0.8f, 0.65f), 0.4f, new[] { brownS, brownL },
                Enemy(p, "MudSlime", 3f), Enemy(p, "RockSpitter", 2f), Enemy(p, "Mole", 2f)),
            Theme("Flooded caves", 41, "cave", tiles, new Color(0.5f, 0.8f, 0.85f), 0.36f, new[] { greyS, greyL },
                Enemy(p, "CaveBat", 2f), Enemy(p, "BubbleSpitter", 2.5f), Enemy(p, "StoneGolem", 1f)),
            Theme("Buried desert", 61, "desert", tiles, new Color(1f, 0.86f, 0.6f), 0.5f, new[] { brownS, brownL },
                Enemy(p, "Scarab", 3f), Enemy(p, "SandWorm", 2f), Enemy(p, "CactusSpitter", 2f)),
            Theme("Core of the earth", 81, "lava", tiles, new Color(1f, 0.52f, 0.36f), 0.42f, new[] { greyL, brownL },
                Enemy(p, "MagmaSlime", 2.5f), Enemy(p, "FireBat", 2f), Enemy(p, "FireSpitter", 2f), Enemy(p, "MagmaGolem", 1f)),
        };
        AssetDatabase.CreateAsset(config, ConfigPath);
        return config;
    }

    private static GameObject Rock(string name) { return AssetDatabase.LoadAssetAtPath<GameObject>(MountainPrefabs + "/" + name + ".prefab"); }

    private static MineOreEntry Ore(string label, string prefab, int first, int last, float weight)
    {
        return new MineOreEntry { label = label, rockPrefab = Rock(prefab), firstFloor = first, lastFloor = last, weight = weight };
    }

    private static MineEnemyEntry Enemy(Dictionary<string, GameObject> p, string name, float weight)
    {
        GameObject prefab;
        p.TryGetValue(name, out prefab);
        if (prefab == null) Debug.LogWarning("Mine shaft: the enemy prefab '" + name + "' is missing, so it is left out of its set.");
        return new MineEnemyEntry { prefab = prefab, weight = weight };
    }

    private static MineTheme Theme(string name, int firstFloor, string key, Dictionary<string, Tile> tiles, Color ambient, float brightness, GameObject[] rocks, params MineEnemyEntry[] enemies)
    {
        return new MineTheme
        {
            themeName = name, firstFloor = firstFloor, lastFloor = firstFloor + 19,
            floorTiles = new TileBase[] { tiles[key + "_floor_a"], tiles[key + "_floor_b"], tiles[key + "_floor_c"] },
            wallFaceTile = tiles[key + "_wall_face"], wallTopTile = tiles[key + "_wall_top"], poolTile = tiles[key + "_pool"],
            ambientColor = ambient, ambientBrightness = brightness,
            plainRocks = rocks, enemies = enemies, statMultiplierAtLastFloor = 1.5f
        };
    }

    // ------------------------------------------------------------------ the scene
    /// <summary>Removes everything that belongs to the mountain trail and empties the tilemaps, keeping the Player, camera, HUD and managers.</summary>
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

    /// <summary>A new tilemap. 'solid' adds the colliders that make its tiles block the player (merged into a few big shapes so it stays fast).</summary>
    private static Tilemap MakeMap(Transform parent, string name, int order, Material material, bool solid)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var map = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = order;
        if (material != null) renderer.sharedMaterial = material;
        if (solid)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tileCollider = go.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;      // solid all the way through, not just an outline
        }
        return map;
    }

    /// <summary>The hub: a fixed, safe room with the way out, the fairy door, the stairs down and the mine cart.</summary>
    private static GameObject BuildHub(Dictionary<string, Tile> tiles, Dictionary<string, GameObject> prefabs, Material mapMaterial, out Transform arrival)
    {
        var hub = new GameObject("Hub");
        var gridGo = new GameObject("HubGrid");
        gridGo.transform.SetParent(hub.transform, false);
        gridGo.AddComponent<Grid>();
        Tilemap floor = MakeMap(gridGo.transform, "HubFloor", 0, mapMaterial, false);
        Tilemap walls = MakeMap(gridGo.transform, "HubWalls", 1, mapMaterial, true);
        Tilemap fill = MakeMap(gridGo.transform, "HubFill", 1, mapMaterial, false);

        // tiles: the hub uses the rocky theme
        var rng = new System.Random(4242);
        TileBase[] floors = { tiles["rock_floor_a"], tiles["rock_floor_b"], tiles["rock_floor_c"] };
        const int margin = 14;
        for (int y = -margin; y < HubH + margin; y++)
            for (int x = -margin; x < HubW + margin; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (HubIsFloor(x, y)) { floor.SetTile(cell, rng.NextDouble() < 0.72 ? floors[0] : floors[1 + rng.Next(2)]); continue; }
                bool nearFloor = false;
                for (int dy = -1; dy <= 1 && !nearFloor; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (HubIsFloor(x + dx, y + dy)) { nearFloor = true; break; }
                if (nearFloor) walls.SetTile(cell, HubIsFloor(x, y - 1) ? tiles["rock_wall_face"] : tiles["rock_wall_top"]);
                else fill.SetTile(cell, tiles["rock_wall_top"]);
            }

        // the way out: walk into the bottom of the corridor to go back to the mountain trail
        var exit = new GameObject("ExitToTrail");
        exit.transform.SetParent(hub.transform, false);
        exit.transform.position = new Vector3(HubCorridor.x + HubCorridor.width * 0.5f, 0.5f, 0f);
        exit.transform.localScale = new Vector3(HubCorridor.width, 1f, 1f);
        var se = exit.AddComponent<SceneExit>();                     // (adds its BoxCollider2D by itself)
        exit.GetComponent<BoxCollider2D>().isTrigger = true;
        Set(se, "sceneName", "Mine"); Set(se, "arriveAt", "FromMineShaft"); Set(se, "promptText", "Leave the mine");

        Spawn(hub.transform, "FromMountain", HubEntry, Vector2.up, true);

        // the fairy door: press the action button once to wake it; after that it leads to the dock at the start of the trail
        GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FairyDoorPrefab);
        if (doorPrefab != null)
        {
            var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, hub.transform);
            door.name = "MineFairyDoor";
            door.transform.position = HubFairyDoor;
            SetUpFairyDoor(door, true, "Mine", "FromMineFairyDoor",
                "A tiny door is set into the rock. It is fast asleep. Maybe a gentle knock would wake it.",
                "The fairy door wakes with a soft glow. From now on it leads to the dock at the foot of the mountain, and back.");
            Spawn(hub.transform, "FromFairyDoor", HubFairyDoor + new Vector2(0f, -1.4f), Vector2.down, false);
        }
        else Debug.LogWarning("Mine shaft: " + FairyDoorPrefab + " was not found, so the hub has no fairy door.");

        // the stairs down to floor 1
        var stairs = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["StairsDown"], hub.transform);
        stairs.transform.position = new Vector3(HubStairs.x, HubStairs.y, 0f);
        var arrive = new GameObject("ArriveFromBelow");
        arrive.transform.SetParent(hub.transform, false);
        arrive.transform.position = new Vector3(HubStairs.x, HubStairs.y - 1.2f, 0f);
        arrival = arrive.transform;

        // the mine cart on a short stretch of rails
        var rails = new GameObject("Rails");
        rails.transform.SetParent(hub.transform, false);
        for (int x = 14; x <= 21; x++)
        {
            var piece = new GameObject("Rail");
            piece.transform.SetParent(rails.transform, false);
            piece.transform.position = new Vector3(x + 0.5f, HubCart.y + 0.1f, 0f);
            var sr = piece.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("mine_rails"); sr.sortingOrder = 2;                      // flat on the ground, under everything that stands up
            if (lit != null) sr.sharedMaterial = lit;
        }
        var cart = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["MineCart"], hub.transform);
        cart.transform.position = new Vector3(HubCart.x, HubCart.y, 0f);

        // lamps
        GameObject lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab);
        if (lampPrefab != null)
            foreach (Vector2 at in HubLamps)
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, hub.transform);
                lamp.transform.position = new Vector3(at.x, at.y, 0f);
            }
        return hub;
    }

    private static bool HubIsFloor(int x, int y)
    {
        return HubRoom.Contains(new Vector2Int(x, y)) || HubCorridor.Contains(new Vector2Int(x, y));
    }

    private static void Spawn(Transform parent, string id, Vector2 at, Vector2 facing, bool isDefault)
    {
        var go = new GameObject(id);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(at.x, at.y, 0f);
        var sp = go.AddComponent<SceneSpawnPoint>();
        var so = new SerializedObject(sp);
        so.FindProperty("spawnId").stringValue = id;
        so.FindProperty("isDefault").boolValue = isDefault;
        so.FindProperty("faceDirection").vector2Value = facing;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetUpFairyDoor(GameObject door, bool wakeOnInteract, string toScene, string arriveAt, string lockedHint, string wakeMessage)
    {
        var d = door.GetComponent<ForestShortcutDoor>();
        if (d == null) { Debug.LogWarning("Mine shaft: the FairyDoor prefab has no ForestShortcutDoor on it."); return; }
        d.partner = null;
        d.requireFlag = MineShaftManager.FairyDoorFlag;
        d.wakeOnInteract = wakeOnInteract;
        d.travelToScene = toScene;
        d.travelArriveAt = arriveAt;
        d.lockedHint = lockedHint;
        d.wakeMessage = wakeMessage;
        PrefabUtility.RecordPrefabInstancePropertyModifications(d);      // so the scene remembers these changes to the prefab
        EditorUtility.SetDirty(d);
    }

    // ------------------------------------------------------------------ the mountain trail end
    /// <summary>In the Mine scene (the trail): points the MineEntrance at the mine shaft, and adds the arrival point and the second fairy door.</summary>
    private static string ConnectTrail(Dictionary<string, GameObject> prefabs)
    {
        Scene trail = EditorSceneManager.OpenScene(TrailScene, OpenSceneMode.Single);
        var notes = new List<string>();

        // everything this menu adds to the trail lives under one object, so a second run replaces it cleanly
        GameObject old = GameObject.Find("MineShaftLinks");
        if (old != null) Object.DestroyImmediate(old);
        var links = new GameObject("MineShaftLinks");

        var entrance = Object.FindAnyObjectByType<MineEntrance>();
        if (entrance != null)
        {
            entrance.sceneName = SceneName;
            entrance.arriveAt = "FromMountain";
            PrefabUtility.RecordPrefabInstancePropertyModifications(entrance);
            EditorUtility.SetDirty(entrance);
            Vector2 back = (Vector2)entrance.transform.position + new Vector2(0f, -1.6f);
            Spawn(links.transform, "FromMineShaft", back, Vector2.down, false);
            notes.Add("MineEntrance at (" + entrance.transform.position.x.ToString("0.0") + ", " + entrance.transform.position.y.ToString("0.0") + ") now leads to " + SceneName + ".");
        }
        else notes.Add("!! No MineEntrance was found in the Mine scene. Place the MineEntrance prefab at the end of the trail and run this again.");

        GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FairyDoorPrefab);
        if (doorPrefab != null)
        {
            bool free;
            Vector2 at = FindDoorSpot(TrailFairyDoorWanted, out free);
            var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, links.transform);
            door.name = "MineFairyDoor";
            door.transform.position = new Vector3(at.x, at.y, 0f);
            SetUpFairyDoor(door, false, SceneName, "FromFairyDoor",
                "A tiny door in the rock, fast asleep. It feels like it is waiting for something deep inside the mountain.", "");
            Spawn(links.transform, "FromMineFairyDoor", at + new Vector2(0f, -1.4f), Vector2.down, false);
            notes.Add("Fairy door on the trail at (" + at.x.ToString("0.0") + ", " + at.y.ToString("0.0") + ")" + (free ? "" : " !! no clear spot was found near the dock, so it was put at the wanted point: check it in the Scene view") + ".");
        }
        else notes.Add("!! " + FairyDoorPrefab + " was not found, so no fairy door was added to the trail.");

        EditorSceneManager.MarkSceneDirty(trail);
        EditorSceneManager.SaveScene(trail);
        return string.Join(" ", notes.ToArray());
    }

    /// <summary>The spot nearest to 'wanted' where the door stands on ground, off the cliffs and the dock, and touches nothing solid.</summary>
    private static Vector2 FindDoorSpot(Vector2 wanted, out bool free)
    {
        Tilemap ground = FindMap("Ground"), cliffs = FindMap("Cliffs"), paths = FindMap("Paths");
        Physics2D.SyncTransforms();
        Vector2 best = wanted; float bestDist = float.MaxValue;
        free = false;
        for (float dy = -6f; dy <= 6f; dy += 0.5f)
            for (float dx = -6f; dx <= 8f; dx += 0.5f)
            {
                var p = new Vector2(wanted.x + dx, wanted.y + dy);
                float dist = new Vector2(dx, dy).sqrMagnitude;
                if (dist >= bestDist || !DoorFits(p, ground, cliffs, paths)) continue;
                best = p; bestDist = dist; free = true;
            }
        return best;
    }

    private static bool DoorFits(Vector2 p, Tilemap ground, Tilemap cliffs, Tilemap paths)
    {
        // the door is about 2 wide and 2 tall, and the player steps out 1.4 below it: all of that must be plain ground
        for (float y = -2f; y <= 2f; y += 1f)
            for (float x = -1.5f; x <= 1.5f; x += 1f)
            {
                var world = new Vector3(p.x + x, p.y + y, 0f);
                if (ground != null && !ground.HasTile(ground.WorldToCell(world))) return false;
                if (cliffs != null && cliffs.HasTile(cliffs.WorldToCell(world))) return false;
                if (paths != null)
                {
                    TileBase t = paths.GetTile(paths.WorldToCell(world));
                    if (t != null && t.name.StartsWith("Dock")) return false;
                }
            }
        foreach (Collider2D c in Physics2D.OverlapBoxAll(p + new Vector2(0f, -0.2f), new Vector2(3f, 4f), 0f))
            if (!c.isTrigger) return false;
        return true;
    }

    // ------------------------------------------------------------------ previews
    private static string Preview(int[] floors)
    {
        if (!File.Exists(ScenePath)) return "There is no " + ScenePath + " yet. Run Farm > Set Up > Mine Shaft first.";
        var made = new List<string>();

        // the trail end: the dock and the fairy door
        EditorSceneManager.OpenScene(TrailScene, OpenSceneMode.Single);
        GameObject trailDoor = GameObject.Find("MineFairyDoor");
        if (trailDoor != null)
        {
            Vector3 d = trailDoor.transform.position;
            Render(new Rect(d.x - 18f, d.y - 9f, 30f, 18f), 16, "Logs/mine_trail_fairy_door.png", made);
        }

        for (int i = -1; i < floors.Length; i++)
        {
            // Objects made a moment ago do not always show up in a picture, so each floor is saved to a throw-away copy of the scene,
            // and the picture is taken from that copy.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var mine = Object.FindAnyObjectByType<MineShaftManager>();
            if (mine == null) return "The scene has no MineShaftManager. Run Farm > Set Up > Mine Shaft again.";
            int floor = i < 0 ? 0 : floors[i];
            Debug.Log(mine.BuildFloor(floor, 1000 + floor));
            Vector3 arrive = floor == 0 ? (Vector3)HubEntry : GameObject.Find("LadderUp") != null ? GameObject.Find("LadderUp").transform.position : Vector3.zero;
            Bounds b = floor == 0 ? new Bounds(new Vector3(HubW * 0.5f, HubH * 0.5f, 0f), new Vector3(HubW, HubH, 0f)) : mine.groundMap.localBounds;

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), PreviewTempScene, true);
            EditorSceneManager.OpenScene(PreviewTempScene, OpenSceneMode.Single);

            if (floor == 0) Render(new Rect(-3f, -3f, HubW + 6f, HubH + 6f), 16, "Logs/mine_hub.png", made);
            else
            {
                string id = floor.ToString("000");
                Render(new Rect(b.min.x - 2f, b.min.y - 2f, b.size.x + 4f, b.size.y + 4f), 8, "Logs/mine_floor_" + id + ".png", made);      // the whole floor, small
                Render(new Rect(arrive.x - 16f, arrive.y - 10f, 32f, 20f), 24, "Logs/mine_floor_" + id + "_start.png", made);                 // close up round the ladder
            }
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AssetDatabase.DeleteAsset(PreviewTempScene);
        return "Saved " + made.Count + " pictures in the project's Logs folder:\n" + string.Join("\n", made.ToArray());
    }

    /// <summary>Takes an overhead picture of part of the open scene.</summary>
    private static void Render(Rect world, int pixelsPerTile, string file, List<string> made)
    {
        GameObject lightGo = null, camGo = null;
        RenderTexture rt = null; Texture2D tex = null;
        try
        {
            SortingGroup.UpdateAllSortingGroups();

            // sprites here use a "lit" material, which is black without a light: add a plain white one if the scene has none switched on
            bool hasGlobal = false;
            foreach (Light2D l in Object.FindObjectsByType<Light2D>())
                if (l.lightType == Light2D.LightType.Global && l.isActiveAndEnabled) { hasGlobal = true; l.intensity = 1f; l.color = Color.white; }
            if (!hasGlobal)
            {
                lightGo = new GameObject("PreviewLight");
                var light = lightGo.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                light.intensity = 1f;
            }

            camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = world.height * 0.5f;
            cam.aspect = world.width / world.height;
            cam.transform.position = new Vector3(world.center.x, world.center.y, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 1f, 0f);

            rt = new RenderTexture(Mathf.RoundToInt(world.width * pixelsPerTile), Mathf.RoundToInt(world.height * pixelsPerTile), 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(file, tex.EncodeToPNG());          // (Unity empties the Temp folder when it closes, so pictures go to Logs)
            made.Add(file);
            cam.targetTexture = null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Mine shaft: could not take the picture " + file + " (" + e.Message + ").");
        }
        finally
        {
            RenderTexture.active = null;
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (lightGo != null) Object.DestroyImmediate(lightGo);
            if (rt != null) Object.DestroyImmediate(rt);
            if (tex != null) Object.DestroyImmediate(tex);
        }
    }

    // ------------------------------------------------------------------ small helpers
    private static void Set(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning("Mine shaft: field '" + field + "' not found on " + target.GetType().Name); return; }
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in scenes)
            if (s.path == path) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
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
}
