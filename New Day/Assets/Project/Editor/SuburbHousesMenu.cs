using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > Suburb Houses
/// Builds enterable houses in the open scene:
///   - ten exterior pictures (colonial, bungalow, brick ranch, modern; Assets/Project/Art/Houses), each with a front door you can enter,
///   - four interior layouts (Cosy, Family, Cosy mirrored, Loft), furnished with the apartment furniture made by Farm > Set Up > General Store,
///   - NpcSpots and path nodes in every house, so NPCs can live there (Farm > Set Up > Suburb Residents moves the neighbours in).
/// Each door has opening hours you can change.
///
/// Farm > Set Up > Suburbs Scene calls Build() with its own list of ten sites (where each house stands, which picture and layout it uses).
/// Run from the menu instead, it builds eight houses in two rows of four next to the shops and links their street nodes together.
/// Delete 'Suburb Houses', 'PathNetwork_Houses' (and the 'SuburbHouseInterior_*' objects) to rebuild. Must live in a folder named "Editor".
/// </summary>
public static class SuburbHousesMenu
{
    /// <summary>One house to build: where it stands, which picture and inside layout it gets, and what its door calls it.</summary>
    public class Site
    {
        public Vector2 position;          // the middle of the front wall, on the ground
        public int picture = 1;           // sub_house_1 ... sub_house_10
        public int kind = 0;              // 0 Cosy, 1 Family, 2 Cosy mirrored, 3 Loft
        public string name = "House";     // shown on the door: "Enter <name>"
    }

    /// <summary>How far the door is from the middle of each picture, in tiles (picture 1 first). The two modern houses have their door left of centre.</summary>
    private static readonly float[] DoorOffsetX = { 0f, 0f, 0f, 0f, 0f, 0f, -0.625f, -0.625f, 0f, 0f };

    /// <summary>Where the front door of a house built from this picture is, measured from the middle of the house (tiles).</summary>
    public static float DoorOffset(int picture)
    {
        return picture >= 1 && picture <= DoorOffsetX.Length ? DoorOffsetX[picture - 1] : 0f;
    }

    private const string Root = "Assets/Farm/Suburb/Houses";
    private static string art;
    private static Dictionary<string, TileBase> tiles = new Dictionary<string, TileBase>();
    private static Dictionary<string, GameObject> furniture = new Dictionary<string, GameObject>();
    private static bool useDecor;

    // ------------------------------------------------------------------ interior layouts
    private struct P { public string prop; public float x, y; public P(string p, float x, float y) { prop = p; this.x = x; this.y = y; } }
    private struct Nd { public string id; public float x, y; public string spot; public Vector2 face; public Nd(string id, float x, float y, string spot = null, bool down = false) { this.id = id; this.x = x; this.y = y; this.spot = spot; face = down ? Vector2.down : Vector2.up; } }

    private class Layout
    {
        public string name; public int W, F; public float doorX;
        public List<FloorZone> zones = new List<FloorZone>();
        public List<PartitionRect> parts = new List<PartitionRect>();
        public List<P> props = new List<P>();
        public List<Nd> nodes = new List<Nd>();
        public List<string[]> chains = new List<string[]>();
        public List<float> windows = new List<float>();
    }

    private static FloorZone Z(string l, int x, int y, int w, int h, string tile)
    { return new FloorZone { label = l, area = new RectInt(x, y, w, h), tile = tiles[tile] }; }
    private static PartitionRect R(string l, int x, int y, int w, int h)
    { return new PartitionRect { label = l, area = new RectInt(x, y, w, h) }; }

