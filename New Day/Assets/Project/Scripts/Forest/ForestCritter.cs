using UnityEngine;

/// <summary>
/// A friendly forest animal (rabbit, squirrel, hedgehog, bird, turtle) that lives in a healed ForestZone.
/// It potters about near where it appeared: rests (breathing, blinking), wanders a little, and now and then does its
/// special move (sits up, nibbles an acorn, curls up, flaps, hides in its shell). Shy ones scurry off if you rush at them.
/// Press the action button next to one to pet it: it does its special move and little hearts float up.
///
/// You normally don't place these by hand: ForestZone makes them with ForestCritter.Spawn, using the pictures on
/// Forest Heal Settings (Assets/Resources/ForestHealSettings.asset).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ForestCritter : MonoBehaviour, IInteractable
{
    [Header("Animal")]
    [Tooltip("Pictures and manners. Filled in by Spawn from Forest Heal Settings.")]
    public ForestHealSettings.CritterArt art;

    [Header("Behaviour")]
    [Tooltip("How far (in tiles) it wanders from where it appeared.")]
    [Min(0.5f)] public float wanderRadius = 3.5f;
    [Tooltip("Shy animals run off when the player comes closer than this (0 = never).")]
    [Min(0f)] public float shyDistance = 1.6f;
    [Tooltip("How much faster it runs than it walks.")]
    [Min(1f)] public float runSpeedMultiplier = 2.2f;
    [Tooltip("Chance that a rest ends with its special move instead of a walk.")]
    [Range(0f, 1f)] public float specialChance = 0.25f;
    [Tooltip("Seconds of rest between walks (a random amount between these two).")]
    public Vector2 restSeconds = new Vector2(1.5f, 4f);

    private enum State { Rest, Walk, Special, Run }
    private State state = State.Rest;
    private float stateTimer, animTime;
    private Vector2 home, target, runDir;
    private int lastPetDay = -1;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Transform pic;
    private Transform player;

    public string InteractPrompt { get { return art != null ? "Pet the " + art.id : "Pet"; } }

    // ------------------------------------------------------------------ making one
    /// <summary>Makes a friendly animal at 'position' (its feet), as a child of 'parent'.</summary>
    public static ForestCritter Spawn(ForestHealSettings.CritterArt art, Vector3 position, Transform parent)
    {
        var go = new GameObject("Critter_" + (art != null ? art.id : "animal"));
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.mass = 0.3f;                                       // light: the player can nudge it
        rb.linearDamping = 6f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.25f;
        col.offset = new Vector2(0f, 0.2f);

        // the picture is on a child, so hopping only lifts the picture, not the body
        var p = new GameObject("Picture");
        p.transform.SetParent(go.transform, false);
        var sr = p.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        Material lit = Resources.Load<Material>("Lighting/SpriteLit");
        if (lit != null) sr.sharedMaterial = lit;
        p.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;

        // a soft round shadow on the ground
        var sh = new GameObject("GroundShadow");
        sh.transform.SetParent(go.transform, false);
        sh.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        sh.transform.localScale = new Vector3(0.7f, 0.25f, 1f);
        var shr = sh.AddComponent<SpriteRenderer>();
        shr.sprite = RuntimeSprites.Circle;
        shr.color = new Color(0f, 0f, 0f, 0.25f);
        shr.sortingOrder = 9;
        go.AddComponent<LightingExempt>();

        var c = go.AddComponent<ForestCritter>();
        c.art = art;
        if (art != null && art.idleFrames != null && art.idleFrames.Length > 0) sr.sprite = art.idleFrames[0];
        return c;
    }

    // ------------------------------------------------------------------ life
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        pic = sr != null ? sr.transform : transform;
    }

    private void Start()
    {
        home = transform.position;
        GoRest();
        stateTimer *= Random.value;                          // so a group of animals doesn't move in step
    }

    private void Update()
    {
        if (art == null) return;
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        // shy animals scurry away from a player who comes too close (unless they are being petted)
        if (!art.tame && shyDistance > 0f && player != null && state != State.Run && state != State.Special
            && Vector2.Distance(player.position, transform.position) < shyDistance)
        {
            runDir = ((Vector2)transform.position - (Vector2)player.position).normalized;
            if (runDir.sqrMagnitude < 0.01f) runDir = Random.insideUnitCircle.normalized;
            state = State.Run; stateTimer = 0.7f; animTime = 0f;
        }

        stateTimer -= Time.deltaTime;
        animTime += Time.deltaTime;
        Vector2 vel = Vector2.zero;

        switch (state)
        {
            case State.Rest:
                if (stateTimer <= 0f)
                {
                    if (Random.value < specialChance && HasFrames(art.specialFrames)) GoSpecial();
                    else GoWalk();
                }
                break;

            case State.Walk:
            {
                Vector2 to = target - (Vector2)transform.position;
                if (to.magnitude < 0.1f || stateTimer <= 0f) GoRest();
                else vel = to.normalized * art.walkSpeed;
                break;
            }

            case State.Special:
                if (stateTimer <= 0f) GoRest();
                break;

            case State.Run:
                vel = runDir * art.walkSpeed * runSpeedMultiplier;
                if (stateTimer <= 0f) GoRest();
                break;
        }

        rb.linearVelocity = vel;
        Animate(vel);
    }

    private void GoRest()
    {
        state = State.Rest;
        stateTimer = Random.Range(restSeconds.x, Mathf.Max(restSeconds.x, restSeconds.y));
        animTime = 0f;
    }

    private void GoWalk()
    {
        // a new spot near home (it drifts back home if it has wandered off)
        target = home + Random.insideUnitCircle * wanderRadius;
        state = State.Walk;
        stateTimer = 4f;                                     // gives up after a while if something is in the way
        animTime = 0f;
    }

    private void GoSpecial()
    {
        state = State.Special;
        stateTimer = art.specialFrames.Length / Mathf.Max(1f, art.framesPerSecond) * 1.5f;   // a little slower than walking
        animTime = 0f;
    }

    private static bool HasFrames(Sprite[] f) { return f != null && f.Length > 0; }

    // ------------------------------------------------------------------ pictures
    private void Animate(Vector2 vel)
    {
        if (sr == null) return;
        if (Mathf.Abs(vel.x) > 0.05f) sr.flipX = vel.x < 0f;     // the pictures face right

        float fps = Mathf.Max(1f, art.framesPerSecond);
        Sprite[] frames;
        float speed = fps;
        bool loop = true;
        if (state == State.Walk || state == State.Run) { frames = art.moveFrames; if (state == State.Run) speed *= 1.6f; }
        else if (state == State.Special) { frames = art.specialFrames; speed = fps / 1.5f; loop = false; }
        else { frames = art.idleFrames; speed = fps * 0.6f; }
        if (!HasFrames(frames)) frames = HasFrames(art.idleFrames) ? art.idleFrames : art.moveFrames;
        if (!HasFrames(frames)) return;

        int i = Mathf.FloorToInt(animTime * speed);
        i = loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1);
        if (frames[i] != null) sr.sprite = frames[i];

        // hoppers lift a little during each move cycle
        float lift = 0f;
        if ((state == State.Walk || state == State.Run) && art.hopHeight > 0f)
        {
            float cycle = frames.Length / speed;
            float k = (animTime % cycle) / cycle;
            lift = Mathf.Sin(k * Mathf.PI) * art.hopHeight;
        }
        pic.localPosition = new Vector3(0f, lift, 0f);
    }

    // ------------------------------------------------------------------ petting
    public void Interact(PlayerActions p)
    {
        if (art == null) return;
        rb.linearVelocity = Vector2.zero;
        if (HasFrames(art.specialFrames)) GoSpecial(); else GoRest();
        if (player != null && Mathf.Abs(player.position.x - transform.position.x) > 0.1f) sr.flipX = player.position.x < transform.position.x;

        // little hearts floating up
        Sprite heart = Heart();
        for (int i = 0; i < 3; i++)
            HealSpark.Spawn(transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0.9f + i * 0.15f, 0f),
                            new Vector2(Random.Range(-0.2f, 0.2f), 0.7f), 1.2f, 1f, new Color(1f, 0.55f, 0.65f, 1f), 30, heart);

        // counts once per animal per day (for achievements and quests)
        int today = TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 0;
        if (today != lastPetDay)
        {
            lastPetDay = today;
            string counter = ForestHealSettings.Current.pettedCounter;
            if (!string.IsNullOrEmpty(counter)) GameState.AddCounter(counter);
        }
    }

    // a tiny pixel heart, drawn once in code (7 x 6 pixels, 16 pixels per unit)
    private static Sprite heartSprite;
    private static Sprite Heart()
    {
        if (heartSprite != null) return heartSprite;
        string[] rows = { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." };
        var tex = new Texture2D(7, 6, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 7; x++)
                tex.SetPixel(x, 5 - y, rows[y][x] == '#' ? Color.white : Color.clear);
        tex.Apply();
        heartSprite = Sprite.Create(tex, new Rect(0, 0, 7, 6), new Vector2(0.5f, 0.5f), 16f);
        return heartSprite;
    }
}
