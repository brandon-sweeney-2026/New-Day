using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the farmhouse. Menu: Farm > House > Build Farmhouse.
///
/// Before running it, copy the "House" folder (the PNG art) anywhere into your project (e.g. Assets/Farm/House).
/// It then:
///   - sets up all the House pictures as pixel-art sprites and makes tiles from the floors and wallpapers,
///   - creates the building materials, the dishes, the recipes and the ~40 house options (all editable assets),
///   - builds the house in your open scene at the centre of your Scene view: an 8 x 4 tile farmhouse outside,
///     a room inside (far from the farm), a door each way, a bed, a stove, a design table, and a General Store stall.
///
/// Safe to run again: assets are reused. If a house is already in the scene it only refreshes the lists.
/// This file must live in a folder named "Editor".
/// </summary>
public static class HouseSetupMenu
{
    private const string DataRoot = "Assets/Farm/House/Data";
    private const string ItemsFolder = "Assets/Farm/Data/Items";

    private static string artFolder;

    // ------------------------------------------------------------------
    // Entry point
    // ------------------------------------------------------------------

    [MenuItem("Farm/House/Build Farmhouse")]
    public static void Build()
    {
        string[] found = AssetDatabase.FindAssets("house_ext_walls t:Texture2D");
        if (found.Length == 0)
        {
            EditorUtility.DisplayDialog("House art not found",
                "Copy the 'House' folder from the zip into your project (for example Assets/Farm/House) first, then run this again.", "OK");
            return;
        }
        artFolder = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(found[0])).Replace('\\', '/');

        var log = new List<string>();

        EnsureFolder(DataRoot);
        EnsureFolder(DataRoot + "/Tiles");
        EnsureFolder(DataRoot + "/Recipes");
        EnsureFolder(DataRoot + "/Upgrades");
        EnsureFolder(ItemsFolder);

        ImportArt();
        Dictionary<string, TileBase> tiles = MakeTiles();
        Dictionary<string, ItemData> items = MakeItems(log);
        List<Recipe> recipes = MakeRecipes(items, log);
        List<HouseUpgradeData> catalog = MakeCatalog(items, tiles, log);

        AssetDatabase.SaveAssets();

        var existing = Object.FindAnyObjectByType<HouseManager>();
        if (existing != null)
        {
            SetArray(existing, "catalog", catalog.Cast<Object>().ToList());
            foreach (CookingStation c in Object.FindObjectsByType<CookingStation>(FindObjectsInactive.Exclude))
                SetArray(c, "recipes", recipes.Cast<Object>().ToList());
            foreach (ShopCounter s in Object.FindObjectsByType<ShopCounter>(FindObjectsInactive.Exclude))
                SetArray(s, "stock", ShopStock().Cast<Object>().ToList());
            log.Add("A house already exists in this scene, so only its lists were refreshed. Delete the 'Farmhouse', 'HouseInterior' and 'HouseManager' objects to rebuild it from scratch.");
        }
        else
        {
            BuildScene(catalog, recipes, log);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Farmhouse:\n - " + string.Join("\n - ", log));
        EditorUtility.DisplayDialog("Farmhouse ready",
            "Done. Stand in front of the front door and press Space to go in.\n\nSee the Console for what was created.", "OK");
    }

