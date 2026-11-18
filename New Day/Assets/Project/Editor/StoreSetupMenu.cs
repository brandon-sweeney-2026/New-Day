using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > General Store         (copy the 'Store' art folder into Assets first)
/// Farm > Set Up > Rebuild Selected Interior   (after changing size, zones, walls or blockers on an interior)
///
/// Build General Store imports the art and makes: 20 food and drink items, the store prop prefabs (shelves, coolers, animated slush machines,
/// checkout, seed rack...), the apartment furniture prefabs, then builds in the OPEN scene:
///   - the two-storey store building (8 x 5 tiles solid, the upper floors drawn above so you can walk behind),
///   - the store inside (20 x 20, far away from the farm),
///   - the shopkeepers' apartment upstairs (20 x 20: 3 bedrooms, 2 bathrooms, kitchen and living room),
///   - doors: street <-> store <-> stairs <-> apartment.
/// Each shelf, cooler and slush machine opens its own little shop. The checkout sells everything.
/// Safe to run again (assets are reused); delete 'SuburbStore', 'StoreInterior' and 'ApartmentInterior' first to rebuild the scene part.
/// This file must live in a folder named "Editor".
/// </summary>
public static class StoreSetupMenu
{
    private const string Root = "Assets/Farm/Suburb";
    private const string ItemsFolder = "Assets/Farm/Data/Items";
    private static string art;

    // ------------------------------------------------------------------ definitions
    private class Food { public string key, name, desc; public int buy, hp, mp; }
    private static readonly Food[] Foods =
    {
        new Food { key="ramen_cup", name="Instant Ramen Cup", desc="Just add hot water. Instant comfort.", buy=18, hp=15 },
        new Food { key="kimchi_jar", name="Kimchi Jar", desc="Crunchy, tangy and a little spicy. Made fresh by the shop.", buy=30, hp=25, mp=5 },
        new Food { key="rice_triangle", name="Triangle Rice Ball", desc="Rice wrapped in crisp seaweed.", buy=14, hp=12 },
        new Food { key="seaweed_snack", name="Seaweed Snack", desc="Thin, salty and crunchy.", buy=10, hp=8, mp=2 },
        new Food { key="rice_cracker", name="Rice Cracker", desc="Light and crispy.", buy=8, hp=6 },
        new Food { key="honey_pastry", name="Honey Pastry", desc="Flaky and sweet.", buy=16, hp=14 },
        new Food { key="tuna_can", name="Canned Tuna", desc="Keeps forever.", buy=12, hp=12 },
        new Food { key="pickled_radish", name="Pickled Radish", desc="Sweet and sour yellow radish.", buy=14, hp=10 },
        new Food { key="instant_rice", name="Instant Rice", desc="Hot rice in two minutes.", buy=12, hp=14 },
        new Food { key="chili_paste", name="Chili Paste", desc="Fiery. Wakes up your magic.", buy=20, hp=8, mp=10 },
        new Food { key="barley_tea", name="Iced Barley Tea", desc="Nutty and refreshing.", buy=12, hp=5, mp=12 },
        new Food { key="citrus_soda", name="Citrus Soda", desc="Fizzy and bright.", buy=10, hp=4, mp=6 },
        new Food { key="melon_milk", name="Melon Milk", desc="Creamy and sweet.", buy=14, hp=10, mp=8 },
        new Food { key="sports_drink", name="Sports Drink", desc="Gets your magic flowing.", buy=16, hp=8, mp=14 },
        new Food { key="milk_tea", name="Milk Tea", desc="Cold, sweet and smooth.", buy=14, hp=6, mp=10 },
        new Food { key="peach_juice", name="Peach Juice", desc="Tastes like summer.", buy=12, hp=8, mp=6 },
        new Food { key="ice_cream_bar", name="Ice Cream Bar", desc="A frosty treat.", buy=15, hp=10, mp=4 },
        new Food { key="frozen_dumplings", name="Frozen Dumplings", desc="Boil, steam or fry.", buy=28, hp=30 },
        new Food { key="slush_red", name="Cherry Slush", desc="Brain-freeze red.", buy=20, hp=5, mp=18 },
        new Food { key="slush_blue", name="Blue Raspberry Slush", desc="Blue tongue guaranteed.", buy=20, hp=5, mp=18 },
        new Food { key="slush_green", name="Lime Slush", desc="Sour and sharp.", buy=20, hp=5, mp=18 },
    };

    private class Prop
    {
        public string name, sprite;           // prefab name, picture file
        public Vector2 col;                   // solid base size in tiles (0 = no collider)
        public int order = 5;
        public string[] stock;                // food keys it sells (null = not a shop)
        public string shop;                   // shop title
        public string flipbook;               // sliced sheet name for animation
        public string[] flavor;               // lines for StoreFlavor
        public bool seeds, everything;        // sells seeds / everything
        public bool flat;                     // drawn flat on the floor (rugs, mats)
    }

