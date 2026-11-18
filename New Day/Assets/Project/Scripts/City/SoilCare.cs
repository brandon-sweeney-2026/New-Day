using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Looks after the things researched at the university and used on the farm:
///   - Sprinklers stand on a tile and water the tilled soil around them every morning. Walk up and press the action button to pick one up.
///   - Moisture gel makes a tile water itself for a few mornings.
///   - Fertilizer makes the NEXT crop harvested from that tile Silver, Gold or Platinum quality.
///   - Soil purifier cleans pollution; growth tonic makes a crop jump ahead.
/// What each item does is listed on the Soil Care Settings asset (Assets/Resources/SoilCareSettings.asset).
///
/// Nothing to set up: it creates itself in every scene that has a FarmManager. The player uses an item by selecting it in the
/// hotbar and pressing the action button facing a tile (PlayerActions calls SoilCare.TryUse).
/// </summary>
public class SoilCare : MonoBehaviour
{
    public static SoilCare Instance { get; private set; }

    /// <summary>What has been done to the soil of one scene. Kept while the game runs, so it is still there when you come back.</summary>
    private class SceneSoil
    {
        public readonly Dictionary<Vector3Int, int> moisture = new Dictionary<Vector3Int, int>();      // mornings of water left
        public readonly Dictionary<Vector3Int, int> fertilizer = new Dictionary<Vector3Int, int>();    // 1 Silver, 2 Gold, 3 Platinum
        public readonly Dictionary<Vector3Int, SoilItem> sprinklers = new Dictionary<Vector3Int, SoilItem>();
    }

    private static readonly Dictionary<string, SceneSoil> allSoil = new Dictionary<string, SceneSoil>();

    private FarmManager farm;
    private SceneSoil soil;
    private readonly Dictionary<Vector3Int, GameObject> sprinklerObjects = new Dictionary<Vector3Int, GameObject>();
    private readonly Dictionary<Vector3Int, SpriteRenderer> dots = new Dictionary<Vector3Int, SpriteRenderer>();   // the little marks on treated tiles

    // ------------------------------------------------------------------ creating itself
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        allSoil.Clear();
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() { EnsureExists(); }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { EnsureExists(); }

    private static void EnsureExists()
    {
        if (Instance != null) return;
        var farm = FindAnyObjectByType<FarmManager>();
        if (farm == null) return;                              // no farm here (a shop, the city...), so nothing to look after
        var go = new GameObject("SoilCare");
        Instance = go.AddComponent<SoilCare>();
        Instance.farm = farm;
        Instance.Load();
    }

