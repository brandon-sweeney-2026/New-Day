using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>One fish that lives in a water area, and how common it is there.</summary>
[System.Serializable]
public class FishChance
{
    public FishData fish;
    [Min(0f)] [Tooltip("Bigger = more common in this water.")] public float weight = 10f;
}

/// <summary>
/// A fishing area. Put this on an object, size the blue box over the water, and fill in "Fish Here". Every water area has its own list,
/// so the pond, the river and the mountain lake can hold different fish. The player must face a spot inside the box to fish.
///
/// Optional: drag your water Tilemap into "Water Tilemap" and fishing only works on painted water tiles inside the box (good for ponds that aren't rectangles).
/// In the Level Editor you can paint the Water_ pieces from the Objects palette and drag the box handles in the Scene view.
/// </summary>
public class FishingWater : MonoBehaviour
{
    public static readonly List<FishingWater> All = new List<FishingWater>();

    public string areaName = "Pond";
    public Vector2 boxSize = new Vector2(10f, 6f);
    [Tooltip("Optional. Only painted tiles of this tilemap (inside the box) count as water.")]
    public Tilemap waterTilemap;
    [Tooltip("The fish that live here.")]
    public List<FishChance> fishHere = new List<FishChance>();

    private void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;           // the picture is only for designing the level
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    public bool Contains(Vector2 p)
    {
        Vector2 c = transform.position;
        if (Mathf.Abs(p.x - c.x) > boxSize.x * 0.5f || Mathf.Abs(p.y - c.y) > boxSize.y * 0.5f) return false;
        if (waterTilemap != null && !waterTilemap.HasTile(waterTilemap.WorldToCell(p))) return false;
        return true;
    }

    /// <summary>The water area at a world point, or null.</summary>
    public static FishingWater At(Vector2 p)
    {
        foreach (FishingWater w in All) if (w != null && w.Contains(p)) return w;
        return null;
    }

    /// <summary>Picks a fish for this moment. 'luck' makes 4-5 star fish more likely; bait makes its fish more likely.</summary>
    public FishData PickFish(float hour, BaitData bait, float luck, float baitExtra)
    {
        var options = new List<KeyValuePair<FishData, float>>();
        float total = 0f;
        foreach (FishChance c in fishHere)
        {
            if (c == null || c.fish == null || c.weight <= 0f || !c.fish.AvailableAt(hour)) continue;
            float w = c.weight;
            if (c.fish.stars >= 4) w *= 1f + luck;
            if (bait != null && bait.attracts != null)
                foreach (FishData a in bait.attracts) if (a == c.fish) { w *= bait.boost + baitExtra; break; }
            options.Add(new KeyValuePair<FishData, float>(c.fish, w));
            total += w;
        }
        if (total <= 0f) return null;
        float roll = Random.value * total;
        foreach (var o in options) { roll -= o.Value; if (roll <= 0f) return o.Key; }
        return options[options.Count - 1].Key;
    }

    private void OnDrawGizmos() { Draw(0.4f); }
    private void OnDrawGizmosSelected() { Draw(1f); }
    private void Draw(float a)
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, a);
        Gizmos.DrawWireCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0f));
        Gizmos.color = new Color(0.3f, 0.7f, 1f, a * 0.1f);
        Gizmos.DrawCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0f));
    }
}
