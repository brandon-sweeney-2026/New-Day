using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// All the screens of fishing, built from code (no setup needed): choose bait, wait for a bite, the rhythm fight, and the result.
/// The fight is a side-scroller: notes (buttons) swim in from the RIGHT and you press the matching button as each one reaches the ring on the LEFT.
/// Hit = stamina goes up. Miss = stamina goes down. Reach 100 and the fish is yours; hit 0 (or run out of notes first) and it escapes.
/// </summary>
public class FishingUI : MonoBehaviour
{
    private static FishingUI instance;
    public static FishingUI Get()
    {
        if (instance == null)
        {
            var go = new GameObject("FishingUI");
            instance = go.AddComponent<FishingUI>();
            instance.BuildCanvas();
        }
        return instance;
    }

    // ---- look (change freely) ----
    private static readonly Color[] LaneColors =
    {
        new Color(0.35f, 0.86f, 1f),    // up
        new Color(1f, 0.67f, 0.24f),    // down
        new Color(0.74f, 0.47f, 1f),    // left
        new Color(1f, 0.43f, 0.67f),    // right
        new Color(0.43f, 0.9f, 0.47f),  // action 1
        new Color(1f, 0.35f, 0.35f),    // action 2
    };
    // easy fish use the first lanes of this list (Left, Right, Up, Down, Action 1, Action 2)
    private static readonly int[] LaneOrder = { 2, 3, 0, 1, 4, 5 };

    private const float HitX = -380f;          // where notes must be pressed (inside the panel)
    private const float StartX = 600f;          // where notes appear (off the right edge, clipped)
    private const float TrackY = -10f;

    private Canvas canvas;
    private RectTransform root;
    private Font font;
    private static Sprite[] arrowSprites;

    // ------------------------------------------------------------------ canvas
    private void BuildCanvas()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cg = new GameObject("FishingCanvas");
        cg.transform.SetParent(transform, false);
        canvas = cg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        var scaler = cg.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var rg = new GameObject("Root", typeof(RectTransform));
        root = rg.GetComponent<RectTransform>();
        root.SetParent(cg.transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
        cg.SetActive(false);
    }

    public void Begin() { canvas.gameObject.SetActive(true); Clear(); }
    public void End() { Clear(); canvas.gameObject.SetActive(false); }

    private void Clear()
    {
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
    }

