using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Stores the pollution level (0-5) of every tile on the farm.
///
///  - Paint pollution in the editor with the "Pollution" layer of the Farm Level Editor. Whatever is painted
///    when the game starts becomes the starting pollution.
///  - From code or events, call SetLevel / Add / PaintCircle / Clean to change it (a factory spills, rain cleans...).
///  - Crops read the level of the tile they grow on (see CropManager).
///
/// Put this on an empty GameObject. Farm > Game > Set Up Game Systems In Scene does it for you.
/// </summary>
public class PollutionMap : MonoBehaviour
{
    public static PollutionMap Instance { get; private set; }

    [Header("References")]
    [Tooltip("The tilemap that shows pollution. Sits above the soil.")]
    [SerializeField] private Tilemap pollutionTilemap;
    [Tooltip("Exactly 5 tiles: index 0 = level 1 ... index 4 = level 5. Painted tiles are matched against these.")]
    [SerializeField] private TileBase[] levelTiles = new TileBase[PollutionSettings.MaxLevel];
    [SerializeField] private PollutionSettings settings;

    [Header("Changes overnight (both 0 = pollution never changes by itself)")]
    [Tooltip("Chance each polluted tile loses one level every night.")]
    [Range(0f, 1f)] [SerializeField] private float decayChancePerNight = 0f;
    [Tooltip("Chance each polluted tile pushes pollution onto one neighbouring tile every night.")]
    [Range(0f, 1f)] [SerializeField] private float spreadChancePerNight = 0f;

    /// <summary>Raised whenever a tile's level changes: (cell, new level).</summary>
    public event System.Action<Vector3Int, int> OnLevelChanged;

    private readonly Dictionary<Vector3Int, int> levels = new Dictionary<Vector3Int, int>();

    private static readonly Vector3Int[] Neighbours =
    {
        new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(-1, 0, 0)
    };

    private void Awake()
    {
        Instance = this;
        if (settings != null) PollutionSettings.Current = settings;
        LoadPainted();
    }

    private void OnEnable()
    {
        FarmManager.OnDayAdvanced += Overnight;
    }

    private void OnDisable()
    {
        FarmManager.OnDayAdvanced -= Overnight;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Reads whatever was painted in the editor into the runtime data.</summary>
    private void LoadPainted()
    {
        levels.Clear();
        if (pollutionTilemap == null) return;

        foreach (Vector3Int cell in pollutionTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = pollutionTilemap.GetTile(cell);
            if (tile == null) continue;

            for (int i = 0; i < levelTiles.Length; i++)
            {
                if (levelTiles[i] == tile) { levels[cell] = i + 1; break; }
            }
        }
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public int GetLevel(Vector3Int cell)
    {
        int level;
        return levels.TryGetValue(cell, out level) ? level : 0;
    }

    public void SetLevel(Vector3Int cell, int level)
    {
        level = Mathf.Clamp(level, 0, PollutionSettings.MaxLevel);
        if (GetLevel(cell) == level) return;

        if (level == 0)
        {
            levels.Remove(cell);
            if (pollutionTilemap != null) pollutionTilemap.SetTile(cell, null);
        }
        else
        {
            levels[cell] = level;
            if (pollutionTilemap != null && level - 1 < levelTiles.Length) pollutionTilemap.SetTile(cell, levelTiles[level - 1]);
        }

        if (OnLevelChanged != null) OnLevelChanged(cell, level);
    }

    public void Add(Vector3Int cell, int delta)
    {
        SetLevel(cell, GetLevel(cell) + delta);
    }

    public void Clean(Vector3Int cell)
    {
        SetLevel(cell, 0);
    }

    /// <summary>Pollutes a round patch. Tiles already worse than 'level' are left alone.</summary>
    public void PaintCircle(Vector3Int center, int radius, int level)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius) continue;
                var cell = new Vector3Int(center.x + x, center.y + y, 0);
                if (GetLevel(cell) < level) SetLevel(cell, level);
            }
        }
    }

    /// <summary>Cleans a round patch (a purifier, rain, a cleansing spell...).</summary>
    public void CleanCircle(Vector3Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                if (x * x + y * y <= radius * radius) Clean(new Vector3Int(center.x + x, center.y + y, 0));
    }

    // ------------------------------------------------------------------
    // Overnight changes
    // ------------------------------------------------------------------

    private void Overnight()
    {
        if (decayChancePerNight <= 0f && spreadChancePerNight <= 0f) return;

        var cells = new List<Vector3Int>(levels.Keys);
        foreach (Vector3Int cell in cells)
        {
            int level = GetLevel(cell);
            if (level <= 0) continue;

            if (Random.value < decayChancePerNight)
            {
                SetLevel(cell, level - 1);
                continue;
            }

            if (level > 1 && Random.value < spreadChancePerNight)
            {
                Vector3Int neighbour = cell + Neighbours[Random.Range(0, Neighbours.Length)];
                if (GetLevel(neighbour) < level - 1) SetLevel(neighbour, level - 1);
            }
        }
    }
}
