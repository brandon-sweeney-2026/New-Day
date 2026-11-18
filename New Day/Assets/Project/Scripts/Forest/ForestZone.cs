using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// One part of the Healing Forest (for example "Creek Bend"). Put it on an empty object and size the box
/// (the green / purple rectangle in the Scene view) to cover the zone.
///
/// While the zone is SICK:   purple fog drifts over it and shadow animals appear when you walk in.
/// The zone's heart stump (a ForestStump with this zone in its "Zone" box) asks for the items in "Requirements".
/// When it has them all the zone HEALS, for good:
///   - a green wave spreads out from the stump over a few seconds,
///   - floor tiles inside the box swap to their healthy version (the pairs are on Forest Heal Settings),
///   - every HealSwap in the box (trees, bushes...) changes picture as the wave reaches it,
///   - every ZoneBlocker of this zone (bramble, broken bridge...) clears the way,
///   - the shadow animals turn back into friendly animals, and more friendly animals live here from now on,
///   - the fog fades and the pollution on the ground inside the box is cleaned.
///
/// Saved: flag "forest_zone_<id>". Every heal also adds 1 to the counter "forest_zones_healed".
/// </summary>
public class ForestZone : MonoBehaviour, IForestOffering
{
    [Header("Zone")]
    [Tooltip("Must be different for every zone - it is how the game remembers this zone is healed.")]
    public string id = "trailhead";
    [Tooltip("The name the player sees, e.g. 'Creek Bend'.")]
    public string displayName = "The Trailhead";
    [Tooltip("1 = the first zone to heal, 2 = the next... The fairy sends you to the lowest one that is still sick.")]
    [Min(1)] public int order = 1;
    [Tooltip("Size of the zone in tiles (the box is centred on this object).")]
    public Vector2 size = new Vector2(30f, 20f);
    [Tooltip("Where the healing wave starts. Leave empty to use this zone's heart stump (or the middle of the box).")]
    public Transform waveOrigin;

    [Header("What the heart stump asks for")]
    public ForestRequirement[] requirements = new ForestRequirement[0];

    [Header("Shadow animals (while sick)")]
    public GameObject[] shadowPrefabs = new GameObject[0];
    [Min(0)] public int minShadows = 1;
    [Min(0)] public int maxShadows = 3;
    [Tooltip("Leave the zone and come back after this many seconds and the shadow animals return (0 = never).")]
    [Min(0f)] public float respawnAfterSeconds = 180f;

    [Header("Friendly animals (once healed)")]
    [Min(0)] public int minCritters = 2;
    [Min(0)] public int maxCritters = 4;
    [Tooltip("Which animals live here (ids from Forest Heal Settings, e.g. rabbit, bird). Leave empty for any of them.")]
    public string[] critterIds = new string[0];

    [Header("Sick look")]
    [Tooltip("Drifting purple fog over the zone while it is sick.")]
    public bool fog = true;
    [Tooltip("0 = no fog, 1 = normal, 2 = thick.")]
    [Range(0f, 2f)] public float fogAmount = 1f;

    // ------------------------------------------------------------------ every zone in the open scene
    public static readonly List<ForestZone> All = new List<ForestZone>();

    /// <summary>Raised when a zone has finished healing (after the wave).</summary>
    public static event System.Action<ForestZone> OnHealed;

    public const string HealedCounter = "forest_zones_healed";
    public static string FlagFor(string zoneId) { return "forest_zone_" + zoneId; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { All.Clear(); OnHealed = null; }

    /// <summary>The zone whose box contains this point (null if none).</summary>
    public static ForestZone At(Vector2 point)
    {
        foreach (ForestZone z in All) if (z != null && z.Contains(point)) return z;
        return null;
    }

    /// <summary>How many zones in this scene are healed.</summary>
    public static int HealedInScene()
    {
        int n = 0;
        foreach (ForestZone z in All) if (z != null && z.IsHealed) n++;
        return n;
    }

    /// <summary>The sick zone with the lowest Order (null when every zone is healed).</summary>
    public static ForestZone NextToHeal()
    {
        ForestZone best = null;
        foreach (ForestZone z in All)
            if (z != null && !z.IsHealed && (best == null || z.order < best.order)) best = z;
        return best;
    }

    // ------------------------------------------------------------------ IForestOffering (the stump's screen uses these)
    public string OfferingId { get { return "zone_" + id; } }
    public ForestRequirement[] Requirements { get { return requirements; } }
    public bool IsDone { get { return IsHealed || IsHealing; } }
    public void Fulfil() { Heal(); }
    public string DoneMessage { get { return displayName + " is healing!"; } }

    // ------------------------------------------------------------------ state
    public bool IsHealed { get { return GameState.HasFlag(FlagFor(id)); } }
    public bool IsHealing { get; private set; }

    public bool Contains(Vector2 point)
    {
        Vector2 d = point - (Vector2)transform.position;
        return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.y) <= size.y * 0.5f;
    }