    private static readonly Prop[] Props =
    {
        new Prop { name="Store_ShelfSnacks", sprite="shelf_snacks", col=new Vector2(2.8f,0.8f), stock=new[]{"seaweed_snack","rice_cracker","honey_pastry","rice_triangle"}, shop="Snack Shelf" },
        new Prop { name="Store_ShelfPantry", sprite="shelf_pantry", col=new Vector2(2.8f,0.8f), stock=new[]{"tuna_can","pickled_radish","instant_rice","chili_paste"}, shop="Pantry Shelf" },
        new Prop { name="Store_ShelfNoodles", sprite="shelf_noodles", col=new Vector2(2.8f,0.8f), stock=new[]{"ramen_cup","instant_rice","chili_paste"}, shop="Noodle Shelf" },
        new Prop { name="Store_CoolerDrinks", sprite="cooler_drinks", col=new Vector2(2.8f,0.9f), stock=new[]{"citrus_soda","sports_drink","barley_tea","peach_juice"}, shop="Drinks Cooler" },
        new Prop { name="Store_CoolerDairy", sprite="cooler_dairy", col=new Vector2(2.8f,0.9f), stock=new[]{"melon_milk","milk_tea"}, shop="Dairy Cooler" },
        new Prop { name="Store_CoolerBanchan", sprite="cooler_banchan", col=new Vector2(2.8f,0.9f), stock=new[]{"kimchi_jar","pickled_radish","rice_triangle"}, shop="Fresh Fridge" },
        new Prop { name="Store_FreezerChest", sprite="freezer_chest", col=new Vector2(2.8f,0.9f), stock=new[]{"ice_cream_bar","frozen_dumplings"}, shop="Freezer" },
        new Prop { name="Store_SlushRed", sprite="slush_red", col=new Vector2(1.8f,0.8f), stock=new[]{"slush_red"}, shop="Slush Machine", flipbook="slush_red" },
        new Prop { name="Store_SlushBlue", sprite="slush_blue", col=new Vector2(1.8f,0.8f), stock=new[]{"slush_blue"}, shop="Slush Machine", flipbook="slush_blue" },
        new Prop { name="Store_SlushGreen", sprite="slush_green", col=new Vector2(1.8f,0.8f), stock=new[]{"slush_green"}, shop="Slush Machine", flipbook="slush_green" },
        new Prop { name="Store_SeedRack", sprite="store_seed_rack", col=new Vector2(1.8f,0.8f), seeds=true, shop="Seed Rack" },
        new Prop { name="Store_Checkout", sprite="store_counter", col=new Vector2(3.8f,1.0f), everything=true, shop="Checkout" },
        new Prop { name="Store_ProduceBin", sprite="store_produce_bin", col=new Vector2(2.3f,0.7f), flavor=new[]{"Fresh produce from local farms. (Sell yours at the shipping bin!)","The apples look perfect. They're display only."} },
        new Prop { name="Store_Baskets", sprite="store_baskets", col=Vector2.zero, flavor=new[]{"Shopping baskets. You don't need one."} },
        new Prop { name="Store_Trash", sprite="store_trash", col=new Vector2(0.8f,0.5f), flavor=new[]{"A trash can."} },
        new Prop { name="Store_Stairs", sprite="store_stairs", col=new Vector2(1.9f,0.5f) },
        new Prop { name="Store_PosterA", sprite="store_poster_a", col=Vector2.zero, order=3, flat=true },
        new Prop { name="Store_PosterB", sprite="store_poster_b", col=Vector2.zero, order=3, flat=true },
        new Prop { name="Store_Doormat", sprite="store_doormat", col=Vector2.zero, order=2, flat=true },
        // ---- apartment ----
        new Prop { name="Apt_Sofa", sprite="apt_sofa", col=new Vector2(2.8f,0.9f) },
        new Prop { name="Apt_Armchair", sprite="apt_armchair", col=new Vector2(1.5f,0.7f) },
        new Prop { name="Apt_CoffeeTable", sprite="apt_coffee_table", col=new Vector2(2.1f,0.7f) },
        new Prop { name="Apt_TvStand", sprite="apt_tv_stand", col=new Vector2(2.5f,0.8f) },
        new Prop { name="Apt_RugLiving", sprite="apt_rug_living", col=Vector2.zero, order=2, flat=true },
        new Prop { name="Apt_Bookshelf", sprite="apt_bookshelf", col=new Vector2(1.6f,0.7f) },
        new Prop { name="Apt_Lamp", sprite="apt_lamp", col=new Vector2(0.5f,0.4f) },
        new Prop { name="Apt_Plant", sprite="apt_plant", col=new Vector2(0.8f,0.5f) },
        new Prop { name="Apt_CounterCab", sprite="apt_counter_cab", col=new Vector2(1.9f,1.0f) },
        new Prop { name="Apt_CounterSink", sprite="apt_counter_sink", col=new Vector2(1.9f,1.0f) },
        new Prop { name="Apt_CounterStove", sprite="apt_counter_stove", col=new Vector2(1.9f,1.0f), flavor=new[]{"The stove is spotless. Somebody cooks here every day."} },
        new Prop { name="Apt_Fridge", sprite="apt_fridge", col=new Vector2(1.5f,0.9f), flavor=new[]{"Kimchi, kimchi and more kimchi."} },
        new Prop { name="Apt_DiningTable", sprite="apt_dining_table", col=new Vector2(2.5f,0.8f) },
        new Prop { name="Apt_Chair", sprite="apt_chair", col=new Vector2(0.7f,0.4f) },
        new Prop { name="Apt_BedDouble", sprite="apt_bed_double", col=new Vector2(2.9f,1.6f) },
        new Prop { name="Apt_BedBlue", sprite="apt_bed_single_blue", col=new Vector2(1.9f,1.6f) },
        new Prop { name="Apt_BedGreen", sprite="apt_bed_single_green", col=new Vector2(1.9f,1.6f) },
        new Prop { name="Apt_Nightstand", sprite="apt_nightstand", col=new Vector2(0.9f,0.5f) },
        new Prop { name="Apt_Wardrobe", sprite="apt_wardrobe", col=new Vector2(2.0f,0.8f) },
        new Prop { name="Apt_Desk", sprite="apt_desk", col=new Vector2(2.4f,0.9f) },
        new Prop { name="Apt_Toilet", sprite="apt_toilet", col=new Vector2(0.9f,0.7f) },
        new Prop { name="Apt_BathSink", sprite="apt_bath_sink", col=new Vector2(1.4f,0.7f) },
        new Prop { name="Apt_Shower", sprite="apt_shower", col=new Vector2(1.8f,1.0f) },
        new Prop { name="Apt_Bathtub", sprite="apt_bathtub", col=new Vector2(2.6f,1.0f) },
        new Prop { name="Apt_Console", sprite="apt_console", col=new Vector2(2.0f,0.8f) },
        new Prop { name="Apt_ShoeRack", sprite="apt_shoe_rack", col=new Vector2(1.2f,0.6f) },
        new Prop { name="Apt_Window", sprite="wall_window", col=Vector2.zero, order=3, flat=true },
    };

