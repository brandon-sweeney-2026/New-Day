using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A glowing stump. Press the action button to see what it wants and give it. When everything is given the linked door opens (for good).
/// Offerings are remembered, so you can bring a few at a time. Put it in the room whose door it unlocks and drag that door into "Door".
/// Needs a trigger Collider2D (added automatically).
///
/// GLOW: a yellow VEIN that runs up the stump, down to the ground and spreads out across the floor like roots. It pulses slowly and stays bright
/// in the dark, and a real yellow light under it lights the ground nearby. Everything is in the Inspector under "Glow".
/// Once the door is open the veins settle to a dim "resting" glow.
/// </summary>
public class ForestStump : MonoBehaviour, IInteractable
{
    public ForestDoor door;
    [Tooltip("What the stump says. Leave empty for the default.")]
    [TextArea] public string whisper = "";
    public Sprite openSprite;                 // picture after the door is open (optional)

    [Header("Glow")]
    public bool glow = true;
    public Color glowColor = new Color(1f, 0.85f, 0.25f, 1f);
    [Tooltip("Seconds per pulse (bigger = slower).")]
    [Min(0.2f)] public float pulseSeconds = 2.2f;
    [Tooltip("How far the roots spread over the ground (1 = normal, 2 = twice as wide).")]
    public float rootSpread = 1f;
    [Tooltip("How tall the vein on the stump is, in world units (up from the ground).")]
    public float veinHeight = 0.85f;
    [Tooltip("Changes the pattern of the roots. Each stump also varies by itself.")]
    public int rootPattern = 0;
    [Range(0f, 1f), Tooltip("Strength of the vein glow.")] public float glowStrength = 0.9f;
    [Tooltip("Also add a real light (works with the night lighting).")]
    public bool realLight = true;
    public float lightIntensity = 0.9f;
    public float lightRadius = 3.8f;
    [Tooltip("Height of the real light above the stump's feet.")]
    public float glowHeight = 0.45f;
    [Range(0f, 1f), Tooltip("How bright it stays after the door is open (0 = goes out).")]
    public float restingStrength = 0.2f;

    public string InteractPrompt { get { return door != null && door.IsOpen ? "The stump rests" : "Offer to the stump"; } }

