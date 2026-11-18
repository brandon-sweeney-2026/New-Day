using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the DAYTIME sun shadow of every wall and cliff (every WallShadowBlock) in the scene.
/// It creates itself as soon as a scene has a WallShadowBlock, so you never add it by hand.
///
/// How it works: Unity's 2D sun (a Global light) cannot make shadows, so like the tree shadows (ShadowCaster)
/// this one is "painted". It takes the solid rectangles of all walls, slides each one along the shadow direction
/// the LightingManager gives (leaning left in the morning, straight down at noon, right in the evening), and paints
/// everything it passes over into ONE pixel-art picture lying on the ground. Because it is one picture, places where
/// two walls' shadows overlap are not extra dark. The picture is redrawn only when the sun has moved.
///
/// Settings are on this object while playing (select "WallSunShadows" in the Hierarchy). The master switch is
/// "Wall Sun Shadows" on the LightingManager; the shadow darkness follows the LightingManager's "Shadow Darkness".
/// </summary>
[DefaultExecutionOrder(50)]
public class WallSunShadows : MonoBehaviour
{
    public static WallSunShadows Instance { get; private set; }

    [Header("Look")]
    [Tooltip("Shadow pixels per tile. 16 = as sharp as the art. Lower is faster and uses less memory; it lowers itself if the area is huge.")]
    [Range(2, 16)] public int pixelsPerUnit = 8;
    [Tooltip("1 = as dark as the tree shadows. Higher = darker wall shadows.")]
    [Range(0f, 2f)] public float darknessMultiplier = 1f;

    [Header("Speed")]
    [Tooltip("The picture is redrawn at most this often (seconds), even if the sun moves faster.")]
    [Min(0f)] public float minSecondsBetweenRedraws = 0.25f;
    [Tooltip("Biggest picture allowed (pixels per side). If the walls cover more, the shadow gets blockier instead.")]
    [Range(256, 8192)] public int maxTextureSize = 4096;

    private static readonly List<WallShadowBlock> blocks = new List<WallShadowBlock>();
    private static bool shuttingDown;

    private class Piece { public Rect rect; public float height; }
    private readonly List<Piece> pieces = new List<Piece>();
    private readonly List<Rect> rectBuf = new List<Rect>();
    private bool piecesDirty = true;

    private SpriteRenderer view;
    private Texture2D tex;
    private byte[] pixels;                // 4 bytes (red, green, blue, alpha) per shadow pixel
    private int texW, texH, usedPpu;
    private Vector2 origin;               // world position of the picture's bottom-left corner
    private float builtShear = float.NaN, builtYScale = float.NaN, nextRedraw;

    // Static lists survive pressing Play again when "Enter Play Mode Options" skip the domain reload, so empty them here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { blocks.Clear(); shuttingDown = false; Instance = null; }

    // ---------------------------------------------------------------- walls signing in and out
    public static void Register(WallShadowBlock b)
    {
        if (b == null || blocks.Contains(b)) return;
        blocks.Add(b);
        if (Instance == null && !shuttingDown)
        {
            var go = new GameObject("WallSunShadows");
            // keep it in the same scene as the walls, so it is removed together with them on a scene change
            if (b.gameObject.scene.IsValid() && go.scene != b.gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, b.gameObject.scene);
            go.AddComponent<WallSunShadows>();
        }
        if (Instance != null) Instance.piecesDirty = true;
    }

    public static void Unregister(WallShadowBlock b)
    {
        blocks.Remove(b);
        if (Instance != null) Instance.piecesDirty = true;
    }

    /// <summary>Call after changing walls while playing (for example painting a cliff tile) so the shadow is redrawn.</summary>
    public static void MarkDirty() { if (Instance != null) Instance.piecesDirty = true; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var go = new GameObject("WallSunShadowPicture");
        go.transform.SetParent(transform, false);
        // the lighting system must not darken this picture again or give it a shadow of its own
        var ex = go.AddComponent<LightingExempt>();
        ex.skipShadow = true; ex.skipDarkness = true;
        view = go.AddComponent<SpriteRenderer>();
        view.sharedMaterial = LightingManager.ShadowMaterial;
        view.enabled = false;
    }