    // ------------------------------------------------------------------ entry
    [MenuItem("Farm/Set Up/General Store", false, 145)]
    public static void Run()
    {
        art = FindFolder("store_exterior");
        if (art == null)
        {
            EditorUtility.DisplayDialog("Store art not found", "Couldn't find store_exterior.png.\nCopy the 'Store' art folder into Assets (e.g. Assets/Art/Store) and run this again.", "OK");
            return;
        }
        EnsureFolder(Root + "/Data/Tiles");
        EnsureFolder(Root + "/Prefabs");
        EnsureFolder(ItemsFolder);

        ImportArt();
        var tiles = MakeTiles();
        var foods = MakeFoods();
        var props = MakeProps(foods);
        AssetDatabase.SaveAssets();

        if (GameObject.Find("SuburbStore") != null || GameObject.Find("StoreInterior") != null)
        {
            EditorUtility.DisplayDialog("Store already in this scene",
                "Assets were refreshed (items, tiles, prefabs). To rebuild the buildings, delete 'SuburbStore', 'StoreInterior' and 'ApartmentInterior' from the scene first.", "OK");
            return;
        }
        BuildScene(tiles, props);
    }

    // ------------------------------------------------------------------ scene
    private static void BuildScene(Dictionary<string, TileBase> tiles, Dictionary<string, GameObject> props)
    {
        Vector3 origin = Vector3.zero;
        if (SceneView.lastActiveSceneView != null) origin = SceneView.lastActiveSceneView.pivot;
        origin = new Vector3(Mathf.Round(origin.x), Mathf.Round(origin.y), 0f);

        float nextX = 600f;
        foreach (HouseInterior h in Object.FindObjectsByType<HouseInterior>()) nextX = Mathf.Max(nextX, h.transform.position.x + 100f);

        // ---------- exterior ----------
        var ext = new GameObject("SuburbStore");
        Undo.RegisterCreatedObjectUndo(ext, "Build general store");
        ext.transform.position = origin;
        var grp = ext.AddComponent<UnityEngine.Rendering.SortingGroup>(); grp.sortingOrder = 10;
        ext.AddComponent<BehindPlayerSorter>();
        var art1 = new GameObject("Building"); art1.transform.SetParent(ext.transform, false);
        art1.AddComponent<SpriteRenderer>().sprite = Spr("store_exterior");
        // only the bottom 8 x 5 tiles are solid, the upper floor can be walked behind
        var body = ext.AddComponent<BoxCollider2D>();
        body.size = new Vector2(8f, 5f); body.offset = new Vector2(0f, 2.5f);
        var frontDoor = Child(ext, "FrontDoor", new Vector3(0f, 0.2f, 0f));
        Box(frontDoor, Vector2.zero, new Vector2(1.8f, 1.6f), true);
        var exitSpot = Child(ext, "ExitSpot", new Vector3(0f, -0.9f, 0f));
        var sp1 = Child(ext, "Spot_StoreFront", new Vector3(0f, -1.6f, 0f)); Spot(sp1, "StoreFront", Vector2.up);

        // ---------- store interior ----------
        GameObject store = BuildRoom("StoreInterior", new Vector3(nextX, 0f, 0f), tiles["floor_store"], tiles["wall_store_upper"], tiles["wall_store_base"], tiles["partition_apt"], out HouseInterior storeInt, out InteriorStyle storeStyle);
        Place(props, store, "Store_Doormat", 10.5f, 0.6f);
        var storeEntry = Child(store, "EntrySpawn", new Vector3(10.5f, 1.4f, 0f));
        var storeExit = Child(store, "ExitDoor", new Vector3(10.5f, 0f, 0f));
        Box(storeExit, Vector2.zero, new Vector2(1.8f, 1.2f), true);
        var storeExitDoor = storeExit.AddComponent<HouseDoor>();

        // back wall: coolers, slush machines, stairs
        Place(props, store, "Store_CoolerDrinks", 2.5f, 17f); Place(props, store, "Store_CoolerDairy", 5.5f, 17f); Place(props, store, "Store_CoolerBanchan", 8.5f, 17f);
        Place(props, store, "Store_SlushRed", 11.5f, 17.2f); Place(props, store, "Store_SlushBlue", 13.5f, 17.2f); Place(props, store, "Store_SlushGreen", 15.5f, 17.2f);
        GameObject stairs = Place(props, store, "Store_Stairs", 18.5f, 17f);
        var stairsDoor = Child(store, "StairsDoor", new Vector3(18.5f, 16.6f, 0f));
        Box(stairsDoor, Vector2.zero, new Vector2(1.8f, 1.2f), true);
        var stairsComp = stairsDoor.AddComponent<HouseDoor>();
        var stairsLanding = Child(store, "StairsLanding", new Vector3(18.5f, 15.2f, 0f));
        // posters above the coolers
        Place(props, store, "Store_PosterA", 2.5f, 21.4f); Place(props, store, "Store_PosterB", 5.5f, 21.4f); Place(props, store, "Store_PosterA", 8.5f, 21.4f);
        Place(props, store, "Store_PosterB", 12.5f, 21.4f); Place(props, store, "Store_PosterA", 15.5f, 21.4f);
        // aisles (the middle stays clear, x 9 - 13)
        Place(props, store, "Store_ShelfSnacks", 3.5f, 13f); Place(props, store, "Store_ShelfPantry", 7f, 13f);
        Place(props, store, "Store_FreezerChest", 14.5f, 13f); Place(props, store, "Store_SeedRack", 18f, 12.5f);
        Place(props, store, "Store_ShelfNoodles", 3.5f, 8.5f); Place(props, store, "Store_ShelfSnacks", 7f, 8.5f);
        Place(props, store, "Store_ProduceBin", 15.5f, 8.5f); Place(props, store, "Store_ProduceBin", 18.3f, 8.5f);
        // front: checkout
        Place(props, store, "Store_Checkout", 16.5f, 2.2f); Place(props, store, "Store_Baskets", 12.8f, 2.6f); Place(props, store, "Store_Trash", 19.2f, 0.6f);

        // spots for the shopkeepers
        Spot(Child(store, "Spot_Checkout", new Vector3(16.5f, 4.6f, 0f)), "StoreCheckout", Vector2.down);
        Spot(Child(store, "Spot_StoreDoor", new Vector3(10.5f, 2.2f, 0f)), "StoreDoorInside", Vector2.down);
        Spot(Child(store, "Spot_Stairs", stairsLanding.transform.localPosition), "StoreStairs", Vector2.up);
        Spot(Child(store, "Spot_Aisle1", new Vector3(11f, 15f, 0f)), "StoreAisleBack", Vector2.up);
        Spot(Child(store, "Spot_Aisle2", new Vector3(11f, 10.5f, 0f)), "StoreAisleMid", Vector2.left);
        Spot(Child(store, "Spot_Slush", new Vector3(13.5f, 15.4f, 0f)), "StoreSlush", Vector2.up);
        Spot(Child(store, "Spot_Seeds", new Vector3(16f, 11f, 0f)), "StoreSeeds", Vector2.right);

        // ---------- apartment ----------
        GameObject apt = BuildRoom("ApartmentInterior", new Vector3(nextX + 100f, 0f, 0f), tiles["floor_apt_wood"], tiles["wall_apt_upper"], tiles["wall_apt_base"], tiles["partition_apt"], out HouseInterior aptInt, out InteriorStyle aptStyle);
        BuildApartment(apt, aptStyle, tiles, props);
        var aptEntry = Child(apt, "EntrySpawn", new Vector3(10.5f, 1.4f, 0f));
        var aptExit = Child(apt, "ExitDoor", new Vector3(10.5f, 0f, 0f));
        Box(aptExit, Vector2.zero, new Vector2(1.8f, 1.2f), true);
        var aptExitDoor = aptExit.AddComponent<HouseDoor>();
        Place(props, apt, "Store_Doormat", 10.5f, 0.6f);

        // ---------- doors ----------
        SetRef(frontDoor.AddComponent<HouseDoor>(), "destination", storeEntry.transform);
        SetRef(frontDoor.GetComponent<HouseDoor>(), "enterInterior", storeInt);
        SetString(frontDoor.GetComponent<HouseDoor>(), "prompt", "Enter store");
        SetRef(storeExitDoor, "destination", exitSpot.transform);
        SetString(storeExitDoor, "prompt", "Go outside");
        SetRef(stairsComp, "destination", aptEntry.transform);
        SetRef(stairsComp, "enterInterior", aptInt);
        SetString(stairsComp, "prompt", "Go upstairs");
        SetRef(aptExitDoor, "destination", stairsLanding.transform);
        SetRef(aptExitDoor, "enterInterior", storeInt);
        SetString(aptExitDoor, "prompt", "Go downstairs");

        storeStyle.Apply(); aptStyle.Apply();
        if (InteriorDecorSetupMenu.Ready()) Debug.Log(InteriorDecorTools.Furnish(aptInt, "KimApartment", "modern", InteriorDecorLayouts.KimApartment, false));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = ext;
        EditorUtility.DisplayDialog("General store ready",
            "Built the store at your Scene view's centre, its inside at x = " + nextX + " and the apartment at x = " + (nextX + 100f) + ".\n\n" +
            "Walk to the front door and press Space.\n\n" +
            "To reshape the store: change Width / Floor Rows on 'StoreInterior' (HouseInterior), add Interior Blocker objects to cut corners, then Farm > Set Up > Rebuild Selected Interior.\n" +
            "Move any shelf by dragging it. Each fixture's shop stock is on its ShopCounter component.", "OK");
    }

