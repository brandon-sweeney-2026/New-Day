using System.Collections.Generic;
using UnityEngine;

/// <summary>One job the Fairy gives. Fill these in the Inspector (the setup menu adds a few to start with).</summary>
[System.Serializable]
public class FairyQuest
{
    public enum Kind { GiveItems, ReachCounter }

    [Tooltip("Unique short name, e.g. clean_water. Used to remember progress.")] public string id = "quest";
    public string title = "A Little Help";
    public Kind kind = Kind.GiveItems;
    [TextArea] public string[] offerPages = { "Could you help me?" };
    [TextArea] public string reminder = "I'm still waiting...";
    [TextArea] public string[] completePages = { "Thank you!" };

    [Header("GiveItems: the fairy takes these")]
    public ForestRequirement[] items;

    [Header("ReachCounter: a number the game already counts")]
    [Tooltip("For example shadows_banished, fish_caught, killed_Bug. Any GameState counter works.")]
    public string counterKey = "shadows_banished";
    [Min(1)] public int counterTarget = 10;
    [Tooltip("Counts only from the moment the quest is accepted.")] public bool countFromAccept = true;
    public string counterLabel = "Shadows banished";

    [Header("Reward")]
    public int rewardGold = 0;
    public ForestRequirement[] rewardItems;
    [Tooltip("A flag set when this quest is finished - use it to unlock things later.")] public string setFlag = "";
}

/// <summary>
/// The Fairy in the grove. Talk to her with the action button. She gives quests one after another, all about cleaning up the pollution.
/// Progress is saved (GameState counters). Edit her lines and quests in the Inspector.
/// Needs a trigger Collider2D (added automatically). The picture bobs gently; the glow is automatic.
/// </summary>
public class FairyNpc : MonoBehaviour, IInteractable
{
    [Header("Who she is")]
    public string fairyName = "Fairy";
    public Sprite[] flyFrames;
    public float flyFps = 6f;
    public float bobHeight = 0.18f;
    public float bobSpeed = 2f;
    public Color glowColor = new Color(0.7f, 1f, 0.8f, 0.28f);

    [Header("Dialogue art (the setup menu fills these in)")]
    public Sprite portraitIdle, portraitTalk, portraitHappy, portraitBlink;
    public Sprite dialoguePanel, portraitFrame;
    public Color nameColor = new Color(0.2f, 0.55f, 0.35f);

    [Header("Story")]
    [TextArea] public string[] firstMeeting =
    {
        "Oh! A visitor... and you don't smell of shadow. Hello, little farmer.",
        "I am the keeper of this grove. The pollution crept in slowly, and now it chokes the forest paths. Everything here is wilting.",
        "If you would help me, perhaps the forest can breathe again.",
    };
    [Tooltip("Said instead of the first line when the player has already met her sick sister at the start of the maze.")]
    [TextArea] public string[] fromSisterPages =
    {
        "Oh... you have spoken to my little sister? I could feel her worry from here. You came all this way for her. Thank you.",
    };
    [TextArea] public string[] aboutPollution =
    {
        "The dark stuff seeps up from the farm and the mountain, carried by water and wind. The animals were the first to change.",
        "Clean it away at the source, and the shadows will let them go. I think. I hope.",
    };
    [TextArea] public string[] allDone = { "The forest is breathing easier because of you. Come and sit by the pond whenever you like." };

    [Header("Quests (done in order)")]
    public FairyQuest[] quests;

    public string InteractPrompt { get { return "Talk to " + fairyName; } }

    private DialogueSpeaker speaker;
    private SpriteRenderer sr;
    private Vector3 home;
    private float t;

