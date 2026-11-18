using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The forest screen that pops up at a stump: it shows which items the stump wants (how many you have given and how many you carry),
/// and lets you offer them. Built from code, no setup. Controls: Space / Enter / E = offer, Esc / Q = close.
/// What the stump is for (heal a ForestZone) is an IForestOffering.
/// </summary>
public class ForestStumpUI : MonoBehaviour
{
    private static ForestStumpUI instance;
    public static bool IsOpen { get { return instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf; } }

    public static void Open(ForestStump stump, IForestOffering target, PlayerActions player, string whisper)
    {
        if (IsOpen || target == null) return;
        if (instance == null)
        {
            var go = new GameObject("ForestStumpUI");
            instance = go.AddComponent<ForestStumpUI>();
            instance.Build();
        }
        instance.StartCoroutine(instance.Run(stump, target, player, whisper));
    }

    // The look (cover, page, ink colours) comes from the Book Skin: Assets/Resources/BookSkin.asset.

    private Canvas canvas;
    private RectTransform root;
    private Font font;

    private void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cg = new GameObject("Canvas");
        cg.transform.SetParent(transform, false);
        canvas = cg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 235;
        var sc = cg.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280f, 720f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        var rg = new GameObject("Root", typeof(RectTransform));
        root = rg.GetComponent<RectTransform>();
        root.SetParent(cg.transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
        cg.SetActive(false);
    }

