using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fills the city streets with life: people on foot (in groups of 1 to 4), cyclists (1 to 3), cars and scooters.
/// They are strangers: each one is put together at random, appears at one end of a route, travels to the other end and is destroyed.
/// A pedestrian will say one to three lines if you stop them.
///
///  - Routes: where each kind may travel (a list of points). The city builder lays them along the sidewalks, the roads and the river walk.
///  - Looks: the pictures to choose from at random.
///  - How busy it is follows the clock (Busy By Hour): rush hours are crowded, the small hours are nearly empty.
/// Put one on an empty object in the City scene. Everything is tunable here in the Inspector.
/// </summary>
public class CityTraffic : MonoBehaviour
{
    public enum Kind { Pedestrian, Cyclist, Car, Scooter }

    /// <summary>A path a kind of traffic may follow. They enter at the first point and leave at the last (or the other way round).</summary>
    [Serializable]
    public class Route
    {
        public string label = "Route";
        public Kind kind = Kind.Pedestrian;
        public Vector2[] points = new Vector2[0];
        [Tooltip("May also be travelled from the last point to the first. Turn OFF for road lanes (cars keep to their side).")]
        public bool bothWays = true;
        [Min(0f), Tooltip("How often this route is picked compared with the others of the same kind.")]
        public float weight = 1f;
    }

    /// <summary>What one pedestrian can look like.</summary>
    [Serializable]
    public class WalkerLook
    {
        public string label = "Person";
        public Sprite[] down, up, side;
        public Sprite portraitIdle, portraitTalk, portraitHappy, portraitBlink;
    }

    /// <summary>What one vehicle can look like (pictures face right).</summary>
    [Serializable]
    public class WheelLook
    {
        public string label = "Car";
        public Sprite[] frames;
        [Min(0.1f)] public float speedMultiplier = 1f;
    }

    /// <summary>How one kind of traffic behaves.</summary>
    [Serializable]
    public class KindSettings
    {
        [Tooltip("Seconds between new groups at the busiest time of day: least and most.")]
        public Vector2 secondsBetween = new Vector2(4f, 9f);
        [Tooltip("How many travel together: least and most.")]
        public Vector2Int groupSize = new Vector2Int(1, 1);
        [Tooltip("Tiles per second: slowest and fastest.")]
        public Vector2 speed = new Vector2(1.5f, 2f);
        [Min(0), Tooltip("Never more than this many of this kind at once.")]
        public int most = 12;
        [Tooltip("Tiles between the members of a group, along the route.")]
        public float spacing = 0.9f;
    }

    [Header("Where they go")]
    public Route[] routes = new Route[0];

    [Header("How each kind behaves")]
    public KindSettings pedestrians = new KindSettings { secondsBetween = new Vector2(3f, 7f), groupSize = new Vector2Int(1, 4), speed = new Vector2(1.3f, 1.9f), most = 26, spacing = 0.2f };
    public KindSettings cyclists = new KindSettings { secondsBetween = new Vector2(9f, 18f), groupSize = new Vector2Int(1, 3), speed = new Vector2(3.2f, 4.2f), most = 8, spacing = 1.6f };
    public KindSettings cars = new KindSettings { secondsBetween = new Vector2(4f, 9f), groupSize = new Vector2Int(1, 1), speed = new Vector2(5f, 7f), most = 10, spacing = 4f };
    public KindSettings scooters = new KindSettings { secondsBetween = new Vector2(10f, 20f), groupSize = new Vector2Int(1, 2), speed = new Vector2(4.2f, 5.4f), most = 6, spacing = 2f };

    [Header("What they look like (one is picked at random)")]
    public WalkerLook[] walkerLooks = new WalkerLook[0];
    public WheelLook[] bikeLooks = new WheelLook[0];
    public WheelLook[] carLooks = new WheelLook[0];
    public WheelLook[] scooterLooks = new WheelLook[0];
    public Sprite dialoguePanel, portraitFrame;

    [Header("How busy, by the hour (0 = nobody, 1 = crowded)")]
    public AnimationCurve busyByHour = new AnimationCurve(
        new Keyframe(0f, 0.08f), new Keyframe(5f, 0.08f), new Keyframe(8f, 1f), new Keyframe(10f, 0.6f), new Keyframe(12.5f, 0.9f),
        new Keyframe(15f, 0.6f), new Keyframe(17.5f, 1f), new Keyframe(21f, 0.35f), new Keyframe(24f, 0.08f));
    [Min(0), Tooltip("Groups already on the streets when the scene opens, so the city is never empty when you arrive.")]
    public int groupsAtStart = 14;