    private static GameObject BuildRoom(string name, Vector3 pos, TileBase floor, TileBase wallU, TileBase wallB, TileBase partition, out HouseInterior interior, out InteriorStyle style)
    {
        var room = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(room, "Build " + name);
        room.transform.position = pos;
        room.AddComponent<Grid>();
        interior = room.AddComponent<HouseInterior>();
        const int W = 20, FLOOR = 20, WALLROWS = 3;

        var back = Child(room, "Backdrop", new Vector3(W * 0.5f, (FLOOR + WALLROWS) * 0.5f, 0f));
        var bsr = back.AddComponent<SpriteRenderer>(); bsr.sprite = Spr("backdrop_dark"); bsr.sortingOrder = -5;
        back.transform.localScale = new Vector3(100f, 80f, 1f);

        var floorGo = Child(room, "Floor", Vector3.zero);
        var floorMap = floorGo.AddComponent<Tilemap>(); floorGo.AddComponent<TilemapRenderer>().sortingOrder = 0;
        var wallGo = Child(room, "Wall", Vector3.zero);
        var wallMap = wallGo.AddComponent<Tilemap>(); wallGo.AddComponent<TilemapRenderer>().sortingOrder = 1;
        var partGo = Child(room, "Partitions", Vector3.zero);
        var partMap = partGo.AddComponent<Tilemap>(); partGo.AddComponent<TilemapRenderer>().sortingOrder = 4;
        partGo.AddComponent<TilemapCollider2D>();

        var edges = Child(room, "Edges", Vector3.zero);
        Box(edges, new Vector2(-0.5f, (FLOOR + WALLROWS) * 0.5f), new Vector2(1f, FLOOR + WALLROWS + 2f), false);
        Box(edges, new Vector2(W + 0.5f, (FLOOR + WALLROWS) * 0.5f), new Vector2(1f, FLOOR + WALLROWS + 2f), false);
        Box(edges, new Vector2(W * 0.5f, -0.5f), new Vector2(W + 2f, 1f), false);
        Box(edges, new Vector2(W * 0.5f, FLOOR + WALLROWS * 0.5f), new Vector2(W, WALLROWS), false);

        var so = new SerializedObject(interior);
        so.FindProperty("width").intValue = W;
        so.FindProperty("floorRows").intValue = FLOOR;
        so.FindProperty("wallRows").intValue = WALLROWS;
        so.FindProperty("floorTilemap").objectReferenceValue = floorMap;
        so.FindProperty("wallTilemap").objectReferenceValue = wallMap;
        so.ApplyModifiedProperties();

        style = room.AddComponent<InteriorStyle>();
        style.floorMap = floorMap; style.wallMap = wallMap; style.partitionMap = partMap;
        style.floorTile = floor; style.wallUpper = wallU; style.wallBase = wallB; style.partitionTile = partition;
        style.zones = new FloorZone[0]; style.partitions = new PartitionRect[0];
        return room;
    }

