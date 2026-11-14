using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > House > Rebuild Room After Resizing
///
/// After you change Width / Floor Rows / Wall Rows on the HouseInterior component, run this. It:
///  1. wipes the old floor and wall tiles (so nothing is left behind outside the new edge)
///  2. repaints them across the new size using your default Floor and Wall from the HouseManager catalog
///  3. puts the default furniture sprites back in their slots
///  4. resizes the invisible edge walls and moves the door, doormat, spawn point and backdrop to match
///  5. pulls any furniture that ended up outside the room back inside
/// Nothing in your other scripts changes. Ctrl+Z undoes it. (In Play mode, buying a floor/wall repaints the new size by itself.)
/// </summary>
public static class HouseRoomRebuildMenu
{
    [MenuItem("Farm/House/Rebuild Room After Resizing")]
    public static void Run()
    {
        var interior = Object.FindAnyObjectByType<HouseInterior>();
        if (interior == null)
        {
            EditorUtility.DisplayDialog("No house interior", "Couldn't find a HouseInterior in the open scene.", "OK");
            return;
        }

        var so = new SerializedObject(interior);
        var floor = so.FindProperty("floorTilemap").objectReferenceValue as Tilemap;
        var wall = so.FindProperty("wallTilemap").objectReferenceValue as Tilemap;

        Undo.SetCurrentGroupName("Rebuild house room");
        int group = Undo.GetCurrentGroup();
        if (floor != null) { Undo.RecordObject(floor, "Rebuild room"); floor.ClearAllTiles(); }
        if (wall != null) { Undo.RecordObject(wall, "Rebuild room"); wall.ClearAllTiles(); }

        // default options come from the HouseManager's catalog
        var mgr = Object.FindAnyObjectByType<HouseManager>();
        int painted = 0;
        if (mgr != null)
        {
            SerializedProperty catalog = new SerializedObject(mgr).FindProperty("catalog");
            for (int i = 0; i < catalog.arraySize; i++)
            {
                var o = catalog.GetArrayElementAtIndex(i).objectReferenceValue as HouseUpgradeData;
                if (o == null || !o.isDefault) continue;
                switch (o.category)
                {
                    case HouseCategory.Floor: interior.PaintFloor(o.tile); painted++; break;
                    case HouseCategory.Wall: interior.PaintWall(o.tile, o.tileBase); painted++; break;
                    case HouseCategory.Siding: case HouseCategory.Roof: break;
                    default: interior.SetFurniture(o.category, o.sprite); break;
                }
            }
        }

        // keep furniture inside the room
        int moved = 0;
        Rect r = interior.WorldBounds;
        SerializedProperty slots = so.FindProperty("slots");
        for (int i = 0; slots != null && i < slots.arraySize; i++)
        {
            var sr = slots.GetArrayElementAtIndex(i).FindPropertyRelative("renderer").objectReferenceValue as SpriteRenderer;
            if (sr == null) continue;
            Vector3 p = sr.transform.position;
            float nx = Mathf.Clamp(p.x, r.xMin + 0.5f, r.xMax - 0.5f);
            float ny = Mathf.Clamp(p.y, r.yMin + 0.5f, r.yMin + interior.floorRows - 0.5f);
            if (!Mathf.Approximately(nx, p.x) || !Mathf.Approximately(ny, p.y))
            {
                Undo.RecordObject(sr.transform, "Rebuild room");
                sr.transform.position = new Vector3(nx, ny, p.z);
                moved++;
            }
        }
        // the invisible walls, door, doormat, spawn and backdrop were built for the original size - move them to the new one
        Transform room = interior.transform;
        float W = interior.width, FL = interior.floorRows, WR = interior.wallRows;
        Transform edges = room.Find("Edges");
        if (edges != null)
        {
            BoxCollider2D[] b = edges.GetComponents<BoxCollider2D>();      // left, right, bottom, back wall (same order the setup menu builds them)
            if (b.Length >= 4)
            {
                Undo.RecordObjects(b, "Rebuild room");
                b[0].offset = new Vector2(-0.5f, (FL + WR) * 0.5f);   b[0].size = new Vector2(1f, FL + WR + 2f);
                b[1].offset = new Vector2(W + 0.5f, (FL + WR) * 0.5f); b[1].size = new Vector2(1f, FL + WR + 2f);
                b[2].offset = new Vector2(W * 0.5f, -0.5f);            b[2].size = new Vector2(W + 2f, 1f);
                b[3].offset = new Vector2(W * 0.5f, FL + WR * 0.5f);   b[3].size = new Vector2(W, WR);
            }
        }
        Place(room, "Backdrop", new Vector3(W * 0.5f, (FL + WR) * 0.5f, 0f));
        Place(room, "EntrySpawn", new Vector3(Mathf.Floor(W * 0.5f) + 0.5f, 1.2f, 0f));
        Place(room, "Doormat", new Vector3(Mathf.Floor(W * 0.5f), 0f, 0f));
        Place(room, "ExitDoor", new Vector3(Mathf.Floor(W * 0.5f) + 0.5f, 0f, 0f));
        Place(room, "Slot_WallArt", new Vector3(Mathf.Floor(W * 0.5f), FL, 0f));
        Undo.CollapseUndoOperations(group);

        EditorUtility.DisplayDialog("Room rebuilt",
            "Room is now " + interior.width + " x " + (interior.floorRows + interior.wallRows) + " tiles.\n" +
            (painted < 2 ? "\nWarning: couldn't find a default Floor and Wall in the HouseManager catalog, so some tiles weren't repainted. Press Play once, or buy a floor/wall, to paint them.\n" : "") +
            "\nFurniture moved back inside: " + moved, "OK");
    }

    private static void Place(Transform room, string childName, Vector3 localPos)
    {
        Transform t = room.Find(childName);
        if (t == null) return;
        Undo.RecordObject(t, "Rebuild room");
        t.localPosition = localPos;
    }
}
