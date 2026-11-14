using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One 10 x 10 room of the forest maze.
///
/// - Build the room: choose what each side is (Wall / Open / Door), pick a "Style", then press "Rebuild Room" (button in the Inspector, or right-click the
///   component's title > Rebuild Room). It makes the floor, thorn walls with colliders, and gates where you chose Door.
///   A gap is 2 tiles wide. Two neighbouring rooms must both have a gap on the shared side (one of them with the Door, the other Open).
///   The maze generator (Farm > Forest > Maze Generator) does all of this for you.
/// - When the player comes into the room, 1-3 shadow animals appear. If you leave and come back after a while they return.
/// - It never spawns on top of scenery, walls or anything with a collider.
/// </summary>
public class ForestRoom : MonoBehaviour
{
    public enum Side { Wall, Open, Door }

    [Header("Shape (Rebuild Room applies these)")]
    public ForestStyle style;
    public Side north = Side.Wall;
    public Side east = Side.Wall;
    public Side south = Side.Wall;
    public Side west = Side.Wall;
    [Tooltip("Room size in tiles (the Maze Generator sets this; default 15). Press Rebuild Room after changing.")]
    public int size = 10;
    public int floorSortingOrder = -20;
    public int wallSortingOrder = 6;

    [Header("Enemies")]
    public GameObject[] enemyPrefabs;
    [Min(0)] public int minEnemies = 1;
    [Min(0)] public int maxEnemies = 3;
    [Tooltip("Leave and come back after this many seconds and the animals return (0 = never).")]
    public float respawnAfterSeconds = 180f;

    public bool Entered { get; private set; }
    public bool PlayerInside { get; private set; }

    private readonly List<GameObject> alive = new List<GameObject>();
    private float leftAt = -9999f;
    private Transform player;
    private bool everSpawned;

    private void Awake()
    {
        // the purple "R" picture on the room prefab is only a marker for the editor: hide it while playing
        var marker = GetComponent<SpriteRenderer>();
        if (marker != null) marker.enabled = false;
    }

    private void Start()
    {
        if (style != null && style.fog) BuildFog();
    }

