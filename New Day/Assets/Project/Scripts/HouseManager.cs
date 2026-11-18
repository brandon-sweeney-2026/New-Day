using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which house options you own and which are in use, and applies them to the exterior and interior.
///
///  - 'catalog' lists every HouseUpgradeData in the game. Add your own assets to it.
///  - Buying takes gold AND materials from the player's backpack.
///  - Put this on an empty GameObject. The Setup menu (Farm > Set Up > Build Farmhouse) does it for you.
///  - What you own is remembered for the whole play session, even when you change scenes. That is what lets you buy an
///    upgrade at Frank's store in the Suburbs (which has its own HouseManager with no house to paint) and find it on
///    the farmhouse when you get home.
/// </summary>
public class HouseManager : MonoBehaviour
{
    public static HouseManager Instance { get; private set; }

    [Header("Options")]
    [SerializeField] private HouseUpgradeData[] catalog;

    [Header("Exterior (tinted with the siding / roof colour)")]
    [SerializeField] private SpriteRenderer sidingRenderer;
    [SerializeField] private SpriteRenderer roofRenderer;

    [Tooltip("Front door and shutters are pictures drawn on top of the house and tinted with the option's colour.")]
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private SpriteRenderer shuttersRenderer;

    [Header("Interior")]
    [SerializeField] private HouseInterior interior;
    [Tooltip("Draws the windows on the back wall (Pack C). Optional.")]
    [SerializeField] private HouseWindows windows;

    [Header("Buying")]
    [Tooltip("ON: new upgrades can only be BOUGHT at Frank's Home & Hardware. The design table at home still lets you switch between things you own.")]
    public bool buyOnlyAtHardwareStore = false;

    private readonly Dictionary<HouseCategory, HouseUpgradeData> selected = new Dictionary<HouseCategory, HouseUpgradeData>();
    private readonly HashSet<string> owned = new HashSet<string>();

    // Remembered between scenes (static = shared by every HouseManager, and kept when a scene is unloaded).
    // Names of the upgrades you own, and the name of the one in use for each category.
    private static readonly HashSet<string> rememberedOwned = new HashSet<string>();
    private static readonly Dictionary<HouseCategory, string> rememberedSelected = new Dictionary<HouseCategory, string>();

    /// <summary>Forget everything when the game starts (pressing Play), so a new session begins with the defaults.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRemembered()
    {
        rememberedOwned.Clear();
        rememberedSelected.Clear();
    }

    /// <summary>
    /// What you have bought and picked, as text for the save file (used by SaveSystem). It reads the remembered
    /// lists, so it works in any scene - even one with no farmhouse in it.
    /// </summary>
    public static string SaveRemembered()
    {
        var parts = new List<string>();
        foreach (string id in rememberedOwned) parts.Add("own:" + id);
        foreach (var kv in rememberedSelected) parts.Add("use:" + kv.Key + "=" + kv.Value);
        return string.Join("\n", parts);
    }

    /// <summary>
    /// Puts back what SaveRemembered wrote. Call it BEFORE the scene loads: every HouseManager reads the
    /// remembered lists when it wakes up, and paints the house from them.
    /// </summary>
    public static void LoadRemembered(string data)
    {
        rememberedOwned.Clear();
        rememberedSelected.Clear();
        if (string.IsNullOrEmpty(data)) return;

        foreach (string line in data.Split('\n'))
        {
            if (line.StartsWith("own:")) rememberedOwned.Add(line.Substring(4));
            else if (line.StartsWith("use:"))
            {
                // "use:Roof=Red Roof"  ->  category Roof, option "Red Roof"
                string body = line.Substring(4);
                int eq = body.IndexOf('=');
                if (eq <= 0) continue;
                HouseCategory category;
                if (System.Enum.TryParse(body.Substring(0, eq), out category)) rememberedSelected[category] = body.Substring(eq + 1);
            }
        }
    }

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

        // Then put back what was bought or picked earlier in this play session (maybe in another scene).
        foreach (string id in rememberedOwned) owned.Add(id);
        foreach (HouseUpgradeData o in catalog)
        {
            string picked;
            if (o != null && rememberedSelected.TryGetValue(o.category, out picked) && picked == o.name) selected[o.category] = o;
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
        if (GameState.Gold < o.GoldPrice) lacks.Add((o.GoldPrice - GameState.Gold) + "g");
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

        GameState.TrySpendGold(o.GoldPrice);
        SterlingInfluence.NoteSale(HouseUpgradeData.PriceMultiplier);   // an upgrade bought at the Sterling tower (5% off) counts as dealing with Sterling
        foreach (MaterialCost m in o.materials)
            if (m != null && m.item != null && inv != null) inv.Remove(m.item, m.count);

        owned.Add(o.name);
        rememberedOwned.Add(o.name);
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
        rememberedSelected[o.category] = o.name;
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
            case HouseCategory.Door:
                ApplyOverlay(doorRenderer, o);
                break;
            case HouseCategory.Shutters:
                ApplyOverlay(shuttersRenderer, o);
                break;
            case HouseCategory.WindowStyle:
            case HouseCategory.WindowLayout:
                ApplyWindows();
                break;
            case HouseCategory.Floor:
                if (interior != null) interior.PaintFloor(o.tile);
                break;
            case HouseCategory.Wall:
                if (interior != null) interior.PaintWall(o.tile, o.tileBase, o.tileTop);
                break;
            default:
                if (interior != null) interior.SetFurniture(o.category, o.sprite);
                break;
        }
    }

    private static void ApplyOverlay(SpriteRenderer r, HouseUpgradeData o)
    {
        if (r == null) return;
        r.sprite = o.sprite;
        r.color = o.tint;
        r.enabled = o.sprite != null;      // an option with no picture means 'the original look / none'
    }

    private void ApplyWindows()
    {
        if (windows == null) return;
        HouseUpgradeData style = Selected(HouseCategory.WindowStyle);
        HouseUpgradeData layout = Selected(HouseCategory.WindowLayout);
        windows.Show(style != null ? style.sprite : null, layout != null ? layout.windowSpots : null);
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
            if (line.StartsWith("own:")) { owned.Add(line.Substring(4)); rememberedOwned.Add(line.Substring(4)); }
            else if (line.StartsWith("use:") && byName.TryGetValue(line.Substring(4), out o)) { selected[o.category] = o; rememberedSelected[o.category] = o.name; }
        }
        ApplyAll();
        if (OnChanged != null) OnChanged();
    }
}
