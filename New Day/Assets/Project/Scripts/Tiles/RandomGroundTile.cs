using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A tile that shows one of several sprites chosen at random, so a big grass field doesn't look like wallpaper.
/// The choice depends only on the cell position, so the same cell always shows the same sprite
/// (repainting or reloading the scene never reshuffles your map).
///
/// Each variant has a weight: 1 = common, 0.06 = rare (a flower tuft every so often).
/// </summary>
[CreateAssetMenu(fileName = "NewRandomGroundTile", menuName = "Farm/Random Ground Tile")]
public class RandomGroundTile : TileBase
{
    [Serializable]
    public class Variant
    {
        public Sprite sprite;
        [Min(0f)] public float weight = 1f;
    }

    public Variant[] variants = new Variant[0];

    [Tooltip("Changing this reshuffles which cell gets which variant.")]
    public int seed = 0;

    public Tile.ColliderType colliderType = Tile.ColliderType.None;

    /// <summary>Sprite shown in the level editor's palette.</summary>
    public Sprite previewSprite;

    private Sprite Pick(Vector3Int position)
    {
        if (variants == null || variants.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null) total += Mathf.Max(0f, variants[i].weight);
        }
        if (total <= 0f) return variants[0] != null ? variants[0].sprite : null;

        // Cheap position hash -> a repeatable number between 0 and 1.
        uint h;
        unchecked
        {
            h = (uint)(position.x * 73856093) ^ (uint)(position.y * 19349663) ^ (uint)(seed * 83492791);
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
        }
        float r = (h / (float)uint.MaxValue) * total;

        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] == null) continue;
            r -= Mathf.Max(0f, variants[i].weight);
            if (r <= 0f) return variants[i].sprite;
        }
        return variants[variants.Length - 1] != null ? variants[variants.Length - 1].sprite : null;
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = Pick(position);
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
        tileData.colliderType = colliderType;
    }
}