    // ------------------------------------------------------------------ little builders
    private RectTransform Rt(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image Img(string name, Transform parent, Vector2 pos, Vector2 size, Color c, Sprite s = null)
    {
        var im = Rt(name, parent, pos, size).gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        return im;
    }

    private Text Txt(string name, Transform parent, string s, int fontSize, Color c, Vector2 pos, Vector2 size, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var t = Rt(name, parent, pos, size).gameObject.AddComponent<Text>();
        t.font = font; t.text = s; t.fontSize = fontSize; t.color = c; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void Stars(Transform parent, Vector2 center, int count, int total, float size = 16f)
    {
        float gap = size + 5f;
        float x0 = center.x - (total - 1) * gap * 0.5f;
        for (int i = 0; i < total; i++)
            Img("Star", parent, new Vector2(x0 + i * gap, center.y), new Vector2(size, size),
                i < count ? new Color(1f, 0.85f, 0.25f) : new Color(0f, 0f, 0f, 0.35f), RuntimeSprites.Circle);
    }

    private static bool Confirm()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame)) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private static bool Cancel()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) return true;
        return Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
    }

    // ------------------------------------------------------------------ arrow pictures drawn in code
    private static Sprite Arrow(int dir)
    {
        if (arrowSprites == null) arrowSprites = new Sprite[4];
        if (arrowSprites[dir] != null) return arrowSprites[dir];
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float u, v;
                switch (dir)
                {
                    case 0: u = x; v = y; break;            // up
                    case 1: u = x; v = 15 - y; break;       // down
                    case 2: u = y; v = 15 - x; break;       // left
                    default: u = y; v = x; break;           // right
                }
                bool head = v >= 7f && v <= 14.5f && Mathf.Abs(u - 7.5f) <= (14.5f - v) * 0.95f + 0.6f;
                bool shaft = v >= 1f && v < 8f && Mathf.Abs(u - 7.5f) <= 1.7f;
                tex.SetPixel(x, y, head || shaft ? Color.white : new Color(0, 0, 0, 0));
            }
        tex.Apply();
        arrowSprites[dir] = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        return arrowSprites[dir];
    }

    /// <summary>Builds the picture inside a note / legend circle for a lane.</summary>
    private void Glyph(Transform parent, int lane, float size)
    {
        if (lane < 4) Img("Arrow", parent, Vector2.zero, new Vector2(size, size), Color.white, Arrow(lane));
        else Txt("Num", parent, lane == 4 ? "1" : "2", Mathf.RoundToInt(size * 0.95f), Color.white, Vector2.zero, new Vector2(size, size));
    }

    // ================================================================== CHOOSE BAIT
    /// <summary>done(-1) = cancelled, done(0) = no bait, done(n) = the n-th bait of the list (1-based).</summary>
    public IEnumerator ChooseBait(List<BaitData> owned, Inventory inv, Action<int> done)
    {
        Clear();
        Img("Dim", root, Vector2.zero, new Vector2(2400, 1400), new Color(0, 0, 0, 0.55f));
        int rows = owned.Count + 1;
        float h = 140f + rows * 54f;
        Img("Border", root, Vector2.zero, new Vector2(612, h + 8), new Color(0.05f, 0.12f, 0.2f));
        Img("Panel", root, Vector2.zero, new Vector2(604, h), new Color(0.12f, 0.3f, 0.45f));
        Txt("Title", root, "Choose your bait", 32, Color.white, new Vector2(0, h * 0.5f - 36f), new Vector2(560, 40));
        Txt("Hint", root, "Up/Down: choose     Space: cast     Esc: cancel", 18, new Color(1, 1, 1, 0.7f), new Vector2(0, -h * 0.5f + 24f), new Vector2(580, 30));

        var rowBg = new List<Image>();
        for (int i = 0; i < rows; i++)
        {
            float y = h * 0.5f - 90f - i * 54f;
            var bg = Img("Row", root, new Vector2(0, y), new Vector2(560, 48), new Color(0, 0, 0, 0.25f));
            rowBg.Add(bg);
            if (i == 0) Txt("Name", bg.transform, "No bait", 24, Color.white, new Vector2(-250, 0), new Vector2(300, 40), TextAnchor.MiddleLeft);
            else
            {
                BaitData b = owned[i - 1];
                if (b.baitItem.icon != null) Img("Icon", bg.transform, new Vector2(-250, 0), new Vector2(40, 40), Color.white, b.baitItem.icon);
                Txt("Name", bg.transform, b.baitItem.itemName + "  x" + inv.CountOf(b.baitItem), 24, Color.white, new Vector2(-225, 0), new Vector2(300, 40), TextAnchor.MiddleLeft);
                string att = "";
                if (b.attracts != null) for (int k = 0; k < b.attracts.Length && k < 3; k++) if (b.attracts[k] != null) att += (att.Length > 0 ? ", " : "") + b.attracts[k].fishName;
                Txt("Attracts", bg.transform, att, 16, new Color(1, 1, 1, 0.7f), new Vector2(120, 0), new Vector2(300, 40), TextAnchor.MiddleLeft);
            }
        }

        int sel = owned.Count > 0 ? 1 : 0;      // start on the first real bait
        yield return null;
        while (true)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) sel = (sel + rows - 1) % rows;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) sel = (sel + 1) % rows;
            }
            for (int i = 0; i < rows; i++) rowBg[i].color = i == sel ? new Color(1f, 0.85f, 0.3f, 0.35f) : new Color(0, 0, 0, 0.25f);
            if (Cancel()) { done(-1); yield break; }
            if (Confirm()) { done(sel); yield break; }
            yield return null;
        }
    }

    // ================================================================== WAIT FOR A BITE
    /// <summary>done(true) = the player reeled in early.</summary>
    public IEnumerator WaitForBite(string areaName, float seconds, string baitName, Action<bool> done)
    {
        Clear();
        Img("Dim", root, Vector2.zero, new Vector2(2400, 1400), new Color(0, 0, 0, 0.45f));
        Img("Border", root, Vector2.zero, new Vector2(708, 268), new Color(0.05f, 0.12f, 0.2f));
        Img("Panel", root, Vector2.zero, new Vector2(700, 260), new Color(0.2f, 0.45f, 0.65f));
        Img("Water", root, new Vector2(0, -40), new Vector2(700, 180), new Color(0.1f, 0.3f, 0.5f));
        Img("Surface", root, new Vector2(0, 50), new Vector2(700, 6), new Color(1, 1, 1, 0.35f));
        Txt("Title", root, "Fishing in the " + areaName, 24, Color.white, new Vector2(0, 100), new Vector2(660, 36));
        Txt("Bait", root, baitName != null ? "Bait: " + baitName : "No bait", 16, new Color(1, 1, 1, 0.7f), new Vector2(0, 76), new Vector2(660, 24));
        RectTransform line = Rt("Line", root, new Vector2(0, 70), new Vector2(3, 40));
        line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.8f);
        Image bob = Img("Bobber", root, new Vector2(0, 50), new Vector2(26, 26), new Color(1f, 0.3f, 0.3f), RuntimeSprites.Circle);
        Img("BobberTop", bob.transform, new Vector2(0, 6), new Vector2(26, 10), Color.white, RuntimeSprites.Circle);
        Text msg = Txt("Msg", root, "Waiting for a bite", 26, Color.white, new Vector2(0, -90), new Vector2(660, 40));
        Txt("Hint", root, "Esc: reel in", 16, new Color(1, 1, 1, 0.6f), new Vector2(0, -118), new Vector2(660, 24));

        yield return null;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            bob.rectTransform.anchoredPosition = new Vector2(0, 50 + Mathf.Sin(Time.unscaledTime * 3f) * 4f);
            msg.text = "Waiting for a bite" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
            if (Cancel()) { done(true); yield break; }
            yield return null;
        }

        // a bite!
        msg.text = "A BITE!"; msg.color = new Color(1f, 0.85f, 0.3f); msg.fontSize = 40;
        float b = 0f;
        while (b < 0.8f)
        {
            b += Time.unscaledDeltaTime;
            bob.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-3f, 3f), 38f + UnityEngine.Random.Range(-4f, 2f));
            yield return null;
        }
        done(false);
    }

    // ================================================================== THE FIGHT
    private class Note
    {
        public int lane; public float time; public int state;      // 0 waiting, 1 hit, 2 missed
        public RectTransform rt; public Image bg;
    }

    private List<Note> BuildChart(FishData fish)
    {
        var notes = new List<Note>();
        int n = Mathf.Clamp(fish.buttonsUsed, 2, 6);
        var lanes = new List<int>();
        for (int i = 0; i < n; i++) lanes.Add(LaneOrder[i]);

        float time = 2.4f;
        float baseGap = 1f / Mathf.Max(0.3f, fish.notesPerSecond);
        int last = -1, repeat = 0, burst = 0;
        for (int i = 0; i < fish.noteCount; i++)
        {
            int lane = lanes[UnityEngine.Random.Range(0, lanes.Count)];
            if (lane == last && repeat >= 2 && lanes.Count > 1) { do { lane = lanes[UnityEngine.Random.Range(0, lanes.Count)]; } while (lane == last); }
            repeat = lane == last ? repeat + 1 : 1;
            last = lane;
            notes.Add(new Note { lane = lane, time = time });

            float gap = baseGap * UnityEngine.Random.Range(0.8f, 1.3f);
            if (burst > 0) { gap *= 0.6f; burst--; }
            else if (fish.stars >= 4 && UnityEngine.Random.value < 0.12f) burst = 2;      // hard fish sometimes dart
            time += Mathf.Max(0.24f, gap);
        }
        return notes;
    }

    /// <summary>done(true) = caught.</summary>
    public IEnumerator Minigame(FishData fish, FishingBonuses bonus, Action<bool> done)
    {
        Clear();
        FishingManager mgr = FishingManager.Instance;

        // ---------- screen ----------
        Img("Dim", root, Vector2.zero, new Vector2(2400, 1400), new Color(0, 0, 0, 0.6f));
        Img("Border", root, Vector2.zero, new Vector2(1128, 428), new Color(0.05f, 0.12f, 0.2f));
        RectTransform clip = Rt("Clip", root, Vector2.zero, new Vector2(1120, 420));
        clip.gameObject.AddComponent<RectMask2D>();
        Img("Water", clip, Vector2.zero, new Vector2(1120, 420), new Color(0.12f, 0.34f, 0.52f));
        for (int i = 0; i < 6; i++) Img("Wave", clip, new Vector2(0, -160 + i * 62), new Vector2(1120, 6), new Color(1, 1, 1, 0.05f));

        Txt("Name", root, fish.fishName, 30, Color.white, new Vector2(-330, 232), new Vector2(380, 40), TextAnchor.MiddleLeft);
        Stars(root, new Vector2(-150, 232), fish.stars, 5, 14f);
        Txt("StaminaLabel", root, "STAMINA", 18, new Color(1, 1, 1, 0.85f), new Vector2(-440, 192), new Vector2(120, 24), TextAnchor.MiddleLeft);
        Img("BarBack", root, new Vector2(40, 192), new Vector2(760, 26), new Color(0, 0, 0, 0.55f));
        RectTransform fillRt = Rt("BarFill", root, new Vector2(-340, 192), new Vector2(10, 20));
        fillRt.pivot = new Vector2(0f, 0.5f);
        Image fill = fillRt.gameObject.AddComponent<Image>();
        Text pct = Txt("Pct", root, "", 18, Color.white, new Vector2(450, 192), new Vector2(100, 24), TextAnchor.MiddleLeft);
        Text combo = Txt("Combo", root, "", 28, new Color(1f, 0.9f, 0.4f), new Vector2(430, 232), new Vector2(200, 36), TextAnchor.MiddleRight);

        Img("Track", clip, new Vector2(0, TrackY), new Vector2(1060, 126), new Color(0.04f, 0.13f, 0.22f, 0.9f));
        Img("RingOuter", clip, new Vector2(HitX, TrackY), new Vector2(118, 118), new Color(1, 1, 1, 0.85f), RuntimeSprites.Circle);
        Img("RingInner", clip, new Vector2(HitX, TrackY), new Vector2(100, 100), new Color(0.04f, 0.13f, 0.22f, 1f), RuntimeSprites.Circle);

        // legend
        var legend = new List<RectTransform>();
        for (int i = 0; i < 6; i++)
        {
            bool used = Array.IndexOf(LaneOrder, i) < Mathf.Clamp(fish.buttonsUsed, 2, 6);
            float x = -300f + i * 120f;
            var c = Img("Key", root, new Vector2(x, -168), new Vector2(46, 46), LaneColors[i] * (used ? 1f : 0.35f), RuntimeSprites.Circle);
            Glyph(c.transform, i, 30f);
            Txt("KeyName", root, mgr.LaneKeyLabel(i), 16, new Color(1, 1, 1, used ? 0.9f : 0.35f), new Vector2(x, -204), new Vector2(110, 22));
            legend.Add(c.rectTransform);
        }

        // the fish
        Image fishImg = Img("Fish", clip, new Vector2(700, 10), new Vector2(120, 120), Color.white, fish.item != null ? fish.item.icon : null);
        fishImg.preserveAspect = true;
        fishImg.enabled = fish.item != null && fish.item.icon != null;
        Text popup = Txt("Popup", root, "", 34, Color.white, new Vector2(HitX, 66), new Vector2(300, 50));
        Text center = Txt("Center", root, "", 60, Color.white, Vector2.zero, new Vector2(800, 90));

        // notes
        List<Note> notes = BuildChart(fish);
        foreach (Note n in notes)
        {
            Color lc = LaneColors[n.lane];
            n.bg = Img("Note", clip, new Vector2(StartX + 200, TrackY), new Vector2(84, 84), lc, RuntimeSprites.Circle);
            n.rt = n.bg.rectTransform;
            Img("Inner", n.rt, Vector2.zero, new Vector2(68, 68), new Color(lc.r * 0.55f, lc.g * 0.55f, lc.b * 0.55f, 1f), RuntimeSprites.Circle);
            Glyph(n.rt, n.lane, 40f);
            n.rt.gameObject.SetActive(false);
        }

        // ---------- numbers ----------
        float speed = fish.scrollSpeed * bonus.noteSpeedMultiplier;
        float perHit = (100f - fish.startStamina) / (fish.noteCount * Mathf.Max(0.3f, fish.requiredHitRatio));
        float gain = perHit * bonus.staminaGainMultiplier;
        float loss = perHit * fish.missPenalty;
        float stamina = fish.startStamina;
        int freeLeft = bonus.freeMisses, resolved = 0, comboCount = 0;
        float popupTimer = 0f, pulse = 0f;
        float t = -1.6f;
        bool caught = false, ended = false;
        float[] legendPulse = new float[6];

        yield return null;
        while (!ended)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            // intro: the fish swims in from the right
            float swim = Mathf.Clamp01((t + 1.6f) / 1.3f);
            float fishX = Mathf.Lerp(700f, HitX - 70f, 1f - (1f - swim) * (1f - swim));
            float fishY = TrackY + 80f + Mathf.Sin(Time.unscaledTime * 3f) * 8f + (stamina - 50f) * 0.15f;
            fishImg.rectTransform.anchoredPosition = new Vector2(fishX, fishY);
            fishImg.rectTransform.localScale = Vector3.one * (1f + pulse * 0.25f);
            fishImg.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Sin(Time.unscaledTime * 6f) * 6f);
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 4f);

            if (t < 0f) center.text = t < -0.7f ? "Get ready!" : "";
            else if (t < 0.6f) center.text = "";

            // presses
            if (t >= 0f)
            {
                for (int lane = 0; lane < 6; lane++)
                {
                    if (!mgr.LanePressed(lane)) continue;
                    legendPulse[lane] = 1f;

                    Note target = null;
                    foreach (Note n in notes)
                    {
                        if (n.state != 0) continue;
                        if (Mathf.Abs(n.time - t) <= bonus.hitWindow && (target == null || n.time < target.time)) target = n;
                    }
                    if (target == null) continue;       // pressed with nothing near: no penalty

                    float off = Mathf.Abs(target.time - t);
                    resolved++;
                    if (target.lane == lane)
                    {
                        target.state = 1;
                        bool perfect = off <= bonus.perfectWindow;
                        stamina = Mathf.Min(100f, stamina + gain * (perfect ? 1.25f : 1f));
                        comboCount++;
                        popup.text = perfect ? "PERFECT!" : "GOOD";
                        popup.color = perfect ? new Color(1f, 0.9f, 0.3f) : new Color(0.7f, 1f, 0.8f);
                        popupTimer = 0.45f; pulse = 1f;
                        target.rt.gameObject.SetActive(false);
                    }
                    else
                    {
                        target.state = 2;
                        PenaltyMiss(ref stamina, loss, ref freeLeft, ref comboCount, popup, ref popupTimer, "WRONG!");
                        target.bg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                    }
                }

                // notes that slid past
                foreach (Note n in notes)
                {
                    if (n.state != 0 || t - n.time <= bonus.hitWindow) continue;
                    n.state = 2; resolved++;
                    PenaltyMiss(ref stamina, loss, ref freeLeft, ref comboCount, popup, ref popupTimer, "MISS");
                    n.bg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                }
            }

            // note positions
            foreach (Note n in notes)
            {
                if (n.state == 1) continue;
                float x = HitX + (n.time - t) * speed;
                bool visible = x < StartX + 100f && x > HitX - 320f;
                if (n.rt.gameObject.activeSelf != visible) n.rt.gameObject.SetActive(visible);
                if (visible) n.rt.anchoredPosition = new Vector2(x, TrackY);
            }

            // meter, text, legend
            float frac = Mathf.Clamp01(stamina / 100f);
            fillRt.sizeDelta = new Vector2(Mathf.Max(2f, 760f * frac - 8f), 18f);
            fill.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.35f, 0.9f, 0.4f), frac);
            pct.text = Mathf.RoundToInt(stamina) + "%";
            combo.text = comboCount >= 3 ? "x" + comboCount : "";
            if (popupTimer > 0f) { popupTimer -= dt; if (popupTimer <= 0f) popup.text = ""; }
            for (int i = 0; i < 6; i++)
            {
                legendPulse[i] = Mathf.MoveTowards(legendPulse[i], 0f, dt * 5f);
                legend[i].localScale = Vector3.one * (1f + legendPulse[i] * 0.3f);
            }

            // finished?
            if (stamina >= 100f) { caught = true; ended = true; }
            else if (stamina <= 0f) ended = true;
            else if (resolved >= notes.Count) ended = true;
            else if (Cancel()) ended = true;
            yield return null;
        }

        center.text = caught ? "HOOKED!" : "IT GOT AWAY...";
        center.color = caught ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.5f, 0.5f);
        float wait = 0f;
        while (wait < 1.0f) { wait += Time.unscaledDeltaTime; yield return null; }
        done(caught);
    }

    private static void PenaltyMiss(ref float stamina, float loss, ref int freeLeft, ref int comboCount, Text popup, ref float popupTimer, string label)
    {
        comboCount = 0;
        popupTimer = 0.45f;
        if (freeLeft > 0)
        {
            freeLeft--;
            popup.text = "SAVED!"; popup.color = new Color(0.5f, 0.8f, 1f);
            return;
        }
        stamina = Mathf.Max(0f, stamina - loss);
        popup.text = label; popup.color = new Color(1f, 0.45f, 0.45f);
    }

    // ================================================================== RESULT
    public IEnumerator Result(FishData fish, bool caught, bool first, int xp)
    {
        Clear();
        Img("Dim", root, Vector2.zero, new Vector2(2400, 1400), new Color(0, 0, 0, 0.6f));
        Img("Border", root, Vector2.zero, new Vector2(528, 388), new Color(0.05f, 0.12f, 0.2f));
        Img("Panel", root, Vector2.zero, new Vector2(520, 380), caught ? new Color(0.14f, 0.36f, 0.5f) : new Color(0.25f, 0.25f, 0.3f));

        if (caught)
        {
            Txt("Title", root, "You caught a", 24, new Color(1, 1, 1, 0.8f), new Vector2(0, 150), new Vector2(480, 30));
            Txt("Name", root, fish.fishName + "!", 40, Color.white, new Vector2(0, 112), new Vector2(480, 50));
            if (fish.item != null && fish.item.icon != null)
            {
                var pic = Img("Fish", root, new Vector2(0, 10), new Vector2(160, 160), Color.white, fish.item.icon);
                pic.preserveAspect = true;
            }
            Stars(root, new Vector2(0, -88), fish.stars, 5, 18f);
            if (fish.item != null) Txt("Value", root, "Worth " + fish.item.sellPrice + " gold", 22, new Color(1f, 0.9f, 0.5f), new Vector2(0, -122), new Vector2(480, 30));
            Txt("Xp", root, "+" + xp + " fishing XP", 20, new Color(0.7f, 1f, 0.8f), new Vector2(0, -148), new Vector2(480, 28));
            if (first) Txt("New", root, "NEW CATCH!", 26, new Color(1f, 0.85f, 0.3f), new Vector2(140, 112), new Vector2(200, 36));
        }
        else
        {
            Txt("Title", root, "It got away...", 40, Color.white, new Vector2(0, 100), new Vector2(480, 50));
            if (fish.item != null && fish.item.icon != null)
            {
                var pic = Img("Fish", root, new Vector2(0, 0), new Vector2(120, 120), new Color(0, 0, 0, 0.55f), fish.item.icon);
                pic.preserveAspect = true;
            }
            Txt("Sub", root, "Better luck next cast.", 22, new Color(1, 1, 1, 0.8f), new Vector2(0, -90), new Vector2(480, 30));
        }
        Txt("Hint", root, "Press Space", 18, new Color(1, 1, 1, 0.6f), new Vector2(0, -170), new Vector2(480, 24));

        yield return null;
        float t = 0f;
        while (t < 15f)
        {
            t += Time.unscaledDeltaTime;
            if (t > 0.3f && (Confirm() || Cancel())) break;
            yield return null;
        }
    }
}
