using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Editor helpers for the decor catalogue, shared by Farm > Set Up > Interior Decor, the Interior Decorator window
/// and the scene builders (farmhouse, General Store apartment, suburb houses):
///   - place / turn / swap / remove pieces (all undoable),
///   - save a room into its InteriorLayout asset and build a room back from it,
///   - paint the room's floor, floor patches and wall covering,
///   - build the "room frame": the thick wall tops around the room and the soft shadows the walls throw on the floor.
/// This file must live in a folder named "Editor".
/// </summary>
public static class InteriorDecorTools
{
    public const string Root = "Assets/Farm/Interiors";
    public const string CatalogPath = Root + "/DecorCatalog.asset";
    public const string LayoutFolder = Root + "/Layouts";
    public const string ArtFolder = "Assets/Project/Art/Interior";

    /// <summary>The catalogue asset (null until Farm > Set Up > Interior Decor has run).</summary>
    public static DecorCatalog Catalog { get { return AssetDatabase.LoadAssetAtPath<DecorCatalog>(CatalogPath); } }

    // ------------------------------------------------------------------ rooms

    /// <summary>Adds a DecorRoom to the interior if it has none, and gives it its room id.</summary>
    public static DecorRoom EnsureRoom(HouseInterior interior, string roomId)
    {
        DecorRoom room = interior.GetComponent<DecorRoom>();
        if (room == null) room = Undo.AddComponent<DecorRoom>(interior.gameObject);
        if (!string.IsNullOrEmpty(roomId) && room.roomId != roomId) { Undo.RecordObject(room, "Room id"); room.roomId = roomId; }
        return room;
    }

    /// <summary>The room's layout asset; makes an empty one (Assets/Farm/Interiors/Layouts/&lt;roomId&gt;.asset) if 'create' is on.</summary>
    public static InteriorLayout LayoutFor(DecorRoom room, bool create)
    {
        if (room.layout != null) return room.layout;
        string path = LayoutFolder + "/" + Safe(room.roomId) + ".asset";
        var layout = AssetDatabase.LoadAssetAtPath<InteriorLayout>(path);
        if (layout == null && create)
        {
            EnsureFolder(LayoutFolder);
            layout = ScriptableObject.CreateInstance<InteriorLayout>();
            AssetDatabase.CreateAsset(layout, path);
        }
        if (layout != null) { Undo.RecordObject(room, "Room layout"); room.layout = layout; EditorUtility.SetDirty(room); }
        return layout;
    }

    public static string Safe(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Room";
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
        return id;
    }

    // ------------------------------------------------------------------ pieces

    /// <summary>Puts a piece in the room. 'localPos' = bottom-left corner of its floor space, in tiles from the room's corner.</summary>
    public static DecorPiece Place(DecorRoom room, DecorItem item, Vector2 localPos, DecorFacing facing, bool undo)
    {
        if (room == null || item == null || item.IsSurface) return null;
        if (!item.CanRotate) facing = DecorFacing.Front;
        var go = new GameObject(item.displayName);
        if (undo) Undo.RegisterCreatedObjectUndo(go, "Place " + item.displayName);
        go.transform.SetParent(room.PiecesRoot, false);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        var piece = go.AddComponent<DecorPiece>();
        piece.item = item; piece.facing = facing;
        SettleOnHost(room, piece);
        piece.Refresh();
        return piece;
    }

    /// <summary>Surface pieces (blenders, toasters): find the counter or table under them and sit on its top.</summary>
    public static void SettleOnHost(DecorRoom room, DecorPiece piece)
    {
        if (piece.item == null || piece.item.mount != DecorMount.Surface) return;
        Vector2 cell = Snap(piece.transform.localPosition, false);
        DecorPiece host = null;
        foreach (DecorPiece p in room.PiecesRoot.GetComponentsInChildren<DecorPiece>())
        {
            if (p == piece || p.item == null || p.item.mount != DecorMount.Floor || p.item.surfaceHeight <= 0) continue;
            Vector2 hp = p.transform.localPosition; Vector2 hs = p.Size;
            if (cell.x + 0.5f > hp.x && cell.x + 0.5f < hp.x + hs.x && cell.y + 0.5f > hp.y && cell.y + 0.5f < hp.y + hs.y) { host = p; break; }
        }
        piece.lift = host != null ? host.item.surfaceHeight : 0;
        piece.hostDepth = host != null ? host.Size.y : 1;
        // sit on the host's front row, a hair lower so it is drawn in front of the host
        float y = host != null ? Mathf.Round(host.transform.localPosition.y) - 0.01f : cell.y;
        piece.transform.localPosition = new Vector3(cell.x, y, 0f);
    }