    // ------------------------------------------------------------------ apartment layout
    private static void BuildApartment(GameObject apt, InteriorStyle style, Dictionary<string, TileBase> tiles, Dictionary<string, GameObject> props)
    {
        // floors per room (x, y, width, height)
        style.zones = new[]
        {
            Zone("Kitchen", 10, 0, 10, 8, tiles["floor_apt_kitchen"]),
            Zone("Bathroom 1", 0, 9, 4, 5, tiles["floor_apt_bath"]),
            Zone("Bathroom 2", 16, 9, 4, 5, tiles["floor_apt_bath"]),
            Zone("Bedroom 1", 0, 15, 7, 5, tiles["floor_apt_carpet"]),
            Zone("Bedroom 2", 8, 15, 6, 5, tiles["floor_apt_carpet"]),
            Zone("Bedroom 3", 15, 15, 5, 5, tiles["floor_apt_carpet"]),
        };
        // inside walls, each with a doorway left open
        style.partitions = new[]
        {
            Part("Living wall (left)", 0, 8, 9, 1), Part("Living wall (right)", 11, 8, 9, 1),             // doorway x 9-10
            Part("Bedroom wall A", 0, 14, 2, 1), Part("Bedroom wall B", 4, 14, 6, 1),
            Part("Bedroom wall C", 12, 14, 4, 1), Part("Bedroom wall D", 18, 14, 2, 1),                  // doorways x 2-3, 10-11, 16-17
            Part("Bath 1 wall (low)", 4, 9, 1, 2), Part("Bath 1 wall (high)", 4, 13, 1, 1),              // doorway y 11-12
            Part("Bath 2 wall (low)", 15, 9, 1, 2), Part("Bath 2 wall (high)", 15, 13, 1, 1),
            Part("Bedroom 1|2", 7, 15, 1, 5), Part("Bedroom 2|3", 14, 15, 1, 5),
        };

        // With the decor catalogue (Farm > Set Up > Interior Decor) the apartment is furnished from it after the doors are made
        // (InteriorDecorLayouts.KimApartment). Without it, the old apartment props below are used.
        if (!InteriorDecorSetupMenu.Ready())
            PlaceOldApartmentProps(apt, props);

        // spots (for the family's schedules)
        Spot(Child(apt, "Spot_Living", new Vector3(5f, 2.2f, 0f)), "AptLiving", Vector2.up);
        Spot(Child(apt, "Spot_Kitchen", new Vector3(14.5f, 5f, 0f)), "AptKitchen", Vector2.up);
        Spot(Child(apt, "Spot_Dining", new Vector3(14.5f, 2f, 0f)), "AptDining", Vector2.up);
        Spot(Child(apt, "Spot_Hall", new Vector3(10f, 10.5f, 0f)), "AptHall", Vector2.up);
        Spot(Child(apt, "Spot_Bed1", new Vector3(3.5f, 15.4f, 0f)), "AptBed1", Vector2.up);
        Spot(Child(apt, "Spot_Bed2", new Vector3(9.3f, 15.5f, 0f)), "AptBed2", Vector2.up);
        Spot(Child(apt, "Spot_Bed3", new Vector3(16.4f, 15.5f, 0f)), "AptBed3", Vector2.up);
        Spot(Child(apt, "Spot_Bath1", new Vector3(2.5f, 10.5f, 0f)), "AptBath1", Vector2.up);
        Spot(Child(apt, "Spot_Bath2", new Vector3(17.5f, 10.5f, 0f)), "AptBath2", Vector2.up);
        Spot(Child(apt, "Spot_Stairs", new Vector3(10.5f, 2f, 0f)), "AptStairs", Vector2.down);
    }

