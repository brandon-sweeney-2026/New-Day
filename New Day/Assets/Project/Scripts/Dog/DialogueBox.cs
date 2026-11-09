using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Who is talking: a name, a close-up portrait (a few expressions) and the box art.</summary>
[Serializable]
public class DialogueSpeaker
{
    public string speakerName = "???";
    public Sprite idle, talk, happy, blink;
    public Sprite panel, frame;                       // optional box art (9-sliced)
    public Color nameColor = new Color(0.62f, 0.32f, 0.1f);
}

/// <summary>One choice in a dialogue. If 'closes' is true (or onPick opens nothing new) the box closes.</summary>
public class DialogueOption
{
    public string label;
    public Action onPick;
    public bool closes;
    public DialogueOption(string label, Action onPick = null, bool closes = false)
    {
        this.label = label; this.onPick = onPick; this.closes = closes;
    }
}

/// <summary>
/// The talking box at the bottom of the screen: a close-up portrait, the speaker's name, typewriter text and
/// (after the last page) a list of choices. Built in code, nothing to set up. Reusable for every NPC.
///
///   DialogueBox.Show(speaker, new[] { "Hello!", "Nice day." }, options, onClosed);
///
/// Keys: Space / Enter / E / J / left click = next page (or finish typing);  Up/Down (W/S) = choose;
///       Space etc. = pick;  Esc / Q / Backspace = close.  The player can't move and the clock pauses while it is open.
/// A choice's onPick may call Show again to continue the conversation; otherwise the box closes.
/// </summary>
public class DialogueBox : MonoBehaviour
{
    private const int MaxOptions = 7;
    private const float CharsPerSecond = 52f;

    private static DialogueBox instance;

    public static bool IsOpen { get { return instance != null && instance.canvas != null && instance.canvas.enabled; } }

    public static void Show(DialogueSpeaker who, string[] pages, List<DialogueOption> options = null, Action onClosed = null, bool happy = false)
    {
        if (instance == null)
        {
            var go = new GameObject("DialogueBox");
            instance = go.AddComponent<DialogueBox>();
            instance.BuildUI();
        }
        instance.Open(who, pages, options, onClosed, happy);
    }

    public static void Close()
    {
        if (instance != null) instance.CloseBox();
    }

    // ---- state ----
    private DialogueSpeaker speaker;
    private string[] pages;
    private List<DialogueOption> options;
    private Action onClosed;
    private bool happy;
    private int pageIndex, optionIndex, version;
    private float shown;
    private bool typing, locked;
    private int ignoreUntilFrame;
    private float portraitTimer, blinkTimer = 3f, blinkUntil;
    private Vector2 lastMouse;

    // ---- ui ----
    private Canvas canvas;
    private Font font;
    private Image panelImg, frameImg, portraitImg, choiceImg;
    private Text nameText, bodyText, hintText;
    private readonly Image[] optionBg = new Image[MaxOptions];
    private readonly Text[] optionText = new Text[MaxOptions];

    // ------------------------------------------------------------------
    // Open / close
    // ------------------------------------------------------------------

    private void Open(DialogueSpeaker who, string[] newPages, List<DialogueOption> newOptions, Action closedCallback, bool isHappy)
    {
        version++;
        speaker = who;
        pages = (newPages == null || newPages.Length == 0) ? new[] { "..." } : newPages;
        options = newOptions;
        onClosed = closedCallback;
        happy = isHappy;
        pageIndex = 0;
        optionIndex = 0;
        StartPage();
        ApplyArt();

        if (!locked)
        {
            locked = true;
            GameState.LockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Pause();
            ignoreUntilFrame = Time.frameCount + 1;          // the press that opened this must not also skip the first line
        }
        else ignoreUntilFrame = Time.frameCount + 1;
        canvas.enabled = true;
        if (Mouse.current != null) lastMouse = Mouse.current.position.ReadValue();
        RefreshOptions();
    }

    private void CloseBox()
    {
        if (!canvas.enabled) return;
        canvas.enabled = false;
        if (locked)
        {
            locked = false;
            GameState.UnlockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        }
        Action cb = onClosed;
        onClosed = null;
        if (cb != null) cb();
    }

