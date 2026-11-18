using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Somebody who stands in one place in the city and talks: the scientist, the city clerk, the hot dog vendor, the plant foreman,
/// and the tower's workers (who only ever say one line).
///
///  - Lines: one is picked at random each time you talk to them.
///  - Service: drag a desk or shop here (ResearchDesk, ShopCounter, CityHallDesk, WasteDesk, Smelter, HardwareDisplay...) and a choice
///    with the Service Label appears in the conversation, which opens it. The desk still works on its own too.
///  - One Line Only: no choices at all, just the line (the tower's staff).
/// Put it on an object with a SpriteRenderer. The city builder makes all of these.
/// </summary>
public class CityPerson : MonoBehaviour, IInteractable
{
    [Header("Who")]
    public string displayName = "Citizen";
    public Color nameColor = new Color(0.25f, 0.4f, 0.55f);
    [Tooltip("Used to remember that you have met (flag npc_met_<id>). Empty = the Display Name.")]
    public string personId = "";

    [Header("Talking")]
    [TextArea] public string[] firstMeeting = new string[0];
    [TextArea, Tooltip("One of these is said each time (picked at random).")]
    public string[] lines = { "Hello." };
    [Tooltip("The tower's workers: just one line and no choices.")]
    public bool oneLineOnly = false;

    [Header("Service (optional)")]
    [Tooltip("A desk or shop this person runs. Must be something you can interact with (ResearchDesk, ShopCounter, CityHallDesk, WasteDesk, Smelter...).")]
    public MonoBehaviour service;
    public string serviceLabel = "Let's do business";

    [Header("Pictures (the setup menu fills these in)")]
    public Sprite portraitIdle;
    public Sprite portraitTalk, portraitHappy, portraitBlink;
    public Sprite dialoguePanel, portraitFrame;
    [Tooltip("Optional: frames played slowly while standing (shifting their weight, working...). Empty = stands still.")]
    public Sprite[] idleFrames;
    [Min(0.1f)] public float idleFramesPerSecond = 1.5f;

    private DialogueSpeaker speaker;
    private SpriteRenderer sr;
    private Transform player;
    private SpriteRenderer playerSr;
    private float offset;

    public string InteractPrompt { get { return "Talk to " + displayName; } }

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (GetComponent<Collider2D>() == null)
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.5f; c.offset = new Vector2(0f, 0.5f); c.isTrigger = true;
        }
        speaker = new DialogueSpeaker
        {
            speakerName = displayName, idle = portraitIdle, talk = portraitTalk, happy = portraitHappy, blink = portraitBlink,
            panel = dialoguePanel, frame = portraitFrame, nameColor = nameColor
        };
        offset = Random.value * 10f;
    }

    private void Update()
    {
        if (sr == null) return;
        if (idleFrames != null && idleFrames.Length > 1 && !DialogueBox.IsOpen)
            sr.sprite = idleFrames[Mathf.FloorToInt((Time.time + offset) * idleFramesPerSecond) % idleFrames.Length];

        // in front of the player when below them on the screen, behind when above (the same rule the other NPCs use)
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

    public void Interact(PlayerActions p)
    {
        if (DialogueBox.IsOpen) return;
        string id = string.IsNullOrEmpty(personId) ? displayName : personId;
        string metFlag = "npc_met_" + id;
        bool first = !GameState.HasFlag(metFlag) && firstMeeting != null && firstMeeting.Length > 0;
        GameState.SetFlag(metFlag);

        string[] pages = first ? firstMeeting : new[] { lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : "..." };
        DialogueBox.Show(speaker, pages, oneLineOnly ? null : Options(p), null, first);
    }

    private List<DialogueOption> Options(PlayerActions p)
    {
        var o = new List<DialogueOption>();
        var desk = service as IInteractable;
        if (desk != null) o.Add(new DialogueOption(serviceLabel, () => { DialogueBox.Close(); desk.Interact(p); }, true));
        o.Add(new DialogueOption("Goodbye", null, true));
        return o;
    }
}
