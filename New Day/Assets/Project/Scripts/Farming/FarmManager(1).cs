using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// Owns the state of every soil tile on the farm (tilled? watered?) and draws it using Tilemaps.
/// Put this on an empty GameObject called "FarmManager" and assign the fields in the Inspector.
///
/// EVERYTHING you are likely to want to tweak is a public/serialized field below,
/// so you can change behaviour without touching code.
/// </summary>
public class FarmManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Inspector settings (edit these in Unity, no code changes needed)
    // ------------------------------------------------------------------

    [Header("Tilemaps (create 3 Tilemaps under one Grid)")]
    [Tooltip("The base ground: grass, dirt, etc. Decides WHERE soil is allowed.")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("Tilled soil is drawn here (sits above ground).")]
    [SerializeField] private Tilemap soilTilemap;
    [Tooltip("The wet overlay is drawn here (sits above soil).")]
    [SerializeField] private Tilemap waterTilemap;

    [Header("Tiles to draw")]
    [SerializeField] private TileBase tilledSoilTile;
    [SerializeField] private TileBase wateredSoilTile;

    [Header("Rules")]
    [Tooltip("If empty, soil can be tilled on ANY ground tile. If filled, only on these tiles.")]
    [SerializeField] private TileBase[] tillableGroundTiles;
    [Tooltip("If true, you can only water soil that has already been tilled.")]
    [SerializeField] private bool wateringRequiresTilledSoil = true;
    [Tooltip("If true, watered soil dries out when a new day starts.")]
    [SerializeField] private bool waterDriesOvernight = true;
    [Tooltip("If true, tilled soil turns back into plain ground if nothing is planted overnight.")]
    [SerializeField] private bool untendedSoilReverts = false;

    [Header("Debug")]
    [Tooltip("Press N to advance a day without a TimeManager. Turn off for release.")]
    [SerializeField] private bool debugNewDayKey = true;

    // ------------------------------------------------------------------
    // Events other systems can listen to
    // ------------------------------------------------------------------

    /// <summary>Fired after the farm processes a new day. Crops will subscribe to this later.</summary>
    public static event Action OnDayAdvanced;

    // ------------------------------------------------------------------
    // Soil data
    // ------------------------------------------------------------------

    private class SoilState
    {
        public bool watered;
        public bool hasCrop; // set by crop code later
    }

    // Only TILLED cells are in this dictionary. No entry = untilled ground.
    private readonly Dictionary<Vector3Int, SoilState> soil = new Dictionary<Vector3Int, SoilState>();

    // ------------------------------------------------------------------
    // Public API (used by FarmToolController, and later by crops/save system)
    // ------------------------------------------------------------------

    /// <summary>Converts a world position into a grid cell.</summary>
    public Vector3Int WorldToCell(Vector3 worldPos) => groundTilemap.WorldToCell(worldPos);

    /// <summary>World-space centre of a cell (handy for placing highlights and crops).</summary>
    public Vector3 CellToWorldCenter(Vector3Int cell) => groundTilemap.GetCellCenterWorld(cell);

    public bool IsTilled(Vector3Int cell) => soil.ContainsKey(cell);

    public bool IsWatered(Vector3Int cell) => soil.TryGetValue(cell, out var s) && s.watered;

    /// <summary>Tries to till the soil at this cell. Returns true if something changed.</summary>
    public bool TryTill(Vector3Int cell)
    {
        if (IsTilled(cell)) return false;
        if (!CanTillGround(cell)) return false;

        soil[cell] = new SoilState();
        soilTilemap.SetTile(cell, tilledSoilTile);
        return true;
    }

    /// <summary>Tries to water the soil at this cell. Returns true if something changed.</summary>
    public bool TryWater(Vector3Int cell)
    {
        if (!soil.TryGetValue(cell, out var state))
        {
            // Not tilled. Either refuse, or (if the rule is off) ignore silently.
            // Change this block if you want untilled ground to be waterable.
            if (wateringRequiresTilledSoil) return false;
            return false;
        }

        if (state.watered) return false;

        state.watered = true;
        waterTilemap.SetTile(cell, wateredSoilTile);
        return true;
    }

    /// <summary>Crop code calls this when a seed is planted or a crop is harvested.</summary>
    public void SetHasCrop(Vector3Int cell, bool hasCrop)
    {
        if (soil.TryGetValue(cell, out var state)) state.hasCrop = hasCrop;
    }

    /// <summary>
    /// Call this when the player sleeps or your TimeManager rolls over to a new day.
    /// Edit this method to change what happens overnight.
    /// </summary>
    public void AdvanceDay()
    {
        // Copy the keys first because we may modify the dictionary while looping.
        var cells = new List<Vector3Int>(soil.Keys);

        foreach (var cell in cells)
        {
            var state = soil[cell];

            if (waterDriesOvernight && state.watered)
            {
                state.watered = false;
                waterTilemap.SetTile(cell, null);
            }

            if (untendedSoilReverts && !state.hasCrop)
            {
                soil.Remove(cell);
                soilTilemap.SetTile(cell, null);
                waterTilemap.SetTile(cell, null);
            }
        }

        OnDayAdvanced?.Invoke();
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    private bool CanTillGround(Vector3Int cell)
    {
        TileBase ground = groundTilemap.GetTile(cell);
        if (ground == null) return false; // no ground here (e.g. water or empty space)

        // No restriction list = any ground tile is fine.
        if (tillableGroundTiles == null || tillableGroundTiles.Length == 0) return true;

        foreach (var allowed in tillableGroundTiles)
            if (allowed == ground) return true;

        return false;
    }

    private void Start()
    {
        LoadPaintedSoil();
    }

    /// <summary>
    /// Reads soil that was painted ahead of time (e.g. with the Farm Level Editor)
    /// into the runtime soil data, so pre-tilled and pre-watered tiles work in-game.
    /// </summary>
    private void LoadPaintedSoil()
    {
        if (soilTilemap == null) return;

        soil.Clear();
        foreach (var cell in soilTilemap.cellBounds.allPositionsWithin)
        {
            if (soilTilemap.GetTile(cell) == null) continue;

            soil[cell] = new SoilState
            {
                watered = waterTilemap != null && waterTilemap.GetTile(cell) != null
            };
        }
    }

    private void Update()
    {
        if (debugNewDayKey && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
            AdvanceDay();
    }
}
