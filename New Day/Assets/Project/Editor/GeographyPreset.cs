using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One "geography": everything the Geography Brush needs to paint a path, road, sidewalk... in one stroke.
/// Made by Farm > Level Editors > Make Geography Presets (Mountain Pathway, Road, Sidewalk, Forest Pathway).
/// Select the asset in Assets/Farm/Data/Geography (or open it inside the Geography Brush window) to change anything.
/// This file must live in a folder named "Editor".
/// </summary>
public class GeographyPreset : ScriptableObject
{
    public enum BrushShape { Round, Square }

    /// <summary>Path = a surface on the Paths layer (roads, trails). Cliff = solid rock on the Cliffs layer that the player cannot walk through.</summary>
    public enum Style { Path, Cliff }

    /// <summary>Where a scatter rule is allowed to put things, relative to the painted stroke.</summary>
    public enum Zone
    {
        [Tooltip("Inside the path, away from its outline.")] PathCore,
        [Tooltip("The outline cells of the path (both sides).")] PathEdge,
        [Tooltip("Just outside the path (see Shoulder Width).")] Shoulder,
        [Tooltip("Anywhere on the path or its shoulder.")] Anywhere
    }

    [System.Serializable]
    public class ScatterRule
    {
        public string label = "Scatter";
        [Tooltip("Tiles (drawn on the PathDecor layer) and/or prefabs (placed under 'Objects'). One is picked at random each time.")]
        public Object[] assets;
        public Zone zone = Zone.PathCore;
        [Tooltip("Chance for each cell in the zone to get one (0.1 = about one cell in ten).")]
        [Range(0f, 1f)] public float chance = 0.1f;
        [Tooltip("Keep at least this many cells between two of these (0 = may touch).")]
        [Min(0)] public int minSpacing = 0;
        [Tooltip("Only used when the brush width is at least this wide.")]
        [Range(1, 10)] public int minBrushWidth = 1;
    }

    [Header("Name")]
    public string title = "New Geography";
    [Tooltip("The Build Path Palettes group this was filled from (e.g. MountainPath). Only used by the Reset button.")]
    public string paletteGroup = "";

    [Header("Style")]
    [Tooltip("Cliff paints on its own Cliffs layer (made for you) with a collider, so the player cannot walk on it.")]
    public Style style = Style.Path;

    [Header("Brush")]
    public BrushShape shape = BrushShape.Round;
    [Range(1, 10)] public int defaultWidth = 3;
    [Tooltip("How much the path width wanders along the stroke, in tiles (0 = perfectly even). Makes natural paths.")]
    [Range(0, 3)] public int widthWobble = 0;
    [Tooltip("Joins to path tiles of this geography that are already painted (crossroads, T-junctions) and fixes their edges.")]
    public bool mergeWithExisting = true;
    [Tooltip("How many tiles outside the path the Shoulder zone reaches.")]
    [Range(0, 6)] public int shoulderWidth = 2;

    [Header("Surface")]
    [Tooltip("Paint the surface on the GROUND layer instead of the Paths layer (docks: so the water next to them reacts and shows stilts).")]
    public bool paintOnGroundLayer = false;
    [Tooltip("Off = every cell gets a plain ground variant, no edge or corner pieces (docks, floors).")]
    public bool useEdgeTiles = true;
    [Tooltip("Plain ground variants. Each inner cell gets a random one, so the path is never flat.")]
    public TileBase[] fillTiles;
    [Tooltip("The 3x3 block in this order: TopLeft, Top, TopRight, Left, Middle, Right, BottomLeft, Bottom, BottomRight.")]
    public TileBase[] wideTiles = new TileBase[9];
    [Tooltip("Inner corners in this order: TL, TR, BL, BR.")]
    public TileBase[] innerTiles = new TileBase[4];
    [Tooltip("The self-connecting tile. Used for 1-wide paths and 1-wide bits.")]
    public TileBase autoTile;
    [Tooltip("Tick this if the inner corners look like they point the wrong way.")]
    public bool swapInnerCorners = false;

    [Header("Cliff pieces (only used when Style = Cliff; Fill Tiles above are the plateau top)")]
    [Tooltip("Upper wall row, random variant each cell.")] public TileBase[] cliffFaceUpper;
    [Tooltip("Lower wall row (the foot), random variant each cell.")] public TileBase[] cliffFaceLower;
    [Tooltip("Top rim pieces in this order: Top, Left, Right, TopLeft, TopRight.")] public TileBase[] cliffLip = new TileBase[5];
    [Tooltip("Inner top corners: TL, TR.")] public TileBase[] cliffInner = new TileBase[2];
    [Tooltip("Wall side-end pieces: UpperLeft, UpperRight, LowerLeft, LowerRight.")] public TileBase[] cliffFaceEdges = new TileBase[4];
    [Tooltip("How ragged the rim and sides are (0 = clean block, 1 = very chewed up). Works from width 3.")]
    [Range(0f, 1f)] public float roughEdges = 0.2f;

    [Header("Centre line (roads)")]
    public TileBase centerHorizontal;
    public TileBase centerVertical;
    [Tooltip("Only draw the centre line when the brush is this wide or wider.")]
    [Range(1, 10)] public int centerMinWidth = 4;
    [Min(1)] public int dashOn = 2;
    [Tooltip("0 = solid line.")]
    [Min(0)] public int dashOff = 2;

    [Header("Variable features")]
    public ScatterRule[] scatter = new ScatterRule[0];
}