    /// <summary>Turns a piece a quarter turn (undoable).</summary>
    public static void Turn(DecorPiece piece, bool clockwise)
    {
        if (piece == null || piece.item == null || !piece.item.CanRotate) return;
        Undo.RecordObject(piece, "Turn " + piece.item.displayName);
        Undo.RecordObject(piece.transform, "Turn");
        RecordParts(piece);
        piece.facing = DecorItem.Turn(piece.facing, clockwise);
        piece.Refresh();
        EditorUtility.SetDirty(piece);
    }

    /// <summary>Swaps a piece for another catalogue item, keeping its place and facing (undoable).</summary>
    public static void Swap(DecorPiece piece, DecorItem item)
    {
        if (piece == null || item == null || item.IsSurface) return;
        Undo.RecordObject(piece, "Swap decor");
        Undo.RecordObject(piece.gameObject, "Swap decor");
        RecordParts(piece);
        piece.item = item;
        piece.gameObject.name = item.displayName;
        if (!item.CanRotate) piece.facing = DecorFacing.Front;
        DecorRoom room = piece.GetComponentInParent<DecorRoom>();
        if (room != null) SettleOnHost(room, piece);
        piece.Refresh();
        EditorUtility.SetDirty(piece);
    }

    private static void RecordParts(DecorPiece piece)
    {
        if (piece.Art != null) { Undo.RecordObject(piece.Art, "Decor"); Undo.RecordObject(piece.Art.transform, "Decor"); }
        var box = piece.GetComponent<BoxCollider2D>(); if (box != null) Undo.RecordObject(box, "Decor");
        var grp = piece.GetComponent<UnityEngine.Rendering.SortingGroup>(); if (grp != null) Undo.RecordObject(grp, "Decor");
    }

    /// <summary>Rounds a position to the tile grid (whole tiles; half tiles for wall pieces).</summary>
    public static Vector2 Snap(Vector2 p, bool half)
    {
        float step = half ? 0.5f : 1f;
        return new Vector2(Mathf.Round(p.x / step) * step, Mathf.Round(p.y / step) * step);
    }

    // ------------------------------------------------------------------ layouts

    /// <summary>Writes the room's pieces (and nothing else) into its layout asset.</summary>
    public static void SaveLayout(DecorRoom room)
    {
        InteriorLayout layout = LayoutFor(room, true);
        Undo.RecordObject(layout, "Save room layout");
        layout.pieces.Clear();
        foreach (DecorPiece p in room.PiecesRoot.GetComponentsInChildren<DecorPiece>())
        {
            if (p.item == null) continue;
            bool half = p.item.mount == DecorMount.Wall;
            layout.pieces.Add(new InteriorLayout.Entry { item = p.item, position = Snap(p.transform.localPosition, half), facing = p.facing });
        }
        EditorUtility.SetDirty(layout);
    }

    /// <summary>Clears the room's pieces and builds them again from the layout, repaints the floor and walls, and rebuilds the frame.</summary>
    public static void ApplyLayout(DecorRoom room, bool undo)
    {
        InteriorLayout layout = LayoutFor(room, false);
        Transform rootT = room.PiecesRoot;
        for (int i = rootT.childCount - 1; i >= 0; i--)
        {
            if (undo) Undo.DestroyObjectImmediate(rootT.GetChild(i).gameObject);
            else UnityEngine.Object.DestroyImmediate(rootT.GetChild(i).gameObject);
        }
        if (layout != null)
        {
            // floor pieces first, so surface pieces find the counter they stand on
            foreach (InteriorLayout.Entry e in layout.pieces)
                if (e != null && e.item != null && e.item.mount != DecorMount.Surface) Place(room, e.item, e.position, e.facing, undo);
            foreach (InteriorLayout.Entry e in layout.pieces)
                if (e != null && e.item != null && e.item.mount == DecorMount.Surface) Place(room, e.item, e.position, e.facing, undo);
        }
        PaintSurfaces(room);
        BuildFrame(room);
        EditorUtility.SetDirty(room);
    }

