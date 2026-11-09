using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mitzi, the ragdoll cat who lives by the house. She has the attitude of a cat: she does what she likes.
///   - Spends her day between the CAT TREE (naps on the platforms), the DOUGLAS FIR (shade) and a SUNNY SPOT.
///   - Walks between them, jumps up and down the cat tree, and fills the time with idle animations:
///     rolling slowly in the sun (white tummy up!), licking herself, a hairball (nothing is spawned), and batting a toy mouse or ball around.
///   - Sleeps on the cat tree at night. If you walk up to the tree she hops down to see what you want.
///   - Talk to her to learn about MAGIC, CRAFTING and FISHING. She knows what you own and tells you what she can see.
/// Everything is tweakable in the Inspector. Art arrays are filled by Farm > NPCs > Set Up Mitzi.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MitziCat : MonoBehaviour, IInteractable
{
    [Header("Art (filled in by Farm > NPCs > Set Up Mitzi)")]
    [SerializeField] private Sprite[] walkSide;          // facing right (left uses a flip)
    [SerializeField] private Sprite[] walkDown;
    [SerializeField] private Sprite[] walkUp;
    [SerializeField] private Sprite[] sitIdle;           // 0 tail left, 1 middle, 2 right, 3 blink
    [SerializeField] private Sprite[] lickPaw;
    [SerializeField] private Sprite[] loaf;              // 2 frames (breathing)
    [SerializeField] private Sprite[] jump;              // 0 crouch, 1 in the air
    [SerializeField] private Sprite[] rollOver;          // 6 frames
    [SerializeField] private Sprite[] hairball;          // 6 frames
    [SerializeField] private Sprite[] batToy;            // 4 frames
    [SerializeField] private Sprite toyMouse, toyBall;
    [SerializeField] private Sprite portraitIdle, portraitTalk, portraitHappy, portraitBlink, dialoguePanel, portraitFrame;

    [Header("Places")]
    [SerializeField] private CatTree catTree;
    [Tooltip("The spot under the Douglas fir where she lounges in the shade.")]
    [SerializeField] private Transform shadeSpot;
    [Tooltip("Sunny patches of ground where she sunbathes and rolls around.")]
    [SerializeField] private Transform[] sunSpots;
    [Tooltip("Glowing sun patches on the ground. They fade in and out with the time of day.")]
    [SerializeField] private SpriteRenderer[] sunPatches;

    [Header("Day")]
    [Tooltip("She only sunbathes between these hours (24h clock).")]
    [SerializeField] private int sunStartHour = 8;
    [SerializeField] private int sunEndHour = 17;
    [Tooltip("At night she sleeps on the cat tree.")]
    [SerializeField] private bool sleepAtNightOnTree = true;

    [Header("Moving")]
    [SerializeField] private float walkSpeed = 1.9f;
    [SerializeField] private float trotSpeed = 3.2f;
    [SerializeField] private float jumpTime = 0.7f;
    [SerializeField] private float stayMin = 30f;
    [SerializeField] private float stayMax = 70f;
    [Tooltip("If you stand this close to the cat tree while she is on it, she hops down to see you.")]
    [SerializeField] private float greetRadius = 2.6f;

    [Header("Animation speeds (frames per second)")]
    [SerializeField] private float walkFps = 7f;
    [SerializeField] private float sitFps = 2.5f;
    [SerializeField] private float lickFps = 4f;
    [SerializeField] private float loafFps = 0.8f;
    [SerializeField] private float rollFps = 2.2f;
    [SerializeField] private float hairballFps = 4f;
    [SerializeField] private float batFps = 7f;

    [Header("Toy play")]
    [SerializeField] private int batsPerSession = 4;
    [SerializeField] private float playRadius = 3f;

    [Header("Talking")]
    [SerializeField] private string catName = "Mitzi";
    [SerializeField] private Color nameColor = new Color(0.35f, 0.3f, 0.62f);
    [Range(0f, 1f)] [SerializeField] private float aloofChance = 0.3f;
    [TextArea] [SerializeField] private string[] aloofLines =
    {
        "*Mitzi pretends not to see you. A very long slow blink later...*",
        "*she finishes licking one paw before turning around, entirely on purpose*",
        "*one ear twitches. That's all you get for now.*",
    };
    [TextArea] [SerializeField] private string[] greetings =
    {
        "Mrrow. Oh, it's you. I suppose I can spare a moment. Between naps.",
        "*slow blink* ...Yes? Make it interesting.",
        "I was in the middle of an important nap. Speak.",
        "Mmrrp. You may approach. Mind the whiskers.",
    };
    [Tooltip("Turn this on once your fishing system exists. Until then Mitzi says the fish haven't moved in yet.")]
    [SerializeField] private bool fishingUnlocked = false;
    [TextArea] [SerializeField] private string[] fishingPages =
    {
        "Equip a Fishing Rod, face the water and press Space to cast. Then wait for a bite. Patience is the whole trick, and nobody is more patient than a cat at a mouse hole.",
    };

    private const string MetFlag = "mitzi_met";

    private enum Mode { Walk, Jump, Sit, Lick, Loaf, Hairball, Roll, Play, Talk }
    private enum Spot { None, TreeBase, Perch, Shade, Sun }
    private enum PlayPhase { Approach, Bat, Slide }

    private static readonly int[] SitSeq = { 0, 1, 2, 1 };
    private static readonly int[] HairSeq = { 0, 1, 2, 3, 2, 3, 2, 3, 4, 5, 5 };
    private static readonly int[] RollSeq = { 0, 1, 2, 3, 4, 3, 4, 3, 2, 1, 5, 5, 0, 0 };

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private SpriteRenderer toy;
    private PlayerController player;
    private PlayerActions playerActions;
    private SpriteRenderer playerSr;
    private DialogueSpeaker speaker;

    private Mode mode = Mode.Sit;
    private Spot spot = Spot.None;
    private Spot goingTo = Spot.None;
    private bool perched;                       // standing on the cat tree (physics off)
    private Vector2 dest;
    private Vector2 desiredVelocity;
    private Vector2 facing = Vector2.down;

    private float stayTimer, activityTime, activityLength, animTime, stuckTimer, greetTimer;
    private Mode lastActivity = Mode.Walk;

    // jump
    private Vector2 jumpFrom, jumpTo;
    private float jumpT;
    private System.Action afterJump;

    // toy
    private PlayPhase playPhase;
    private int playCount;
    private bool launched;
    private float slideTimer, toyFade = 1f;
    private Vector2 toyPos, toyVel, playCenter;
    private bool facingLeft;

    public string InteractPrompt { get { return "Talk to " + catName; } }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        sr = GetComponentInChildren<SpriteRenderer>();

        var toyGo = new GameObject("MitziToy");
        toy = toyGo.AddComponent<SpriteRenderer>();
        toy.sortingOrder = 9;
        toyGo.SetActive(false);

        speaker = new DialogueSpeaker
        {
            speakerName = catName,
            idle = portraitIdle, talk = portraitTalk, happy = portraitHappy, blink = portraitBlink,
            panel = dialoguePanel, frame = portraitFrame, nameColor = nameColor,
        };
    }

    private void Start()
    {
        FindPlayer();
        if (catTree == null) catTree = FindAnyObjectByType<CatTree>();

        // start wherever a cat would be at this time of day
        if (IsNight && sleepAtNightOnTree && catTree != null)
        {
            Vector2 p = catTree.RandomPerch();
            PlaceOnPerch(p);
            spot = Spot.Perch;
            ArriveAtSpot();
        }
        else
        {
            Vector2 p = PickGroundSpotPosition(out spot);
            Warp(p);
            ArriveAtSpot();
        }
    }

    private void OnDestroy()
    {
        if (toy != null) Destroy(toy.gameObject);
    }

    private void FindPlayer()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        playerActions = player.GetComponent<PlayerActions>();
        playerSr = player.GetComponentInChildren<SpriteRenderer>();

        Collider2D[] mine = GetComponents<Collider2D>();
        Collider2D[] theirs = player.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D a in mine) foreach (Collider2D b in theirs) Physics2D.IgnoreCollision(a, b, true);
    }

    private void Warp(Vector2 p)
    {
        perched = false;
        rb.simulated = true;
        rb.position = p;
        transform.position = p;
        rb.linearVelocity = Vector2.zero;
        stuckTimer = 0f;
    }

    private void PlaceOnPerch(Vector2 p)
    {
        perched = true;
        rb.simulated = false;
        transform.position = p;
    }

    // ------------------------------------------------------------------
    // Time of day
    // ------------------------------------------------------------------

    private bool IsNight { get { return TimeManager.Instance != null && TimeManager.Instance.IsNight; } }

    private bool IsSunny
    {
        get
        {
            if (TimeManager.Instance == null) return true;
            int h = TimeManager.Instance.HourOfDay;
            return h >= sunStartHour && h < sunEndHour;
        }
    }

    private float Daylight
    {
        get
        {
            if (TimeManager.Instance == null) return 1f;
            float h = TimeManager.Instance.HoursDecimal % 24f;
            return Mathf.Clamp01(Mathf.Min(h - (sunStartHour - 1f), (sunEndHour + 1f) - h));
        }
    }

    // ------------------------------------------------------------------
    // Main loop
    // ------------------------------------------------------------------

    private void Update()
    {
        if (player == null) { FindPlayer(); }

        UpdateSunPatches();
        desiredVelocity = Vector2.zero;
        animTime += Time.deltaTime;

        switch (mode)
        {
            case Mode.Talk: break;
            case Mode.Walk: UpdateWalk(); break;
            case Mode.Jump: UpdateJump(); break;
            case Mode.Play: UpdatePlay(); break;
            default: UpdateIdle(); break;
        }

        UpdateToyVisual();
        Animate();
        UpdateSorting();
    }

    private void FixedUpdate()
    {
        if (rb != null && rb.simulated) rb.linearVelocity = desiredVelocity;
    }

    private void UpdateSunPatches()
    {
        if (sunPatches == null) return;
        float a = Daylight;
        foreach (SpriteRenderer p in sunPatches) if (p != null) p.color = new Color(1f, 1f, 1f, a);
    }

    // ------------------------------------------------------------------
    // Choosing where to be
    // ------------------------------------------------------------------

    private Vector2 PickGroundSpotPosition(out Spot chosen)
    {
        var options = new List<Spot>();
        if (shadeSpot != null) options.Add(Spot.Shade);
        if (IsSunny && sunSpots != null && sunSpots.Length > 0) options.Add(Spot.Sun);
        if (options.Count == 0 && catTree != null) { chosen = Spot.TreeBase; return catTree.GroundPoint; }
        if (options.Count == 0) { chosen = Spot.None; return transform.position; }
        chosen = options[Random.Range(0, options.Count)];
        return SpotPosition(chosen);
    }

    private Vector2 SpotPosition(Spot s)
    {
        switch (s)
        {
            case Spot.Shade: return shadeSpot != null ? (Vector2)shadeSpot.position : (Vector2)transform.position;
            case Spot.Sun:
            {
                Transform t = sunSpots[Random.Range(0, sunSpots.Length)];
                return (Vector2)t.position + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.2f));
            }
            case Spot.TreeBase:
            case Spot.Perch: return catTree != null ? catTree.GroundPoint : (Vector2)transform.position;
            default: return transform.position;
        }
    }

    private void ChooseNextSpot()
    {
        var options = new List<Spot>();
        if (IsNight && sleepAtNightOnTree && catTree != null) options.Add(Spot.Perch);
        else
        {
            if (catTree != null) options.Add(Spot.Perch);
            if (shadeSpot != null) options.Add(Spot.Shade);
            if (IsSunny && sunSpots != null && sunSpots.Length > 0) options.Add(Spot.Sun);
        }
        options.Remove(spot);
        if (options.Count == 0) { stayTimer = Random.Range(stayMin, stayMax); BeginActivity(); return; }

        goingTo = options[Random.Range(0, options.Count)];
        Vector2 target = SpotPosition(goingTo);

        if (perched)
        {
            // hop down first, then wander over
            JumpTo(catTree.GroundPoint, false, () => StartWalk(target));
        }
        else StartWalk(target);
    }

    private void StartWalk(Vector2 target)
    {
        dest = target;
        mode = Mode.Walk;
        animTime = 0f;
        stuckTimer = 0f;
    }

    private void ArriveAtSpot()
    {
        stayTimer = Random.Range(stayMin, stayMax);
        goingTo = Spot.None;
        BeginActivity();
    }

    // ------------------------------------------------------------------
    // Walking and jumping
    // ------------------------------------------------------------------

    private void UpdateWalk()
    {
        Vector2 pos = rb.position;
        Vector2 to = dest - pos;
        float dist = to.magnitude;

        if (dist < 0.12f)
        {
            // got there
            if (goingTo == Spot.Perch && catTree != null)
            {
                JumpTo(catTree.RandomPerch(), true, () => { spot = Spot.Perch; ArriveAtSpot(); });
            }
            else
            {
                if (goingTo != Spot.None) spot = goingTo;
                ArriveAtSpot();
            }
            return;
        }

        Vector2 dir = to / dist;
        float speed = dist > 6f ? trotSpeed : walkSpeed;
        desiredVelocity = dir * speed;
        facing = dir;

        if (rb.linearVelocity.magnitude < speed * 0.2f) stuckTimer += Time.deltaTime; else stuckTimer = 0f;
        if (stuckTimer > 1.5f) { Warp(dest); }
    }

    private void JumpTo(Vector2 target, bool up, System.Action then)
    {
        jumpFrom = transform.position;
        jumpTo = target;
        jumpT = 0f;
        afterJump = then;
        mode = Mode.Jump;
        animTime = 0f;
        rb.simulated = false;
        facingLeft = target.x < jumpFrom.x;
        perched = true;                          // physics stay off until she lands on the ground
        landingOnPerch = up;
    }

    private bool landingOnPerch;

    private void UpdateJump()
    {
        jumpT += Time.deltaTime / Mathf.Max(0.1f, jumpTime);
        float t = Mathf.Clamp01(jumpT);
        float e = t * t * (3f - 2f * t);
        float height = 0.45f + Mathf.Abs(jumpTo.y - jumpFrom.y) * 0.25f;
        Vector2 p = Vector2.Lerp(jumpFrom, jumpTo, e) + Vector2.up * (4f * height * t * (1f - t));
        transform.position = p;

        if (t >= 1f)
        {
            transform.position = jumpTo;
            if (landingOnPerch) { perched = true; rb.simulated = false; }
            else Warp(jumpTo);
            System.Action cb = afterJump;
            afterJump = null;
            if (cb != null) cb();
        }
    }

    // ------------------------------------------------------------------
    // Idle activities
    // ------------------------------------------------------------------

    private void UpdateIdle()
    {
        stayTimer -= Time.deltaTime;
        activityTime += Time.deltaTime;

        // cat tree visitors: hop down when the player comes close
        if (spot == Spot.Perch && perched && catTree != null && player != null && mode != Mode.Hairball)
        {
            float d = Vector2.Distance(player.transform.position, catTree.GroundPoint);
            greetTimer = d < greetRadius ? greetTimer + Time.deltaTime : 0f;
            if (greetTimer > 1.0f)
            {
                greetTimer = 0f;
                spot = Spot.TreeBase;
                JumpTo(catTree.GroundPoint, false, () => { stayTimer = 20f; goingTo = Spot.None; BeginActivity(Mode.Sit); });
                return;
            }
        }

        if (activityTime >= activityLength)
        {
            if (stayTimer <= 0f && !(IsNight && sleepAtNightOnTree && spot == Spot.Perch)) ChooseNextSpot();
            else BeginActivity();
        }
    }

    private void BeginActivity(Mode forced = Mode.Walk)
    {
        Mode next = forced != Mode.Walk ? forced : PickActivity();
        lastActivity = next;
        mode = next;
        animTime = 0f;
        activityTime = 0f;

        switch (next)
        {
            case Mode.Sit: activityLength = Random.Range(4f, 8f); break;
            case Mode.Lick: activityLength = Random.Range(3f, 6f); break;
            case Mode.Loaf: activityLength = Random.Range(8f, 16f); break;
            case Mode.Hairball: activityLength = HairSeq.Length / hairballFps + 1.2f; break;
            case Mode.Roll: activityLength = RollSeq.Length / rollFps + 1.5f; break;
            case Mode.Play: StartPlay(); break;
        }
    }

    private Mode PickActivity()
    {
        bool night = IsNight;
        var picks = new List<KeyValuePair<Mode, float>>();
        bool canRoll = !perched && spot == Spot.Sun && IsSunny;
        bool canPlay = !perched && !night && (spot == Spot.Shade || spot == Spot.Sun);

        if (night && spot == Spot.Perch) return Mode.Loaf;

        picks.Add(new KeyValuePair<Mode, float>(Mode.Sit, 2f));
        picks.Add(new KeyValuePair<Mode, float>(Mode.Lick, 2f));
        picks.Add(new KeyValuePair<Mode, float>(Mode.Loaf, spot == Spot.Perch ? 5f : 3f));
        picks.Add(new KeyValuePair<Mode, float>(Mode.Hairball, 0.6f));
        if (canRoll) picks.Add(new KeyValuePair<Mode, float>(Mode.Roll, 4f));
        if (canPlay) picks.Add(new KeyValuePair<Mode, float>(Mode.Play, 1.6f));

        for (int attempt = 0; attempt < 2; attempt++)
        {
            float total = 0f;
            foreach (var p in picks) total += p.Value;
            float r = Random.value * total;
            foreach (var p in picks)
            {
                r -= p.Value;
                if (r <= 0f) { if (p.Key != lastActivity || attempt == 1) return p.Key; break; }
            }
        }
        return Mode.Sit;
    }

    // ------------------------------------------------------------------
    // Playing with the toy
    // ------------------------------------------------------------------

    private void StartPlay()
    {
        playCenter = transform.position;
        playCount = 0;
        launched = false;
        toyFade = 1f;
        toy.sprite = Random.value < 0.5f ? toyMouse : toyBall;
        toy.gameObject.SetActive(toy.sprite != null);
        if (toy.sprite == null) { mode = Mode.Sit; activityLength = 3f; return; }

        float side = Random.value < 0.5f ? -1f : 1f;
        toyPos = (Vector2)transform.position + new Vector2(side * 1.4f, Random.Range(-0.3f, 0.3f));
        toyVel = Vector2.zero;
        toy.transform.position = toyPos;
        playPhase = PlayPhase.Approach;
    }

    private void UpdatePlay()
    {
        // the toy slides and slows down
        toyPos += toyVel * Time.deltaTime;
        toyVel *= Mathf.Max(0f, 1f - 3.2f * Time.deltaTime);
        Vector2 fromCenter = toyPos - playCenter;
        if (fromCenter.magnitude > playRadius)
        {
            toyPos = playCenter + fromCenter.normalized * playRadius;
            toyVel = -toyVel * 0.6f;
        }

        Vector2 pos = rb.position;
        switch (playPhase)
        {
            case PlayPhase.Approach:
            {
                Vector2 to = toyPos - pos;
                float d = to.magnitude;
                facing = to.sqrMagnitude > 0.001f ? to.normalized : facing;
                if (d < 0.95f)
                {
                    playPhase = PlayPhase.Bat;
                    animTime = 0f;
                    launched = false;
                    facingLeft = toyPos.x < pos.x;
                }
                else
                {
                    desiredVelocity = (to / d) * trotSpeed;
                    if (rb.linearVelocity.magnitude < trotSpeed * 0.2f) stuckTimer += Time.deltaTime; else stuckTimer = 0f;
                    if (stuckTimer > 1.5f) { EndPlay(); }
                }
                break;
            }
            case PlayPhase.Bat:
            {
                float frame = animTime * batFps;
                if (!launched && frame >= 2f)
                {
                    launched = true;
                    Vector2 dir = new Vector2(facingLeft ? -1f : 1f, Random.Range(-0.6f, 0.6f)).normalized;
                    // bat it back towards the middle if it is getting far away
                    if (Vector2.Distance(toyPos, playCenter) > playRadius * 0.6f) dir = (playCenter - toyPos).normalized;
                    toyVel = dir * Random.Range(4f, 6f);
                }
                if (frame >= 4f) { playPhase = PlayPhase.Slide; slideTimer = 0f; }
                break;
            }
            default:
                slideTimer += Time.deltaTime;
                if (slideTimer > 0.9f)
                {
                    playCount++;
                    if (playCount >= batsPerSession) EndPlay();
                    else playPhase = PlayPhase.Approach;
                }
                break;
        }
    }

    private void EndPlay()
    {
        toy.gameObject.SetActive(false);
        BeginActivity(Mode.Lick);          // a cat always grooms after pretending it meant to do that
    }

    private void UpdateToyVisual()
    {
        if (toy == null || !toy.gameObject.activeSelf) return;
        toy.transform.position = toyPos;
        if (toy.sprite == toyBall) toy.transform.Rotate(0f, 0f, -toyVel.x * 120f * Time.deltaTime);
        else toy.flipX = toyVel.x < -0.1f ? true : (toyVel.x > 0.1f ? false : toy.flipX);
    }

    // ------------------------------------------------------------------
    // Animation
    // ------------------------------------------------------------------

    private void Animate()
    {
        if (sr == null) return;
        Sprite s = null;

        switch (mode)
        {
            case Mode.Walk:
                s = WalkFrame(desiredVelocity.sqrMagnitude > 0.01f ? desiredVelocity : facing);
                break;
            case Mode.Jump:
                s = (jump != null && jump.Length > 1) ? jump[jumpT < 0.18f ? 0 : 1] : null;
                sr.flipX = facingLeft;
                break;
            case Mode.Play:
                if (playPhase == PlayPhase.Approach) s = WalkFrame(desiredVelocity.sqrMagnitude > 0.01f ? desiredVelocity : facing);
                else if (batToy != null && batToy.Length > 0)
                {
                    int f = playPhase == PlayPhase.Bat ? Mathf.Min(batToy.Length - 1, (int)(animTime * batFps)) : batToy.Length - 1;
                    s = batToy[f];
                    sr.flipX = facingLeft;
                }
                break;
            case Mode.Lick:
                s = Frame(lickPaw, animTime * lickFps);
                sr.flipX = false;
                break;
            case Mode.Loaf:
                s = Frame(loaf, animTime * loafFps);
                sr.flipX = false;
                break;
            case Mode.Hairball:
                if (hairball != null && hairball.Length > 0)
                    s = hairball[Mathf.Min(hairball.Length - 1, HairSeq[Mathf.Min(HairSeq.Length - 1, (int)(animTime * hairballFps))])];
                sr.flipX = false;
                break;
            case Mode.Roll:
                if (rollOver != null && rollOver.Length > 0)
                    s = rollOver[Mathf.Min(rollOver.Length - 1, RollSeq[Mathf.Min(RollSeq.Length - 1, (int)(animTime * rollFps))])];
                sr.flipX = false;
                break;
            default:   // Sit and Talk
                if (sitIdle != null && sitIdle.Length > 3)
                {
                    bool blink = (animTime % 5f) > 4.85f;
                    s = blink ? sitIdle[3] : sitIdle[SitSeq[(int)(animTime * sitFps) % SitSeq.Length]];
                }
                sr.flipX = false;
                break;
        }
        if (s != null) sr.sprite = s;
    }

    private Sprite WalkFrame(Vector2 v)
    {
        Sprite[] set;
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) { set = walkSide; sr.flipX = v.x < 0f; }
        else { set = v.y > 0f ? walkUp : walkDown; sr.flipX = false; }
        return Frame(set, animTime * walkFps);
    }

    private static Sprite Frame(Sprite[] set, float t)
    {
        if (set == null || set.Length == 0) return null;
        return set[((int)t) % set.Length];
    }

    private void UpdateSorting()
    {
        if (sr == null) return;
        if (perched && catTree != null && catTree.Renderer != null)
        {
            sr.sortingLayerID = catTree.Renderer.sortingLayerID;
            sr.sortingOrder = catTree.Renderer.sortingOrder + 1;
            return;
        }
        if (playerSr == null || player == null) return;
        bool inFront = transform.position.y < player.transform.position.y;
        sr.sortingLayerID = playerSr.sortingLayerID;
        sr.sortingOrder = playerSr.sortingOrder + (inFront ? 2 : -2);
    }

    // ------------------------------------------------------------------
    // Talking
    // ------------------------------------------------------------------

    public void Interact(PlayerActions p)
    {
        if (DialogueBox.IsOpen || mode == Mode.Jump) return;
        playerActions = p;
        if (mode == Mode.Play) { toy.gameObject.SetActive(false); }
        mode = Mode.Talk;
        animTime = 0f;
        desiredVelocity = Vector2.zero;

        if (!GameState.HasFlag(MetFlag))
        {
            GameState.SetFlag(MetFlag);
            DialogueBox.Show(speaker, new[]
            {
                "Mrrrow. A new face. I'm " + catName + ". This is my house. You may live in it.",
                "I know about magic, crafting and fishing. Ask nicely. Or don't. I'm comfortable either way.",
            }, HubOptions(), OnClosed);
            return;
        }

        if (Random.value < aloofChance && aloofLines.Length > 0)
        {
            DialogueBox.Show(speaker, new[] { aloofLines[Random.Range(0, aloofLines.Length)], greetings[Random.Range(0, greetings.Length)] }, HubOptions(), OnClosed);
            return;
        }
        DialogueBox.Show(speaker, new[] { greetings[Random.Range(0, greetings.Length)] }, HubOptions(), OnClosed);
    }

    private void OnClosed()
    {
        if (mode == Mode.Talk) BeginActivity(Mode.Sit);
    }

    private List<DialogueOption> HubOptions()
    {
        var o = new List<DialogueOption>();
        o.Add(new DialogueOption("Teach me about magic", () => Lesson(MitziLore.Magic(playerActions))));
        o.Add(new DialogueOption("Teach me about crafting", () => Lesson(MitziLore.Crafting(playerActions))));
        o.Add(new DialogueOption("Teach me about fishing", () => Lesson(MitziLore.Fishing(playerActions, fishingUnlocked, fishingPages))));
        o.Add(new DialogueOption("Pet " + catName, Pet));
        o.Add(new DialogueOption("Let her get back to her nap", null, true));
        return o;
    }

    private void Hub(string line)
    {
        DialogueBox.Show(speaker, new[] { line }, HubOptions(), OnClosed);
    }

    private void Lesson(string[] pages)
    {
        var o = new List<DialogueOption>();
        o.Add(new DialogueOption("Interesting. What else?", () => Hub("Hmm. Anything else? Be quick. The sun is moving.")));
        o.Add(new DialogueOption("Thanks, Mitzi!", null, true));
        DialogueBox.Show(speaker, pages, o, OnClosed);
    }

    private void Pet()
    {
        if (Random.value < 0.55f)
        {
            DialogueBox.Show(speaker, new[] { "*purrrrrrr* ...Don't tell anyone I enjoyed that." }, null,
                () => { if (IsSunny && !perched) BeginActivity(Mode.Roll); else BeginActivity(Mode.Loaf); }, true);
        }
        else
        {
            var o = new List<DialogueOption>();
            o.Add(new DialogueOption("Sorry, Mitzi...", () => Hub("Apology accepted. Barely.")));
            o.Add(new DialogueOption("(back away slowly)", null, true));
            DialogueBox.Show(speaker, new[] { "*swat!* Mrrow! Not the belly. NEVER the belly." }, o, OnClosed);
        }
    }
}
