using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A 1-tile-wide path that connects to its neighbours by itself.
/// Paint it with the Tile Palette brush and it picks the right piece (straight, corner, T, cross, end, dot).
/// Tiles with the same "group" text connect to each other, so you can make two different paths join.
/// Made for you by Farm > Biomes > Build Path Palettes.
/// </summary>
public class AutoPathTile : TileBase
{
    [Tooltip("Pieces indexed by which neighbours connect: North=1, East=2, South=4, West=8 (so 0 = lone dot, 5 = vertical straight, 10 = horizontal straight, 15 = cross).")]
    public Sprite[] sprites = new Sprite[16];

    [Tooltip("Path tiles with the same group connect to each other.")]
    public string group = "path";

    public Tile.ColliderType colliderType = Tile.ColliderType.None;

    private static readonly Vector3Int[] Dirs =
    {
        new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(-1, 0, 0)
    };

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        base.RefreshTile(position, tilemap);
        for (int i = 0; i < Dirs.Length; i++) tilemap.RefreshTile(position + Dirs[i]);
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        int mask = 0;
        for (int i = 0; i < Dirs.Length; i++)
        {
            var other = tilemap.GetTile(position + Dirs[i]) as AutoPathTile;
            if (other != null && other.group == group) mask |= 1 << i;
        }
        tileData.sprite = (sprites != null && mask < sprites.Length) ? sprites[mask] : null;
        tileData.color = Color.white;
        tileData.colliderType = colliderType;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
    }
}
