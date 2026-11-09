using UnityEngine;

/// <summary>
/// Meatball's dog house. When he is "at home" he hangs out at the Sit Point next to it,
/// and you can still talk to him there. Add this to the dog house object (Farm > NPCs > Set Up Meatball does it for you).
/// </summary>
public class DogHouse : MonoBehaviour
{
    public static DogHouse Instance { get; private set; }

    [Tooltip("Where Meatball sits when he is at home. Move this child object around. Made automatically if empty.")]
    [SerializeField] private Transform sitPoint;

    public Vector2 SitPoint
    {
        get
        {
            if (sitPoint != null) return sitPoint.position;
            return (Vector2)transform.position + new Vector2(1.4f, -0.1f);
        }
    }

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.2f);
        Gizmos.DrawWireSphere(SitPoint, 0.25f);
    }
}
