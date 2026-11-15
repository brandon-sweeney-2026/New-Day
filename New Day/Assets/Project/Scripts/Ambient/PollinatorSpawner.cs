using System.Collections.Generic;
using UnityEngine;

/// <summary>One kind of insect the spawner can make.</summary>
[System.Serializable]
public class InsectKind
{
    public string name = "Butterfly";
    [Tooltip("Wing pictures (2 is enough: wings open, wings closed).")]
    public Sprite[] frames;
    [Tooltip("How common it is compared with the other kinds (2 = twice as common).")]
    [Min(0f)] public float weight = 1f;
    public float speed = 1.6f;
    [Tooltip("Side to side drift while flying. Butterflies about 0.35, bees about 0.12.")]
    public float wobble = 0.35f;
    public float flapsPerSecond = 9f;
    [Tooltip("Seconds it sits on a flower (random between the two).")]
    public Vector2 restSeconds = new Vector2(2.5f, 6f);
}

/// <summary>
/// Every so often sends a butterfly or a bee to a flower near the player, so the world feels alive.
/// Put ONE of these in a scene that has flowers with a FlowerSpot on them (the suburb builder does it for you).
/// Insects only come out in the daytime. Every number is in the Inspector.
/// </summary>
public class PollinatorSpawner : MonoBehaviour
{
    [Header("Insects")]
    public InsectKind[] kinds;

    [Header("How many, how often")]
    [Min(0), Tooltip("Most insects out at the same time.")]
    public int maxAlive = 4;
    [Tooltip("Seconds between new insects (random between the two).")]
    public Vector2 spawnEvery = new Vector2(5f, 12f);

    [Header("Where")]
    [Tooltip("Only flowers this close to the player get visitors (so you actually see them).")]
    public float nearPlayerRadius = 12f;
    [Tooltip("How far away from its first flower an insect appears.")]
    public float appearDistance = 11f;
    [Tooltip("Drawn above everything else in the scene.")]
    public int sortingOrder = 40;

    [Header("When")]
    [Tooltip("Insects only appear between these hours (24-hour clock). Needs a TimeManager; without one they always appear.")]
    public int fromHour = 7;
    public int untilHour = 19;

    private readonly List<Pollinator> alive = new List<Pollinator>();
    private float timer;
    private Transform player;

    private void Start()
    {
        timer = Random.Range(1f, spawnEvery.x);     // the first one comes quickly
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(spawnEvery.x, spawnEvery.y);

        alive.RemoveAll(p => p == null);
        if (alive.Count >= maxAlive || kinds == null || kinds.Length == 0) return;
        if (!IsDaytime()) return;

        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            player = pc.transform;
        }

        FlowerSpot flower = FlowerSpot.RandomNear(player.position, nearPlayerRadius);
        if (flower == null) return;                 // no free flower nearby right now

        InsectKind kind = PickKind();
        if (kind == null || kind.frames == null || kind.frames.Length == 0) return;

        // Appear off to one side and a bit above, then fly in.
        float a = Random.Range(0f, Mathf.PI * 2f);
        Vector2 from = flower.LandingPoint + new Vector2(Mathf.Cos(a), Mathf.Abs(Mathf.Sin(a)) * 0.7f + 0.2f).normalized * appearDistance;

        var go = new GameObject(kind.name);
        go.transform.SetParent(transform, false);
        go.transform.position = from;
        go.AddComponent<LightingExempt>();          // too small to need a shadow

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = kind.frames[0];
        sr.sortingOrder = sortingOrder;

        var insect = go.AddComponent<Pollinator>();
        insect.frames = kind.frames;
        insect.speed = kind.speed * Random.Range(0.85f, 1.15f);
        insect.wobble = kind.wobble;
        insect.flapsPerSecond = kind.flapsPerSecond;
        insect.restSeconds = kind.restSeconds;
        insect.Begin(flower);
        alive.Add(insect);
    }

    private bool IsDaytime()
    {
        if (TimeManager.Instance == null) return true;
        int h = TimeManager.Instance.HourOfDay;
        return h >= fromHour && h < untilHour;
    }

    private InsectKind PickKind()
    {
        float total = 0f;
        foreach (InsectKind k in kinds) if (k != null) total += k.weight;
        if (total <= 0f) return null;
        float roll = Random.value * total;
        foreach (InsectKind k in kinds)
        {
            if (k == null) continue;
            roll -= k.weight;
            if (roll <= 0f) return k;
        }
        return kinds[kinds.Length - 1];
    }
}
