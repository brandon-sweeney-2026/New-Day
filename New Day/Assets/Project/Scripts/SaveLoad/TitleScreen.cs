using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The title screen shown when the game starts: Continue, New Game, Load Game, Quit.
///
///  - Continue loads the save that was written most recently.
///  - New Game asks which of the 3 slots to use (and asks again before replacing a slot that has a save in it).
///  - Load Game lists the slots with their date and gold.
/// The farm is visible, dimmed, behind the menu. The player can't move and the clock is stopped until a choice is made.
///
/// It creates itself when the game starts, so there is nothing to set up. To change the text or colours, add this
/// component to an object in your first scene (Farm) and change the Inspector values; that copy is then used.
/// To skip it while testing in the Editor: Farm > Test and Debug > Skip Title Screen When Testing.
/// It is always shown in a built game.
///
/// Keys: Up/Down (W/S) choose, Enter/Space/E select, Esc/Backspace back. Mouse and gamepad work too.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    /// <summary>PlayerPrefs key set by the editor menu "Skip Title Screen When Testing".</summary>
    public const string SkipInEditorKey = "NewDay_SkipTitleScreenInEditor";

    [Header("Text")]
    [SerializeField] private string gameTitle = "New Day";
    [SerializeField] private string subtitle = "a cozy farming story";

    // The screen is drawn as the front cover of a leather book (the Book Skin: Assets/Resources/BookSkin.asset),
    // with the title pressed in gold and the menu written on a paper page set into the cover.
    [Header("Colours")]
    [Tooltip("Laid over the whole screen. Lower the alpha to see more of the farm behind the book.")]
    [SerializeField] private Color backdropColor = new Color(0.1f, 0.06f, 0.03f, 0.8f);
    [Tooltip("The game title, pressed into the leather.")]
    [SerializeField] private Color titleColor = new Color(0.98f, 0.82f, 0.45f);
    [SerializeField] private Color subtitleColor = new Color(0.96f, 0.88f, 0.7f);
    [Tooltip("Writing on the page. Light colours are turned into matching dark ink so they read on paper.")]
    [SerializeField] private Color textColor = new Color(0.24f, 0.15f, 0.09f);
    [Tooltip("The line under the menu that describes the highlighted choice.")]
    [SerializeField] private Color messageColor = new Color(0.48f, 0.18f, 0.1f);

    [Header("Size")]
    [SerializeField] private Vector2 coverSize = new Vector2(720f, 660f);

    /// <summary>True while the title screen is up.</summary>
    public static bool IsOpen { get; private set; }

    private class Row
    {
        public string label = "";
        public string right = "";       // text on the right edge (a slot's date and gold)
        public string detail = "";      // shown under the menu while this row is highlighted
        public bool enabled = true;
        public Action onSelect;
    }

    private enum Page { Main, NewGameSlots, LoadSlots, ConfirmOverwrite }

    private const int MaxRows = 6;
    private const float RowHeight = 44f, RowGap = 8f;

    private Page page = Page.Main;
    private int selected;
    private int pendingSlot;
    private string message = "";
    private bool busy;                  // a save is loading; ignore keys
    private List<Row> rows = new List<Row>();

    private Canvas canvas;
    private BookSkin skin;
    private Text messageText, hintText;
    private readonly Image[] rowImages = new Image[MaxRows];
    private readonly Text[] rowLabels = new Text[MaxRows];
    private readonly Text[] rowRights = new Text[MaxRows];

    private bool locked;
    private int ignoreInputUntilFrame;
    private Vector2 lastMouse;

    // ------------------------------------------------------------------
    // Showing it when the game starts
    // ------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        IsOpen = false;
    }

    // Runs once, after the first scene has loaded (not on later scene changes).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ShowAtGameStart()
    {
        if (Application.isBatchMode) return;                                                  // the batch scene builders
        if (Application.isEditor && PlayerPrefs.GetInt(SkipInEditorKey, 0) == 1) return;      // testing shortcut

        TitleScreen screen = FindAnyObjectByType<TitleScreen>();
        if (screen == null) screen = new GameObject("TitleScreen").AddComponent<TitleScreen>();
        screen.Open();
    }

    private void Open()
    {
        if (canvas == null) BuildUI();
        if (!locked)
        {
            locked = true;
            GameState.LockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Pause();
        }
        IsOpen = true;
        canvas.enabled = true;
        if (Mouse.current != null) lastMouse = Mouse.current.position.ReadValue();
        GoTo(Page.Main);
    }

    /// <summary>Hides the title screen and hands the game to the player.</summary>
    private void Close()
    {
        if (locked)
        {
            locked = false;
            GameState.UnlockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        }
        IsOpen = false;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Loading a save throws this scene away with the title screen still up, so let go of the locks here too.
        if (locked)
        {
            GameState.UnlockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        }
        IsOpen = false;
    }

    // ------------------------------------------------------------------
    // The pages
    // ------------------------------------------------------------------

    private void GoTo(Page p)
    {
        page = p;
        selected = 0;
        message = "";
        ignoreInputUntilFrame = Time.frameCount + 1;
        Rebuild();
    }

    private void Rebuild()
    {
        rows = BuildRows();
        selected = rows.Count == 0 ? 0 : Mathf.Clamp(selected, 0, rows.Count - 1);
        Draw();
    }

    private List<Row> BuildRows()
    {
        var list = new List<Row>();
        switch (page)
        {
            case Page.Main:
            {
                int recent = SaveSystem.MostRecentSlot();
                if (recent >= 0)
                    list.Add(new Row { label = "Continue", right = SaveSystem.Describe(recent),
                                       detail = "Carry on from your last save (slot " + (recent + 1) + ").", onSelect = () => LoadSlot(recent) });
                list.Add(new Row { label = "New Game", detail = "Start a new farm in one of " + SaveSystem.SlotCount + " save slots.",
                                   onSelect = () => GoTo(Page.NewGameSlots) });
                list.Add(new Row { label = "Load Game", enabled = recent >= 0,
                                   detail = recent >= 0 ? "Pick a saved game." : "There are no saved games yet.",
                                   onSelect = () => GoTo(Page.LoadSlots) });
                list.Add(new Row { label = "Quit", detail = "Close the game.", onSelect = Quit });
                break;
            }

            case Page.NewGameSlots:
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    int slot = i;
                    bool used = SaveSystem.HasSave(slot);
                    list.Add(new Row
                    {
                        label = "Slot " + (slot + 1),
                        right = SaveSystem.Describe(slot),
                        detail = used ? "This slot has a save in it (saved " + SaveSystem.SavedAt(slot) + "). Starting here replaces it."
                                      : "An empty slot. Your new farm will be saved here every night.",
                        onSelect = () =>
                        {
                            if (!SaveSystem.HasSave(slot)) { BeginNewGame(slot); return; }
                            pendingSlot = slot;
                            GoTo(Page.ConfirmOverwrite);
                        }
                    });
                }
                list.Add(new Row { label = "Back", onSelect = () => GoTo(Page.Main) });
                break;

            case Page.LoadSlots:
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    int slot = i;
                    bool used = SaveSystem.HasSave(slot);
                    list.Add(new Row
                    {
                        label = "Slot " + (slot + 1),
                        right = SaveSystem.Describe(slot),
                        enabled = used,
                        detail = used ? "Saved " + SaveSystem.SavedAt(slot) + "." : "This slot is empty.",
                        onSelect = () => { if (SaveSystem.HasSave(slot)) LoadSlot(slot); }
                    });
                }
                list.Add(new Row { label = "Back", onSelect = () => GoTo(Page.Main) });
                break;

            case Page.ConfirmOverwrite:
                // "No" comes first, so pressing Enter twice by accident does not wipe a save.
                list.Add(new Row { label = "No, keep that save", onSelect = () => GoTo(Page.NewGameSlots) });
                list.Add(new Row { label = "Yes, start over in slot " + (pendingSlot + 1), right = SaveSystem.Describe(pendingSlot),
                                   detail = "The save in this slot will be replaced. This can't be undone.",
                                   onSelect = () => BeginNewGame(pendingSlot) });
                break;
        }
        return list;
    }

    // ------------------------------------------------------------------
    // What the choices do
    // ------------------------------------------------------------------

    private void BeginNewGame(int slot)
    {
        bool saved = SaveSystem.StartNewGame(slot);
        Close();
        GameEvents.ShowToast(saved ? "New game in slot " + (slot + 1) + ". The game saves itself every night."
                                   : "Started a new game, but it could not be saved. See the Console.");
    }

    private void LoadSlot(int slot)
    {
        if (SaveSystem.Load(slot))
        {
            busy = true;                 // the farm scene is reloading; this screen goes with the old scene
            message = "Loading...";
            Draw();
            return;
        }
        message = "That save could not be read. It may be damaged.";
        Draw();
    }

    private void Quit()
    {
        Application.Quit();
        if (Application.isEditor)
        {
            message = "Quit only works in a built game. Press Play again to stop.";
            Draw();
        }
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    private void Update()
    {
        if (busy || canvas == null || !canvas.enabled || Time.frameCount <= ignoreInputUntilFrame) return;

        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Mouse mouse = Mouse.current;

        bool up = Pressed(kb, Key.UpArrow, Key.W) || (pad != null && pad.dpad.up.wasPressedThisFrame);
        bool down = Pressed(kb, Key.DownArrow, Key.S) || (pad != null && pad.dpad.down.wasPressedThisFrame);
        bool choose = Pressed(kb, Key.Enter, Key.Space) || Pressed(kb, Key.E, Key.NumpadEnter) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        bool back = Pressed(kb, Key.Escape, Key.Backspace) || (pad != null && pad.buttonEast.wasPressedThisFrame);

        if (back && page != Page.Main)
        {
            GoTo(page == Page.ConfirmOverwrite ? Page.NewGameSlots : Page.Main);
            return;
        }
        if (rows.Count == 0) return;

        if (up) { selected = (selected - 1 + rows.Count) % rows.Count; message = ""; Draw(); }
        if (down) { selected = (selected + 1) % rows.Count; message = ""; Draw(); }

        // The mouse highlights the row it is over, and a click chooses it.
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            int over = RowUnder(mp);
            if ((mp - lastMouse).sqrMagnitude > 0.5f)
            {
                lastMouse = mp;
                if (over >= 0 && over != selected) { selected = over; message = ""; Draw(); }
            }
            if (over >= 0 && mouse.leftButton.wasPressedThisFrame) { selected = over; choose = true; }
        }

        if (!choose) return;
        Row row = rows[selected];
        if (!row.enabled)
        {
            message = row.detail;
            Draw();
            return;
        }
        if (row.onSelect != null) row.onSelect();
    }

    private int RowUnder(Vector2 screenPoint)
    {
        for (int i = 0; i < rows.Count && i < MaxRows; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(rowImages[i].rectTransform, screenPoint, null)) return i;
        return -1;
    }

    private static bool Pressed(Keyboard kb, Key a, Key b)
    {
        return kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame);
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void Draw()
    {
        for (int i = 0; i < MaxRows; i++)
        {
            bool exists = i < rows.Count;
            rowImages[i].gameObject.SetActive(exists);
            if (!exists) continue;

            Row r = rows[i];
            bool sel = i == selected;
            skin.PaintRow(rowImages[i], sel);                // honey highlighter behind the chosen row

            Color c = skin.Ink(textColor);
            if (!r.enabled) c = skin.Disabled(c);
            rowLabels[i].color = c;
            rowRights[i].color = r.enabled ? skin.fadedInk : skin.Disabled(skin.fadedInk);
            rowLabels[i].text = (sel ? "> " : "   ") + r.label;
            rowRights[i].text = r.right;
        }

        string detail = rows.Count > 0 ? rows[selected].detail : "";
        messageText.text = !string.IsNullOrEmpty(message) ? message : detail;
        hintText.text = page == Page.Main ? "Up/Down: choose     Enter: select" : "Up/Down: choose     Enter: select     Esc: back";
    }

    private void BuildUI()
    {
        skin = BookSkin.Current;

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;          // above everything else, the HUD included
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        Image backdrop = Img(root, backdropColor);
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = Vector2.zero;
        backdrop.rectTransform.offsetMax = Vector2.zero;

        // ---- the front cover of the book ----
        // It never gets taller than the screen (720 at the reference size), so everything on it stays visible.
        Vector2 size = new Vector2(coverSize.x, Mathf.Min(coverSize.y, 700f));
        Image cover = skin.NewImage(root, "Cover", BookSkin.Part.Cover);
        Place(cover.rectTransform, Vector2.zero, size);
        RectTransform c = cover.rectTransform;

        // Title and subtitle pressed in gold into the leather.
        Text title = Txt(c, gameTitle, 72, titleColor, TextAnchor.MiddleCenter);
        BookSkin.TopStrip(title.rectTransform, -28f, 96f);
        title.rectTransform.sizeDelta = new Vector2(-80f, 96f);
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.08f, 0.03f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Text sub = Txt(c, subtitle, 22, subtitleColor, TextAnchor.MiddleCenter);
        BookSkin.TopStrip(sub.rectTransform, -124f, 30f);
        sub.rectTransform.sizeDelta = new Vector2(-80f, 30f);
        var subShadow = sub.gameObject.AddComponent<Shadow>();
        subShadow.effectColor = new Color(0.2f, 0.08f, 0.03f, 0.9f);
        subShadow.effectDistance = new Vector2(1f, -1f);

        // ---- a paper page set into the cover, with the menu written on it ----
        const float plateTop = 170f, plateSide = 44f;
        float plateBottom = skin.coverBottomMargin + 6f;
        Image plate = skin.NewImage(c, "Page", BookSkin.Part.PageSingle);
        BookSkin.Inset(plate.rectTransform, Vector2.zero, Vector2.one, plateSide, plateBottom, plateSide, plateTop);

        RectTransform page = BookSkin.NewArea(plate.rectTransform, "Content");
        BookSkin.Inset(page, Vector2.zero, Vector2.one, skin.pageOuterPadding * 0.85f, skin.pageBottomPadding, skin.pageOuterPadding, skin.pageTopPadding);

        for (int i = 0; i < MaxRows; i++)
        {
            Image bg = Img(page, Color.clear);
            BookSkin.TopStrip(bg.rectTransform, -i * (RowHeight + RowGap), RowHeight);
            skin.AddRule(bg.rectTransform);
            rowImages[i] = bg;

            rowLabels[i] = Txt(bg.rectTransform, "", 24, skin.Ink(textColor), TextAnchor.MiddleLeft, false);
            rowLabels[i].rectTransform.anchorMin = Vector2.zero;
            rowLabels[i].rectTransform.anchorMax = new Vector2(0.62f, 1f);
            rowLabels[i].rectTransform.offsetMin = new Vector2(14f, 0f);
            rowLabels[i].rectTransform.offsetMax = Vector2.zero;

            rowRights[i] = Txt(bg.rectTransform, "", 17, skin.fadedInk, TextAnchor.MiddleRight, false);
            rowRights[i].rectTransform.anchorMin = new Vector2(0.38f, 0f);
            rowRights[i].rectTransform.anchorMax = Vector2.one;
            rowRights[i].rectTransform.offsetMin = Vector2.zero;
            rowRights[i].rectTransform.offsetMax = new Vector2(-14f, 0f);
        }

        // The description of the highlighted choice, at the bottom of the page.
        messageText = Txt(page, "", 18, skin.Ink(messageColor), TextAnchor.MiddleCenter, false);
        BookSkin.BottomStrip(messageText.rectTransform, 0f, 44f);

        // ---- key help in gold under the page ----
        hintText = skin.NewCoverHint(c, "", 15);
    }

    private static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
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

    /// <summary>Book-style text that shrinks to stay inside its box. 'onLeather' adds a dark shadow so gold text stands out.</summary>
    private Text Txt(RectTransform parent, string text, int size, Color color, TextAnchor anchor, bool onLeather)
    {
        Text t = skin.NewText(parent, "Text", size, color, anchor);
        t.text = text;
        if (onLeather)
        {
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.2f, 0.08f, 0.03f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }
        return t;
    }

    /// <summary>Text on the leather (the title lines), with no extra shadow (the caller adds its own outline).</summary>
    private Text Txt(RectTransform parent, string text, int size, Color color, TextAnchor anchor)
    {
        return Txt(parent, text, size, color, anchor, false);
    }
}