    private void OnDestroy()
    {
        if (locked)
        {
            GameState.UnlockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        }
        if (instance == this) instance = null;
    }

    private void StartPage()
    {
        shown = 0f;
        typing = true;
        bodyText.text = "";
    }

    // ------------------------------------------------------------------
    // Update
    // ------------------------------------------------------------------

    private bool OptionsVisible
    {
        get { return !typing && pageIndex >= pages.Length - 1 && options != null && options.Count > 0; }
    }

    private void Update()
    {
        if (!canvas.enabled) return;

        AnimatePortrait();

        if (typing)
        {
            string page = pages[pageIndex];
            shown += CharsPerSecond * Time.unscaledDeltaTime;
            int n = Mathf.Min(page.Length, Mathf.FloorToInt(shown));
            bodyText.text = page.Substring(0, n);
            if (n >= page.Length) { typing = false; RefreshOptions(); }
        }

        if (Time.frameCount < ignoreUntilFrame) return;

        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Mouse mouse = Mouse.current;

        if (Pressed(kb, Key.Escape, Key.Backspace) || (kb != null && kb[Key.Q].wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            CloseBox();
            return;
        }

        bool confirm = Pressed(kb, Key.Space, Key.Enter) || Pressed(kb, Key.E, Key.J) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

        if (OptionsVisible)
        {
            int count = Mathf.Min(options.Count, MaxOptions);
            bool up = Pressed(kb, Key.UpArrow, Key.W) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = Pressed(kb, Key.DownArrow, Key.S) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            if (up) optionIndex = (optionIndex + count - 1) % count;
            if (down) optionIndex = (optionIndex + 1) % count;

            int hovered = -1;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                for (int i = 0; i < count; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(optionBg[i].rectTransform, mp, null)) { hovered = i; break; }
                if (hovered >= 0 && (mp - lastMouse).sqrMagnitude > 0.5f) optionIndex = hovered;
                lastMouse = mp;
            }

            if (up || down || hovered >= 0) RefreshOptions();

            if (confirm || (click && hovered >= 0))
            {
                if (click && hovered >= 0) optionIndex = hovered;
                Pick(optionIndex);
            }
            return;
        }

        if (confirm || click)
        {
            if (typing)
            {
                shown = pages[pageIndex].Length;       // finish the line instantly
            }
            else if (pageIndex < pages.Length - 1)
            {
                pageIndex++;
                StartPage();
                RefreshOptions();
            }
            else CloseBox();                            // last page and no choices
        }
    }

    private void Pick(int index)
    {
        if (options == null || index < 0 || index >= options.Count) return;
        DialogueOption o = options[index];
        if (o.closes) { CloseBox(); return; }

        int v = version;
        if (o.onPick != null) o.onPick();
        if (version == v) CloseBox();                  // the choice didn't continue the conversation
    }

    private static bool Pressed(Keyboard kb, Key a, Key b)
    {
        return kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame);
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void AnimatePortrait()
    {
        if (speaker == null) return;
        Sprite s = speaker.idle;
        if (typing)
        {
            portraitTimer += Time.unscaledDeltaTime;
            bool open = ((int)(portraitTimer / 0.12f)) % 2 == 1;
            if (open && speaker.talk != null) s = speaker.talk;
        }
        else
        {
            portraitTimer = 0f;
            if (happy && speaker.happy != null) s = speaker.happy;
            else
            {
                blinkTimer -= Time.unscaledDeltaTime;
                if (blinkTimer <= 0f) { blinkUntil = Time.unscaledTime + 0.14f; blinkTimer = UnityEngine.Random.Range(2.5f, 5f); }
                if (Time.unscaledTime < blinkUntil && speaker.blink != null) s = speaker.blink;
            }
        }
        if (s != null) portraitImg.sprite = s;
    }

