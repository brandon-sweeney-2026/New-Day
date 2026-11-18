using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One line in a menu.</summary>
public class MenuRow
{
    public string label = "";
    public string right = "";          // text on the right edge (a price, a status...)
    public string detail = "";         // shown at the bottom while this row is highlighted
    public bool enabled = true;        // greyed out when false (it can still be selected; your callback decides what happens)
    public Color? color;               // optional text colour
    public Action onSelect;            // Space / Enter / E
    public Action onLeft, onRight;     // Left / Right arrows (A / D)
}

/// <summary>
/// A small keyboard-driven menu used by the shop, the stove and the house design table.
/// Built in code, so there is nothing to set up. Menus stack: opening a new one on top of another lets Esc go back.
///
///   MenuUI.Open("Title", () => rows, "hint text");     // 'rows' is called again after every action, so it refreshes itself
///
/// Keys: Up/Down (W/S) move, Space/Enter/E choose, Left/Right (A/D) change, Esc/Backspace/Q go back.
/// While a menu is open the player can't move and the game clock is paused.
/// </summary>
public class MenuUI : MonoBehaviour
{
    private class Frame
    {
        public string title;
        public string hint;
        public Func<List<MenuRow>> build;
        public int selected;
        public int scroll;
    }

    private const int VisibleRows = 9;

    private static MenuUI instance;
    private readonly Stack<Frame> stack = new Stack<Frame>();
    private List<MenuRow> rows = new List<MenuRow>();
    private int ignoreInputUntilFrame;
    private bool locked;

    private Canvas canvas;
    private Text titleText, hintText, detailText;
    private readonly Image[] rowBackgrounds = new Image[VisibleRows];
    private readonly Text[] rowLabels = new Text[VisibleRows];
    private readonly Text[] rowRights = new Text[VisibleRows];
    private BookSkin skin;

    // The page: room for the title ribbon, the rows and the description underneath (the book is sized to fit these).
    private const float PageWidth = 600f;
    private const float RowHeight = 34f;
    private const float RowsTop = 52f;          // below the title ribbon
    private const float DetailHeight = 76f;

    public static bool IsOpen { get { return instance != null && instance.stack.Count > 0; } }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public static void Open(string title, Func<List<MenuRow>> build, string hint = null)
    {
        if (instance == null)
        {
            var go = new GameObject("MenuUI");
            instance = go.AddComponent<MenuUI>();
            instance.BuildUI();
        }
        instance.Push(new Frame
        {
            title = title,
            build = build,
            hint = hint ?? "Up/Down: choose     Space: select     Esc: back"
        });
    }

    /// <summary>Closes every open menu.</summary>
    public static void CloseAll()
    {
        if (instance != null) instance.Clear();
    }

    /// <summary>Goes back one menu (or closes if it was the only one).</summary>
    public static void Back()
    {
        if (instance != null) instance.Pop();
    }

    /// <summary>Redraws the current menu (call after something outside the menu changed).</summary>
    public static void Refresh()
    {
        if (instance != null && instance.stack.Count > 0) instance.Rebuild();
    }

    // ------------------------------------------------------------------
    // Stack handling
    // ------------------------------------------------------------------

    private void Push(Frame frame)
    {
        stack.Push(frame);
        ignoreInputUntilFrame = Time.frameCount + 1;   // the key press that opened the menu must not also select a row
        if (!locked)
        {
            locked = true;
            GameState.LockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Pause();
        }
        canvas.enabled = true;
        Rebuild();
    }

    private void Pop()
    {
        if (stack.Count > 0) stack.Pop();
        ignoreInputUntilFrame = Time.frameCount + 1;
        if (stack.Count == 0) Close();
        else Rebuild();
    }

    private void Clear()
    {
        stack.Clear();
        Close();
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
    // Input
    // ------------------------------------------------------------------

    private void Update()
    {
        if (stack.Count == 0 || Time.frameCount < ignoreInputUntilFrame) return;

        Frame f = stack.Peek();
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;

        bool up = Pressed(kb, Key.UpArrow, Key.W) || (pad != null && pad.dpad.up.wasPressedThisFrame);
        bool down = Pressed(kb, Key.DownArrow, Key.S) || (pad != null && pad.dpad.down.wasPressedThisFrame);
        bool left = Pressed(kb, Key.LeftArrow, Key.A) || (pad != null && pad.dpad.left.wasPressedThisFrame);
        bool right = Pressed(kb, Key.RightArrow, Key.D) || (pad != null && pad.dpad.right.wasPressedThisFrame);
        bool choose = Pressed(kb, Key.Space, Key.Enter) || Pressed(kb, Key.E, Key.J) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        bool back = Pressed(kb, Key.Escape, Key.Backspace) || Pressed(kb, Key.Q, Key.Tab) || (pad != null && pad.buttonEast.wasPressedThisFrame);

        if (back) { Pop(); return; }
        if (rows.Count == 0) return;

        if (up) Move(f, -1);
        else if (down) Move(f, 1);

        MenuRow row = rows[Mathf.Clamp(f.selected, 0, rows.Count - 1)];
        if (choose && row.onSelect != null) { row.onSelect(); if (stack.Count > 0) Rebuild(); }
        else if (left && row.onLeft != null) { row.onLeft(); if (stack.Count > 0) Rebuild(); }
        else if (right && row.onRight != null) { row.onRight(); if (stack.Count > 0) Rebuild(); }
    }

    private static bool Pressed(Keyboard kb, Key a, Key b)
    {
        return kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame);
    }

