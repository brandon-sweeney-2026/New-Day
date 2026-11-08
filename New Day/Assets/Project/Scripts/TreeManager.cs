using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Grows trees on the farm. Put this on an empty GameObject (Farm > Trees > Set Up Trees In Scene does it).
///
///  - New trees sprout at random, only on the ground tiles YOU pick in 'allowedGround' (e.g. Grass and DirtFlat).
///  - Each new day: existing trees grow, mature trees may drop fruit, and a few new saplings may appear.
///  - Trees change look with the season (TimeManager).
///  - Everything below is an Inspector setting.
/// </summary>
public class TreeManager : MonoBehaviour
{
    public static TreeManager Instance { get; private set; }

    [Header("What grows")]
    [SerializeField] private TreeData[] species;

    [Header("Where trees may sprout")]
    [Tooltip("Trees only sprout on these ground tiles. Drag your tile assets here (Grass, DirtFlat...).")]
    [SerializeField] private TileBase[] allowedGround;
    [Tooltip("The ground tilemap. Found through the FarmManager if empty.")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("Cells with a tile on ANY of these tilemaps are skipped (paths, tilled soil, fences, pollution...).")]
    [SerializeField] private Tilemap[] blockedTilemaps;
    [Tooltip("Trees never sprout within 'Keep Clear Radius' tiles of these (the house, the player's start...).")]
    [SerializeField] private Transform[] keepClearAround;
    [SerializeField] private float keepClearRadius = 3f;

    [Header("How many, how often")]
    [Tooltip("Never more than this many trees in the scene.")]
    [SerializeField] private int maxTrees = 60;
    [Tooltip("Each new day, this many random spots are tried...")]
    [SerializeField] private int attemptsPerDay = 8;
    [Tooltip("...and each one sprouts a sapling with this chance.")]
    [Range(0f, 1f)] [SerializeField] private float sproutChance = 0.25f;
    [Tooltip("Trees keep at least this many tiles between each other.")]
    [SerializeField] private float minSpacing = 3f;
    [Tooltip("Trees already on the map when the game starts (random growth stages).")]
    [SerializeField] private int startingTrees = 14;

    [Header("Look")]
    [Tooltip("Same as the player and props so trees sort by height.")]
    [SerializeField] private int sortingOrder = 10;
    [Tooltip("Where the trunk base sits inside its tile, from the tile centre.")]
    [SerializeField] private Vector2 baseOffset = new Vector2(0f, -0.35f);

    private FarmManager farm;
    private readonly Dictionary<Vector3Int, GrowingTree> trees = new Dictionary<Vector3Int, GrowingTree>();
    private Transform holder;

    private void Awake()
    {
        Instance = this;
        farm = FindAnyObjectByType<FarmManager>();
        if (groundTilemap == null && farm != null) groundTilemap = farm.GroundTilemap;
        holder = new GameObject("Trees").transform;
        holder.SetParent(transform, false);
    }

    private void OnEnable()
    {
        FarmManager.OnDayAdvanced += OnNewDay;
    }

    private void OnDisable()
    {
        FarmManager.OnDayAdvanced -= OnNewDay;
    }

    private void Start()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnSeasonChanged += OnSeasonChanged;
        ScatterStartingTrees();
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnSeasonChanged -= OnSeasonChanged;
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public bool HasTree(Vector3Int cell) { return trees.ContainsKey(cell); }
    public int TreeCount { get { return trees.Count; } }

    /// <summary>Plants a tree by hand (a seed item, an event...). Returns null if the spot is not allowed.</summary>
    public GrowingTree Plant(Vector3Int cell, TreeData data, int stage = 0, bool ignoreRules = false, bool byHand = false)
    {
        if (data == null || trees.ContainsKey(cell)) return null;
        if (!ignoreRules && WhyNot(cell, byHand) != null) return null;

        var go = new GameObject("Tree: " + data.treeName);
        go.transform.SetParent(holder, false);
        go.transform.position = groundTilemap.GetCellCenterWorld(cell) + (Vector3)baseOffset;

        var tree = go.AddComponent<GrowingTree>();
        tree.Init(this, data, cell, stage, sortingOrder);
        trees[cell] = tree;
        return tree;
    }

    /// <summary>The tree that a seed item plants, or null.</summary>
    public TreeData FindBySeed(ItemData seed)
    {
        if (seed == null || species == null) return null;
        foreach (TreeData s in species) if (s != null && s.seedItem == seed) return s;
        return null;
    }

    public void Unregister(GrowingTree tree)
    {
        if (tree != null) trees.Remove(tree.Cell);
    }

    // ------------------------------------------------------------------
    // Days and seasons
    // ------------------------------------------------------------------

