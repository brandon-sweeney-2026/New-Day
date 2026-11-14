using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The forest screen that pops up at a stump: it shows which items the stump wants (how many you have given and how many you carry),
/// and lets you offer them. Built from code, no setup. Controls: Space / Enter / E = offer, Esc / Q = close.
/// </summary>
public class ForestStumpUI : MonoBehaviour
{
    private static ForestStumpUI instance;
    public static bool IsOpen { get { return instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf; } }

    public static void Open(ForestStump stump, ForestDoor door, PlayerActions player, string whisper)
    {
        if (IsOpen) return;
        if (instance == null)
        {
            var go = new GameObject("ForestStumpUI");
            instance = go.AddComponent<ForestStumpUI>();
            instance.Build();
        }
        instance.StartCoroutine(instance.Run(stump, door, player, whisper));
    }

    // ---- look (change freely) ----
    private static readonly Color Panel = new Color(0.07f, 0.13f, 0.09f, 0.95f);
    private static readonly Color Leaf = new Color(0.45f, 0.85f, 0.5f);
    private static readonly Color Soft = new Color(0.78f, 0.95f, 0.8f);
    private static readonly Color Bad = new Color(1f, 0.55f, 0.6f);

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

    private IEnumerator Run(ForestStump stump, ForestDoor door, PlayerActions player, string whisper)
    {
        GameState.LockInput();
        canvas.gameObject.SetActive(true);
        yield return null;                                      // so the key that opened this isn't read again

        Inventory inv = player != null ? player.Inventory : Inventory.Instance;
        string msg = string.IsNullOrEmpty(whisper) ? "The old stump hums. Faint roots glow beneath the bark.\nGive it what it asks and the thorns will let go." : whisper;
        bool done = false;

        while (!done)
        {
            Draw(door, inv, msg);
            yield return null;
            while (!Pressed(true) && !Pressed(false)) yield return null;
            if (Pressed(false)) break;

            // offer: give as much as possible of each wanted item
            int gaveAny = 0;
            bool allMet = true;
            ForestRequirement[] reqs = door.requirements;
            if (reqs != null)
                for (int i = 0; i < reqs.Length; i++)
                {
                    ForestRequirement r = reqs[i];
                    if (r == null || r.item == null) continue;
                    int need = r.count - ForestProgress.Given(door.id, i);
                    if (need > 0 && inv != null)
                    {
                        int give = Mathf.Min(need, ForestItemMatch.Count(inv, r.item));
                        if (give > 0 && ForestItemMatch.Remove(inv, r.item, give) == give) { ForestProgress.AddGiven(door.id, i, give); gaveAny += give; }
                    }
                    if (ForestProgress.Given(door.id, i) < r.count) allMet = false;
                }

            if (allMet)
            {
                msg = "The roots glow bright. The thorns shiver and let go...";
                Draw(door, inv, msg);
                yield return new WaitForSecondsRealtime(0.9f);
                door.Unlock();
                stump.Refresh();
                GameEvents.ShowToast("The way forward is open.");
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

    private void Draw(ForestDoor door, Inventory inv, string msg)
    {
        Clear();
        Img(root, Vector2.zero, new Vector2(3000f, 2000f), new Color(0f, 0.03f, 0f, 0.55f));
        Img(root, Vector2.zero, new Vector2(620f, 460f), Leaf);
        Img(root, Vector2.zero, new Vector2(608f, 448f), Panel);
        Txt(root, "THE STUMP ASKS", 30, Leaf, new Vector2(0f, 190f), new Vector2(560f, 44f));
        Txt(root, msg, 17, Soft, new Vector2(0f, 128f), new Vector2(540f, 60f));

        ForestRequirement[] reqs = door.requirements;
        int n = reqs == null ? 0 : reqs.Length;
        if (n == 0) Txt(root, "It asks for nothing. Just offer.", 20, Soft, new Vector2(0f, 20f), new Vector2(500f, 40f));
        for (int i = 0; i < n; i++)
        {
            ForestRequirement r = reqs[i];
            if (r == null || r.item == null) continue;
            float y = 55f - i * 64f;
            int given = Mathf.Min(r.count, ForestProgress.Given(door.id, i));
            int have = inv != null ? ForestItemMatch.Count(inv, r.item) : 0;
            Img(root, new Vector2(-50f, y), new Vector2(520f, 56f), new Color(1f, 1f, 1f, 0.06f));
            if (r.item.icon != null) Img(root, new Vector2(-265f, y), new Vector2(40f, 40f), Color.white, r.item.icon);
            Txt(root, r.item.itemName, 21, Soft, new Vector2(-135f, y + 10f), new Vector2(200f, 28f), TextAnchor.MiddleLeft);
            Txt(root, "You carry " + have, 14, have > 0 ? Leaf : Bad, new Vector2(-135f, y - 14f), new Vector2(200f, 20f), TextAnchor.MiddleLeft);
            // progress bar
            Img(root, new Vector2(90f, y), new Vector2(150f, 14f), new Color(0f, 0f, 0f, 0.5f));
            float f = Mathf.Clamp01((float)given / r.count);
            var bar = Img(root, new Vector2(90f - 75f * (1f - f), y), new Vector2(150f * f, 14f), given >= r.count ? Leaf : new Color(0.9f, 0.8f, 0.4f));
            Txt(root, given + " / " + r.count, 20, given >= r.count ? Leaf : Soft, new Vector2(215f, y), new Vector2(110f, 28f));
        }

        Txt(root, "[Space] Offer      [Esc] Leave", 18, Leaf, new Vector2(0f, -195f), new Vector2(540f, 30f));
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