    private void Move(Frame f, int delta)
    {
        f.selected = (f.selected + delta + rows.Count) % rows.Count;
        Draw();
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void Rebuild()
    {
        Frame f = stack.Peek();
        rows = f.build != null ? f.build() : new List<MenuRow>();
        f.selected = rows.Count == 0 ? 0 : Mathf.Clamp(f.selected, 0, rows.Count - 1);
        Draw();
    }

    private void Draw()
    {
        Frame f = stack.Peek();
        titleText.text = f.title;
        hintText.text = f.hint;

        // Keep the highlighted row inside the visible window.
        if (f.selected < f.scroll) f.scroll = f.selected;
        if (f.selected >= f.scroll + VisibleRows) f.scroll = f.selected - VisibleRows + 1;
        f.scroll = Mathf.Max(0, Mathf.Min(f.scroll, Mathf.Max(0, rows.Count - VisibleRows)));

        for (int i = 0; i < VisibleRows; i++)
        {
            int index = f.scroll + i;
            bool exists = index < rows.Count;
            rowBackgrounds[i].gameObject.SetActive(exists);
            if (!exists) continue;

            MenuRow r = rows[index];
            bool sel = index == f.selected;
            skin.PaintRow(rowBackgrounds[i], sel);           // honey highlighter behind the chosen row

            // Row colours were picked for the old dark window; Ink() turns them into darker ink that reads on paper.
            Color c = skin.Ink(r.color);
            if (!r.enabled) c = skin.Disabled(c);
            rowLabels[i].color = c;
            rowRights[i].color = c;
            rowLabels[i].text = (sel ? "> " : "   ") + r.label;
            rowRights[i].text = r.right;
        }

        detailText.text = rows.Count > 0 ? rows[f.selected].detail : "";
    }

    // ------------------------------------------------------------------
    // Building the UI
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        skin = BookSkin.Current;
        DontDestroyOnLoad(gameObject);

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = gameObject.GetComponent<RectTransform>();

        Image dim = Img(root, "Dim", skin.dimColor);
        Stretch(dim.rectTransform);

        // ---- one page in a leather cover, sized so the title, rows and description all fit on the page ----
        float contentHeight = RowsTop + VisibleRows * RowHeight + 8f + DetailHeight;
        BookSkin.Book book = skin.BuildBook(root, "Book", false);
        Place(book.root, new Vector2(0.5f, 0.5f), Vector2.zero, skin.CoverSizeFor(false, PageWidth, contentHeight));
        RectTransform page = book.leftContent;

        // ---- the title on a red ribbon ----
        titleText = skin.NewBanner(page, "Title", PageWidth * 0.75f, 0f, 22);

        // ---- the rows ----
        for (int i = 0; i < VisibleRows; i++)
        {
            Image bg = Img(page, "Row" + i, Color.clear);
            BookSkin.TopStrip(bg.rectTransform, -RowsTop - i * RowHeight, RowHeight - 2f);
            skin.AddRule(bg.rectTransform);
            rowBackgrounds[i] = bg;

            rowLabels[i] = skin.NewText(bg.rectTransform, "Label", 20, skin.ink, TextAnchor.MiddleLeft);
            rowLabels[i].rectTransform.anchorMin = Vector2.zero;
            rowLabels[i].rectTransform.anchorMax = new Vector2(0.62f, 1f);
            rowLabels[i].rectTransform.offsetMin = new Vector2(8f, 0f);
            rowLabels[i].rectTransform.offsetMax = Vector2.zero;

            rowRights[i] = skin.NewText(bg.rectTransform, "Right", 18, skin.ink, TextAnchor.MiddleRight);
            rowRights[i].rectTransform.anchorMin = new Vector2(0.45f, 0f);
            rowRights[i].rectTransform.anchorMax = Vector2.one;
            rowRights[i].rectTransform.offsetMin = Vector2.zero;
            rowRights[i].rectTransform.offsetMax = new Vector2(-8f, 0f);
        }

        // ---- a flourish, then the description of the highlighted row at the bottom of the page ----
        Image flourish = skin.NewImage(page, "Divider", BookSkin.Part.Divider);
        float flourishH = skin.NaturalHeight(BookSkin.Part.Divider, 12f);
        BookSkin.BottomStrip(flourish.rectTransform, DetailHeight, flourishH);

        detailText = skin.NewText(page, "Detail", 17, skin.ink, TextAnchor.UpperLeft);
        BookSkin.BottomStrip(detailText.rectTransform, 0f, DetailHeight - 4f);

        // ---- key help, pressed in gold into the leather under the page ----
        hintText = skin.NewCoverHint(book.root, "", 15);

        canvas.enabled = false;
    }

    private Image Img(RectTransform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var i = go.GetComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
        return i;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