    private static Layout Cosy(bool mirror)
    {
        var L = new Layout { name = mirror ? "Cosy (mirrored)" : "Cosy", W = 14, F = 10, doorX = 7f };
        L.zones.Add(Z("Kitchen", 0, 5, 7, 5, "floor_apt_kitchen")); L.zones.Add(Z("Bath", 10, 0, 4, 4, "floor_apt_bath")); L.zones.Add(Z("Bedroom", 8, 5, 6, 5, "floor_apt_carpet"));
        L.parts.Add(R("Bedroom wall (low)", 7, 4, 1, 1)); L.parts.Add(R("Bedroom wall (high)", 7, 7, 1, 3));       // doorway y 5-6
        L.parts.Add(R("Bedroom front", 8, 4, 6, 1)); L.parts.Add(R("Bath wall", 9, 0, 1, 2));                       // bath doorway y 2-3
        L.props.AddRange(new[] {
            new P("Apt_CounterCab", 1.0f, 7.8f), new P("Apt_CounterSink", 2.9f, 7.8f), new P("Apt_CounterStove", 4.8f, 7.8f), new P("Apt_Fridge", 6.3f, 7.8f),
            new P("Apt_RugLiving", 4.0f, 2.4f), new P("Apt_Sofa", 3.0f, 3.5f), new P("Apt_CoffeeTable", 3.0f, 1.9f), new P("Apt_Armchair", 5.8f, 2.3f),
            new P("Apt_Lamp", 0.7f, 3.4f), new P("Apt_Plant", 0.8f, 0.9f), new P("Apt_ShoeRack", 8.3f, 0.7f),
            new P("Apt_BedDouble", 11.0f, 8.0f), new P("Apt_Nightstand", 13.2f, 8.0f), new P("Apt_Wardrobe", 12.6f, 5.7f), new P("Apt_Plant", 8.8f, 5.8f),
            new P("Apt_Tub", 12.4f, 2.6f), new P("Apt_Toilet", 13.2f, 0.9f), new P("Apt_BathSink", 10.9f, 0.9f) });
        L.windows.AddRange(new[] { 3.5f, 11f });
        L.nodes.AddRange(new[] { new Nd("Door", 7f, 1.4f, "Door", true), new Nd("Living", 4.8f, 1.2f, "Living"), new Nd("Mid", 6.8f, 4.4f), new Nd("Kitchen", 3.8f, 6.3f, "Kitchen"),
            new Nd("BedDoor", 7.5f, 5.5f), new Nd("Bed", 10f, 6.6f, "Bed"), new Nd("BathDoor", 8.5f, 2.5f) });
        L.chains.Add(new[] { "Door", "Mid", "Kitchen" }); L.chains.Add(new[] { "Door", "Living" }); L.chains.Add(new[] { "Mid", "BedDoor", "Bed" }); L.chains.Add(new[] { "Door", "BathDoor" });
        return mirror ? Mirror(L) : L;
    }

