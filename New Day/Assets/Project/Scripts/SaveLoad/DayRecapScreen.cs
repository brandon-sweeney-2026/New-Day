using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The "End of Day" screen shown every morning after you sleep or pass out:
/// the experience you earned in each skill, everything the mailbox shipped, the total, and whether the game was saved.
///
/// DayRecap.FinishDay opens it. Press Space / Enter / E / click (gamepad A) to start the new day.
/// While it is open the player can't move and the clock is stopped.
///
/// Built by code, so there is nothing to set up. To change its look, add this component to an object in a scene
/// and change the numbers in the Inspector (that copy is then used in that scene).
/// </summary>
public class DayRecapScreen : MonoBehaviour
{
    [Header("Closing")]
    [Tooltip("Seconds before a key press can close the screen, so the button you went to bed with does not skip it straight away.")]
    [Min(0f)] [SerializeField] private float minSecondsOpen = 0.6f;

    [Header("Size")]
    [Tooltip("Most shipped items listed one by one. The rest are summed up in one line (the total still counts them).")]
    [Min(1)] [SerializeField] private int maxShippedLines = 8;
    [Tooltip("Width of the writing on each page of the open book. The book grows taller to fit the lines.")]
    [Min(200f)] [SerializeField] private float pageWidth = 380f;

    // The screen is an open journal drawn by the Book Skin (Assets/Resources/BookSkin.asset):
    // the day and your experience on the left page, what was shipped and the total on the right page.
    // The colours below are turned into matching dark ink (BookSkin.Ink) so they read on paper.
    [Header("Colours")]
    [SerializeField] private Color headingColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private Color textColor = new Color(0.95f, 0.92f, 0.85f);
    [SerializeField] private Color quietColor = new Color(0.65f, 0.62f, 0.56f);
    [SerializeField] private Color xpColor = new Color(0.55f, 0.85f, 1f);
    [SerializeField] private Color levelUpColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private Color goldColor = new Color(0.3f, 0.95f, 0.4f);
    [SerializeField] private Color warningColor = new Color(1f, 0.55f, 0.45f);

    private static DayRecapScreen instance;

    private Canvas canvas;
    private BookSkin skin;
    private BookSkin.Book book;
    private RectTransform leftWriting, rightWriting;     // tonight's lines go in here (thrown away and rewritten every night)
    private RectTransform panel;        // the page currently being written on (left or right)
    private float y;                    // where the next line goes, measured down from the top of that page
    private float openedAt;
    private bool locked;

    /// <summary>True while the recap is on screen.</summary>
    public static bool IsOpen { get { return instance != null && instance.canvas != null && instance.canvas.enabled; } }

    /// <summary>Shows the recap for the day that just ended.</summary>
    public static void Show(DayRecap.Summary summary)
    {
        if (summary == null) return;
        if (instance == null) instance = FindAnyObjectByType<DayRecapScreen>();
        if (instance == null) instance = new GameObject("DayRecapScreen").AddComponent<DayRecapScreen>();
        instance.Open(summary);
    }

