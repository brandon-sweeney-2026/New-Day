using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>A patch of floor with its own tile (the kitchen's tiles, the bathroom...). Rectangle in tiles inside the room: X, Y, Width, Height.</summary>
[Serializable]
public class FloorZone
{
    public string label = "Zone";
    public RectInt area = new RectInt(0, 0, 4, 4);
    public TileBase tile;
}

/// <summary>An inside wall. Rectangle in tiles inside the room (a wall 1 thick is Width 1 or Height 1).</summary>
[Serializable]
public class PartitionRect
{
    public string label = "Wall";
    public RectInt area = new RectInt(0, 0, 1, 4);
}

/// <summary>
/// How a shop / apartment interior looks and what shape it is. Sits next to the HouseInterior on the room object.
/// "Apply Style" (button / right-click menu, or Farm > Set Up > Rebuild Selected Interior) clears and repaints:
///   floor with the default tile, then each Floor Zone on top, the back wall, the inside Partitions, and any Interior Blockers
/// (use Blockers to cut corners off the room so it isn't a plain rectangle).
/// To change the SIZE, change Width / Floor Rows / Wall Rows on the HouseInterior, then run Rebuild Selected Interior.
/// </summary>
[RequireComponent(typeof(HouseInterior))]
public class InteriorStyle : MonoBehaviour
{
    [Header("Tilemaps (made by the setup menu)")]
    public Tilemap floorMap;
    public Tilemap wallMap;
    public Tilemap partitionMap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase wallUpper;
    public TileBase wallBase;
    [Tooltip("Optional: the highest wall row (with crown moulding). Set by the Interior Decorator's wall coverings.")]
    public TileBase wallTop;
    public TileBase partitionTile;
    [Tooltip("What fills the corners cut off by Interior Blockers.")] public TileBase blockerTile;

    [Header("Floor patches (painted over the default floor)")]
    public FloorZone[] zones;

    [Header("Inside walls")]
    public PartitionRect[] partitions;

    [ContextMenu("Apply Style")]
    public void Apply()
    {
        var interior = GetComponent<HouseInterior>();
        if (floorMap != null) floorMap.ClearAllTiles();
        if (wallMap != null) wallMap.ClearAllTiles();
        if (partitionMap != null) partitionMap.ClearAllTiles();

        interior.PaintFloor(floorTile);
        interior.PaintWall(wallUpper, wallBase, wallTop);

        if (zones != null && floorMap != null)
            foreach (FloorZone z in zones)
                if (z != null && z.tile != null) Fill(floorMap, z.area, z.tile, interior);

        if (partitions != null && partitionMap != null && partitionTile != null)
            foreach (PartitionRect p in partitions)
                if (p != null) Fill(partitionMap, p.area, partitionTile, interior);

        foreach (InteriorBlocker b in GetComponentsInChildren<InteriorBlocker>())
            b.Apply(floorMap, wallMap, blockerTile != null ? blockerTile : wallUpper);
    }

    private static void Fill(Tilemap map, RectInt r, TileBase tile, HouseInterior interior)
    {
        for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                if (x < 0 || y < 0 || x >= interior.width || y >= interior.floorRows) continue;
                map.SetTile(new Vector3Int(x, y, 0), tile);
            }
    }
}
