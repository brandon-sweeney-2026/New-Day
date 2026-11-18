using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scatters random clutter over the farm and adds a little more every morning:
///   - pollution litter (cans, debris, black lumps, purple lumps): rare at first, more the more you deal with Sterling;
///   - weeds, cleared with the scythe;
///   - stones (and now and then a bit of ore), broken with the pickaxe;
///   - fallen trees, chopped with the axe.
///
/// Nothing to set up in the scene: it creates itself in the Farm scene. All the numbers are on
/// Assets/Resources/FarmDebrisSettings.asset (made by Farm > Set Up > Farm Debris).
///
/// Where things may appear follows the usual spawner rules (see SpawnAreaBase): on the Ground tilemap, never on tilled soil,
/// water, fences or (by default) paths, never on top of another collider, and never near a KeepClearMarker.
///
/// What lies where is remembered while the game runs, so leaving the farm and coming back shows the same weeds,
/// and it is written to the save file (SaveSystem calls CaptureForSave and LoadFromSave). A save made before this
/// existed gets a fresh scatter when loaded.
/// </summary>
public class FarmDebrisSpawner : SpawnAreaBase
{
    /// <summary>One remembered thing on the farm.</summary>
    private class Placed
    {
        public string kindId;
        public Vector2 position;
        public Vector3Int cell;
        public GameObject live;    // the object in the scene (null while the farm scene is not loaded)
    }

    // ---- remembered for the whole play session (static), so it survives going to town and back
    private static readonly List<Placed> placed = new List<Placed>();
    private static int lastSpawnDay = -1;     // the day number of the last morning spawn (-1 = never filled yet)

    public static FarmDebrisSpawner Instance { get; private set; }

    private FarmDebrisSettings settings;
    private FarmManager farm;
    private bool ready;

    protected override Color ZoneColor { get { return new Color(0.6f, 0.85f, 0.4f, 1f); } }