    private void OnApplicationQuit() { shuttingDown = true; }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (tex != null) Destroy(tex);
        if (view != null && view.sprite != null) Destroy(view.sprite);
    }

    // ---------------------------------------------------------------- every frame
    private void LateUpdate()
    {
        for (int i = blocks.Count - 1; i >= 0; i--)
            if (blocks[i] == null) { blocks.RemoveAt(i); piecesDirty = true; }
        LightingManager m = LightingManager.Instance;
        bool visible = m != null && m.wallSunShadows && m.ShadowsVisible && blocks.Count > 0;
        if (!visible) { view.enabled = false; return; }

        if (piecesDirty) { GatherPieces(); builtShear = float.NaN; }
        if (pieces.Count == 0) { view.enabled = false; return; }

        bool sunMoved = !Mathf.Approximately(m.ShadowShear, builtShear) || !Mathf.Approximately(m.ShadowYScale, builtYScale);
        if (sunMoved && Time.unscaledTime >= nextRedraw)
        {
            nextRedraw = Time.unscaledTime + minSecondsBetweenRedraws;
            Redraw(m);
        }

        view.enabled = tex != null;
        view.color = new Color(0f, 0f, 0f, Mathf.Clamp01(m.ShadowAlpha * darknessMultiplier));
        view.sortingOrder = m.ShadowSortingOrder;
    }

    // ---------------------------------------------------------------- collecting the walls
    private void GatherPieces()
    {
        piecesDirty = false;
        pieces.Clear();
        bool layerSet = false;
        foreach (WallShadowBlock b in blocks)
        {
            if (b == null || !b.sunShadow || b.height <= 0f || !b.isActiveAndEnabled) continue;
            rectBuf.Clear();
            b.CollectRects(rectBuf);
            foreach (Rect r in rectBuf) pieces.Add(new Piece { rect = r, height = b.height });
            if (!layerSet && rectBuf.Count > 0)
            {
                Renderer rr = b.MainRenderer();
                if (rr != null) { view.sortingLayerID = rr.sortingLayerID; layerSet = true; }
            }
        }
    }

    // ---------------------------------------------------------------- painting the picture
    private void Redraw(LightingManager m)
    {
        builtShear = m.ShadowShear;
        builtYScale = m.ShadowYScale;
        EnsureTexture(m);
        if (tex == null) return;

        System.Array.Clear(pixels, 0, pixels.Length);
        float ppu = usedPpu;

        // 1. slide every wall piece along the shadow direction and paint what it passes over
        foreach (Piece p in pieces)
        {
            Vector2 v = new Vector2(builtShear * p.height, -builtYScale * p.height) * ppu;   // shadow tip offset, in pixels
            Rect r = ToPixels(p.rect);
            Paint(r.xMin, r.yMin, r.xMax, r.yMax, v.x, v.y, 255);
        }
        // 2. rub out the walls themselves, so the shadow only lies on the ground around them
        foreach (Piece p in pieces)
        {
            Rect r = ToPixels(p.rect);
            Paint(r.xMin, r.yMin, r.xMax, r.yMax, 0f, 0f, 0);
        }

        tex.SetPixelData(pixels, 0);
        tex.Apply(false);
    }

    private Rect ToPixels(Rect world)
    {
        return Rect.MinMaxRect((world.xMin - origin.x) * usedPpu, (world.yMin - origin.y) * usedPpu,
                               (world.xMax - origin.x) * usedPpu, (world.yMax - origin.y) * usedPpu);
    }

    /// <summary>
    /// Paints the shape a rectangle (x0,y0)-(x1,y1) covers while it slides from where it is to (vx,vy) further on.
    /// Done one pixel row at a time: for each row, work out which part of the slide crosses that row and fill
    /// the matching span. All numbers are in pixels of the picture. vy is always 0 or below (shadows fall down).
    /// </summary>
    private void Paint(float x0, float y0, float x1, float y1, float vx, float vy, byte alpha)
    {
        float drop = -vy;                                               // how far down the shadow reaches
        int rowFrom = Mathf.Max(0, Mathf.FloorToInt(y0 - drop));
        int rowTo = Mathf.Min(texH - 1, Mathf.CeilToInt(y1));
        for (int j = rowFrom; j <= rowTo; j++)
        {
            float yc = j + 0.5f;                                        // centre of this pixel row
            float tMin, tMax;                                           // part of the slide (0 = start, 1 = end) that covers this row
            if (drop < 0.0001f)
            {
                if (yc < y0 || yc > y1) continue;
                tMin = 0f; tMax = 1f;
            }
            else
            {
                tMin = Mathf.Max(0f, (y0 - yc) / drop);
                tMax = Mathf.Min(1f, (y1 - yc) / drop);
                if (tMin > tMax) continue;
            }
            float xl = x0 + Mathf.Min(tMin * vx, tMax * vx);
            float xr = x1 + Mathf.Max(tMin * vx, tMax * vx);
            int i0 = Mathf.Max(0, Mathf.CeilToInt(xl - 0.5f));
            int i1 = Mathf.Min(texW - 1, Mathf.CeilToInt(xr - 0.5f) - 1);
            int idx = (j * texW + i0) * 4 + 3;                          // the alpha byte of the first pixel
            for (int i = i0; i <= i1; i++, idx += 4) pixels[idx] = alpha;
        }
    }

    /// <summary>
    /// Makes the picture big enough for every wall plus the longest shadow the sun can ever give,
    /// so it is not resized as the day goes on. Only remade when the walls change.
    /// </summary>
    private void EnsureTexture(LightingManager m)
    {
        float maxHeight = 0f;
        Rect all = pieces[0].rect;
        foreach (Piece p in pieces)
        {
            maxHeight = Mathf.Max(maxHeight, p.height);
            all = Rect.MinMaxRect(Mathf.Min(all.xMin, p.rect.xMin), Mathf.Min(all.yMin, p.rect.yMin),
                                  Mathf.Max(all.xMax, p.rect.xMax), Mathf.Max(all.yMax, p.rect.yMax));
        }
        float side = maxHeight * Mathf.Max(Mathf.Abs(m.ShadowShear), Mathf.Tan(m.shadowCapDegrees * Mathf.Deg2Rad) * Mathf.Abs(m.shadowStretch));
        float down = maxHeight * Mathf.Max(m.ShadowYScale, m.noonShadowHeight, m.edgeShadowHeight);
        all = Rect.MinMaxRect(all.xMin - side - 1f, all.yMin - down - 1f, all.xMax + side + 1f, all.yMax + 1f);

        // fewer pixels per tile if the area would make the picture too big
        int ppu = pixelsPerUnit;
        while (ppu > 1 && (all.width * ppu > maxTextureSize || all.height * ppu > maxTextureSize)) ppu--;

        // snap the corner to the shadow pixel grid so shadow pixels line up with the tiles
        Vector2 o = new Vector2(Mathf.Floor(all.xMin), Mathf.Floor(all.yMin));
        int w = Mathf.Clamp(Mathf.CeilToInt((all.xMax - o.x) * ppu), 1, maxTextureSize);
        int h = Mathf.Clamp(Mathf.CeilToInt((all.yMax - o.y) * ppu), 1, maxTextureSize);

        if (tex != null && w == texW && h == texH && ppu == usedPpu && o == origin) return;

        if (ppu != pixelsPerUnit)
            Debug.Log("WallSunShadows: the walls cover a big area, so the sun shadow uses " + ppu + " pixels per tile instead of " + pixelsPerUnit + ".");

        if (tex != null) Destroy(tex);
        if (view.sprite != null) Destroy(view.sprite);
        texW = w; texH = h; usedPpu = ppu; origin = o;
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "WallSunShadowTexture";
        tex.filterMode = FilterMode.Point;                              // crisp pixel-art edges
        tex.wrapMode = TextureWrapMode.Clamp;
        pixels = new byte[w * h * 4];                                   // all zero = black and fully see-through
        view.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
        view.transform.position = new Vector3(o.x, o.y, 0f);
    }
}
