using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plants, grows and harvests crops. Put this on an empty GameObject.
///
/// How pollution works for crops (levels come from the PollutionMap, effects from PollutionSettings):
///  - Every night, until the crop is ripe, the crop "feels" the pollution on its tile
///    (tile level x the crop's pollutionSensitivity, rounded, 0-5).
///  - That level changes the chance the crop grows that night (growthChance) and tints the plant.
///  - The AVERAGE level over the whole growing period is stored on the harvest.
///    That level lowers the yield (yieldMultiplier) and the sale price (saleMultiplier).
/// </summary>
public class CropManager : MonoBehaviour
{
    public static CropManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private FarmManager farm;
    [Tooltip("Every crop in the game. Seeds are matched to crops through CropData.seedItem.")]
    [SerializeField] private CropData[] crops;

    [Header("Look")]
    [Tooltip("Where the plant sits inside its tile, from the tile centre. Your crop sprites have their pivot a quarter up from the bottom.")]
    [SerializeField] private Vector2 plantOffset = new Vector2(0f, 0f);
    [SerializeField] private int sortingOrder = 5;

    private class CropInstance
    {
        public CropData data;
        public Vector3Int cell;
        public int daysGrown;
        public float pollutionSum;
        public int samples;
        public SpriteRenderer renderer;
    }

    private readonly Dictionary<Vector3Int, CropInstance> planted = new Dictionary<Vector3Int, CropInstance>();
    private Transform holder;

    private void Awake()
    {
        Instance = this;
        if (farm == null) farm = FindFirstObjectByType<FarmManager>();
        holder = new GameObject("Crops").transform;
        holder.SetParent(transform, false);
    }

    private void OnEnable()
    {
        FarmManager.OnBeforeDayAdvanced += GrowOvernight;
    }

    private void OnDisable()
    {
        FarmManager.OnBeforeDayAdvanced -= GrowOvernight;
    }

    private void Start()
    {
        if (PollutionMap.Instance != null) PollutionMap.Instance.OnLevelChanged += OnPollutionChanged;
    }

    private void OnDestroy()
    {
        if (PollutionMap.Instance != null) PollutionMap.Instance.OnLevelChanged -= OnPollutionChanged;
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // Queries
    // ------------------------------------------------------------------

    public CropData FindBySeed(ItemData seed)
    {
        if (seed == null || crops == null) return null;
        foreach (CropData c in crops) if (c != null && c.seedItem == seed) return c;
        return null;
    }

    public bool HasCrop(Vector3Int cell) { return planted.ContainsKey(cell); }

    public bool IsRipe(Vector3Int cell)
    {
        CropInstance c;
        return planted.TryGetValue(cell, out c) && c.data.IsFullyGrown(c.daysGrown);
    }

    // ------------------------------------------------------------------
    // Planting and harvesting
    // ------------------------------------------------------------------

    public bool TryPlant(Vector3Int cell, CropData data)
    {
        if (data == null || farm == null) return false;
        if (!farm.IsTilled(cell) || planted.ContainsKey(cell)) return false;

        var go = new GameObject("Crop: " + data.cropName);
        go.transform.SetParent(holder, false);
        go.transform.position = farm.CellToWorldCenter(cell) + (Vector3)plantOffset;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        var crop = new CropInstance { data = data, cell = cell, renderer = sr };
        planted[cell] = crop;
        farm.SetHasCrop(cell, true);

        Sample(crop);
        Refresh(crop);
        return true;
    }

    /// <summary>Harvests a ripe crop into the inventory (extra items drop on the ground). Returns true if harvested.</summary>
    public bool TryHarvest(Vector3Int cell, Inventory inventory)
    {
        CropInstance crop;
        if (!planted.TryGetValue(cell, out crop)) return false;

        if (!crop.data.IsFullyGrown(crop.daysGrown))
        {
            GameEvents.ShowToast(crop.data.cropName + " isn't ready yet.");
            return false;
        }

        PollutionSettings settings = PollutionSettings.Current;
        int level = AveragePollution(crop);
        float yield = settings.YieldMultiplier(level);
        int amount = Mathf.RoundToInt(crop.data.harvestAmount * yield);
        if (yield > 0f) amount = Mathf.Max(1, amount);

        ItemData item = crop.data.harvestItem;
        if (item != null && amount > 0 && inventory != null)
        {
            int leftover = inventory.Add(item, amount, level);
            if (leftover > 0) ItemPickup.Spawn(item, leftover, farm.CellToWorldCenter(cell), level);
        }

        string label = level > 0 ? " (" + settings.Label(level) + " pollution)" : "";
        GameEvents.ShowToast("Harvested " + amount + " " + (item != null ? item.itemName : crop.data.cropName) + label);
        GameState.AddCounter("harvested_" + crop.data.cropName.ToLowerInvariant().Replace(' ', '_'), amount);
        GameState.AddCounter("crops_harvested", amount);

        Remove(crop);
        return true;
    }

    /// <summary>Digs up a crop without harvesting (shovel). Returns true if there was one.</summary>
    public bool TryRemove(Vector3Int cell)
    {
        CropInstance crop;
        if (!planted.TryGetValue(cell, out crop)) return false;
        Remove(crop);
        return true;
    }

    private void Remove(CropInstance crop)
    {
        planted.Remove(crop.cell);
        if (crop.renderer != null) Destroy(crop.renderer.gameObject);
        if (farm != null) farm.SetHasCrop(crop.cell, false);
    }

    // ------------------------------------------------------------------
    // Growth
    // ------------------------------------------------------------------

    private void GrowOvernight()
    {
        foreach (CropInstance crop in planted.Values)
        {
            if (crop.data.IsFullyGrown(crop.daysGrown)) continue;   // ripe crops keep the level they ripened with

            Sample(crop);

            if (crop.data.needsWater && (farm == null || !farm.IsWatered(crop.cell))) { Refresh(crop); continue; }

            float chance = PollutionSettings.Current.GrowthChance(FeltLevel(crop));
            if (Random.value <= chance) crop.daysGrown++;

            Refresh(crop);
        }
    }

    // ------------------------------------------------------------------
    // Pollution helpers
    // ------------------------------------------------------------------

    private int TileLevel(Vector3Int cell)
    {
        return PollutionMap.Instance != null ? PollutionMap.Instance.GetLevel(cell) : 0;
    }

    private int FeltLevel(CropInstance crop)
    {
        return crop.data.EffectiveLevel(TileLevel(crop.cell));
    }

    private void Sample(CropInstance crop)
    {
        crop.pollutionSum += FeltLevel(crop);
        crop.samples++;
    }

    private int AveragePollution(CropInstance crop)
    {
        if (crop.samples <= 0) return FeltLevel(crop);
        return Mathf.Clamp(Mathf.RoundToInt(crop.pollutionSum / crop.samples), 0, PollutionSettings.MaxLevel);
    }

    private void OnPollutionChanged(Vector3Int cell, int level)
    {
        CropInstance crop;
        if (planted.TryGetValue(cell, out crop)) Refresh(crop);
    }

    private void Refresh(CropInstance crop)
    {
        if (crop.renderer == null) return;
        crop.renderer.sprite = crop.data.GetSpriteForDays(crop.daysGrown);
        crop.renderer.color = PollutionSettings.Current.CropTint(FeltLevel(crop));
    }
}
