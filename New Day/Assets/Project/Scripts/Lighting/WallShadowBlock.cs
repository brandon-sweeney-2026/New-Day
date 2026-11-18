using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Marks a wall or cliff as something that throws shadows.
/// Put it on either:
///   - an object with a BoxCollider2D (the forest's thorn wall strips), or
///   - a Tilemap with a TilemapCollider2D (the mountain "Cliffs" map, the forest "Walls" map).
///     Only tiles that have a collider count (moss, snow and other decoration tiles do not).
/// Farm > Set Up > Wall Shadows adds it for you, and the forest room builder and the Geography Brush add it to new walls.
///
/// It does two things:
///   1. DAYTIME: tells WallSunShadows to draw a sun shadow on the ground next to it, leaning with the clock like
///      the tree shadows. "Height" sets how long that shadow is.
///   2. NIGHT / LAMPS: makes sure there is a Shadow Caster 2D shaped like the collider, so lamps, torches,
///      mushrooms and crystals cannot shine through it.
/// </summary>
[DisallowMultipleComponent]
public class WallShadowBlock : MonoBehaviour
{
    [Header("Daytime sun shadow")]
    [Tooltip("Untick to give this wall no sun shadow.")]
    public bool sunShadow = true;
    [Tooltip("How tall the wall looks, in tiles. The sun shadow is as long as a tree shadow of this height would be.")]
    [Min(0f)] public float height = 1.5f;

    [Header("Lamp shadow")]
    [Tooltip("Lamps, torches, mushrooms and crystals cannot shine through this wall (adds a Shadow Caster 2D shaped like the collider if there is none).")]
    public bool blockLamps = true;

    private void OnEnable() { WallSunShadows.Register(this); }
    private void OnDisable() { WallSunShadows.Unregister(this); }

    private void Start()
    {
        if (!blockLamps) return;
        Collider2D col = SolidCollider();
        if (col != null) LampShadowHelper.CastFromCollider(col);
    }

    /// <summary>The collider that decides the wall's shape (a tilemap collider wins over a box).</summary>
    public Collider2D SolidCollider()
    {
        var tc = GetComponent<TilemapCollider2D>();
        if (tc != null) return tc;
        foreach (Collider2D c in GetComponents<Collider2D>())
            if (!c.isTrigger) return c;
        return null;
    }

    /// <summary>
    /// Adds the solid parts of this wall as world-space rectangles. A tilemap gives one rectangle per row of
    /// neighbouring solid tiles (fewer, bigger rectangles are faster to draw than one per tile).
    /// </summary>
    public void CollectRects(List<Rect> into)
    {
        var map = GetComponent<Tilemap>();
        if (map != null) { CollectTiles(map, into); return; }

        var box = GetComponent<BoxCollider2D>();
        if (box == null || box.isTrigger) return;
        // corners of the box in world space (works with scaled parents; walls are never rotated)
        Vector3 a = transform.TransformPoint(box.offset - box.size * 0.5f);
        Vector3 b = transform.TransformPoint(box.offset + box.size * 0.5f);
        into.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
    }

    private static void CollectTiles(Tilemap map, List<Rect> into)
    {
        map.CompressBounds();
        BoundsInt b = map.cellBounds;
        for (int y = b.yMin; y < b.yMax; y++)
        {
            int runStart = int.MinValue;
            for (int x = b.xMin; x <= b.xMax; x++)            // one step past the end closes the last run
            {
                bool solid = x < b.xMax && IsSolid(map, new Vector3Int(x, y, 0));
                if (solid && runStart == int.MinValue) runStart = x;
                else if (!solid && runStart != int.MinValue)
                {
                    Vector3 lo = map.CellToWorld(new Vector3Int(runStart, y, 0));
                    Vector3 hi = map.CellToWorld(new Vector3Int(x, y + 1, 0));
                    into.Add(Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y)));
                    runStart = int.MinValue;
                }
            }
        }
    }

    private static bool IsSolid(Tilemap map, Vector3Int cell)
    {
        return map.HasTile(cell) && map.GetColliderType(cell) != Tile.ColliderType.None;
    }

    /// <summary>The renderer of this wall, used to put the sun shadow on the same sorting layer.</summary>
    public Renderer MainRenderer()
    {
        var r = GetComponent<Renderer>();
        return r != null ? r : GetComponentInChildren<Renderer>();
    }
}