    private int CurrentSeason
    {
        get { return TimeManager.Instance != null ? (int)TimeManager.Instance.CurrentSeason : 0; }
    }

    private void OnNewDay()
    {
        int season = CurrentSeason;
        foreach (GrowingTree t in new List<GrowingTree>(trees.Values))
            if (t != null) t.AdvanceDay(season);

        for (int i = 0; i < attemptsPerDay && trees.Count < maxTrees; i++)
        {
            if (Random.value > sproutChance) continue;
            TrySproutRandom(0);
        }
    }

    private void OnSeasonChanged()
    {
        int season = CurrentSeason;
        foreach (GrowingTree t in trees.Values) if (t != null) t.Refresh(season);
    }

    // ------------------------------------------------------------------
    // Sprouting
    // ------------------------------------------------------------------

    private void ScatterStartingTrees()
    {
        int placed = 0;
        for (int attempts = 0; attempts < startingTrees * 20 && placed < startingTrees; attempts++)
        {
            if (TrySproutRandom(Random.Range(0, 4))) placed++;
        }
    }

    private bool TrySproutRandom(int stage)
    {
        if (groundTilemap == null || species == null || species.Length == 0) return false;

        BoundsInt b = groundTilemap.cellBounds;
        if (b.size.x <= 0 || b.size.y <= 0) return false;

        var cell = new Vector3Int(Random.Range(b.xMin, b.xMax), Random.Range(b.yMin, b.yMax), 0);
        if (!CanSprout(cell)) return false;

        TreeData data = PickSpecies();
        if (data == null) return false;
        if (Random.value > data.sproutChance) return false;      // this kind is rare
        if (!data.CanSproutIn(CurrentSeason)) return false;      // wrong season for it
        return Plant(cell, data, stage) != null;
    }

    private TreeData PickSpecies()
    {
        float total = 0f;
        foreach (TreeData s in species) if (s != null) total += s.spawnWeight;
        if (total <= 0f) return null;

        float roll = Random.value * total;
        foreach (TreeData s in species)
        {
            if (s == null) continue;
            roll -= s.spawnWeight;
            if (roll <= 0f) return s;
        }
        return species[species.Length - 1];
    }

    /// <summary>The rules for where a wild tree may sprout.</summary>
    public bool CanSprout(Vector3Int cell) { return WhyNot(cell, false) == null; }

    /// <summary>Same rules, but relaxed for a seed the player plants (no keep-clear zone, closer spacing, the player doesn't block).</summary>
    public bool CanPlantByHand(Vector3Int cell) { return WhyNot(cell, true) == null; }

    /// <summary>Returns why a tree can't go in this cell, or null if it can.</summary>
    public string WhyNot(Vector3Int cell, bool byHand)
    {
        if (groundTilemap == null) return "No ground Tilemap set on the TreeManager.";
        if (trees.ContainsKey(cell)) return "There is already a tree here.";

        // 1. Must be one of the ground tiles you chose.
        TileBase ground = groundTilemap.GetTile(cell);
        if (ground == null || !IsAllowed(ground)) return "Trees can't grow on this ground.";

        // 2. Nothing else painted there (paths, soil, fences, pollution...).
        if (blockedTilemaps != null)
            foreach (Tilemap t in blockedTilemaps)
                if (t != null && t.GetTile(cell) != null) return "Something is in the way here.";

        // 3. Not on farmland.
        if (farm != null && (farm.IsTilled(cell) || farm.HasCrop(cell))) return "Trees can't grow on farmland.";

        Vector3 center = groundTilemap.GetCellCenterWorld(cell);

        // 4. Keep clear around the house, player start, etc. (wild trees only)
        if (!byHand && keepClearAround != null)
            foreach (Transform t in keepClearAround)
                if (t != null && Vector2.Distance(t.position, center) < keepClearRadius) return "Too close to the house.";

        // 5. Spacing from other trees (closer is fine when planting by hand).
        float spacing = byHand ? Mathf.Min(minSpacing, 1.5f) : minSpacing;
        foreach (Vector3Int other in trees.Keys)
            if (Vector2.Distance(new Vector2(other.x, other.y), new Vector2(cell.x, cell.y)) < spacing) return "Too close to another tree.";

        // 6. No solid object (building, rock...) in the way. The player never counts when planting by hand.
        foreach (Collider2D c in Physics2D.OverlapCircleAll(center, 0.45f))
        {
            if (c == null || c.isTrigger) continue;
            if (byHand && c.GetComponentInParent<PlayerController>() != null) continue;
            return "Something solid is in the way.";
        }

        return null;
    }

    private bool IsAllowed(TileBase tile)
    {
        if (allowedGround == null) return false;
        foreach (TileBase t in allowedGround) if (t == tile) return true;
        return false;
    }
}