    private void Awake()
    {
        if (instance == null) instance = this;
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

    // ------------------------------------------------------------------
    // Opening and closing
    // ------------------------------------------------------------------

    private void Open(DayRecap.Summary s)
    {
        if (canvas == null) BuildCanvas();
        Fill(s);

        openedAt = Time.unscaledTime;
        if (!locked)
        {
            locked = true;
            GameState.LockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Pause();
        }
        canvas.enabled = true;
    }

    private void Close()
    {
        canvas.enabled = false;
        if (locked)
        {
            locked = false;
            GameState.UnlockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        }
    }

    private void Update()
    {
        if (canvas == null || !canvas.enabled) return;
        if (Time.unscaledTime < openedAt + minSecondsOpen) return;

        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Mouse mouse = Mouse.current;

        bool go = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                                  || kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
               || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame))
               || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (go) Close();
    }

    // ------------------------------------------------------------------
    // Filling in the panel
    // ------------------------------------------------------------------

    private void Fill(DayRecap.Summary s)
    {
        // Throw away last night's lines and write tonight's.
        for (int i = leftWriting.childCount - 1; i >= 0; i--) Destroy(leftWriting.GetChild(i).gameObject);
        for (int i = rightWriting.childCount - 1; i >= 0; i--) Destroy(rightWriting.GetChild(i).gameObject);

        Color heading = skin.Ink(headingColor), text = skin.Ink(textColor), quiet = skin.Ink(quietColor);
        Color xpInk = skin.Ink(xpColor), levelInk = skin.Ink(levelUpColor), goldInk = skin.Ink(goldColor), warnInk = skin.Ink(warningColor);

        // ================= left page: the day and the experience =================
        panel = leftWriting;
        y = 0f;
        skin.NewBanner(panel, "End Of Day", pageWidth * 0.8f, 0f, 22).text = "End of Day";
        y += skin.NaturalHeight(BookSkin.Part.Banner, 40f) + 6f;
        Line(s.date, 20, text, TextAnchor.MiddleCenter, 26f);
        if (s.passedOut) Line("You didn't make it to bed, so you woke up tired.", 16, warnInk, TextAnchor.MiddleCenter, 40f);
        y += 10f;

        Heading("Experience", heading);
        if (s.xp.Count == 0) Line("No experience earned today.", 17, quiet, TextAnchor.MiddleLeft, 24f);
        foreach (DayRecap.XpLine x in s.xp)
        {
            TwoSided(null, x.skill, "+" + x.gained.ToString("N0") + " XP", text, xpInk);
            if (x.levelAfter > x.levelBefore)
                Line("     Level up!  " + x.skill + " is now level " + x.levelAfter + ".", 16, levelInk, TextAnchor.MiddleLeft, 22f);
        }
        float leftHeight = y;

        // Saved? (at the bottom of the left page)
        string saveText;
        Color saveColor = quiet;
        if (s.saved) saveText = "Game saved (slot " + (s.slot + 1) + ").";
        else if (s.hasSlot) { saveText = "The game could NOT be saved. See the Console for why."; saveColor = warnInk; }
        else saveText = "Not saved: start the game from the title screen to use a save slot.";
        const float saveHeight = 40f;
        Text save = skin.NewText(panel, "Saved", 15, saveColor, TextAnchor.LowerCenter);
        BookSkin.BottomStrip(save.rectTransform, 0f, saveHeight);
        save.text = saveText;

        // ================= right page: what was shipped =================
        panel = rightWriting;
        y = 0f;
        Heading("Shipped", heading);
        if (s.shipped.Count == 0) Line("Nothing was shipped today.", 17, quiet, TextAnchor.MiddleLeft, 24f);

        int shown = Mathf.Min(s.shipped.Count, maxShippedLines);
        for (int i = 0; i < shown; i++)
        {
            DayRecap.ShippedLine line = s.shipped[i];
            string name = line.item.itemName + "  x" + line.count;
            if (line.item.type == ItemData.ItemType.Crop && line.quality > 0) name += "  (" + CropQuality.Name(line.quality) + ")";
            TwoSided(line.item.icon, name, line.gold.ToString("N0") + " G", text, goldInk);
        }
        if (s.shipped.Count > shown)
        {
            int restCount = 0, restGold = 0;
            for (int i = shown; i < s.shipped.Count; i++) { restCount += s.shipped[i].count; restGold += s.shipped[i].gold; }
            TwoSided(null, "...and " + restCount + " more", restGold.ToString("N0") + " G", quiet, goldInk);
        }
        y += 10f;
        TotalBox(s.totalGold, quiet, goldInk);
        float rightHeight = y;

        // ================= size the book so both pages hold everything =================
        float contentHeight = Mathf.Max(leftHeight + saveHeight + 8f, rightHeight);
        book.root.sizeDelta = skin.CoverSizeFor(true, pageWidth, contentHeight);
    }

    /// <summary>A section heading with a flourish under it.</summary>
    private void Heading(string text, Color color)
    {
        Line(text, 19, color, TextAnchor.MiddleLeft, 26f);
        Image flourish = skin.NewImage(panel, "Divider", BookSkin.Part.Divider);
        float h = skin.NaturalHeight(BookSkin.Part.Divider, 12f);
        BookSkin.TopStrip(flourish.rectTransform, -y, h);
        y += h + 2f;
    }

    /// <summary>One line of text across the page (it shrinks or wraps to stay inside 'height').</summary>
    private void Line(string text, int size, Color color, TextAnchor anchor, float height)
    {
        Text t = Txt(panel, text, size, color, anchor);
        PlaceTop(t.rectTransform, 0f, 0f, height);
        y += height;
    }

    /// <summary>A line with words on the left (and an optional icon) and a number on the right, over a dotted rule.</summary>
    private void TwoSided(Sprite icon, string left, string right, Color leftColor, Color rightColor)
    {
        const float h = 30f;
        RectTransform row = BookSkin.NewArea(panel, "Row");
        PlaceTop(row, 0f, 0f, h);
        skin.AddRule(row);

        float textX = 4f;
        if (icon != null)
        {
            Image img = Img(row, Color.white);
            img.sprite = icon;
            img.preserveAspect = true;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(4f, 0f);
            r.sizeDelta = new Vector2(26f, 26f);
            textX += 34f;
        }

        // The words get everything left of the number's column, so the two never run into each other.
        const float numberWidth = 96f;
        Text l = Txt(row, left, 18, leftColor, TextAnchor.MiddleLeft);
        Stretch(l.rectTransform, textX, numberWidth + 6f);
        Text rt = Txt(row, right, 18, rightColor, TextAnchor.MiddleRight);
        rt.rectTransform.anchorMin = new Vector2(1f, 0f);
        rt.rectTransform.anchorMax = new Vector2(1f, 1f);
        rt.rectTransform.pivot = new Vector2(1f, 0.5f);
        rt.rectTransform.anchoredPosition = new Vector2(-4f, 0f);
        rt.rectTransform.sizeDelta = new Vector2(numberWidth, 0f);
        y += h;
    }

    private void TotalBox(int total, Color labelColor, Color valueColor)
    {
        const float h = 52f;
        Image box = skin.NewImage(panel, "Total", BookSkin.Part.LabelBox);
        PlaceTop(box.rectTransform, 0f, 0f, h);

        Text label = Txt(box.rectTransform, "TOTAL", 16, labelColor, TextAnchor.MiddleLeft);
        Stretch(label.rectTransform, 18f, 120f);
        Text value = Txt(box.rectTransform, total.ToString("N0") + " G", 28, valueColor, TextAnchor.MiddleRight);
        Stretch(value.rectTransform, 80f, 18f);
        y += h;
    }

    // ------------------------------------------------------------------
    // Building the screen
    // ------------------------------------------------------------------

    private void BuildCanvas()
    {
        skin = BookSkin.Current;

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;      // above the HUD, menus and shipping screen; below the title screen (500)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        Image dim = Img(root, new Color(0.08f, 0.04f, 0.02f, 0.7f));
        Stretch(dim.rectTransform, 0f, 0f);

        // The open journal. Its height is set each night by Fill so both pages hold all the lines.
        book = skin.BuildBook(root, "Journal", true);
        book.root.anchorMin = book.root.anchorMax = new Vector2(0.5f, 0.5f);
        book.root.pivot = new Vector2(0.5f, 0.5f);
        book.root.anchoredPosition = Vector2.zero;
        book.root.sizeDelta = skin.CoverSizeFor(true, pageWidth, 360f);

        leftWriting = BookSkin.NewArea(book.leftContent, "Writing");
        Stretch(leftWriting, 0f, 0f);
        rightWriting = BookSkin.NewArea(book.rightContent, "Writing");
        Stretch(rightWriting, 0f, 0f);

        skin.NewCoverHint(book.root, "Press Space to start the new day", 15);

        canvas.enabled = false;
    }

    /// <summary>Places a child at the current line: 'left' in from the left edge, 'right' in from the right edge.</summary>
    private void PlaceTop(RectTransform r, float left, float right, float height)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(left, -y - height);
        r.offsetMax = new Vector2(-right, -y);
    }

    private static void Stretch(RectTransform r, float left, float right)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, 0f);
        r.offsetMax = new Vector2(-right, 0f);
    }

    private static Image Img(RectTransform parent, Color color)
    {
        var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Book-style text: it shrinks and wraps to stay inside its box.</summary>
    private Text Txt(RectTransform parent, string text, int size, Color color, TextAnchor anchor)
    {
        Text t = skin.NewText(parent, "Text", size, color, anchor);
        t.text = text;
        return t;
    }
}
