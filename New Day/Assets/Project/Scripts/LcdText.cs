using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws text from a picture strip of letters (a "bitmap font") using small UI images.
/// The HUD uses it for the digital alarm-clock look: big seven-segment digits for the time, and a small LCD font for the date.
/// It is plain code (not a component): GameHUD creates and updates it.
/// </summary>
public class LcdText
{
    private readonly RectTransform container;
    private readonly Image[] cells;
    private readonly Image[] ghosts;
    private readonly Dictionary<char, Sprite> map = new Dictionary<char, Sprite>();
    private readonly int maxChars;
    private readonly bool alignRight;
    private int blinkIndex = -1;

    public RectTransform Rect { get { return container; } }

    /// <param name="atlas">One row of equally wide cells.</param>
    /// <param name="charset">The characters in the strip, in order.</param>
    /// <param name="scale">Screen pixels per picture pixel.</param>
    public LcdText(RectTransform parent, string name, Texture2D atlas, string charset, int cellW, int cellH, float scale, int maxChars, bool alignRight)
    {
        this.maxChars = maxChars;
        this.alignRight = alignRight;

        for (int i = 0; i < charset.Length; i++)
            map[charset[i]] = Sprite.Create(atlas, new Rect(i * cellW, 0, cellW, cellH), new Vector2(0.5f, 0.5f), 50f);

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        container = go.GetComponent<RectTransform>();
        container.sizeDelta = new Vector2(maxChars * cellW * scale, cellH * scale);

        cells = new Image[maxChars];
        ghosts = new Image[maxChars];
        for (int i = 0; i < maxChars; i++)
        {
            ghosts[i] = MakeCell(go.transform, "Ghost " + i, i, cellW, cellH, scale);
            cells[i] = MakeCell(go.transform, "Char " + i, i, cellW, cellH, scale);
        }
    }

    private static Image MakeCell(Transform parent, string name, int index, int cellW, int cellH, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(cellW * scale, cellH * scale);
        rt.anchoredPosition = new Vector2(index * cellW * scale, 0f);
        img.enabled = false;
        return img;
    }

    /// <summary>Shows the text. 'ghostMask' (same length) lights the dim "unused segments" behind each character; ' ' = none.</summary>
    public void Set(string text, Color lit, string ghostMask = null, Color ghostColor = default(Color))
    {
        text = alignRight ? text.PadLeft(maxChars) : text.PadRight(maxChars);
        if (text.Length > maxChars) text = text.Substring(text.Length - maxChars);
        if (ghostMask != null) ghostMask = alignRight ? ghostMask.PadLeft(maxChars) : ghostMask.PadRight(maxChars);

        for (int i = 0; i < maxChars; i++)
        {
            char c = char.ToUpperInvariant(text[i]);
            Sprite s;
            bool has = c != ' ' && map.TryGetValue(c, out s);
            cells[i].enabled = has;
            if (has) { cells[i].sprite = map[c]; cells[i].color = lit; }

            char g = ghostMask != null && i < ghostMask.Length ? ghostMask[i] : ' ';
            bool hasGhost = g != ' ' && map.ContainsKey(g);
            ghosts[i].enabled = hasGhost;
            if (hasGhost) { ghosts[i].sprite = map[g]; ghosts[i].color = ghostColor; }
        }
    }

    /// <summary>Makes one character (the colon) flash on and off.</summary>
    public void SetBlinkIndex(int index) { blinkIndex = index; }

    public void Blink(bool on)
    {
        if (blinkIndex < 0 || blinkIndex >= cells.Length) return;
        Color c = cells[blinkIndex].color;
        c.a = on ? 1f : 0.12f;
        cells[blinkIndex].color = c;
    }
}
