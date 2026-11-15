using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Cuts a rectangle out of a shop / apartment so the room can be L-shaped, T-shaped, have a pillar or a back office.
/// Put it on a child of the interior, move it, drag its box handles (Scene view), then run Farm > Set Up > Rebuild Selected Interior.
/// The area is filled with wall and becomes solid. Snaps to the tile grid.
/// </summary>
public class InteriorBlocker : MonoBehaviour
{
    public Vector2 size = new Vector2(4f, 4f);

    /// <summary>Paints wall over the area and makes it solid. Called by InteriorStyle.Apply.</summary>
    public void Apply(Tilemap floor, Tilemap wall, TileBase fill)
    {
        var interior = GetComponentInParent<HouseInterior>();
        if (interior == null) return;
        Vector3 local = interior.transform.InverseTransformPoint(transform.position);
        int x0 = Mathf.RoundToInt(local.x - size.x * 0.5f), y0 = Mathf.RoundToInt(local.y - size.y * 0.5f);
        int w = Mathf.RoundToInt(size.x), h = Mathf.RoundToInt(size.y);
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                var c = new Vector3Int(x, y, 0);
                if (floor != null) floor.SetTile(c, null);
                if (wall != null && fill != null) wall.SetTile(c, fill);
            }
        var col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.size = size; col.offset = Vector2.zero;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.15f);
        Gizmos.DrawCube(transform.position, new Vector3(size.x, size.y, 0f));
    }
}
