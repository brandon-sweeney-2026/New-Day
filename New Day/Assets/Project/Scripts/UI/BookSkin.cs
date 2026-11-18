using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "storybook" look shared by every window in the game: the menus, the player menu, the title screen, the mailbox,
/// the dialogue box, the end-of-day recap and the forest stump. Each window is drawn as a leather-bound book with cream
/// paper pages, ink-coloured writing, ribbon bookmarks for tabs and a honey highlighter on the chosen row.
///
/// CHANGING THE ART
///  - The pictures are PNG files in Assets/Project/Art/UI/Book (book_cover, page_left, page_right, page_single, tab...).
///    Paint over them in any art program and save with the same name: every window picks up the change.
///  - Or drag a different sprite into a slot on the Book Skin asset (Assets/Resources/BookSkin.asset).
///  - The edges of each picture stay sharp and the middle is stretched or repeated to fit the window. How many pixels count
///    as "edge" is the sprite's Border, shown as green lines in Unity's Sprite Editor.
///  - Colours of the writing, how much leather shows and the space between the page edge and the writing are all below.
///
/// The asset is made by Farm > Set Up > Book UI (and by itself the first time the PNGs are imported).
/// Without it, the built-in values below are used, with plain parchment-coloured boxes instead of pictures.
/// </summary>
[CreateAssetMenu(menuName = "Farm/Book Skin", fileName = "BookSkin")]
public class BookSkin : ScriptableObject
{
    /// <summary>The different pictures a window can be made of.</summary>
    public enum Part
    {
        Cover, PageSingle, PageLeft, PageRight, Gutter, Tab, TabSelected, Highlight, Rule,
        Banner, Divider, Slot, SlotSelected, LabelBox, BarBack, PortraitFrame
    }

    [Header("Pictures (repaint the PNGs in Assets/Project/Art/UI/Book, or drag your own here)")]
    [Tooltip("Leather cover behind the pages. The middle is repeated (tiled).")]
    public Sprite cover;
    [Tooltip("One page on its own (menus, mailbox, dialogue). The page edges stacked on the right and bottom.")]
    public Sprite pageSingle;
    [Tooltip("Left page of an open book. Its right side is the fold.")]
    public Sprite pageLeft;
    [Tooltip("Right page of an open book. Its left side is the fold.")]
    public Sprite pageRight;
    [Tooltip("The shadow of the fold, laid over the middle of an open book. Stretched to the page height.")]
    public Sprite gutter;
    [Tooltip("A bookmark ribbon used as a tab.")]
    public Sprite tab;
    [Tooltip("The bookmark of the open tab.")]
    public Sprite tabSelected;
    [Tooltip("The highlighter wash behind the chosen row.")]
    public Sprite highlight;
    [Tooltip("The faint dotted line under each row. Repeated sideways.")]
    public Sprite rule;
    [Tooltip("The ribbon banner behind window titles.")]
    public Sprite banner;
    [Tooltip("A flourish line between parts of a page.")]
    public Sprite divider;
    [Tooltip("An inked box for an item slot.")]
    public Sprite slot;
    [Tooltip("The ring drawn around the chosen item slot.")]
    public Sprite slotSelected;
    [Tooltip("A box for totals and notes.")]
    public Sprite labelBox;
    [Tooltip("The outline of a progress bar (the fill is a plain colour).")]
    public Sprite barBack;
    [Tooltip("The pasted-in photo frame around a speaker's portrait.")]
    public Sprite portraitFrame;

    [Header("Size")]
    [Tooltip("Screen pixels per art pixel (at the 1280 x 720 reference size). 2 = chunky but small, 3 = bigger pixels.")]
    [Range(1f, 6f)] public float pixelScale = 2f;
    [Tooltip("How much leather shows around the pages, left / right / top.")]
    [Min(0f)] public float coverMargin = 16f;
    [Tooltip("How much leather shows under the pages. The key help is written there.")]
    [Min(0f)] public float coverBottomMargin = 46f;
    [Tooltip("The key help on the leather starts this far above the bottom edge, so it sits above the stitching and between the brass corners.")]
    [Min(0f)] public float coverTextBottom = 17f;
    [Tooltip("Space between the outer edge of a page and the writing (the stacked page edges are on this side).")]
    [Min(0f)] public float pageOuterPadding = 30f;
    [Tooltip("Space between the fold of an open book and the writing.")]
    [Min(0f)] public float pageGutterPadding = 30f;
    [Tooltip("Space between the top of a page and the writing.")]
    [Min(0f)] public float pageTopPadding = 22f;
    [Tooltip("Space between the bottom of a page and the writing.")]
    [Min(0f)] public float pageBottomPadding = 26f;

