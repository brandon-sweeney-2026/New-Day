using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One entry in the interior decor catalogue: a sofa, a fridge, a window, a floor, a wallpaper...
/// Made by Farm > Set Up > Interior Decor from the pictures that Tools/make_interior_art.ps1 draws
/// (in Assets/Farm/Interiors/Items). You can also make your own: right-click > Create > Farm > Decor Item,
/// fill in the pictures, then add it to the DecorCatalog asset so the Interior Decorator window lists it.
///
/// Furniture has one picture per side (Front, Right, Back, Left) so it can be turned and you see its back.
/// Flooring uses 'Floor Tile'; a wall covering uses the three wall tiles.
/// </summary>
[CreateAssetMenu(fileName = "NewDecorItem", menuName = "Farm/Decor Item")]
public class DecorItem : ScriptableObject
{
    [Header("Basics")]
    [Tooltip("Short unique name used by the setup menu, e.g. couch_cottage.")]
    public string id;
    public string displayName = "New Decor";
    [TextArea] public string description;
    public DecorCategory category;
    [Tooltip("cottage, modern, rustic, retro, coastal, or any. The Interior Decorator can restyle a room to one theme.")]
    public string theme = "any";
    [Tooltip("What it would cost (for a shop later). Not used yet.")]
    [Min(0)] public int price = 100;

    [Header("Placing")]
    public DecorMount mount = DecorMount.Floor;
    [Tooltip("Floor space when facing Front, in tiles across. Turned Right / Left, width and depth swap.")]
    [Min(1)] public int width = 1;
    [Tooltip("Floor space when facing Front, in tiles from front to back. For Wall pieces: tiles tall.")]
    [Min(1)] public int depth = 1;
    [Tooltip("Floor pieces only: the player can't walk through it.")]
    public bool blocksPlayer = true;
    [Tooltip("Height in pixels of the top that Surface pieces (blenders, toasters) stand on. 0 = nothing can stand on it.")]
    [Min(0)] public int surfaceHeight = 0;

    [Header("Pictures (one per side; empty sides use the Front picture)")]
    public Sprite front;
    public Sprite right;
    public Sprite back;
    public Sprite left;

    [Header("Flooring")]
    public TileBase floorTile;

    [Header("Wall covering (top row has the moulding, base row the skirting)")]
    public TileBase wallTop;
    public TileBase wallUpper;
    public TileBase wallBase;

    /// <summary>True for pieces that can be turned (wall pieces, floors and wallpapers can't).</summary>
    public bool CanRotate { get { return mount != DecorMount.Wall && category != DecorCategory.Flooring && category != DecorCategory.WallCovering; } }

    /// <summary>True for a floor or a wall covering (they paint the room instead of being placed).</summary>
    public bool IsSurface { get { return category == DecorCategory.Flooring || category == DecorCategory.WallCovering; } }

    /// <summary>The picture for one side.</summary>
    public Sprite SpriteFor(DecorFacing f)
    {
        Sprite s = null;
        switch (f)
        {
            case DecorFacing.Right: s = right; break;
            case DecorFacing.Back: s = back; break;
            case DecorFacing.Left: s = left; break;
        }
        return s != null ? s : front;
    }

    /// <summary>Floor space in tiles for a facing (turned sideways = width and depth swap).</summary>
    public Vector2Int SizeFor(DecorFacing f)
    {
        bool side = (f == DecorFacing.Right || f == DecorFacing.Left) && CanRotate;
        return side ? new Vector2Int(depth, width) : new Vector2Int(width, depth);
    }

    /// <summary>The next facing when turning clockwise (seen from above): Front -> Left -> Back -> Right -> Front.</summary>
    public static DecorFacing Turn(DecorFacing f, bool clockwise)
    {
        DecorFacing[] order = { DecorFacing.Front, DecorFacing.Left, DecorFacing.Back, DecorFacing.Right };
        int i = System.Array.IndexOf(order, f);
        return order[(i + (clockwise ? 1 : 3)) % 4];
    }
}
