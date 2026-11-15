using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Runs the mine shaft scene. There is ONE scene for the hub and all 100 floors:
///  - Floor 0 is the hub. It is built by hand (by Farm > Set Up > Mine Shaft) under the object "Hub" and is simply switched on.
///  - Floors 1-100 are made on the spot: the Hub is switched off, a new plan comes from MineFloorGenerator, and this script
///    paints the tiles and places the ladder, the stairs, lamps, rocks, ore and enemies for that depth.
///
/// Every number you may want to tune (ore chance, ore table, enemy sets, sizes) is in the Config asset, not here.
/// Other scripts call MineShaftManager.Instance.GoToFloor(n) (MineStairs and MineCart do).
/// </summary>
public class MineShaftManager : MonoBehaviour
{
    /// <summary>GameState counter: the deepest floor the player has reached this play session.</summary>
    public const string DeepestFloorKey = "mine_deepest_floor";
    /// <summary>GameState flag: the fairy door in the hub has been woken.</summary>
    public const string FairyDoorFlag = "mine_fairy_door";

    public static MineShaftManager Instance { get; private set; }
    /// <summary>The floor the player is on. 0 = the hub.</summary>
    public static int CurrentFloor { get; private set; }

    [Header("Tuning (click the asset to edit the numbers)")]
    public MineShaftConfig config;

    [Header("Tilemaps for generated floors (the setup menu fills these in)")]
    [Tooltip("The floor the player walks on.")] public Tilemap groundMap;
    [Tooltip("Walls and pools next to the floor. This one has the collider.")] public Tilemap wallMap;
    [Tooltip("Solid rock away from the floor. No collider, it only fills the screen.")] public Tilemap fillMap;
    [Min(0)] [Tooltip("How many tiles of rock are painted past the edge of a floor, so the camera never sees empty space.")]
    public int rockMargin = 14;

    [Header("Hub (floor 0)")]
    [Tooltip("The parent of everything in the hub. Switched off while the player is on a generated floor.")]
    public GameObject hubRoot;
    [Tooltip("Where the player appears in the hub when they climb back up.")]
    public Transform hubArrival;
    [Tooltip("The area the camera may show while in the hub (world units).")]
    public Rect hubCameraBounds = new Rect(0f, 0f, 24f, 16f);

    [Header("Things placed on every generated floor")]
    public GameObject ladderUpPrefab;
    public GameObject stairsDownPrefab;

    [Header("The player's own light")]
    [Tooltip("The player carries a small light underground so they can always see around them.")]
    public bool givePlayerLight = true;
    public Color playerLightColor = new Color(1f, 0.85f, 0.6f);
    [Min(0f)] public float playerLightRadius = 5.5f;
    [Min(0f)] public float playerLightIntensity = 0.9f;

    private GameObject floorRoot;          // parent of everything generated for the current floor
    private bool busy;
    private Vector3 arrivalPoint;
    private static Image fadeImage;

    private void Awake()
    {
        Instance = this;
        Underground.Active = true;
        BuildFloor(0, 0);                  // the scene always opens in the hub
    }

    private void Start()
    {
        if (givePlayerLight) AddPlayerLight();
        ApplyCameraBounds();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Underground.Active = false;
    }

    // ------------------------------------------------------------------ travelling

    /// <summary>Takes the player to a floor (0 = the hub) with a quick fade.</summary>
    public void GoToFloor(int floor)
    {
        if (busy || config == null) return;
        StartCoroutine(Travel(Mathf.Clamp(floor, 0, config.floorCount)));
    }

    private IEnumerator Travel(int floor)
    {
        busy = true;
        GameState.LockInput();
        yield return Fade(0f, 1f, 0.25f);

        int seed = config.fixedSeed != 0 ? config.fixedSeed + floor : Random.Range(1, int.MaxValue);
        Debug.Log(BuildFloor(floor, seed));
        MovePlayer(arrivalPoint);
        ApplyCameraBounds();
        yield return null;                 // one frame so the new walls are solid before the picture comes back

        yield return Fade(1f, 0f, 0.3f);
        GameState.UnlockInput();
        busy = false;

        if (floor == 0) GameEvents.ShowToast("Mine entrance");
        else
        {
            MineTheme theme = config.ThemeFor(floor);
            GameEvents.ShowToast("Floor " + floor + (theme != null ? " - " + theme.themeName : ""));
        }
    }

