using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Victor Sterling: the man in the suit who owns the tower, and the villain of the story.
///
/// WHAT HE DOES
///   - Paces the lobby between his Pace Points. At each one he stops and puts on a little performance: flips a gold coin,
///     laughs, checks his watch or straightens his tie (the "acts").
///   - Talks to the player. What he says changes with the story:
///       the first meeting; then ordinary smug lines;
///       once the player has SEEN the back room (flag saw_tower_dumping) a new choice appears, and asking it makes him angry
///       (sets ceo_confronted and adds a quest to the journal);
///       if the player has dumped in the river themselves he treats them as a partner; if they have treated a lot of waste he sneers.
///   - He has eight faces instead of four: the usual idle / talk / happy / blink, plus smug, angry, laughing and scheming.
///
/// Everything he says is in the Inspector, so the story can be rewritten without touching code. The city builder sets him up.
/// </summary>
public class CeoNpc : MonoBehaviour, IInteractable
{
    [Header("Who")]
    public string displayName = "Victor Sterling";
    public Color nameColor = new Color(0.75f, 0.6f, 0.15f);

    [Header("Pacing the lobby")]
    [Tooltip("He walks from one of these to the next, in order, and back to the first.")]
    public Transform[] pacePoints = new Transform[0];
    [Min(0.1f)] public float walkSpeed = 1.6f;
    public float walkFps = 7f;
    [Tooltip("Seconds he stands at each point: least and most.")]
    public Vector2 pauseSeconds = new Vector2(3f, 6f);

    [Header("Walk pictures (each a row of frames, first frame = standing)")]
    public Sprite[] down;
    public Sprite[] up;
    public Sprite[] side;            // faces right; flipped for left

    [Header("Acts (played while he stands still; 4 frames each)")]
    public Sprite[] actCoin;
    public Sprite[] actLaugh;
    public Sprite[] actWatch;
    public Sprite[] actTie;
    [Min(1f)] public float actFps = 6f;

    [Header("Faces")]
    public Sprite portraitIdle;
    public Sprite portraitTalk, portraitHappy, portraitBlink;
    public Sprite portraitSmug, portraitAngry, portraitLaugh, portraitScheming;
    public Sprite dialoguePanel, portraitFrame;

    [Header("What he says")]
    [TextArea] public string[] firstMeeting =
    {
        "Well, well. Mud on the marble. You must be the one who took the old farm.",
        "Victor Sterling. I own this tower, most of this street, and a controlling interest in tomorrow.",
        "Seeds, steel, a new kitchen. Whatever you need, I sell it for five percent less than those quaint little shops back in the suburbs.",
        "Do tell them I said hello. While they are still open."
    };
    [TextArea] public string[] ordinaryLines =
    {
        "Time is money, farmer. And I have a great deal more of both than you.",
        "Frank? The hammer man? Charming. I bought his supplier last spring.",
        "Everything has a price. The trick is making sure somebody else pays it.",
        "You grow things. I grow NUMBERS. Mine never need watering."
    };
    [TextArea] public string[] aboutPrices =
    {
        "Five percent under the suburb, on everything, every day. Seeds on the left, smelting on the right, home upgrades by the lifts.",
        "How do I do it? Volume, my friend. And a few... efficiencies... in waste disposal."
    };
    [TextArea] public string[] confronted =
    {
        "The back room? You were in my back room.",
        "What you saw is a perfectly ordinary 'liquid asset relocation programme'. The river takes it away. That is what rivers are FOR.",
        "The forest? The farms downstream? Not on my balance sheet.",
        "Run along to City Hall if it troubles you. The clerk and I have an understanding. Had. Have."
    };
    [TextArea] public string[] afterConfronted =
    {
        "Still here? Security has your face now, farmer.",
        "Clean up my river and you clean out my margins. I will not forget it."
    };
    [TextArea] public string[] playerDumpsToo =
    {
        "I hear you have been tipping your own rubbish in the river. Cheaper, isn't it? I KNEW you were one of us.",
        "Every barrel you throw in is a barrel nobody can blame on me. Do keep it up."
    };
    [TextArea] public string[] playerCleansUp =
    {
        "Paying the treatment plant. With your own money. How noble. How poor.",
        "You cannot scrub a whole river with good intentions, farmer."
    };

