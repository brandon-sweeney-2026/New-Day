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

    private const float SlotSize = 56f, Gap = 4f;
    private const float PanelW = 660f, PanelH = 506f;
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
        int amount = wholeStack ? s.count : 1;

        if (cell < ShippingBin.SlotCount)
        {
            // mailbox -> backpack
            int left = inv.Add(item, amount, pollution);
            int moved = amount - left;
            if (moved <= 0) { GameEvents.ShowToast("Your backpack is full."); return; }
            bin.RemoveFromSlot(cell, moved);
        }
        else
        {
            // backpack -> mailbox
            if (!bin.Accepts(item)) { GameEvents.ShowToast("The mailbox won't take " + item.itemName + "."); return; }
            int left = bin.TryAdd(item, amount, pollution);
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
            c.label.text = has && !hasIcon ? Short(s.item.itemName) : "";
            c.count.text = has && s.count > 1 ? s.count.ToString() : "";

            bool polluted = has && s.pollution > 0;
            c.marker.enabled = polluted;
            if (polluted) c.marker.color = PollutionSettings.Current.MarkerColor(s.pollution);
        }

        totalValue.text = bin.Total().ToString("N0") + " G";
        totalValue.color = bin.TotalColor;
        titleText.text = "MAILBOX";
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
        string ship = bin.Accepts(s.item) ? "" : "   (can't be shipped)";
        infoText.text = s.item.itemName + " x" + s.count + pol + "\n" + each + "g each   =   " + (each * s.count) + "g" + ship;
    }

    private void ApplyArt()
    {
        if (bin.PanelSprite != null) { panelImg.sprite = bin.PanelSprite; panelImg.type = Image.Type.Sliced; panelImg.color = Color.white; }
        else { panelImg.sprite = null; panelImg.color = new Color(0.1f, 0.16f, 0.32f, 0.97f); }

        for (int i = 0; i < Cells; i++)
        {
            if (bin.SlotSprite != null) { cells[i].bg.sprite = bin.SlotSprite; cells[i].bg.type = Image.Type.Sliced; cells[i].bg.color = Color.white; }
            else { cells[i].bg.sprite = null; cells[i].bg.color = new Color(0.08f, 0.12f, 0.26f, 1f); }
        }

        if (bin.SelectSprite != null) { select.sprite = bin.SelectSprite; select.type = Image.Type.Sliced; select.color = Color.white; }
        else { select.sprite = null; select.color = new Color(1f, 1f, 1f, 0.5f); }

        if (bin.TotalBoxSprite != null) { totalBox.sprite = bin.TotalBoxSprite; totalBox.type = Image.Type.Sliced; totalBox.color = Color.white; }
        else { totalBox.sprite = null; totalBox.color = Color.black; }

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
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        Image dim = Img(root, "Dim", new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim.rectTransform);

        panelImg = Img(root, "Panel", Color.white);
        Place(panelImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH));
        RectTransform panel = panelImg.rectTransform;

        titleText = Txt(panel, "Title", 28, TextAnchor.MiddleCenter);
        Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(300f, 36f));
        Shadow(titleText);

        envL = Img(panel, "Envelope L", Color.white);
        Place(envL.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-130f, -26f), new Vector2(48f, 32f));
        envR = Img(panel, "Envelope R", Color.white);
        Place(envR.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(130f, -26f), new Vector2(48f, 32f));
        envR.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        float gridW = ShippingBin.Columns * SlotSize + (ShippingBin.Columns - 1) * Gap;
        float x0 = (PanelW - gridW) * 0.5f;
        float gridH = ShippingBin.Rows * SlotSize + (ShippingBin.Rows - 1) * Gap;

        binLabel = Txt(panel, "Bin Label", 15, TextAnchor.LowerLeft);
        binLabel.text = "OUTGOING MAIL  (shipped and paid at the end of the day)";
        binLabel.color = new Color(0.8f, 0.9f, 1f);
        Place(binLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0, -50f), new Vector2(gridW, 22f));
        Shadow(binLabel);

        float binTop = 76f;
        float packLabelTop = binTop + gridH + 8f;
        float packTop = packLabelTop + 26f;

        packLabel = Txt(panel, "Pack Label", 15, TextAnchor.LowerLeft);
        packLabel.text = "YOUR BACKPACK";
        packLabel.color = new Color(0.8f, 0.9f, 1f);
        Place(packLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0, -packLabelTop), new Vector2(gridW, 22f));
        Shadow(packLabel);

        for (int i = 0; i < Cells; i++)
        {
            int k = i % ShippingBin.SlotCount;
            int c = k % ShippingBin.Columns, r = k / ShippingBin.Columns;
            float top = (i < ShippingBin.SlotCount ? binTop : packTop) + r * (SlotSize + Gap);
            cells[i] = MakeCell(panel, i, x0 + c * (SlotSize + Gap), -top);
        }

        select = Img(panel, "Cursor", Color.white);   // re-parented onto the active cell by UpdateCursor

        // the bottom row: info about the item under the cursor, and the total
        float rowTop = packTop + gridH + 14f;

        infoText = Txt(panel, "Info", 16, TextAnchor.UpperLeft);
        infoText.color = new Color(0.9f, 0.95f, 1f);
        Place(infoText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0 + 4f, -rowTop - 2f), new Vector2(330f, 50f));
        Shadow(infoText);

        totalBox = Img(panel, "Total Box", Color.black);
        Place(totalBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-x0, -rowTop), new Vector2(236f, 50f));
        totalLabel = Txt(totalBox.rectTransform, "Total Label", 14, TextAnchor.MiddleLeft);
        totalLabel.text = "TOTAL";
        totalLabel.color = new Color(0.7f, 0.8f, 0.7f);
        Place(totalLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(60f, 30f));
        totalValue = Txt(totalBox.rectTransform, "Total Value", 28, TextAnchor.MiddleRight);
        totalValue.color = new Color(0.3f, 0.95f, 0.4f);
        Place(totalValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(160f, 40f));

        hintText = Txt(panel, "Hint", 13, TextAnchor.MiddleCenter);
        hintText.text = "Click or Space: move stack     Right-click or X: move one     Esc: close";
        hintText.color = new Color(0.7f, 0.8f, 0.95f);
        Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(PanelW - 40f, 22f));

        canvas.enabled = false;
    }

    private Cell MakeCell(RectTransform parent, int index, float x, float y)
    {
        var cell = new Cell();
        cell.bg = Img(parent, "Cell " + index, new Color(0.08f, 0.12f, 0.26f, 1f));
        cell.rect = cell.bg.rectTransform;
        Place(cell.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(SlotSize, SlotSize));
        cell.rect.pivot = new Vector2(0f, 1f);

        cell.icon = Img(cell.rect, "Icon", Color.white);
        Stretch(cell.icon.rectTransform, 6f);
        cell.icon.preserveAspect = true;
        cell.icon.enabled = false;

        cell.label = Txt(cell.rect, "Label", 12, TextAnchor.MiddleCenter);
        Stretch(cell.label.rectTransform, 2f);
        cell.label.color = new Color(1f, 1f, 1f, 0.8f);

        cell.count = Txt(cell.rect, "Count", 14, TextAnchor.LowerRight);
        Stretch(cell.count.rectTransform, 3f);
        Shadow(cell.count);

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

    private static void Shadow(Text t)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.8f);
        s.effectDistance = new Vector2(1f, -1f);
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