    private void MovePlayer(Vector3 to)
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        var rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = to; rb.linearVelocity = Vector2.zero; }
        pc.transform.position = to;
        pc.FaceDirection(Vector2.down);
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.SnapToTarget();
    }

    private void ApplyCameraBounds()
    {
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam == null) return;
        if (CurrentFloor == 0) cam.SetBoundsOverride(hubCameraBounds);
        else if (groundMap != null)
        {
            // the painted floor plus a little of the rock around it
            Bounds b = groundMap.localBounds;
            cam.SetBoundsOverride(new Rect(b.min.x - 4f, b.min.y - 4f, b.size.x + 8f, b.size.y + 8f));
        }
    }

    private void AddPlayerLight()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc == null || pc.transform.Find("MinerLight") != null) return;
        // LightSource reads its settings when it wakes up, so the object is made switched off, set up, then switched on
        var go = new GameObject("MinerLight");
        go.SetActive(false);
        go.transform.SetParent(pc.transform, false);
        var light = go.AddComponent<LightSource>();
        light.lightColor = playerLightColor;
        light.intensity = playerLightIntensity;
        light.outerRadius = playerLightRadius;
        light.innerRadius = 0.5f;
        light.lightOffset = new Vector2(0f, 0.6f);
        light.behaviour = LightBehaviour.AlwaysOn;
        light.flickerAmount = 0.05f;
        go.SetActive(true);
    }

    // ------------------------------------------------------------------ building a floor

    /// <summary>
    /// Builds a floor right now and returns one line describing what was made (it goes to the Console).
    /// Also used by the editor preview (Farm > Test and Debug > Mine Floor Previews), so it works without pressing Play.
    /// </summary>
    public string BuildFloor(int floor, int seed)
    {
        ClearGenerated();
        CurrentFloor = floor;

        if (floor <= 0 || config == null)
        {
            CurrentFloor = 0;
            if (hubRoot != null) hubRoot.SetActive(true);
            if (config != null) { Underground.AmbientColor = config.hubAmbientColor; Underground.AmbientBrightness = config.hubAmbientBrightness; }
            arrivalPoint = hubArrival != null ? hubArrival.position : transform.position;
            return "MINEFLOOR|floor=0|hub";
        }

        if (hubRoot != null) hubRoot.SetActive(false);
        MineTheme theme = config.ThemeFor(floor);
        if (theme == null) return "MINEFLOOR|floor=" + floor + "|!! the Config has no themes";

        var rng = new System.Random(seed);
        int w = rng.Next(Mathf.Min(config.floorSizeMin.x, config.floorSizeMax.x), Mathf.Max(config.floorSizeMin.x, config.floorSizeMax.x) + 1);
        int h = rng.Next(Mathf.Min(config.floorSizeMin.y, config.floorSizeMax.y), Mathf.Max(config.floorSizeMin.y, config.floorSizeMax.y) + 1);
        var style = (MineLayoutStyle)rng.Next(4);
        MineFloorLayout layout = MineFloorGenerator.Generate(w, h, style, rng);

        PaintTiles(layout, theme, rng);

        // Everything is made under a switched-off parent and switched on at the end. That way an enemy's own start-up code
        // runs AFTER its health and damage were raised for this depth.
        floorRoot = new GameObject("Floor " + floor);
        floorRoot.SetActive(false);

        var taken = new HashSet<Vector2Int>();
        var counts = new Dictionary<string, int>();
        PlaceLadderAndStairs(layout, floor, taken);
        int lamps = PlaceLamps(layout, taken, rng);
        int rocks = PlaceRocks(layout, theme, floor, taken, rng, counts);
        int enemies = PlaceEnemies(layout, theme, floor, taken, rng, counts);
        floorRoot.SetActive(true);

        Underground.AmbientColor = theme.ambientColor;
        Underground.AmbientBrightness = theme.ambientBrightness;
        arrivalPoint = new Vector3(layout.start.x + 0.5f, layout.start.y - 0.4f, 0f);       // just below the ladder

        if (Application.isPlaying && floor > GameState.GetCounter(DeepestFloorKey)) GameState.SetCounter(DeepestFloorKey, floor);

        var sb = new StringBuilder("MINEFLOOR|floor=" + floor + "|theme=" + theme.themeName + "|style=" + layout.style + "|size=" + layout.width + "x" + layout.height +
                                   "|floorTiles=" + layout.floorCells + "|stairsSteps=" + layout.stairsDistance + "|lamps=" + lamps + "|rocks=" + rocks + "|enemies=" + enemies +
                                   "|x" + config.EnemyStatMultiplier(floor).ToString("0.00") + "|");
        foreach (KeyValuePair<string, int> c in counts) sb.Append(c.Key + ":" + c.Value + " ");
        return sb.ToString();
    }

    private void ClearGenerated()
    {
        if (floorRoot != null)
        {
            floorRoot.SetActive(false);
            if (Application.isPlaying) Destroy(floorRoot); else DestroyImmediate(floorRoot);
            floorRoot = null;
        }
        if (groundMap != null) { groundMap.ClearAllTiles(); groundMap.CompressBounds(); }
        if (wallMap != null) { wallMap.ClearAllTiles(); wallMap.CompressBounds(); }
        if (fillMap != null) { fillMap.ClearAllTiles(); fillMap.CompressBounds(); }
    }

    /// <summary>Paints the three tilemaps from the plan: floor, walls + pools (solid), and the rock that fills the rest of the screen.</summary>
    private void PaintTiles(MineFloorLayout layout, MineTheme theme, System.Random rng)
    {
        int m = rockMargin, w = layout.width, h = layout.height;
        int fullW = w + 2 * m, fullH = h + 2 * m;
        var ground = new TileBase[fullW * fullH];
        var walls = new TileBase[fullW * fullH];
        var fill = new TileBase[fullW * fullH];

        for (int y = -m; y < h + m; y++)
            for (int x = -m; x < w + m; x++)
            {
                int i = (x + m) + (y + m) * fullW;
                if (layout.IsFloor(x, y)) { ground[i] = FloorTile(theme, rng); continue; }

                bool pool = layout.InBounds(x, y) && layout.pool[x, y];
                if (pool)
                {
                    walls[i] = theme.poolTile != null ? theme.poolTile : theme.wallTopTile;
                    ground[i] = FloorTile(theme, rng);                    // floor under the pool, in case the pool picture has see-through edges
                }
                else if (layout.FloorNeighbours(x, y) > 0)
                    walls[i] = layout.IsFloor(x, y - 1) ? theme.wallFaceTile : theme.wallTopTile;   // floor right below = we see the front of the wall
                else
                    fill[i] = theme.wallTopTile;
            }

        var area = new BoundsInt(-m, -m, 0, fullW, fullH, 1);
        if (groundMap != null) groundMap.SetTilesBlock(area, ground);
        if (wallMap != null) wallMap.SetTilesBlock(area, walls);
        if (fillMap != null) fillMap.SetTilesBlock(area, fill);
        if (groundMap != null) groundMap.CompressBounds();
    }

    private static TileBase FloorTile(MineTheme theme, System.Random rng)
    {
        if (theme.floorTiles == null || theme.floorTiles.Length == 0) return null;
        // the first tile most of the time, the others sprinkled in
        if (theme.floorTiles.Length == 1 || rng.NextDouble() < 0.72) return theme.floorTiles[0];
        return theme.floorTiles[1 + rng.Next(theme.floorTiles.Length - 1)];
    }

    private GameObject Put(GameObject prefab, float x, float y)
    {
        if (prefab == null) return null;
        return Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity, floorRoot.transform);
    }

    private void PlaceLadderAndStairs(MineFloorLayout layout, int floor, HashSet<Vector2Int> taken)
    {
        GameObject ladder = Put(ladderUpPrefab, layout.start.x + 0.5f, layout.start.y);
        if (ladder != null) ladder.name = "LadderUp";
        taken.Add(layout.start);
        taken.Add(layout.start + Vector2Int.down);       // where the player appears
        taken.Add(layout.start + Vector2Int.up);         // the ladder is taller than one tile

        if (floor >= config.floorCount) return;          // the last floor has no way further down
        GameObject stairs = Put(stairsDownPrefab, layout.stairs.x + 0.5f, layout.stairs.y);
        if (stairs != null) stairs.name = "StairsDown";
        taken.Add(layout.stairs);
    }

    /// <summary>Lamps stand at the foot of a wall, spaced out, and never where they could block a corridor.</summary>
    private int PlaceLamps(MineFloorLayout layout, HashSet<Vector2Int> taken, System.Random rng)
    {
        if (config.lampPrefab == null) return 0;
        var spots = new List<Vector2Int>();
        for (int y = 0; y < layout.height; y++)
            for (int x = 0; x < layout.width; x++)
            {
                if (!layout.floor[x, y] || layout.IsFloor(x, y + 1) || layout.pool[x, y + 1]) continue;       // needs a wall right above
                if (!layout.IsFloor(x - 1, y) || !layout.IsFloor(x + 1, y) || !layout.IsFloor(x - 1, y - 1) || !layout.IsFloor(x, y - 1) || !layout.IsFloor(x + 1, y - 1)) continue;
                if (!taken.Contains(new Vector2Int(x, y))) spots.Add(new Vector2Int(x, y));
            }
        Shuffle(spots, rng);

        var placed = new List<Vector2Int>();
        float spacing = Mathf.Max(3, config.lampSpacing);
        foreach (Vector2Int s in spots)
        {
            bool tooClose = false;
            foreach (Vector2Int p in placed)
                if ((p - s).sqrMagnitude < spacing * spacing) { tooClose = true; break; }
            if (tooClose) continue;
            placed.Add(s); taken.Add(s);
            GameObject lamp = Put(config.lampPrefab, s.x + 0.5f, s.y + 0.45f);
            if (lamp != null) lamp.name = "Lamp";
        }
        return placed.Count;
    }

    /// <summary>Plain rocks always generate. Each rock then has 'Ore Chance' of being an ore or gem from the table for this depth.</summary>
    private int PlaceRocks(MineFloorLayout layout, MineTheme theme, int floor, HashSet<Vector2Int> taken, System.Random rng, Dictionary<string, int> counts)
    {
        var spots = new List<Vector2Int>();
        for (int y = 0; y < layout.height; y++)
            for (int x = 0; x < layout.width; x++)
            {
                var c = new Vector2Int(x, y);
                if (!layout.floor[x, y] || taken.Contains(c)) continue;
                if (layout.FloorNeighbours(x, y) < 6) continue;                       // keep narrow corridors clear
                if ((c - layout.start).sqrMagnitude < 9 || (c - layout.stairs).sqrMagnitude < 4) continue;
                spots.Add(c);
            }
        Shuffle(spots, rng);

        int want = Mathf.Min(spots.Count, Mathf.RoundToInt(layout.floorCells * config.rocksPer100Tiles / 100f));
        int made = 0;
        for (int i = 0; i < want; i++)
        {
            GameObject prefab = null;
            if (rng.NextDouble() < config.oreChance) prefab = config.PickOre(floor, rng);
            if (prefab == null && theme.plainRocks != null && theme.plainRocks.Length > 0) prefab = theme.plainRocks[rng.Next(theme.plainRocks.Length)];
            if (prefab == null) continue;

            // a little off the tile centre so the rocks don't sit on a perfect grid (rocks have their pivot at the bottom)
            float jitter = (float)rng.NextDouble() * 0.3f - 0.15f;
            Put(prefab, spots[i].x + 0.5f + jitter, spots[i].y + 0.1f);
            taken.Add(spots[i]);
            Tally(counts, prefab.name);
            made++;
        }
        return made;
    }

    /// <summary>Enemies come from the theme's enemy set and get stronger the deeper the floor is inside the theme.</summary>
    private int PlaceEnemies(MineFloorLayout layout, MineTheme theme, int floor, HashSet<Vector2Int> taken, System.Random rng, Dictionary<string, int> counts)
    {
        if (theme.enemies == null || theme.enemies.Length == 0) return 0;
        float totalWeight = 0f;
        foreach (MineEnemyEntry e in theme.enemies) if (e != null && e.prefab != null) totalWeight += e.weight;
        if (totalWeight <= 0f) return 0;

        var spots = new List<Vector2Int>();
        for (int y = 0; y < layout.height; y++)
            for (int x = 0; x < layout.width; x++)
            {
                var c = new Vector2Int(x, y);
                if (!layout.floor[x, y] || taken.Contains(c) || layout.FloorNeighbours(x, y) < 8) continue;
                if ((c - layout.start).sqrMagnitude < config.safeRadius * config.safeRadius) continue;
                spots.Add(c);
            }
        Shuffle(spots, rng);

        int want = Mathf.Min(spots.Count, config.maxEnemies, Mathf.RoundToInt(layout.floorCells * config.enemiesPer100Tiles / 100f));
        float stronger = config.EnemyStatMultiplier(floor);
        int made = 0;
        for (int i = 0; i < want; i++)
        {
            float roll = (float)rng.NextDouble() * totalWeight;
            GameObject prefab = null;
            foreach (MineEnemyEntry e in theme.enemies)
            {
                if (e == null || e.prefab == null) continue;
                prefab = e.prefab;
                roll -= e.weight;
                if (roll <= 0f) break;
            }

            GameObject go = Put(prefab, spots[i].x + 0.5f, spots[i].y + 0.3f);
            if (go == null) continue;
            var enemy = go.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.maxHealth = Mathf.Max(1, Mathf.RoundToInt(enemy.maxHealth * stronger));
                enemy.contactDamage = Mathf.RoundToInt(enemy.contactDamage * stronger);
            }
            taken.Add(spots[i]);
            Tally(counts, prefab.name);
            made++;
        }
        return made;
    }

    private static void Tally(Dictionary<string, int> counts, string key)
    {
        int n;
        counts.TryGetValue(key, out n);
        counts[key] = n + 1;
    }

    private static void Shuffle(List<Vector2Int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int keep = list[i]; list[i] = list[j]; list[j] = keep;
        }
    }

    // ------------------------------------------------------------------ fade to black

    private static IEnumerator Fade(float from, float to, float seconds)
    {
        if (fadeImage == null)
        {
            var go = new GameObject("MineShaftFade");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            var img = new GameObject("Black");
            img.transform.SetParent(go.transform, false);
            fadeImage = img.AddComponent<Image>();
            fadeImage.raycastTarget = false;
            RectTransform rt = fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            DontDestroyOnLoad(go);
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }
}