    private SpriteRenderer veinSr, rootSr, ownSr;
    private ForestRoom roomOwner;
    private Light2D lamp;
    private float seed;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.6f; c.isTrigger = true;
        }
        seed = Random.value * 6.28f;
        if (glow) BuildGlow();
    }

    private void BuildGlow()
    {
        var own = GetComponent<SpriteRenderer>();
        ownSr = own;
        roomOwner = GetComponentInParent<ForestRoom>();
        int order = own != null ? own.sortingOrder : 8;
        int pattern = rootPattern * 7919 + Mathf.RoundToInt(transform.position.x * 31f + transform.position.y * 17f);

        // roots flat on the ground, around the stump's feet (drawn under the stump)
        var rg = new GameObject("StumpRoots");
        rg.transform.SetParent(transform, false);
        rg.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        rg.transform.localScale = Vector3.one * Mathf.Max(0.1f, rootSpread);
        rootSr = rg.AddComponent<SpriteRenderer>();
        rootSr.sprite = MakeVeinSprite(pattern, true);
        if (own != null) rootSr.sortingLayerID = own.sortingLayerID;
        rootSr.sortingOrder = RootOrder(order);
        rootSr.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        rg.AddComponent<LightingExempt>().skipDarkness = true;       // stays bright in the dark
        rg.GetComponent<LightingExempt>().skipShadow = true;

        // the vein up the stump itself (drawn over it), joined to the roots at the ground
        var vg = new GameObject("StumpVein");
        vg.transform.SetParent(transform, false);
        vg.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        float vh = veinHeight;
        if (own != null && own.sprite != null) vh = Mathf.Min(vh, own.sprite.bounds.size.y * 0.6f);   // stays inside the stump's picture
        vg.transform.localScale = Vector3.one * (vh / 1.5f);
        veinSr = vg.AddComponent<SpriteRenderer>();
        veinSr.sprite = MakeVeinSprite(pattern + 1, false);
        if (own != null) veinSr.sortingLayerID = own.sortingLayerID;
        veinSr.sortingOrder = order + 1;
        veinSr.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        vg.AddComponent<LightingExempt>().skipDarkness = true;
        vg.GetComponent<LightingExempt>().skipShadow = true;

        if (realLight)
        {
            var l = new GameObject("StumpLight");
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0f, glowHeight, 0f);
            lamp = l.AddComponent<Light2D>();
            lamp.lightType = Light2D.LightType.Point;
            lamp.color = glowColor;
            lamp.pointLightOuterRadius = lightRadius;
            lamp.pointLightInnerRadius = 0.3f;
            lamp.intensity = 0f;
            lamp.shadowsEnabled = false;
        }
    }

    // ---------------------------------------------------------------- drawing the vein
    private static readonly System.Collections.Generic.Dictionary<int, Sprite> veinCache = new System.Collections.Generic.Dictionary<int, Sprite>();

    /// <summary>
    /// Draws a glowing vein picture in code (1 pixel = 1/16 unit).
    /// roots = true: a flat web of roots spreading over the ground from the middle (80 x 48 pixels, squashed so it looks like it lies on the floor).
    /// roots = false: a thin crooked vein climbing up from the bottom centre (20 x 24 pixels = 1.25 x 1.5 units) with a couple of little forks.
    /// </summary>
    private static Sprite MakeVeinSprite(int seed, bool roots)
    {
        int key = seed * 2 + (roots ? 1 : 0);
        Sprite cached;
        if (veinCache.TryGetValue(key, out cached) && cached != null) return cached;

        int w = roots ? 80 : 20, hgt = roots ? 48 : 24;
        var px = new float[w * hgt];
        var rnd = new System.Random(seed);
        Vector2 origin = roots ? new Vector2(w * 0.5f, hgt * 0.5f) : new Vector2(w * 0.5f, 0f);

        if (roots)
        {
            int n = 6 + rnd.Next(3);
            float start = (float)rnd.NextDouble() * 6.28f;
            for (int i = 0; i < n; i++)
            {
                float ang = start + i * 6.28318f / n + ((float)rnd.NextDouble() - 0.5f) * 0.6f;
                Branch(px, w, hgt, rnd, origin, ang, 22f + (float)rnd.NextDouble() * 14f, 0, 2, true);
            }
        }
        else
        {
            Branch(px, w, hgt, rnd, origin, 1.5708f, 22f, 0, 0, false);       // up
        }

        var tex = new Texture2D(w, hgt, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var cols = new Color32[w * hgt];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(px[i] * 255f), 0, 255));
        tex.SetPixels32(cols);
        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, hgt), roots ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f), 16f);
        veinCache[key] = sp;
        return sp;
    }

    /// <summary>Walks a wobbly line out from a point, leaving a bright 1-pixel core with a softer halo, and forks into smaller branches.</summary>
    private static void Branch(float[] px, int w, int h, System.Random rnd, Vector2 from, float angle, float length, int depth, int maxDepth, bool flat)
    {
        Vector2 p = from;
        float a = angle;
        int steps = Mathf.RoundToInt(length);
        for (int i = 0; i < steps; i++)
        {
            a += ((float)rnd.NextDouble() - 0.5f) * 0.55f;
            if (!flat) a += (1.5708f - a) * 0.5f;                                    // the vein on the stump keeps climbing straight up, in the middle
            float sx = Mathf.Cos(a), sy = Mathf.Sin(a) * (flat ? 0.6f : 1f);       // roots lie flat, so squash up/down
            p += new Vector2(sx, sy);
            float fade = 1f - (float)i / steps * 0.55f;
            float strength = (depth == 0 ? 1f : 0.85f) * fade;
            Plot(px, w, h, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), strength);
            // soft halo beside the core (not on the first few pixels so the trunk stays crisp)
            Plot(px, w, h, Mathf.RoundToInt(p.x) + 1, Mathf.RoundToInt(p.y), strength * 0.28f);
            Plot(px, w, h, Mathf.RoundToInt(p.x) - 1, Mathf.RoundToInt(p.y), strength * 0.28f);
            Plot(px, w, h, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) + 1, strength * 0.28f);
            Plot(px, w, h, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) - 1, strength * 0.28f);
            if (depth < maxDepth && i > 4 && i < steps - 5 && rnd.NextDouble() < (flat ? 0.1 : 0.07))
            {
                float side = rnd.Next(2) == 0 ? 0.6f : -0.6f;
                Branch(px, w, h, rnd, p, a + side, (steps - i) * (flat ? 0.6f : 0.4f), depth + 1, maxDepth, flat);
            }
        }
    }

    private static void Plot(float[] px, int w, int h, int x, int y, float v)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = y * w + x;
        if (v > px[i]) px[i] = v;
    }

    private void Start() { Refresh(); }

    /// <summary>Roots lie on the ground: just above the floor and always well below the stump and anything else standing on it.</summary>
    private int RootOrder(int stumpOrder)
    {
        int floor = roomOwner != null ? roomOwner.floorSortingOrder + 2 : stumpOrder - 4;
        return Mathf.Min(floor, stumpOrder - 1);
    }

    private void LateUpdate()
    {
        if (!glow || rootSr == null || ownSr == null) return;
        rootSr.sortingOrder = RootOrder(ownSr.sortingOrder);        // follow the stump if its order ever changes
        veinSr.sortingOrder = ownSr.sortingOrder + 1;
    }

    private void Update()
    {
        if (!glow || rootSr == null) return;
        float rest = door != null && door.IsOpen ? Mathf.Clamp01(restingStrength) : 1f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (6.2832f / pulseSeconds) + seed);      // 0..1
        float k = Mathf.Lerp(0.55f, 1f, pulse) * rest;

        Color c = glowColor; c.a = glowStrength * k;
        rootSr.color = c;
        veinSr.color = c;
        if (lamp != null) lamp.intensity = lightIntensity * k;
    }

    public void Refresh()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && openSprite != null && door != null && door.IsOpen) sr.sprite = openSprite;
    }

    public void Interact(PlayerActions player)
    {
        if (door == null) { GameEvents.ShowToast("This stump isn't linked to a door yet."); return; }
        if (door.IsOpen) { GameEvents.ShowToast("The vines have withered. The way is open."); return; }
        ForestStumpUI.Open(this, door, player, whisper);
    }
}