    /// <summary>The apartment's original plain furniture (used only when the decor catalogue doesn't exist yet).</summary>
    private static void PlaceOldApartmentProps(GameObject apt, Dictionary<string, GameObject> props)
    {
        // living room (open plan with the kitchen)
        Place(props, apt, "Apt_RugLiving", 5f, 4.2f);
        Place(props, apt, "Apt_Sofa", 3f, 6.6f); Place(props, apt, "Apt_TvStand", 7.2f, 6.6f);
        Place(props, apt, "Apt_CoffeeTable", 4.6f, 3.4f); Place(props, apt, "Apt_Armchair", 8.2f, 3.2f);
        Place(props, apt, "Apt_Lamp", 0.9f, 6.8f); Place(props, apt, "Apt_Plant", 1.2f, 1.6f); Place(props, apt, "Apt_Bookshelf", 8.9f, 1.2f);
        // kitchen
        Place(props, apt, "Apt_CounterCab", 11.2f, 6.2f); Place(props, apt, "Apt_CounterStove", 13.2f, 6.2f);
        Place(props, apt, "Apt_CounterSink", 15.2f, 6.2f); Place(props, apt, "Apt_CounterCab", 17.2f, 6.2f); Place(props, apt, "Apt_Fridge", 19f, 6.2f);
        Place(props, apt, "Apt_DiningTable", 14.5f, 3f);
        Place(props, apt, "Apt_Chair", 12.3f, 3.4f); Place(props, apt, "Apt_Chair", 16.7f, 3.4f); Place(props, apt, "Apt_Chair", 14.5f, 1.4f);
        // hall
        Place(props, apt, "Apt_Console", 9f, 12f); Place(props, apt, "Apt_Plant", 6.2f, 12f); Place(props, apt, "Apt_Plant", 13.8f, 12f); Place(props, apt, "Apt_ShoeRack", 11.8f, 12.2f);
        // bathrooms
        Place(props, apt, "Apt_Bathtub", 2.1f, 12.2f); Place(props, apt, "Apt_BathSink", 1.0f, 9.3f); Place(props, apt, "Apt_Toilet", 3.0f, 9.3f);
        Place(props, apt, "Apt_Shower", 18.2f, 11.3f); Place(props, apt, "Apt_BathSink", 16.9f, 9.3f); Place(props, apt, "Apt_Toilet", 19f, 9.3f);
        // bedrooms (parents, then the two kids)
        Place(props, apt, "Apt_BedDouble", 4f, 16.4f); Place(props, apt, "Apt_Nightstand", 6.3f, 16.4f); Place(props, apt, "Apt_Wardrobe", 1.2f, 16.2f);
        Place(props, apt, "Apt_BedBlue", 9.3f, 16.6f); Place(props, apt, "Apt_Desk", 12.2f, 16.4f);
        Place(props, apt, "Apt_BedGreen", 16.4f, 16.6f); Place(props, apt, "Apt_Bookshelf", 18.5f, 16.4f);
        // windows on the back wall
        Place(props, apt, "Apt_Window", 4f, 21.4f); Place(props, apt, "Apt_Window", 11f, 21.4f); Place(props, apt, "Apt_Window", 17.5f, 21.4f);
    }

