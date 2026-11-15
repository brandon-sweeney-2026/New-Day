using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > Frank's Home and Hardware      (art: Assets/Project/Art/Hardware)
///
/// Frank's is the home improvement store on Shop Street, run by Frank (short, round, white tank top, blue jeans).
/// Farm > Set Up > Suburbs Scene runs this for you and puts the store in its place on the street.
///
/// Builds in the OPEN scene:
///   - the store building: 10 tiles wide, 10 tiles of solid wall (ground floor and second floor), and a roof / sign band above that you walk behind,
///   - the store inside: 30 x 30 tiles, far away from the farm, with aisles of displays, a staff office, a customer counter and Frank,
///   - the front door, and a path network + NpcSpots so Frank can walk around.
/// Every display sells something: flooring, paint, cabinets, stoves, windows, doors, exterior parts, lumber, glass, masonry, fabric...
/// The Tools display sells the tool upgrades (hoe, watering can, scythe, axe, pickaxe) made by Farm > Set Up > Tools.
/// Run Farm > Set Up > Farmhouse Hardware Upgrades first so there is something to sell.
/// House upgrades need a HouseManager in the scene: the Suburbs scene builder adds one that only holds the catalog.
/// Safe to run again for assets; delete 'FranksStore', 'FranksInterior', 'Frank' and 'PathNetwork_Hardware' to rebuild the scene part.
/// Must live in a folder named "Editor".
/// </summary>
public static class FrankStoreSetupMenu
{
    private const string Root = "Assets/Farm/Suburb/Hardware";
    private static string art;
    private const int W = 30, FLOOR = 30, WALLROWS = 3;

    private static Sprite Spr(string n) { return HardwareEditorUtil.Spr(art, n); }

    // ------------------------------------------------------------------ the displays
    private class Prop
    {
        public string name, sprite, title, prompt, look;
        public Vector2 col;                       // solid base size in tiles (0 = nothing solid)
        public HouseCategory[] upgrades;          // house upgrades sold here
        public string[] mats;                     // loose materials sold here (item asset names)
        public bool flat; public int order = 5;
        public bool everything;                   // Frank's counter: all upgrades and all materials
        public bool tools;                        // sells the farm tool upgrades (a ShopCounter instead of a HardwareDisplay)
    }

    private static HouseCategory[] Cats(params HouseCategory[] c) { return c; }
    private static readonly string[] AllMats = { "WoodPlank", "PaintBucket", "StoneBrick", "FabricRoll", "GlassPane" };