    [Header("What pedestrians say (each says 1 to 3 of these)")]
    public Vector2Int linesPerPerson = new Vector2Int(1, 3);
    [TextArea] public string[] pedestrianLines =
    {
        "Sorry, can't stop, I'm late!", "Lovely day for it.", "Have you tried the hot dogs? Life-changing.", "Is it me, or does the river smell odd lately?",
        "The Sterling tower gets taller every year. Or I'm getting shorter.", "Excuse me. Coming through.", "You're not from the city, are you? It's the boots.",
        "My cousin lives out in the suburbs. Says the bread there is amazing."
    };
    public string[] strangerNames = { "Passer-by", "Commuter", "Shopper", "Student", "Tourist", "Local" };

    private readonly float[] nextAt = new float[4];
    private readonly List<CityMover> alive = new List<CityMover>();
    private Material lit;

    private void Start()
    {
        lit = Resources.Load<Material>("Lighting/SpriteLit");          // so they go dark at night like everything else
        for (int i = 0; i < groupsAtStart; i++)
        {
            Kind k = i % 5 == 4 ? Kind.Car : (i % 5 == 3 ? Kind.Cyclist : Kind.Pedestrian);
            if (UnityEngine.Random.value <= Busy()) SpawnGroup(k, true);
        }
        for (int k = 0; k < 4; k++) nextAt[k] = Time.time + UnityEngine.Random.Range(0.5f, 3f);
    }

    private float Busy()
    {
        float hour = TimeManager.Instance != null ? TimeManager.Instance.HoursDecimal % 24f : 12f;
        return Mathf.Clamp01(busyByHour.Evaluate(hour));
    }

    private KindSettings SettingsOf(Kind k)
    {
        switch (k)
        {
            case Kind.Cyclist: return cyclists;
            case Kind.Car: return cars;
            case Kind.Scooter: return scooters;
            default: return pedestrians;
        }
    }

    private void Update()
    {
        if (TimeManager.Instance != null && TimeManager.Instance.Paused) return;
        if (HouseInterior.PlayerIsInside) return;                      // nobody is watching the street
        alive.RemoveAll(m => m == null);

        float busy = Busy();
        for (int k = 0; k < 4; k++)
        {
            if (Time.time < nextAt[k]) continue;
            KindSettings s = SettingsOf((Kind)k);
            // quieter hours = longer gaps between groups
            nextAt[k] = Time.time + UnityEngine.Random.Range(s.secondsBetween.x, s.secondsBetween.y) / Mathf.Max(0.05f, busy);
            if (Count((Kind)k) < s.most) SpawnGroup((Kind)k, false);
        }
    }

    private int Count(Kind k)
    {
        int n = 0;
        foreach (CityMover m in alive) if (m != null && m.kind == k) n++;
        return n;
    }

    // ------------------------------------------------------------------ making a group
    private Route PickRoute(Kind k)
    {
        float total = 0f;
        foreach (Route r in routes) if (r != null && r.kind == k && r.points != null && r.points.Length >= 2) total += Mathf.Max(0f, r.weight);
        if (total <= 0f) return null;
        float roll = UnityEngine.Random.value * total;
        foreach (Route r in routes)
        {
            if (r == null || r.kind != k || r.points == null || r.points.Length < 2) continue;
            roll -= Mathf.Max(0f, r.weight);
            if (roll <= 0f) return r;
        }
        return null;
    }