    [Header("Writing")]
    [Tooltip("Leave empty to use Unity's built-in font. A storybook or handwriting font looks great here.")]
    public Font font;
    [Tooltip("Normal writing on the page.")]
    public Color ink = new Color(0.24f, 0.15f, 0.09f);
    [Tooltip("Hints, greyed-out rows and quiet notes.")]
    public Color fadedInk = new Color(0.49f, 0.38f, 0.27f);
    [Tooltip("Headings and titles written on the page.")]
    public Color headingInk = new Color(0.48f, 0.18f, 0.1f);
    [Tooltip("Good news: things you can afford, finished goals, XP.")]
    public Color goodInk = new Color(0.22f, 0.42f, 0.16f);
    [Tooltip("Bad news: warnings, things you can't do.")]
    public Color badInk = new Color(0.66f, 0.2f, 0.13f);
    [Tooltip("Money.")]
    public Color goldInk = new Color(0.55f, 0.36f, 0.05f);
    [Tooltip("Writing on the ribbon banner.")]
    public Color bannerText = new Color(1f, 0.93f, 0.78f);
    [Tooltip("Gold lettering pressed into the leather cover (key help, the game title).")]
    public Color coverText = new Color(0.95f, 0.8f, 0.48f);
    [Tooltip("Writing on a closed bookmark tab.")]
    public Color tabText = new Color(0.27f, 0.17f, 0.09f);
    [Tooltip("Writing on the open bookmark tab.")]
    public Color tabSelectedText = new Color(1f, 0.93f, 0.8f);

    [Header("Plain colours (used only when a picture slot above is empty)")]
    public Color paperColor = new Color(0.95f, 0.89f, 0.76f);
    public Color coverColor = new Color(0.48f, 0.24f, 0.13f);
    public Color highlightColor = new Color(0.95f, 0.78f, 0.35f, 0.55f);
    public Color slotColor = new Color(0.89f, 0.8f, 0.62f);
    [Tooltip("Laid over the game behind a window.")]
    public Color dimColor = new Color(0.08f, 0.04f, 0.02f, 0.55f);

    [Header("Older art")]
    [Tooltip("Ticked: characters that have their own dialogue box pictures (Panel / Frame on their speaker) keep them. Unticked: everyone talks on a book page.")]
    public bool keepOldScreenArt = false;

    // ------------------------------------------------------------------
    // Finding the skin
    // ------------------------------------------------------------------

    private static BookSkin current;
    private static Font builtinFont;

    /// <summary>The skin in Assets/Resources/BookSkin.asset, or the built-in values when there is none.</summary>
    public static BookSkin Current
    {
        get
        {
            if (current == null) current = Resources.Load<BookSkin>("BookSkin");
            if (current == null)
            {
                current = CreateInstance<BookSkin>();
                current.hideFlags = HideFlags.HideAndDontSave;
            }
            return current;
        }
    }

    /// <summary>The font every window writes with.</summary>
    public Font Font
    {
        get
        {
            if (font != null) return font;
            if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return builtinFont;
        }
    }

    public Sprite Get(Part part)
    {
        switch (part)
        {
            case Part.Cover: return cover;
            case Part.PageSingle: return pageSingle;
            case Part.PageLeft: return pageLeft != null ? pageLeft : pageSingle;
            case Part.PageRight: return pageRight != null ? pageRight : pageSingle;
            case Part.Gutter: return gutter;
            case Part.Tab: return tab;
            case Part.TabSelected: return tabSelected != null ? tabSelected : tab;
            case Part.Highlight: return highlight;
            case Part.Rule: return rule;
            case Part.Banner: return banner;
            case Part.Divider: return divider;
            case Part.Slot: return slot;
            case Part.SlotSelected: return slotSelected;
            case Part.LabelBox: return labelBox;
            case Part.BarBack: return barBack;
            case Part.PortraitFrame: return portraitFrame;
        }
        return null;
    }

