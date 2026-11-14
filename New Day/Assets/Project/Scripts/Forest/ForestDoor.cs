using UnityEngine;

/// <summary>
/// A thorny gate that seals the way to the next room. It opens when the stump in the room is given what it asks for, and then stays open for good.
/// Put it in a door gap of a ForestRoom (the maze generator does this for you). Needs a Collider2D (added automatically).
///
/// "Id" must be different for every door - it is how the game remembers the door is open.
/// "Requirements" is what the stump asks for. Leave it empty and the stump opens the door for free.
///
/// ANIMATION
///   Unlock(): the purple vines shake and turn GREEN, then the two halves of the gate slide apart into the walls and fade, in a burst of green sparks.
///   Lock():   the reverse - the halves slide back together and the vines turn purple again (used by the test menu; you can call it from your own scripts).
///   Speeds are in the Inspector (Green Seconds, Slide Seconds). The green pictures come from the Forest Style (Farm > Forest > Add Green Gates).
///   If the style has no green pictures, the purple ones are tinted green instead.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ForestDoor : MonoBehaviour, IInteractable
{
    public string id = "door_1";
    public ForestRequirement[] requirements;
    [Tooltip("Facing: tick if this gate sits in a left/right wall (it is tall instead of wide).")]
    public bool upDown = false;
    public ForestStyle style;

    [Header("Animation")]
    [Tooltip("How long the purple -> green colour change takes.")]
    [Min(0.05f)] public float greenSeconds = 0.7f;
    [Tooltip("How long the two halves take to slide apart (or together).")]
    [Min(0.05f)] public float slideSeconds = 0.9f;
    [Tooltip("Only used when the style has no green pictures: the purple gate is tinted this colour.")]
    public Color greenTint = new Color(0.45f, 1f, 0.55f, 1f);
    public Color sparkColor = new Color(0.6f, 1f, 0.65f, 0.95f);

    [TextArea] public string lockedHint = "Thorny vines block the way. Look for a stump nearby.";

    public bool IsOpen { get; private set; }
    public bool IsAnimating { get { return anim != Anim.None; } }
    public string InteractPrompt { get { return IsOpen ? "" : "Inspect"; } }

    private enum Anim { None, Opening, Closing }
    private Anim anim = Anim.None;
    private float t;
    private bool colliderSwitched;

    private SpriteRenderer sr;
    private Collider2D col;

    // the animated halves: [0] = left / bottom, [1] = right / top
    private bool built;
    private Transform animRoot;
    private readonly Transform[] halfT = new Transform[2];
    private readonly SpriteRenderer[] purple = new SpriteRenderer[2];
    private readonly SpriteRenderer[] green = new SpriteRenderer[2];
    private readonly Vector3[] halfDir = new Vector3[2];
    private float slideDist = 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        if (col == null)
        {
            var b = gameObject.AddComponent<BoxCollider2D>();
            b.size = upDown ? new Vector2(1f, 2f) : new Vector2(2f, 1f);
            col = b;
        }
        ApplyLook(false);
    }

    private void Start()
    {
        if (ForestProgress.IsUnlocked(id)) SetOpen(true);
    }

    // ------------------------------------------------------------------ look
    private Sprite LockedSprite { get { return style == null ? null : (upDown ? style.gateUpDownLocked : style.gateAcrossLocked); } }
    private Sprite GreenSprite { get { return style == null ? null : (upDown ? style.gateUpDownGreen : style.gateAcrossGreen); } }

    private void ApplyLook(bool open)
    {
        if (style == null || sr == null) return;
        Sprite s = upDown ? (open ? style.gateUpDownOpen : style.gateUpDownLocked) : (open ? style.gateAcrossOpen : style.gateAcrossLocked);
        if (s != null) sr.sprite = s;
    }

    /// <summary>Instant, no animation (used when the game starts and the door was already open).</summary>
    private void SetOpen(bool open)
    {
        anim = Anim.None;
        HideHalves();
        IsOpen = open;
        if (col != null) col.enabled = !open;
        if (sr != null) { sr.enabled = true; sr.color = Color.white; }
        ApplyLook(open);
    }

    // ------------------------------------------------------------------ public
    /// <summary>Opens the door with the colour change + slide animation and remembers it.</summary>
    public void Unlock()
    {
        if (IsOpen && anim != Anim.Closing) return;
        ForestProgress.Unlock(id);
        IsOpen = true;
        if (!BeginAnimation(Anim.Opening)) { SetOpen(true); return; }
        Burst();
    }

    /// <summary>Closes the door again (the reverse animation) and forgets that it was open.</summary>
    public void Lock()
    {
        GameState.SetFlag(ForestProgress.Key(id), false);
        if (!IsOpen && anim != Anim.Opening) return;
        IsOpen = false;
        if (!BeginAnimation(Anim.Closing)) SetOpen(false);
    }

    public void Interact(PlayerActions player)
    {
        if (!IsOpen) GameEvents.ShowToast(lockedHint);
    }

    // ------------------------------------------------------------------ animation
    private bool BeginAnimation(Anim a)
    {
        if (!BuildHalves()) return false;
        anim = a;
        t = 0f;
        colliderSwitched = false;
        sr.enabled = false;
        if (a == Anim.Closing && col != null) col.enabled = true;     // blocks the way straight away
        Update();                                                      // place the halves right now so nothing flickers
        return true;
    }

    private void Update()
    {
        if (anim == Anim.None) return;
        t += Time.deltaTime;
        float total = greenSeconds + slideSeconds;

        // Opening: colour (0..greenSeconds) then slide. Closing: slide in, then colour back to purple.
        float color, slide;                     // color 0 = purple, 1 = green;  slide 0 = together, 1 = fully apart
        if (anim == Anim.Opening)
        {
            color = Mathf.Clamp01(t / greenSeconds);
            slide = Mathf.Clamp01((t - greenSeconds) / slideSeconds);
        }
        else
        {
            slide = 1f - Mathf.Clamp01(t / slideSeconds);
            color = 1f - Mathf.Clamp01((t - slideSeconds) / greenSeconds);
        }

        float eased = slide * slide * (3f - 2f * slide);
        float shake = 0f;
        if (anim == Anim.Opening && slide <= 0f) shake = Mathf.Sin(t * 70f) * 0.03f * Mathf.Lerp(0.4f, 1f, color);   // vines tremble as they change
        float fade = 1f - Mathf.Clamp01((slide - 0.55f) / 0.45f);                                                    // the halves fade out at the end

        for (int i = 0; i < 2; i++)
        {
            halfT[i].localPosition = halfDir[i] * (eased * slideDist) + (upDown ? new Vector3(shake, 0f, 0f) : new Vector3(0f, shake, 0f));
            SetAlpha(purple[i], (1f - color) * fade);
            SetAlpha(green[i], color * fade);
        }

        if (anim == Anim.Opening && slide > 0f && !colliderSwitched)
        {
            colliderSwitched = true;
            if (col != null) col.enabled = false;
            Burst();
        }

        if (t >= total)
        {
            bool opened = anim == Anim.Opening;
            anim = Anim.None;
            SetOpen(opened);
        }
    }

    private static void SetAlpha(SpriteRenderer r, float a)
    {
        if (r == null) return;
        Color c = r.color; c.a = Mathf.Clamp01(a); r.color = c;
    }

    private bool BuildHalves()
    {
        if (built) { ShowHalves(); return true; }
        Sprite lockedS = LockedSprite;
        if (lockedS == null) return false;
        Sprite greenS = GreenSprite;
        bool tint = greenS == null;
        if (tint) greenS = lockedS;

        animRoot = new GameObject("GateAnim").transform;
        animRoot.SetParent(transform, false);
        animRoot.localPosition = Vector3.zero;

        Vector2 size = lockedS.bounds.size;
        slideDist = (upDown ? size.y : size.x) * 0.5f;
        halfDir[0] = upDown ? Vector3.down : Vector3.left;
        halfDir[1] = upDown ? Vector3.up : Vector3.right;

        for (int i = 0; i < 2; i++)
        {
            var h = new GameObject("Half" + i);
            h.transform.SetParent(animRoot, false);
            halfT[i] = h.transform;
            purple[i] = MakeRenderer(h.transform, "Purple", Half(lockedS, i), sr.sortingOrder - 3, Color.white);
            green[i] = MakeRenderer(h.transform, "Green", Half(greenS, i), sr.sortingOrder - 2, tint ? greenTint : Color.white);
        }
        built = true;
        ShowHalves();
        return true;
    }

    private SpriteRenderer MakeRenderer(Transform parent, string name, Sprite s, int order, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = s;
        r.sortingLayerID = sr.sortingLayerID;
        r.sortingOrder = order;           // lower than the walls, so the halves slide in behind them
        r.color = c;
        return r;
    }

    /// <summary>One half of a picture, with its pivot on the cut line so the two halves fit together at (0,0).</summary>
    private Sprite Half(Sprite whole, int i)
    {
        Rect r = whole.textureRect;
        Rect part; Vector2 pivot;
        if (!upDown)
        {
            part = new Rect(r.x + (i == 0 ? 0f : r.width * 0.5f), r.y, r.width * 0.5f, r.height);
            pivot = i == 0 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        }
        else
        {
            part = new Rect(r.x, r.y + (i == 0 ? 0f : r.height * 0.5f), r.width, r.height * 0.5f);
            pivot = i == 0 ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        }
        return Sprite.Create(whole.texture, part, pivot, whole.pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private void ShowHalves() { if (animRoot != null) animRoot.gameObject.SetActive(true); }
    private void HideHalves() { if (animRoot != null) animRoot.gameObject.SetActive(false); }

    private void Burst()
    {
        for (int i = 0; i < 16; i++)
        {
            Vector2 along = upDown ? Vector2.up : Vector2.right;
            Vector2 across = upDown ? Vector2.right : Vector2.up;
            Vector3 p = transform.position + (Vector3)(along * Random.Range(-0.9f, 0.9f) + across * Random.Range(-0.3f, 0.3f));
            Vector2 v = across * Random.Range(-0.8f, 0.8f) + Vector2.up * Random.Range(0.3f, 1.1f) + along * Random.Range(-0.5f, 0.5f);
            GateSpark.Spawn(p, v, Random.Range(0.6f, 1.1f), Random.Range(0.08f, 0.16f), sparkColor, sr.sortingOrder + 2);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsOpen ? new Color(0.4f, 1f, 0.4f, 0.6f) : new Color(1f, 0.3f, 0.5f, 0.6f);
        Gizmos.DrawWireCube(transform.position, upDown ? new Vector3(1f, 2f, 0f) : new Vector3(2f, 1f, 0f));
    }
}

/// <summary>A tiny glowing speck that drifts up and fades (the green sparks when a gate unlocks).</summary>
public class GateSpark : MonoBehaviour
{
    private Vector2 vel; private float life, age, size; private Color color; private SpriteRenderer sr;

    public static void Spawn(Vector3 pos, Vector2 velocity, float lifetime, float size, Color color, int order)
    {
        var go = new GameObject("Spark");
        go.transform.position = pos;
        var m = go.AddComponent<GateSpark>();
        m.vel = velocity; m.life = lifetime; m.size = size; m.color = color;
        m.sr = go.AddComponent<SpriteRenderer>();
        m.sr.sprite = RuntimeSprites.Circle;
        m.sr.sortingOrder = order;
        m.sr.color = color;
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<LightingExempt>();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float k = age / life;
        transform.position += (Vector3)(vel * Time.deltaTime);
        transform.localScale = Vector3.one * size * (1f - k * 0.5f);
        var c = color; c.a = color.a * (1f - k);
        sr.color = c;
    }
}