    private void SpawnGroup(Kind k, bool alreadyOnTheWay)
    {
        Route r = PickRoute(k);
        if (r == null) return;
        KindSettings s = SettingsOf(k);

        var points = (Vector2[])r.points.Clone();
        if (r.bothWays && UnityEngine.Random.value < 0.5f) Array.Reverse(points);
        float length = 0f;
        for (int i = 0; i + 1 < points.Length; i++) length += Vector2.Distance(points[i], points[i + 1]);

        int size = UnityEngine.Random.Range(s.groupSize.x, s.groupSize.y + 1);
        float speed = UnityEngine.Random.Range(s.speed.x, s.speed.y);

        // How far behind the leader each member is. People walk two abreast; wheels follow each other in a line.
        var behind = new float[size];
        float groupLength = 0f;
        for (int i = 0; i < size; i++)
        {
            behind[i] = k == Kind.Pedestrian ? (i / 2) * 0.8f + UnityEngine.Random.Range(0f, s.spacing) : i * s.spacing;
            groupLength = Mathf.Max(groupLength, behind[i]);
        }

        // The route is made a little longer at its start (off the edge of the map), so the whole group has room to line up out of sight.
        points[0] -= (points[1] - points[0]).normalized * groupLength;
        float start = groupLength + (alreadyOnTheWay ? UnityEngine.Random.Range(0.1f, 0.8f) * length : 0f);

        for (int i = 0; i < size; i++)
        {
            float sideStep = k == Kind.Pedestrian ? (size == 1 ? 0f : (i % 2 == 0 ? -0.32f : 0.32f)) : 0f;
            CityMover m = Spawn(k, points, speed, sideStep);
            if (m == null) return;
            m.Begin(start - behind[i]);
            alive.Add(m);
        }
    }

    private CityMover Spawn(Kind k, Vector2[] points, float speed, float sideStep)
    {
        var go = new GameObject(k.ToString());
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        if (lit != null) sr.sharedMaterial = lit;
        var m = go.AddComponent<CityMover>();
        m.kind = k; m.route = points; m.speed = speed; m.sideStep = sideStep;

        if (k == Kind.Pedestrian)
        {
            if (walkerLooks == null || walkerLooks.Length == 0) { Destroy(go); return null; }
            WalkerLook look = walkerLooks[UnityEngine.Random.Range(0, walkerLooks.Length)];
            m.down = look.down; m.up = look.up; m.side = look.side;
            m.framesPerSecond = 7f;
            m.speaker = new DialogueSpeaker
            {
                speakerName = strangerNames != null && strangerNames.Length > 0 ? strangerNames[UnityEngine.Random.Range(0, strangerNames.Length)] : "Passer-by",
                idle = look.portraitIdle, talk = look.portraitTalk, happy = look.portraitHappy, blink = look.portraitBlink,
                panel = dialoguePanel, frame = portraitFrame, nameColor = new Color(0.35f, 0.4f, 0.5f)
            };
            m.pages = PickLines();
            var c = go.AddComponent<CircleCollider2D>();           // so the action button can find them (a trigger: you can walk through a crowd)
            c.radius = 0.4f; c.offset = new Vector2(0f, 0.5f); c.isTrigger = true;
        }
        else
        {
            WheelLook[] looks = k == Kind.Car ? carLooks : (k == Kind.Cyclist ? bikeLooks : scooterLooks);
            if (looks == null || looks.Length == 0) { Destroy(go); return null; }
            WheelLook look = looks[UnityEngine.Random.Range(0, looks.Length)];
            m.side = look.frames;
            m.speed = speed * look.speedMultiplier;
            m.framesPerSecond = k == Kind.Cyclist ? 6f : 1f;
            m.stopDistance = k == Kind.Car ? 2.4f : 1.5f;
        }
        return m;
    }

    /// <summary>One to three different lines for one pedestrian.</summary>
    private string[] PickLines()
    {
        if (pedestrianLines == null || pedestrianLines.Length == 0) return new string[0];
        int want = Mathf.Clamp(UnityEngine.Random.Range(linesPerPerson.x, linesPerPerson.y + 1), 1, pedestrianLines.Length);
        var pool = new List<string>(pedestrianLines);
        var picked = new List<string>();
        for (int i = 0; i < want; i++)
        {
            int at = UnityEngine.Random.Range(0, pool.Count);
            picked.Add(pool[at]);
            pool.RemoveAt(at);
        }
        return picked.ToArray();
    }

    private void OnDrawGizmosSelected()
    {
        if (routes == null) return;
        foreach (Route r in routes)
        {
            if (r == null || r.points == null) continue;
            Gizmos.color = r.kind == Kind.Pedestrian ? Color.green : (r.kind == Kind.Cyclist ? Color.cyan : (r.kind == Kind.Car ? Color.red : Color.yellow));
            for (int i = 0; i + 1 < r.points.Length; i++) Gizmos.DrawLine(r.points[i], r.points[i + 1]);
        }
    }
}
