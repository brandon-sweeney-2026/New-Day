using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Shared settings for the Healing Forest: which floor tile turns into which when a zone heals, how the green
/// healing wave looks, and the friendly animals that appear afterwards.
///
/// The game loads it from Assets/Resources/ForestHealSettings.asset. Farm > Set Up > Forest Scene makes it and fills
/// in the pictures. Without the asset the built-in defaults below are used (but then no tiles or animals are known).
/// </summary>
[CreateAssetMenu(fileName = "ForestHealSettings", menuName = "Farm/Forest Heal Settings")]
public class ForestHealSettings : ScriptableObject
{
    /// <summary>One floor tile and the tile it becomes when its zone heals.</summary>
    [System.Serializable]
    public class TilePair
    {
        public TileBase sick;
        public TileBase healthy;
    }

    /// <summary>The pictures and manners of one kind of friendly animal.</summary>
    [System.Serializable]
    public class CritterArt
    {
        [Tooltip("Just a name, e.g. 'rabbit'.")]
        public string id = "rabbit";
        [Tooltip("The shadow animal of this style turns into this animal when its zone heals.")]
        public ShadowCreature.Style shadowStyle = ShadowCreature.Style.Hop;
        [Tooltip("Row 0 of the critter sheet: breathing and blinking.")] public Sprite[] idleFrames;
        [Tooltip("Row 1: one step / hop (loops).")] public Sprite[] moveFrames;
        [Tooltip("Row 2: its special move (sit up, nibble, curl up, flap, hide in shell). Played once now and then, and when you pet it.")] public Sprite[] specialFrames;
        [Min(0.1f)] public float walkSpeed = 1.2f;
        [Min(1f)] public float framesPerSecond = 8f;
        [Tooltip("Hops / bounds lift it a little (bird = tiny hops). 0 = it walks.")]
        [Min(0f)] public float hopHeight = 0f;
        [Tooltip("Tick for animals that don't mind you (they won't run when you come close).")]
        public bool tame = false;
    }

    [Header("Ground")]
    [Tooltip("Each sick floor tile and the healthy tile it becomes. Tiles not listed are left alone.")]
    public TilePair[] tilePairs = new TilePair[0];

    [Header("The healing wave")]
    [Tooltip("Seconds for the green wave to spread from the heart stump over the whole zone.")]
    [Min(0.5f)] public float waveSeconds = 3.5f;
    [Tooltip("Colour of the sparkles along the edge of the wave.")]
    public Color sparkleColor = new Color(0.7f, 1f, 0.55f, 0.95f);
    [Tooltip("Sparkles per second along the edge of the wave.")]
    [Min(0)] public int sparklesPerSecond = 60;
    [Tooltip("A soft green light that swells at the stump while the wave spreads.")]
    public bool waveLight = true;
    public Color waveLightColor = new Color(0.6f, 1f, 0.6f, 1f);
    [Min(0f)] public float waveLightIntensity = 1.6f;

    [Header("Sick look")]
    [Tooltip("Drifting purple fog patches over a zone while it is sick.")]
    public Color fogColor = new Color(0.45f, 0.2f, 0.65f, 0.16f);
    [Tooltip("How long the fog takes to fade once the zone heals.")]
    [Min(0.1f)] public float fogFadeSeconds = 2.5f;

    [Header("Friendly animals")]
    public CritterArt[] critters = new CritterArt[0];
    [Tooltip("GameState counter added to each time you pet an animal (once per animal per day).")]
    public string pettedCounter = "forest_critters_petted";

    // ------------------------------------------------------------------ finding the settings
    private static ForestHealSettings current;

    /// <summary>The settings asset in Resources, or the built-in defaults if there is none.</summary>
    public static ForestHealSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<ForestHealSettings>("ForestHealSettings");
                if (current == null) current = CreateInstance<ForestHealSettings>();
            }
            return current;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; }

    /// <summary>The healthy tile for a sick one (null if the tile isn't in the list).</summary>
    public TileBase HealthyFor(TileBase sick)
    {
        if (sick == null || tilePairs == null) return null;
        foreach (TilePair p in tilePairs) if (p != null && p.sick == sick) return p.healthy;
        return null;
    }

    /// <summary>The sick tile for a healthy one (used when a zone is reset by the test menu).</summary>
    public TileBase SickFor(TileBase healthy)
    {
        if (healthy == null || tilePairs == null) return null;
        foreach (TilePair p in tilePairs) if (p != null && p.healthy == healthy) return p.sick;
        return null;
    }

    /// <summary>The friendly animal a shadow animal of this style becomes (null if none is set up).</summary>
    public CritterArt CritterFor(ShadowCreature.Style style)
    {
        if (critters == null) return null;
        foreach (CritterArt c in critters) if (c != null && c.shadowStyle == style) return c;
        return null;
    }
}
