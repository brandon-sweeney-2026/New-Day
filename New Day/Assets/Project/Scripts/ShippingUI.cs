using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The shipping screen: the mailbox's 10 x 2 grid on top, your backpack (10 x 2) below it, and the total in green at the bottom.
/// Move things between the two (nothing is sold yet; the box is emptied and paid out at the end of the day).
///
/// Mouse:  left-click = move the whole stack,  right-click = move one.
/// Keys:   arrows / WASD move the cursor,  Space / Enter / E / J = move the stack,  X = move one,  Esc / Q / Tab = close.
/// Gamepad: D-pad, A = move stack, X = move one, B = close.
/// Built by code, so there is nothing to set up in the scene.
/// </summary>
public class ShippingUI : MonoBehaviour
{
    private static ShippingUI instance;

    private const float SlotSize = 56f, Gap = 4f;      // the book page is sized around the grids, so they always fit
    private const int Cells = ShippingBin.SlotCount * 2;      // 20 mailbox cells then 20 backpack cells

    private class Cell
    {
        public RectTransform rect;
        public Image bg, icon, marker;
        public Text count, label;
    }

    private ShippingBin bin;
    private Inventory inv;
    private Canvas canvas;
    private Font font;
    private BookSkin skin;      // the storybook look (Assets/Resources/BookSkin.asset)

    private Image panelImg, totalBox, envL, envR, select;
    private Text titleText, binLabel, packLabel, infoText, totalLabel, totalValue, hintText;
    private readonly Cell[] cells = new Cell[Cells];
    private int cursor = ShippingBin.Columns * ShippingBin.Rows;   // starts on the backpack
    private bool locked;
    private int ignoreInputUntilFrame;
    private Vector2 lastMouse;

    // ------------------------------------------------------------------
    // Opening and closing
    // ------------------------------------------------------------------

    public static void Open(ShippingBin bin, Inventory inventory)
    {
        if (instance == null)
        {
            var go = new GameObject("ShippingUI");
            instance = go.AddComponent<ShippingUI>();
            instance.BuildUI();
        }
        instance.Show(bin, inventory);
    }

    public static bool IsOpen { get { return instance != null && instance.canvas != null && instance.canvas.enabled; } }

    private void Show(ShippingBin b, Inventory i)
    {
        bin = b; inv = i;
        ApplyArt();
        ignoreInputUntilFrame = Time.frameCount + 1;      // the press that opened this must not also move an item
        if (!locked)
        {
            locked = true;
            GameState.LockInput();
            if (TimeManager.Instance != null) TimeManager.Instance.Pause();
        }
        canvas.enabled = true;
        if (Mouse.current != null) lastMouse = Mouse.current.position.ReadValue();
        RefreshAll();
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
        if (!canvas.enabled || bin == null || inv == null) return;
        if (Time.frameCount < ignoreInputUntilFrame) return;

        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Mouse mouse = Mouse.current;

        bool back = Pressed(kb, Key.Escape, Key.Backspace) || Pressed(kb, Key.Q, Key.Tab) || (pad != null && pad.buttonEast.wasPressedThisFrame);
        if (back) { Close(); return; }

        // cursor movement
        int col = cursor % ShippingBin.Columns, row = cursor / ShippingBin.Columns;
        int rows = Cells / ShippingBin.Columns;
        bool up = Pressed(kb, Key.UpArrow, Key.W) || (pad != null && pad.dpad.up.wasPressedThisFrame);
        bool down = Pressed(kb, Key.DownArrow, Key.S) || (pad != null && pad.dpad.down.wasPressedThisFrame);
        bool left = Pressed(kb, Key.LeftArrow, Key.A) || (pad != null && pad.dpad.left.wasPressedThisFrame);
        bool right = Pressed(kb, Key.RightArrow, Key.D) || (pad != null && pad.dpad.right.wasPressedThisFrame);
        if (up) row = Mathf.Max(0, row - 1);
        if (down) row = Mathf.Min(rows - 1, row + 1);
        if (left) col = Mathf.Max(0, col - 1);
        if (right) col = Mathf.Min(ShippingBin.Columns - 1, col + 1);
        int moved = row * ShippingBin.Columns + col;

        // mouse hover
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            if ((mp - lastMouse).sqrMagnitude > 0.5f)
            {
                lastMouse = mp;
                for (int i = 0; i < Cells; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(cells[i].rect, mp, null)) { moved = i; break; }
            }
        }
        if (moved != cursor) { cursor = moved; UpdateCursor(); UpdateInfo(); }

