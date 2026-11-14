using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Meatball, the aussiedoodle farm guide.
///   - Follows the farmer around the farm (walks, runs to catch up, warps if left far behind).
///   - Sits when you stop, and now and then scratches an ear or rolls over.
///   - Talk to him (action button) for: a tip about the closest thing you can do, what a villager likes (ONE per day),
///     a belly rub, or send him back to his dog house / call him out again.
/// All the numbers are in the Inspector. The art arrays are filled by Farm > NPCs > Set Up Meatball.
/// He lives in the farm scene, so he only follows you on the farm.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MeatballDog : MonoBehaviour, IInteractable
{
    [Header("Art (filled in by Farm > NPCs > Set Up Meatball)")]
    [SerializeField] private Sprite[] walkSide;          // facing right (left uses a flip)
    [SerializeField] private Sprite[] walkDown;
    [SerializeField] private Sprite[] walkUp;
    [SerializeField] private Sprite[] sitIdle;           // 0 tail left, 1 tail middle, 2 tail right, 3 blink
    [SerializeField] private Sprite[] rollOver;
    [SerializeField] private Sprite[] scratchEar;
    [SerializeField] private Sprite portraitIdle, portraitTalk, portraitHappy, portraitBlink;
    [SerializeField] private Sprite dialoguePanel, portraitFrame;

    [Header("Following")]
    [SerializeField] private float walkSpeed = 3.4f;
    [SerializeField] private float runSpeed = 6f;
    [Tooltip("He starts following when you are farther than this (tiles).")]
    [SerializeField] private float followDistance = 2.4f;
    [Tooltip("...and stops once he is this close.")]
    [SerializeField] private float closeEnough = 1.5f;
    [Tooltip("If you get farther than this (or walk through a door) he appears next to you.")]
    [SerializeField] private float teleportDistance = 14f;
    [Tooltip("He trots this far behind you...")]
    [SerializeField] private float behindDistance = 1.2f;
    [Tooltip("...and this far to the side.")]
    [SerializeField] private float sideOffset = 0.6f;

    [Header("Idle behaviour")]
    [SerializeField] private float sitAfterSeconds = 1.0f;
    [SerializeField] private float idleEventMin = 6f;
    [SerializeField] private float idleEventMax = 14f;
    [Tooltip("When he does something while sitting: this chance he rolls over, otherwise he scratches an ear.")]
    [Range(0f, 1f)] [SerializeField] private float rollChance = 0.3f;
    [SerializeField] private float scratchSeconds = 2.4f;

    [Header("Animation speeds (frames per second)")]
    [SerializeField] private float walkFps = 8f;
    [SerializeField] private float sitFps = 3f;
    [SerializeField] private float rollFps = 6f;
    [SerializeField] private float scratchFps = 7f;

    [Header("Talking")]
    [SerializeField] private string dogName = "Meatball";
    [SerializeField] private Color nameColor = new Color(0.62f, 0.32f, 0.1f);
    [TextArea] [SerializeField] private string[] greetings =
    {
        "Woof! Hi boss! Ready to get to work?",
        "Woof woof! Did somebody say farm time?",
        "*tail wag* You came to see me? Best day ever!",
        "Arf! I've been sniffing around. Need anything?",
    };
    [Tooltip("Drag NPC Profile assets here. Meatball can tell the player what ONE of them likes each day.")]
    [SerializeField] private NpcProfile[] knownNpcs;
    [Min(3)] [SerializeField] private int npcsPerPage = 5;

    private const string HomeFlag = "meatball_home";
    private const string MetFlag = "meatball_met";
    private const string LikesDayKey = "meatball_likes_day";

    private enum Mode { Move, Sit, Roll, Scratch, Talk }
    private static readonly int[] SitSequence = { 1, 0, 1, 2, 1, 0, 1, 2, 1, 3 };

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private PlayerController player;
    private PlayerActions playerActions;
    private SpriteRenderer playerSr;

    private Mode mode = Mode.Sit;
    private Vector2 desiredVelocity;
    private Vector2 facing = Vector2.down;
    private bool moving;
    private float modeTime, animTime, idleTimer, nextIdleEvent, stuckTimer;
    private int likesPage;
    private DialogueSpeaker speaker;

    public string InteractPrompt { get { return "Talk to " + dogName; } }

    // He counts as "at home" when you sent him there, OR while the player is inside the house (he waits outside).
    private bool AtHome { get { return DogHouse.Instance != null && (GameState.HasFlag(HomeFlag) || PlayerIndoors); } }

    private bool wasIndoors;
    private HouseInterior[] rooms;

    /// <summary>True while the player is standing inside a house room.</summary>
    private bool PlayerIndoors
    {
        get
        {
            if (player == null) return false;
            if (rooms == null) rooms = FindObjectsByType<HouseInterior>();
            Vector2 p = player.transform.position;
            foreach (HouseInterior room in rooms)
                if (room != null && room.WorldBounds.Contains(p)) return true;
            return false;
        }
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        sr = GetComponentInChildren<SpriteRenderer>();

        speaker = new DialogueSpeaker
        {
            speakerName = dogName,
            idle = portraitIdle, talk = portraitTalk, happy = portraitHappy, blink = portraitBlink,
            panel = dialoguePanel, frame = portraitFrame, nameColor = nameColor,
        };
        nextIdleEvent = Random.Range(idleEventMin, idleEventMax);
    }

    private void Start()
    {
        FindPlayer();
        if (player == null) return;

        // start the day at the dog house if he was sent home, otherwise next to the player
        if (AtHome) Warp(DogHouse.Instance.SitPoint);
        else Warp((Vector2)player.transform.position + new Vector2(-1f, -0.5f));
    }

    private void FindPlayer()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        playerActions = player.GetComponent<PlayerActions>();
        playerSr = player.GetComponentInChildren<SpriteRenderer>();

        // he walks through the player instead of shoving them
        Collider2D[] mine = GetComponents<Collider2D>();
        Collider2D[] theirs = player.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D a in mine) foreach (Collider2D b in theirs) Physics2D.IgnoreCollision(a, b, true);
    }

    private void Warp(Vector2 position)
    {
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        stuckTimer = 0f;
    }

    // ------------------------------------------------------------------
    // Behaviour
    // ------------------------------------------------------------------

    private void Update()
    {
        if (player == null) { FindPlayer(); if (player == null) return; }

        desiredVelocity = Vector2.zero;

        // the player just went indoors: he is already waiting at his dog house when they come back out
        bool indoors = PlayerIndoors;
        if (indoors && !wasIndoors && DogHouse.Instance != null && mode != Mode.Talk) { Warp(DogHouse.Instance.SitPoint); EnterSit(); }
        wasIndoors = indoors;

        switch (mode)
        {
            case Mode.Talk:
                break;                                         // stays put and listens
            case Mode.Roll:
            case Mode.Scratch:
                modeTime += Time.deltaTime;
                if (modeTime >= ActionLength()) EnterSit();
                break;
            default:
                Locomotion();
                break;
        }

        Animate();
        UpdateSorting();
    }

    private void FixedUpdate()
    {
        if (rb != null) rb.linearVelocity = desiredVelocity;
    }

    private float ActionLength()
    {
        if (mode == Mode.Roll) return rollOver != null && rollOver.Length > 0 ? rollOver.Length / rollFps + 0.4f : 1f;
        return scratchSeconds;
    }

    private void Locomotion()
    {
        Vector2 pos = rb.position;
        Vector2 ppos = player.transform.position;
        bool home = AtHome;

        Vector2 target;
        float stop;
        if (home) { target = DogHouse.Instance.SitPoint; stop = 0.15f; }
        else
        {
            Vector2 f = player.FacingDirection;
            target = ppos - f * behindDistance + new Vector2(-f.y, f.x) * sideOffset;
            stop = 0.35f;
        }

        float dPlayer = Vector2.Distance(pos, ppos);
        float dTarget = Vector2.Distance(pos, target);

        // far away (or you walked through a door): appear next to the player
        if (!home && dPlayer > teleportDistance) { Warp(target); return; }

        if (!moving) moving = home ? dTarget > 0.5f : dPlayer > followDistance;
        else moving = home ? dTarget > stop : !(dPlayer < closeEnough || dTarget < stop);

        if (moving)
        {
            Vector2 dir = (target - pos);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.zero;
            float speed = dPlayer > 6f ? runSpeed : walkSpeed;
            desiredVelocity = dir * speed;
            if (dir != Vector2.zero) facing = dir;
            mode = Mode.Move;
            idleTimer = 0f;

            // stuck behind something? hop to the target
            if (rb.linearVelocity.magnitude < speed * 0.25f) stuckTimer += Time.deltaTime; else stuckTimer = 0f;
            if (stuckTimer > 1.5f && dTarget > 1.5f) Warp(target);
            return;
        }

        // standing around: stand for a moment, then sit
        stuckTimer = 0f;
        if (mode == Mode.Move)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= sitAfterSeconds) EnterSit();
            return;
        }
        if (mode == Mode.Sit)
        {
            modeTime += Time.deltaTime;
            if (modeTime >= nextIdleEvent) DoIdleEvent();
        }
    }

    private void EnterSit()
    {
        mode = Mode.Sit;
        modeTime = 0f;
        animTime = 0f;
        nextIdleEvent = Random.Range(idleEventMin, idleEventMax);
    }

    private void DoIdleEvent()
    {
        if (Random.value < rollChance && rollOver != null && rollOver.Length > 0) StartRoll();
        else if (scratchEar != null && scratchEar.Length > 0) { mode = Mode.Scratch; modeTime = 0f; animTime = 0f; }
        else EnterSit();
    }

    private void StartRoll()
    {
        mode = Mode.Roll;
        modeTime = 0f;
        animTime = 0f;
    }

    // ------------------------------------------------------------------
    // Animation
    // ------------------------------------------------------------------

    private void Animate()
    {
        if (sr == null) return;
        animTime += Time.deltaTime;
        Sprite s = null;

        switch (mode)
        {
            case Mode.Move:
            {
                Vector2 v = desiredVelocity.sqrMagnitude > 0.01f ? desiredVelocity : facing;
                Sprite[] set;
                if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) { set = walkSide; sr.flipX = v.x < 0f; }
                else { set = v.y > 0f ? walkUp : walkDown; sr.flipX = false; }
                float fps = desiredVelocity.magnitude > walkSpeed + 0.5f ? walkFps * 1.5f : walkFps;
                bool standing = desiredVelocity.sqrMagnitude < 0.01f;          // paused for a moment before sitting
                s = Frame(set, standing ? 0f : animTime * fps);
                break;
            }
            case Mode.Roll:
                s = rollOver != null && rollOver.Length > 0 ? rollOver[Mathf.Min(rollOver.Length - 1, (int)(animTime * rollFps))] : null;
                break;
            case Mode.Scratch:
                s = Frame(scratchEar, animTime * scratchFps);
                break;
            default:   // Sit and Talk
                if (sitIdle != null && sitIdle.Length > 0)
                {
                    int step = (int)(animTime * sitFps) % SitSequence.Length;
                    s = sitIdle[Mathf.Min(sitIdle.Length - 1, SitSequence[step])];
                }
                sr.flipX = false;
                break;
        }
        if (s != null) sr.sprite = s;
    }

    private static Sprite Frame(Sprite[] set, float t)
    {
        if (set == null || set.Length == 0) return null;
        return set[((int)t) % set.Length];
    }

    private void UpdateSorting()
    {
        if (sr == null || playerSr == null) return;
        bool inFront = transform.position.y < player.transform.position.y;
        sr.sortingLayerID = playerSr.sortingLayerID;
        sr.sortingOrder = playerSr.sortingOrder + (inFront ? 2 : -2);
    }

    // ------------------------------------------------------------------
    // Talking
    // ------------------------------------------------------------------

    public void Interact(PlayerActions p)
    {
        if (DialogueBox.IsOpen) return;
        playerActions = p;
        mode = Mode.Talk;
        animTime = 0f;
        desiredVelocity = Vector2.zero;
        sr.flipX = false;

        if (!GameState.HasFlag(MetFlag))
        {
            GameState.SetFlag(MetFlag);
            DialogueBox.Show(speaker, new[]
            {
                "Woof! Hi there! I'm " + dogName + "! I know this farm like the back of my paw.",
                "I can sniff out what needs doing, I know what the villagers like, and I'll follow you wherever you go. Just say the word!",
            }, HubOptions(), OnClosed);
            return;
        }
        DialogueBox.Show(speaker, new[] { greetings[Random.Range(0, greetings.Length)] }, HubOptions(), OnClosed);
    }

    private void OnClosed()
    {
        if (mode == Mode.Talk) EnterSit();
    }

    private List<DialogueOption> HubOptions()
    {
        var o = new List<DialogueOption>();
        o.Add(new DialogueOption("Got any tips for me?", GiveTip));
        o.Add(new DialogueOption("What do the villagers like?", () => { likesPage = 0; OpenLikesMenu(); }));
        o.Add(new DialogueOption("Give " + dogName + " a belly rub", Pet));
        if (AtHome || GameState.HasFlag(HomeFlag)) o.Add(new DialogueOption("Come with me, boy!", CallOut));
        else o.Add(new DialogueOption("Go back to your dog house", SendHome));
        o.Add(new DialogueOption("See you later", null, true));
        return o;
    }

    private void Hub(string line)
    {
        DialogueBox.Show(speaker, new[] { line }, HubOptions(), OnClosed);
    }

    private void GiveTip()
    {
        string tip = MeatballTips.FindTip(playerActions);
        var o = new List<DialogueOption>();
        o.Add(new DialogueOption("Thanks! Anything else?", () => Hub("Anything else? Just ask!")));
        o.Add(new DialogueOption("Got it, bye!", null, true));
        DialogueBox.Show(speaker, new[] { "Woof! Here's my tip:", tip }, o, OnClosed);
    }

    // ---- villager likes (one villager per day) ----

    private bool LikesUsedToday()
    {
        int today = TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 1;
        return GameState.GetCounter(LikesDayKey) == today;
    }

    private void OpenLikesMenu()
    {
        if (knownNpcs == null || knownNpcs.Length == 0)
        {
            var back = new List<DialogueOption> { new DialogueOption("Okay!", () => Hub("Anything else I can do?")) };
            DialogueBox.Show(speaker, new[] { "Hmm, I haven't met anyone around here yet. I'll keep my nose to the ground!" }, back, OnClosed);
            return;
        }

        int per = Mathf.Min(npcsPerPage, 6);
        int pages = Mathf.CeilToInt(knownNpcs.Length / (float)per);
        likesPage = Mathf.Clamp(likesPage, 0, pages - 1);

        var o = new List<DialogueOption>();
        for (int i = likesPage * per; i < Mathf.Min(knownNpcs.Length, (likesPage + 1) * per); i++)
        {
            NpcProfile npc = knownNpcs[i];
            if (npc == null) continue;
            o.Add(new DialogueOption(npc.npcName, () => TellLikes(npc)));
        }
        if (pages > 1) o.Add(new DialogueOption("More people...", () => { likesPage = (likesPage + 1) % pages; OpenLikesMenu(); }));
        o.Add(new DialogueOption("Never mind", () => Hub("Okay! What else?")));

        string intro = LikesUsedToday()
            ? "I already told you about someone today! Ask me about a different person tomorrow. (I'll repeat the same one though.)"
            : "I can tell you about ONE villager each day. Who do you want to know about?";
        DialogueBox.Show(speaker, new[] { intro }, o, OnClosed);
    }

    private void TellLikes(NpcProfile npc)
    {
        int today = TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 1;
        string todayKey = "meatball_likes_" + npc.npcName + "_" + today;

        var o = new List<DialogueOption>();
        o.Add(new DialogueOption("Thanks, Meatball!", () => Hub("Anytime! Gifts make friends.")));
        o.Add(new DialogueOption("Got it, bye!", null, true));

        if (LikesUsedToday() && !GameState.HasFlag(todayKey))
        {
            DialogueBox.Show(speaker, new[] { "Sorry! My nose is tired. I've already told you about someone today. Ask again tomorrow!" }, o, OnClosed);
            return;
        }

        GameState.SetCounter(LikesDayKey, today);
        GameState.SetFlag(todayKey);
        DialogueBox.Show(speaker, new[] { "Okay okay, I know this one! " + npc.Describe() }, o, OnClosed);
    }

    // ---- other options ----

    private void Pet()
    {
        DialogueBox.Show(speaker, new[] { "Ooh! Right there! *tail wagging so hard*" }, null, () => { StartRoll(); }, true);
    }

    private void SendHome()
    {
        if (DogHouse.Instance == null)
        {
            Hub("I'd love to, but there's no dog house on the farm yet!");
            return;
        }
        GameState.SetFlag(HomeFlag, true);
        DialogueBox.Show(speaker, new[] { "Okay! I'll be at my dog house if you need me. Just come say hi!" }, null, OnClosed);
    }

    private void CallOut()
    {
        GameState.SetFlag(HomeFlag, false);
        DialogueBox.Show(speaker, new[] { "Yay! Adventure time! Lead the way!" }, null, OnClosed, true);
    }
}