    private static readonly Prop[] Props =
    {
        new Prop { name="Hw_Counter", sprite="hw_counter", col=new Vector2(2.8f,0.9f), everything=true, title="Frank's Counter", prompt="Shop at the counter" },
        new Prop { name="Hw_Flooring", sprite="hw_flooring", col=new Vector2(2.8f,0.9f), upgrades=Cats(HouseCategory.Floor), title="Flooring", prompt="Browse flooring" },
        new Prop { name="Hw_PaintWall", sprite="hw_paint_wall", col=new Vector2(2.8f,0.9f), upgrades=Cats(HouseCategory.Wall), mats=new[]{"PaintBucket"}, title="Paint and Walls", prompt="Browse paint" },
        new Prop { name="Hw_Lumber", sprite="hw_rack_lumber", col=new Vector2(2.8f,1.0f), mats=new[]{"WoodPlank"}, title="Lumber", prompt="Browse lumber" },
        new Prop { name="Hw_Cabinets", sprite="hw_cabinets", col=new Vector2(2.8f,0.9f), upgrades=Cats(HouseCategory.Cabinets), title="Kitchen Cabinets", prompt="Browse cabinets" },
        new Prop { name="Hw_Appliance", sprite="hw_appliance", col=new Vector2(1.8f,0.9f), upgrades=Cats(HouseCategory.Kitchen), title="Stoves and Ranges", prompt="Browse stoves" },
        new Prop { name="Hw_Windows", sprite="hw_windows", col=new Vector2(2.8f,0.9f), upgrades=Cats(HouseCategory.WindowStyle, HouseCategory.WindowLayout), mats=new[]{"GlassPane"}, title="Windows", prompt="Browse windows" },
        new Prop { name="Hw_Doors", sprite="hw_doors", col=new Vector2(2.8f,0.9f), upgrades=Cats(HouseCategory.Door), title="Doors", prompt="Browse doors" },
        new Prop { name="Hw_Exterior", sprite="hw_exterior_samples", col=new Vector2(2.8f,0.9f), upgrades=Cats(HouseCategory.Siding, HouseCategory.Roof, HouseCategory.Shutters), title="Exterior", prompt="Browse exterior parts" },
        new Prop { name="Hw_Masonry", sprite="hw_masonry", col=new Vector2(1.8f,0.8f), mats=new[]{"StoneBrick"}, title="Masonry", prompt="Browse masonry" },
        new Prop { name="Hw_Glass", sprite="hw_glass_rack", col=new Vector2(1.8f,0.8f), mats=new[]{"GlassPane"}, title="Glass", prompt="Browse glass" },
        new Prop { name="Hw_Fabric", sprite="hw_fabric_rack", col=new Vector2(1.8f,0.8f), upgrades=Cats(HouseCategory.Rug), mats=new[]{"FabricRoll"}, title="Fabric and Rugs", prompt="Browse fabric and rugs" },
        new Prop { name="Hw_Lighting", sprite="hw_lighting", col=new Vector2(1.8f,0.8f), upgrades=Cats(HouseCategory.Lamp, HouseCategory.WallArt, HouseCategory.Plant), title="Lighting and Decor", prompt="Browse lighting and decor" },
        new Prop { name="Hw_Furnishings", sprite="hw_rack_boxes", col=new Vector2(2.8f,1.0f), upgrades=Cats(HouseCategory.Bed, HouseCategory.Table, HouseCategory.Bookshelf), title="Home Furnishings", prompt="Browse furnishings" },
        new Prop { name="Hw_Tools", sprite="hw_tools", col=new Vector2(2.8f,0.9f), title="Frank's Tools", tools=true },
        new Prop { name="Hw_Plumbing", sprite="hw_plumbing", col=new Vector2(1.8f,0.8f), title="Plumbing", prompt="Look", look="Pipes and fittings. Nobody has ever asked Frank for these twice." },
        new Prop { name="Hw_Cart", sprite="hw_cart", col=new Vector2(0.9f,0.5f), title="Cart", prompt="Look", look="A shopping cart with one wonky wheel." },
        new Prop { name="Hw_Ladder", sprite="hw_ladder", col=new Vector2(0.8f,0.4f), title="Ladder", prompt="Look", look="A tall orange ladder. Please do not climb it." },
        new Prop { name="Hw_Cot", sprite="hw_cot", col=new Vector2(1.9f,0.8f), title="Cot", prompt="Look", look="Frank's cot. There's a pillow with a hammer printed on it." },
        new Prop { name="Hw_OfficeDesk", sprite="hw_office_desk", col=new Vector2(1.9f,0.8f), title="Desk", prompt="Look", look="Invoices, sticky notes and a very old calculator." },
        new Prop { name="Hw_MiniFridge", sprite="hw_mini_fridge", col=new Vector2(0.9f,0.6f), title="Fridge", prompt="Look", look="A tiny fridge full of cold sandwiches." },
        new Prop { name="Hw_Doormat", sprite="hw_doormat", flat=true, order=2 },
        new Prop { name="Hw_BannerA", sprite="hw_banner_a", flat=true, order=3 },
        new Prop { name="Hw_BannerB", sprite="hw_banner_b", flat=true, order=3 },
        new Prop { name="Hw_SignLumber", sprite="hw_sign_lumber", flat=true, order=3 },
        new Prop { name="Hw_SignPaint", sprite="hw_sign_paint", flat=true, order=3 },
        new Prop { name="Hw_SignFloors", sprite="hw_sign_floors", flat=true, order=3 },
        new Prop { name="Hw_SignKitchen", sprite="hw_sign_kitchen", flat=true, order=3 },
        new Prop { name="Hw_SignWindows", sprite="hw_sign_windows", flat=true, order=3 },
        new Prop { name="Hw_SignDoors", sprite="hw_sign_doors", flat=true, order=3 },
    };

