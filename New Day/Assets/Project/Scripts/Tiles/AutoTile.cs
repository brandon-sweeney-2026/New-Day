using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A tile that automatically picks the right sprite for its edges and corners based on its neighbours
/// (so dirt patches, ponds, paths, tilled soil and fences join up as you paint).
///
/// Two modes:
///   Blob47    - 8 neighbours (edges AND corners). Used for terrain, soil and paths.
///   Cardinal4 - 4 neighbours (up/right/down/left). Used for fences.
///
/// You normally never create these by hand: the Kiln Harvest importer (Farm > Kiln Harvest) makes them.
/// To make your own, use Create > Farm > Auto Tile and fill in the Pieces list.
///
/// Neighbour bits:
///   Blob47:    N=1  NE=2  E=4  SE=8  S=16  SW=32  W=64  NW=128   (a corner only counts if both its sides are set)
///   Cardinal4: N=1  E=2  S=4  W=8
/// </summary>
[CreateAssetMenu(fileName = "NewAutoTile", menuName = "Farm/Auto Tile")]
public class AutoTile : TileBase
{
    public enum Mode { Blob47, Cardinal4 }

    /// <summary>The sprite(s) for one neighbour pattern. One frame = still image, several = animated (water).</summary>
    [Serializable]
    public class Piece
    {
        public Sprite[] frames;
    }

    [Tooltip("Blob47 looks at all 8 neighbours. Cardinal4 only looks up/right/down/left.")]
    public Mode mode = Mode.Blob47;

    [Tooltip("Indexed by neighbour mask. Blob47 uses indices 0-255 (only 47 are ever used). Cardinal4 uses 0-15.")]
    public Piece[] pieces = new Piece[256];

    [Tooltip("Playback speed for animated pieces, in frames per second.")]
    public float animationFramesPerSecond = 4f;

    [Tooltip("None = walk through it. Grid = solid, fills the whole cell (use for water and fences with a TilemapCollider2D).")]
    public Tile.ColliderType colliderType = Tile.ColliderType.None;

    /// <summary>Sprite shown in the level editor's palette.</summary>
    public Sprite previewSprite;

    // ------------------------------------------------------------------
    // Neighbour maths (public + static so it is easy to test or reuse)
    // ------------------------------------------------------------------

    /// <summary>Turns the 8 neighbour flags into one of the 47 canonical blob masks.</summary>
    public static int BlobMask(bool n, bool ne, bool e, bool se, bool s, bool sw, bool w, bool nw)
    {
        int m = 0;
        if (n) m |= 1;
        if (e) m |= 4;
        if (s) m |= 16;
        if (w) m |= 64;
        // Corners only matter when both of the sides next to them are present.
        if (ne && n && e) m |= 2;
        if (se && e && s) m |= 8;
        if (sw && s && w) m |= 32;
        if (nw && w && n) m |= 128;
        return m;
    }

    public static int CardinalMask(bool n, bool e, bool s, bool w)
    {
        int m = 0;
        if (n) m |= 1;
        if (e) m |= 2;
        if (s) m |= 4;
        if (w) m |= 8;
        return m;
    }

    private bool IsSame(ITilemap tilemap, Vector3Int position, int dx, int dy)
    {
        return tilemap.GetTile(new Vector3Int(position.x + dx, position.y + dy, position.z)) == this;
    }

    private int ComputeMask(Vector3Int position, ITilemap tilemap)
    {
        if (mode == Mode.Cardinal4)
        {
            return CardinalMask(
                IsSame(tilemap, position, 0, 1),
                IsSame(tilemap, position, 1, 0),
                IsSame(tilemap, position, 0, -1),
                IsSame(tilemap, position, -1, 0));
        }

        return BlobMask(
            IsSame(tilemap, position, 0, 1),    // N
            IsSame(tilemap, position, 1, 1),    // NE
            IsSame(tilemap, position, 1, 0),    // E
            IsSame(tilemap, position, 1, -1),   // SE
            IsSame(tilemap, position, 0, -1),   // S
            IsSame(tilemap, position, -1, -1),  // SW
            IsSame(tilemap, position, -1, 0),   // W
            IsSame(tilemap, position, -1, 1));  // NW
    }

    private Piece PieceFor(int mask)
    {
        if (pieces == null || mask < 0 || mask >= pieces.Length) return null;
        return pieces[mask];
    }

    // ------------------------------------------------------------------
    // TileBase overrides
    // ------------------------------------------------------------------

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        Piece piece = PieceFor(ComputeMask(position, tilemap));

        tileData.sprite = (piece != null && piece.frames != null && piece.frames.Length > 0) ? piece.frames[0] : null;
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
        tileData.colliderType = colliderType;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        Piece piece = PieceFor(ComputeMask(position, tilemap));
        if (piece == null || piece.frames == null || piece.frames.Length < 2) return false;

        tileAnimationData.animatedSprites = piece.frames;
        tileAnimationData.animationSpeed = animationFramesPerSecond;
        tileAnimationData.animationStartTime = 0f;
        return true;
    }

    // When this tile is placed or removed, its neighbours need to redraw too.
    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                tilemap.RefreshTile(new Vector3Int(position.x + dx, position.y + dy, position.z));
            }
        }
    }
}