        // actions
        bool stack = Pressed(kb, Key.Space, Key.Enter) || Pressed(kb, Key.E, Key.J) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        bool one = (kb != null && kb[Key.X].wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            bool overCell = false;
            for (int i = 0; i < Cells; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[i].rect, mp, null)) { cursor = i; overCell = true; break; }
            if (overCell && mouse.leftButton.wasPressedThisFrame) stack = true;
            if (overCell && mouse.rightButton.wasPressedThisFrame) one = true;
        }

        if (stack) Move(cursor, true);
        else if (one) Move(cursor, false);
    }

    private static bool Pressed(Keyboard kb, Key a, Key b)
    {
        return kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame);
    }

    // ------------------------------------------------------------------
    // Moving items
    // ------------------------------------------------------------------

    private int InvIndexOf(int cell)
    {
        int k = cell - ShippingBin.SlotCount;                 // 0..19, top row first
        int c = k % ShippingBin.Columns;
        return (k / ShippingBin.Columns == 0) ? Inventory.Columns + c : c;     // top row = upper backpack row, bottom row = hotbar
    }

    private ItemStack StackAt(int cell)
    {
        return cell < ShippingBin.SlotCount ? ShippingBin.Pending[cell] : inv.GetSlot(InvIndexOf(cell));
    }

    private void Move(int cell, bool wholeStack)
    {
        ItemStack s = StackAt(cell);
        if (s == null || s.IsEmpty) return;

        ItemData item = s.item;
        int pollution = s.pollution;
        int quality = s.quality;
        int amount = wholeStack ? s.count : 1;

        if (cell < ShippingBin.SlotCount)
        {
            // mailbox -> backpack
            int left = inv.Add(item, amount, pollution, quality);
            int moved = amount - left;
            if (moved <= 0) { GameEvents.ShowToast("Your backpack is full."); return; }
            bin.RemoveFromSlot(cell, moved);
        }
        else
        {
            // backpack -> mailbox
            if (!bin.Accepts(item)) { GameEvents.ShowToast("The mailbox won't take " + item.itemName + "."); return; }
            int left = bin.TryAdd(item, amount, pollution, quality);
            int moved = amount - left;
            if (moved <= 0) { GameEvents.ShowToast("The mailbox is full."); return; }
            inv.RemoveFromSlot(InvIndexOf(cell), moved);
        }
        RefreshAll();
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void RefreshAll()
    {
        for (int i = 0; i < Cells; i++)
        {
            ItemStack s = StackAt(i);
            Cell c = cells[i];
            bool has = s != null && !s.IsEmpty;

            bool hasIcon = has && s.item.icon != null;
            c.icon.enabled = hasIcon;
            if (hasIcon) c.icon.sprite = s.item.icon;
            CropQuality.Band(c.icon, has ? s : null);       // the coloured band around a crop shows its quality
            c.label.text = has && !hasIcon ? Short(s.item.itemName) : "";
            c.count.text = has && s.count > 1 ? s.count.ToString() : "";

            bool polluted = has && s.pollution > 0;
            c.marker.enabled = polluted;
            if (polluted) c.marker.color = PollutionSettings.Current.MarkerColor(s.pollution);
        }

        totalValue.text = bin.Total().ToString("N0") + " G";
        totalValue.color = skin.Ink(bin.TotalColor);      // the mailbox's colour, darkened to ink so it reads on paper
        titleText.text = "Mailbox";
        UpdateCursor();
        UpdateInfo();
    }

    private void UpdateCursor()
    {
        select.rectTransform.SetParent(cells[cursor].rect, false);
        select.rectTransform.anchorMin = Vector2.zero;
        select.rectTransform.anchorMax = Vector2.one;
        select.rectTransform.offsetMin = new Vector2(-4f, -4f);
        select.rectTransform.offsetMax = new Vector2(4f, 4f);
        select.rectTransform.SetAsFirstSibling();
        // keep the ring above the slot background but under the icon
        select.rectTransform.SetSiblingIndex(1);
    }

    private void UpdateInfo()
    {
        ItemStack s = StackAt(cursor);
        if (s == null || s.IsEmpty)
        {
            infoText.text = cursor < ShippingBin.SlotCount ? "Empty mailbox slot" : "Empty slot";
            return;
        }
        int each = bin.PriceEach(s);
        string pol = s.pollution > 0 ? "  [" + PollutionSettings.Current.Label(s.pollution) + " pollution]" : "";
        if (s.item.type == ItemData.ItemType.Crop) pol = "  [" + CropQuality.Name(s.quality) + "]" + pol;
        string ship = bin.Accepts(s.item) ? "" : "   (can't be shipped)";
        infoText.text = s.item.itemName + " x" + s.count + pol + "\n" + each + "g each   =   " + (each * s.count) + "g" + ship;
    }

    private void ApplyArt()
    {
        // The window is a book page (BookSkin). The mailbox's own envelope picture still decorates the title.
        bool env = bin.EnvelopeSprite != null;
        envL.enabled = env; envR.enabled = env;
        if (env)
        {
            envL.sprite = bin.EnvelopeSprite; envR.sprite = bin.EnvelopeSprite;
            envL.SetNativeSize(); envR.SetNativeSize();
        }
    }

    // ------------------------------------------------------------------
    // Building the screen
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        skin = BookSkin.Current;
        font = skin.Font;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        // dim the game behind
        Image dim = Img(root, "Dim", skin.dimColor);
        Stretch(dim.rectTransform);

        // ---- the page: sized so both grids, the labels and the bottom row fit inside it ----
        float gridW = ShippingBin.Columns * SlotSize + (ShippingBin.Columns - 1) * Gap;
        float gridH = ShippingBin.Rows * SlotSize + (ShippingBin.Rows - 1) * Gap;
        float bannerH = skin.NaturalHeight(BookSkin.Part.Banner, 40f);
        float binTop = bannerH + 8f + 24f;                 // under the title and the "outgoing mail" label
        float packLabelTop = binTop + gridH + 10f;
        float packTop = packLabelTop + 24f;
        float rowTop = packTop + gridH + 12f;               // the info text and the total
        const float bottomRowH = 54f;
        float contentW = gridW + 8f;
        float contentH = rowTop + bottomRowH;

        BookSkin.Book book = skin.BuildBook(root, "Book", false);
        Place(book.root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, skin.CoverSizeFor(false, contentW, contentH));
        RectTransform panel = book.leftContent;
        panelImg = book.coverImage;
        float x0 = (contentW - gridW) * 0.5f;

        titleText = skin.NewBanner(panel, "Title", 300f, 0f, 24);

        envL = Img(panel, "Envelope L", Color.white);
        Place(envL.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-200f, -bannerH * 0.5f), new Vector2(48f, 32f));
        envR = Img(panel, "Envelope R", Color.white);
        Place(envR.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(200f, -bannerH * 0.5f), new Vector2(48f, 32f));
        envR.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        binLabel = Txt(panel, "Bin Label", 15, TextAnchor.LowerLeft);
        binLabel.text = "Outgoing mail  (shipped and paid at the end of the day)";
        binLabel.color = skin.headingInk;
        Place(binLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0, -binTop + 26f), new Vector2(gridW, 22f));

        packLabel = Txt(panel, "Pack Label", 15, TextAnchor.LowerLeft);
        packLabel.text = "Your backpack";
        packLabel.color = skin.headingInk;
        Place(packLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0, -packLabelTop), new Vector2(gridW, 22f));

        for (int i = 0; i < Cells; i++)
        {
            int k = i % ShippingBin.SlotCount;
            int c = k % ShippingBin.Columns, r = k / ShippingBin.Columns;
            float top = (i < ShippingBin.SlotCount ? binTop : packTop) + r * (SlotSize + Gap);
            cells[i] = MakeCell(panel, i, x0 + c * (SlotSize + Gap), -top);
        }

        select = skin.NewImage(panel, "Cursor", BookSkin.Part.SlotSelected);   // re-parented onto the active cell by UpdateCursor

        // the bottom row: info about the item under the cursor, and the total
        const float totalW = 236f;
        infoText = Txt(panel, "Info", 16, TextAnchor.UpperLeft);
        infoText.color = skin.ink;
        Place(infoText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0 + 4f, -rowTop - 2f), new Vector2(gridW - totalW - 16f, bottomRowH - 4f));

        totalBox = skin.NewImage(panel, "Total Box", BookSkin.Part.LabelBox);
        Place(totalBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-x0, -rowTop), new Vector2(totalW, bottomRowH - 4f));
        totalLabel = Txt(totalBox.rectTransform, "Total Label", 14, TextAnchor.MiddleLeft);
        totalLabel.text = "TOTAL";
        totalLabel.color = skin.fadedInk;
        Place(totalLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(60f, 30f));
        totalValue = Txt(totalBox.rectTransform, "Total Value", 28, TextAnchor.MiddleRight);
        totalValue.color = skin.goldInk;
        Place(totalValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(150f, 40f));

        // key help, pressed in gold into the leather under the page
        hintText = skin.NewCoverHint(book.root, "Click or Space: move stack     Right-click or X: move one     Esc: close", 14);

        canvas.enabled = false;
    }

    private Cell MakeCell(RectTransform parent, int index, float x, float y)
    {
        var cell = new Cell();
        cell.bg = skin.NewImage(parent, "Cell " + index, BookSkin.Part.Slot);   // an inked box on the page
        cell.rect = cell.bg.rectTransform;
        Place(cell.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(SlotSize, SlotSize));
        cell.rect.pivot = new Vector2(0f, 1f);

        cell.icon = Img(cell.rect, "Icon", Color.white);
        Stretch(cell.icon.rectTransform, 6f);
        cell.icon.preserveAspect = true;
        cell.icon.enabled = false;

        cell.label = Txt(cell.rect, "Label", 12, TextAnchor.MiddleCenter);
        Stretch(cell.label.rectTransform, 2f);
        cell.label.color = skin.fadedInk;

        // The count is dark ink with a paper-coloured edge so it reads on top of any picture.
        cell.count = Txt(cell.rect, "Count", 14, TextAnchor.LowerRight);
        Stretch(cell.count.rectTransform, 3f);
        cell.count.color = skin.ink;
        var edge = cell.count.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(skin.paperColor.r, skin.paperColor.g, skin.paperColor.b, 0.9f);
        edge.effectDistance = new Vector2(1f, -1f);

        cell.marker = Img(cell.rect, "Pollution", Color.white);
        Place(cell.marker.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-3f, -3f), new Vector2(10f, 10f));
        cell.marker.rectTransform.pivot = new Vector2(1f, 1f);
        cell.marker.enabled = false;
        return cell;
    }

    // ---- small helpers ----

    private static Image Img(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Book-style text (shrinks to stay inside its box).</summary>
    private Text Txt(RectTransform parent, string name, int size, TextAnchor anchor)
    {
        Text t = skin.NewText(parent, name, size, skin.ink, anchor);
        t.font = font;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static string Short(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        string[] words = name.Split(' ');
        if (words.Length >= 2) return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
        return name.Length <= 3 ? name : name.Substring(0, 3);
    }
}