    private void Update()
    {
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null) return;
        }

        Vector2 d = (Vector2)player.position - (Vector2)transform.position;
        bool inside = Mathf.Abs(d.x) <= size * 0.5f && Mathf.Abs(d.y) <= size * 0.5f;
        if (inside && !PlayerInside)
        {
            PlayerInside = true; Entered = true;
            bool away = Time.time - leftAt > respawnAfterSeconds && respawnAfterSeconds > 0f;
            if (!everSpawned || away) Populate();
        }
        else if (!inside && PlayerInside)
        {
            PlayerInside = false; leftAt = Time.time;
        }
    }

    private void Populate()
    {
        everSpawned = true;
        for (int i = alive.Count - 1; i >= 0; i--) if (alive[i] == null) alive.RemoveAt(i);
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        int want = Random.Range(minEnemies, Mathf.Max(minEnemies, maxEnemies) + 1) - alive.Count;
        for (int i = 0; i < want; i++)
        {
            GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            if (prefab == null) continue;
            Vector2 spot;
            if (!FindSpot(out spot)) continue;
            GameObject e = Instantiate(prefab, spot, Quaternion.identity, transform);
            e.name = prefab.name;
            alive.Add(e);
        }
    }

    /// <summary>A free spot inside the room: away from the walls, scenery, the stump and the player.</summary>
    private bool FindSpot(out Vector2 spot)
    {
        float r = size * 0.5f - 2f;
        for (int tries = 0; tries < 40; tries++)
        {
            Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-r, r), Random.Range(-r, r));
            if (player != null && Vector2.Distance(p, player.position) < 3.5f) continue;   // not in the player's face
            if (Physics2D.OverlapCircle(p, 0.6f) != null) continue;
            foreach (KeepClearMarker m in KeepClearMarker.All)
                if (m != null && Vector2.Distance(p, m.transform.position) < m.radius) { p = Vector2.positiveInfinity; break; }
            if (float.IsInfinity(p.x)) continue;
            spot = p;
            return true;
        }
        spot = Vector2.zero;
        return false;
    }

    // ---------------------------------------------------------------- fog
    private void BuildFog()
    {
        float k = size / 10f;
        float amount = Mathf.Max(0f, style.fogAmount);
        int patches = Mathf.RoundToInt(5 * k * amount);
        float alphaMul = amount <= 1f ? Mathf.Lerp(0.4f, 1f, amount) : 1f + (amount - 1f) * 0.8f;
        Color fogCol = style.fogColor; fogCol.a = Mathf.Clamp01(fogCol.a * alphaMul);
        for (int i = 0; i < patches; i++)
        {
            var go = new GameObject("Fog");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Random.Range(-3f, 3f) * k, Random.Range(-3f, 3f) * k, 0f);
            go.transform.localScale = Vector3.one * Random.Range(5f, 8f) * k;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.Circle;
            sr.color = fogCol;
            sr.sortingOrder = 40;
            go.AddComponent<LightingExempt>();
            var f = go.AddComponent<ForestFogDrift>();
            f.baseColor = fogCol;
            f.range = 2.5f; f.speed = Random.Range(0.1f, 0.25f);
        }
    }

    // ---------------------------------------------------------------- building (design time)
    [ContextMenu("Rebuild Room")]
    public void Rebuild()
    {
        // remove the old built parts
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name == "Built") { DestroyNow(c.gameObject); }
        }
        if (style == null) { Debug.LogWarning(name + ": choose a Style first."); return; }

        var built = new GameObject("Built");
        built.transform.SetParent(transform, false);
        built.hideFlags = HideFlags.None;

        float h = size * 0.5f;
        // floor
        var floor = new GameObject("Floor");
        floor.transform.SetParent(built.transform, false);
        Sprite[] tiles = style.floorTiles;
        if (tiles != null && tiles.Length > 0)
        {
            // a patchwork so the floor isn't one repeated tile: random tiles of 1x1 would be many objects, so use rows of 2x2 blocks per variant
            int block = tiles.Length > 1 ? 2 : size;
            Random.State saved = Random.state;
            Random.InitState(Mathf.RoundToInt(transform.position.x * 31f + transform.position.y * 17f));
            for (int y = 0; y < size; y += block)
                for (int x = 0; x < size; x += block)
                {
                    int bw = Mathf.Min(block, size - x), bh = Mathf.Min(block, size - y);      // the last block is smaller when the size is odd
                    var t = new GameObject("F");
                    t.transform.SetParent(floor.transform, false);
                    t.transform.localPosition = new Vector3(-h + x + bw * 0.5f, -h + y + bh * 0.5f, 0f);
                    var sr = t.AddComponent<SpriteRenderer>();
                    sr.sprite = tiles[Random.Range(0, tiles.Length)];
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.size = new Vector2(bw, bh);
                    sr.sortingOrder = floorSortingOrder;
                    t.AddComponent<LightingExempt>();
                }
            Random.state = saved;
        }

        // walls: each side is a strip 1 tile thick just inside the room edge, with a 2-wide gap in the middle if Open or Door
        BuildSide(built.transform, Vector2.up, north);
        BuildSide(built.transform, Vector2.right, east);
        BuildSide(built.transform, Vector2.down, south);
        BuildSide(built.transform, Vector2.left, west);

        // scenery
        BuildScenery(built.transform);
    }

    /// <summary>Redoes only the scenery (trees, bushes...) and leaves walls, gates, stumps and everything else alone. Used by Farm > Forest > Update Maze.</summary>
    public void RebuildScenery()
    {
        Transform built = transform.Find("Built");
        if (built == null) { Rebuild(); return; }
        Transform old = built.Find("Scenery");
        if (old != null) DestroyNow(old.gameObject);
        BuildScenery(built);
    }

    private void BuildScenery(Transform built)
    {
        if (style == null || style.scenery == null || style.scenery.Length == 0) return;
        float h = size * 0.5f;
        Random.State saved = Random.state;
        Random.InitState(Mathf.RoundToInt(transform.position.x * 13f + transform.position.y * 7f) + 5);
        var sc = new GameObject("Scenery");
        sc.transform.SetParent(built, false);
        int count = Mathf.RoundToInt(style.sceneryCount * size * size / 100f);          // more scenery in bigger rooms
        count = Mathf.Min(count, Mathf.Max(0, style.maxScenery));                      // ...but never more than the cap
        var placed = new System.Collections.Generic.List<Vector3>();                   // x,y = position, z = space it needs
        var trees = new System.Collections.Generic.List<Sprite>();                      // the dead trees in the style's list
        foreach (Sprite t in style.scenery)
            if (t != null && t.name.ToLowerInvariant().Contains("dead_tree")) trees.Add(t);
        if (trees.Count > 0 && count < 1) count = 1;                                    // the cap never removes the guaranteed tree
        int attempts = 0;
        while (placed.Count < count && attempts++ < count * 30)
        {
            Sprite pic = style.scenery[Random.Range(0, style.scenery.Length)];
            if (placed.Count == 0 && trees.Count > 0) pic = trees[Random.Range(0, trees.Count)];      // every room gets at least one tree
            bool bigTree = pic != null && pic.name.ToLowerInvariant().Contains("dead_tree");
            float scale = bigTree ? Mathf.Max(0.1f, style.deadTreeScale) : 1f;
            float room = bigTree ? 1.6f * scale : 1f;                                  // how much space this one needs around it
            float margin = bigTree ? 2.5f + scale : 2.5f;
            Vector2 p = new Vector2(Mathf.Round(Random.Range(-h + margin, h - margin)), Mathf.Round(Random.Range(-h + margin, h - margin)));
            if (p.magnitude < 1.8f + (bigTree ? scale : 0f)) continue;                  // keep the middle clear for the stump
            bool clash = false;
            foreach (Vector3 q in placed)
                if (Vector2.Distance(p, q) < (room + q.z) * 0.7f) { clash = true; break; }
            if (clash) continue;
            placed.Add(new Vector3(p.x, p.y, room));

            var o = new GameObject("Scenery");
            o.transform.SetParent(sc.transform, false);
            o.transform.localPosition = new Vector3(p.x, p.y, 0f);
            o.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = pic;
            sr.sortingOrder = style.sceneryFlat ? floorSortingOrder + 1 : 8;
            sr.flipX = Random.value < 0.5f;
            if (!style.sceneryFlat)
            {
                var col = o.AddComponent<BoxCollider2D>();
                float trunk = bigTree ? 1f : 0.8f;                                      // collider is drawn in world size, so divide by the scale
                col.size = new Vector2(trunk, 0.5f) / scale;
                col.offset = new Vector2(0f, 0.25f / scale);
                o.AddComponent<BehindPlayerSorter>();
                if (style.sceneryShadows)
                {
                    var sh = o.AddComponent<ShadowCaster>();                            // takes light from the day/night sun and lamps
                    sh.lengthMultiplier = bigTree ? style.treeShadowLength : 1f;
                }
            }
        }
        Random.state = saved;
    }

    private void BuildSide(Transform parent, Vector2 dir, Side side)
    {
        float h = size * 0.5f;
        bool horizontal = dir.y != 0f;                  // top or bottom wall runs left-right
        float gap = side == Side.Wall ? 0f : 2f;
        float along = size;                              // length of the wall
        Vector2 centre = dir * (h - 0.5f);

        var holder = new GameObject("Wall_" + (dir.y > 0 ? "N" : dir.y < 0 ? "S" : dir.x > 0 ? "E" : "W"));
        holder.transform.SetParent(parent, false);

        if (gap <= 0f) { MakeWall(holder.transform, centre, horizontal, along); }
        else
        {
            float seg = (along - gap) * 0.5f;
            float off = gap * 0.5f + seg * 0.5f;
            Vector2 a = horizontal ? new Vector2(-off, 0f) : new Vector2(0f, -off);
            Vector2 b = horizontal ? new Vector2(off, 0f) : new Vector2(0f, off);
            MakeWall(holder.transform, centre + a, horizontal, seg);
            MakeWall(holder.transform, centre + b, horizontal, seg);
        }

        if (side == Side.Door)
        {
            var g = new GameObject("Gate");
            g.transform.SetParent(holder.transform, false);
            g.transform.localPosition = centre;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sortingOrder = wallSortingOrder + 1;
            var d = g.AddComponent<ForestDoor>();
            d.style = style;
            d.upDown = !horizontal;
            d.id = "door_" + name + "_" + holder.name;
            sr.sprite = horizontal ? style.gateAcrossLocked : style.gateUpDownLocked;
            var bc = g.AddComponent<BoxCollider2D>();
            bc.size = horizontal ? new Vector2(2f, 1f) : new Vector2(1f, 2f);
            g.AddComponent<LightingExempt>();
        }
    }

    private void MakeWall(Transform parent, Vector2 centre, bool horizontal, float length)
    {
        if (length <= 0.01f) return;
        var w = new GameObject("Wall");
        w.transform.SetParent(parent, false);
        w.transform.localPosition = centre;
        Vector2 sz = horizontal ? new Vector2(length, 1f) : new Vector2(1f, length);
        if (style.wallTile != null)
        {
            var sr = w.AddComponent<SpriteRenderer>();
            sr.sprite = style.wallTile;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = sz;
            sr.sortingOrder = wallSortingOrder;
        }
        var col = w.AddComponent<BoxCollider2D>();
        col.size = sz;
        w.AddComponent<LightingExempt>();
    }

    private static void DestroyNow(GameObject go)
    {
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.5f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size, size, 0f));
        DrawSide(Vector2.up, north); DrawSide(Vector2.right, east); DrawSide(Vector2.down, south); DrawSide(Vector2.left, west);
    }

    private void DrawSide(Vector2 dir, Side s)
    {
        if (s == Side.Wall) return;
        Gizmos.color = s == Side.Door ? new Color(1f, 0.4f, 0.6f, 0.9f) : new Color(0.4f, 1f, 0.5f, 0.9f);
        Vector3 c = transform.position + (Vector3)(dir * (size * 0.5f - 0.5f));
        Gizmos.DrawWireCube(c, dir.y != 0f ? new Vector3(2f, 1f, 0f) : new Vector3(1f, 2f, 0f));
    }
}

/// <summary>Makes a patch of fog slowly drift and breathe.</summary>
public class ForestFogDrift : MonoBehaviour
{
    public Color baseColor = new Color(0.45f, 0.2f, 0.65f, 0.16f);
    public float range = 2.5f, speed = 0.15f;
    private Vector3 home;
    private float seed;
    private SpriteRenderer sr;

    private void Start() { home = transform.localPosition; seed = Random.value * 100f; sr = GetComponent<SpriteRenderer>(); }

    private void Update()
    {
        float t = Time.time * speed + seed;
        transform.localPosition = home + new Vector3(Mathf.Sin(t) * range, Mathf.Cos(t * 0.8f) * range * 0.6f, 0f);
        if (sr != null) sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (0.7f + 0.3f * Mathf.Sin(t * 1.7f)));
    }
}
