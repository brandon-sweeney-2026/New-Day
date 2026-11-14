using UnityEngine;

/// <summary>One extra thing a rock can drop on top of its stone (coal, copper...).</summary>
[System.Serializable]
public class RockDrop
{
    public ItemData item;
    [Min(1)] public int min = 1;
    [Min(1)] public int max = 1;
    [Range(0f, 1f)] [Tooltip("1 = always, 0.3 = three times in ten.")] public float chance = 1f;
}

/// <summary>
/// A rock the player smashes with a pickaxe. It always drops stone and may drop extra things (coal, copper, iron, gems...).
/// Put it on a prefab with a Collider2D. Plain rocks, ore rocks and gem rocks are all this same script with different settings.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MineableRock : MonoBehaviour, IToolTarget
{
    [Tooltip("Shown in toasts and in Meatball's tips.")]
    public string rockName = "Rock";
    [Min(1)] [Tooltip("How many pickaxe swings it takes.")] public int hits = 3;

    [Header("Drops")]
    public ItemData stone;
    [Min(0)] public int stoneMin = 1;
    [Min(0)] public int stoneMax = 3;
    [Tooltip("Extra drops, each rolled separately.")]
    public RockDrop[] bonusDrops;

    public ItemData.ToolAction RequiredTool { get { return ItemData.ToolAction.Pickaxe; } }
    public bool IsIntact { get { return hitsLeft > 0; } }

    private int hitsLeft;
    private Vector3 basePos;
    private float shakeUntil;
    private SpriteRenderer sr;

    private void Awake()
    {
        hitsLeft = hits;
        basePos = transform.position;
        sr = GetComponent<SpriteRenderer>();
    }

    public bool OnToolUsed(ItemData.ToolAction tool, PlayerActions user)
    {
        if (hitsLeft <= 0) return false;
        if (tool != ItemData.ToolAction.Pickaxe)
        {
            GameEvents.ShowToast("This needs a pickaxe.");
            return false;
        }

        hitsLeft--;
        shakeUntil = Time.time + 0.15f;
        // a rock gets darker as it cracks
        if (sr != null) sr.color = Color.Lerp(Color.white, new Color(0.7f, 0.7f, 0.7f), 1f - (float)hitsLeft / hits);

        if (hitsLeft <= 0) Break();
        return true;
    }

    private void Break()
    {
        Vector3 at = transform.position + Vector3.up * 0.3f;
        int amount = Random.Range(stoneMin, Mathf.Max(stoneMin, stoneMax) + 1);
        if (stone != null && amount > 0) ItemPickup.Spawn(stone, amount, at);

        if (bonusDrops != null)
            foreach (RockDrop d in bonusDrops)
            {
                if (d == null || d.item == null || Random.value > d.chance) continue;
                ItemPickup.Spawn(d.item, Random.Range(d.min, Mathf.Max(d.min, d.max) + 1), at);
            }

        GameState.AddCounter("mined_rocks");
        GameState.AddCounter("mined_" + rockName);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (Time.time < shakeUntil) transform.position = basePos + new Vector3(Mathf.Sin(Time.time * 90f) * 0.04f, 0f, 0f);
        else if (transform.position != basePos) transform.position = basePos;
    }
}
