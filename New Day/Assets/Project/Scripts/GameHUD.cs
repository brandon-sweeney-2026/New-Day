using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The whole on-screen interface, built in code so there is nothing to lay out by hand:
///   Top-left     health and magic bars
///   Top-right    time, day of the week, season, gold (and the pollution of the tile you're facing)
///   Bottom       two rows of ten inventory slots (bottom row = hotbar); the selected slot is the equipped item
///   Centre       short messages ("Sold 3 items for 120g")
///
/// Mouse: left-click a slot to equip it. Right-click a slot to swap it with the equipped slot.
/// Put this on an empty GameObject called "GameHUD". Colours and sizes are all fields below.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Leave empty to use Unity's built-in font. Drag a pixel font here for a nicer look.")]
    [SerializeField] private Font font;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1280f, 720f);
    [SerializeField] private float slotSize = 56f;
    [SerializeField] private float slotSpacing = 4f;
    [SerializeField] private Color panelColor = new Color(0.12f, 0.09f, 0.07f, 0.8f);
    [SerializeField] private Color slotColor = new Color(0.25f, 0.19f, 0.14f, 0.9f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color healthColor = new Color(0.85f, 0.2f, 0.25f);
    [SerializeField] private Color magicColor = new Color(0.25f, 0.5f, 0.95f);
    [SerializeField] private float barWidth = 220f;
    [SerializeField] private float barHeight = 20f;

    [Header("Messages")]
    [SerializeField] private float toastSeconds = 2.5f;

    // ---- scene links ----
    private Inventory inventory;
    private PlayerStats stats;
    private PlayerActions actions;
    private TimeManager time;

    // ---- UI pieces ----
    private Canvas canvas;
    private RectTransform healthFill, magicFill;
    private Text healthText, magicText, clockText, dateText, goldText, pollutionText;
    private Text equippedText, promptText, toastText;
    private readonly Image[] slotBackgrounds = new Image[Inventory.SlotCount];
    private readonly Image[] slotBorders = new Image[Inventory.SlotCount];
    private readonly Image[] slotIcons = new Image[Inventory.SlotCount];
    private readonly Text[] slotCounts = new Text[Inventory.SlotCount];
    private readonly Text[] slotLabels = new Text[Inventory.SlotCount];
    private readonly Image[] slotMarkers = new Image[Inventory.SlotCount];
    private readonly RectTransform[] slotRects = new RectTransform[Inventory.SlotCount];

    private readonly Queue<string> toasts = new Queue<string>();
    private float toastUntil;

    private void Awake()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    private void Start()
    {
        inventory = Inventory.Instance != null ? Inventory.Instance : FindFirstObjectByType<Inventory>();
        stats = PlayerStats.Instance != null ? PlayerStats.Instance : FindFirstObjectByType<PlayerStats>();
        actions = FindFirstObjectByType<PlayerActions>();
        time = TimeManager.Instance != null ? TimeManager.Instance : FindFirstObjectByType<TimeManager>();

        if (inventory != null) { inventory.OnChanged += RefreshInventory; inventory.OnSelectionChanged += RefreshInventory; }
        if (stats != null) stats.OnChanged += RefreshBars;
        if (time != null) time.OnMinuteChanged += RefreshClock;
        GameState.OnGoldChanged += OnGold;
        GameEvents.Toast += OnToast;

        RefreshInventory();
        RefreshBars();
        RefreshClock();
        OnGold(GameState.Gold);
    }

    private void OnDestroy()
    {
        if (inventory != null) { inventory.OnChanged -= RefreshInventory; inventory.OnSelectionChanged -= RefreshInventory; }
        if (stats != null) stats.OnChanged -= RefreshBars;
        if (time != null) time.OnMinuteChanged -= RefreshClock;
        GameState.OnGoldChanged -= OnGold;
        GameEvents.Toast -= OnToast;
    }

    private void Update()
    {
        HandleMouse();
        UpdateToast();
        UpdateTargetInfo();
    }

    // ------------------------------------------------------------------
    // Refreshing
    // ------------------------------------------------------------------

    private void RefreshBars()
    {
        if (stats == null) return;
        float h = stats.maxHealth > 0 ? Mathf.Clamp01(stats.Health / stats.maxHealth) : 0f;
        float m = stats.maxMagic > 0 ? Mathf.Clamp01(stats.Magic / stats.maxMagic) : 0f;
        healthFill.anchorMax = new Vector2(h, 1f);
        magicFill.anchorMax = new Vector2(m, 1f);
        healthText.text = "HP " + Mathf.CeilToInt(stats.Health) + "/" + stats.maxHealth;
        magicText.text = "MP " + Mathf.CeilToInt(stats.Magic) + "/" + Mathf.CeilToInt(stats.maxMagic);
    }

    private void RefreshClock()
    {
        if (time == null) return;
        clockText.text = time.GetClockString();
        dateText.text = time.WeekDayName + "  -  " + time.SeasonName + " " + time.Day;
    }

    private void OnGold(int gold)
    {
        goldText.text = gold + " g";
    }

    private void RefreshInventory()
    {
        if (inventory == null) return;

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            ItemStack s = inventory.GetSlot(i);
            bool has = s != null && !s.IsEmpty;

            slotBorders[i].enabled = i == inventory.SelectedIndex;

            slotIcons[i].enabled = has && s.item.icon != null;
            slotIcons[i].sprite = has ? s.item.icon : null;

            // No icon yet? Show the first letters of the name instead.
            slotLabels[i].text = (has && s.item.icon == null) ? Short(s.item.itemName) : (i < 10 ? ((i + 1) % 10).ToString() : "");
            slotLabels[i].alignment = (has && s.item.icon == null) ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;

            slotCounts[i].text = has && s.count > 1 ? s.count.ToString() : "";

            bool polluted = has && s.pollution > 0;
            slotMarkers[i].enabled = polluted;
            if (polluted) slotMarkers[i].color = PollutionSettings.Current.MarkerColor(s.pollution);
        }

        ItemStack sel = inventory.Selected;
        if (sel.IsEmpty) equippedText.text = "";
        else
        {
            string extra = sel.pollution > 0 ? "  [" + PollutionSettings.Current.Label(sel.pollution) + " pollution]" : "";
            equippedText.text = sel.item.itemName + extra;
        }
    }

    private void UpdateTargetInfo()
    {
        if (actions == null) return;

        string prompt = actions.ActionPrompt;
        promptText.text = string.IsNullOrEmpty(prompt) ? "" : "[Space]  " + prompt;

        string info = "";
        if (actions.HasTargetCell && PollutionMap.Instance != null)
        {
            int level = PollutionMap.Instance.GetLevel(actions.TargetCell);
            if (level > 0) info = "Soil: " + PollutionSettings.Current.Label(level) + " pollution (" + level + "/5)";
        }
        pollutionText.text = info;
    }

    // ------------------------------------------------------------------
    // Mouse on inventory slots
    // ------------------------------------------------------------------

    private void HandleMouse()
    {
        if (inventory == null || Mouse.current == null) return;

        bool left = Mouse.current.leftButton.wasPressedThisFrame;
        bool right = Mouse.current.rightButton.wasPressedThisFrame;
        if (!left && !right) return;

        Vector2 pos = Mouse.current.position.ReadValue();
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(slotRects[i], pos, null)) continue;

            if (left) inventory.Select(i);
            else inventory.Swap(i, inventory.SelectedIndex);
            return;
        }
    }

    // ------------------------------------------------------------------
    // Toasts
    // ------------------------------------------------------------------

    private void OnToast(string message)
    {
        // Don't queue the same message twice in a row (picking up a stack of items would spam).
        if (toasts.Count > 0 && toasts.ToArray()[toasts.Count - 1] == message) return;
        if (toasts.Count > 4) toasts.Dequeue();
        toasts.Enqueue(message);
    }

    private void UpdateToast()
    {
        if (Time.unscaledTime >= toastUntil)
        {
            if (toasts.Count > 0)
            {
                toastText.text = toasts.Dequeue();
                toastUntil = Time.unscaledTime + toastSeconds;
            }
            else
            {
                toastText.text = "";
            }
        }

        Color c = toastText.color;
        float remaining = toastUntil - Time.unscaledTime;
        c.a = Mathf.Clamp01(remaining / 0.4f);
        toastText.color = c;
    }

    // ------------------------------------------------------------------
    // Building the UI
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        var canvasGo = new GameObject("HUD Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasGo.GetComponent<RectTransform>();

        BuildBars(root);
        BuildInfoPanel(root);
        BuildInventory(root);

        toastText = MakeText(root, "Toast", 20, TextAnchor.MiddleCenter);
        SetRect(toastText.rectTransform, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 40f));
        toastText.color = new Color(1f, 1f, 1f, 0f);
        AddShadow(toastText);

        promptText = MakeText(root, "Prompt", 18, TextAnchor.MiddleCenter);
        SetRect(promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(500f, 30f));
        AddShadow(promptText);
    }

    private void BuildBars(RectTransform root)
    {
        var panel = MakeImage(root, "Bars", panelColor);
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(barWidth + 20f, barHeight * 2f + 24f));

        healthFill = MakeBar(panel.rectTransform, "Health", healthColor, -8f, out healthText);
        magicFill = MakeBar(panel.rectTransform, "Magic", magicColor, -8f - barHeight - 4f, out magicText);
    }

    private RectTransform MakeBar(RectTransform parent, string name, Color color, float y, out Text label)
    {
        var back = MakeImage(parent, name + " Back", new Color(0f, 0f, 0f, 0.55f));
        SetRect(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(barWidth, barHeight));

        var fill = MakeImage(back.rectTransform, name + " Fill", color);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        label = MakeText(back.rectTransform, name + " Text", 14, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        AddShadow(label);

        return fill.rectTransform;
    }

    private void BuildInfoPanel(RectTransform root)
    {
        var panel = MakeImage(root, "Info", panelColor);
        SetRect(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(230f, 112f));

        clockText = MakeText(panel.rectTransform, "Clock", 26, TextAnchor.UpperRight);
        SetRect(clockText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(210f, 32f));

        dateText = MakeText(panel.rectTransform, "Date", 16, TextAnchor.UpperRight);
        SetRect(dateText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -40f), new Vector2(210f, 22f));

        goldText = MakeText(panel.rectTransform, "Gold", 16, TextAnchor.UpperRight);
        goldText.color = new Color(1f, 0.88f, 0.35f);
        SetRect(goldText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -62f), new Vector2(210f, 22f));

        pollutionText = MakeText(root, "Pollution", 14, TextAnchor.UpperRight);
        pollutionText.color = new Color(0.95f, 0.75f, 0.45f);
        SetRect(pollutionText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -130f), new Vector2(300f, 22f));
        AddShadow(pollutionText);
    }

    private void BuildInventory(RectTransform root)
    {
        float width = Inventory.Columns * slotSize + (Inventory.Columns - 1) * slotSpacing + 16f;
        float height = Inventory.Rows * slotSize + (Inventory.Rows - 1) * slotSpacing + 16f;

        var panel = MakeImage(root, "Inventory", panelColor);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(width, height));

        equippedText = MakeText(root, "Equipped", 18, TextAnchor.MiddleCenter);
        SetRect(equippedText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f + height + 4f), new Vector2(500f, 26f));
        AddShadow(equippedText);

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            int col = i % Inventory.Columns;
            int row = i / Inventory.Columns;      // row 0 = bottom (hotbar)

            float x = 8f + col * (slotSize + slotSpacing);
            float y = 8f + row * (slotSize + slotSpacing);

            var bg = MakeImage(panel.rectTransform, "Slot " + i, slotColor);
            SetRect(bg.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(slotSize, slotSize));
            slotBackgrounds[i] = bg;
            slotRects[i] = bg.rectTransform;

            var border = MakeImage(bg.rectTransform, "Selected", selectedColor);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = new Vector2(-3f, -3f);
            border.rectTransform.offsetMax = new Vector2(3f, 3f);
            border.transform.SetAsFirstSibling();
            border.enabled = false;
            slotBorders[i] = border;

            // The border sits behind the slot background, so make the background a little inset by drawing it on top.
            var inner = MakeImage(bg.rectTransform, "Inner", slotColor);
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = Vector2.zero;
            inner.rectTransform.offsetMax = Vector2.zero;

            var icon = MakeImage(bg.rectTransform, "Icon", Color.white);
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(6f, 6f);
            icon.rectTransform.offsetMax = new Vector2(-6f, -6f);
            icon.preserveAspect = true;
            icon.enabled = false;
            slotIcons[i] = icon;

            var label = MakeText(bg.rectTransform, "Label", 12, TextAnchor.UpperLeft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(3f, 2f);
            label.rectTransform.offsetMax = new Vector2(-3f, -2f);
            label.color = new Color(1f, 1f, 1f, 0.8f);
            slotLabels[i] = label;

            var count = MakeText(bg.rectTransform, "Count", 14, TextAnchor.LowerRight);
            count.rectTransform.anchorMin = Vector2.zero;
            count.rectTransform.anchorMax = Vector2.one;
            count.rectTransform.offsetMin = new Vector2(2f, 1f);
            count.rectTransform.offsetMax = new Vector2(-3f, -2f);
            AddShadow(count);
            slotCounts[i] = count;

            var marker = MakeImage(bg.rectTransform, "Pollution", Color.white);
            SetRect(marker.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-3f, -3f), new Vector2(10f, 10f));
            marker.enabled = false;
            slotMarkers[i] = marker;
        }
    }

    // ---- small UI helpers ----

    private Image MakeImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Text MakeText(RectTransform parent, string name, int size, TextAnchor anchor)
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

    private static void AddShadow(Text t)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.8f);
        s.effectDistance = new Vector2(1f, -1f);
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private static string Short(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        string[] words = name.Split(' ');
        if (words.Length >= 2) return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
        return name.Length <= 3 ? name : name.Substring(0, 3);
    }
}