    private static FloorZone Zone(string label, int x, int y, int w, int h, TileBase tile)
    { return new FloorZone { label = label, area = new RectInt(x, y, w, h), tile = tile }; }
    private static PartitionRect Part(string label, int x, int y, int w, int h)
    { return new PartitionRect { label = label, area = new RectInt(x, y, w, h) }; }

    private static GameObject Place(Dictionary<string, GameObject> props, GameObject room, string prop, float x, float y)
    {
        GameObject prefab;
        if (!props.TryGetValue(prop, out prefab) || prefab == null) { Debug.LogWarning("Store: missing prop " + prop); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(room.transform, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        return go;
    }

    private static void Spot(GameObject go, string id, Vector2 facing)
    {
        var s = go.AddComponent<NpcSpot>();
        s.spotId = id; s.facing = facing;
    }

    // ------------------------------------------------------------------ assets
    private static Dictionary<string, TileBase> MakeTiles()
    {
        var map = new Dictionary<string, TileBase>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (!file.StartsWith("floor_") && !file.StartsWith("wall_store") && !file.StartsWith("wall_apt") && !file.StartsWith("partition_")) continue;
            var tile = LoadOrCreate<Tile>(Root + "/Data/Tiles/" + file + ".asset");
            tile.sprite = Spr(file);
            EditorUtility.SetDirty(tile);
            map[file] = tile;
        }
        return map;
    }

    private static Dictionary<string, ItemData> MakeFoods()
    {
        var d = new Dictionary<string, ItemData>();
        foreach (Food f in Foods)
        {
            var it = LoadOrCreate<ItemData>(ItemsFolder + "/Food_" + f.key + ".asset");
            it.itemName = f.name; it.description = f.desc; it.type = ItemData.ItemType.Food;
            it.buyPrice = f.buy; it.sellPrice = Mathf.Max(1, f.buy / 2); it.maxStack = 99;
            it.energyRestored = f.hp; it.magicRestored = f.mp;
            it.icon = Spr("icon_" + f.key);
            EditorUtility.SetDirty(it);
            d[f.key] = it;
        }
        return d;
    }

    private static Dictionary<string, GameObject> MakeProps(Dictionary<string, ItemData> foods)
    {
        var seeds = new List<ItemData>();
        var everything = new List<ItemData>();
        var materials = new List<ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it == null || it.buyPrice <= 0) continue;
            if (it.type == ItemData.ItemType.Seed) seeds.Add(it);
            else if (it.type == ItemData.ItemType.Resource) materials.Add(it);     // wood planks, bricks, glass... (the farm's old stall sold these)
        }
        seeds.Sort((a, b) => string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase));
        materials.Sort((a, b) => string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase));
        everything.AddRange(foods.Values.OrderBy(i => i.itemName));
        everything.AddRange(seeds);
        everything.AddRange(materials);

        var result = new Dictionary<string, GameObject>();
        foreach (Prop p in Props)
        {
            var go = new GameObject(p.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr(p.sprite);
            sr.sortingOrder = p.order;
            if (p.col != Vector2.zero)
            {
                var bc = go.AddComponent<BoxCollider2D>();
                bc.size = p.col; bc.offset = new Vector2(0f, p.col.y * 0.5f);
                go.AddComponent<BehindPlayerSorter>().thresholdOffset = p.col.y;
            }
            if (p.flipbook != null)
            {
                var fb = go.AddComponent<SpriteFlipbook>();
                fb.frames = SheetFrames(p.flipbook, 4); fb.framesPerSecond = 3f;
                if (fb.frames.Length > 0) sr.sprite = fb.frames[0];
            }
            if (p.stock != null || p.seeds || p.everything)
            {
                var shop = go.AddComponent<ShopCounter>();
                var list = new List<Object>();
                if (p.everything) list.AddRange(everything.Cast<Object>());
                else if (p.seeds) list.AddRange(seeds.Cast<Object>());
                else foreach (string k in p.stock) list.Add(foods[k]);
                SetArray(shop, "stock", list);
                SetString(shop, "shopName", p.shop);
            }
            if (p.flavor != null)
            {
                // StoreFlavor needs a collider to already be there (Unity refuses to add it otherwise), so make the collider first.
                if (go.GetComponent<Collider2D>() == null) { var bc = go.AddComponent<BoxCollider2D>(); bc.size = new Vector2(1f, 1f); bc.offset = new Vector2(0f, 0.5f); bc.isTrigger = true; }
                var fl = go.AddComponent<StoreFlavor>();
                fl.lines = p.flavor;
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + p.name + ".prefab");
            Object.DestroyImmediate(go);
            AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
            result[p.name] = prefab;
        }
        return result;
    }

    // ------------------------------------------------------------------ art import
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n.StartsWith("slush_")) { SliceSheet(p, 32, 44); continue; }
            bool centre = n.StartsWith("floor_") || n.StartsWith("wall_") || n.StartsWith("partition_") || n.StartsWith("icon_") || n.StartsWith("backdrop")
                          || n.Contains("poster") || n.Contains("doormat") || n.Contains("rug");
            ImportSingle(p, centre ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        }
        AssetDatabase.Refresh();
    }

    private static Sprite Spr(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(art + "/" + name + ".png"); }

    private static Sprite[] SheetFrames(string sheet, int count)
    {
        var list = new List<Sprite>();
        string p = art + "/" + sheet + ".png";
        for (int i = 0; i < count; i++)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
            {
                var s = o as Sprite;
                if (s != null && s.name == sheet + "_" + i + "_0") { list.Add(s); break; }
            }
        return list.ToArray();
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

    private static void SliceSheet(string path, int cellW, int cellH)
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
                rect.alignment = SpriteAlignment.BottomCenter;
                rect.pivot = new Vector2(0.5f, 0f);
                rect.spriteID = StableSpriteId.For(provider, baseName, rect.name);   // keeps the same ID every run, so nothing loses its art
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
        if (p == null) return;
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
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
}
