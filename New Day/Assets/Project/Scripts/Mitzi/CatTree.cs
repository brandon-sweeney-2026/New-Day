using UnityEngine;

/// <summary>
/// Mitzi's cat tree. She jumps up to the Perch Points to nap and hops down to the Ground Point.
/// Add this to the cat tree object (Farm > NPCs > Set Up Mitzi does it for you).
/// </summary>
public class CatTree : MonoBehaviour
{
    public static CatTree Instance { get; private set; }

    [Tooltip("Where she stands at the foot of the tree before jumping up. Made automatically if empty.")]
    [SerializeField] private Transform groundPoint;
    [Tooltip("The platforms she can sit on (child objects). Move them to match your art.")]
    [SerializeField] private Transform[] perchPoints;

    public SpriteRenderer Renderer { get; private set; }

    public Vector2 GroundPoint
    {
        get { return groundPoint != null ? (Vector2)groundPoint.position : (Vector2)transform.position + new Vector2(0.2f, -0.5f); }
    }

    public bool HasPerch { get { return perchPoints != null && perchPoints.Length > 0; } }

    public Vector2 RandomPerch()
    {
        if (!HasPerch) return (Vector2)transform.position + new Vector2(0f, 2.1f);
        // the top platform (first in the list) is her favourite
        int i = Random.value < 0.65f ? 0 : Random.Range(0, perchPoints.Length);
        return perchPoints[i].position;
    }

    private void Awake()
    {
        Instance = this;
        Renderer = GetComponent<SpriteRenderer>();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawWireSphere(GroundPoint, 0.2f);
        if (perchPoints != null)
            foreach (Transform t in perchPoints) if (t != null) Gizmos.DrawWireCube(t.position, new Vector3(0.6f, 0.2f, 0f));
    }
}