    /// <summary>Paints the floor, the floor patches and the wall covering chosen in the layout (rooms that use the HouseManager skip this).</summary>
    public static void PaintSurfaces(DecorRoom room)
    {
        InteriorLayout layout = room.layout;
        if (layout == null || room.surfacesFromHouseManager) return;
        var interior = room.GetComponent<HouseInterior>();
        var style = room.GetComponent<InteriorStyle>();
        if (style != null)
        {
            Undo.RecordObject(style, "Paint room");
            if (style.floorMap != null) Undo.RecordObject(style.floorMap, "Paint room");
            if (style.wallMap != null) Undo.RecordObject(style.wallMap, "Paint room");
            if (style.partitionMap != null) Undo.RecordObject(style.partitionMap, "Paint room");
            if (layout.floor != null && layout.floor.floorTile != null) style.floorTile = layout.floor.floorTile;
            if (layout.wallCovering != null && layout.wallCovering.wallUpper != null)
            {
                style.wallUpper = layout.wallCovering.wallUpper; style.wallBase = layout.wallCovering.wallBase; style.wallTop = layout.wallCovering.wallTop;
            }
            if (style.zones != null)
                foreach (FloorZone z in style.zones)
                    foreach (InteriorLayout.ZoneFloor zf in layout.zoneFloors)
                        if (z != null && zf != null && zf.floor != null && zf.floor.floorTile != null && zf.zoneLabel == z.label) z.tile = zf.floor.floorTile;
            style.Apply();
            EditorUtility.SetDirty(style);
        }
        else
        {
            var so = new SerializedObject(interior);
            var floorMap = so.FindProperty("floorTilemap").objectReferenceValue as Tilemap;
            var wallMap = so.FindProperty("wallTilemap").objectReferenceValue as Tilemap;
            if (floorMap != null) Undo.RecordObject(floorMap, "Paint room");
            if (wallMap != null) Undo.RecordObject(wallMap, "Paint room");
            if (layout.floor != null && layout.floor.floorTile != null) interior.PaintFloor(layout.floor.floorTile);
            if (layout.wallCovering != null && layout.wallCovering.wallUpper != null) interior.PaintWall(layout.wallCovering.wallUpper, layout.wallCovering.wallBase, layout.wallCovering.wallTop);
        }
    }

    // ------------------------------------------------------------------ the room frame (depth)