    private static Layout Family()
    {
        var L = new Layout { name = "Family", W = 20, F = 14, doorX = 5f };
        L.zones.Add(Z("Kitchen", 0, 7, 10, 7, "floor_apt_kitchen")); L.zones.Add(Z("Bath", 16, 0, 4, 4, "floor_apt_bath"));
        L.zones.Add(Z("Bedroom A", 16, 5, 4, 4, "floor_apt_carpet")); L.zones.Add(Z("Bedroom B", 16, 10, 4, 4, "floor_apt_carpet"));
        L.parts.Add(R("Living|hall (low)", 10, 0, 1, 1)); L.parts.Add(R("Living|hall (high)", 10, 4, 1, 10));       // doorway y 1-3
        L.parts.Add(R("Right rooms (a)", 15, 0, 1, 2)); L.parts.Add(R("Right rooms (b)", 15, 4, 1, 2)); L.parts.Add(R("Right rooms (c)", 15, 8, 1, 3)); L.parts.Add(R("Right rooms (d)", 15, 13, 1, 1));
        L.parts.Add(R("Bath top", 16, 4, 4, 1)); L.parts.Add(R("Bedrooms divider", 16, 9, 4, 1));
        L.props.AddRange(new[] {
            new P("Apt_RugLiving", 4.0f, 2.6f), new P("Apt_Sofa", 3.0f, 4.6f), new P("Apt_CoffeeTable", 3.0f, 3.0f), new P("Apt_Armchair", 6.4f, 3.2f),
            new P("Apt_Lamp", 0.7f, 4.8f), new P("Apt_Plant", 0.8f, 0.9f), new P("Apt_Bookshelf", 8.4f, 5.2f),
            new P("Apt_CounterCab", 1.0f, 11.9f), new P("Apt_CounterSink", 2.9f, 11.9f), new P("Apt_CounterStove", 4.8f, 11.9f), new P("Apt_CounterCab", 6.7f, 11.9f), new P("Apt_Fridge", 8.5f, 11.9f),
            new P("Apt_DiningTable", 2.8f, 9.2f), new P("Apt_Chair", 1.1f, 9.4f), new P("Apt_Chair", 4.5f, 9.4f),
            new P("Apt_Console", 11.8f, 12.2f), new P("Apt_ShoeRack", 13.6f, 0.9f), new P("Apt_Plant", 14.0f, 12.2f),
            new P("Apt_Toilet", 16.8f, 0.8f), new P("Apt_BathSink", 18.9f, 0.8f), new P("Apt_Tub", 18.5f, 2.3f),
            new P("Apt_BedBlue", 18.0f, 6.2f), new P("Apt_Plant", 16.5f, 5.6f), new P("Apt_BedGreen", 18.0f, 11.2f), new P("Apt_Desk", 12.6f, 8.0f) });
        L.windows.AddRange(new[] { 3f, 8f, 13f, 17.5f });
        L.nodes.AddRange(new[] { new Nd("Door", 5f, 1.4f, "Door", true), new Nd("Living", 4.8f, 1.9f, "Living"), new Nd("Mid", 5.3f, 6f), new Nd("Kitchen", 5.6f, 10.5f, "Kitchen"),
            new Nd("HallDoor", 9.5f, 2f), new Nd("Hall", 12.5f, 2f), new Nd("HallMid", 12.5f, 6.5f), new Nd("HallTop", 12.5f, 11.5f),
            new Nd("BedADoor", 14.5f, 6.5f), new Nd("BedA", 16.7f, 5.8f, "Bed"), new Nd("BedBDoor", 14.5f, 11.5f), new Nd("BedB", 16.6f, 10.8f, "Bed2"), new Nd("BathDoor", 14.5f, 2.5f) });
        L.chains.Add(new[] { "Door", "Mid", "Kitchen" }); L.chains.Add(new[] { "Door", "Living" }); L.chains.Add(new[] { "Door", "HallDoor", "Hall", "HallMid", "HallTop" });
        L.chains.Add(new[] { "HallMid", "BedADoor", "BedA" }); L.chains.Add(new[] { "HallTop", "BedBDoor", "BedB" }); L.chains.Add(new[] { "Hall", "BathDoor" });
        return L;
    }

    private static Layout Loft()
    {
        var L = new Layout { name = "Loft", W = 12, F = 12, doorX = 6f };
        L.zones.Add(Z("Bath", 0, 0, 4, 4, "floor_apt_bath")); L.zones.Add(Z("Kitchen", 0, 6, 6, 6, "floor_apt_kitchen")); L.zones.Add(Z("Sleeping", 7, 6, 5, 6, "floor_apt_carpet"));
        L.parts.Add(R("Bath side", 4, 0, 1, 2)); L.parts.Add(R("Bath top", 0, 4, 5, 1));                              // doorway y 2-3
        L.props.AddRange(new[] {
            new P("Apt_CounterCab", 1.0f, 9.9f), new P("Apt_CounterSink", 2.9f, 9.9f), new P("Apt_CounterStove", 4.8f, 9.9f), new P("Apt_Fridge", 6.4f, 9.9f),
            new P("Apt_DiningTable", 2.5f, 7.0f), new P("Apt_Chair", 0.9f, 7.2f), new P("Apt_Chair", 4.1f, 7.2f),
            new P("Apt_RugLiving", 8.5f, 2.6f), new P("Apt_Sofa", 8.5f, 3.6f), new P("Apt_CoffeeTable", 8.5f, 2.0f), new P("Apt_Armchair", 10.8f, 2.4f), new P("Apt_Lamp", 11.4f, 4.2f),
            new P("Apt_BedDouble", 9.5f, 9.9f), new P("Apt_Nightstand", 11.6f, 9.9f), new P("Apt_Wardrobe", 11.0f, 6.6f),
            new P("Apt_Toilet", 1.0f, 0.9f), new P("Apt_Tub", 2.2f, 2.6f) });
        L.windows.AddRange(new[] { 3f, 9f });
        L.nodes.AddRange(new[] { new Nd("Door", 6f, 1.4f, "Door", true), new Nd("Living", 8.5f, 0.9f, "Living"), new Nd("Mid", 5.5f, 4.5f), new Nd("Kitchen", 3.8f, 8.7f, "Kitchen"), new Nd("Bed", 9.5f, 8.2f, "Bed") });
        L.chains.Add(new[] { "Door", "Living" }); L.chains.Add(new[] { "Door", "Mid", "Kitchen" }); L.chains.Add(new[] { "Mid", "Bed" });
        return L;
    }

