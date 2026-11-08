using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// The inside of the house. It is a small room built far away from the farm (the Setup menu places it);
/// the door teleports the player in and out, and the camera is limited to this room while inside.
///
/// The room has: a floor tilemap, a wall tilemap (the back wall) and a slot for each piece of customizable furniture.
/// Slots are plain child objects with a SpriteRenderer, so you can move them in the Scene view to rearrange the room.
/// Put this on the "HouseInterior" object, which also has a Grid component (cell size 1).
/// </summary>
public class HouseInterior : MonoBehaviour
{
    [Serializable]
    public class FurnitureSlot
    {
        public HouseCategory category;
        public SpriteRenderer renderer;
    }

    [Header("Room size (in tiles)")]
    [Min(4)] public int width = 20;
    [Min(2)] public int floorRows = 20;
    [Min(1)] public int wallRows = 3;

    [Header("References")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private FurnitureSlot[] slots;

    private static HouseInterior current;

    /// <summary>The room rectangle in world space (used to limit the camera).</summary>
    public Rect WorldBounds
    {
        get { return new Rect(transform.position.x, transform.position.y, width, floorRows + wallRows); }
    }

    // ------------------------------------------------------------------
    // Painting
    // ------------------------------------------------------------------

    public void PaintFloor(TileBase tile)
    {
        if (floorTilemap == null) return;
        for (int y = 0; y < floorRows; y++)
            for (int x = 0; x < width; x++)
                floorTilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }

    /// <summary>'baseTile' is the row that meets the floor (with the skirting board); 'upper' fills the rest.</summary>
    public void PaintWall(TileBase upper, TileBase baseTile)
    {
        if (wallTilemap == null) return;
        for (int r = 0; r < wallRows; r++)
        {
            TileBase t = r == 0 ? (baseTile != null ? baseTile : upper) : upper;
            for (int x = 0; x < width; x++)
                wallTilemap.SetTile(new Vector3Int(x, floorRows + r, 0), t);
        }
    }

    public void SetFurniture(HouseCategory category, Sprite sprite)
    {
        if (slots == null) return;
        foreach (FurnitureSlot s in slots)
        {
            if (s == null || s.renderer == null || s.category != category) continue;
            s.renderer.sprite = sprite;
            s.renderer.enabled = sprite != null;
        }
    }

    // ------------------------------------------------------------------
    // Being inside
    // ------------------------------------------------------------------

    /// <summary>Limits the camera to this room.</summary>
    public void EnterRoom()
    {
        current = this;
        CameraFollow2D cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.SetBoundsOverride(WorldBounds);
    }

    /// <summary>Frees the camera again (back on the farm).</summary>
    public void ExitRoom()
    {
        LeaveAnyRoom();
    }

    public static void LeaveAnyRoom()
    {
        current = null;
        CameraFollow2D cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.ClearBoundsOverride();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Rect r = WorldBounds;
        Gizmos.DrawWireCube(r.center, r.size);
    }
}
