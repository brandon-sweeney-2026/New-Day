using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sick little fairy at the start of the maze (the grove Fairy's sister).
/// She tries to fly, struggles, drops to the ground, lies there for a while, then tries again. Talk to her with the action button:
///   "Ask for help"  - she senses the forest and tells you where to go next and what the current stump wants (needs a ForestGuide in the scene).
///   "What happened to you?" / "About your sister" - story.
/// All the lines are in the Inspector. The animation speeds are in the Inspector too.
/// The picture must be on a child object (the prefab does this) so only the picture moves, not the collider.
/// </summary>
public class SickFairyNpc : MonoBehaviour, IInteractable
{
    [Header("Who she is")]
    public string fairyName = "Little Fairy";

    [Header("Pictures (the setup menu fills these in)")]
    [Tooltip("Weak flapping: 2 pictures.")] public Sprite[] flapFrames;
    public Sprite fallFrame;
    [Tooltip("Lying on the ground: 2 pictures (breathing / coughing).")] public Sprite[] lieFrames;
    [Tooltip("Propped up while you talk to her.")] public Sprite sitFrame;
    public Sprite portraitIdle, portraitTalk, portraitHappy, portraitBlink;
    public Sprite dialoguePanel, portraitFrame;
    public Color nameColor = new Color(0.42f, 0.58f, 0.5f);
    public Color glowColor = new Color(0.65f, 0.85f, 0.75f, 0.22f);

    [Header("The struggle (all in seconds / tiles)")]
    [Min(0.5f)] public float lieSeconds = 4.5f;
    [Min(0.2f)] public float riseSeconds = 1.3f;
    [Min(0.2f)] public float struggleSeconds = 1.1f;
    public float riseHeight = 1.5f;
    [Tooltip("How fast she falls.")] public float gravity = 16f;
    [Tooltip("Height of the picture's centre above her feet when she is on the ground.")] public float groundOffset = 0.75f;

    [Header("Story")]
    [TextArea] public string[] firstMeeting =
    {
        "...Oh. Is someone there? Please, don't be afraid of me.",
        "I tried to fly out of the maze to find help. But the pollution is in the air here... my wings won't hold me. I keep falling.",
        "My sister keeps the grove at the far end of the maze. The shadows are getting worse and she cannot face them alone.",
        "Please... will you go to her? Will you help her?",
    };
    [TextArea] public string[] thanks = { "Thank you... thank you. I knew the forest would send someone.", "Talk to me if you get lost. I can't fly, but I can still feel the paths." };
    [TextArea] public string[] whatHappened =
    {
        "The dark seeped up from the farm and the mountain. It crept into the roots first, and then into us.",
        "My sister is stronger than I am. She stayed to hold the grove together. I was only supposed to fetch a helper...",
        "(She coughs weakly.) I'm all right. I just need to rest. Lots of resting.",
    };
    [TextArea] public string[] aboutSister =
    {
        "My sister is the keeper of the grove. She is taller than me, and much more serious, and she hums when she's worried.",
        "The grove is the only place the pollution has not reached. It's at the end of the maze, past the very last gate. Look for the fireflies.",
    };
    [TextArea] public string[] noGuide = { "I can feel the path, but it's tangled... Go deeper into the maze, through the gates. Every gate has a stump that holds its key." };

    public string InteractPrompt { get { return "Talk to " + fairyName; } }

    private enum Phase { Lying, Rising, Struggling, Falling }
    private Phase phase = Phase.Lying;
    private float phaseT;
    private float height, vy, rot, spinTarget;
    private int bounces;
    private bool wasTalking;
    private float sparkT;

    private SpriteRenderer sr, glow, shadow;
    private Transform pic, glowT, shadowT;
    private DialogueSpeaker speaker;
    private Transform player;

    private const string MetFlag = "sick_fairy_met";
    private const string PromisedFlag = "sick_fairy_promised";

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.gameObject == gameObject)
        {
            var g = new GameObject("Picture");
            g.transform.SetParent(transform, false);
            sr = g.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 12;
        }
        pic = sr.transform;

        if (GetComponent<Collider2D>() == null)
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 1.3f; c.isTrigger = true; c.offset = new Vector2(0f, 0.7f);
        }

        speaker = new DialogueSpeaker
        {
            speakerName = fairyName,
            idle = portraitIdle, talk = portraitTalk, happy = portraitHappy, blink = portraitBlink,
            panel = dialoguePanel, frame = portraitFrame, nameColor = nameColor,
        };

        // soft glow (follows the picture)
        var gl = new GameObject("Glow");
        gl.transform.SetParent(transform, false);
        gl.transform.localScale = Vector3.one * 2f;
        glow = gl.AddComponent<SpriteRenderer>();
        glow.sprite = RuntimeSprites.Circle;
        glow.color = glowColor;
        glow.sortingOrder = sr.sortingOrder - 1;
        glowT = gl.transform;
        gl.AddComponent<LightingExempt>();

        // a little shadow on the ground so you can see how high she is
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(transform, false);
        shadow = sh.AddComponent<SpriteRenderer>();
        shadow.sprite = RuntimeSprites.Circle;
        shadow.color = new Color(0f, 0f, 0f, 0.28f);
        shadow.sortingOrder = sr.sortingOrder - 2;
        shadowT = sh.transform;

        if (lieFrames != null && lieFrames.Length > 0) sr.sprite = lieFrames[0];
        phaseT = Random.value * lieSeconds * 0.5f;
    }

    // ------------------------------------------------------------------ animation
    private void Update()
    {
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        bool talking = DialogueBox.IsOpen && player != null && Vector2.Distance(player.position, transform.position) < 3.5f;
        float dt = Time.deltaTime;

        if (talking)
        {
            // she stops struggling and sits up
            height = Mathf.MoveTowards(height, 0f, 6f * dt);
            rot = Mathf.MoveTowards(rot, 0f, 240f * dt);
            if (height <= 0.02f && sitFrame != null) sr.sprite = sitFrame;
            else if (fallFrame != null) sr.sprite = fallFrame;
            Place(0f, 0f);
            wasTalking = true;
            return;
        }
        if (wasTalking) { wasTalking = false; phase = Phase.Lying; phaseT = 0f; height = 0f; vy = 0f; }

        phaseT += dt;
        float t = Time.time;
        float jitter = 0f, bob = 0f;

        switch (phase)
        {
            case Phase.Lying:
            {
                height = 0f; rot = Mathf.MoveTowards(rot, 0f, 300f * dt);
                if (lieFrames != null && lieFrames.Length > 0)
                    sr.sprite = lieFrames[(Mathf.FloorToInt(t * 0.55f) % 5 == 4 && lieFrames.Length > 1) ? 1 : 0];   // an occasional cough
                bob = Mathf.Sin(t * 2.2f) * 0.012f;
                if (phaseT > lieSeconds - 0.7f) jitter = Mathf.Sin(t * 45f) * 0.025f;                        // gathering her strength
                if (phaseT >= lieSeconds) Go(Phase.Rising);
                break;
            }
            case Phase.Rising:
            {
                float k = Mathf.Clamp01(phaseT / riseSeconds);
                height = riseHeight * (1f - (1f - k) * (1f - k));
                rot = Mathf.Sin(t * 9f) * 6f;
                jitter = Mathf.Sin(t * 40f) * 0.03f;
                Flap(10f);
                Sparks(0.12f);
                if (k >= 1f) Go(Phase.Struggling);
                break;
            }
            case Phase.Struggling:
            {
                float k = Mathf.Clamp01(phaseT / struggleSeconds);
                height = riseHeight + Mathf.Sin(phaseT * 7f) * 0.08f - phaseT * 0.3f;
                rot = Mathf.Sin(t * 8f) * Mathf.Lerp(6f, 24f, k);
                jitter = Mathf.Sin(t * 30f) * 0.04f;
                Flap(Mathf.Lerp(8f, 2.5f, k));                                                              // the flapping gives up
                Sparks(0.2f);
                if (k >= 1f) { vy = 0f; bounces = 0; spinTarget = Random.value < 0.5f ? -80f : 80f; Go(Phase.Falling); }
                break;
            }
            case Phase.Falling:
            {
                vy -= gravity * dt;
                height += vy * dt;
                if (fallFrame != null) sr.sprite = fallFrame;
                rot = Mathf.MoveTowards(rot, spinTarget, 260f * dt);
                jitter = Mathf.Sin(t * 25f) * 0.03f;
                Sparks(0.07f);
                if (height <= 0f)
                {
                    height = 0f;
                    Puff(transform.position + Vector3.up * 0.15f, 7, 1.6f);
                    if (bounces == 0 && vy < -2f) { vy = -vy * 0.28f; bounces++; }
                    else { vy = 0f; rot = 0f; Go(Phase.Lying); }
                }
                break;
            }
        }
        Place(jitter, bob);
    }

    private void Go(Phase p) { phase = p; phaseT = 0f; }

    private void Flap(float fps)
    {
        if (flapFrames == null || flapFrames.Length == 0) return;
        sr.sprite = flapFrames[Mathf.FloorToInt(Time.time * fps) % flapFrames.Length];
    }

    private void Place(float jitter, float bob)
    {
        pic.localPosition = new Vector3(jitter, groundOffset + height + bob, 0f);
        pic.localRotation = Quaternion.Euler(0f, 0f, rot);

        float air = Mathf.Clamp01(height / Mathf.Max(0.1f, riseHeight));
        shadowT.localPosition = new Vector3(0f, 0.12f, 0f);
        shadowT.localScale = new Vector3(Mathf.Lerp(1.1f, 0.55f, air), Mathf.Lerp(0.4f, 0.22f, air), 1f);
        glowT.localPosition = pic.localPosition;
        float flick = 0.75f + 0.25f * Mathf.Sin(Time.time * 6.5f);
        Color c = glowColor; c.a = glowColor.a * Mathf.Lerp(0.35f, 1f, air) * flick;
        glow.color = c;
    }

    // little dim motes falling off her while she struggles, and a puff of dust when she lands
    private void Sparks(float every)
    {
        sparkT -= Time.deltaTime;
        if (sparkT > 0f) return;
        sparkT = every;
        SickFairyMote.Spawn(pic.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.3f), 0f), new Vector2(Random.Range(-0.3f, 0.3f), -0.4f), 0.9f, 0.09f, new Color(0.75f, 0.95f, 0.85f, 0.8f), sr.sortingOrder - 1);
    }

    private void Puff(Vector3 at, int n, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Random.Range(0f, Mathf.PI);
            SickFairyMote.Spawn(at, new Vector2(Mathf.Cos(a) * speed * Random.Range(0.4f, 1f), Mathf.Sin(a) * speed * 0.5f), 0.5f, Random.Range(0.12f, 0.2f), new Color(0.85f, 0.9f, 0.8f, 0.6f), sr.sortingOrder + 1);
        }
    }

    // ------------------------------------------------------------------ talking
    public void Interact(PlayerActions p)
    {
        if (DialogueBox.IsOpen) return;
        if (!GameState.HasFlag(MetFlag))
        {
            GameState.SetFlag(MetFlag);
            var o = new List<DialogueOption>
            {
                new DialogueOption("I'll help your sister.", () => Promise(p)),
                new DialogueOption("Tell me more.", () => DialogueBox.Show(speaker, aboutSister, FirstChoice(p), null)),
                new DialogueOption("Not right now.", () => Hub(p, "...I understand. I'll be here. I'm not going anywhere. (She tries to smile.)")),
            };
            DialogueBox.Show(speaker, firstMeeting, o, null);
            return;
        }
        Hub(p, null);
    }

    private List<DialogueOption> FirstChoice(PlayerActions p)
    {
        return new List<DialogueOption>
        {
            new DialogueOption("I'll help your sister.", () => Promise(p)),
            new DialogueOption("Not right now.", () => Hub(p, "...I understand. I'll be here.")),
        };
    }

    private void Promise(PlayerActions p)
    {
        GameState.SetFlag(PromisedFlag);
        GameEvents.ShowToast("Find the Fairy's sister in the grove at the end of the maze.");
        var o = new List<DialogueOption>
        {
            new DialogueOption("Ask for help", () => Guidance(p)),
            new DialogueOption("Goodbye", null, true),
        };
        DialogueBox.Show(speaker, thanks, o, null, true);
    }

    private string Greeting()
    {
        if (GameState.HasFlag("forest_grove_reached")) return "You found my sister? I can feel the grove breathing again. Please, keep helping her.";
        if (GameState.HasFlag(PromisedFlag)) return "Please... she needs you. How can I help?";
        return "...You came back. Is there something you need?";
    }

    private void Hub(PlayerActions p, string line)
    {
        var o = new List<DialogueOption>
        {
            new DialogueOption("Ask for help", () => Guidance(p)),
            new DialogueOption("What happened to you?", () => DialogueBox.Show(speaker, whatHappened, BackOptions(p), null)),
            new DialogueOption("About your sister", () => DialogueBox.Show(speaker, aboutSister, BackOptions(p), null)),
            new DialogueOption("Goodbye", null, true),
        };
        DialogueBox.Show(speaker, new[] { line ?? Greeting() }, o, null);
    }

    private List<DialogueOption> BackOptions(PlayerActions p)
    {
        return new List<DialogueOption> { new DialogueOption("Anything else?", () => Hub(p, "Yes?")), new DialogueOption("Goodbye", null, true) };
    }

    /// <summary>"Ask for help": where to go next and what the current stump wants.</summary>
    private void Guidance(PlayerActions p)
    {
        ForestGuide guide = FindAnyObjectByType<ForestGuide>();
        string[] pages = guide != null && p != null ? guide.Describe(p.transform.position) : noGuide;
        DialogueBox.Show(speaker, pages, BackOptions(p), null);
    }
}

/// <summary>A tiny glowing speck that drifts and fades. Used for the fairy's sparks and dust.</summary>
public class SickFairyMote : MonoBehaviour
{
    private Vector2 vel; private float life, age, size; private Color color; private SpriteRenderer sr;

    public static void Spawn(Vector3 pos, Vector2 velocity, float lifetime, float size, Color color, int order)
    {
        var go = new GameObject("Mote");
        go.transform.position = pos;
        var m = go.AddComponent<SickFairyMote>();
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
        transform.localScale = Vector3.one * size * (1f + k * 0.8f);
        var c = color; c.a = color.a * (1f - k);
        sr.color = c;
    }
}
