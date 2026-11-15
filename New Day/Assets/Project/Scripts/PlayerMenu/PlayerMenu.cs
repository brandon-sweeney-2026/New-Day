using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One line in the player menu. It is a normal MenuRow (label, right-hand text, detail, what happens when chosen)
/// with a few extras: a small picture, a progress bar, and a title and big picture for the panel on the right.
/// </summary>
public class PlayerMenuRow : MenuRow
{
    public Sprite icon;                 // small picture at the left of the row (optional)
    public float bar = -1f;             // 0-1 draws a thin progress bar under the row; -1 = no bar
    public Color barColor = new Color(0.45f, 0.85f, 0.45f);
    public string detailTitle;          // heading of the panel on the right (the label is used when this is empty)
    public Sprite detailIcon;           // big picture in the panel on the right (optional)
}

/// <summary>
/// The player menu: one window with a tab for each part of the game
/// (Character, Skills, Crafting, Quests, Achievements, People, Settings).
///
/// HOW IT WORKS
///  - It creates itself when the game starts and stays alive in every scene, so there is nothing to put in a scene.
///  - It only DRAWS and handles keys. What each tab shows is written in PlayerMenuPages.cs as lists of rows.
///  - Colours, the open key and all the game numbers are in the Player Menu Settings asset
///    (Assets/Resources/PlayerMenuSettings.asset, made by Farm > Set Up > Player Menu).
///
/// KEYS
///   Tab (or Esc)      open            Q / E          previous / next tab
///   Up / Down         choose a row    Space / Enter  select
///   Left / Right      change a value  Esc            back      Tab   close
///   The mouse works too: click a tab or a row (click a highlighted row to select it), wheel to scroll.
///
/// While the menu is open the player can't move and the game clock is paused.
/// </summary>
public class PlayerMenu : MonoBehaviour
{
    /// <summary>One screen of rows. Choosing a row can push another page on top (Esc goes back).</summary>
    private class Page
    {
        public string title;
        public Func<List<PlayerMenuRow>> build;
        public int selected;
        public int scroll;
    }

    private const int VisibleRows = 12;
    private const float PanelWidth = 1040f, PanelHeight = 620f;
    private const float RowHeight = 38f;
    private const float ListWidth = 500f;

    private static PlayerMenu instance;

    private PlayerMenuSettings settings;
    private bool open;
    private bool locked;
    private int tab;
    private int ignoreInputUntilFrame;
    private int lastLockedFrame = -10;
    private List<PlayerMenuRow> rows = new List<PlayerMenuRow>();

    // One stack of pages per tab: the bottom page is the tab itself.
    private readonly List<Page>[] stacks = new List<Page>[PlayerMenuPages.TabCount];

    // ---- UI pieces ----
    private Canvas canvas;
    private Font font;
    private readonly Image[] tabImages = new Image[PlayerMenuPages.TabCount];
    private readonly Text[] tabTexts = new Text[PlayerMenuPages.TabCount];
    private Text pageTitleText, hintText, detailTitleText, detailBodyText;
    private Image detailIconImage;
    private readonly Image[] rowBackgrounds = new Image[VisibleRows];
    private readonly Image[] rowIcons = new Image[VisibleRows];
    private readonly Text[] rowLabels = new Text[VisibleRows];
    private readonly Text[] rowRights = new Text[VisibleRows];
    private readonly Image[] rowBarBacks = new Image[VisibleRows];
    private readonly RectTransform[] rowBarFills = new RectTransform[VisibleRows];
    private readonly Image[] rowBarFillImages = new Image[VisibleRows];

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public static bool IsOpen { get { return instance != null && instance.open; } }

    /// <summary>Opens the menu on a tab (0 = Character ... 6 = Settings).</summary>
    public static void OpenTab(int tabIndex)
    {
        if (instance == null) return;
        instance.tab = Mathf.Clamp(tabIndex, 0, PlayerMenuPages.TabCount - 1);
        if (!instance.open) instance.Open();
        else instance.Rebuild();
    }