    private static Sprite RoomSprite(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/Room/" + name + ".png"); }

    /// <summary>
    /// Builds the child "RoomFrame": a thick wall top drawn all round the room (with a gap at each door in the front wall),
    /// the shadow the back wall throws on the floor, and soft shadows along the side walls. This is what makes the room
    /// look like a box with walls instead of a flat sheet. Safe to run again (it replaces the old frame).
    /// </summary>
    public static void BuildFrame(DecorRoom room)
    {
        Sprite capH = RoomSprite("room_cap_h"), capV = RoomSprite("room_cap_v"), wallShadow = RoomSprite("room_wall_shadow"), sideShadow = RoomSprite("room_side_shadow");
        if (capH == null || capV == null) return;
        var interior = room.GetComponent<HouseInterior>();
        float W = interior.width, F = interior.floorRows, WR = interior.wallRows, T = 6f / 16f;

        Transform old = room.transform.Find("RoomFrame");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var frame = new GameObject("RoomFrame");
        Undo.RegisterCreatedObjectUndo(frame, "Room frame");
        frame.transform.SetParent(room.transform, false);

        // shadows on the floor
        if (wallShadow != null) Strip(frame, "BackWallShadow", wallShadow, new Vector2(0f, F - 1f), new Vector2(W, 1f), 1, false, false);
        if (sideShadow != null)
        {
            Strip(frame, "LeftWallShadow", sideShadow, new Vector2(0f, 0f), new Vector2(1f, F), 1, false, false);
            Strip(frame, "RightWallShadow", sideShadow, new Vector2(W, 0f), new Vector2(1f, F), 1, true, false);
        }

        // wall tops: left, right, top, and the front wall with gaps for the doors
        Strip(frame, "WallTop_Left", capV, new Vector2(-T, -T), new Vector2(T, F + WR + 2 * T), 40, false, false);
        Strip(frame, "WallTop_Right", capV, new Vector2(W + T, -T), new Vector2(T, F + WR + 2 * T), 40, true, false);
        Strip(frame, "WallTop_Back", capH, new Vector2(0f, F + WR + T), new Vector2(W, T), 40, false, true);

        var gaps = new List<Vector2>();
        foreach (HouseDoor d in room.GetComponentsInChildren<HouseDoor>())
        {
            Vector3 lp = room.transform.InverseTransformPoint(d.transform.position);
            if (lp.y < 1.5f) gaps.Add(new Vector2(lp.x - 1f, lp.x + 1f));
        }
        gaps.Sort((a, b) => a.x.CompareTo(b.x));
        float x = 0f; int n = 0;
        foreach (Vector2 g in gaps)
        {
            if (g.x > x) Strip(frame, "WallTop_Front" + (n++), capH, new Vector2(x, -T), new Vector2(g.x - x, T), 40, false, false);
            x = Mathf.Max(x, g.y);
        }
        if (W > x) Strip(frame, "WallTop_Front" + n, capH, new Vector2(x, -T), new Vector2(W - x, T), 40, false, false);
    }

    /// <summary>A tiled strip. 'pos' is its bottom-left corner (or bottom-right when flipped across, top-left when flipped up).</summary>
    private static void Strip(GameObject parent, string name, Sprite sprite, Vector2 pos, Vector2 size, int order, bool flipAcross, bool flipUp)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(flipAcross ? -1f : 1f, flipUp ? -1f : 1f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.sortingOrder = order;
    }

    // ------------------------------------------------------------------ furnishing a room from its layout (used by the builders)

    /// <summary>
    /// Makes 'interior' a decor room called 'roomId' and furnishes it. If the layout asset already exists it is used as it is
    /// (so your redecorating is kept); otherwise 'defaults' fills a new one. Old apartment props (objects named Apt_...) are removed.
    /// Returns a short report.
    /// </summary>
    public static string Furnish(HouseInterior interior, string roomId, string theme, Action<LayoutBuilder> defaults, bool surfacesFromHouseManager)
    {
        DecorCatalog catalog = Catalog;
        if (catalog == null || catalog.items.Count == 0) return roomId + ": no decor catalogue yet (run Farm > Set Up > Interior Decor).";

        DecorRoom room = EnsureRoom(interior, roomId);
        room.surfacesFromHouseManager = surfacesFromHouseManager;

        int removed = 0;
        for (int i = interior.transform.childCount - 1; i >= 0; i--)
        {
            Transform c = interior.transform.GetChild(i);
            if (c.name.StartsWith("Apt_")) { Undo.DestroyObjectImmediate(c.gameObject); removed++; }
        }

        string path = LayoutFolder + "/" + Safe(roomId) + ".asset";
        var layout = AssetDatabase.LoadAssetAtPath<InteriorLayout>(path);
        bool fresh = layout == null;
        if (fresh)
        {
            EnsureFolder(LayoutFolder);
            layout = ScriptableObject.CreateInstance<InteriorLayout>();
            var b = new LayoutBuilder(layout, catalog, theme, interior.width, interior.floorRows);
            if (defaults != null) defaults(b);
            int dropped = DropBlocked(room, layout);
            AssetDatabase.CreateAsset(layout, path);
            if (dropped > 0) Debug.Log(roomId + ": left out " + dropped + " starting piece(s) that would sit on something already in the room.");
        }
        room.layout = layout;
        ApplyLayout(room, true);
        return roomId + ": " + (fresh ? "furnished from the starting layout" : "rebuilt from its saved layout") + " (" + layout.pieces.Count + " pieces" + (removed > 0 ? ", " + removed + " old props removed" : "") + ").";
    }

    /// <summary>Removes starting pieces that would stand on an inside wall or on furniture that isn't from the catalogue (the farmhouse bed and stove...).</summary>
    private static int DropBlocked(DecorRoom room, InteriorLayout layout)
    {
        var blocked = new List<Rect>();
        Transform rt = room.transform;
        Physics2D.SyncTransforms();      // so colliders made a moment ago report the right size
        foreach (Collider2D c in rt.GetComponentsInChildren<Collider2D>())
        {
            if (c.isTrigger || c is TilemapCollider2D || c is CompositeCollider2D) continue;
            if (c.transform.IsChildOf(room.PiecesRoot) || c.transform.name == "Edges") continue;
            Bounds b = c.bounds;
            Vector3 min = rt.InverseTransformPoint(b.min), max = rt.InverseTransformPoint(b.max);
            blocked.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }
        var style = room.GetComponent<InteriorStyle>();
        if (style != null && style.partitions != null)
            foreach (PartitionRect p in style.partitions) if (p != null) blocked.Add(new Rect(p.area.x, p.area.y, p.area.width, p.area.height));

        int dropped = 0;
        for (int i = layout.pieces.Count - 1; i >= 0; i--)
        {
            InteriorLayout.Entry e = layout.pieces[i];
            if (e.item == null) { layout.pieces.RemoveAt(i); continue; }
            Vector2 s = e.item.SizeFor(e.facing);
            // anything that would stick out of the room (a starting layout made for a bigger room) is left out
            var hi = room.GetComponent<HouseInterior>();
            float top = e.item.mount == DecorMount.Wall ? hi.floorRows + hi.wallRows : hi.floorRows;
            if (e.position.x < 0f || e.position.y < 0f || e.position.x + s.x > hi.width + 0.01f || e.position.y + s.y > top + 0.01f) { layout.pieces.RemoveAt(i); dropped++; continue; }
            if (e.item.mount != DecorMount.Floor) continue;
            var r = new Rect(e.position.x + 0.1f, e.position.y + 0.1f, s.x - 0.2f, s.y - 0.2f);
            foreach (Rect b in blocked) if (r.Overlaps(b)) { layout.pieces.RemoveAt(i); dropped++; break; }
        }
        return dropped;
    }

    // ------------------------------------------------------------------ small helpers

    public static void EnsureFolder(string path)
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

/// <summary>
/// Fills a new InteriorLayout in code: "put the couch of this room's theme here, facing that way".
/// Positions are the bottom-left corner of the piece's floor space, in tiles from the room's bottom-left corner.
/// Wall pieces are placed with Wall(...), measured up from the top of the floor.
/// </summary>
public class LayoutBuilder
{
    public readonly InteriorLayout layout;
    public readonly DecorCatalog catalog;
    public readonly string theme;
    public readonly int W, F;

    public LayoutBuilder(InteriorLayout layout, DecorCatalog catalog, string theme, int width, int floorRows)
    {
        this.layout = layout; this.catalog = catalog; this.theme = theme; W = width; F = floorRows;
        layout.theme = theme;
    }

    /// <summary>The theme's piece of a category, at x, y.</summary>
    public LayoutBuilder Add(DecorCategory cat, float x, float y, DecorFacing facing = DecorFacing.Front)
    {
        return Put(catalog.Pick(cat, theme), x, y, facing);
    }

    /// <summary>A particular piece by id (e.g. "toilet"), at x, y.</summary>
    public LayoutBuilder Item(string id, float x, float y, DecorFacing facing = DecorFacing.Front)
    {
        return Put(catalog.ById(id), x, y, facing);
    }

    /// <summary>A wall piece of a category: x along the wall, 'up' = tiles above the top of the floor.</summary>
    public LayoutBuilder Wall(DecorCategory cat, float x, float up)
    {
        return Put(catalog.Pick(cat, theme), x, F + up, DecorFacing.Front);
    }

    /// <summary>A wall piece by id.</summary>
    public LayoutBuilder WallItem(string id, float x, float up)
    {
        return Put(catalog.ById(id), x, F + up, DecorFacing.Front);
    }

    private LayoutBuilder Put(DecorItem item, float x, float y, DecorFacing facing)
    {
        if (item == null) return this;
        layout.pieces.Add(new InteriorLayout.Entry { item = item, position = new Vector2(x, y), facing = item.CanRotate ? facing : DecorFacing.Front });
        return this;
    }

    public LayoutBuilder Floor(string id) { layout.floor = catalog.ById(id); return this; }
    public LayoutBuilder WallCovering(string id) { layout.wallCovering = catalog.ById(id); return this; }
    public LayoutBuilder Zone(string label, string floorId)
    {
        DecorItem f = catalog.ById(floorId);
        if (f != null) layout.zoneFloors.Add(new InteriorLayout.ZoneFloor { zoneLabel = label, floor = f });
        return this;
    }

    /// <summary>The theme's floor / wall covering ids.</summary>
    public string ThemeFloor { get { switch (theme) { case "cottage": return "floor_oak"; case "modern": return "floor_concrete"; case "rustic": return "floor_barnwood"; case "retro": return "floor_parquet"; default: return "floor_whitewash"; } } }
    public string ThemeWall { get { switch (theme) { case "cottage": return "wall_cottage"; case "modern": return "wall_modern"; case "rustic": return "wall_rustic"; case "retro": return "wall_retro"; default: return "wall_coastal"; } } }
    public string ThemeKitchenFloor { get { switch (theme) { case "retro": return "floor_checker"; case "modern": return "floor_concrete"; case "coastal": return "floor_mosaic"; default: return "floor_terracotta"; } } }

    /// <summary>Mirrors everything left to right (used for the mirrored Cosy house).</summary>
    public void Mirror()
    {
        foreach (InteriorLayout.Entry e in layout.pieces)
        {
            Vector2 s = e.item.SizeFor(e.facing);
            e.position = new Vector2(W - e.position.x - s.x, e.position.y);
            if (e.facing == DecorFacing.Left) e.facing = DecorFacing.Right; else if (e.facing == DecorFacing.Right) e.facing = DecorFacing.Left;
        }
    }
}