    // ------------------------------------------------------------------ creating itself
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        placed.Clear();
        lastSpawnDay = -1;
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() { Make(SceneManager.GetActiveScene()); }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Make(scene); }

    private static void Make(Scene scene)
    {
        FarmDebrisSettings s = FarmDebrisSettings.Current;
        if (s == null || scene.name != s.farmSceneName) return;
        if (FindAnyObjectByType<FarmDebrisSpawner>() != null) return;
        if (FindAnyObjectByType<FarmManager>() == null) return;          // no farm, nothing to clutter
        var go = new GameObject("FarmDebrisSpawner");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<FarmDebrisSpawner>();
    }

    // ------------------------------------------------------------------ life cycle
    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        settings = FarmDebrisSettings.Current;
        farm = FindAnyObjectByType<FarmManager>();
        if (settings != null)
        {
            allowOnPaths = settings.allowOnPaths;
            keepClearRadius = 2.5f;
        }
        if (farm != null && farm.GroundTilemap != null) spawnOnTilemap = farm.GroundTilemap;
    }

    private IEnumerator Start()
    {
        // wait one frame so the TimeManager has put the clock right (after a load or a scene change)
        yield return null;
        if (settings == null) yield break;

        RebuildRemembered();
        CatchUp();
        ready = true;

        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay += CatchUp;
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay -= CatchUp;
        if (Instance == this) Instance = null;
        // the scene is closing: keep the memory, but the objects are gone
        foreach (Placed p in placed) p.live = null;
    }

    private void LateUpdate()
    {
        // while a save is being loaded the memory already holds the loaded farm, which has no objects yet: leave it alone
        if (!ready || SaveSystem.IsLoading) return;
        // anything cut, broken or picked up since last frame is forgotten, so it does not come back next visit
        for (int i = placed.Count - 1; i >= 0; i--)
            if (placed[i].live == null) placed.RemoveAt(i);
    }

    private static int Today { get { return TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 1; } }

    // ------------------------------------------------------------------ the save file
    /// <summary>
    /// SaveSystem calls this while writing a save: copies everything on the farm into 'into' and returns the day of the
    /// last morning spawn. While the farm is open only things still standing count; elsewhere the memory is used as it is.
    /// </summary>
    public static int CaptureForSave(List<SavedDebris> into)
    {
        bool farmOpen = Instance != null;
        foreach (Placed p in placed)
        {
            if (farmOpen && p.live == null) continue;      // cut or picked up this very frame
            into.Add(new SavedDebris { kind = p.kindId, x = p.position.x, y = p.position.y });
        }
        return lastSpawnDay;
    }

    /// <summary>
    /// SaveSystem calls this while loading a save, before the farm scene opens. 'saved' null = an old save without clutter:
    /// the farm gets a fresh starting scatter.
    /// </summary>
    public static void LoadFromSave(List<SavedDebris> saved, int day)
    {
        placed.Clear();
        lastSpawnDay = saved != null ? day : -1;
        if (saved == null) return;
        foreach (SavedDebris d in saved)
        {
            if (d == null || string.IsNullOrEmpty(d.kind)) continue;
            // the tile is worked out again when the farm opens (RebuildRemembered), as there is no farm to ask yet
            placed.Add(new Placed { kindId = d.kind, position = new Vector2(d.x, d.y) });
        }
    }

    /// <summary>Puts back everything that was on the farm when the player left.</summary>
    private void RebuildRemembered()
    {
        for (int i = placed.Count - 1; i >= 0; i--)
        {
            Placed p = placed[i];
            FarmDebrisSettings.Kind kind = settings.KindById(p.kindId);
            if (kind == null || kind.prefab == null) { placed.RemoveAt(i); continue; }   // the kind was removed from the settings
            p.cell = CellOf(p.position);
            p.live = Create(kind, p.position);
        }
    }

    // ------------------------------------------------------------------ spawning
    /// <summary>Adds the clutter of every morning since the last spawn (or the starting scatter on a brand new farm).</summary>
    private void CatchUp()
    {
        if (settings == null) return;

        if (lastSpawnDay < 0)
        {
            StartingScatter();
            lastSpawnDay = Today;
            return;
        }

        int days = Mathf.Clamp(Today - lastSpawnDay, 0, settings.maxCatchUpDays);
        lastSpawnDay = Today;
        TimeManager.Season season = TimeManager.Instance != null ? TimeManager.Instance.CurrentSeason : TimeManager.Season.Spring;
        for (int d = 0; d < days; d++)
            foreach (FarmDebrisSettings.GroupRule r in settings.rules)
            {
                if (r == null || !r.AppearsIn(season)) continue;
                SpawnGroup(r, Roll(settings.PerDay(r)));
            }
    }

    /// <summary>
    /// The clutter of a brand new farm. Pollution gets its exact Start Count. Weeds, stones and fallen trees fill
    /// 'Start Coverage' of the free tiles (0.5 = half), one thing per tile, split between them by their Start Counts.
    /// A fallen tree is two tiles wide, so it takes a tile and the one to its right.
    /// </summary>
    private void StartingScatter()
    {
        FarmDebrisSettings.GroupRule pollution = settings.RuleFor(FarmDebrisSettings.Group.Pollution);
        if (pollution != null) SpawnGroup(pollution, pollution.startCount);

        // the other groups, and how big a share each one gets
        var groups = new List<FarmDebrisSettings.GroupRule>();
        int shareTotal = 0;
        foreach (FarmDebrisSettings.GroupRule r in settings.rules)
        {
            if (r == null || r.group == FarmDebrisSettings.Group.Pollution || r.startCount <= 0) continue;
            groups.Add(r);
            shareTotal += r.startCount;
        }
        if (groups.Count == 0) return;

        if (settings.startCoverage <= 0f)
        {
            foreach (FarmDebrisSettings.GroupRule r in groups) SpawnGroup(r, r.startCount);   // the old way: exact counts
            return;
        }

        // every free tile, in random order
        List<KeyValuePair<Vector3Int, Vector2>> free = AllFreeTiles(settings.keepAwayFromPlayer);
        var freeSet = new HashSet<Vector3Int>();
        foreach (var t in free)
            if (!(farm != null && farm.IsTilled(t.Key)) && !(TreeManager.Instance != null && TreeManager.Instance.HasTree(t.Key))) freeSet.Add(t.Key);
        foreach (Placed p in placed) freeSet.Remove(p.cell);              // the starting litter already took its tiles
        for (int i = free.Count - 1; i > 0; i--)                          // shuffle
        {
            int j = Random.Range(0, i + 1);
            var tmp = free[i]; free[i] = free[j]; free[j] = tmp;
        }

        int target = Mathf.RoundToInt(freeSet.Count * settings.startCoverage);
        var used = new HashSet<Vector3Int>();
        int placedCount = 0;
        foreach (var tile in free)
        {
            if (placedCount >= target) break;
            Vector3Int cell = tile.Key;
            if (!freeSet.Contains(cell) || used.Contains(cell)) continue;

            // which group gets this tile (by share)
            int roll = Random.Range(0, shareTotal);
            FarmDebrisSettings.GroupRule rule = groups[groups.Count - 1];
            foreach (FarmDebrisSettings.GroupRule r in groups) { roll -= r.startCount; if (roll < 0) { rule = r; break; } }
            FarmDebrisSettings.Kind kind = settings.PickKind(rule.group);
            if (kind == null) continue;

            Vector2 spot = tile.Value + spawnOffset;
            if (rule.group == FarmDebrisSettings.Group.FallenTree)
            {
                // two tiles wide: needs the tile to the right too, and sits on the line between them
                Vector3Int right = cell + Vector3Int.right;
                if (!freeSet.Contains(right) || used.Contains(right)) continue;
                used.Add(right);
                spot += new Vector2(0.5f, 0f);
                placedCount++;          // coverage counts tiles, and a tree covers two (the second is counted below)
            }
            else
            {
                spot += new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(-0.05f, 0.05f));   // not on a perfect grid
            }

            used.Add(cell);
            GameObject go = Spawn(kind, spot);
            if (go != null) { placed[placed.Count - 1].cell = cell; placedCount++; }
        }
    }

    /// <summary>Turns "1.4 a day" into a whole number: 1 for sure, and a 40% chance of one more.</summary>
    private static int Roll(float perDay)
    {
        int whole = Mathf.FloorToInt(perDay);
        return whole + (Random.value < perDay - whole ? 1 : 0);
    }

    private int CountOf(FarmDebrisSettings.Group g)
    {
        int n = 0;
        foreach (Placed p in placed)
        {
            FarmDebrisSettings.Kind k = settings.KindById(p.kindId);
            if (k != null && k.group == g) n++;
        }
        return n;
    }

    private void SpawnGroup(FarmDebrisSettings.GroupRule rule, int howMany)
    {
        int room = settings.MaxOnFarm(rule) - CountOf(rule.group);
        howMany = Mathf.Min(howMany, room);
        for (int i = 0; i < howMany; i++)
        {
            FarmDebrisSettings.Kind kind = settings.PickKind(rule.group);
            if (kind == null) return;
            Vector2 spot;
            if (!FindFreeSpot(out spot)) continue;
            Spawn(kind, spot);
        }
    }

    /// <summary>Places one thing at a world position and remembers it. Also used by the test menu.</summary>
    public GameObject Spawn(FarmDebrisSettings.Kind kind, Vector2 position)
    {
        if (kind == null || kind.prefab == null) return null;
        var p = new Placed { kindId = kind.id, position = position, cell = CellOf(position) };
        p.live = Create(kind, position);
        placed.Add(p);
        return p.live;
    }

    private GameObject Create(FarmDebrisSettings.Kind kind, Vector2 position)
    {
        GameObject go = Instantiate(kind.prefab, position, Quaternion.identity, transform);
        go.name = kind.prefab.name;
        return go;
    }

    private bool FindFreeSpot(out Vector2 spot)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            if (!TryFindSpot(out spot, settings.keepAwayFromPlayer)) break;
            Vector3Int cell = CellOf(spot);
            if (farm != null && farm.IsTilled(cell)) continue;
            if (TreeManager.Instance != null && TreeManager.Instance.HasTree(cell)) continue;
            if (!FarFromOthers(spot)) continue;
            return true;
        }
        spot = Vector2.zero;
        return false;
    }

    private bool FarFromOthers(Vector2 p)
    {
        foreach (Placed other in placed)
            if (Vector2.Distance(other.position, p) < settings.minSpacing) return false;
        return true;
    }

    /// <summary>The tile a thing stands on (its position is at its feet, so step back up by the spawn offset).</summary>
    private Vector3Int CellOf(Vector2 position)
    {
        Vector2 centre = position - spawnOffset;
        return farm != null ? farm.WorldToCell(centre) : Vector3Int.FloorToInt(centre);
    }

    // ------------------------------------------------------------------ questions other scripts ask
    /// <summary>True if weeds, litter, a stone or a fallen tree is on this tile (the hoe can't till there).</summary>
    public static bool IsCellCluttered(Vector3Int cell)
    {
        foreach (Placed p in placed)
            if (p.live != null && p.cell == cell) return true;
        return false;
    }

    /// <summary>How many things of a group are on the farm right now (for debug readouts).</summary>
    public static int Count(FarmDebrisSettings.Group g)
    {
        FarmDebrisSettings s = FarmDebrisSettings.Current;
        if (s == null) return 0;
        int n = 0;
        foreach (Placed p in placed)
        {
            FarmDebrisSettings.Kind k = s.KindById(p.kindId);
            if (k != null && k.group == g) n++;
        }
        return n;
    }

    /// <summary>Test helper: removes all the clutter and scatters a brand new farm's worth again (with today's Start Coverage).</summary>
    public void Rescatter()
    {
        foreach (Placed p in placed) if (p.live != null) Destroy(p.live);
        placed.Clear();
        lastSpawnDay = -1;
        CatchUp();
    }

    /// <summary>Test helper: drops one thing of the group near the player straight away.</summary>
    public bool SpawnNearPlayer(FarmDebrisSettings.Group g)
    {
        FarmDebrisSettings.Kind kind = settings != null ? settings.PickKind(g) : null;
        if (kind == null) return false;
        Vector2 spot = Vector2.zero; bool ok = false;
        for (int tries = 0; tries < 12 && !ok; tries++)
        {
            if (!TryFindSpot(out spot, 1.5f, 6f)) break;
            ok = FarFromOthers(spot);
        }
        if (!ok) return false;
        Spawn(kind, spot);
        return true;
    }
}
