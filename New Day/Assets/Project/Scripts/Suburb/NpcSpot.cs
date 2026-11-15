using UnityEngine;

/// <summary>
/// A named place NPCs can stand or walk to ("behind the checkout", "the shop door", "kitchen"). Drop an empty object with this on it.
/// The NPC pathing pack uses these as destinations. Shows as a small marker in the Scene view.
/// </summary>
public class NpcSpot : MonoBehaviour
{
    public string spotId = "Spot";
    [Tooltip("Direction an NPC faces when it arrives.")] public Vector2 facing = Vector2.down;

    private static readonly System.Collections.Generic.List<NpcSpot> all = new System.Collections.Generic.List<NpcSpot>();
    public static System.Collections.Generic.IReadOnlyList<NpcSpot> All { get { return all; } }
    private void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    private void OnEnable() { all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    public static NpcSpot Find(string id)
    {
        foreach (NpcSpot s in all) if (s != null && s.spotId == id) return s;
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(facing.normalized * 0.5f));
    }
}
