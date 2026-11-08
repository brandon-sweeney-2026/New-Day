using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which house options you own and which are in use, and applies them to the exterior and interior.
///
///  - 'catalog' lists every HouseUpgradeData in the game. Add your own assets to it.
///  - Buying takes gold AND materials from the player's backpack.
///  - Put this on an empty GameObject. The Setup menu (Farm > House) does it for you.
/// </summary>
public class HouseManager : MonoBehaviour
{
    public static HouseManager Instance { get; private set; }

    [Header("Options")]
    [SerializeField] private HouseUpgradeData[] catalog;

    [Header("Exterior (tinted with the siding / roof colour)")]
    [SerializeField] private SpriteRenderer sidingRenderer;
    [SerializeField] private SpriteRenderer roofRenderer;

    [Header("Interior")]
    [SerializeField] private HouseInterior interior;

    private readonly Dictionary<HouseCategory, HouseUpgradeData> selected = new Dictionary<HouseCategory, HouseUpgradeData>();
    private readonly HashSet<string> owned = new HashSet<string>();

    /// <summary>Raised whenever something is bought or changed.</summary>
    public event System.Action OnChanged;

    public HouseInterior Interior { get { return interior; } }

    private void Awake()
    {
        Instance = this;
        if (catalog == null) catalog = new HouseUpgradeData[0];

        // The default of each category is free and already owned.
        foreach (HouseUpgradeData o in catalog)
        {
            if (o == null || !o.isDefault) continue;
            owned.Add(o.name);
            selected[o.category] = o;
        }
    }

    private void Start()
    {
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // Queries
    // ------------------------------------------------------------------

    public List<HouseUpgradeData> Options(HouseCategory category)
    {
        var list = new List<HouseUpgradeData>();
        foreach (HouseUpgradeData o in catalog) if (o != null && o.category == category) list.Add(o);
        return list;
    }

    public bool IsOwned(HouseUpgradeData o) { return o != null && owned.Contains(o.name); }

    public bool IsSelected(HouseUpgradeData o)
    {
        HouseUpgradeData s;
        return o != null && selected.TryGetValue(o.category, out s) && s == o;
    }

    public HouseUpgradeData Selected(HouseCategory category)
    {
        HouseUpgradeData s;
        return selected.TryGetValue(category, out s) ? s : null;
    }

    /// <summary>Extra cooking servings chance from the kitchen you have installed.</summary>
    public float FoodBonus
    {
        get
        {
            HouseUpgradeData k = Selected(HouseCategory.Kitchen);
            return k != null ? k.foodBonus : 0f;
        }
    }

    /// <summary>True if the player has the gold and materials. 'missing' says what is short.</summary>
    public bool CanAfford(HouseUpgradeData o, Inventory inv, out string missing)
    {
        missing = "";
        if (o == null) return false;

        var lacks = new List<string>();
        if (GameState.Gold < o.goldCost) lacks.Add((o.goldCost - GameState.Gold) + "g");
        foreach (MaterialCost m in o.materials)
        {
            if (m == null || m.item == null) continue;
            int have = inv != null ? inv.CountOf(m.item) : 0;
            if (have < m.count) lacks.Add((m.count - have) + " " + m.item.itemName);
        }

        missing = string.Join(", ", lacks);
        return lacks.Count == 0;
    }

    // ------------------------------------------------------------------
    // Buying and choosing
    // ------------------------------------------------------------------

    /// <summary>Pays for an option, then uses it. Returns true on success.</summary>
    public bool TryBuy(HouseUpgradeData o, Inventory inv)
    {
        if (o == null || IsOwned(o)) return false;

        string missing;
        if (!CanAfford(o, inv, out missing))
        {
            GameEvents.ShowToast("You still need: " + missing);
            return false;
        }

        GameState.TrySpendGold(o.goldCost);
        foreach (MaterialCost m in o.materials)
            if (m != null && m.item != null && inv != null) inv.Remove(m.item, m.count);

        owned.Add(o.name);
        GameState.AddCounter("house_upgrades");
        GameEvents.ShowToast("Bought " + o.displayName + "!");
        Select(o);
        return true;
    }

    /// <summary>Uses an option you already own.</summary>
    public void Select(HouseUpgradeData o)
    {
        if (o == null || !IsOwned(o)) return;
        selected[o.category] = o;
        Apply(o);
        if (OnChanged != null) OnChanged();
    }

    // ------------------------------------------------------------------
    // Showing it
    // ------------------------------------------------------------------

    public void ApplyAll()
    {
        foreach (var kv in selected) Apply(kv.Value);
    }

    private void Apply(HouseUpgradeData o)
    {
        switch (o.category)
        {
            case HouseCategory.Siding:
                if (sidingRenderer != null) sidingRenderer.color = o.tint;
                break;
            case HouseCategory.Roof:
                if (roofRenderer != null) roofRenderer.color = o.tint;
                break;
            case HouseCategory.Floor:
                if (interior != null) interior.PaintFloor(o.tile);
                break;
            case HouseCategory.Wall:
                if (interior != null) interior.PaintWall(o.tile, o.tileBase);
                break;
            default:
                if (interior != null) interior.SetFurniture(o.category, o.sprite);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Saving (simple text; plug into your save system later)
    // ------------------------------------------------------------------

    public string SaveData()
    {
        var parts = new List<string>();
        foreach (string id in owned) parts.Add("own:" + id);
        foreach (var kv in selected) parts.Add("use:" + kv.Value.name);
        return string.Join("\n", parts);
    }

    public void LoadData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;

        var byName = new Dictionary<string, HouseUpgradeData>();
        foreach (HouseUpgradeData o in catalog) if (o != null) byName[o.name] = o;

        foreach (string line in data.Split('\n'))
        {
            HouseUpgradeData o;
            if (line.StartsWith("own:")) owned.Add(line.Substring(4));
            else if (line.StartsWith("use:") && byName.TryGetValue(line.Substring(4), out o)) selected[o.category] = o;
        }
        ApplyAll();
        if (OnChanged != null) OnChanged();
    }
}
