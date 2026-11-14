using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Shared "where may things appear" rules for RockSpawner and EnemySpawner. You don't add this directly.
///
/// Things appear on tiles of the "Spawn On Tilemap" (your Ground tilemap), never on a tile of a "Keep Clear" tilemap (fences, water; paths too unless Allow On Paths is ticked),
/// never on top of another collider (trees, rocks, the house, exits) and never close to a "Keep Clear Point" (put one on the entrance and the
/// player's start so nothing blocks them). You can also limit it to a box with "Limit To Box" (the box is centred on this object - select it to see it).
/// If you leave the tilemaps empty it looks for ones named like Ground / Path / Fence / Water by itself.
/// </summary>
public abstract class SpawnAreaBase : MonoBehaviour
{
    [Header("Where things may appear")]
    [Tooltip("Things only appear on painted tiles of this tilemap. Empty = find the one named like 'Ground'.")]
    public Tilemap spawnOnTilemap;
    [Tooltip("Never appear on painted tiles of these (paths, fences, water...). Empty = found by name.")]
    public Tilemap[] keepClearTilemaps;
    [Tooltip("ON = rocks, ore and enemies may appear on painted paths (the Paths and PathDecor layers from the Geography Brush). OFF = they keep off all paths. Only affects the tilemaps found by name; ones you assign above are always kept clear.")]
    public bool allowOnPaths = true;
    public bool limitToBox = false;
    public Vector2 boxSize = new Vector2(40f, 30f);
    [Tooltip("Empty objects marking places to keep free (the entrance, the player's start, exits).")]
    public Transform[] keepClearPoints;
    public float keepClearRadius = 3f;
    [Tooltip("Nothing appears this close to another collider.")] public float obstacleRadius = 0.6f;
    [Tooltip("Shift from the tile centre (rocks have their pivot at the bottom).")] public Vector2 spawnOffset = new Vector2(0f, -0.4f);

    private List<Vector3Int> cells;
    private bool searched;

    protected virtual Color ZoneColor { get { return new Color(0.4f, 0.8f, 1f, 1f); } }

    // the little picture used while designing the level is hidden when the game runs
    protected virtual void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    private static readonly string[] BlockHints = { "path", "road", "fence", "water", "soil", "wet", "pollution" };

    private void AutoFind()
    {
        if (searched) return;
        searched = true;
        if (spawnOnTilemap == null)
            foreach (Tilemap t in FindObjectsByType<Tilemap>())
                if (t.gameObject.name.ToLowerInvariant().Contains("ground")) { spawnOnTilemap = t; break; }

        if (keepClearTilemaps == null || keepClearTilemaps.Length == 0)
        {
            var list = new List<Tilemap>();
            foreach (Tilemap t in FindObjectsByType<Tilemap>())
            {
                string n = t.gameObject.name.ToLowerInvariant();
                if (allowOnPaths && n.Contains("path")) continue;          // Paths + PathDecor: mountain trails are good places for ore
                foreach (string h in BlockHints) if (n.Contains(h)) { list.Add(t); break; }
            }
            keepClearTilemaps = list.ToArray();
        }

        if (spawnOnTilemap == null && !limitToBox)
        {
            limitToBox = true;
            Debug.LogWarning(name + ": no ground tilemap found, so it uses the box around this object instead. Assign 'Spawn On Tilemap' to fix.");
        }
    }

    private bool InBox(Vector2 p)
    {
        if (!limitToBox) return true;
        Vector2 c = transform.position;
        return Mathf.Abs(p.x - c.x) <= boxSize.x * 0.5f && Mathf.Abs(p.y - c.y) <= boxSize.y * 0.5f;
    }

    private void BuildCells()
    {
        cells = new List<Vector3Int>();
        if (spawnOnTilemap == null) return;
        BoundsInt b = spawnOnTilemap.cellBounds;
        foreach (Vector3Int c in b.allPositionsWithin)
        {
            if (!spawnOnTilemap.HasTile(c)) continue;
            if (!InBox(spawnOnTilemap.GetCellCenterWorld(c))) continue;
            cells.Add(c);
        }
    }

    /// <summary>Finds a free spot following the rules above. Returns false if it couldn't find one after a number of tries.</summary>
    protected bool TryFindSpot(out Vector2 spot, float minDistanceFromPlayer = 0f, float maxDistanceFromPlayer = 0f)
    {
        AutoFind();
        if (cells == null) BuildCells();
        spot = Vector2.zero;

        Transform player = null;
        if (minDistanceFromPlayer > 0f || maxDistanceFromPlayer > 0f)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 world, p;
            if (spawnOnTilemap != null)
            {
                if (cells.Count == 0) return false;
                Vector3Int cell = cells[Random.Range(0, cells.Count)];
                world = spawnOnTilemap.GetCellCenterWorld(cell);
                // spread inside the tile a little so rocks don't sit on a perfect grid
                world += new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));
            }
            else
            {
                Vector2 c = transform.position;
                world = c + new Vector2(Random.Range(-boxSize.x, boxSize.x), Random.Range(-boxSize.y, boxSize.y)) * 0.5f;
            }
            p = world;

            if (!InBox(p)) continue;
            if (Blocked(p)) continue;
            if (player != null)
            {
                float d = Vector2.Distance(p, player.position);
                if (minDistanceFromPlayer > 0f && d < minDistanceFromPlayer) continue;
                if (maxDistanceFromPlayer > 0f && d > maxDistanceFromPlayer) continue;
            }
            spot = p + spawnOffset;
            return true;
        }
        return false;
    }

    private bool Blocked(Vector2 p)
    {
        if (keepClearTilemaps != null)
            foreach (Tilemap t in keepClearTilemaps)
                if (t != null && t.HasTile(t.WorldToCell(p))) return true;

        if (keepClearPoints != null)
            foreach (Transform kp in keepClearPoints)
                if (kp != null && Vector2.Distance(p, kp.position) < keepClearRadius) return true;

        foreach (KeepClearMarker m in KeepClearMarker.All)
            if (m != null && Vector2.Distance(p, m.transform.position) < m.radius) return true;

        return Physics2D.OverlapCircle(p, obstacleRadius) != null;
    }

    /// <summary>Call this if you repaint the ground while playing.</summary>
    public void RefreshArea() { cells = null; }

    // The zone box is always drawn faintly in the Scene view, and brighter when selected.
    protected virtual void OnDrawGizmos() { DrawZone(0.35f); }
    protected virtual void OnDrawGizmosSelected()
    {
        DrawZone(1f);
        if (keepClearPoints != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.7f);
            foreach (Transform kp in keepClearPoints) if (kp != null) Gizmos.DrawWireSphere(kp.position, keepClearRadius);
        }
    }

    private void DrawZone(float alpha)
    {
        if (!limitToBox) return;
        Color c = ZoneColor; c.a = alpha;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0f));
        c.a = alpha * 0.08f;
        Gizmos.color = c;
        Gizmos.DrawCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0f));
    }
}