    private Transform player;
    private bool playerInside;
    private float leftAt = -9999f;
    private bool everSpawnedShadows;
    private readonly List<GameObject> shadows = new List<GameObject>();
    private readonly List<GameObject> critters = new List<GameObject>();

    // fog patches: their picture, resting place, and drift settings
    private class FogPatch { public SpriteRenderer sr; public Vector3 home; public float seed, speed, range; }
    private readonly List<FogPatch> fogPatches = new List<FogPatch>();
    private float fogStrength = 1f;           // 1 = full fog, 0 = gone
    private float fogTarget = 1f;

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        UpgradeOldSave();
    }

    // ------------------------------------------------------------------ saves from the old maze forest
    /// <summary>Set once the game has checked whether this save comes from the old maze forest.</summary>
    public const string OldSaveCheckedFlag = "forest_old_save_checked";
    /// <summary>Set when the save had already reached the grove in the old maze: every zone then counts as healed.</summary>
    public const string OldSaveGroveFlag = "forest_old_save_grove";

    /// <summary>
    /// A save from before the Healing Forest may already have reached the grove through the old maze. That player has
    /// finished the forest, so every zone is healed for them (quietly: no wave, the forest is simply green when they arrive).
    /// The first time any zone loads, the save is checked once: grove reached but no zone healed = an old save.
    /// Runs in OnEnable, so it happens before any zone, tree or blocker reads IsHealed in Start.
    /// </summary>
    private void UpgradeOldSave()
    {
        if (!GameState.HasFlag(OldSaveCheckedFlag))
        {
            GameState.SetFlag(OldSaveCheckedFlag);
            if (GameState.HasFlag("forest_grove_reached") && GameState.GetCounter(HealedCounter) == 0)
            {
                GameState.SetFlag(OldSaveGroveFlag);
                Debug.Log("Healing Forest: this save reached the grove in the old maze, so every zone counts as healed.");
            }
        }
        if (GameState.HasFlag(OldSaveGroveFlag) && !IsHealed)
        {
            GameState.SetFlag(FlagFor(id));
            GameState.AddCounter(HealedCounter);
        }
    }
    private void OnDisable() { All.Remove(this); }

    private void Start()
    {
        if (IsHealed)
        {
            SwapTiles(true);                            // healthy floor straight away
            fogStrength = fogTarget = 0f;
            StartCoroutine(CleanPollutionSoon());
        }
        else if (fog) BuildFog();
    }

    private void Update()
    {
        AnimateFog();

        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null) return;
        }
        bool inside = Contains(player.position);
        if (inside && !playerInside)
        {
            playerInside = true;
            if (IsHealed && !IsHealing) FillCritters();
            else if (!IsHealed)
            {
                bool away = respawnAfterSeconds > 0f && Time.time - leftAt > respawnAfterSeconds;
                if (!everSpawnedShadows || away) FillShadows();
            }
        }
        else if (!inside && playerInside)
        {
            playerInside = false;
            leftAt = Time.time;
        }
    }

    // ------------------------------------------------------------------ healing
    /// <summary>Heals the zone with the green wave and remembers it. Does nothing if it is already healed.</summary>
    public void Heal()
    {
        if (IsHealed || IsHealing) return;
        GameState.SetFlag(FlagFor(id));
        GameState.AddCounter(HealedCounter);
        StartCoroutine(Wave());
    }

    /// <summary>Makes the zone sick again, at once (for the test menu). Offerings already given are forgotten too.</summary>
    public void ResetToSick()
    {
        StopAllCoroutines();
        IsHealing = false;
        if (IsHealed) GameState.AddCounter(HealedCounter, -1);
        GameState.SetFlag(FlagFor(id), false);
        ForestProgress.ResetGiven(OfferingId, requirements != null ? requirements.Length : 0);

        SwapTiles(false);
        foreach (HealSwap s in HealSwap.All) if (s != null && s.Zone == this) s.SetHealthy(false, true);
        foreach (ZoneBlocker b in ZoneBlocker.All) if (b != null && b.Zone == this) b.SetOpen(false, true);
        foreach (GameObject c in critters) if (c != null) Destroy(c);
        critters.Clear();
        if (fog && fogPatches.Count == 0) BuildFog();
        fogStrength = fogTarget = 1f;
        everSpawnedShadows = false;
        playerInside = false;                          // so walking in again fills the zone with shadows
    }

    private Vector2 WaveOrigin()
    {
        if (waveOrigin != null) return waveOrigin.position;
        foreach (ForestStump s in FindObjectsByType<ForestStump>())
            if (s != null && s.zone == this) return s.transform.position;
        return transform.position;
    }

    // One floor tile waiting for the wave.
    private class TileJob { public Tilemap map; public Vector3Int cell; public TileBase to; public float dist; }

    private IEnumerator Wave()
    {
        IsHealing = true;
        ForestHealSettings set = ForestHealSettings.Current;
        Vector2 origin = WaveOrigin();

        // how far the wave has to travel: to the farthest corner of the box
        Vector2 c = transform.position, h = size * 0.5f;
        float maxR = 0f;
        foreach (Vector2 corner in new[] { c + new Vector2(-h.x, -h.y), c + new Vector2(h.x, -h.y), c + new Vector2(-h.x, h.y), c + h })
            maxR = Mathf.Max(maxR, Vector2.Distance(origin, corner));

        // everything the wave will reach, nearest first
        List<TileJob> tiles = CollectTiles(true, origin);
        var swaps = new List<HealSwap>();
        foreach (HealSwap s in HealSwap.All) if (s != null && s.Zone == this) swaps.Add(s);
        swaps.Sort((a, b) => Vector2.Distance(origin, a.transform.position).CompareTo(Vector2.Distance(origin, b.transform.position)));
        var blockers = new List<ZoneBlocker>();
        foreach (ZoneBlocker b in ZoneBlocker.All) if (b != null && b.Zone == this) blockers.Add(b);
        var shadowList = new List<ShadowCreature>();
        foreach (ShadowCreature sc in FindObjectsByType<ShadowCreature>()) if (sc != null && !sc.IsDead && Contains(sc.transform.position)) shadowList.Add(sc);

        // a soft green light swelling at the stump
        Light2D glow = null;
        if (set.waveLight)
        {
            var lg = new GameObject("HealGlow");
            lg.transform.position = new Vector3(origin.x, origin.y + 0.5f, 0f);
            glow = lg.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = set.waveLightColor;
            glow.pointLightInnerRadius = 0.5f;
            glow.intensity = 0f;
            glow.shadowsEnabled = false;
        }

        fogTarget = 0f;
        int ti = 0, si = 0;
        float t = 0f, sparkleDebt = 0f, seconds = Mathf.Max(0.5f, set.waveSeconds);
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            float r = maxR * (1f - (1f - k) * (1f - k));                // fast at first, then slowing down

            for (; ti < tiles.Count && tiles[ti].dist <= r; ti++) tiles[ti].map.SetTile(tiles[ti].cell, tiles[ti].to);
            for (; si < swaps.Count && Vector2.Distance(origin, swaps[si].transform.position) <= r; si++) swaps[si].SetHealthy(true, false);
            foreach (ZoneBlocker b in blockers) if (!b.IsOpen && Vector2.Distance(origin, b.transform.position) <= r) b.SetOpen(true, false);
            for (int i = shadowList.Count - 1; i >= 0; i--)
            {
                ShadowCreature sc = shadowList[i];
                if (sc == null || sc.IsDead) { shadowList.RemoveAt(i); continue; }
                if (Vector2.Distance(origin, sc.transform.position) <= r) { Purify(sc); shadowList.RemoveAt(i); }
            }

            // sparkles along the edge of the wave (only the part of the circle inside the box)
            sparkleDebt += set.sparklesPerSecond * Time.deltaTime;
            for (; sparkleDebt >= 1f; sparkleDebt -= 1f)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector2 p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!Contains(p)) continue;
                HealSpark.Spawn(p, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 1.1f)), Random.Range(0.6f, 1.1f), Random.Range(0.07f, 0.14f), set.sparkleColor, 50);
            }
            if (glow != null)
            {
                glow.intensity = set.waveLightIntensity * Mathf.Sin(k * Mathf.PI);
                glow.pointLightOuterRadius = Mathf.Lerp(2f, 9f, k);
            }
            yield return null;
        }

        // anything the wave didn't quite reach (rounding) is finished off now
        for (; ti < tiles.Count; ti++) tiles[ti].map.SetTile(tiles[ti].cell, tiles[ti].to);
        for (; si < swaps.Count; si++) swaps[si].SetHealthy(true, false);
        foreach (ZoneBlocker b in blockers) if (!b.IsOpen) b.SetOpen(true, false);
        foreach (ShadowCreature sc in shadowList) if (sc != null && !sc.IsDead) Purify(sc);
        if (glow != null) Destroy(glow.gameObject);

        CleanPollution();
        IsHealing = false;
        if (playerInside) FillCritters();
        GameEvents.ShowToast(displayName + " is healthy again!");
        if (OnHealed != null) OnHealed(this);
    }

    /// <summary>Every floor tile in the box that has a partner on the settings (sick -> healthy, or back), nearest to 'origin' first.</summary>
    private List<TileJob> CollectTiles(bool toHealthy, Vector2 origin)
    {
        var jobs = new List<TileJob>();
        ForestHealSettings set = ForestHealSettings.Current;
        if (set.tilePairs == null || set.tilePairs.Length == 0) return jobs;
        Vector2 c = transform.position, h = size * 0.5f;
        foreach (Tilemap map in FindObjectsByType<Tilemap>())
        {
            Vector3Int a = map.WorldToCell(c - h), b = map.WorldToCell(c + h);
            for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    TileBase now = map.GetTile(cell);
                    if (now == null) continue;
                    TileBase to = toHealthy ? set.HealthyFor(now) : set.SickFor(now);
                    if (to == null) continue;
                    Vector2 world = map.GetCellCenterWorld(cell);
                    if (!Contains(world)) continue;
                    jobs.Add(new TileJob { map = map, cell = cell, to = to, dist = Vector2.Distance(origin, world) });
                }
        }
        jobs.Sort((p, q) => p.dist.CompareTo(q.dist));
        return jobs;
    }

    /// <summary>Changes all the floor tiles at once (no wave): used when the scene opens on a healed zone, and by the reset.</summary>
    private void SwapTiles(bool toHealthy)
    {
        foreach (TileJob j in CollectTiles(toHealthy, transform.position)) j.map.SetTile(j.cell, j.to);
    }

    /// <summary>A shadow animal caught by the wave: a puff of sparkles and it is a friendly animal again.</summary>
    private void Purify(ShadowCreature sc)
    {
        Vector3 at = sc.transform.position;
        ShadowCreature.Style style = sc.style;
        Destroy(sc.gameObject);
        shadows.Remove(sc.gameObject);
        Color col = ForestHealSettings.Current.sparkleColor;
        for (int i = 0; i < 14; i++)
            HealSpark.Spawn(at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.6f), 0f),
                            Random.insideUnitCircle * 1.4f + Vector2.up * 0.6f, Random.Range(0.5f, 0.9f), Random.Range(0.08f, 0.15f), col, 50);
        ForestHealSettings.CritterArt art = ForestHealSettings.Current.CritterFor(style);
        if (art != null) critters.Add(ForestCritter.Spawn(art, at, transform).gameObject);
    }

    // ------------------------------------------------------------------ pollution on the ground
    private IEnumerator CleanPollutionSoon()
    {
        // wait a frame: the river's pollution (RiverPollution) is painted when the scene starts, and this must run after it
        yield return null;
        yield return null;
        CleanPollution();
    }

    private void CleanPollution()
    {
        PollutionMap map = PollutionMap.Instance;
        if (map == null) return;
        Vector2 c = transform.position, h = size * 0.5f;
        Grid grid = map.GetComponentInParent<Grid>();
        if (grid == null) grid = FindAnyObjectByType<Grid>();
        if (grid == null) return;
        Vector3Int a = grid.WorldToCell(c - h), b = grid.WorldToCell(c + h);
        for (int y = a.y; y <= b.y; y++)
            for (int x = a.x; x <= b.x; x++)
                map.Clean(new Vector3Int(x, y, 0));
    }

    // ------------------------------------------------------------------ animals
    private void FillShadows()
    {
        everSpawnedShadows = true;
        shadows.RemoveAll(g => g == null);
        if (shadowPrefabs == null || shadowPrefabs.Length == 0) return;
        int want = Random.Range(minShadows, Mathf.Max(minShadows, maxShadows) + 1) - shadows.Count;
        for (int i = 0; i < want; i++)
        {
            GameObject prefab = shadowPrefabs[Random.Range(0, shadowPrefabs.Length)];
            Vector2 spot;
            if (prefab == null || !FindSpot(out spot)) continue;
            GameObject e = Instantiate(prefab, spot, Quaternion.identity, transform);
            e.name = prefab.name;
            shadows.Add(e);
        }
    }

    private void FillCritters()
    {
        critters.RemoveAll(g => g == null);
        var choices = new List<ForestHealSettings.CritterArt>();
        ForestHealSettings set = ForestHealSettings.Current;
        if (set.critters != null)
            foreach (ForestHealSettings.CritterArt a in set.critters)
            {
                if (a == null) continue;
                bool wanted = critterIds == null || critterIds.Length == 0;
                if (!wanted) foreach (string cid in critterIds) if (cid == a.id) { wanted = true; break; }
                if (wanted) choices.Add(a);
            }
        if (choices.Count == 0) return;
        int want = Random.Range(minCritters, Mathf.Max(minCritters, maxCritters) + 1) - critters.Count;
        for (int i = 0; i < want; i++)
        {
            Vector2 spot;
            if (!FindSpot(out spot)) continue;
            critters.Add(ForestCritter.Spawn(choices[Random.Range(0, choices.Count)], spot, transform).gameObject);
        }
    }

    /// <summary>A free spot inside the zone: not in a wall or tree, not on a keep-clear marker, not right next to the player.</summary>
    private bool FindSpot(out Vector2 spot)
    {
        Vector2 h = size * 0.5f - Vector2.one;
        for (int tries = 0; tries < 40; tries++)
        {
            Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-h.x, h.x), Random.Range(-h.y, h.y));
            if (player != null && Vector2.Distance(p, player.position) < 3.5f) continue;
            if (Physics2D.OverlapCircle(p, 0.6f) != null) continue;
            bool blocked = false;
            foreach (KeepClearMarker m in KeepClearMarker.All)
                if (m != null && Vector2.Distance(p, m.transform.position) < m.radius) { blocked = true; break; }
            if (blocked) continue;
            spot = p;
            return true;
        }
        spot = Vector2.zero;
        return false;
    }

    // ------------------------------------------------------------------ fog
    private void BuildFog()
    {
        ForestHealSettings set = ForestHealSettings.Current;
        float area = size.x * size.y / 100f;
        int patches = Mathf.Clamp(Mathf.RoundToInt(5f * area * fogAmount), 0, 24);
        for (int i = 0; i < patches; i++)
        {
            var go = new GameObject("Fog");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f) * size.x, Random.Range(-0.4f, 0.4f) * size.y, 0f);
            go.transform.localScale = Vector3.one * Random.Range(5f, 8f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.Circle;
            sr.sortingOrder = 40;
            sr.color = set.fogColor;
            go.AddComponent<LightingExempt>();
            fogPatches.Add(new FogPatch { sr = sr, home = go.transform.localPosition, seed = Random.value * 100f, speed = Random.Range(0.1f, 0.25f), range = 2.5f });
        }
    }

    private void AnimateFog()
    {
        if (fogPatches.Count == 0) return;
        ForestHealSettings set = ForestHealSettings.Current;
        fogStrength = Mathf.MoveTowards(fogStrength, fogTarget, Time.deltaTime / Mathf.Max(0.1f, set.fogFadeSeconds));
        foreach (FogPatch f in fogPatches)
        {
            if (f.sr == null) continue;
            f.sr.enabled = fogStrength > 0.001f;
            if (!f.sr.enabled) continue;
            float t = Time.time * f.speed + f.seed;
            f.sr.transform.localPosition = f.home + new Vector3(Mathf.Sin(t) * f.range, Mathf.Cos(t * 0.8f) * f.range * 0.6f, 0f);
            Color c = set.fogColor;
            c.a *= (0.7f + 0.3f * Mathf.Sin(t * 1.7f)) * fogStrength;
            f.sr.color = c;
        }
    }

    // ------------------------------------------------------------------ editor
    private void OnDrawGizmos()
    {
        bool healed = Application.isPlaying && IsHealed;
        Gizmos.color = healed ? new Color(0.4f, 1f, 0.45f, 0.7f) : new Color(0.7f, 0.35f, 1f, 0.7f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
    }
}
