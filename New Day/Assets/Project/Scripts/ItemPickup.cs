using UnityEngine;

/// <summary>
/// An item lying on the ground. It floats toward the player when they get close and goes into the backpack.
/// Create one from code with ItemPickup.Spawn(item, count, position). Enemies, trees and harvests use this to drop things.
/// If the backpack is full it stays on the ground.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public ItemData item;
    [Min(1)] public int count = 1;
    [Range(0, 5)] public int pollution;

    [Header("Behaviour")]
    [SerializeField] private float pickupRadius = 1.4f;
    [SerializeField] private float magnetSpeed = 7f;
    [Tooltip("Seconds after dropping before it can be picked up.")]
    [SerializeField] private float pickupDelay = 0.5f;
    [SerializeField] private float bobHeight = 0.05f;

    private float readyAt;
    private Vector3 basePosition;
    private float phase;
    private SpriteRenderer sr;

    /// <summary>Drops an item into the world. 'scatter' throws it a little sideways so drops don't stack exactly.</summary>
    public static ItemPickup Spawn(ItemData item, int count, Vector3 position, int pollution = 0, float scatter = 0.4f)
    {
        if (item == null || count <= 0) return null;

        var go = new GameObject("Drop: " + item.itemName);
        go.transform.position = position + (Vector3)(Random.insideUnitCircle * scatter);

        var pickup = go.AddComponent<ItemPickup>();
        pickup.item = item;
        pickup.count = count;
        pickup.pollution = pollution;
        pickup.Setup();
        return pickup;
    }

    private void Awake()
    {
        Setup();
    }

    private void Setup()
    {
        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        }

        if (item != null)
        {
            sr.sprite = item.icon != null ? item.icon : RuntimeSprites.Circle;
            if (item.icon == null) { sr.color = new Color(1f, 0.9f, 0.4f); transform.localScale = Vector3.one * 0.4f; }
        }
        sr.sortingOrder = 20;

        readyAt = Time.time + pickupDelay;
        basePosition = transform.position;
        phase = Random.value * 6.28f;
    }

    private void Update()
    {
        if (item == null) { Destroy(gameObject); return; }

        Inventory inv = Inventory.Instance;
        if (inv == null) return;

        Vector3 playerPos = inv.transform.position;
        float dist = Vector2.Distance(playerPos, basePosition);

        if (Time.time >= readyAt && dist <= pickupRadius && inv.CanFit(item, count, pollution) > 0)
        {
            // Float toward the player, then collect.
            basePosition = Vector3.MoveTowards(basePosition, playerPos, magnetSpeed * Time.deltaTime);
            if (dist < 0.3f) Collect(inv);
        }

        transform.position = basePosition + Vector3.up * Mathf.Sin(Time.time * 3f + phase) * bobHeight;
    }

    private void Collect(Inventory inv)
    {
        int leftover = inv.Add(item, count, pollution);
        int taken = count - leftover;
        if (taken > 0) GameEvents.ShowToast("+" + taken + " " + item.itemName);

        if (leftover <= 0) Destroy(gameObject);
        else count = leftover;
    }
}
