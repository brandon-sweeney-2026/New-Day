using UnityEngine;

/// <summary>
/// Trees, rocks, stumps... anything the player chops or smashes with a tool.
/// Add to a prefab that has a Collider2D, pick the tool it needs and what it drops.
/// (Adding this to the Kiln Harvest trees: select the tree prefab, Add Component > Breakable.)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Breakable : MonoBehaviour, IToolTarget
{
    [SerializeField] private ItemData.ToolAction requiredTool = ItemData.ToolAction.Axe;
    [Tooltip("How many hits it takes.")]
    [Min(1)] [SerializeField] private int hits = 3;
    [Header("Drops")]
    [SerializeField] private ItemData drop;
    [Min(0)] [SerializeField] private int dropMin = 2;
    [Min(0)] [SerializeField] private int dropMax = 4;
    [Tooltip("Destroy the object when broken. Turn off to leave a stump (swap the sprite yourself).")]
    [SerializeField] private bool destroyWhenBroken = true;

    private int hitsLeft;
    private Vector3 basePos;
    private float shakeUntil;

    private void Awake()
    {
        hitsLeft = hits;
        basePos = transform.position;
    }

    public bool OnToolUsed(ItemData.ToolAction tool, PlayerActions user)
    {
        if (hitsLeft <= 0) return false;

        if (tool != requiredTool)
        {
            GameEvents.ShowToast("This needs " + Nice(requiredTool) + ".");
            return false;
        }

        hitsLeft--;
        shakeUntil = Time.time + 0.15f;

        if (hitsLeft <= 0)
        {
            int amount = Random.Range(dropMin, Mathf.Max(dropMin, dropMax) + 1);
            if (drop != null && amount > 0) ItemPickup.Spawn(drop, amount, transform.position + Vector3.up * 0.3f);
            GameState.AddCounter("broke_" + requiredTool);
            if (destroyWhenBroken) Destroy(gameObject);
        }
        return true;
    }

    private void Update()
    {
        // A tiny shake when hit.
        if (Time.time < shakeUntil)
            transform.position = basePos + new Vector3(Mathf.Sin(Time.time * 80f) * 0.03f, 0f, 0f);
        else if (transform.position != basePos)
            transform.position = basePos;
    }

    private static string Nice(ItemData.ToolAction a)
    {
        return a == ItemData.ToolAction.Pickaxe ? "a pickaxe" : "an " + a.ToString().ToLowerInvariant();
    }
}
