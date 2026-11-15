using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>One kind of ore or gem rock, and the floors it can appear on.</summary>
[System.Serializable]
public class MineOreEntry
{
    [Tooltip("Only a label for you (Coal, Copper...).")]
    public string label = "Ore";
    [Tooltip("The rock prefab (it has a MineableRock on it).")]
    public GameObject rockPrefab;
    [Range(1, 100)] [Tooltip("The first floor this ore can appear on.")] public int firstFloor = 1;
    [Range(1, 100)] [Tooltip("The last floor this ore can appear on.")] public int lastFloor = 100;
    [Min(0f)] [Tooltip("How often it is picked compared with the other ores allowed on the same floor. 10 is picked twice as often as 5.")]
    public float weight = 10f;
}

/// <summary>One kind of enemy in a theme's enemy set.</summary>
[System.Serializable]
public class MineEnemyEntry
{
    public GameObject prefab;
    [Min(0f)] [Tooltip("How often it is picked compared with the others in the set.")] public float weight = 1f;
}

/// <summary>The look, rocks and enemies of one block of floors (for example floors 1-20, icy).</summary>
[System.Serializable]
public class MineTheme
{
    public string themeName = "Theme";
    [Range(1, 100)] public int firstFloor = 1;
    [Range(1, 100)] public int lastFloor = 20;

    [Header("Tiles")]
    [Tooltip("Floor tiles. The first one is used most; the others are sprinkled in.")]
    public TileBase[] floorTiles;
    [Tooltip("The front of a wall (drawn where a wall has floor right below it).")]
    public TileBase wallFaceTile;
    [Tooltip("The top of the rock (everything that is not floor).")]
    public TileBase wallTopTile;
    [Tooltip("Pools the player cannot cross: ice, pits, water, quicksand, lava.")]
    public TileBase poolTile;

    [Header("Light")]
    [Tooltip("Colour of the faint light everywhere on these floors.")]
    public Color ambientColor = new Color(0.6f, 0.65f, 0.8f);
    [Range(0f, 1f)] [Tooltip("How bright it is away from lamps. 0 = pitch black, 1 = daylight.")]
    public float ambientBrightness = 0.35f;

    [Header("Plain rocks (always generate)")]
    public GameObject[] plainRocks;

    [Header("Enemy set")]
    public MineEnemyEntry[] enemies;
    [Min(1f)] [Tooltip("Enemy health and damage grow from x1 on the theme's first floor to this on its last floor.")]
    public float statMultiplierAtLastFloor = 1.5f;
}

/// <summary>
/// ALL the tuning numbers of the mine shaft in one place. The asset is Assets/Farm/Mine/MineShaftConfig.asset:
/// click it and change the numbers in the Inspector (you can do it while the game is playing; the next floor uses them).
///
///  - Ore Chance: each rock that generates has this chance of being an ore or gem instead of plain stone (0.05 = 5%).
///  - Ores: which ore can appear on which floors, and how common each one is.
///  - Themes: the five blocks of 20 floors, each with its tiles, light, plain rocks and enemy set.
/// Farm > Set Up > Mine Shaft makes this asset once and then leaves it alone. Delete it to get the starting numbers back.
/// </summary>
[CreateAssetMenu(fileName = "MineShaftConfig", menuName = "Farm/Mine Shaft Config")]
public class MineShaftConfig : ScriptableObject
{
    [Header("Floors")]
    [Range(1, 100)] [Tooltip("How many floors there are below the hub.")]
    public int floorCount = 100;
    [Tooltip("Smallest width and height of a floor, in tiles.")]
    public Vector2Int floorSizeMin = new Vector2Int(56, 56);
    [Tooltip("Largest width and height of a floor, in tiles (100 x 100 at most).")]
    public Vector2Int floorSizeMax = new Vector2Int(96, 96);
    [Min(1)] [Tooltip("The mine cart offers every floor that divides by this number (5 = floors 5, 10, 15...) once the player has reached it.")]
    public int cartEveryNFloors = 5;
    [Tooltip("0 = every floor is different each visit. Any other number = the same mine every time (handy for testing).")]
    public int fixedSeed = 0;

    [Header("Rocks and ore")]
    [Min(0f)] [Tooltip("Rocks for every 100 floor tiles. A floor has roughly 1500-3500 floor tiles.")]
    public float rocksPer100Tiles = 6f;
    [Range(0f, 1f)] [Tooltip("The chance that a rock is an ore or gem instead of plain stone. 0.05 = 5%.")]
    public float oreChance = 0.05f;
    [Tooltip("Which ore appears on which floors. An ore is picked from the ones allowed on the floor, by weight.")]
    public MineOreEntry[] ores;

    [Header("Enemies")]
    [Min(0f)] [Tooltip("Enemies for every 100 floor tiles.")]
    public float enemiesPer100Tiles = 0.5f;
    [Min(0)] [Tooltip("Never more than this many enemies on one floor.")]
    public int maxEnemies = 26;
    [Min(0f)] [Tooltip("No enemy starts closer than this to the ladder the player arrives at (tiles).")]
    public float safeRadius = 8f;

    [Header("Lamps on generated floors")]
    [Tooltip("The lamp placed along the walls (a prefab with a LightSource).")]
    public GameObject lampPrefab;
    [Min(3)] [Tooltip("Lamps are at least this many tiles apart. Bigger = darker floors.")]
    public int lampSpacing = 11;

    [Header("Hub (floor 0) light")]
    public Color hubAmbientColor = new Color(1f, 0.9f, 0.75f);
    [Range(0f, 1f)] public float hubAmbientBrightness = 0.6f;

    [Header("Themes (floors 1-20, 21-40, ...)")]
    public MineTheme[] themes;

    /// <summary>The theme a floor belongs to (the last theme if the floor is past the end).</summary>
    public MineTheme ThemeFor(int floor)
    {
        if (themes == null || themes.Length == 0) return null;
        foreach (MineTheme t in themes)
            if (t != null && floor >= t.firstFloor && floor <= t.lastFloor) return t;
        return themes[themes.Length - 1];
    }

    /// <summary>Picks an ore rock for this floor from the table (by weight). Returns null if no ore is allowed here.</summary>
    public GameObject PickOre(int floor, System.Random rng)
    {
        if (ores == null) return null;
        float total = 0f;
        foreach (MineOreEntry o in ores)
            if (Allowed(o, floor)) total += o.weight;
        if (total <= 0f) return null;

        float roll = (float)rng.NextDouble() * total;
        foreach (MineOreEntry o in ores)
        {
            if (!Allowed(o, floor)) continue;
            roll -= o.weight;
            if (roll <= 0f) return o.rockPrefab;
        }
        return null;
    }

    private static bool Allowed(MineOreEntry o, int floor)
    {
        return o != null && o.rockPrefab != null && o.weight > 0f && floor >= o.firstFloor && floor <= o.lastFloor;
    }

    /// <summary>How much stronger enemies are on this floor than on the first floor of its theme (1 = no change).</summary>
    public float EnemyStatMultiplier(int floor)
    {
        MineTheme t = ThemeFor(floor);
        if (t == null || t.lastFloor <= t.firstFloor) return 1f;
        float along = Mathf.Clamp01((float)(floor - t.firstFloor) / (t.lastFloor - t.firstFloor));
        return Mathf.Lerp(1f, t.statMultiplierAtLastFloor, along);
    }
}
