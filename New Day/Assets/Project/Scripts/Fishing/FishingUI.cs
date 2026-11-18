using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The two fishing screens, built from code (no setup needed): the rhythm FIGHT and the CATCH card.
/// (Casting, waiting and the bite happen out in the world: see FishingRig.)
///
/// The fight is an underwater scene in a wooden frame. Notes (buttons) swim in from the RIGHT and you press the matching button
/// as each one reaches the ring on the LEFT. Hit = the meter goes up and the fish is pulled closer. Miss = the meter goes down.
/// Reach 100 and the fish is yours; hit 0 (or run out of notes first) and it escapes.
///
/// All pictures come from FishingArt (drawn in code). Colours and sizes are the constants below.
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

    private static readonly Color Gold = new Color(1f, 0.84f, 0.28f);
    private static readonly Color Cream = new Color(1f, 0.96f, 0.84f);
    private static readonly Color MissRed = new Color(1f, 0.35f, 0.35f);

    // ---- layout (screen units; the screen is 1280 x 720 with 0,0 in the middle) ----
    private const float WinW = 1100f, WinH = 360f, WinY = 18f;    // the underwater window
    private const float HeaderY = 252f, FooterY = -206f;           // the wooden bars above and below it
    private const float HitX = -400f;                              // where notes must be pressed (inside the window)
    private const float StartX = 600f;                             // where notes appear (off the right edge, hidden)
    private const float TrackY = -12f;
    private const float FishY = 105f;
    private const float BarLeft = -96f, BarWidth = 572f;           // the catch meter

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
        particles.Clear();
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

    /// <summary>An image that stretches by its middle only, so a frame's corners keep their size.</summary>
    private Image Sliced(string name, Transform parent, Vector2 pos, Vector2 size, Color c, Sprite s)
    {
        Image im = Img(name, parent, pos, size, c, s);
        im.type = Image.Type.Sliced;
        return im;
    }

    /// <summary>Moves a RectTransform's pivot without moving where 'pos' is measured to.</summary>
    private static void SetPivot(RectTransform rt, Vector2 pivot, Vector2 pos)
    {
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }

    private Text Txt(string name, Transform parent, string s, int fontSize, Color c, Vector2 pos, Vector2 size, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var t = Rt(name, parent, pos, size).gameObject.AddComponent<Text>();
        t.font = font; t.text = s; t.fontSize = fontSize; t.color = c; t.alignment = anchor;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        // a dark edge so the writing reads on any background
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.03f, 0.05f, 0.1f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private void Stars(Transform parent, Vector2 center, int count, int total, float size)
    {
        float gap = size + 4f;
        float x0 = center.x - (total - 1) * gap * 0.5f;
        for (int i = 0; i < total; i++)
            Img("Star", parent, new Vector2(x0 + i * gap, center.y), new Vector2(size, size),
                i < count ? Gold : new Color(0.25f, 0.3f, 0.38f, 0.85f), FishingArt.UiStar);
    }

    private static Color Dim(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    private static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k); }

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

    // ------------------------------------------------------------------ sparkles and flashes
    private class Particle
    {
        public RectTransform rt; public Image img; public Color color;
        public Vector2 vel; public float life, maxLife, gravity, spin, size0, size1;
    }
    private readonly List<Particle> particles = new List<Particle>();

    private void SpawnFx(Transform parent, Sprite s, Vector2 pos, Vector2 vel, Color c, float life, float size0, float size1, float gravity, float spin)
    {
        Image img = Img("Fx", parent, pos, Vector2.one * size0, c, s);
        particles.Add(new Particle { rt = img.rectTransform, img = img, color = c, vel = vel, maxLife = life, gravity = gravity, spin = spin, size0 = size0, size1 = size1 });
    }

    /// <summary>Sparkles flying out from 'pos', and a ring that spreads and fades.</summary>
    private void Burst(Transform parent, Vector2 pos, Color c, int count, float speed)
    {
        SpawnFx(parent, FishingArt.UiRing, pos, Vector2.zero, new Color(c.r, c.g, c.b, 0.8f), 0.3f, 110f, 200f, 0f, 0f);
        for (int i = 0; i < count; i++)
        {
            float a = (i + UnityEngine.Random.value * 0.6f) / count * Mathf.PI * 2f;
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * UnityEngine.Random.Range(0.6f, 1.2f);
            Color col = Color.Lerp(c, Color.white, UnityEngine.Random.value * 0.5f);
            SpawnFx(parent, FishingArt.UiSpark, pos + v * 0.12f, v, col, UnityEngine.Random.Range(0.35f, 0.6f), 30f, 8f, 420f, UnityEngine.Random.Range(-360f, 360f));
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle p = particles[i];
            p.life += dt;
            float k = p.life / p.maxLife;
            if (k >= 1f || p.rt == null)
            {
                if (p.rt != null) Destroy(p.rt.gameObject);
                particles.RemoveAt(i);
                continue;
            }
            p.vel.y -= p.gravity * dt;
            p.rt.anchoredPosition += p.vel * dt;
            p.rt.sizeDelta = Vector2.one * Mathf.Lerp(p.size0, p.size1, k);
            p.rt.Rotate(0f, 0f, p.spin * dt);
            Color c = p.color;
            c.a *= 1f - k * k;
            p.img.color = c;
        }
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

    /// <summary>The picture inside a note: an arrow or a number, with a soft shadow under it.</summary>
    private void Glyph(Transform parent, int lane, float size, float alpha)
    {
        Vector2 s = new Vector2(size, size);
        if (lane < 4)
        {
            Img("GlyphShadow", parent, new Vector2(2f, -3f) * (size / 40f), s, new Color(0f, 0f, 0f, 0.45f * alpha), Arrow(lane));
            Img("Glyph", parent, Vector2.zero, s, new Color(1f, 1f, 1f, alpha), Arrow(lane));
        }
        else Txt("Glyph", parent, lane == 4 ? "1" : "2", Mathf.RoundToInt(size * 0.95f), new Color(1f, 1f, 1f, alpha), Vector2.zero, s);
    }

    /// <summary>A round shaded note in its lane's colour (also used for the button legend). 'dim' below 1 greys it out.</summary>
    private RectTransform NoteVisual(string name, Transform parent, Vector2 pos, float size, int lane, float dim, out Image body)
    {
        RectTransform rt = Rt(name, parent, pos, Vector2.one * size);
        Img("Shadow", rt, new Vector2(0f, -size * 0.08f), Vector2.one * size, new Color(0f, 0f, 0f, 0.3f * dim), FishingArt.UiNoteBody);
        body = Img("Body", rt, Vector2.zero, Vector2.one * size, Dim(LaneColors[lane], dim), FishingArt.UiNoteBody);
        Img("Shine", rt, Vector2.zero, Vector2.one * size, new Color(1f, 1f, 1f, dim), FishingArt.UiNoteShine);
        Glyph(rt, lane, size * 0.48f, Mathf.Lerp(0.4f, 1f, dim));
        return rt;
    }

    // ================================================================== THE FIGHT
    private class Note
    {
        public int lane; public float time; public int state;      // 0 waiting, 1 hit, 2 missed
        public float missTime;
        public RectTransform rt; public Image body; public CanvasGroup group;
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
        int lanesUsed = Mathf.Clamp(fish.buttonsUsed, 2, 6);
        Sprite fishSprite = fish.item != null ? fish.item.icon : null;

        // ---------- the frame ----------
        Img("Dim", root, Vector2.zero, new Vector2(2400, 1400), new Color(0.01f, 0.03f, 0.07f, 0.6f));
        Sliced("Frame", root, new Vector2(0f, WinY), new Vector2(WinW + 36f, WinH + 36f), Color.white, FishingArt.UiWood);
        RectTransform clip = Rt("Window", root, new Vector2(0f, WinY), new Vector2(WinW, WinH));
        clip.gameObject.AddComponent<RectMask2D>();
        RectTransform world = Rt("Scene", clip, Vector2.zero, new Vector2(WinW, WinH));     // everything in here shakes together on a miss

        // ---------- the underwater scene ----------
        Img("Sea", world, Vector2.zero, new Vector2(WinW + 40f, WinH + 24f), Color.white, FishingArt.UiSea);

        float[] rayX = { -270f, 30f, 330f };
        var rays = new Image[rayX.Length];
        for (int i = 0; i < rays.Length; i++)
        {
            rays[i] = Img("LightRay", world, Vector2.zero, new Vector2(120f, WinH * 1.1f), new Color(1f, 1f, 1f, 0.1f), FishingArt.UiRay);
            SetPivot(rays[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(rayX[i], WinH * 0.5f + 6f));
            rays[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -14f);
        }

        const int bubbleCount = 16;
        var bubbles = new RectTransform[bubbleCount];
        var bubbleX = new float[bubbleCount];
        var bubbleSpeed = new float[bubbleCount];
        for (int i = 0; i < bubbleCount; i++)
        {
            float s = UnityEngine.Random.Range(10f, 24f);
            bubbleX[i] = UnityEngine.Random.Range(-WinW * 0.5f, WinW * 0.5f);
            bubbleSpeed[i] = UnityEngine.Random.Range(28f, 70f);
            bubbles[i] = Img("Bubble", world, new Vector2(bubbleX[i], UnityEngine.Random.Range(-WinH * 0.5f, WinH * 0.5f)), new Vector2(s, s),
                             new Color(1f, 1f, 1f, 0.75f), FishingArt.UiBubble).rectTransform;
        }

        float[] weedX = { -525f, -470f, -250f, -70f, 160f, 345f, 510f };
        var weeds = new RectTransform[weedX.Length];
        for (int i = 0; i < weeds.Length; i++)
        {
            Image w = Img("Seaweed", world, Vector2.zero, new Vector2(28f, UnityEngine.Random.Range(100f, 165f)),
                          Color.Lerp(Color.white, new Color(0.75f, 1f, 0.85f), UnityEngine.Random.value), FishingArt.UiSeaweed);
            SetPivot(w.rectTransform, new Vector2(0.5f, 0f), new Vector2(weedX[i], -WinH * 0.5f - 6f));
            weeds[i] = w.rectTransform;
        }

        // ---------- the note track and the ring ----------
        Sliced("Track", world, new Vector2(0f, TrackY), new Vector2(WinW - 30f, 128f), new Color(0.06f, 0.2f, 0.32f, 0.55f), FishingArt.UiTrack);
        Img("TrackLine", world, new Vector2(0f, TrackY), new Vector2(WinW - 60f, 2f), new Color(1f, 1f, 1f, 0.08f));
        Image ringGlow = Img("RingGlow", world, new Vector2(HitX, TrackY), new Vector2(210f, 210f), Color.clear, FishingArt.UiGlow);
        Image ring = Img("Ring", world, new Vector2(HitX, TrackY), new Vector2(124f, 124f), Color.white, FishingArt.UiRing);

        // ---------- the fish on the line ----------
        Image lineImg = Img("Line", world, Vector2.zero, new Vector2(3f, 10f), new Color(0.95f, 0.95f, 0.9f, 0.75f));
        SetPivot(lineImg.rectTransform, new Vector2(0.5f, 0f), Vector2.zero);
        Image fishShadow = Img("FishShadow", world, new Vector2(700f, FishY - 60f), new Vector2(90f, 16f), new Color(0f, 0f, 0f, 0.22f), RuntimeSprites.Circle);
        Image fishImg = Img("Fish", world, new Vector2(700f, FishY), new Vector2(110f, 110f), Color.white, fishSprite);
        fishImg.preserveAspect = true;
        fishImg.enabled = fishSprite != null;
        fishShadow.enabled = fishSprite != null;

        // ---------- header: name, stars, the catch meter ----------
        Sliced("Header", root, new Vector2(0f, HeaderY), new Vector2(WinW + 36f, 70f), Color.white, FishingArt.UiWood);
        Txt("Name", root, fish.fishName, 30, Cream, new Vector2(-372f, HeaderY + 11f), new Vector2(340f, 36f), TextAnchor.MiddleLeft);
        Stars(root, new Vector2(-497f, HeaderY - 16f), fish.stars, 5, 18f);
        Txt("MeterLabel", root, "REEL", 20, new Color(1f, 0.92f, 0.7f), new Vector2(-150f, HeaderY), new Vector2(80f, 28f));
        Sliced("BarBack", root, new Vector2(BarLeft - 4f + (BarWidth + 8f) * 0.5f, HeaderY), new Vector2(BarWidth + 8f, 32f), Color.white, FishingArt.UiInset);
        RectTransform fillRt = Rt("BarFill", root, Vector2.zero, new Vector2(10f, 24f));
        SetPivot(fillRt, new Vector2(0f, 0.5f), new Vector2(BarLeft, HeaderY));
        Image fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = FishingArt.UiBarFill; fill.type = Image.Type.Sliced; fill.raycastTarget = false;
        Image marker = Img("Marker", root, new Vector2(BarLeft, HeaderY), new Vector2(36f, 36f), Color.white, fishSprite);
        marker.preserveAspect = true;
        marker.enabled = fishSprite != null;
        Text pct = Txt("Pct", root, "", 22, Color.white, new Vector2(530f, HeaderY), new Vector2(90f, 28f), TextAnchor.MiddleLeft);

        // ---------- footer: the buttons this fish uses ----------
        Sliced("Footer", root, new Vector2(0f, FooterY), new Vector2(WinW + 36f, 66f), Color.white, FishingArt.UiWood);
        var legend = new List<RectTransform>();
        for (int i = 0; i < 6; i++)
        {
            bool used = Array.IndexOf(LaneOrder, i) < lanesUsed;
            float x = -450f + i * 180f;
            Image unused;
            legend.Add(NoteVisual("Key", root, new Vector2(x - 22f, FooterY), 46f, i, used ? 1f : 0.35f, out unused));
            Txt("KeyName", root, mgr.LaneKeyLabel(i), 20, new Color(1f, 1f, 1f, used ? 0.95f : 0.35f), new Vector2(x + 48f, FooterY), new Vector2(90f, 28f), TextAnchor.MiddleLeft);
        }
        Txt("Hint", root, "Esc: let it go", 16, new Color(1f, 1f, 1f, 0.55f), new Vector2(0f, FooterY - 50f), new Vector2(400f, 24f));

        // ---------- writing on top ----------
        Vector2 popupHome = new Vector2(HitX, WinY + TrackY + 88f);
        Text popup = Txt("Popup", root, "", 34, Color.white, popupHome, new Vector2(320f, 50f));
        Text combo = Txt("Combo", root, "", 26, new Color(1f, 0.9f, 0.4f), new Vector2(HitX, WinY + TrackY - 92f), new Vector2(300f, 36f));
        Text center = Txt("Center", root, "", 64, Color.white, new Vector2(0f, WinY + 20f), new Vector2(900f, 100f));

        // ---------- the notes ----------
        List<Note> notes = BuildChart(fish);
        foreach (Note n in notes)
        {
            n.rt = NoteVisual("Note", world, new Vector2(StartX + 200f, TrackY), 86f, n.lane, 1f, out n.body);
            n.group = n.rt.gameObject.AddComponent<CanvasGroup>();
            n.group.interactable = false; n.group.blocksRaycasts = false;
            n.rt.gameObject.SetActive(false);
        }

        // ---------- numbers ----------
        float speed = fish.scrollSpeed * bonus.noteSpeedMultiplier;
        float perHit = (100f - fish.startStamina) / (fish.noteCount * Mathf.Max(0.3f, fish.requiredHitRatio));
        float gain = perHit * bonus.staminaGainMultiplier;
        float loss = perHit * fish.missPenalty;
        float stamina = fish.startStamina;
        int freeLeft = bonus.freeMisses, resolved = 0, comboCount = 0;
        float popupTimer = 0f, pulse = 0f, missJolt = 0f, shake = 0f, ringPulse = 0f, ringFlash = 0f, comboPop = 0f;
        Color flashColor = Color.white;
        float t = -1.6f;
        float fishX = 720f;
        bool caught = false, ended = false;
        float[] legendPulse = new float[6];

        yield return null;
        while (!ended)
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            t += dt;

            // ---- the scene keeps moving: light rays shimmer, bubbles rise, weeds sway ----
            for (int i = 0; i < rays.Length; i++)
                rays[i].color = new Color(1f, 1f, 1f, 0.07f + 0.05f * Mathf.Sin(now * 0.7f + i * 2.1f));
            for (int i = 0; i < bubbleCount; i++)
            {
                Vector2 p = bubbles[i].anchoredPosition;
                p.y += bubbleSpeed[i] * dt;
                if (p.y > WinH * 0.5f + 20f) { p.y = -WinH * 0.5f - 15f; bubbleX[i] = UnityEngine.Random.Range(-WinW * 0.5f, WinW * 0.5f); }
                p.x = bubbleX[i] + Mathf.Sin(now * 2f + i) * 6f;
                bubbles[i].anchoredPosition = p;
            }
            for (int i = 0; i < weeds.Length; i++)
                weeds[i].localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(now * 1.2f + i * 1.7f) * 7f);

            if (t < 0f) center.text = t < -0.7f ? "Get ready!" : "";
            else if (t < 0.6f) center.text = "";

            // ---- button presses ----
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
                        comboPop = 1f;
                        popup.text = perfect ? "PERFECT!" : "GOOD";
                        popup.color = perfect ? new Color(1f, 0.9f, 0.3f) : new Color(0.7f, 1f, 0.8f);
                        popupTimer = 0.6f; pulse = 1f; ringPulse = 1f; ringFlash = 1f;
                        flashColor = perfect ? Gold : LaneColors[target.lane];
                        Burst(world, new Vector2(HitX, TrackY), flashColor, perfect ? 12 : 8, perfect ? 430f : 320f);
                        target.rt.gameObject.SetActive(false);
                    }
                    else
                    {
                        target.state = 2; target.missTime = t;
                        target.body.color = new Color(0.45f, 0.45f, 0.5f);
                        PenaltyMiss(ref stamina, loss, ref freeLeft, ref comboCount, popup, ref popupTimer, "WRONG!");
                        shake = 0.3f; missJolt = 1f; ringFlash = 0.8f; flashColor = MissRed;
                    }
                }

                // notes that slid past
                foreach (Note n in notes)
                {
                    if (n.state != 0 || t - n.time <= bonus.hitWindow) continue;
                    n.state = 2; n.missTime = t; resolved++;
                    n.body.color = new Color(0.45f, 0.45f, 0.5f);
                    PenaltyMiss(ref stamina, loss, ref freeLeft, ref comboCount, popup, ref popupTimer, "MISS");
                    shake = 0.25f; missJolt = 1f; ringFlash = 0.8f; flashColor = MissRed;
                }
            }

            // ---- the notes move; missed ones sink and fade ----
            Note next = null;
            foreach (Note n in notes)
            {
                if (n.state == 1) continue;
                if (n.state == 0 && next == null) next = n;      // notes are in time order: the first waiting one is the next to hit
                float x = HitX + (n.time - t) * speed;
                float y = TrackY;
                bool visible = x < StartX + 100f && x > -WinW * 0.5f - 80f;
                if (n.state == 2)
                {
                    float m = t - n.missTime;
                    y -= m * m * 500f + m * 40f;
                    n.group.alpha = Mathf.Clamp01(1f - m * 2.2f);
                    n.rt.localEulerAngles = new Vector3(0f, 0f, m * 120f);
                    visible = visible && n.group.alpha > 0f;
                }
                else y += Mathf.Sin(now * 5f + n.time * 3f) * 3f;     // a gentle bob, like they are floating
                if (n.rt.gameObject.activeSelf != visible) n.rt.gameObject.SetActive(visible);
                if (visible) n.rt.anchoredPosition = new Vector2(x, y);
            }

            // ---- the ring takes on the colour of the note about to arrive, and flashes on a hit or miss ----
            float near = next != null ? Mathf.Clamp01(1f - (next.time - t) / 0.5f) : 0f;
            Color ringCol = Color.Lerp(Color.white, next != null ? LaneColors[next.lane] : Color.white, near * 0.8f);
            ring.color = Color.Lerp(ringCol, flashColor, ringFlash * 0.7f);
            ring.rectTransform.localScale = Vector3.one * (1f + ringPulse * 0.18f);
            ringGlow.color = new Color(flashColor.r, flashColor.g, flashColor.b, ringFlash * 0.8f);
            ringGlow.rectTransform.localScale = Vector3.one * (0.8f + ringFlash * 0.4f);
            ringPulse = Mathf.MoveTowards(ringPulse, 0f, dt * 5f);
            ringFlash = Mathf.MoveTowards(ringFlash, 0f, dt * 3f);

            // ---- a miss shakes the window ----
            world.anchoredPosition = shake > 0f ? UnityEngine.Random.insideUnitCircle * 10f * (shake / 0.3f) : Vector2.zero;
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);

            // ---- the fish: pulled toward the ring as the meter fills; jerks on a hit, darts away on a miss ----
            float frac = Mathf.Clamp01(stamina / 100f);
            float targetX = Mathf.Lerp(430f, HitX + 170f, frac);
            fishX = Mathf.Lerp(fishX, targetX, 1f - Mathf.Exp(-dt * (t < 0f ? 2.5f : 4f)));
            float fx = fishX - pulse * 30f + missJolt * 40f;
            float fy = FishY + Mathf.Sin(now * 2.6f) * 10f;
            fishImg.rectTransform.anchoredPosition = new Vector2(fx, fy);
            fishImg.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(now * 7f) * 7f - missJolt * 15f);
            fishImg.rectTransform.localScale = Vector3.one * (1f + pulse * 0.15f);
            fishShadow.rectTransform.anchoredPosition = new Vector2(fx, fy - 62f);
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 4f);
            missJolt = Mathf.MoveTowards(missJolt, 0f, dt * 3f);

            // the line comes down from above the window (where the rod is) to the fish's mouth
            Vector2 rodPoint = new Vector2(HitX - 60f, WinH * 0.5f + 30f);
            Vector2 toFish = new Vector2(fx - 45f, fy + 5f) - rodPoint;
            lineImg.rectTransform.anchoredPosition = rodPoint;
            lineImg.rectTransform.sizeDelta = new Vector2(3f, toFish.magnitude);
            lineImg.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(toFish.y, toFish.x) * Mathf.Rad2Deg - 90f);

            // ---- the meter ----
            float fillW = Mathf.Max(10f, BarWidth * frac);
            fillRt.sizeDelta = new Vector2(fillW, 24f);
            fill.color = frac < 0.5f ? Color.Lerp(new Color(0.92f, 0.28f, 0.25f), new Color(1f, 0.82f, 0.25f), frac * 2f)
                                     : Color.Lerp(new Color(1f, 0.82f, 0.25f), new Color(0.38f, 0.92f, 0.42f), (frac - 0.5f) * 2f);
            marker.rectTransform.anchoredPosition = new Vector2(BarLeft + fillW, HeaderY + Mathf.Sin(now * 6f) * 2f);
            pct.text = Mathf.RoundToInt(stamina) + "%";

            // ---- popups float up and fade; the combo bounces ----
            if (popupTimer > 0f)
            {
                popupTimer -= dt;
                float k = 1f - popupTimer / 0.6f;
                popup.rectTransform.anchoredPosition = popupHome + Vector2.up * 40f * EaseOut(k);
                popup.rectTransform.localScale = Vector3.one * (1f + Mathf.Pow(1f - Mathf.Clamp01(k * 3f), 2f) * 0.4f);
                Color c = popup.color; c.a = popupTimer < 0.2f ? Mathf.Clamp01(popupTimer / 0.2f) : 1f; popup.color = c;
                if (popupTimer <= 0f) popup.text = "";
            }
            combo.text = comboCount >= 3 ? "x" + comboCount + " COMBO" : "";
            combo.rectTransform.localScale = Vector3.one * (1f + comboPop * 0.3f);
            comboPop = Mathf.MoveTowards(comboPop, 0f, dt * 5f);

            for (int i = 0; i < 6; i++)
            {
                legendPulse[i] = Mathf.MoveTowards(legendPulse[i], 0f, dt * 5f);
                legend[i].localScale = Vector3.one * (1f + legendPulse[i] * 0.3f);
            }
            UpdateParticles(dt);

            // ---- finished? ----
            if (stamina >= 100f) { caught = true; ended = true; }
            else if (stamina <= 0f) ended = true;
            else if (resolved >= notes.Count) ended = true;
            else if (Cancel()) ended = true;
            yield return null;
        }

        // ---------- the ending: a big word that drops in ----------
        center.text = caught ? "HOOKED!" : "IT GOT AWAY...";
        center.color = caught ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.5f, 0.5f);
        if (caught)
        {
            Burst(world, fishImg.rectTransform.anchoredPosition, Gold, 16, 480f);
            Burst(root, new Vector2(0f, WinY + 20f), new Color(0.6f, 1f, 0.7f), 10, 380f);
        }
        float wait = 0f;
        while (wait < 1.1f)
        {
            float dt = Time.unscaledDeltaTime;
            wait += dt;
            center.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, EaseOut(wait / 0.25f));
            if (!caught) fishImg.rectTransform.anchoredPosition += Vector2.right * 700f * dt;      // the fish swims off
            UpdateParticles(dt);
            yield return null;
        }
        done(caught);
    }

    private static void PenaltyMiss(ref float stamina, float loss, ref int freeLeft, ref int comboCount, Text popup, ref float popupTimer, string label)
    {
        comboCount = 0;
        popupTimer = 0.6f;
        if (freeLeft > 0)
        {
            freeLeft--;
            popup.text = "SAVED!"; popup.color = new Color(0.5f, 0.8f, 1f);
            return;
        }
        stamina = Mathf.Max(0f, stamina - loss);
        popup.text = label; popup.color = MissRed;
    }

    // ================================================================== THE CATCH CARD
    public IEnumerator Result(FishData fish, bool caught, bool first, int xp)
    {
        Clear();
        Sprite fishSprite = fish.item != null ? fish.item.icon : null;
        Img("Dim", root, Vector2.zero, new Vector2(2400, 1400), new Color(0.01f, 0.03f, 0.07f, 0.6f));
        RectTransform card = Rt("Card", root, Vector2.zero, new Vector2(580f, 450f));
        Sliced("Frame", card, Vector2.zero, new Vector2(580f, 450f), Color.white, FishingArt.UiWood);
        Sliced("Panel", card, Vector2.zero, new Vector2(536f, 406f), caught ? Color.white : new Color(0.7f, 0.7f, 0.75f), FishingArt.UiPanel);

        Image rays = null, pic = null;
        if (caught)
        {
            rays = Img("Rays", card, new Vector2(0f, 12f), new Vector2(380f, 380f), new Color(1f, 0.9f, 0.55f, 0.3f), FishingArt.UiSunburst);
            Img("Glow", card, new Vector2(0f, 12f), new Vector2(230f, 230f), new Color(1f, 0.95f, 0.7f, 0.35f), FishingArt.UiGlow);
            Txt("Title", card, "You caught a", 24, new Color(0.85f, 0.93f, 1f), new Vector2(0f, 168f), new Vector2(480f, 30f));
            Txt("Name", card, fish.fishName + "!", 42, Cream, new Vector2(0f, 128f), new Vector2(500f, 50f));
            if (fishSprite != null)
            {
                pic = Img("Fish", card, new Vector2(0f, 12f), new Vector2(170f, 170f), Color.white, fishSprite);
                pic.preserveAspect = true;
            }
            Stars(card, new Vector2(0f, -92f), fish.stars, 5, 26f);
            if (fish.item != null) Txt("Value", card, "Worth " + fish.item.sellPrice + " gold", 22, new Color(1f, 0.9f, 0.5f), new Vector2(0f, -128f), new Vector2(480f, 30f));
            Txt("Xp", card, "+" + xp + " fishing XP", 20, new Color(0.7f, 1f, 0.8f), new Vector2(0f, -156f), new Vector2(480f, 28f));
            if (first)
            {
                Image tag = Sliced("NewTag", card, new Vector2(196f, 196f), new Vector2(196f, 46f), new Color(0.86f, 0.22f, 0.22f), FishingArt.UiPill);
                tag.rectTransform.localEulerAngles = new Vector3(0f, 0f, -10f);
                Txt("New", tag.rectTransform, "NEW CATCH!", 22, Color.white, Vector2.zero, new Vector2(190f, 40f));
            }
        }
        else
        {
            Txt("Title", card, "It got away...", 40, Color.white, new Vector2(0f, 100f), new Vector2(480f, 50f));
            if (fishSprite != null)
            {
                pic = Img("Fish", card, new Vector2(0f, 0f), new Vector2(120f, 120f), new Color(0f, 0f, 0f, 0.55f), fishSprite);
                pic.preserveAspect = true;
            }
            Txt("Sub", card, "Better luck next cast.", 22, new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, -90f), new Vector2(480f, 30f));
        }
        Txt("Hint", card, "Press Space", 18, new Color(1f, 1f, 1f, 0.6f), new Vector2(0f, -184f), new Vector2(480f, 24f));

        yield return null;
        float t = 0f;
        while (t < 15f)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            // the card pops in, the rays turn, the fish bobs and sparkles appear around it
            card.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(0.6f, 1.06f, t / 0.15f) : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((t - 0.15f) / 0.1f)));
            if (rays != null) rays.rectTransform.Rotate(0f, 0f, dt * 18f);
            if (pic != null && caught) pic.rectTransform.anchoredPosition = new Vector2(0f, 12f + Mathf.Sin(t * 2.5f) * 6f);
            if (caught && UnityEngine.Random.value < dt * 8f)
            {
                Vector2 at = new Vector2(0f, 12f) + UnityEngine.Random.insideUnitCircle * 120f;
                SpawnFx(card, FishingArt.UiSpark, at, Vector2.up * 20f, Gold, 0.6f, 24f, 4f, 0f, 90f);
            }
            UpdateParticles(dt);
            if (t > 0.3f && (Confirm() || Cancel())) break;
            yield return null;
        }
    }
}
