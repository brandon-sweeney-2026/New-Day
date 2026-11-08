using System;
using UnityEngine;

/// <summary>
/// The 5-point pollution scale and what each level does. Everything here is editable in the Inspector.
///
///   Level 0 = clean (no effect)     Levels 1-5 = increasingly polluted
///
/// Per level you can set:
///   saleMultiplier  - the price of harvested produce is multiplied by this (1 = full price)
///   growthChance    - chance (0-1) that a watered crop actually grows on a given night
///   yieldMultiplier - multiplies how much you harvest
///   cropTint        - colour wash on the growing plant, so polluted fields look sick
///
/// Create the asset with Farm > Game > Set Up Game Systems In Scene, or Create > Farm > Pollution Settings.
/// </summary>
[CreateAssetMenu(fileName = "PollutionSettings", menuName = "Farm/Pollution Settings")]
public class PollutionSettings : ScriptableObject
{
    public const int MaxLevel = 5;

    [Serializable]
    public class Level
    {
        public string label = "Clean";
        [Min(0f)] public float saleMultiplier = 1f;
        [Range(0f, 1f)] public float growthChance = 1f;
        [Min(0f)] public float yieldMultiplier = 1f;
        public Color cropTint = Color.white;
        [Tooltip("Colour used for the small level marker on inventory slots.")]
        public Color markerColor = Color.white;
    }

    [Tooltip("Exactly 6 entries: index 0 = clean, 1-5 = pollution levels.")]
    public Level[] levels = DefaultLevels();

    // ------------------------------------------------------------------
    // Access (a default copy is used if no asset has been assigned yet)
    // ------------------------------------------------------------------

    private static PollutionSettings current;

    /// <summary>The settings the game is using. Set by the PollutionMap; falls back to built-in defaults.</summary>
    public static PollutionSettings Current
    {
        get
        {
            if (current == null)
            {
                current = CreateInstance<PollutionSettings>();
                current.hideFlags = HideFlags.DontSave;
                current.levels = DefaultLevels();
            }
            return current;
        }
        set { current = value; }
    }

    private Level Get(int level)
    {
        if (levels == null || levels.Length == 0) return null;
        return levels[Mathf.Clamp(level, 0, levels.Length - 1)];
    }

    public float SaleMultiplier(int level) { Level l = Get(level); return l != null ? l.saleMultiplier : 1f; }
    public float GrowthChance(int level) { Level l = Get(level); return l != null ? l.growthChance : 1f; }
    public float YieldMultiplier(int level) { Level l = Get(level); return l != null ? l.yieldMultiplier : 1f; }
    public Color CropTint(int level) { Level l = Get(level); return l != null ? l.cropTint : Color.white; }
    public Color MarkerColor(int level) { Level l = Get(level); return l != null ? l.markerColor : Color.white; }
    public string Label(int level) { Level l = Get(level); return l != null ? l.label : ""; }

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    public static Level[] DefaultLevels()
    {
        return new[]
        {
            Make("Clean",    1.00f, 1.00f, 1.00f, new Color(1.00f, 1.00f, 1.00f), new Color(0.40f, 0.85f, 0.40f)),
            Make("Trace",    0.90f, 1.00f, 1.00f, new Color(0.96f, 1.00f, 0.88f), new Color(0.75f, 0.90f, 0.30f)),
            Make("Light",    0.75f, 0.95f, 1.00f, new Color(0.90f, 0.96f, 0.76f), new Color(0.90f, 0.85f, 0.25f)),
            Make("Moderate", 0.55f, 0.85f, 0.90f, new Color(0.82f, 0.86f, 0.66f), new Color(0.95f, 0.65f, 0.20f)),
            Make("Heavy",    0.35f, 0.70f, 0.75f, new Color(0.74f, 0.66f, 0.62f), new Color(0.90f, 0.35f, 0.25f)),
            Make("Toxic",    0.15f, 0.50f, 0.50f, new Color(0.62f, 0.48f, 0.66f), new Color(0.65f, 0.15f, 0.75f)),
        };
    }

    private static Level Make(string label, float sale, float growth, float yield, Color tint, Color marker)
    {
        return new Level
        {
            label = label, saleMultiplier = sale, growthChance = growth,
            yieldMultiplier = yield, cropTint = tint, markerColor = marker
        };
    }

    private void Reset()
    {
        levels = DefaultLevels();
    }
}