    // ------------------------------------------------------------------ menu
    [MenuItem("Farm/Set Up/Frank's Home and Hardware", false, 150)]
    public static void RunFromMenu()
    {
        // where to build: next door to the general store if there is one, otherwise the middle of the Scene view
        Vector3 origin = Vector3.zero;
        if (SceneView.lastActiveSceneView != null) origin = SceneView.lastActiveSceneView.pivot;
        GameObject generalStore = GameObject.Find("SuburbStore");
        if (generalStore != null) origin = generalStore.transform.position + new Vector3(14f, 0f, 0f);

        string result = Build(origin);
        bool ok = result.StartsWith("Built");
        if (ok)
        {
            HouseManager hm = Object.FindAnyObjectByType<HouseManager>();
            if (hm != null && !hm.buyOnlyAtHardwareStore &&
                EditorUtility.DisplayDialog("Frank's is the shop for house upgrades?", "Make Frank's the ONLY place to buy new house upgrades?\n\nYes: the design table at home only lets you switch between things you own.\nNo: you can still buy at the design table too. (Change it any time: HouseManager > Buy Only At Hardware Store.)", "Yes, Frank's only", "No, both"))
            {
                hm.buyOnlyAtHardwareStore = true;
                EditorUtility.SetDirty(hm);
            }
        }
        EditorUtility.DisplayDialog(ok ? "Frank's Home and Hardware ready" : "Frank's Home and Hardware", result, "OK");
    }

    /// <summary>
    /// Builds Frank's store in the open scene with its front door at 'origin'. Returns a sentence saying what happened
    /// (it starts with "Built" when it worked). No pop-ups, so the Suburbs scene builder can call it too.
    /// </summary>
    public static string Build(Vector3 origin)
    {
        art = HardwareEditorUtil.FindFolder("hw_exterior");
        if (art == null) return "Couldn't find hw_exterior.png. The 'Hardware' art folder belongs in Assets/Project/Art/Hardware.";
        HardwareEditorUtil.EnsureFolder(Root + "/Data/Tiles");
        HardwareEditorUtil.EnsureFolder(Root + "/Data/Schedules");
        HardwareEditorUtil.EnsureFolder(Root + "/Prefabs");
        ImportArt();
        var tiles = MakeTiles();
        var props = MakeProps();
        AssetDatabase.SaveAssets();

        if (GameObject.Find("FranksStore") != null || GameObject.Find("FranksInterior") != null)
            return "Frank's is already in this scene, so only the assets were refreshed. To rebuild the buildings, delete 'FranksStore', 'FranksInterior', 'Frank' and 'PathNetwork_Hardware' from the scene first.";
        return BuildScene(tiles, props, origin);
    }