    /// <summary>Shows a new page on top of the current one (for example the list of items that fit a slot).</summary>
    public static void Push(string title, Func<List<PlayerMenuRow>> build)
    {
        if (instance == null || !instance.open) return;
        instance.stacks[instance.tab].Add(new Page { title = title, build = build });
        instance.ignoreInputUntilFrame = Time.frameCount + 1;
        instance.Rebuild();
    }

    /// <summary>Goes back one page (or closes the menu from a tab's first page).</summary>
    public static void Back()
    {
        if (instance != null && instance.open) instance.Pop();
    }

    public static void Close()
    {
        if (instance != null && instance.open) instance.CloseMenu();
    }

    // ------------------------------------------------------------------
    // Starting up
    // ------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSelf()
    {
        if (instance != null) return;
        var go = new GameObject("PlayerMenu");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PlayerMenu>();
    }

    private void Awake()
    {
        settings = PlayerMenuSettings.Current;
        font = settings.font != null ? settings.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < stacks.Length; i++) stacks[i] = new List<Page>();
        BuildUI();
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

    private void Open()
    {
        open = true;
        canvas.enabled = true;
        ignoreInputUntilFrame = Time.frameCount + 1;    // the key press that opened the menu must not also do something in it

        // Start every tab on its first page again.
        for (int i = 0; i < stacks.Length; i++)
        {
            int index = i;
            Page root = stacks[i].Count > 0 ? stacks[i][0] : new Page();
            root.title = PlayerMenuPages.TabNames[i];
            root.build = () => PlayerMenuPages.BuildTab(index);
            stacks[i].Clear();
            stacks[i].Add(root);
        }

        if (!locked)
        {
            locked = true;
            GameState.LockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Pause();
        }
        Rebuild();
    }

    private void CloseMenu()
    {
        open = false;
        canvas.enabled = false;
        lastLockedFrame = Time.frameCount;
        if (locked)
        {
            locked = false;
            GameState.UnlockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        }
    }

    private Page Current { get { List<Page> s = stacks[tab]; return s[s.Count - 1]; } }

    private void Pop()
    {
        List<Page> s = stacks[tab];
        ignoreInputUntilFrame = Time.frameCount + 1;
        if (s.Count <= 1) { CloseMenu(); return; }
        s.RemoveAt(s.Count - 1);
        Rebuild();
    }

    private void SetTab(int index)
    {
        int count = PlayerMenuPages.TabCount;
        tab = ((index % count) + count) % count;
        Rebuild();
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;

        if (!open)
        {
            // Another screen (dialogue, a shop, fishing...) has the controls: stay closed. The one-frame wait
            // stops the Esc or Tab press that CLOSED that screen from opening this menu in the same moment.
            if (GameState.InputLocked) { lastLockedFrame = Time.frameCount; return; }
            if (Time.frameCount <= lastLockedFrame + 1) return;

            bool want = Down(kb, settings.openKey) || Down(kb, settings.secondOpenKey) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (want) Open();
            return;
        }

        if (Time.frameCount < ignoreInputUntilFrame) return;

        // Close completely.
        if (Down(kb, settings.openKey) || (pad != null && pad.startButton.wasPressedThisFrame)) { CloseMenu(); return; }

        // Back one page.
        if (Down(kb, Key.Escape) || Down(kb, Key.Backspace) || (pad != null && pad.buttonEast.wasPressedThisFrame)) { Pop(); return; }

        // Tabs.
        if (Down(kb, Key.Q) || Down(kb, Key.PageUp) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) { SetTab(tab - 1); return; }
        if (Down(kb, Key.E) || Down(kb, Key.PageDown) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) { SetTab(tab + 1); return; }

        if (HandleMouse()) return;
        if (rows.Count == 0) return;

        bool up = Down(kb, Key.UpArrow) || Down(kb, Key.W) || (pad != null && pad.dpad.up.wasPressedThisFrame);
        bool down = Down(kb, Key.DownArrow) || Down(kb, Key.S) || (pad != null && pad.dpad.down.wasPressedThisFrame);
        bool left = Down(kb, Key.LeftArrow) || Down(kb, Key.A) || (pad != null && pad.dpad.left.wasPressedThisFrame);
        bool right = Down(kb, Key.RightArrow) || Down(kb, Key.D) || (pad != null && pad.dpad.right.wasPressedThisFrame);
        bool choose = Down(kb, Key.Space) || Down(kb, Key.Enter) || Down(kb, Key.J) || (pad != null && pad.buttonSouth.wasPressedThisFrame);

        Page page = Current;
        if (up) { Move(page, -1, true); return; }
        if (down) { Move(page, 1, true); return; }

        PlayerMenuRow row = rows[Mathf.Clamp(page.selected, 0, rows.Count - 1)];
        if (choose) Run(row.onSelect);
        else if (left) Run(row.onLeft);
        else if (right) Run(row.onRight);
    }

