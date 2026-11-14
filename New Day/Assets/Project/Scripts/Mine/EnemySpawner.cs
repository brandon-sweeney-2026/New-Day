using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps an area populated with enemies. Enemies appear out of sight (not too near the player), up to "Max Alive", one every few seconds.
/// Each enemy type has its own weight (how common) and its own cap (e.g. at most 2 bats).
/// </summary>
public class EnemySpawner : SpawnAreaBase
{
    [System.Serializable]
    public class EnemyType
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Min(1)] [Tooltip("At most this many of this kind alive at once.")] public int maxAlive = 3;
    }

    [Header("Enemies")]
    public EnemyType[] enemyTypes;
    [Min(1)] public int maxAlive = 6;
    [Tooltip("Enemies that are already there when the area loads.")] public int startCount = 4;
    [Tooltip("Seconds between new enemies (random between the two).")] public Vector2 spawnEvery = new Vector2(10f, 20f);

    [Header("Where relative to the player")]
    [Tooltip("Never closer than this (so they don't pop up in your face).")] public float minDistanceFromPlayer = 9f;
    [Tooltip("Never farther than this (so they come to you).")] public float maxDistanceFromPlayer = 30f;
    [Tooltip("No new enemies while the player is inside a house room.")] public bool pauseWhenIndoors = true;

    protected override Color ZoneColor { get { return new Color(1f, 0.35f, 0.35f, 1f); } }

    private readonly List<GameObject> alive = new List<GameObject>();
    private readonly List<EnemyType> kind = new List<EnemyType>();
    private float timer;

    private void Start()
    {
        for (int i = 0; i < startCount; i++) SpawnOne(true);
        timer = Random.Range(spawnEvery.x, spawnEvery.y);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(spawnEvery.x, spawnEvery.y);
        SpawnOne(false);
    }

    private int CountOf(EnemyType t)
    {
        int n = 0;
        for (int i = 0; i < alive.Count; i++) if (alive[i] != null && kind[i] == t) n++;
        return n;
    }

    private void Clean()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
            if (alive[i] == null) { alive.RemoveAt(i); kind.RemoveAt(i); }
    }

    private void SpawnOne(bool atStart)
    {
        Clean();
        if (alive.Count >= maxAlive || enemyTypes == null) return;

        float total = 0f;
        foreach (EnemyType t in enemyTypes)
            if (t != null && t.prefab != null && CountOf(t) < t.maxAlive) total += t.weight;
        if (total <= 0f) return;

        float roll = Random.value * total;
        EnemyType pick = null;
        foreach (EnemyType t in enemyTypes)
        {
            if (t == null || t.prefab == null || CountOf(t) >= t.maxAlive) continue;
            pick = t;
            roll -= t.weight;
            if (roll <= 0f) break;
        }
        if (pick == null) return;

        Vector2 spot;
        // at the very start the player may be standing right in the middle of the area, so don't use the distance rule yet
        bool found = atStart ? TryFindSpot(out spot, 6f, 0f) : TryFindSpot(out spot, minDistanceFromPlayer, maxDistanceFromPlayer);
        if (!found) return;

        GameObject e = Instantiate(pick.prefab, spot - spawnOffset + Vector2.up * 0.05f, Quaternion.identity, transform);
        e.name = pick.prefab.name;
        alive.Add(e); kind.Add(pick);
    }
}