    /// <summary>The plain colour used for a part whose picture slot is empty.</summary>
    private Color Fallback(Part part)
    {
        switch (part)
        {
            case Part.Cover: return coverColor;
            case Part.PageSingle: case Part.PageLeft: case Part.PageRight: case Part.LabelBox: case Part.PortraitFrame: return paperColor;
            case Part.Gutter: return new Color(0.42f, 0.28f, 0.16f, 0.35f);
            case Part.Tab: return new Color(0.75f, 0.63f, 0.42f);
            case Part.TabSelected: return new Color(0.65f, 0.23f, 0.16f);
            case Part.Highlight: return highlightColor;
            case Part.Rule: return new Color(fadedInk.r, fadedInk.g, fadedInk.b, 0.25f);
            case Part.Banner: return new Color(0.65f, 0.23f, 0.16f);
            case Part.Divider: return new Color(fadedInk.r, fadedInk.g, fadedInk.b, 0.6f);
            case Part.Slot: return slotColor;
            case Part.SlotSelected: return new Color(0.65f, 0.23f, 0.16f, 0.5f);
            case Part.BarBack: return new Color(0.42f, 0.3f, 0.18f, 0.6f);
        }
        return Color.white;
    }

    // ------------------------------------------------------------------
    // Painting images
    // ------------------------------------------------------------------

    /// <summary>Gives an Image one of the book pictures, drawn at the skin's pixel size. Empty slot = a plain colour box.</summary>
    public void Paint(Image img, Part part)
    {
        if (img == null) return;
        Sprite s = Get(part);
        if (s == null)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = Fallback(part);
            return;
        }