    private string MetFlag { get { return "fairy_met"; } }
    private static string State(string id) { return "fairy_q_" + id; }          // 0 new, 1 accepted, 2 done

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (GetComponent<Collider2D>() == null)
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 1f; c.isTrigger = true;
        }
        speaker = new DialogueSpeaker
        {
            speakerName = fairyName,
            idle = portraitIdle, talk = portraitTalk, happy = portraitHappy, blink = portraitBlink,
            panel = dialoguePanel, frame = portraitFrame, nameColor = nameColor,
        };
        home = transform.position;

        // soft glow
        var g = new GameObject("Glow");
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        g.transform.localScale = Vector3.one * 2.4f;
        var gs = g.AddComponent<SpriteRenderer>();
        gs.sprite = RuntimeSprites.Circle;
        gs.color = glowColor;
        if (sr != null) { gs.sortingLayerID = sr.sortingLayerID; gs.sortingOrder = sr.sortingOrder - 1; }
        g.AddComponent<LightingExempt>();
    }

    private void Update()
    {
        t += Time.deltaTime;
        if (sr != null && !DialogueBox.IsOpen)
        {
            if (flyFrames != null && flyFrames.Length > 0) sr.sprite = flyFrames[Mathf.FloorToInt(t * flyFps) % flyFrames.Length];
        }
    }

    private void LateUpdate()
    {
        // bob the picture (not the collider). Put the picture on a child object so only it bobs.
        if (sr != null && sr.transform != transform) sr.transform.localPosition = new Vector3(0f, Mathf.Sin(t * bobSpeed) * bobHeight, 0f);
    }

    // ------------------------------------------------------------------ talking
    public void Interact(PlayerActions p)
    {
        if (DialogueBox.IsOpen) return;
        if (!GameState.HasFlag(MetFlag))
        {
            GameState.SetFlag(MetFlag);
            string[] pages = firstMeeting;
            if (GameState.HasFlag("sick_fairy_met") && fromSisterPages != null && fromSisterPages.Length > 0)
            {
                var list = new List<string>(fromSisterPages);
                for (int i = 1; i < firstMeeting.Length; i++) list.Add(firstMeeting[i]);   // skip the "a visitor!" line
                pages = list.ToArray();
            }
            DialogueBox.Show(speaker, pages, null, () => Hub(p, null), true);
            return;
        }
        Hub(p, null);
    }

    private FairyQuest CurrentQuest()
    {
        if (quests == null) return null;
        foreach (FairyQuest q in quests)
            if (q != null && GameState.GetCounter(State(q.id)) < 2) return q;
        return null;
    }

    private void Hub(PlayerActions p, string line)
    {
        FairyQuest q = CurrentQuest();
        var o = new List<DialogueOption>();
        string[] pages;

        if (q == null)
        {
            pages = new[] { line ?? (allDone != null && allDone.Length > 0 ? allDone[0] : "The grove is quiet today.") };
        }
        else if (GameState.GetCounter(State(q.id)) == 0)
        {
            pages = new[] { line ?? "I have something to ask of you, if you have the time." };
            o.Add(new DialogueOption("What do you need?", () => Offer(p, q)));
        }
        else
        {
            pages = new[] { line ?? ("Any news about \"" + q.title + "\"?") };
            o.Add(new DialogueOption("About \"" + q.title + "\"", () => Progress(p, q)));
        }
        o.Add(new DialogueOption("Tell me about the pollution", () => DialogueBox.Show(speaker, aboutPollution, BackOptions(p), null)));
        o.Add(new DialogueOption("Goodbye", null, true));
        DialogueBox.Show(speaker, pages, o, null);
    }

    private List<DialogueOption> BackOptions(PlayerActions p)
    {
        return new List<DialogueOption> { new DialogueOption("Anything else?", () => Hub(p, "Yes?")), new DialogueOption("Goodbye", null, true) };
    }

    private void Offer(PlayerActions p, FairyQuest q)
    {
        var o = new List<DialogueOption>
        {
            new DialogueOption("I'll do it.", () =>
            {
                GameState.SetCounter(State(q.id), 1);
                if (q.kind == FairyQuest.Kind.ReachCounter && q.countFromAccept) GameState.SetCounter("fairy_base_" + q.id, GameState.GetCounter(q.counterKey));
                GameEvents.ShowToast("Quest: " + q.title);
                Hub(p, "Thank you. Come back when it's done.");
            }),
            new DialogueOption("Maybe later.", () => Hub(p, "Of course. The forest will wait. A little longer.")),
        };
        DialogueBox.Show(speaker, q.offerPages, o, null);
    }

    private int CounterProgress(FairyQuest q)
    {
        int v = GameState.GetCounter(q.counterKey);
        if (q.countFromAccept) v -= GameState.GetCounter("fairy_base_" + q.id);
        return Mathf.Max(0, v);
    }

    private string ProgressText(FairyQuest q, Inventory inv)
    {
        if (q.kind == FairyQuest.Kind.ReachCounter)
            return q.counterLabel + ": " + Mathf.Min(CounterProgress(q), q.counterTarget) + " / " + q.counterTarget;
        var sb = new System.Text.StringBuilder();
        if (q.items != null)
            foreach (ForestRequirement r in q.items)
                if (r != null && r.item != null)
                    sb.Append(r.item.itemName).Append(": ").Append(inv != null ? Mathf.Min(inv.CountOf(r.item), r.count) : 0).Append(" / ").Append(r.count).Append("   ");
        return sb.ToString();
    }

    private bool IsMet(FairyQuest q, Inventory inv)
    {
        if (q.kind == FairyQuest.Kind.ReachCounter) return CounterProgress(q) >= q.counterTarget;
        if (q.items == null || inv == null) return true;
        foreach (ForestRequirement r in q.items)
            if (r != null && r.item != null && inv.CountOf(r.item) < r.count) return false;
        return true;
    }

    private void Progress(PlayerActions p, FairyQuest q)
    {
        Inventory inv = p != null ? p.Inventory : Inventory.Instance;
        if (!IsMet(q, inv))
        {
            Hub(p, q.reminder + "\n(" + ProgressText(q, inv) + ")");
            return;
        }
        Complete(p, q, inv);
    }

    private void Complete(PlayerActions p, FairyQuest q, Inventory inv)
    {
        if (q.kind == FairyQuest.Kind.GiveItems && q.items != null && inv != null)
            foreach (ForestRequirement r in q.items) if (r != null && r.item != null) inv.Remove(r.item, r.count);

        GameState.SetCounter(State(q.id), 2);
        GameState.AddCounter("fairy_quests_done");
        if (!string.IsNullOrEmpty(q.setFlag)) GameState.SetFlag(q.setFlag);
        if (q.rewardGold > 0) GameState.AddGold(q.rewardGold);
        if (q.rewardItems != null && inv != null)
            foreach (ForestRequirement r in q.rewardItems)
                if (r != null && r.item != null)
                {
                    int left = inv.Add(r.item, r.count);
                    if (left > 0 && p != null) ItemPickup.Spawn(r.item, left, p.transform.position + Vector3.down * 0.5f);
                }
        GameEvents.ShowToast("Quest complete: " + q.title + (q.rewardGold > 0 ? "  (+" + q.rewardGold + " gold)" : ""));

        var o = new List<DialogueOption> { new DialogueOption("Anything else?", () => Hub(p, "Yes... there may be more to do.")), new DialogueOption("Goodbye", null, true) };
        DialogueBox.Show(speaker, q.completePages, o, null, true);
    }
}