    [Header("Story numbers")]
    [Min(1), Tooltip("Items the player must have thrown in the river before he calls them a partner.")]
    public int dumpedToBePartner = 5;
    [Min(1), Tooltip("Items the player must have taken to the treatment plant before he sneers about it.")]
    public int treatedToBeSneeredAt = 20;

    public const string SawDumpingFlag = "saw_tower_dumping";
    public const string ConfrontedFlag = "ceo_confronted";

    public string InteractPrompt { get { return "Talk to " + displayName; } }

    private enum Mode { Walking, Standing, Acting }
    private Mode mode = Mode.Standing;
    private SpriteRenderer sr;
    private Transform player;
    private SpriteRenderer playerSr;
    private int target;
    private float timer, animTime;
    private Vector2 facing = Vector2.down;
    private Sprite[] act;
    private DialogueSpeaker calm, smug, angry;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (GetComponent<Collider2D>() == null)
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.5f; c.offset = new Vector2(0f, 0.5f); c.isTrigger = true;
        }
        calm = Speaker(portraitIdle, portraitTalk, portraitLaugh != null ? portraitLaugh : portraitHappy);
        smug = Speaker(portraitSmug != null ? portraitSmug : portraitIdle, portraitScheming != null ? portraitScheming : portraitTalk, portraitLaugh != null ? portraitLaugh : portraitHappy);
        angry = Speaker(portraitAngry != null ? portraitAngry : portraitIdle, portraitAngry != null ? portraitAngry : portraitTalk, portraitAngry != null ? portraitAngry : portraitHappy);
        timer = Random.Range(pauseSeconds.x, pauseSeconds.y);
    }

    private DialogueSpeaker Speaker(Sprite idle, Sprite talk, Sprite happy)
    {
        return new DialogueSpeaker { speakerName = displayName, idle = idle, talk = talk, happy = happy, blink = portraitBlink, panel = dialoguePanel, frame = portraitFrame, nameColor = nameColor };
    }

    private void Update()
    {
        // he stands still while anybody is talking or a menu is open (the game clock is paused then)
        bool frozen = DialogueBox.IsOpen || (TimeManager.Instance != null && TimeManager.Instance.Paused);
        if (!frozen) Think();
        Draw();
    }

    private void Think()
    {
        switch (mode)
        {
            case Mode.Walking:
                if (pacePoints == null || pacePoints.Length == 0 || pacePoints[target] == null) { mode = Mode.Standing; break; }
                Vector2 to = (Vector2)pacePoints[target].position - (Vector2)transform.position;
                float step = walkSpeed * Time.deltaTime;
                if (to.magnitude <= step)
                {
                    transform.position = new Vector3(pacePoints[target].position.x, pacePoints[target].position.y, transform.position.z);
                    mode = Mode.Standing; facing = Vector2.down;
                    timer = Random.Range(pauseSeconds.x, pauseSeconds.y);
                }
                else
                {
                    facing = to.normalized;
                    transform.position += (Vector3)(facing * step);
                    animTime += Time.deltaTime;
                }
                break;

            case Mode.Standing:
                timer -= Time.deltaTime;
                if (timer > 0f) break;
                // half the time he performs before moving on
                act = Random.value < 0.6f ? PickAct() : null;
                if (act != null && act.Length > 0) { mode = Mode.Acting; animTime = 0f; timer = Random.Range(2f, 3.5f); }
                else NextPoint();
                break;

            case Mode.Acting:
                animTime += Time.deltaTime;
                timer -= Time.deltaTime;
                if (timer <= 0f) NextPoint();
                break;
        }
    }

    private Sprite[] PickAct()
    {
        var choices = new List<Sprite[]>();
        foreach (Sprite[] a in new[] { actCoin, actLaugh, actWatch, actTie }) if (a != null && a.Length > 0) choices.Add(a);
        return choices.Count == 0 ? null : choices[Random.Range(0, choices.Count)];
    }

    private void NextPoint()
    {
        act = null; animTime = 0f;
        if (pacePoints == null || pacePoints.Length < 2) { mode = Mode.Standing; timer = Random.Range(pauseSeconds.x, pauseSeconds.y); return; }
        target = (target + 1) % pacePoints.Length;
        mode = Mode.Walking;
    }

    private void Draw()
    {
        if (sr == null) return;
        if (mode == Mode.Acting && act != null && act.Length > 0)
        {
            sr.sprite = act[Mathf.FloorToInt(animTime * actFps) % act.Length];
            sr.flipX = false;
        }
        else
        {
            Sprite[] set; bool flip = false;
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y)) { set = side; flip = facing.x < 0f; }
            else set = facing.y > 0f ? up : down;
            if (set != null && set.Length > 0)
                sr.sprite = mode == Mode.Walking && set.Length > 1 ? set[1 + Mathf.FloorToInt(animTime * walkFps) % (set.Length - 1)] : set[0];
            sr.flipX = flip;
        }

        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) { player = pc.transform; playerSr = pc.GetComponentInChildren<SpriteRenderer>(); }
        }
        if (playerSr != null)
        {
            sr.sortingLayerID = playerSr.sortingLayerID;
            sr.sortingOrder = playerSr.sortingOrder + (transform.position.y < player.position.y - 0.05f ? 2 : -2);
        }
    }

    // ------------------------------------------------------------------ talking
    public void Interact(PlayerActions p)
    {
        if (DialogueBox.IsOpen) return;
        if (player != null) { Vector2 d = player.position - transform.position; if (d.sqrMagnitude > 0.01f) facing = d.normalized; }
        mode = Mode.Standing; act = null; timer = Random.Range(pauseSeconds.x, pauseSeconds.y);
        SterlingInfluence.NoteTalk();   // chatting with him (once a day) brings a little more litter to the farm

        if (!GameState.HasFlag("npc_met_sterling"))
        {
            GameState.SetFlag("npc_met_sterling");
            DialogueBox.Show(smug, firstMeeting, Options(p), null);
            return;
        }

        // which mood is he in?
        if (GameState.HasFlag(ConfrontedFlag)) { DialogueBox.Show(angry, new[] { Pick(afterConfronted) }, Options(p), null); return; }
        if (GameState.GetCounter(RiverPollution.DumpedKey) >= dumpedToBePartner && Random.value < 0.6f) { DialogueBox.Show(smug, new[] { Pick(playerDumpsToo) }, Options(p), null, true); return; }
        if (GameState.GetCounter(RiverPollution.TreatedKey) >= treatedToBeSneeredAt && Random.value < 0.6f) { DialogueBox.Show(smug, new[] { Pick(playerCleansUp) }, Options(p), null); return; }
        DialogueBox.Show(calm, new[] { Pick(ordinaryLines) }, Options(p), null);
    }

    private static string Pick(string[] lines) { return lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : "..."; }

    private List<DialogueOption> Options(PlayerActions p)
    {
        var o = new List<DialogueOption>();
        o.Add(new DialogueOption("How are you so cheap?", () => DialogueBox.Show(smug, aboutPrices, Options(p), null, true)));
        if (GameState.HasFlag(SawDumpingFlag) && !GameState.HasFlag(ConfrontedFlag))
            o.Add(new DialogueOption("What are you pouring into the river?", () =>
            {
                GameState.SetFlag(ConfrontedFlag);
                QuestLog.Add("sterling_river", "Something in the Water", "You",
                    "The Sterling tower is pouring its waste into the river. Victor Sterling does not care. Somebody at City Hall might.",
                    () => RiverPollution.Describe(RiverPollution.Level));
                DialogueBox.Show(angry, confronted, Options(p), null);
            }));
        o.Add(new DialogueOption("Goodbye", null, true));
        return o;
    }
}
