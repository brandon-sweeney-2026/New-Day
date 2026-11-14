using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters mineable rocks around the area and puts some back every day.
///
/// "Rock Types" is a list of rock prefabs with a "weight": the bigger the weight the more often it appears. So plain rocks get big weights and
/// ore rocks (coal, copper) small ones - that is your chance of a higher tier ore. Example: weights 20, 18, 17, 15 for plain rocks and 9, 6 for coal and
/// copper means 15 out of 85 rocks (about 18%) are ore.
/// </summary>
public class RockSpawner : SpawnAreaBase
{
    [System.Serializable]
    public class RockType
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Header("Rocks")]
    public RockType[] rockTypes;
    [Tooltip("How many rocks the area holds when full.")] [Min(1)] public int targetCount = 28;
    [Tooltip("Rocks that appear at the very start.")] public bool fillOnStart = true;
    [Tooltip("At most this many new rocks appear each morning (so the area refills over a few days).")] [Min(0)] public int respawnPerDay = 8;
    [Tooltip("Rocks keep at least this far from each other.")] public float minSpacing = 1.6f;

    protected override Color ZoneColor { get { return new Color(0.85f, 0.6f, 0.35f, 1f); } }

    private readonly List<GameObject> alive = new List<GameObject>();

    private void Start()
    {
        if (fillOnStart) Fill(targetCount);
        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay += OnNewDay;
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay -= OnNewDay;
    }

    private void OnNewDay() { Fill(respawnPerDay); }

    /// <summary>Adds up to 'howMany' rocks (never going over the target count).</summary>
    public void Fill(int howMany)
    {
        alive.RemoveAll(g => g == null);
        int room = Mathf.Min(howMany, targetCount - alive.Count);
        for (int i = 0; i < room; i++)
        {
            GameObject prefab = PickPrefab();
            if (prefab == null) return;

            Vector2 spot = Vector2.zero; bool ok = false;
            for (int tries = 0; tries < 12 && !ok; tries++)
            {
                if (!TryFindSpot(out spot)) break;
                ok = FarFromOthers(spot);
            }
            if (!ok) continue;

            GameObject rock = Instantiate(prefab, spot, Quaternion.identity, transform);
            rock.name = prefab.name;
            alive.Add(rock);
        }
    }

    private bool FarFromOthers(Vector2 p)
    {
        foreach (GameObject g in alive)
            if (g != null && Vector2.Distance(g.transform.position, p) < minSpacing) return false;
        return true;
    }

    private GameObject PickPrefab()
    {
        if (rockTypes == null) return null;
        float total = 0f;
        foreach (RockType r in rockTypes) if (r != null && r.prefab != null) total += r.weight;
        if (total <= 0f) return null;
        float roll = Random.value * total;
        foreach (RockType r in rockTypes)
        {
            if (r == null || r.prefab == null) continue;
            roll -= r.weight;
            if (roll <= 0f) return r.prefab;
        }
        return rockTypes[rockTypes.Length - 1].prefab;
    }
}
