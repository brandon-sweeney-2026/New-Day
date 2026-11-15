using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a flower (or a flower pot) that butterflies and bees may visit. Put it on the flower object.
/// The PollinatorSpawner looks these up; you never need to wire anything by hand.
/// </summary>
public class FlowerSpot : MonoBehaviour
{
    [Tooltip("Where an insect sits, measured from this object's position (its base). Up = the flower head.")]
    public Vector2 landingOffset = new Vector2(0f, 0.6f);

    /// <summary>True while an insect is on its way here or sitting here (so two don't pick the same flower).</summary>
    public bool Taken { get; set; }

    /// <summary>The spot in the world where the insect lands.</summary>
    public Vector2 LandingPoint { get { return (Vector2)transform.position + landingOffset; } }

    // Every flower that is switched on right now.
    private static readonly List<FlowerSpot> all = new List<FlowerSpot>();
    public static IReadOnlyList<FlowerSpot> All { get { return all; } }

    private void OnEnable() { all.Add(this); }
    private void OnDisable() { all.Remove(this); Taken = false; }

    /// <summary>A free flower within 'radius' of a point, picked at random. Null if there is none.</summary>
    public static FlowerSpot RandomNear(Vector2 point, float radius, FlowerSpot except = null)
    {
        FlowerSpot pick = null;
        int seen = 0;
        float r2 = radius * radius;
        for (int i = 0; i < all.Count; i++)
        {
            FlowerSpot f = all[i];
            if (f == null || f == except || f.Taken) continue;
            if (((Vector2)f.transform.position - point).sqrMagnitude > r2) continue;
            seen++;
            if (Random.Range(0, seen) == 0) pick = f;     // each candidate ends up equally likely
        }
        return pick;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.9f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + (Vector3)landingOffset, 0.08f);
    }
}