    /// <summary>Runs a row's action, then redraws (the action usually changed something).</summary>
    private void Run(Action action)
    {
        if (action == null) return;
        action();
        if (open) Rebuild();
    }

    private static bool Down(Keyboard kb, Key key)
    {
        return kb != null && key != Key.None && kb[key].wasPressedThisFrame;
    }

    private void Move(Page page, int delta, bool wrap)
    {
        if (rows.Count == 0) return;
        int next = page.selected + delta;
        page.selected = wrap ? (next + rows.Count) % rows.Count : Mathf.Clamp(next, 0, rows.Count - 1);
        Draw();
    }

    /// <summary>Mouse clicks and the wheel. Returns true when the mouse did something this frame.</summary>
    private bool HandleMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;
        Vector2 pos = mouse.position.ReadValue();
        Page page = Current;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            for (int i = 0; i < tabImages.Length; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(tabImages[i].rectTransform, pos, null)) continue;
                SetTab(i);
                return true;
            }
            for (int i = 0; i < VisibleRows; i++)
            {
                if (!rowBackgrounds[i].gameObject.activeSelf) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rowBackgrounds[i].rectTransform, pos, null)) continue;

                int index = page.scroll + i;
                if (index >= rows.Count) return true;
                if (index == page.selected) Run(rows[index].onSelect);      // second click on the same row = select
                else { page.selected = index; Draw(); }
                return true;
            }
        }

        float wheel = mouse.scroll.ReadValue().y;
        if (wheel > 0.1f) { Move(page, -1, false); return true; }
        if (wheel < -0.1f) { Move(page, 1, false); return true; }
        return false;
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    /// <summary>Asks the current page for its rows again, then draws them.</summary>
    private void Rebuild()
    {
        Page page = Current;
        rows = page.build != null ? page.build() : null;
        if (rows == null) rows = new List<PlayerMenuRow>();
        page.selected = rows.Count == 0 ? 0 : Mathf.Clamp(page.selected, 0, rows.Count - 1);
        Draw();
    }

    private void Draw()
    {
        Page page = Current;

        for (int i = 0; i < tabImages.Length; i++)
        {
            tabImages[i].color = i == tab ? settings.tabSelectedColor : settings.tabColor;
            tabTexts[i].color = i == tab ? Color.white : new Color(1f, 1f, 1f, 0.7f);
        }

        // "Skills > Farming" when a page is open on top of the tab.
        List<Page> stack = stacks[tab];
        string title = "";
        for (int i = 0; i < stack.Count; i++) title += (i > 0 ? "  >  " : "") + stack[i].title;
        pageTitleText.text = title;

        // Keep the highlighted row inside the visible window.
        if (page.selected < page.scroll) page.scroll = page.selected;
        if (page.selected >= page.scroll + VisibleRows) page.scroll = page.selected - VisibleRows + 1;
        page.scroll = Mathf.Max(0, Mathf.Min(page.scroll, Mathf.Max(0, rows.Count - VisibleRows)));

        for (int i = 0; i < VisibleRows; i++)
        {
            int index = page.scroll + i;
            bool exists = index < rows.Count;
            rowBackgrounds[i].gameObject.SetActive(exists);
            if (!exists) continue;

            PlayerMenuRow r = rows[index];
            bool sel = index == page.selected;
            rowBackgrounds[i].color = sel ? settings.rowSelectedColor : settings.rowColor;

            Color c = r.color.HasValue ? r.color.Value : Color.white;
            if (!r.enabled) c = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 1f);
            rowLabels[i].color = c;
            rowRights[i].color = c;
            rowLabels[i].text = r.label;
            rowRights[i].text = r.right;

            // The label moves right to make room for a picture.
            bool hasIcon = r.icon != null;
            rowIcons[i].enabled = hasIcon;
            rowIcons[i].sprite = r.icon;
            rowLabels[i].rectTransform.offsetMin = new Vector2(hasIcon ? 42f : 10f, 0f);

            bool hasBar = r.bar >= 0f;
            rowBarBacks[i].enabled = hasBar;
            rowBarFillImages[i].enabled = hasBar;
            if (hasBar)
            {
                rowBarFills[i].anchorMax = new Vector2(Mathf.Clamp01(r.bar), 1f);
                rowBarFillImages[i].color = r.barColor;
            }
        }

        // The panel on the right describes the highlighted row.
        PlayerMenuRow current = rows.Count > 0 ? rows[page.selected] : null;
        detailTitleText.text = current == null ? "" : (!string.IsNullOrEmpty(current.detailTitle) ? current.detailTitle : current.label);
        detailBodyText.text = current == null ? "" : (current.detail ?? "");

        Sprite big = current != null ? current.detailIcon : null;
        detailIconImage.enabled = big != null;
        detailIconImage.sprite = big;
        // The text starts lower when there is a picture above it.
        detailBodyText.rectTransform.offsetMax = new Vector2(-14f, big != null ? -160f : -52f);

        hintText.text = "Q / E: change tab     Up / Down: choose     Space: select     Left / Right: change     Esc: back     " + KeyName(settings.openKey) + ": close";
    }

    private static string KeyName(Key key)
    {
        return key == Key.None ? "Esc" : key.ToString();
    }

    // ------------------------------------------------------------------
    // Building the UI (everything is made in code, like the HUD)
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;          // above the HUD (100), below the small menus and dialogue (200)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = gameObject.GetComponent<RectTransform>();

        Image dim = Img(root, "Dim", settings.dimColor);
        Stretch(dim.rectTransform);

        Image panel = Img(root, "Panel", settings.panelColor);
        RectTransform p = panel.rectTransform;
        p.anchorMin = p.anchorMax = p.pivot = new Vector2(0.5f, 0.5f);
        p.anchoredPosition = Vector2.zero;
        p.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        // ---- tabs along the top ----
        int tabCount = PlayerMenuPages.TabCount;
        float gap = 4f, side = 12f;
        float tabWidth = (PanelWidth - side * 2f - gap * (tabCount - 1)) / tabCount;
        for (int i = 0; i < tabCount; i++)
        {
            Image t = Img(p, "Tab " + PlayerMenuPages.TabNames[i], settings.tabColor);
            TopLeft(t.rectTransform, side + i * (tabWidth + gap), -10f, tabWidth, 38f);
            tabImages[i] = t;

            tabTexts[i] = Txt(t.rectTransform, "Label", 18, TextAnchor.MiddleCenter);
            Stretch(tabTexts[i].rectTransform);
            tabTexts[i].text = PlayerMenuPages.TabNames[i];
        }

        // ---- the name of the page ----
        pageTitleText = Txt(p, "Page Title", 20, TextAnchor.MiddleLeft);
        pageTitleText.color = settings.headingColor;
        TopLeft(pageTitleText.rectTransform, side + 4f, -52f, PanelWidth - side * 2f, 28f);

        // ---- the list on the left ----
        float listTop = -86f;
        for (int i = 0; i < VisibleRows; i++)
        {
            Image bg = Img(p, "Row " + i, settings.rowColor);
            TopLeft(bg.rectTransform, side, listTop - i * RowHeight, ListWidth, RowHeight - 2f);
            rowBackgrounds[i] = bg;

            Image icon = Img(bg.rectTransform, "Icon", Color.white);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(8f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
            icon.preserveAspect = true;
            icon.enabled = false;
            rowIcons[i] = icon;

            rowLabels[i] = Txt(bg.rectTransform, "Label", 19, TextAnchor.MiddleLeft);
            rowLabels[i].rectTransform.anchorMin = Vector2.zero;
            rowLabels[i].rectTransform.anchorMax = new Vector2(0.64f, 1f);
            rowLabels[i].rectTransform.offsetMin = new Vector2(10f, 0f);
            rowLabels[i].rectTransform.offsetMax = Vector2.zero;

            rowRights[i] = Txt(bg.rectTransform, "Right", 17, TextAnchor.MiddleRight);
            rowRights[i].rectTransform.anchorMin = new Vector2(0.4f, 0f);
            rowRights[i].rectTransform.anchorMax = Vector2.one;
            rowRights[i].rectTransform.offsetMin = Vector2.zero;
            rowRights[i].rectTransform.offsetMax = new Vector2(-10f, 0f);

            // A thin progress bar along the bottom edge of the row.
            Image barBack = Img(bg.rectTransform, "Bar", new Color(0f, 0f, 0f, 0.5f));
            barBack.rectTransform.anchorMin = new Vector2(0f, 0f);
            barBack.rectTransform.anchorMax = new Vector2(1f, 0f);
            barBack.rectTransform.pivot = new Vector2(0.5f, 0f);
            barBack.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            barBack.rectTransform.sizeDelta = new Vector2(-16f, 4f);
            rowBarBacks[i] = barBack;

            Image barFill = Img(barBack.rectTransform, "Fill", Color.white);
            Stretch(barFill.rectTransform);
            rowBarFills[i] = barFill.rectTransform;
            rowBarFillImages[i] = barFill;
        }

        // ---- the description panel on the right ----
        float detailX = side + ListWidth + 12f;
        Image detail = Img(p, "Detail", settings.detailPanelColor);
        TopLeft(detail.rectTransform, detailX, listTop, PanelWidth - detailX - side, VisibleRows * RowHeight - 2f);

        detailTitleText = Txt(detail.rectTransform, "Title", 23, TextAnchor.UpperLeft);
        detailTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        detailTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        detailTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        detailTitleText.rectTransform.anchoredPosition = new Vector2(0f, -12f);
        detailTitleText.rectTransform.sizeDelta = new Vector2(-28f, 30f);

        detailIconImage = Img(detail.rectTransform, "Picture", Color.white);
        detailIconImage.rectTransform.anchorMin = detailIconImage.rectTransform.anchorMax = detailIconImage.rectTransform.pivot = new Vector2(0f, 1f);
        detailIconImage.rectTransform.anchoredPosition = new Vector2(14f, -52f);
        detailIconImage.rectTransform.sizeDelta = new Vector2(96f, 96f);
        detailIconImage.preserveAspect = true;
        detailIconImage.enabled = false;

        detailBodyText = Txt(detail.rectTransform, "Body", 17, TextAnchor.UpperLeft);
        detailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;      // long text wraps onto the next line
        detailBodyText.color = settings.detailTextColor;
        detailBodyText.rectTransform.anchorMin = Vector2.zero;
        detailBodyText.rectTransform.anchorMax = Vector2.one;
        detailBodyText.rectTransform.offsetMin = new Vector2(14f, 10f);
        detailBodyText.rectTransform.offsetMax = new Vector2(-14f, -52f);

        // ---- key help along the bottom ----
        hintText = Txt(p, "Hint", 14, TextAnchor.MiddleCenter);
        hintText.color = new Color(0.7f, 0.7f, 0.7f);
        hintText.rectTransform.anchorMin = new Vector2(0f, 0f);
        hintText.rectTransform.anchorMax = new Vector2(1f, 0f);
        hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
        hintText.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        hintText.rectTransform.sizeDelta = new Vector2(0f, 26f);

        canvas.enabled = false;
    }

    // ---- small UI helpers ----

    private Image Img(RectTransform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var i = go.GetComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
        return i;
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
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Places a box by its top-left corner, measured from the top-left corner of its parent (y goes down as a negative number).</summary>
    private static void TopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }
}
