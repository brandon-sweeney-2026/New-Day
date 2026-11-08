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

    [Header("Art (optional: Farm > UI > Apply HUD Art fills these in)")]
    [SerializeField] private Sprite forestPanel;        // bottom inventory frame
    [SerializeField] private Sprite forestSlot;
    [SerializeField] private Sprite forestSelected;
    [SerializeField] private Sprite leavesOrnament;
    [SerializeField] private Sprite mushroomOrnament;
    [SerializeField] private Sprite cityPanel;          // time / calendar frame
    [SerializeField] private Sprite citySkyline;
    [SerializeField] private Sprite cityLamp;
    [SerializeField] private Sprite mountainPanel;      // health / magic frame
    [SerializeField] private Sprite mountainPeaks;
    [SerializeField] private Sprite crystalOrnament;
    [SerializeField] private Sprite mountainSlot;
    [SerializeField] private Sprite barBack;
    [SerializeField] private Sprite barFill;
    [SerializeField] private Sprite portraitFace;       // the player's face
    [SerializeField] private Sprite portraitFrame;

    [Header("Digital clock (the HUD art menu fills these in)")]
    [SerializeField] private Texture2D lcdDigits;       // seven-segment digits
    [SerializeField] private Texture2D lcdFont;         // small LCD letters for the date
    [SerializeField] private Sprite lcdWindow;
    [SerializeField] private Sprite alarmBell;
    [SerializeField] private Color clockColor = new Color(1f, 0.25f, 0.19f);
    [SerializeField] private Color clockGhostColor = new Color(0.27f, 0.05f, 0.04f);
    [SerializeField] private Color dateColor = new Color(1f, 0.69f, 0.25f);
    [Tooltip("Colour of the money.")]
    [SerializeField] private Color goldColor = new Color(0.3f, 0.95f, 0.4f);

    [Header("Inventory position")]
    [Tooltip("The item bar lives at the bottom. While the player is where it would sit, it slides to the top, and returns to the bottom as soon as the player leaves that area.")]
    [SerializeField] private bool moveWhenCoveringPlayer = true;
    [SerializeField] private float slideSpeed = 2500f;
    [Tooltip("Extra space (screen units) kept around the player before the bar moves.")]
    [SerializeField] private float playerMargin = 6f;

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
    private Image equippedIcon;
    private Text equippedInitials;
    private readonly Image[] slotBackgrounds = new Image[Inventory.SlotCount];
    private readonly Image[] slotBorders = new Image[Inventory.SlotCount];
    private readonly Image[] slotIcons = new Image[Inventory.SlotCount];
    private readonly Text[] slotCounts = new Text[Inventory.SlotCount];
    private readonly Text[] slotLabels = new Text[Inventory.SlotCount];
    private readonly Image[] slotMarkers = new Image[Inventory.SlotCount];
    private readonly RectTransform[] slotRects = new RectTransform[Inventory.SlotCount];

    private LcdText timeLcd, ampmLcd, dateLcd;

    // item bar placement
    private RectTransform invRect, barsRect, infoRect;
    private float invWidth, invHeight, invExtra, invX, invY = float.NaN;
    private bool invAtTop;
    private Transform playerTf;
    private SpriteRenderer playerSr;

    private readonly Queue<string> toasts = new Queue<string>();
    private float toastUntil;

    private void Awake()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    private void Start()
    {
        inventory = Inventory.Instance != null ? Inventory.Instance : FindAnyObjectByType<Inventory>();
        stats = PlayerStats.Instance != null ? PlayerStats.Instance : FindAnyObjectByType<PlayerStats>();
        actions = FindAnyObjectByType<PlayerActions>();
        time = TimeManager.Instance != null ? TimeManager.Instance : FindAnyObjectByType<TimeManager>();

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
        if (timeLcd != null) timeLcd.Blink(((int)Time.unscaledTime) % 2 == 0);      // flashing colon
        UpdateToast();
        UpdateTargetInfo();
        UpdateInventoryPlacement();
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

        if (timeLcd != null)
        {
            int h = time.HourOfDay % 12;
            if (h == 0) h = 12;
            timeLcd.Set(h + ":" + time.Minute.ToString("00"), clockColor, "88 88", clockGhostColor);
            ampmLcd.Set(time.HourOfDay >= 12 ? "PM" : "AM", clockColor);
            dateLcd.Set(time.WeekDayName + " " + time.SeasonName + " " + time.Day, dateColor);
        }
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
        bool hasItem = !sel.IsEmpty;
        equippedIcon.enabled = hasItem && sel.item.icon != null;
        equippedIcon.sprite = hasItem ? sel.item.icon : null;
        equippedInitials.text = (hasItem && sel.item.icon == null) ? Short(sel.item.itemName) : "";

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
    // Item bar placement: slides to the top of the screen when it would cover the player
    // ------------------------------------------------------------------

    private void UpdateInventoryPlacement()
    {
        if (invRect == null) return;
        RectTransform rootRt = canvas.GetComponent<RectTransform>();
        float rootW = rootRt.rect.width, rootH = rootRt.rect.height;

        // Where the bar goes at the top: between the health panel and the clock panel.
        float minCenter = 0f, maxCenter = 0f;
        float leftLimit = barsRect != null ? barsRect.anchoredPosition.x + barsRect.sizeDelta.x + 8f : 0f;
        float rightLimit = infoRect != null ? rootW + infoRect.anchoredPosition.x - infoRect.sizeDelta.x - 8f : rootW;
        minCenter = leftLimit + invWidth * 0.5f - rootW * 0.5f;
        maxCenter = rightLimit - invWidth * 0.5f - rootW * 0.5f;
        float topX = minCenter <= maxCenter ? Mathf.Clamp(0f, minCenter, maxCenter) : 0f;

        float bottomY = 10f;
        float topY = rootH - 10f - invHeight - invExtra + 30f;          // keeps the leaves on screen

        if (moveWhenCoveringPlayer && TryGetPlayerRect(out Rect player))
        {
            player.xMin -= playerMargin; player.xMax += playerMargin;
            player.yMin -= playerMargin; player.yMax += playerMargin;

            Rect bottomRect = new Rect(rootW * 0.5f - invWidth * 0.5f, bottomY, invWidth, invHeight + invExtra);
            Rect topRect = new Rect(rootW * 0.5f + topX - invWidth * 0.5f, topY, invWidth, invHeight + invExtra);

            // The bottom is home. Go up only while the player is where the bar would sit; come straight back down as soon as they leave.
            bool hitsBottom = player.Overlaps(bottomRect);
            bool hitsTop = player.Overlaps(topRect);
            if (!hitsBottom) invAtTop = false;
            else if (!hitsTop) invAtTop = true;
            // (if the player is in both places, the bar stays where it is)
        }
        else if (!moveWhenCoveringPlayer) invAtTop = false;

        float targetX = invAtTop ? topX : 0f;
        float targetY = invAtTop ? topY : bottomY;
        if (float.IsNaN(invY)) { invX = targetX; invY = targetY; }
        invX = Mathf.MoveTowards(invX, targetX, slideSpeed * Time.unscaledDeltaTime);
        invY = Mathf.MoveTowards(invY, targetY, slideSpeed * Time.unscaledDeltaTime);

        invRect.anchoredPosition = new Vector2(invX, invY);

        // the equipped item's name stays with the bar (above it at the bottom, below it at the top)
        float textY = invAtTop ? invY - 28f : invY + invHeight + (forestPanel != null ? 30f : 4f);
        equippedText.rectTransform.anchoredPosition = new Vector2(invX, textY);
    }

    /// <summary>The player's sprite on screen, in the HUD's own units (bottom-left origin).</summary>
    private bool TryGetPlayerRect(out Rect rect)
    {
        rect = default(Rect);
        if (playerTf == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return false;
            playerTf = pc.transform;
            playerSr = pc.GetComponent<SpriteRenderer>();
        }
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 min, max;
        if (playerSr != null) { min = playerSr.bounds.min; max = playerSr.bounds.max; }
        else { min = playerTf.position + new Vector3(-0.5f, 0f, 0f); max = playerTf.position + new Vector3(0.5f, 1.5f, 0f); }

        Vector3 a = cam.WorldToScreenPoint(min);
        Vector3 b = cam.WorldToScreenPoint(max);
        if (a.z < 0f) return false;

        float k = 1f / canvas.scaleFactor;
        rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x) * k, Mathf.Min(a.y, b.y) * k, Mathf.Max(a.x, b.x) * k, Mathf.Max(a.y, b.y) * k);
        return true;
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

    // ------------------------------------------------------------------
    // Art helpers. Every picture is optional: leave a field empty and the plain colour boxes are used instead.
    // The art is drawn at 2 screen pixels per art pixel (set by Farm > UI > Apply HUD Art).
    // ------------------------------------------------------------------

    /// <summary>Gives an Image a picture. Sliced = the corners stay sharp while the middle stretches.</summary>
    private static void UseArt(Image img, Sprite sprite, bool sliced)
    {
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = Color.white;
        img.preserveAspect = false;
    }

    /// <summary>A decoration at its natural size. anchor/pivot are 0-1 corners of the parent; pos is in screen pixels.</summary>
    private Image Ornament(RectTransform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 pos, bool flip = false)
    {
        if (sprite == null) return null;
        Image img = MakeImage(parent, name, Color.white);
        img.sprite = sprite;
        img.SetNativeSize();
        RectTransform rt = img.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos;
        if (flip) rt.localScale = new Vector3(-1f, 1f, 1f);
        return img;
    }

    private float BarsTopMargin { get { return mountainPeaks != null ? 52f : 12f; } }
    private float BarsPanelHeight { get { return mountainPanel != null ? 100f : barHeight * 2f + 24f; } }

    private void BuildBars(RectTransform root)
    {
        bool art = mountainPanel != null;
        float portraitW = art ? 100f : 0f;
        float panelH = BarsPanelHeight;
        float panelW = art ? portraitW + barWidth + 30f : barWidth + 20f;
        float top = BarsTopMargin;

        var panel = MakeImage(root, "Bars", panelColor);
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -top), new Vector2(panelW, panelH));
        if (art) UseArt(panel, mountainPanel, true);
        barsRect = panel.rectTransform;

        if (art)
        {
            // snowy peaks rising behind the top edge, and crystals at the right end
            Ornament(panel.rectTransform, "Peaks", mountainPeaks, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(portraitW + 10f, -10f));
            Ornament(panel.rectTransform, "Crystals", crystalOrnament, new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-14f, -8f));
        }

        // Big picture of the equipped item, under the bars.
        var box = MakeImage(root, "Equipped Box", panelColor);
        SetRect(box.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -top - panelH - 6f), new Vector2(72f, 72f));
        if (art && mountainSlot != null) UseArt(box, mountainSlot, true);
        var boxInner = MakeImage(box.rectTransform, "Inner", slotColor);
        boxInner.rectTransform.anchorMin = Vector2.zero;
        boxInner.rectTransform.anchorMax = Vector2.one;
        boxInner.rectTransform.offsetMin = new Vector2(4f, 4f);
        boxInner.rectTransform.offsetMax = new Vector2(-4f, -4f);
        if (art && mountainSlot != null) boxInner.enabled = false;
        equippedIcon = MakeImage(box.rectTransform, "Equipped Icon", Color.white);
        equippedIcon.rectTransform.anchorMin = Vector2.zero;
        equippedIcon.rectTransform.anchorMax = Vector2.one;
        equippedIcon.rectTransform.offsetMin = new Vector2(10f, 10f);
        equippedIcon.rectTransform.offsetMax = new Vector2(-10f, -10f);
        equippedIcon.preserveAspect = true;
        equippedIcon.enabled = false;
        equippedInitials = MakeText(box.rectTransform, "Initials", 22, TextAnchor.MiddleCenter);
        equippedInitials.rectTransform.anchorMin = Vector2.zero;
        equippedInitials.rectTransform.anchorMax = Vector2.one;
        equippedInitials.rectTransform.offsetMin = Vector2.zero;
        equippedInitials.rectTransform.offsetMax = Vector2.zero;

        float barX = art ? portraitW + 6f : 10f;
        float y1 = art ? -(panelH - 2f * barHeight - 4f) * 0.5f : -8f;
        healthFill = MakeBar(panel.rectTransform, "Health", healthColor, barX, y1, out healthText);
        magicFill = MakeBar(panel.rectTransform, "Magic", magicColor, barX, y1 - barHeight - 4f, out magicText);

        if (art && portraitFace != null)
        {
            // the player's face, with its ornate ring on top
            float cy = -panelH * 0.5f;
            Ornament(panel.rectTransform, "Portrait", portraitFace, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(52f, cy));
            if (portraitFrame != null) Ornament(panel.rectTransform, "Portrait Frame", portraitFrame, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(52f, cy));
        }
    }

    private RectTransform MakeBar(RectTransform parent, string name, Color color, float x, float y, out Text label)
    {
        var back = MakeImage(parent, name + " Back", new Color(0f, 0f, 0f, 0.55f));
        SetRect(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(barWidth, barHeight));
        if (barBack != null) UseArt(back, barBack, true);

        // The fill lives in an inset area so it never covers the decorated edge of the bar.
        var area = new GameObject(name + " Fill Area", typeof(RectTransform));
        area.transform.SetParent(back.rectTransform, false);
        var areaRt = area.GetComponent<RectTransform>();
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = barFill != null ? new Vector2(4f, 4f) : Vector2.zero;
        areaRt.offsetMax = barFill != null ? new Vector2(-4f, -4f) : Vector2.zero;

        var fill = MakeImage(areaRt, name + " Fill", color);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        if (barFill != null) { fill.sprite = barFill; fill.type = Image.Type.Simple; fill.color = color; }

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
        bool art = cityPanel != null;
        bool lcd = art && lcdDigits != null && lcdFont != null;
        float w = art ? 250f : 230f;
        float h = lcd ? 164f : art ? 134f : 112f;
        float edge = art ? 26f : 10f;           // text keeps clear of the frame
        float top = art && alarmBell != null ? 36f : 12f;

        var panel = MakeImage(root, "Info", panelColor);
        SetRect(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -top), new Vector2(w, h));
        infoRect = panel.rectTransform;
        if (art)
        {
            UseArt(panel, cityPanel, true);
            // the skyline sits along the bottom, a street lamp stands at the left edge, alarm bells on top
            Image sky = Ornament(panel.rectTransform, "Skyline", citySkyline, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f));
            if (sky != null) sky.color = new Color(1f, 1f, 1f, 0.6f);
            Ornament(panel.rectTransform, "Lamp", cityLamp, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 2f));
            Ornament(panel.rectTransform, "Bell L", alarmBell, new Vector2(0f, 1f), new Vector2(0.5f, 0f), new Vector2(50f, -6f));
            Ornament(panel.rectTransform, "Bell R", alarmBell, new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(-50f, -6f), true);
        }

        clockText = MakeText(panel.rectTransform, "Clock", 26, TextAnchor.UpperRight);
        SetRect(clockText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-edge, art ? -20f : -6f), new Vector2(w - edge * 2f, 32f));

        dateText = MakeText(panel.rectTransform, "Date", 16, TextAnchor.UpperRight);
        SetRect(dateText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-edge, art ? -54f : -40f), new Vector2(w - edge * 2f, 22f));

        if (lcd)
        {
            // Digital alarm clock: dark glass window with big red seven-segment digits, AM/PM, and the date below in amber.
            clockText.enabled = false;
            dateText.enabled = false;

            Image window = MakeImage(panel.rectTransform, "Display Window", Color.white);
            if (lcdWindow != null) UseArt(window, lcdWindow, true); else window.color = Color.black;
            SetRect(window.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(w - 40f, 56f));

            timeLcd = new LcdText(window.rectTransform, "Time", lcdDigits, "0123456789: ", 11, 17, 2f, 5, true);
            SetRect(timeLcd.Rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), timeLcd.Rect.sizeDelta);
            timeLcd.SetBlinkIndex(2);

            ampmLcd = new LcdText(window.rectTransform, "AmPm", lcdFont, LcdCharset, 6, 8, 2f, 2, false);
            SetRect(ampmLcd.Rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f + 110f + 8f, -8f), ampmLcd.Rect.sizeDelta);

            // black glass boxes behind the date and the money so they read clearly
            Image dateBox = MakeImage(panel.rectTransform, "Date Box", Color.white);
            if (lcdWindow != null) UseArt(dateBox, lcdWindow, true); else dateBox.color = Color.black;
            SetRect(dateBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-edge, -84f), new Vector2(14f * 12f + 16f, 26f));

            dateLcd = new LcdText(panel.rectTransform, "DateLcd", lcdFont, LcdCharset, 6, 8, 2f, 14, true);
            SetRect(dateLcd.Rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-edge - 8f, -90f), dateLcd.Rect.sizeDelta);

            Image goldBox = MakeImage(panel.rectTransform, "Gold Box", Color.white);
            if (lcdWindow != null) UseArt(goldBox, lcdWindow, true); else goldBox.color = Color.black;
            SetRect(goldBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-edge, -114f), new Vector2(124f, 26f));
        }

        goldText = MakeText(panel.rectTransform, "Gold", 16, TextAnchor.UpperRight);
        goldText.color = goldColor;
        SetRect(goldText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(lcd ? -edge - 8f : -edge, lcd ? -119f : art ? -76f : -62f), new Vector2(lcd ? 108f : w - edge * 2f, 22f));

        if (art) { AddShadow(clockText); AddShadow(dateText); AddShadow(goldText); }

        pollutionText = MakeText(root, "Pollution", 14, TextAnchor.UpperRight);
        pollutionText.color = new Color(0.95f, 0.75f, 0.45f);
        SetRect(pollutionText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -top - h - 6f), new Vector2(300f, 22f));
        AddShadow(pollutionText);
    }

    private const string LcdCharset = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-:";

    private void BuildInventory(RectTransform root)
    {
        bool art = forestPanel != null;
        float pad = art ? 28f : 8f;
        float width = Inventory.Columns * slotSize + (Inventory.Columns - 1) * slotSpacing + pad * 2f;
        float height = Inventory.Rows * slotSize + (Inventory.Rows - 1) * slotSpacing + pad * 2f;

        var panel = MakeImage(root, "Inventory", panelColor);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(width, height));
        invRect = panel.rectTransform;
        invWidth = width;
        invHeight = height;
        invExtra = art ? 64f : 36f;           // ornaments and the equipped-item name above the bar
        if (art)
        {
            UseArt(panel, forestPanel, true);
            // leaves hang over the top corners, mushrooms grow on the frame
            Ornament(panel.rectTransform, "Leaves L", leavesOrnament, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(-10f, -34f));
            Ornament(panel.rectTransform, "Leaves R", leavesOrnament, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(10f, -34f), true);
            Ornament(panel.rectTransform, "Mushrooms L", mushroomOrnament, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(70f, -8f));
            Ornament(panel.rectTransform, "Mushrooms R", mushroomOrnament, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-70f, -8f), true);
        }

        equippedText = MakeText(root, "Equipped", 18, TextAnchor.MiddleCenter);
        SetRect(equippedText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f + height + (art ? 30f : 4f)), new Vector2(500f, 26f));
        AddShadow(equippedText);

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            int col = i % Inventory.Columns;
            int row = i / Inventory.Columns;      // row 0 = bottom (hotbar)

            float x = pad + col * (slotSize + slotSpacing);
            float y = pad + row * (slotSize + slotSpacing);

            var bg = MakeImage(panel.rectTransform, "Slot " + i, slotColor);
            SetRect(bg.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(slotSize, slotSize));
            if (art && forestSlot != null) UseArt(bg, forestSlot, true);
            slotBackgrounds[i] = bg;
            slotRects[i] = bg.rectTransform;

            var border = MakeImage(bg.rectTransform, "Selected", selectedColor);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            float grow = art && forestSelected != null ? 4f : 3f;
            border.rectTransform.offsetMin = new Vector2(-grow, -grow);
            border.rectTransform.offsetMax = new Vector2(grow, grow);
            if (art && forestSelected != null) UseArt(border, forestSelected, true);
            border.transform.SetAsFirstSibling();
            border.enabled = false;
            slotBorders[i] = border;

            // The border sits behind the slot background, so make the background a little inset by drawing it on top.
            var inner = MakeImage(bg.rectTransform, "Inner", slotColor);
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = Vector2.zero;
            inner.rectTransform.offsetMax = Vector2.zero;
            if (art && forestSlot != null) inner.enabled = false;

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