    private void ApplyArt()
    {
        nameText.text = speaker.speakerName;
        nameText.color = speaker.nameColor;

        if (speaker.panel != null) { panelImg.sprite = speaker.panel; panelImg.type = Image.Type.Sliced; panelImg.color = Color.white; choiceImg.sprite = speaker.panel; choiceImg.type = Image.Type.Sliced; choiceImg.color = Color.white; }
        else { panelImg.sprite = null; panelImg.color = new Color(0.96f, 0.9f, 0.76f, 0.97f); choiceImg.sprite = null; choiceImg.color = new Color(0.96f, 0.9f, 0.76f, 0.97f); }

        if (speaker.frame != null) { frameImg.sprite = speaker.frame; frameImg.type = Image.Type.Sliced; frameImg.color = Color.white; }
        else { frameImg.sprite = null; frameImg.color = new Color(0.6f, 0.45f, 0.25f, 1f); }

        portraitImg.sprite = speaker.idle;
        portraitImg.enabled = speaker.idle != null;
    }

    private void RefreshOptions()
    {
        bool show = OptionsVisible;
        choiceImg.gameObject.SetActive(show);
        hintText.text = show ? "W/S: choose     Space: pick     Esc: close" : (typing ? "" : (pageIndex < pages.Length - 1 ? "Space  >>" : "Space: close"));
        if (!show) return;

        int count = Mathf.Min(options.Count, MaxOptions);
        float rowH = 40f;
        choiceImg.rectTransform.sizeDelta = new Vector2(430f, 24f + count * rowH);
        for (int i = 0; i < MaxOptions; i++)
        {
            bool on = i < count;
            optionBg[i].gameObject.SetActive(on);
            if (!on) continue;
            RectTransform rt = optionBg[i].rectTransform;
            rt.anchoredPosition = new Vector2(0f, -12f - i * rowH);
            bool sel = i == optionIndex;
            optionBg[i].color = sel ? new Color(0.96f, 0.78f, 0.4f, 1f) : new Color(1f, 1f, 1f, 0f);
            optionText[i].text = (sel ? "> " : "   ") + options[i].label;
            optionText[i].color = new Color(0.25f, 0.15f, 0.1f);
        }
    }

    // ------------------------------------------------------------------
    // Building the box
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        // main box, bottom centre
        panelImg = Img(root, "Panel", Color.white);
        Place(panelImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1040f, 210f));
        RectTransform panel = panelImg.rectTransform;

        frameImg = Img(panel, "Portrait Frame", Color.white);
        Place(frameImg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(160f, 160f));
        portraitImg = Img(frameImg.rectTransform, "Portrait", Color.white);
        Place(portraitImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128f, 128f));
        portraitImg.preserveAspect = true;

        nameText = Txt(panel, "Name", 28, TextAnchor.UpperLeft);
        nameText.fontStyle = FontStyle.Bold;
        Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -16f), new Vector2(500f, 36f));

        bodyText = Txt(panel, "Body", 26, TextAnchor.UpperLeft);
        bodyText.color = new Color(0.25f, 0.15f, 0.1f);
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Truncate;
        bodyText.lineSpacing = 1.05f;
        Place(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -56f), new Vector2(800f, 130f));

        hintText = Txt(panel, "Hint", 16, TextAnchor.LowerRight);
        hintText.color = new Color(0.45f, 0.32f, 0.22f);
        Place(hintText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 14f), new Vector2(400f, 24f));

        // choice box, floating above the main box on the right
        choiceImg = Img(root, "Choices", Color.white);
        Place(choiceImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(520f, 234f), new Vector2(430f, 200f));
        for (int i = 0; i < MaxOptions; i++)
        {
            optionBg[i] = Img(choiceImg.rectTransform, "Option " + i, Color.clear);
            Place(optionBg[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 38f));
            optionText[i] = Txt(optionBg[i].rectTransform, "Label", 24, TextAnchor.MiddleLeft);
            optionText[i].rectTransform.anchorMin = Vector2.zero;
            optionText[i].rectTransform.anchorMax = Vector2.one;
            optionText[i].rectTransform.offsetMin = new Vector2(10f, 0f);
            optionText[i].rectTransform.offsetMax = new Vector2(-6f, 0f);
        }
        choiceImg.gameObject.SetActive(false);
        canvas.enabled = false;
    }

    private static Image Img(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Text Txt(RectTransform parent, string name, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = true;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
}