    // ------------------------------------------------------------------ art
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n == "npc_frank") HardwareEditorUtil.SliceSheet(p, 16, 28, SpriteAlignment.BottomCenter);
            else if (n == "portraits_frank") HardwareEditorUtil.SliceSheet(p, 64, 64, SpriteAlignment.Center);
            else if (n.StartsWith("floor_") || n.StartsWith("wall_hw") || n.StartsWith("partition_") || n.StartsWith("backdrop")
                     || n.StartsWith("hw_banner") || n.StartsWith("hw_sign") || n == "hw_doormat")
                HardwareEditorUtil.ImportSingle(p, SpriteAlignment.Center);
            else if (n.StartsWith("hw_")) HardwareEditorUtil.ImportSingle(p, SpriteAlignment.BottomCenter);
        }
        AssetDatabase.Refresh();
    }

    private static Dictionary<string, TileBase> MakeTiles()
    {
        var map = new Dictionary<string, TileBase>();
        foreach (string file in new[] { "floor_hw", "floor_hw_lane", "floor_hw_office", "floor_hw_back", "wall_hw_upper", "wall_hw_base", "partition_hw" })
        {
            var tile = HardwareEditorUtil.LoadOrCreate<Tile>(Root + "/Data/Tiles/" + file + ".asset");
            tile.sprite = Spr(file);
            EditorUtility.SetDirty(tile);
            map[file] = tile;
        }
        return map;
    }

    private static Dictionary<string, GameObject> MakeProps()
    {
        var all = new List<Object>();
        foreach (string m in AllMats) { ItemData it = HardwareEditorUtil.FindItem(m); if (it != null) all.Add(it); }
        if (all.Count == 0) Debug.LogWarning("Frank's: no building materials found (WoodPlank, PaintBucket...). Build the farmhouse (Farm > Set Up > Build Farmhouse) first, then rebuild Frank's props.");
        List<ItemData> toolsForSale = ToolsForSale();
        var allCats = ((HouseCategory[])System.Enum.GetValues(typeof(HouseCategory)));

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
            if (p.tools)
            {
                // the tool rack is an ordinary shop: it sells every tool upgrade that has a Buy Price
                var shop = go.AddComponent<ShopCounter>();
                HardwareEditorUtil.SetString(shop, "shopName", p.title);
                HardwareEditorUtil.SetObjects(shop, "stock", toolsForSale.ConvertAll(t => (Object)t));
            }
            else if (!p.flat)
            {
                var hd = go.AddComponent<HardwareDisplay>();
                HardwareEditorUtil.SetString(hd, "title", p.title ?? "Hardware");
                HardwareEditorUtil.SetString(hd, "prompt", p.prompt ?? "Browse");
                HardwareEditorUtil.SetString(hd, "lookLine", p.look ?? "");

                var cats = p.everything ? allCats : (p.upgrades ?? new HouseCategory[0]);
                var so = new SerializedObject(hd);
                SerializedProperty up = so.FindProperty("upgrades");
                up.arraySize = cats.Length;
                for (int i = 0; i < cats.Length; i++) up.GetArrayElementAtIndex(i).enumValueIndex = (int)cats[i];
                so.ApplyModifiedProperties();

                var mats = new List<Object>();
                if (p.everything) mats.AddRange(all);
                else if (p.mats != null) foreach (string m in p.mats) { ItemData it = HardwareEditorUtil.FindItem(m); if (it != null) mats.Add(it); }
                HardwareEditorUtil.SetObjects(hd, "materials", mats);
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + p.name + ".prefab");
            Object.DestroyImmediate(go);
            AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
            result[p.name] = prefab;
        }
        return result;
    }

    /// <summary>Every tool that can be bought (made by Farm > Set Up > Tools), cheapest tier first.</summary>
    private static List<ItemData> ToolsForSale()
    {
        var list = new List<ItemData>();
        if (!AssetDatabase.IsValidFolder("Assets/Farm/Data/Tools")) return list;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Farm/Data/Tools" }))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it != null && it.type == ItemData.ItemType.Tool && it.buyPrice > 0) list.Add(it);
        }
        list.Sort((a, b) => a.tier != b.tier ? ((int)a.tier).CompareTo((int)b.tier) : string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    // ------------------------------------------------------------------ scene
    private static string BuildScene(Dictionary<string, TileBase> tiles, Dictionary<string, GameObject> props, Vector3 origin)
    {
        origin = new Vector3(Mathf.Round(origin.x), Mathf.Round(origin.y), 0f);

        float nextX = 600f;
        foreach (HouseInterior h in Object.FindObjectsByType<HouseInterior>()) nextX = Mathf.Max(nextX, h.transform.position.x + 100f);

        // ---------- the building ----------
        var ext = new GameObject("FranksStore");
        Undo.RegisterCreatedObjectUndo(ext, "Build Frank's store");
        ext.transform.position = origin;
        var grp = ext.AddComponent<UnityEngine.Rendering.SortingGroup>(); grp.sortingOrder = 10;
        ext.AddComponent<BehindPlayerSorter>().thresholdOffset = 3f;
        var picture = new GameObject("Building"); picture.transform.SetParent(ext.transform, false);
        picture.AddComponent<SpriteRenderer>().sprite = Spr("hw_exterior");
        // 10 x 10 tiles are solid (ground floor and second floor); the roof and sign band above can be walked behind
        var body = ext.AddComponent<BoxCollider2D>();
        body.size = new Vector2(9.8f, 10f); body.offset = new Vector2(0f, 5f);
        var frontDoor = Child(ext, "FrontDoor", new Vector3(0f, 0.3f, 0f));
        Box(frontDoor, Vector2.zero, new Vector2(2.4f, 1.8f), true);
        var exitSpot = Child(ext, "ExitSpot", new Vector3(0f, -0.9f, 0f));
        Spot(Child(ext, "Spot_HwFront", new Vector3(0f, -1.6f, 0f)), "HwStreet", Vector2.up);

        // ---------- the inside: 30 x 30 ----------
        GameObject room = BuildRoom(new Vector3(nextX, 0f, 0f), tiles, out HouseInterior interior, out InteriorStyle style);
        Place(props, room, "Hw_Doormat", 15f, 0.6f);
        var entry = Child(room, "EntrySpawn", new Vector3(15f, 1.4f, 0f));
        var exitDoorGo = Child(room, "ExitDoor", new Vector3(15f, 0f, 0f));
        Box(exitDoorGo, Vector2.zero, new Vector2(2.4f, 1.2f), true);
        var exitDoor = exitDoorGo.AddComponent<HouseDoor>();

        // front: counter and carts
        GameObject counter = Place(props, room, "Hw_Counter", 23.5f, 2.4f);
        Place(props, room, "Hw_Cart", 11f, 2.2f); Place(props, room, "Hw_Cart", 12.3f, 2.4f); Place(props, room, "Hw_Ladder", 27.8f, 1.2f);
        // left side: lumber, flooring, paint, masonry, glass, fabric
        Place(props, room, "Hw_Lumber", 3.2f, 17f); Place(props, room, "Hw_Lumber", 7.4f, 17f);
        Place(props, room, "Hw_Flooring", 3.2f, 11.5f); Place(props, room, "Hw_Flooring", 7.4f, 11.5f);
        Place(props, room, "Hw_PaintWall", 11f, 18f); Place(props, room, "Hw_Masonry", 11f, 12f);
        Place(props, room, "Hw_Glass", 3f, 4.5f); Place(props, room, "Hw_Fabric", 6.5f, 4.5f);
        // right side: kitchen, windows, doors, exterior, lighting
        Place(props, room, "Hw_Cabinets", 19.8f, 17f); Place(props, room, "Hw_Appliance", 23.4f, 17f); Place(props, room, "Hw_Lighting", 27f, 17f);
        Place(props, room, "Hw_Windows", 19.8f, 11.5f); Place(props, room, "Hw_Doors", 23.5f, 11.5f); Place(props, room, "Hw_Exterior", 27.3f, 11.5f);
        // back wall
        Place(props, room, "Hw_Furnishings", 12f, 26.5f); Place(props, room, "Hw_Furnishings", 16f, 26.5f);
        Place(props, room, "Hw_Plumbing", 20f, 26.5f); Place(props, room, "Hw_Tools", 24f, 26.5f); Place(props, room, "Hw_Furnishings", 28f, 26.5f);
        // staff office
        Place(props, room, "Hw_Cot", 2.4f, 27.2f); Place(props, room, "Hw_OfficeDesk", 5.8f, 27.2f); Place(props, room, "Hw_MiniFridge", 8.1f, 27.2f);
        // signs on the back wall
        Place(props, room, "Hw_SignFloors", 11.5f, 31.6f); Place(props, room, "Hw_SignLumber", 14.5f, 31.6f); Place(props, room, "Hw_BannerA", 18f, 31.6f);
        Place(props, room, "Hw_SignKitchen", 21.5f, 31.6f); Place(props, room, "Hw_SignWindows", 24.5f, 31.6f); Place(props, room, "Hw_SignDoors", 27.5f, 31.6f);
        Place(props, room, "Hw_BannerB", 5f, 31.6f);

        // spots for Frank
        Spot(Child(room, "Spot_Counter", new Vector3(23.5f, 4.6f, 0f)), "HwCounter", Vector2.down);
        Spot(Child(room, "Spot_Office", new Vector3(4.2f, 25f, 0f)), "HwBackOffice", Vector2.down);
        Spot(Child(room, "Spot_DoorInside", new Vector3(15f, 2.4f, 0f)), "HwDoorInside", Vector2.down);
        Spot(Child(room, "Spot_Lumber", new Vector3(5.3f, 15.2f, 0f)), "HwLumber", Vector2.up);
        Spot(Child(room, "Spot_Flooring", new Vector3(5.3f, 9.8f, 0f)), "HwFlooring", Vector2.up);
        Spot(Child(room, "Spot_Paint", new Vector3(11f, 16.3f, 0f)), "HwPaint", Vector2.up);
        Spot(Child(room, "Spot_Kitchen", new Vector3(21.5f, 15.2f, 0f)), "HwKitchen", Vector2.up);
        Spot(Child(room, "Spot_Windows", new Vector3(22f, 9.9f, 0f)), "HwWindows", Vector2.up);
        // spots for customers (the neighbours' schedules use these)
        Spot(Child(room, "Spot_Customer", new Vector3(23.5f, 1.3f, 0f)), "HwCustomer", Vector2.up);
        Spot(Child(room, "Spot_Browse", new Vector3(24f, 24.6f, 0f)), "HwTools", Vector2.up);

        // the door in and out
        HardwareEditorUtil.SetRef(frontDoor.AddComponent<HouseDoor>(), "destination", entry.transform);
        HardwareEditorUtil.SetRef(frontDoor.GetComponent<HouseDoor>(), "enterInterior", interior);
        HardwareEditorUtil.SetString(frontDoor.GetComponent<HouseDoor>(), "prompt", "Enter Frank's");
        HardwareEditorUtil.SetRef(exitDoor, "destination", exitSpot.transform);
        HardwareEditorUtil.SetString(exitDoor, "prompt", "Go outside");

        style.Apply();
        BuildPaths(ext, room);
        BuildFrank(room, counter != null ? counter.GetComponent<HardwareDisplay>() : null);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = ext;

        return "Built Frank's store at (" + origin.x + ", " + origin.y + "), its inside at x = " + nextX + ".\n\n" +
               "Walk to the glass doors and press Space. Every display sells something; Frank's counter sells everything, and the Tools rack sells the tool upgrades. Frank works the counter and walks the aisles on a schedule (Assets/Farm/Suburb/Hardware/Data/Schedules).\n\n" +
               "Reshape the inside: change Width / Floor Rows on 'FranksInterior', add Interior Blockers, then Farm > Set Up > Rebuild Selected Interior.";
    }

    private static GameObject BuildRoom(Vector3 pos, Dictionary<string, TileBase> tiles, out HouseInterior interior, out InteriorStyle style)
    {
        var room = new GameObject("FranksInterior");
        Undo.RegisterCreatedObjectUndo(room, "Build Frank's interior");
        room.transform.position = pos;
        room.AddComponent<Grid>();
        interior = room.AddComponent<HouseInterior>();

        var back = Child(room, "Backdrop", new Vector3(W * 0.5f, (FLOOR + WALLROWS) * 0.5f, 0f));
        // (the dark backdrop picture is shared with the general store: Assets/Project/Art/Store/backdrop_dark.png)
        string backdropFolder = HardwareEditorUtil.FindFolder("backdrop_dark");
        var bsr = back.AddComponent<SpriteRenderer>(); bsr.sprite = backdropFolder != null ? HardwareEditorUtil.Spr(backdropFolder, "backdrop_dark") : null; bsr.sortingOrder = -5;
        back.transform.localScale = new Vector3(120f, 100f, 1f);

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
        style.floorTile = tiles["floor_hw"]; style.wallUpper = tiles["wall_hw_upper"]; style.wallBase = tiles["wall_hw_base"]; style.partitionTile = tiles["partition_hw"];
        style.zones = new[]
        {
            Zone("Centre aisle", 14, 1, 2, 27, tiles["floor_hw_lane"]),
            Zone("Cross aisle", 1, 7, 28, 2, tiles["floor_hw_lane"]),
            Zone("Back strip", 10, 23, 20, 7, tiles["floor_hw_back"]),
            Zone("Staff office", 0, 23, 9, 7, tiles["floor_hw_office"]),
        };
        style.partitions = new[]
        {
            Part("Office front (left)", 0, 22, 6, 1), Part("Office front (right)", 8, 22, 2, 1),   // doorway at x 6-7
            Part("Office side", 9, 23, 1, 7),
        };
        return room;
    }

    private static FloorZone Zone(string label, int x, int y, int w, int h, TileBase tile)
    {
        return new FloorZone { label = label, area = new RectInt(x, y, w, h), tile = tile };
    }
    private static PartitionRect Part(string label, int x, int y, int w, int h)
    {
        return new PartitionRect { label = label, area = new RectInt(x, y, w, h) };
    }

    // ------------------------------------------------------------------ paths for Frank
    private static void BuildPaths(GameObject ext, GameObject room)
    {
        Transform root = new GameObject("PathNetwork_Hardware").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Hardware paths");
        Vector2 R = room.transform.position;
        PathNode N(string id, float x, float y) { return PathToolsMenu.MakeNode(id, R + new Vector2(x, y), root); }

        PathNode street = PathToolsMenu.MakeNode("Street_Hardware", (Vector2)ext.transform.position + new Vector2(0f, -1.4f), root);
        PathNode door = N("Hw_Door", 15f, 1.5f), front = N("Hw_Front", 15f, 4.5f), counter = N("Hw_Counter", 23.5f, 4.6f);
        PathNode aisle = N("Hw_Aisle", 15f, 8f), leftMid = N("Hw_LeftMid", 9f, 8f), flooring = N("Hw_Flooring", 5.3f, 9.8f), lumber = N("Hw_Lumber", 5.3f, 15.2f);
        PathNode center = N("Hw_Center", 15f, 13f), centerUp = N("Hw_CenterUp", 15f, 16f), paint = N("Hw_Paint", 11f, 16.3f), kitchen = N("Hw_Kitchen", 21.5f, 15.2f);
        PathNode rightMid = N("Hw_RightMid", 21.5f, 8f), windows = N("Hw_Windows", 22f, 9.9f);
        PathNode back = N("Hw_Back", 15f, 21.6f), officeDoor = N("Hw_OfficeDoor", 7f, 21.6f), officeIn = N("Hw_OfficeIn", 7f, 23.8f), office = N("Hw_Office", 4.2f, 25f);

        Chain(door, front, aisle, center, centerUp, back, officeDoor, officeIn, office);
        Chain(front, counter);
        Chain(aisle, leftMid, flooring, lumber);
        Chain(aisle, rightMid, windows);
        Chain(rightMid, counter);
        Chain(centerUp, paint);
        Chain(centerUp, kitchen);
        // for customers: the shopper's side of the counter, and the tool rack on the back wall
        Chain(door, N("Hw_Customer", 23.5f, 1.3f));
        Chain(back, N("Hw_ToolsAisle", 24f, 21.6f), N("Hw_Tools", 24f, 24.6f));
        street.portalTo = door; door.portalTo = street;
        EditorUtility.SetDirty(street); EditorUtility.SetDirty(door);
        PathNode.MarkDirty();
    }

    private static void Chain(params PathNode[] n) { for (int i = 0; i + 1 < n.Length; i++) PathToolsMenu.Link(n[i], n[i + 1]); }

    // ------------------------------------------------------------------ Frank
    private static void BuildFrank(GameObject room, HardwareDisplay counter)
    {
        var go = new GameObject("Frank");
        Undo.RegisterCreatedObjectUndo(go, "Add Frank");
        NpcSpot office = null;
        foreach (NpcSpot sp in Object.FindObjectsByType<NpcSpot>()) if (sp.spotId == "HwBackOffice") office = sp;
        go.transform.position = office != null ? office.transform.position : room.transform.position + new Vector3(4.2f, 25f, 0f);
        go.AddComponent<SpriteRenderer>().sortingOrder = 0;
        var npc = go.AddComponent<ScheduledNpc>();
        npc.displayName = "Frank";
        npc.schedule = MakeSchedule();
        HardwareEditorUtil.GiveNpcArt(npc, art, "npc_frank", "portraits_frank");
        npc.nameColor = new Color(0.85f, 0.45f, 0.1f);
        npc.shopSpotId = "HwCounter"; npc.shopSpotRadius = 3.5f;
        npc.hardware = counter;
        npc.firstMeeting = new[]
        {
            "Well, hello there! Name's Frank. Frank's Home and Hardware. Yes, that's the sign, yes, I put it up myself.",
            "Floors, walls, windows, doors, cabinets, stoves. If your house needs it, I've got it. Or I'll order it.",
            "Walk around, look at everything, and holler if you want a hand."
        };
        npc.chatLines = new[]
        {
            Chat("Default", 0, null, "Need a hand with the house?"),
            Chat("Default 2", 0, null, "Measure twice, cut once. Then come back and buy another board anyway. Everybody does."),
            Chat("Default 3", 0, null, "That big tower in the city sells doors in a box. A door! In a box! You come to Frank for a real door."),
            Chat("Default 4", 0, null, "Gus from next door says my prices are robbery. Gus has been 'just looking' for eleven years."),
            Chat("Rain", 10, new NpcCondition { weather = WeatherMask.Rain | WeatherMask.Storm }, "Good weather for staying in and fixing up the house, if you ask me."),
            Chat("Snow", 10, new NpcCondition { weather = WeatherMask.Snow }, "Snow's coming down. Best time to think about insulation. And windows. Mostly windows."),
            Chat("Sunday", 12, new NpcCondition { days = DayMask.Sun }, "Sundays I put my feet up. Don't tell anyone I answered the door."),
            Chat("A few upgrades", 20, new NpcCondition { counters = new[] { new CounterCheck { key = "house_upgrades", atLeast = 3 } } }, "I hear your house is looking sharp. That's my doing, you know. Mostly."),
            Chat("Lots of upgrades", 30, new NpcCondition { counters = new[] { new CounterCheck { key = "house_upgrades", atLeast = 12 } } }, "Twelve upgrades! I'm going to need to hire somebody just to carry your orders.")
        };
        EditorUtility.SetDirty(npc);
    }

    private static NpcChatLine Chat(string label, int prio, NpcCondition c, params string[] pages)
    {
        return new NpcChatLine { label = label, priority = prio, conditions = c ?? new NpcCondition(), pages = pages };
    }

    private static NpcScheduleData MakeSchedule()
    {
        // A schedule that already exists is kept, so your own edits survive a rebuild. Delete the asset to get these starter routes back.
        var existing = AssetDatabase.LoadAssetAtPath<NpcScheduleData>(Root + "/Data/Schedules/Schedule_Frank.asset");
        if (existing != null) return existing;
        var s = HardwareEditorUtil.LoadOrCreate<NpcScheduleData>(Root + "/Data/Schedules/Schedule_Frank.asset");
        s.npcId = "frank"; s.idleSpot = "HwBackOffice"; s.idleScene = "";
        // Two routes that overlap in time can't both run (the higher priority wins, the other is dropped for the day), so each
        // route below is finished before the next one starts. 'Stay' routes end at their last stop instead of walking back to the office.
        const DayMask workDays = DayMask.Mon | DayMask.Tue | DayMask.Wed | DayMask.Thu | DayMask.Fri | DayMask.Sat;
        NpcCondition Work() { return new NpcCondition { days = workDays }; }
        s.routes = new[]
        {
            Stay("Open the store", 7.5f, 10, Work(), Stop("HwDoorInside", 5, "Unlocking the doors"), Stop("HwCounter", 100, "Running the till")),
            Stay("Morning rounds", 10f, 10, Work(), Stop("HwLumber", 20, "Restocking lumber"), Stop("HwFlooring", 15, "Tidying the flooring samples"), Stop("HwCounter", 55, "Running the till")),
            Stay("Rainy-day paint sale", 10f, 30, new NpcCondition { days = workDays, weather = WeatherMask.Rain | WeatherMask.Storm }, Stop("HwPaint", 30, "Mixing paint"), Stop("HwCounter", 70, "Running the till")),
            Stay("Lunch", 12.5f, 10, Work(), Stop("HwBackOffice", 40, "Eating a sandwich")),
            Stay("Afternoon till", 13.75f, 10, Work(), Stop("HwCounter", 115, "Running the till")),
            Stay("Helping customers", 16.25f, 10, Work(), Stop("HwKitchen", 20, "Showing someone a stove"), Stop("HwWindows", 20, "Measuring a window"), Stop("HwCounter", 55, "Running the till")),
            Route("Close up", 18.75f, 10, Work(), Stop("HwDoorInside", 5, "Locking up")),
            Route("Close up, then the bench", 18.75f, 20, new NpcCondition { days = workDays, weather = WeatherMask.Clear | WeatherMask.Cloudy }, Stop("HwDoorInside", 5, "Locking up"), Stop("PlazaBench2", 45, "Putting his feet up")),
            Route("Sunday off", 8f, 50, new NpcCondition { days = DayMask.Sun }, Stop("HwBackOffice", 90, "Having a slow morning"), Stop("ParkBenchB", 120, "Watching the football"), Stop("PlazaBench1", 45, "Reading the paper")),
        };
        EditorUtility.SetDirty(s);
        return s;
    }

    private static ScheduleStop Stop(string place, float wait, string activity)
    {
        return new ScheduleStop { place = place, waitMinutes = wait, activity = activity };
    }

    /// <summary>A route that walks back to the idle spot (the back office) when it is finished.</summary>
    private static ScheduleRoute Route(string name, float hour, int prio, NpcCondition cond, params ScheduleStop[] stops)
    {
        return new ScheduleRoute { routeName = name, startHour = hour, priority = prio, conditions = cond ?? new NpcCondition(), stops = stops, whenFinished = RouteEnd.ReturnToIdleSpot };
    }

    /// <summary>A route that ends at its last stop: he stays there until the next route starts.</summary>
    private static ScheduleRoute Stay(string name, float hour, int prio, NpcCondition cond, params ScheduleStop[] stops)
    {
        ScheduleRoute r = Route(name, hour, prio, cond, stops);
        r.whenFinished = RouteEnd.StayAtLastStop;
        return r;
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

    private static GameObject Place(Dictionary<string, GameObject> props, GameObject room, string prop, float x, float y)
    {
        GameObject prefab;
        if (!props.TryGetValue(prop, out prefab) || prefab == null) { Debug.LogWarning("Frank's: missing prop " + prop); return null; }
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
}