    private static Layout Mirror(Layout L)
    {
        int W = L.W;
        foreach (FloorZone z in L.zones) z.area = new RectInt(W - z.area.x - z.area.width, z.area.y, z.area.width, z.area.height);
        foreach (PartitionRect p in L.parts) p.area = new RectInt(W - p.area.x - p.area.width, p.area.y, p.area.width, p.area.height);
        for (int i = 0; i < L.props.Count; i++) { P p = L.props[i]; L.props[i] = new P(p.prop, W - p.x, p.y); }
        for (int i = 0; i < L.nodes.Count; i++) { Nd n = L.nodes[i]; n.x = W - n.x; L.nodes[i] = n; }
        for (int i = 0; i < L.windows.Count; i++) L.windows[i] = W - L.windows[i];
        L.doorX = W - L.doorX;
        return L;
    }

    private static Layout MakeLayout(int kind)
    {
        switch (kind) { case 0: return Cosy(false); case 1: return Family(); case 2: return Cosy(true); default: return Loft(); }
    }

    // ------------------------------------------------------------------ menu
    [MenuItem("Farm/Set Up/Suburb Houses", false, 151)]
    public static void RunFromMenu()
    {
        // where to build: next to the shops if they are in the scene, otherwise the middle of the Scene view
        Vector3 origin = Vector3.zero;
        if (SceneView.lastActiveSceneView != null) origin = SceneView.lastActiveSceneView.pivot;
        GameObject hw = GameObject.Find("FranksStore"), gs = GameObject.Find("SuburbStore");
        if (hw != null) origin = hw.transform.position + new Vector3(14f, 0f, 0f);
        else if (gs != null) origin = gs.transform.position + new Vector3(14f, 0f, 0f);
        origin = new Vector3(Mathf.Round(origin.x), Mathf.Round(origin.y), 0f);

        // eight houses in two rows of four: picture 1-8, layouts Cosy, Family, Cosy mirrored, Loft, Family, Cosy mirrored, Loft, Cosy
        int[] kinds = { 0, 1, 2, 3, 1, 2, 3, 0 };
        var sites = new List<Site>();
        for (int i = 0; i < 8; i++)
            sites.Add(new Site { position = (Vector2)origin + new Vector2((i % 4) * 12f, -(i / 4) * 20f), picture = i + 1, kind = kinds[i], name = "House " + (i + 1) });

        string result = Build(sites);
        if (!result.StartsWith("Built")) { EditorUtility.DisplayDialog("Suburb houses", result, "OK"); return; }

        // streets: along each row, and a lane down the left side that joins the two rows
        Transform pathRoot = GameObject.Find("PathNetwork_Houses").transform;
        var streetNodes = new PathNode[8];
        for (int i = 0; i < 8; i++) streetNodes[i] = HardwareEditorUtil.FindNode("H" + (i + 1) + "_Street");
        for (int i = 0; i < 3; i++) PathToolsMenu.Link(streetNodes[i], streetNodes[i + 1]);
        for (int i = 4; i < 7; i++) PathToolsMenu.Link(streetNodes[i], streetNodes[i + 1]);
        PathNode laneA = PathToolsMenu.MakeNode("Lane_A", (Vector2)streetNodes[0].transform.position + new Vector2(-6f, 0f), pathRoot);
        PathNode laneB = PathToolsMenu.MakeNode("Lane_B", (Vector2)streetNodes[4].transform.position + new Vector2(-6f, 0f), pathRoot);
        PathToolsMenu.Link(laneA, streetNodes[0]); PathToolsMenu.Link(laneB, streetNodes[4]); PathToolsMenu.Link(laneA, laneB);
        // join the first street to the shops if they are in the scene
        PathNode hardware = HardwareEditorUtil.FindNode("Street_Hardware");
        if (hardware != null) PathToolsMenu.Link(hardware, laneA);
        PathNode.MarkDirty();

        EditorUtility.DisplayDialog("Suburb houses built", result +
            "\n\nDrag houses around freely, then run Farm > Level Editors > Path Nodes - Auto-Link Within Range if you move the street.", "OK");
    }