        img.sprite = s;
        img.color = Color.white;
        img.preserveAspect = false;
        img.fillCenter = true;
        switch (part)
        {
            case Part.Gutter:
                img.type = Image.Type.Simple;             // one column of shading, stretched to the page height
                break;
            case Part.Cover: case Part.PageSingle: case Part.PageLeft: case Part.PageRight: case Part.Rule:
                img.type = Image.Type.Tiled;              // leather grain and paper fibre are repeated, not smeared
                break;
            default:
                img.type = Image.Type.Sliced;             // corners stay sharp, the middle stretches
                break;
        }
        // A sprite imported at any pixels-per-unit is shown at 'pixelScale' screen pixels per art pixel.
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, 100f / (Mathf.Max(1f, s.pixelsPerUnit) * Mathf.Max(0.1f, pixelScale)));
    }

    /// <summary>Makes a new Image child painted with a book picture.</summary>
    public Image NewImage(Transform parent, string name, Part part)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        Paint(img, part);
        return img;
    }

    /// <summary>The row background: the highlighter wash when chosen, nothing otherwise.</summary>
    public void PaintRow(Image img, bool selected)
    {
        if (selected) Paint(img, Part.Highlight);
        else { img.sprite = null; img.type = Image.Type.Simple; img.color = Color.clear; }
    }

    /// <summary>A height that fits the picture of a part (for the banner and divider), in screen units.</summary>
    public float NaturalHeight(Part part, float fallback)
    {
        Sprite s = Get(part);
        return s != null ? s.rect.height * pixelScale : fallback;
    }

    // ------------------------------------------------------------------
    // Writing
    // ------------------------------------------------------------------

    /// <summary>
    /// Makes a Text child in the book style. With 'fit' on, the text shrinks (down to about half size) and wraps
    /// so it always stays inside its box instead of spilling over the page edge.
    /// </summary>
    public Text NewText(Transform parent, string name, int size, Color color, TextAnchor anchor, bool fit = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.supportRichText = true;
        if (fit) Fit(t, size);
        else
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }
        return t;
    }

    /// <summary>Keeps a Text inside its box: it wraps, and shrinks when it still doesn't fit.</summary>
    public static void Fit(Text t, int size)
    {
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMaxSize = size;
        t.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.55f));
    }

    /// <summary>
    /// Turns a colour that was picked for the old dark windows (light green, pale yellow, white...) into an ink colour
    /// that reads well on paper: same hue, but darker. No colour = plain ink.
    /// </summary>
    public Color Ink(Color? c)
    {
        if (!c.HasValue) return ink;
        Color col = c.Value;
        float h, s, v;
        Color.RGBToHSV(col, out h, out s, out v);
        if (s < 0.15f)
        {
            // white / grey: plain ink (light) or faded ink (mid grey); dark colours already read well
            if (v > 0.8f) return new Color(ink.r, ink.g, ink.b, col.a);
            if (v > 0.45f) return new Color(fadedInk.r, fadedInk.g, fadedInk.b, col.a);
            return col;
        }
        Color dark = Color.HSVToRGB(h, Mathf.Max(s, 0.65f), Mathf.Min(v, 0.5f));
        dark.a = col.a;
        return dark;
    }

    /// <summary>Greyed-out ink for a row that can't be chosen right now.</summary>
    public Color Disabled(Color inkColor)
    {
        return Color.Lerp(inkColor, fadedInk, 0.7f) * new Color(1f, 1f, 1f, 0.75f);
    }

    // ------------------------------------------------------------------
    // Building a book
    // ------------------------------------------------------------------

    /// <summary>The pieces of a book made by BuildBook. Put writing inside the Content rectangles and it stays on the page.</summary>
    public class Book
    {
        public RectTransform root;          // the leather cover: size and place this
        public RectTransform leftPage;      // the only page of a single-page book
        public RectTransform rightPage;     // null for a single page
        public RectTransform leftContent;   // the writing area of the left (or only) page
        public RectTransform rightContent;  // null for a single page
        public Image coverImage, leftPageImage, rightPageImage;
    }

    /// <summary>
    /// Makes a leather cover with one page (spread = false) or an open book with two pages (spread = true).
    /// The pages fill the cover, leaving 'coverMargin' of leather around them (and 'coverBottomMargin' below).
    /// Size and place the returned root yourself; the pages and content areas follow it.
    /// </summary>
    public Book BuildBook(Transform parent, string name, bool spread)
    {
        var b = new Book();
        b.coverImage = NewImage(parent, name, Part.Cover);
        b.root = b.coverImage.rectTransform;

        float m = coverMargin, mb = coverBottomMargin;
        if (!spread)
        {
            b.leftPageImage = NewImage(b.root, "Page", Part.PageSingle);
            b.leftPage = b.leftPageImage.rectTransform;
            Inset(b.leftPage, Vector2.zero, Vector2.one, m, mb, m, m);
            b.leftContent = NewArea(b.leftPage, "Content");
            Inset(b.leftContent, Vector2.zero, Vector2.one, pageOuterPadding * 0.85f, pageBottomPadding, pageOuterPadding, pageTopPadding);
            return b;
        }

        b.leftPageImage = NewImage(b.root, "Left Page", Part.PageLeft);
        b.leftPage = b.leftPageImage.rectTransform;
        Inset(b.leftPage, Vector2.zero, new Vector2(0.5f, 1f), m, mb, 0f, m);

        b.rightPageImage = NewImage(b.root, "Right Page", Part.PageRight);
        b.rightPage = b.rightPageImage.rectTransform;
        Inset(b.rightPage, new Vector2(0.5f, 0f), Vector2.one, 0f, mb, m, m);

        // the fold's shadow, laid across the join between the pages
        Image fold = NewImage(b.root, "Fold", Part.Gutter);
        RectTransform f = fold.rectTransform;
        f.anchorMin = new Vector2(0.5f, 0f);
        f.anchorMax = new Vector2(0.5f, 1f);
        f.pivot = new Vector2(0.5f, 0.5f);
        float foldWidth = gutter != null ? gutter.rect.width * pixelScale : 12f;
        f.offsetMin = new Vector2(-foldWidth * 0.5f, mb);
        f.offsetMax = new Vector2(foldWidth * 0.5f, -m);

        b.leftContent = NewArea(b.leftPage, "Content");
        Inset(b.leftContent, Vector2.zero, Vector2.one, pageOuterPadding, pageBottomPadding, pageGutterPadding, pageTopPadding);
        b.rightContent = NewArea(b.rightPage, "Content");
        Inset(b.rightContent, Vector2.zero, Vector2.one, pageGutterPadding, pageBottomPadding, pageOuterPadding, pageTopPadding);
        return b;
    }

    /// <summary>The cover size needed so that the writing area of each page is at least contentWidth x contentHeight.</summary>
    public Vector2 CoverSizeFor(bool spread, float contentWidth, float contentHeight)
    {
        float h = coverMargin + coverBottomMargin + pageTopPadding + pageBottomPadding + contentHeight;
        float w = spread
            ? coverMargin * 2f + (pageOuterPadding + pageGutterPadding + contentWidth) * 2f
            : coverMargin * 2f + pageOuterPadding * 1.85f + contentWidth;
        return new Vector2(Mathf.Ceil(w), Mathf.Ceil(h));
    }

    /// <summary>A red ribbon banner with a title written on it, centred at the top of 'parent'.</summary>
    public Text NewBanner(Transform parent, string name, float width, float y, int fontSize)
    {
        float h = Mathf.Max(fontSize + 14f, NaturalHeight(Part.Banner, 40f));
        Image band = NewImage(parent, name, Part.Banner);
        RectTransform r = band.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = new Vector2(width, h);

        // The writing keeps clear of the forked tails and sits on the raised band (a little above the middle).
        Text t = NewText(r, "Text", fontSize, bannerText, TextAnchor.MiddleCenter);
        float tail = banner != null ? banner.border.x * pixelScale : 10f;
        Inset(t.rectTransform, Vector2.zero, Vector2.one, tail, h * 0.18f, tail, 0f);
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.3f, 0.07f, 0.04f, 0.8f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    /// <summary>
    /// Writes gold key help on the leather under the pages of 'book' (the cover). It sits in the strip between the
    /// stitching and the pages, clear of the brass corners, and shrinks if the line is too long.
    /// </summary>
    public Text NewCoverHint(RectTransform cover, string text, int size)
    {
        Text t = NewText(cover, "Hint", size, coverText, TextAnchor.MiddleCenter);
        float h = Mathf.Max(14f, coverBottomMargin - coverTextBottom - 2f);
        BottomStrip(t.rectTransform, coverTextBottom, h);
        t.rectTransform.sizeDelta = new Vector2(-90f, h);
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.15f, 0.06f, 0.02f, 0.9f);
        shadow.effectDistance = new Vector2(1f, -1f);
        t.text = text;
        return t;
    }

    /// <summary>Adds the faint dotted line along the bottom of a row.</summary>
    public Image AddRule(RectTransform row)
    {
        Image line = NewImage(row, "Rule", Part.Rule);
        RectTransform r = line.rectTransform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(-8f, Mathf.Max(1f, pixelScale));
        line.transform.SetAsFirstSibling();
        return line;
    }

    // ------------------------------------------------------------------
    // Small layout helpers (anyone may use these)
    // ------------------------------------------------------------------

    /// <summary>An empty rectangle to put things in.</summary>
    public static RectTransform NewArea(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    /// <summary>Stretches a rectangle between two anchor corners of its parent, then pulls each side in.</summary>
    public static void Inset(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, float left, float bottom, float right, float top)
    {
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Places a box by its top-left corner, measured from the top-left of its parent (y goes down as a negative number).</summary>
    public static void TopLeft(RectTransform r, float x, float y, float width, float height)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(width, height);
    }

    /// <summary>A full-width strip at a given distance from the top of its parent.</summary>
    public static void TopStrip(RectTransform r, float y, float height)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = new Vector2(0f, height);
    }

    /// <summary>A full-width strip at a given distance from the bottom of its parent.</summary>
    public static void BottomStrip(RectTransform r, float y, float height)
    {
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = new Vector2(0f, height);
    }
}