    // ------------------------------------------------------------------
    // Art import
    // ------------------------------------------------------------------

    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { artFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            SpriteAlignment align = SpriteAlignment.Center;
            if (file.StartsWith("house_ext_") || file == "stall") align = SpriteAlignment.BottomCenter;
            else if (file.StartsWith("fur_")) align = SpriteAlignment.BottomLeft;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;

            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.textureType = TextureImporterType.Sprite;
            ts.spriteMode = (int)SpriteImportMode.Single;
            ts.spriteAlignment = (int)align;
            ts.spritePixelsPerUnit = file == "backdrop" ? 4f : 16f;
            ts.filterMode = FilterMode.Point;
            ts.mipmapEnabled = false;
            imp.SetTextureSettings(ts);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    private static Sprite Spr(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(artFolder + "/" + file + ".png");
    }

    private static Dictionary<string, TileBase> MakeTiles()
    {
        var map = new Dictionary<string, TileBase>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { artFolder }))
        {
            string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (!file.StartsWith("floor_") && !file.StartsWith("wall_")) continue;

            string tilePath = DataRoot + "/Tiles/" + file + ".asset";
            var tile = LoadOrCreate<Tile>(tilePath);
            tile.sprite = Spr(file);
            EditorUtility.SetDirty(tile);
            map[file] = tile;
        }
        return map;
    }

    // ------------------------------------------------------------------
    // Items, recipes, catalog
    // ------------------------------------------------------------------

    private static ItemData Mat(string asset, string name, string desc, int buy, int sell, string icon, List<string> log)
    {
        var it = LoadOrCreate<ItemData>(ItemsFolder + "/" + asset + ".asset");
        it.itemName = name; it.description = desc; it.type = ItemData.ItemType.Resource;
        it.buyPrice = buy; it.sellPrice = sell; it.maxStack = 99; it.icon = Spr(icon);
        EditorUtility.SetDirty(it);
        return it;
    }

    private static ItemData Dish(string asset, string name, string desc, int hp, int mp, int sell, string icon)
    {
        var it = LoadOrCreate<ItemData>(ItemsFolder + "/" + asset + ".asset");
        it.itemName = name; it.description = desc; it.type = ItemData.ItemType.Food;
        it.energyRestored = hp; it.magicRestored = mp; it.sellPrice = sell; it.buyPrice = 0; it.maxStack = 20; it.icon = Spr(icon);
        EditorUtility.SetDirty(it);
        return it;
    }

    private static Dictionary<string, ItemData> MakeItems(List<string> log)
    {
        var d = new Dictionary<string, ItemData>();
        d["wood"] = Mat("WoodPlank", "Wood Plank", "Sturdy pine planks for floors, furniture and roofing.", 25, 8, "icon_wood_plank", log);
        d["paint"] = Mat("PaintBucket", "Paint Bucket", "Paint for walls and siding.", 30, 10, "icon_paint_bucket", log);
        d["fabric"] = Mat("FabricRoll", "Fabric Roll", "Soft cloth for bedding, rugs and decorations.", 35, 12, "icon_fabric_roll", log);
        d["stone"] = Mat("StoneBrick", "Stone Brick", "Bricks for tiles and kitchens.", 30, 10, "icon_stone_brick", log);
        d["glass"] = Mat("GlassPane", "Glass Pane", "Clear glass for lamps and appliances.", 40, 14, "icon_glass_pane", log);

        d["salad"] = Dish("GardenSalad", "Garden Salad", "Fresh and crunchy.", 25, 5, 160, "icon_dish_salad");
        d["stew"] = Dish("VeggieStew", "Veggie Stew", "Warm and filling.", 45, 15, 280, "icon_dish_stew");
        d["feast"] = Dish("FarmhouseFeast", "Farmhouse Feast", "Everything from the garden on one plate.", 80, 30, 550, "icon_dish_feast");

        log.Add("Created 5 building materials and 3 dishes (items).");
        return d;
    }

    private static List<Recipe> MakeRecipes(Dictionary<string, ItemData> items, List<string> log)
    {
        var list = new List<Recipe>();

        list.Add(MakeRecipe("GardenSalad", "Garden Salad", "A light meal from any two crops.", items["salad"], 1, 20, new[] { AnyCrop(2) }));
        list.Add(MakeRecipe("VeggieStew", "Veggie Stew", "A hearty stew from any three crops.", items["stew"], 1, 40, new[] { AnyCrop(3) }));
        list.Add(MakeRecipe("FarmhouseFeast", "Farmhouse Feast", "A big plate made from five crops.", items["feast"], 1, 60, new[] { AnyCrop(5) }));

        // One plate per crop the player can grow.
        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (crop == null || crop.harvestItem == null) continue;

            string baseName = crop.harvestItem.itemName;
            string asset = baseName.Replace(" ", "") + "Plate";
            ItemData plate = Dish(asset, baseName + " Plate", "A simple plate of " + baseName.ToLowerInvariant() + ".", 20, 5, 130, "icon_dish_plate");

            var ing = new Recipe.Ingredient { item = crop.harvestItem, anyCrop = false, count = 2 };
            list.Add(MakeRecipe(asset, baseName + " Plate", "Two " + baseName.ToLowerInvariant() + " cooked simply.", plate, 1, 25, new[] { ing }));
        }

        log.Add("Created " + list.Count + " recipes.");
        return list;
    }

    private static Recipe.Ingredient AnyCrop(int count)
    {
        return new Recipe.Ingredient { anyCrop = true, count = count };
    }

    private static Recipe MakeRecipe(string asset, string name, string desc, ItemData result, int count, int minutes, Recipe.Ingredient[] ingredients)
    {
        var r = LoadOrCreate<Recipe>(DataRoot + "/Recipes/" + asset + ".asset");
        r.recipeName = name; r.description = desc; r.result = result; r.resultCount = count; r.cookMinutes = minutes;
        r.ingredients = new List<Recipe.Ingredient>(ingredients);
        EditorUtility.SetDirty(r);
        return r;
    }

    private struct Cost { public string key; public int count; public Cost(string k, int c) { key = k; count = c; } }

    private static List<HouseUpgradeData> MakeCatalog(Dictionary<string, ItemData> items, Dictionary<string, TileBase> tiles, List<string> log)
    {
        var list = new List<HouseUpgradeData>();

        Cost Wood(int n) { return new Cost("wood", n); }
        Cost Paint(int n) { return new Cost("paint", n); }
        Cost Fabric(int n) { return new Cost("fabric", n); }
        Cost Stone(int n) { return new Cost("stone", n); }
        Cost Glass(int n) { return new Cost("glass", n); }

        void Add(HouseCategory cat, string id, string name, string desc, bool isDefault, int gold, Cost[] costs,
                 string sprite = null, string tile = null, string tileBase = null, Color? tint = null, float food = 0f)
        {
            var o = LoadOrCreate<HouseUpgradeData>(DataRoot + "/Upgrades/" + cat + "_" + id + ".asset");
            o.category = cat; o.displayName = name; o.description = desc; o.isDefault = isDefault; o.goldCost = gold;
            o.materials = new List<MaterialCost>();
            foreach (Cost c in costs) o.materials.Add(new MaterialCost { item = items[c.key], count = c.count });
            o.sprite = sprite != null ? Spr(sprite) : null;
            o.tile = tile != null ? tiles[tile] : null;
            o.tileBase = tileBase != null ? tiles[tileBase] : null;
            o.tint = tint ?? Color.white;
            o.foodBonus = food;
            EditorUtility.SetDirty(o);
            list.Add(o);
        }
        Cost[] None = new Cost[0];

        // ---- floors ----
        Add(HouseCategory.Floor, "Pine", "Pine Boards", "Light, warm floorboards.", true, 0, None, tile: "floor_pine");
        Add(HouseCategory.Floor, "Oak", "Oak Boards", "Rich, dark hardwood.", false, 120, new[] { Wood(8) }, tile: "floor_oak");
        Add(HouseCategory.Floor, "Slate", "Slate Tile", "Cool grey stone tile.", false, 200, new[] { Stone(10) }, tile: "floor_slate");
        Add(HouseCategory.Floor, "Checker", "Checker Tile", "Classic black and white kitchen checks.", false, 160, new[] { Stone(8) }, tile: "floor_checker");

        // ---- walls ----
        Add(HouseCategory.Wall, "Cream", "Cream Walls", "Soft cream paint with white skirting.", true, 0, None, tile: "wall_cream_top", tileBase: "wall_cream_base");
        Add(HouseCategory.Wall, "Sage", "Sage Walls", "A calm, leafy green.", false, 80, new[] { Paint(3) }, tile: "wall_sage_top", tileBase: "wall_sage_base");
        Add(HouseCategory.Wall, "Blue", "Blue Stripe", "Fresh blue and white stripes.", false, 100, new[] { Paint(3) }, tile: "wall_blue_top", tileBase: "wall_blue_base");
        Add(HouseCategory.Wall, "Brick", "Exposed Brick", "Rustic red brick.", false, 140, new[] { Stone(8) }, tile: "wall_brick_top", tileBase: "wall_brick_base");

        // ---- outside ----
        Add(HouseCategory.Siding, "White", "White Siding", "Crisp white board-and-batten.", true, 0, None, tint: Color.white);
        Add(HouseCategory.Siding, "Sage", "Sage Siding", "Soft green siding.", false, 150, new[] { Paint(4) }, tint: new Color(0.78f, 0.88f, 0.78f));
        Add(HouseCategory.Siding, "BarnRed", "Barn Red Siding", "A bold traditional barn red.", false, 150, new[] { Paint(4) }, tint: new Color(0.85f, 0.45f, 0.42f));
        Add(HouseCategory.Siding, "Charcoal", "Charcoal Siding", "Modern dark grey.", false, 180, new[] { Paint(5) }, tint: new Color(0.42f, 0.44f, 0.48f));

        Add(HouseCategory.Roof, "Charcoal", "Charcoal Metal Roof", "Black-grey standing-seam metal.", true, 0, None, tint: new Color(0.30f, 0.32f, 0.36f));
        Add(HouseCategory.Roof, "Rust", "Rust Red Roof", "Warm terracotta red.", false, 200, new[] { Wood(6) }, tint: new Color(0.75f, 0.35f, 0.25f));
        Add(HouseCategory.Roof, "Forest", "Forest Green Roof", "Deep green, blends into the farm.", false, 200, new[] { Wood(6) }, tint: new Color(0.25f, 0.50f, 0.32f));
        Add(HouseCategory.Roof, "SlateBlue", "Slate Blue Roof", "Cool blue-grey.", false, 220, new[] { Wood(6) }, tint: new Color(0.30f, 0.42f, 0.62f));

        // ---- furniture ----
        Add(HouseCategory.Bed, "Plain", "Plain Bed", "A simple bed with a green blanket.", true, 0, None, sprite: "fur_bed_plain");
        Add(HouseCategory.Bed, "Cozy", "Cozy Quilted Bed", "A blue quilt that's hard to leave.", false, 250, new[] { Fabric(6), Wood(4) }, sprite: "fur_bed_cozy");

        Add(HouseCategory.Kitchen, "Iron", "Cast Iron Stove", "Wood-fired and reliable.", true, 0, None, sprite: "fur_stove_iron");
        Add(HouseCategory.Kitchen, "Modern", "Modern Range", "A 25% chance of an extra serving every time you cook.", false, 400, new[] { Stone(8), Glass(2) }, sprite: "fur_stove_modern", food: 0.25f);

        Add(HouseCategory.Table, "Plain", "Wooden Table", "A sturdy table with a fruit bowl.", true, 0, None, sprite: "fur_table_plain");
        Add(HouseCategory.Table, "Farm", "Farmhouse Table", "White painted base, natural top.", false, 180, new[] { Wood(6) }, sprite: "fur_table_farm");

        Add(HouseCategory.Rug, "None", "No Rug", "Bare floor.", true, 0, None);
        Add(HouseCategory.Rug, "Cream", "Cream Rug", "A soft round rug.", false, 90, new[] { Fabric(3) }, sprite: "fur_rug_cream");
        Add(HouseCategory.Rug, "Red", "Red Rug", "A bold red rug.", false, 90, new[] { Fabric(3) }, sprite: "fur_rug_red");

        Add(HouseCategory.Bookshelf, "None", "No Bookshelf", "", true, 0, None);
        Add(HouseCategory.Bookshelf, "Oak", "Oak Bookshelf", "Warm wood, full of books.", false, 160, new[] { Wood(6) }, sprite: "fur_bookshelf_oak");
        Add(HouseCategory.Bookshelf, "White", "White Bookshelf", "Painted white.", false, 180, new[] { Wood(6), Paint(1) }, sprite: "fur_bookshelf_white");

        Add(HouseCategory.Plant, "None", "No Plant", "", true, 0, None);
        Add(HouseCategory.Plant, "Small", "Potted Plant", "A little green friend.", false, 40, None, sprite: "fur_plant_small");
        Add(HouseCategory.Plant, "Fern", "Big Fern", "A leafy fern in a white pot.", false, 80, None, sprite: "fur_plant_fern");

        Add(HouseCategory.Lamp, "None", "No Lamp", "", true, 0, None);
        Add(HouseCategory.Lamp, "Brass", "Brass Lamp", "A warm glow.", false, 90, new[] { Glass(1) }, sprite: "fur_lamp_brass");
        Add(HouseCategory.Lamp, "Black", "Black Lamp", "Sleek and modern.", false, 110, new[] { Glass(1) }, sprite: "fur_lamp_black");

        Add(HouseCategory.WallArt, "None", "No Wall Art", "", true, 0, None);
        Add(HouseCategory.WallArt, "Landscape", "Landscape Painting", "A view of rolling hills.", false, 120, new[] { Wood(2) }, sprite: "fur_art_landscape");
        Add(HouseCategory.WallArt, "Wreath", "Wreath", "A leafy wreath with berries.", false, 70, new[] { Fabric(2) }, sprite: "fur_art_wreath");

        log.Add("Created " + list.Count + " house options (HouseUpgradeData). Edit prices and materials on the assets in " + DataRoot + "/Upgrades.");
        return list;
    }

    private static List<ItemData> ShopStock()
    {
        var stock = new List<ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it != null && it.buyPrice > 0 && (it.type == ItemData.ItemType.Seed || it.type == ItemData.ItemType.Resource)) stock.Add(it);
        }
        stock.Sort((a, b) =>
        {
            int t = a.type.CompareTo(b.type);
            return t != 0 ? t : string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase);
        });
        return stock;
    }

    // ------------------------------------------------------------------
    // Scene
    // ------------------------------------------------------------------

    private class Slot
    {
        public HouseCategory category; public string name; public int x, y; public int order;
        public Vector2 colSize, colOffset;      // colSize zero = no collider
        public string kind;                     // "", "bed", "kitchen"
        public Slot(HouseCategory c, string n, int x, int y, int order, Vector2 cs, Vector2 co, string kind = "")
        { category = c; name = n; this.x = x; this.y = y; this.order = order; colSize = cs; colOffset = co; this.kind = kind; }
    }

    private static void BuildScene(List<HouseUpgradeData> catalog, List<Recipe> recipes, List<string> log)
    {
        // ---- where to put the house: the middle of the Scene view, snapped to the tile grid ----
        Vector3 origin = Vector3.zero;
        if (SceneView.lastActiveSceneView != null) origin = SceneView.lastActiveSceneView.pivot;
        else
        {
            var p = Object.FindAnyObjectByType<PlayerController>();
            if (p != null) origin = p.transform.position + new Vector3(0f, 5f, 0f);
        }
        origin = new Vector3(Mathf.Round(origin.x), Mathf.Round(origin.y), 0f);

        // ---------------- exterior ----------------
        var house = new GameObject("Farmhouse");
        Undo.RegisterCreatedObjectUndo(house, "Create Farmhouse");
        house.transform.position = origin;

        SpriteRenderer walls = Layer(house, "Walls", "house_ext_walls", 6);
        SpriteRenderer roof = Layer(house, "Roof", "house_ext_roof", 7);
        Layer(house, "Details", "house_ext_details", 8);

        var body = house.AddComponent<BoxCollider2D>();      // the building is solid (8 x 4 tiles)
        body.size = new Vector2(7.6f, 3.4f);
        body.offset = new Vector2(0f, 1.9f);

        // ---------------- interior ----------------
        var room = new GameObject("HouseInterior");
        Undo.RegisterCreatedObjectUndo(room, "Create HouseInterior");
        room.transform.position = new Vector3(500f, 0f, 0f);       // far from the farm
        room.AddComponent<Grid>();
        var interior = room.AddComponent<HouseInterior>();
        const int W = 10, FLOOR = 5, WALLROWS = 2;

        var back = Child(room, "Backdrop", new Vector3(W * 0.5f, (FLOOR + WALLROWS) * 0.5f, 0f));
        var backSr = back.AddComponent<SpriteRenderer>();
        backSr.sprite = Spr("backdrop"); backSr.sortingOrder = -5;
        back.transform.localScale = new Vector3(60f, 40f, 1f);

        var floorGo = Child(room, "Floor", Vector3.zero);
        var floorMap = floorGo.AddComponent<Tilemap>(); floorGo.AddComponent<TilemapRenderer>().sortingOrder = 0;
        var wallGo = Child(room, "Wall", Vector3.zero);
        var wallMap = wallGo.AddComponent<Tilemap>(); wallGo.AddComponent<TilemapRenderer>().sortingOrder = 1;

        // invisible room edges so the player stays inside
        var edges = Child(room, "Edges", Vector3.zero);
        Box(edges, new Vector2(-0.5f, (FLOOR + WALLROWS) * 0.5f), new Vector2(1f, FLOOR + WALLROWS + 2f), false);
        Box(edges, new Vector2(W + 0.5f, (FLOOR + WALLROWS) * 0.5f), new Vector2(1f, FLOOR + WALLROWS + 2f), false);
        Box(edges, new Vector2(W * 0.5f, -0.5f), new Vector2(W + 2f, 1f), false);
        Box(edges, new Vector2(W * 0.5f, FLOOR + WALLROWS * 0.5f), new Vector2(W, WALLROWS), false);

        // spawn point and exit door
        var entry = Child(room, "EntrySpawn", new Vector3(5.5f, 1.2f, 0f));
        var mat = Child(room, "Doormat", new Vector3(5f, 0f, 0f));
        var matSr = mat.AddComponent<SpriteRenderer>(); matSr.sprite = Spr("fur_doormat"); matSr.sortingOrder = 2;

        var exitDoor = Child(room, "ExitDoor", new Vector3(5.5f, 0f, 0f));
        Box(exitDoor, Vector2.zero, new Vector2(1.8f, 1.2f), true);
        var exitComp = exitDoor.AddComponent<HouseDoor>();

        // ---- furniture slots ----
        var slotSpecs = new List<Slot>
        {
            new Slot(HouseCategory.Rug,       "Slot_Rug",       3, 0, 3, Vector2.zero, Vector2.zero),
            new Slot(HouseCategory.Table,     "Slot_Table",     3, 1, 5, new Vector2(1.8f, 1.0f), new Vector2(1f, 0.9f)),
            new Slot(HouseCategory.Bed,       "Slot_Bed",       1, 3, 5, new Vector2(2f, 1.6f), new Vector2(1f, 0.8f), "bed"),
            new Slot(HouseCategory.Lamp,      "Slot_Lamp",      3, 3, 5, new Vector2(0.6f, 0.4f), new Vector2(0.5f, 0.3f)),
            new Slot(HouseCategory.Bookshelf, "Slot_Bookshelf", 4, 3, 5, new Vector2(1f, 1.2f), new Vector2(0.5f, 0.6f)),
            new Slot(HouseCategory.WallArt,   "Slot_WallArt",   5, 5, 4, Vector2.zero, Vector2.zero),
            new Slot(HouseCategory.Kitchen,   "Slot_Kitchen",   7, 3, 5, new Vector2(2f, 1.6f), new Vector2(1f, 0.8f), "kitchen"),
            new Slot(HouseCategory.Plant,     "Slot_Plant",     9, 3, 5, new Vector2(0.7f, 0.5f), new Vector2(0.5f, 0.3f)),
        };

        var slotList = new List<(HouseCategory, SpriteRenderer)>();
        foreach (Slot s in slotSpecs)
        {
            var go = Child(room, s.name, new Vector3(s.x, s.y, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = s.order;
            if (s.colSize != Vector2.zero) Box(go, s.colOffset, s.colSize, false);

            if (s.kind == "bed")
            {
                var wake = Child(go, "WakePoint", new Vector3(1.5f, -0.6f, 0f));
                var bed = go.AddComponent<Bed>();
                SetRef(bed, "wakePoint", wake.transform);
                SetRef(bed, "interior", interior);
            }
            else if (s.kind == "kitchen")
            {
                var cook = go.AddComponent<CookingStation>();
                SetArray(cook, "recipes", recipes.Cast<Object>().ToList());
            }
            slotList.Add((s.category, sr));
        }

        // design table (not customizable, it's the thing you use to customize)
        var design = Child(room, "DesignTable", new Vector3(7f, 0f, 0f));
        var designSr = design.AddComponent<SpriteRenderer>(); designSr.sprite = Spr("fur_designtable"); designSr.sortingOrder = 5;
        Box(design, new Vector2(1f, 0.9f), new Vector2(1.8f, 1.2f), false);
        design.AddComponent<HouseCustomizer>();

        // wire the interior component
        var so = new SerializedObject(interior);
        so.FindProperty("width").intValue = W;
        so.FindProperty("floorRows").intValue = FLOOR;
        so.FindProperty("wallRows").intValue = WALLROWS;
        so.FindProperty("floorTilemap").objectReferenceValue = floorMap;
        so.FindProperty("wallTilemap").objectReferenceValue = wallMap;
        SerializedProperty slots = so.FindProperty("slots");
        slots.arraySize = slotList.Count;
        for (int i = 0; i < slotList.Count; i++)
        {
            SerializedProperty e = slots.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("category").enumValueIndex = (int)slotList[i].Item1;
            e.FindPropertyRelative("renderer").objectReferenceValue = slotList[i].Item2;
        }
        so.ApplyModifiedProperties();

        // ---------------- doors between outside and inside ----------------
        var frontDoor = Child(house, "FrontDoor", new Vector3(0f, 0.1f, 0f));
        Box(frontDoor, Vector2.zero, new Vector2(1.8f, 1.8f), true);
        var frontSpot = Child(house, "ExitSpot", new Vector3(0f, -0.7f, 0f));
        var frontComp = frontDoor.AddComponent<HouseDoor>();
        SetRef(frontComp, "destination", entry.transform);
        SetRef(frontComp, "enterInterior", interior);
        SetString(frontComp, "prompt", "Enter house");

        SetRef(exitComp, "destination", frontSpot.transform);
        SetString(exitComp, "prompt", "Go outside");

        // ---------------- manager ----------------
        var mgrGo = new GameObject("HouseManager");
        Undo.RegisterCreatedObjectUndo(mgrGo, "Create HouseManager");
        var mgr = mgrGo.AddComponent<HouseManager>();
        SetArray(mgr, "catalog", catalog.Cast<Object>().ToList());
        SetRef(mgr, "sidingRenderer", walls);
        SetRef(mgr, "roofRenderer", roof);
        SetRef(mgr, "interior", interior);

        // show the starting look in the editor too
        foreach (HouseUpgradeData o in catalog)
        {
            if (!o.isDefault) continue;
            switch (o.category)
            {
                case HouseCategory.Siding: walls.color = o.tint; break;
                case HouseCategory.Roof: roof.color = o.tint; break;
                case HouseCategory.Floor: interior.PaintFloor(o.tile); break;
                case HouseCategory.Wall: interior.PaintWall(o.tile, o.tileBase); break;
                default: interior.SetFurniture(o.category, o.sprite); break;
            }
        }

        // ---------------- general store stall ----------------
        var stall = new GameObject("GeneralStore");
        Undo.RegisterCreatedObjectUndo(stall, "Create GeneralStore");
        stall.transform.position = origin + new Vector3(7f, 0f, 0f);
        var stallSr = stall.AddComponent<SpriteRenderer>(); stallSr.sprite = Spr("stall"); stallSr.sortingOrder = 6;
        Box(stall, new Vector2(0f, 0.7f), new Vector2(2.8f, 1.2f), false);
        var shop = stall.AddComponent<ShopCounter>();
        SetArray(shop, "stock", ShopStock().Cast<Object>().ToList());

        // ---------------- clean up the earlier placeholder bed (the house has a real one now) ----------------
        foreach (Bed b in Object.FindObjectsByType<Bed>(FindObjectsInactive.Include))
        {
            if (b.transform.IsChildOf(room.transform)) continue;
            Undo.DestroyObjectImmediate(b.gameObject);
            log.Add("Removed the old placeholder Bed (the house has one inside now).");
        }

        log.Add("Built the farmhouse at " + origin + " (8 x 4 tiles). Its inside is at x = 500, far from the farm.");
        log.Add("Added a General Store stall next to the house. Stock is every seed and material that has a Buy Price.");
        log.Add("If you have another farmhouse prop from the Kiln Harvest layout, delete it or move the new house.");
        Selection.activeGameObject = house;
    }

    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    private static SpriteRenderer Layer(GameObject parent, string name, string sprite, int order)
    {
        var go = Child(parent, name, Vector3.zero);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(sprite);
        sr.sortingOrder = order;
        return sr;
    }

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
        c.offset = offset;
        c.size = size;
        c.isTrigger = trigger;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Field not found: " + field + " on " + target.GetType().Name); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.stringValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetArray(Object target, string field, List<Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Field not found: " + field + " on " + target.GetType().Name); return; }
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