    /// <summary>
    /// Builds one house per site in the open scene, numbered H1, H2 ... in list order. Returns a sentence saying what happened
    /// (it starts with "Built" when it worked). No pop-ups. It does NOT link the street nodes (H1_Street ...) to anything:
    /// the caller joins them to its own street network.
    /// </summary>
    public static string Build(IList<Site> sites)
    {
        art = HardwareEditorUtil.FindFolder("sub_house_1");
        if (art == null) return "Couldn't find sub_house_1.png. The 'Houses' art folder belongs in Assets/Project/Art/Houses.";
        if (GameObject.Find("Suburb Houses") != null) return "'Suburb Houses' already exists in this scene. Delete it, 'PathNetwork_Houses' and the 'SuburbHouseInterior_*' objects to rebuild.";
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string tp = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(tp).StartsWith("sub_house_")) HardwareEditorUtil.ImportSingle(tp, SpriteAlignment.BottomCenter);
        }
        AssetDatabase.Refresh();

        foreach (string t in new[] { "floor_apt_wood", "floor_apt_kitchen", "floor_apt_bath", "floor_apt_carpet", "wall_apt_upper", "wall_apt_base", "partition_apt" })
        {
            TileBase tile = FindTile(t);
            if (tile == null) return "Couldn't find the tile '" + t + "'. Run Farm > Set Up > General Store first (it makes the apartment tiles and furniture).";
            tiles[t] = tile;
        }
        foreach (string f in new[] { "Apt_Sofa", "Apt_Armchair", "Apt_CoffeeTable", "Apt_RugLiving", "Apt_Bookshelf", "Apt_Lamp", "Apt_Plant", "Apt_CounterCab", "Apt_CounterSink", "Apt_CounterStove", "Apt_Fridge",
            "Apt_DiningTable", "Apt_Chair", "Apt_BedDouble", "Apt_BedBlue", "Apt_BedGreen", "Apt_Nightstand", "Apt_Wardrobe", "Apt_Desk", "Apt_Toilet", "Apt_BathSink", "Apt_Bathtub", "Apt_Console", "Apt_ShoeRack", "Apt_Window" })
        {
            GameObject pf = FindPrefab(f);
            if (pf == null) return "Couldn't find the furniture prefab '" + f + "'. Run Farm > Set Up > General Store first.";
            furniture[f] = pf;
        }
        furniture["Apt_Tub"] = furniture["Apt_Bathtub"];     // (short name used in the layouts)
        useDecor = InteriorDecorSetupMenu.Ready();           // furnish from the decor catalogue (Farm > Set Up > Interior Decor) when it exists

        // every inside is a room far to the right of the map, 100 tiles after the last one
        float nextX = 600f;
        foreach (HouseInterior h in Object.FindObjectsByType<HouseInterior>()) nextX = Mathf.Max(nextX, h.transform.position.x + 100f);

        var parent = new GameObject("Suburb Houses");
        Undo.RegisterCreatedObjectUndo(parent, "Suburb houses");
        Transform pathRoot = new GameObject("PathNetwork_Houses").transform;
        Undo.RegisterCreatedObjectUndo(pathRoot.gameObject, "House paths");

        for (int i = 0; i < sites.Count; i++)
            BuildHouse(i + 1, sites[i], nextX + i * 100f, parent.transform, pathRoot);
        PathNode.MarkDirty();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = parent;
        return "Built " + sites.Count + " houses. The insides start at x = " + nextX + ", 100 tiles apart.\n\n" +
               "Each has NpcSpots (H1Door, H1Living, H1Kitchen, H1Bed, and H1Bed2 in Family houses, plus H1Street outside) and path nodes, so any NPC with a schedule can live there: " +
               "use those spot ids in the NPC's schedule stops.\n\n" +
               "Doors are open 8 am to 9 pm; change that on each house's FrontDoor (Suburb House Door).";
    }

    // ------------------------------------------------------------------ one house
    private static PathNode BuildHouse(int n, Site site, float interiorX, Transform parent, Transform pathRoot)
    {
        Layout L = MakeLayout(site.kind);
        string tag = "H" + n;
        Vector3 pos = new Vector3(site.position.x, site.position.y, 0f);
        int picture = site.picture;
        float dx = DoorOffset(picture);                    // the door is not in the middle of every picture

        // ----- outside
        var ext = new GameObject("SuburbHouse_" + n);
        ext.transform.SetParent(parent, false);
        ext.transform.position = pos;
        ext.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 10;
        ext.AddComponent<BehindPlayerSorter>().thresholdOffset = 2.2f;
        var pic = new GameObject("Building"); pic.transform.SetParent(ext.transform, false);
        pic.AddComponent<SpriteRenderer>().sprite = HardwareEditorUtil.Spr(art, "sub_house_" + picture);
        var body = ext.AddComponent<BoxCollider2D>();
        body.size = new Vector2(7.2f, 4.4f); body.offset = new Vector2(0f, 2.2f);
        var door = Child(ext, "FrontDoor", new Vector3(dx, 0.4f, 0f));
        var dc = door.AddComponent<BoxCollider2D>(); dc.isTrigger = true; dc.size = new Vector2(1.8f, 1.4f);     // (the collider goes on first: SuburbHouseDoor needs one)
        var exitSpot = Child(ext, "ExitSpot", new Vector3(dx, -0.9f, 0f));
        Spot(Child(ext, "Spot_Street", new Vector3(dx, -1.6f, 0f)), tag + "Street", Vector2.up);

        // ----- inside
        var room = new GameObject("SuburbHouseInterior_" + n);
        Undo.RegisterCreatedObjectUndo(room, "House interior");
        room.transform.position = new Vector3(interiorX, 0f, 0f);
        room.AddComponent<Grid>();
        var interior = room.AddComponent<HouseInterior>();
        const int WALLROWS = 3;

        var back = Child(room, "Backdrop", new Vector3(L.W * 0.5f, (L.F + WALLROWS) * 0.5f, 0f));
        var bsr = back.AddComponent<SpriteRenderer>(); bsr.sprite = RuntimeBackdrop(); bsr.sortingOrder = -5; back.transform.localScale = new Vector3(100f, 80f, 1f);
        var floorGo = Child(room, "Floor", Vector3.zero); var floorMap = floorGo.AddComponent<Tilemap>(); floorGo.AddComponent<TilemapRenderer>().sortingOrder = 0;
        var wallGo = Child(room, "Wall", Vector3.zero); var wallMap = wallGo.AddComponent<Tilemap>(); wallGo.AddComponent<TilemapRenderer>().sortingOrder = 1;
        var partGo = Child(room, "Partitions", Vector3.zero); var partMap = partGo.AddComponent<Tilemap>(); partGo.AddComponent<TilemapRenderer>().sortingOrder = 4; partGo.AddComponent<TilemapCollider2D>();
        var edges = Child(room, "Edges", Vector3.zero);
        Box(edges, new Vector2(-0.5f, (L.F + WALLROWS) * 0.5f), new Vector2(1f, L.F + WALLROWS + 2f), false);
        Box(edges, new Vector2(L.W + 0.5f, (L.F + WALLROWS) * 0.5f), new Vector2(1f, L.F + WALLROWS + 2f), false);
        Box(edges, new Vector2(L.W * 0.5f, -0.5f), new Vector2(L.W + 2f, 1f), false);
        Box(edges, new Vector2(L.W * 0.5f, L.F + WALLROWS * 0.5f), new Vector2(L.W, WALLROWS), false);

        var so = new SerializedObject(interior);
        so.FindProperty("width").intValue = L.W; so.FindProperty("floorRows").intValue = L.F; so.FindProperty("wallRows").intValue = WALLROWS;
        so.FindProperty("floorTilemap").objectReferenceValue = floorMap; so.FindProperty("wallTilemap").objectReferenceValue = wallMap;
        so.ApplyModifiedProperties();

        var style = room.AddComponent<InteriorStyle>();
        style.floorMap = floorMap; style.wallMap = wallMap; style.partitionMap = partMap;
        style.floorTile = tiles["floor_apt_wood"]; style.wallUpper = tiles["wall_apt_upper"]; style.wallBase = tiles["wall_apt_base"]; style.partitionTile = tiles["partition_apt"];
        style.zones = L.zones.ToArray(); style.partitions = L.parts.ToArray();

        // furniture: the decor catalogue when it exists (see InteriorDecorLayouts.cs), otherwise the old apartment props
        if (!useDecor)
        {
            foreach (P p in L.props) Place(room, p.prop, p.x, p.y);
            foreach (float wx in L.windows) Place(room, "Apt_Window", wx, L.F + 1.4f);
        }

        var entry = Child(room, "EntrySpawn", new Vector3(L.doorX, 1.4f, 0f));
        var exitGo = Child(room, "ExitDoor", new Vector3(L.doorX, 0f, 0f));
        Box(exitGo, Vector2.zero, new Vector2(2.2f, 1.2f), true);
        var exitDoor = exitGo.AddComponent<HouseDoor>();
        HardwareEditorUtil.SetRef(exitDoor, "destination", exitSpot.transform);
        HardwareEditorUtil.SetString(exitDoor, "prompt", "Go outside");

        var sdoor = door.AddComponent<SuburbHouseDoor>();
        HardwareEditorUtil.SetString(sdoor, "houseName", string.IsNullOrEmpty(site.name) ? "House " + n : site.name);
        HardwareEditorUtil.SetRef(sdoor, "destination", entry.transform);
        HardwareEditorUtil.SetRef(sdoor, "enterInterior", interior);
        style.Apply();
        if (useDecor) Debug.Log(InteriorDecorTools.Furnish(interior, tag, InteriorDecorLayouts.ThemeForHouse(n), InteriorDecorSetupMenu.SuburbLayoutFor(interior), false));

        // ----- spots and paths inside
        var made = new Dictionary<string, PathNode>();
        foreach (Nd nd in L.nodes)
        {
            Vector2 wp = (Vector2)room.transform.position + new Vector2(nd.x, nd.y);
            made[nd.id] = PathToolsMenu.MakeNode(tag + "_" + nd.id, wp, pathRoot);
            if (nd.spot != null) { var s = Child(room, "Spot_" + nd.spot, new Vector3(nd.x, nd.y, 0f)); Spot(s, tag + nd.spot, nd.face); }
        }
        foreach (string[] chain in L.chains)
            for (int i = 0; i + 1 < chain.Length; i++) PathToolsMenu.Link(made[chain[i]], made[chain[i + 1]]);

        // ----- the street node outside, joined to the front door by a portal
        PathNode street = PathToolsMenu.MakeNode(tag + "_Street", (Vector2)pos + new Vector2(dx, -1.4f), pathRoot);
        street.portalTo = made["Door"]; made["Door"].portalTo = street;
        EditorUtility.SetDirty(street); EditorUtility.SetDirty(made["Door"]);
        return street;
    }

    private static Sprite backdropSprite;
    private static Sprite RuntimeBackdrop()
    {
        if (backdropSprite != null) return backdropSprite;
        string p = HardwareEditorUtil.FindFolder("backdrop_dark");
        backdropSprite = p != null ? HardwareEditorUtil.Spr(p, "backdrop_dark") : null;
        return backdropSprite;
    }

    // ------------------------------------------------------------------ helpers
    private static TileBase FindTile(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Tile"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<TileBase>(p);
        }
        return null;
    }

    private static GameObject FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
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
        c.offset = offset; c.size = size; c.isTrigger = trigger;
    }

    private static void Place(GameObject room, string prop, float x, float y)
    {
        GameObject prefab;
        if (!furniture.TryGetValue(prop, out prefab) || prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(room.transform, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
    }

    private static void Spot(GameObject go, string id, Vector2 facing)
    {
        var s = go.AddComponent<NpcSpot>();
        s.spotId = id; s.facing = facing;
    }
}