    private RectTransform Rt(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    private Image Img(Transform parent, Vector2 pos, Vector2 size, Color c, Sprite s = null)
    {
        var im = Rt("Img", parent, pos, size).gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        return im;
    }

    private Text Txt(Transform parent, string s, int size, Color c, Vector2 pos, Vector2 box, TextAnchor a = TextAnchor.MiddleCenter)
    {
        var t = Rt("Text", parent, pos, box).gameObject.AddComponent<Text>();
        t.font = font; t.text = s; t.fontSize = size; t.color = c; t.alignment = a;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void Clear() { for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject); }

    private static bool Pressed(bool confirm)
    {
        Keyboard kb = Keyboard.current; Gamepad gp = Gamepad.current;
        if (confirm)
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
        return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)) || (gp != null && gp.buttonEast.wasPressedThisFrame);
    }

    private IEnumerator Run(ForestStump stump, IForestOffering target, PlayerActions player, string whisper)
    {
        GameState.LockInput();
        canvas.gameObject.SetActive(true);
        yield return null;                                      // so the key that opened this isn't read again

        Inventory inv = player != null ? player.Inventory : Inventory.Instance;
        bool isZone = target is ForestZone;
        string msg = !string.IsNullOrEmpty(whisper) ? whisper
                   : isZone ? "The old stump hums. Faint roots glow beneath the bark.\nGive it what it asks and the forest around it will heal."
                            : "The old stump hums. Faint roots glow beneath the bark.\nGive it what it asks and the thorns will let go.";
        bool done = false;

        while (!done)
        {
            Draw(target, inv, msg);
            yield return null;
            while (!Pressed(true) && !Pressed(false)) yield return null;
            if (Pressed(false)) break;

            // offer: give as much as possible of each wanted item
            int gaveAny = 0;
            bool allMet = true;
            string id = target.OfferingId;
            ForestRequirement[] reqs = target.Requirements;
            if (reqs != null)
                for (int i = 0; i < reqs.Length; i++)
                {
                    ForestRequirement r = reqs[i];
                    if (r == null || r.item == null) continue;
                    int need = r.count - ForestProgress.Given(id, i);
                    if (need > 0 && inv != null)
                    {
                        int give = Mathf.Min(need, ForestItemMatch.Count(inv, r.item));
                        if (give > 0 && ForestItemMatch.Remove(inv, r.item, give) == give) { ForestProgress.AddGiven(id, i, give); gaveAny += give; }
                    }
                    if (ForestProgress.Given(id, i) < r.count) allMet = false;
                }

            if (allMet)
            {
                msg = isZone ? "The roots blaze green. Life rushes out through the ground..." : "The roots glow bright. The thorns shiver and let go...";
                Draw(target, inv, msg);
                yield return new WaitForSecondsRealtime(0.9f);
                target.Fulfil();
                if (stump != null) stump.Refresh();
                GameEvents.ShowToast(target.DoneMessage);
                done = true;
            }
            else msg = gaveAny > 0 ? "The stump drinks it in. It still wants more..." : "You have nothing the stump wants right now.";
            yield return null;
        }

        canvas.gameObject.SetActive(false);
        Clear();
        yield return null;
        GameState.UnlockInput();
    }

    // Layout of the page (the book is sized to fit these, so everything stays on the page).
    private const float PageWidth = 540f, MessageHeight = 60f, ReqRowHeight = 64f;

    /// <summary>Redraws the screen as a page in a leather book (the Book Skin), sized to fit every item the stump asks for.</summary>
    private void Draw(IForestOffering target, Inventory inv, string msg)
    {
        Clear();
        BookSkin skin = BookSkin.Current;
        Img(root, Vector2.zero, new Vector2(3000f, 2000f), skin.dimColor);

        ForestRequirement[] reqs = target.Requirements;
        int n = reqs == null ? 0 : reqs.Length;

        float bannerH = skin.NaturalHeight(BookSkin.Part.Banner, 40f);
        float listTop = bannerH + 8f + MessageHeight + 10f;
        float contentH = listTop + Mathf.Max(1, n) * ReqRowHeight;
        BookSkin.Book book = skin.BuildBook(root, "Book", false);
        book.root.anchorMin = book.root.anchorMax = book.root.pivot = new Vector2(0.5f, 0.5f);
        book.root.anchoredPosition = Vector2.zero;
        book.root.sizeDelta = skin.CoverSizeFor(false, PageWidth, contentH);
        RectTransform page = book.leftContent;

        skin.NewBanner(page, "Title", 340f, 0f, 24).text = "The Stump Asks";

        Text message = skin.NewText(page, "Message", 17, skin.ink, TextAnchor.MiddleCenter);
        BookSkin.TopStrip(message.rectTransform, -bannerH - 8f, MessageHeight);
        message.text = msg;

        if (n == 0)
        {
            Text none = skin.NewText(page, "Nothing", 20, skin.fadedInk, TextAnchor.MiddleCenter);
            BookSkin.TopStrip(none.rectTransform, -listTop, ReqRowHeight);
            none.text = "It asks for nothing. Just offer.";
        }
        for (int i = 0; i < n; i++)
        {
            ForestRequirement r = reqs[i];
            if (r == null || r.item == null) continue;
            int given = Mathf.Min(r.count, ForestProgress.Given(target.OfferingId, i));
            int have = inv != null ? ForestItemMatch.Count(inv, r.item) : 0;
            bool met = given >= r.count;

            // one row per wanted item: picture, name, how many you carry, a progress bar, and given / needed
            RectTransform row = BookSkin.NewArea(page, "Wants " + r.item.itemName);
            BookSkin.TopStrip(row, -listTop - i * ReqRowHeight, ReqRowHeight - 6f);
            skin.AddRule(row);

            if (r.item.icon != null)
            {
                Image icon = Img(row, Vector2.zero, new Vector2(40f, 40f), Color.white, r.item.icon);
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            }

            Text name = skin.NewText(row, "Name", 21, skin.ink, TextAnchor.MiddleLeft);
            BookSkin.TopLeft(name.rectTransform, 54f, -2f, 220f, 28f);
            name.text = r.item.itemName;
            Text carry = skin.NewText(row, "Carry", 14, have > 0 ? skin.goodInk : skin.badInk, TextAnchor.MiddleLeft);
            BookSkin.TopLeft(carry.rectTransform, 54f, -30f, 220f, 20f);
            carry.text = "You carry " + have;

            // progress bar: an inked outline with a coloured fill inside it
            Image back = skin.NewImage(row, "Bar", BookSkin.Part.BarBack);
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = back.rectTransform.pivot = new Vector2(0f, 0.5f);
            back.rectTransform.anchoredPosition = new Vector2(290f, 0f);
            back.rectTransform.sizeDelta = new Vector2(140f, 14f);
            RectTransform fillArea = BookSkin.NewArea(back.rectTransform, "Fill Area");
            BookSkin.Inset(fillArea, Vector2.zero, Vector2.one, 2f, 2f, 2f, 2f);
            Image fill = Img(fillArea, Vector2.zero, Vector2.zero, met ? skin.goodInk : new Color(0.85f, 0.65f, 0.25f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)given / r.count), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            Text count = skin.NewText(row, "Count", 20, met ? skin.goodInk : skin.ink, TextAnchor.MiddleRight);
            count.rectTransform.anchorMin = new Vector2(1f, 0f);
            count.rectTransform.anchorMax = new Vector2(1f, 1f);
            count.rectTransform.pivot = new Vector2(1f, 0.5f);
            count.rectTransform.anchoredPosition = new Vector2(-4f, 0f);
            count.rectTransform.sizeDelta = new Vector2(90f, 0f);
            count.text = given + " / " + r.count;
        }

        // key help, pressed in gold into the leather under the page
        skin.NewCoverHint(book.root, "[Space] Offer      [Esc] Leave", 16);
    }
}

/// <summary>
/// Matches items for the forest. Two different item assets that share a name (for example two "Stone" assets made by different packs) count as the SAME item,
/// so a stump asking for "Stone" accepts any stack called "Stone" in your inventory.
/// </summary>
public static class ForestItemMatch
{
    public static bool Same(ItemData a, ItemData b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        return string.Equals((a.itemName ?? "").Trim(), (b.itemName ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static int Count(Inventory inv, ItemData item)
    {
        if (inv == null || item == null) return 0;
        int total = 0;
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            ItemStack s = inv.GetSlot(i);
            if (s != null && !s.IsEmpty && Same(s.item, item)) total += s.count;
        }
        return total;
    }

    /// <summary>Removes up to 'n' matching items from any slots. Returns how many were removed.</summary>
    public static int Remove(Inventory inv, ItemData item, int n)
    {
        if (inv == null || item == null) return 0;
        int left = n;
        for (int i = 0; i < Inventory.SlotCount && left > 0; i++)
        {
            ItemStack s = inv.GetSlot(i);
            if (s == null || s.IsEmpty || !Same(s.item, item)) continue;
            left -= inv.RemoveFromSlot(i, left);
        }
        return n - left;
    }
}