    private void Load()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (!allSoil.TryGetValue(scene, out soil)) { soil = new SceneSoil(); allSoil[scene] = soil; }
        foreach (KeyValuePair<Vector3Int, SoilItem> s in soil.sprinklers) ShowSprinkler(s.Key, s.Value);
        foreach (Vector3Int cell in soil.moisture.Keys) RefreshDot(cell);
        foreach (Vector3Int cell in soil.fertilizer.Keys) RefreshDot(cell);
    }

    private void OnEnable() { FarmManager.OnDayAdvanced += Morning; }
    private void OnDisable() { FarmManager.OnDayAdvanced -= Morning; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ------------------------------------------------------------------ saving and loading (used by SaveSystem)
    /// <summary>Everything SoilCare has put on tiles, in every scene, as plain data for the save file.</summary>
    public static List<SavedSoilMark> CaptureForSave()
    {
        var list = new List<SavedSoilMark>();
        foreach (KeyValuePair<string, SceneSoil> scene in allSoil)
        {
            foreach (KeyValuePair<Vector3Int, int> m in scene.Value.moisture)
                list.Add(new SavedSoilMark { scene = scene.Key, kind = 0, x = m.Key.x, y = m.Key.y, value = m.Value });
            foreach (KeyValuePair<Vector3Int, int> f in scene.Value.fertilizer)
                list.Add(new SavedSoilMark { scene = scene.Key, kind = 1, x = f.Key.x, y = f.Key.y, value = f.Value });
            foreach (KeyValuePair<Vector3Int, SoilItem> s in scene.Value.sprinklers)
                if (s.Value != null && s.Value.item != null)
                    list.Add(new SavedSoilMark { scene = scene.Key, kind = 2, x = s.Key.x, y = s.Key.y, item = s.Value.item.name });
        }
        return list;
    }

    /// <summary>
    /// Puts back what CaptureForSave wrote. Call it BEFORE the scene loads: the SoilCare that is made for the
    /// new scene reads this and shows the sprinklers and marks.
    /// </summary>
    public static void LoadFromSave(List<SavedSoilMark> saved)
    {
        allSoil.Clear();
        if (saved == null) return;

        foreach (SavedSoilMark m in saved)
        {
            SceneSoil scene;
            if (!allSoil.TryGetValue(m.scene, out scene)) { scene = new SceneSoil(); allSoil[m.scene] = scene; }

            var cell = new Vector3Int(m.x, m.y, 0);
            if (m.kind == 0) scene.moisture[cell] = m.value;
            else if (m.kind == 1) scene.fertilizer[cell] = m.value;
            else
            {
                // A sprinkler is remembered by its item's name; the Soil Care Settings say what that item does.
                SoilItem what = SoilCareSettings.Current.Find(SaveCatalog.Current.FindItem(m.item));
                if (what != null) scene.sprinklers[cell] = what;
            }
        }
    }

    // ------------------------------------------------------------------ what other scripts ask
    /// <summary>Is this one of the items listed on the Soil Care Settings asset?</summary>
    public static bool IsSoilItem(ItemData item) { return SoilCareSettings.Current.Find(item) != null; }

    /// <summary>
    /// Called by CropManager when a crop is harvested: the quality (0 Bronze ... 3 Platinum) the fertilizer in this tile gives.
    /// The fertilizer is used up by that harvest.
    /// </summary>
    public static int UseFertilizer(Vector3Int cell)
    {
        if (Instance == null || Instance.soil == null) return 0;
        int tier;
        if (!Instance.soil.fertilizer.TryGetValue(cell, out tier)) return 0;
        Instance.soil.fertilizer.Remove(cell);
        Instance.RefreshDot(cell);
        return Mathf.Clamp(tier, 0, CropQuality.Max);
    }

    /// <summary>The player pressed the action button holding 'item'. Returns true if it was used up on the tile in front of them.</summary>
    public static bool TryUse(PlayerActions player, ItemData item)
    {
        SoilItem what = SoilCareSettings.Current.Find(item);
        if (what == null || player == null) return false;
        if (Instance == null || !player.HasTargetCell)
        {
            GameEvents.ShowToast(item.itemName + " is for the farm. Use it on your soil at home.");
            return false;
        }
        if (!Instance.Use(what, player.TargetCell)) return false;

        player.Inventory.RemoveFromSlot(player.Inventory.SelectedIndex, 1);
        player.RaiseItemUsed(item, 0.3f);
        GameState.AddCounter("soil_items_used");
        return true;
    }

    private bool Use(SoilItem what, Vector3Int cell)
    {
        CropManager crops = CropManager.Instance;
        switch (what.kind)
        {
            case SoilItemKind.Sprinkler:
                if (soil.sprinklers.ContainsKey(cell)) { GameEvents.ShowToast("There is already a sprinkler here."); return false; }
                if (crops != null && crops.HasCrop(cell)) { GameEvents.ShowToast("A crop is growing here. Put the sprinkler beside it."); return false; }
                soil.sprinklers[cell] = what;
                ShowSprinkler(cell, what);
                int wet = WaterAround(cell, what.power);
                GameEvents.ShowToast("Sprinkler placed." + (wet > 0 ? " It watered " + wet + " tile" + (wet == 1 ? "." : "s.") : " It waters tilled soil around it every morning."));
                return true;

            case SoilItemKind.MoistureGel:
                if (!farm.IsTilled(cell)) { GameEvents.ShowToast("Use the gel on tilled soil."); return false; }
                int left;
                if (soil.moisture.TryGetValue(cell, out left) && left >= what.power) { GameEvents.ShowToast("This soil already has gel in it (" + left + " more morning" + (left == 1 ? ")." : "s).")); return false; }
                soil.moisture[cell] = what.power;
                farm.TryWater(cell);
                RefreshDot(cell);
                GameEvents.ShowToast("The soil will water itself for " + what.power + " more morning" + (what.power == 1 ? "." : "s."));
                return true;

            case SoilItemKind.Fertilizer:
                if (!farm.IsTilled(cell)) { GameEvents.ShowToast("Use the fertilizer on tilled soil."); return false; }
                int tier;
                if (soil.fertilizer.TryGetValue(cell, out tier) && tier >= what.power) { GameEvents.ShowToast("This soil already has " + CropQuality.Name(tier) + " fertilizer in it."); return false; }
                soil.fertilizer[cell] = Mathf.Clamp(what.power, 1, CropQuality.Max);
                RefreshDot(cell);
                GameEvents.ShowToast("The next crop harvested here will be " + CropQuality.Name(what.power) + " quality.");
                return true;

            case SoilItemKind.Purifier:
                if (PollutionMap.Instance == null) { GameEvents.ShowToast("There is nothing to clean here."); return false; }
                PollutionMap.Instance.CleanCircle(cell, what.power);
                GameEvents.ShowToast("The soil around here is clean again.");
                return true;

            case SoilItemKind.GrowthTonic:
                if (crops == null || !crops.Boost(cell, what.power)) { GameEvents.ShowToast("Pour the tonic on a growing crop."); return false; }
                GameEvents.ShowToast("The crop shoots up!");
                return true;
        }
        return false;
    }

    /// <summary>Takes a sprinkler back off its tile and into the backpack (called by PlacedSprinkler).</summary>
    public void PickUpSprinkler(Vector3Int cell, Inventory inv)
    {
        SoilItem what;
        if (soil == null || !soil.sprinklers.TryGetValue(cell, out what) || inv == null) return;
        if (inv.CanFit(what.item, 1) < 1) { GameEvents.ShowToast("Your backpack is full."); return; }
        inv.Add(what.item, 1);
        soil.sprinklers.Remove(cell);
        GameObject go;
        if (sprinklerObjects.TryGetValue(cell, out go)) { if (go != null) Destroy(go); sprinklerObjects.Remove(cell); }
        GameEvents.ShowToast("Picked up the " + what.item.itemName + ".");
    }

    // ------------------------------------------------------------------ every morning
    /// <summary>Runs after the farm has dried out overnight: gel tiles and sprinklers water the soil again.</summary>
    private void Morning()
    {
        if (soil == null || farm == null) return;

        foreach (Vector3Int cell in new List<Vector3Int>(soil.moisture.Keys))
        {
            int left = soil.moisture[cell];
            if (!farm.IsTilled(cell) || left <= 0) { soil.moisture.Remove(cell); RefreshDot(cell); continue; }
            farm.TryWater(cell);
            if (left <= 1) soil.moisture.Remove(cell); else soil.moisture[cell] = left - 1;
            RefreshDot(cell);
        }
        foreach (KeyValuePair<Vector3Int, SoilItem> s in soil.sprinklers) WaterAround(s.Key, s.Value.power);
    }

    /// <summary>Waters the tilled tiles a sprinkler reaches. Returns how many it wet.</summary>
    private int WaterAround(Vector3Int centre, int power)
    {
        int wet = 0;
        int reach = power >= 3 ? 2 : 1;
        for (int x = -reach; x <= reach; x++)
            for (int y = -reach; y <= reach; y++)
            {
                if (x == 0 && y == 0) continue;
                if (power <= 1 && x != 0 && y != 0) continue;            // the basic sprinkler only reaches straight up, down, left and right
                if (farm.TryWater(centre + new Vector3Int(x, y, 0))) wet++;
            }
        return wet;
    }

    // ------------------------------------------------------------------ what you see
    private void ShowSprinkler(Vector3Int cell, SoilItem what)
    {
        var go = new GameObject("Sprinkler");
        go.transform.SetParent(transform, false);
        go.transform.position = farm.CellToWorldCenter(cell);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = what.placedSprite != null ? what.placedSprite : (what.item != null ? what.item.icon : null);
        sr.sortingOrder = 6;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.5f, 0.5f);
        box.isTrigger = true;                                    // you can walk over it, like the other sprinklers in the project
        var placed = go.AddComponent<PlacedSprinkler>();
        placed.cell = cell;
        placed.itemName = what.item != null ? what.item.itemName : "sprinkler";
        sprinklerObjects[cell] = go;
    }

    /// <summary>A small coloured mark in the corner of a treated tile: blue = gel, the quality colour = fertilizer.</summary>
    private void RefreshDot(Vector3Int cell)
    {
        int tier, wetDays;
        bool fert = soil.fertilizer.TryGetValue(cell, out tier);
        bool gel = soil.moisture.TryGetValue(cell, out wetDays);
        SpriteRenderer dot;
        dots.TryGetValue(cell, out dot);
        if (!fert && !gel)
        {
            if (dot != null) Destroy(dot.gameObject);
            dots.Remove(cell);
            return;
        }
        if (dot == null)
        {
            var go = new GameObject("SoilMark");
            go.transform.SetParent(transform, false);
            go.transform.position = farm.CellToWorldCenter(cell) + new Vector3(0.32f, -0.32f, 0f);
            go.transform.localScale = Vector3.one * 0.16f;
            dot = go.AddComponent<SpriteRenderer>();
            dot.sprite = RuntimeSprites.Square;
            dot.sortingOrder = 4;
            dots[cell] = dot;
        }
        dot.color = fert ? CropQuality.Color(tier) : new Color(0.35f, 0.7f, 1f);
    }
}
