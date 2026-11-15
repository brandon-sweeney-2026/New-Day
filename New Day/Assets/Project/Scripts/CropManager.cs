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

    [Header("Seasons")]
    [Tooltip("A crop still in the ground when its last season ends dries up and dies. Turn off to let crops finish growing out of season.")]
    [SerializeField] private bool witherOutOfSeason = true;
    [Tooltip("The colour a withered crop turns.")]
    [SerializeField] private Color witheredTint = new Color(0.62f, 0.48f, 0.30f, 1f);
    [Tooltip("How far a withered crop droops over (degrees).")]
    [SerializeField] private float witheredLean = 18f;

    [Header("Wind (crops sway; taller crops sway more)")]
    [SerializeField] private bool swayInWind = true;
    [Tooltip("How far a just-planted crop leans each way (degrees).")]
    [SerializeField] private float swayWhenSmall = 1.5f;
    [Tooltip("How far a fully grown crop leans each way (degrees).")]
    [SerializeField] private float swayWhenGrown = 4.5f;

    [Header("Ripe crops")]
    [Tooltip("Ripe crops give off a little sparkle now and then so you can spot them.")]
    [SerializeField] private bool ripeSparkles = true;
    [Tooltip("Seconds between sparkles (somewhere on the farm).")]
    [SerializeField] private float sparkleEvery = 0.5f;

    private class CropInstance
    {
        public CropData data;
        public Vector3Int cell;
        public int daysGrown;
        public float pollutionSum;
        public int samples;
        public SpriteRenderer renderer;
        public WindSway sway;          // makes the plant rock in the wind
        public bool withered;          // died because its season ended
    }

    private float nextSparkle;

    private readonly Dictionary<Vector3Int, CropInstance> planted = new Dictionary<Vector3Int, CropInstance>();
    private Transform holder;

    private void Awake()
    {
        Instance = this;
        if (farm == null) farm = FindAnyObjectByType<FarmManager>();
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

    /// <summary>Every cell that has a crop growing on it (used by Meatball's tips).</summary>
    public IEnumerable<Vector3Int> PlantedCells { get { return planted.Keys; } }

    /// <summary>The crop growing at this cell, or null.</summary>
    public CropData CropAt(Vector3Int cell)
    {
        CropInstance c;
        return planted.TryGetValue(cell, out c) ? c.data : null;
    }

    /// <summary>True if a crop here still needs water today.</summary>
    public bool IsThirsty(Vector3Int cell)
    {
        CropInstance c;
        if (!planted.TryGetValue(cell, out c)) return false;
        return !c.withered && c.data.needsWater && !c.data.IsFullyGrown(c.daysGrown) && farm != null && !farm.IsWatered(cell);
    }

    public bool IsRipe(Vector3Int cell)
    {
        CropInstance c;
        return planted.TryGetValue(cell, out c) && !c.withered && c.data.IsFullyGrown(c.daysGrown);
    }

    /// <summary>True if the crop here died when its season ended (the scythe or shovel clears it).</summary>
    public bool IsWithered(Vector3Int cell)
    {
        CropInstance c;
        return planted.TryGetValue(cell, out c) && c.withered;
    }

    /// <summary>The season right now (Spring if the scene has no TimeManager).</summary>
    private static TimeManager.Season SeasonNow
    {
        get { return TimeManager.Instance != null ? TimeManager.Instance.CurrentSeason : TimeManager.Season.Spring; }
    }

    /// <summary>
    /// Why this crop can't be planted right now, as a sentence for the player - or null if it can.
    /// PlayerActions shows this when you try to plant out of season.
    /// </summary>
    public string WhyNotPlantable(CropData data)
    {
        if (data == null || TimeManager.Instance == null) return null;
        TimeManager.Season season = SeasonNow;
        if (data.CanGrowIn(season)) return null;
        if (season == TimeManager.Season.Winter) return "Nothing grows in winter. Wait for spring.";
        string seasons = data.seedItem != null ? data.seedItem.PlantSeasonsText() : "another season";
        return data.cropName + " doesn't grow in " + season + ". Plant it in: " + seasons + ".";
    }

    // ------------------------------------------------------------------
    // Planting and harvesting
    // ------------------------------------------------------------------

    public bool TryPlant(Vector3Int cell, CropData data)
    {
        if (data == null || farm == null) return false;
        if (!farm.IsTilled(cell) || planted.ContainsKey(cell)) return false;
        if (WhyNotPlantable(data) != null) return false;      // wrong season (or winter)

        var go = new GameObject("Crop: " + data.cropName);
        go.transform.SetParent(holder, false);
        go.transform.position = farm.CellToWorldCenter(cell) + (Vector3)plantOffset;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        var crop = new CropInstance { data = data, cell = cell, renderer = sr };
        if (swayInWind)
        {
            crop.sway = go.AddComponent<WindSway>();          // the amount is set in Refresh, from how tall the plant is
            crop.sway.speed = 1.6f;
            crop.sway.stretch = 0f;
        }
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

        // A withered crop gives nothing; the scythe just clears it away so the soil can be used again.
        if (crop.withered)
        {
            GameEvents.ShowToast("Cleared the withered " + crop.data.cropName.ToLowerInvariant() + ".");
            Remove(crop);
            return true;
        }

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
        // This runs as the new day begins, so SeasonNow is already the new day's season.
        TimeManager.Season season = SeasonNow;
        int withered = 0;

        foreach (CropInstance crop in planted.Values)
        {
            if (crop.withered) continue;

            // Season over for this crop? It dries up, ripe or not - harvest before the season ends!
            if (witherOutOfSeason && TimeManager.Instance != null && !crop.data.CanGrowIn(season))
            {
                crop.withered = true;
                withered++;
                Refresh(crop);
                continue;
            }

            if (crop.data.IsFullyGrown(crop.daysGrown)) continue;   // ripe crops keep the level they ripened with

            Sample(crop);

            if (crop.data.needsWater && (farm == null || !farm.IsWatered(crop.cell))) { Refresh(crop); continue; }

            float chance = PollutionSettings.Current.GrowthChance(FeltLevel(crop));
            if (Random.value <= chance) crop.daysGrown++;

            Refresh(crop);
        }

        if (withered > 0)
            GameEvents.ShowToast(season == TimeManager.Season.Winter
                ? "Winter is here. " + withered + " crop" + (withered == 1 ? " has" : "s have") + " withered."
                : withered + " crop" + (withered == 1 ? " has" : "s have") + " withered: the season changed.");
    }

    // ------------------------------------------------------------------
    // Little touches
    // ------------------------------------------------------------------

    private void Update()
    {
        if (!ripeSparkles || planted.Count == 0 || Time.time < nextSparkle) return;
        nextSparkle = Time.time + Mathf.Max(0.1f, sparkleEvery);

        // Look at one random crop; if it is ripe and on screen, it twinkles.
        int pick = Random.Range(0, planted.Count), i = 0;
        foreach (CropInstance crop in planted.Values)
        {
            if (i++ != pick) continue;
            if (crop.withered || crop.renderer == null || !crop.renderer.isVisible || !crop.data.IsFullyGrown(crop.daysGrown)) break;

            Vector3 at = crop.renderer.bounds.center + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0f, 0.3f), 0f);
            FxFade spark = CombatFx.Spawn(RuntimeSprites.Square, at, 45f, Vector2.one * 0.03f, Vector2.one * 0.13f,
                                          new Color(1f, 0.97f, 0.7f, 0.95f), 0.45f, crop.renderer.sortingOrder + 1, crop.renderer.sortingLayerID);
            spark.drift = Vector2.up * 0.35f;
            break;
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

        if (crop.withered)
        {
            // Dried up: brown, drooping to one side, and no longer moving in the wind.
            crop.renderer.color = witheredTint;
            if (crop.sway != null) crop.sway.enabled = false;
            float side = (crop.cell.x + crop.cell.y) % 2 == 0 ? 1f : -1f;      // neighbours droop different ways
            crop.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, witheredLean * side);
            return;
        }

        crop.renderer.color = PollutionSettings.Current.CropTint(FeltLevel(crop));
        if (crop.sway != null) crop.sway.maxAngle = Mathf.Lerp(swayWhenSmall, swayWhenGrown, crop.data.GrowthFraction(crop.daysGrown));
    }
}
