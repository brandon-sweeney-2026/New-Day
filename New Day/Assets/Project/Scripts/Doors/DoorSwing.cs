using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the door painted on a building swing open and shut.
///
/// The doors are part of each building's picture, so this script cuts the door rectangle out of that picture
/// (and out of any layers drawn on top of it, like the farmhouse's painted door upgrade), lays the copy exactly
/// over the painted door, and then squeezes it toward its hinge while darkening it. Behind it a dark "doorway"
/// shows, so it looks like the door swings inward.
///
/// You don't need to add this by hand: HouseDoor and SuburbHouseDoor add it to themselves when the game starts.
/// It finds the building picture by name in the Known Doors table below. A building that is not in the table
/// simply has no door animation (the screen still fades). To animate a new building, either add its picture to
/// the table, or add this component to its door object yourself and fill in "Door Pixels".
/// </summary>
public class DoorSwing : MonoBehaviour
{
    [Header("Where the door is in the picture")]
    [Tooltip("The door rectangle in the building picture, in pixels, measured from the picture's BOTTOM-LEFT corner. " +
             "Leave the width at 0 to look the picture up in the Known Doors table instead.")]
    [SerializeField] private RectInt doorPixels = new RectInt(0, 0, 0, 0);
    [Tooltip("Two doors that open from the middle (shops and city buildings).")]
    [SerializeField] private bool doubleDoor = false;
    [Tooltip("For a single door: tick if the hinges are on the right (the handle on the left).")]
    [SerializeField] private bool hingeOnRight = false;
    [Tooltip("The building's picture. Leave empty to find it automatically among the building's sprites.")]
    [SerializeField] private SpriteRenderer picture;

    [Header("Animation")]
    [Tooltip("Seconds the door takes to swing open.")]
    [Range(0.05f, 1f)] [SerializeField] private float openSeconds = 0.22f;
    [Tooltip("Seconds the door takes to swing shut.")]
    [Range(0.05f, 1f)] [SerializeField] private float closeSeconds = 0.22f;
    [Tooltip("How wide the door still looks when fully open (0.2 = a fifth of its width, seen side-on).")]
    [Range(0.05f, 0.6f)] [SerializeField] private float openWidth = 0.2f;
    [Tooltip("How dark the door gets as it turns away from you (1 = no change, 0.5 = half as bright).")]
    [Range(0.2f, 1f)] [SerializeField] private float openShade = 0.6f;
    [Tooltip("Colour of the dark gap you see through the open door.")]
    [SerializeField] private Color insideColor = new Color(0.08f, 0.06f, 0.07f, 1f);

    // ------------------------------------------------------------------
    // Known doors: picture name -> door rectangle (x, y from the bottom, width, height) in pixels.
    // Measured from the PNGs. Add a line here when you draw a new building with a door.
    // ------------------------------------------------------------------
    private struct Known
    {
        public RectInt rect; public bool twoLeaves;
        public Known(int x, int y, int w, int h, bool twoLeaves) { rect = new RectInt(x, y, w, h); this.twoLeaves = twoLeaves; }
    }

    private static readonly Dictionary<string, Known> KnownDoors = new Dictionary<string, Known>
    {
        { "house_ext_details", new Known(53, 10, 22, 34, false) },   // the farmhouse (its door is on the Details layer)
        { "store_exterior",    new Known(56,  6, 16, 44, false) },   // the General Store
        { "hw_exterior",       new Known(62,  4, 36, 54, true)  },   // Frank's Home and Hardware
        { "city_university",   new Known(98, 12, 28, 48, true)  },
        { "city_tower",        new Known(76,  0, 40, 46, true)  },   // the Sterling tower
        { "city_hall",         new Known(100, 12, 24, 42, true) },
        { "city_plant",        new Known(98,  6, 28, 42, false) },   // the treatment plant
        { "sub_house_1",       new Known(56,  4, 16, 28, false) },
        { "sub_house_2",       new Known(56,  4, 16, 28, false) },
        { "sub_house_3",       new Known(56,  6, 16, 26, false) },
        { "sub_house_4",       new Known(56,  6, 16, 26, false) },
        { "sub_house_5",       new Known(56,  6, 16, 26, false) },
        { "sub_house_6",       new Known(56,  6, 16, 26, false) },
        { "sub_house_7",       new Known(44,  6, 20, 26, false) },
        { "sub_house_8",       new Known(44,  6, 20, 26, false) },
        { "sub_house_9",       new Known(56,  4, 16, 28, false) },
        { "sub_house_10",      new Known(56,  6, 16, 26, false) },
    };

    // One swinging half of the door: a hinge point plus the cut-out pieces that hang from it.
    private class Leaf
    {
        public Transform hinge;
        public int widthPixels;
        public List<SpriteRenderer> pieces = new List<SpriteRenderer>();
        public List<Color> colors = new List<Color>();
    }

    private readonly List<Leaf> leaves = new List<Leaf>();
    private SpriteRenderer doorway;
    private GameObject fxRoot;
    private float openAmount;          // 0 = shut, 1 = fully open
    private Coroutine running;
    private static Sprite whitePixel;

    /// <summary>True when this door found its picture and can animate.</summary>
    public bool IsReady { get { return fxRoot != null; } }

    private void Start()
    {
        Build();
        SetAmount(0f);
    }

    // ------------------------------------------------------------------
    // Playing
    // ------------------------------------------------------------------

    /// <summary>Swings the door open. Use with "yield return" to wait until it is open.</summary>
    public IEnumerator Open() { return Animate(1f, openSeconds); }

    /// <summary>Swings the door shut. Use with "yield return" to wait until it is shut.</summary>
    public IEnumerator Close() { return Animate(0f, closeSeconds); }

    /// <summary>Jumps straight to open or shut (used while the screen is black).</summary>
    public void SetOpenInstantly(bool open)
    {
        if (running != null) { StopCoroutine(running); running = null; }
        SetAmount(open ? 1f : 0f);
    }

    /// <summary>Starts closing the door on its own (nothing waits for it).</summary>
    public void CloseInBackground()
    {
        if (!IsReady || !isActiveAndEnabled) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Animate(0f, closeSeconds));
    }

    private IEnumerator Animate(float target, float seconds)
    {
        if (!IsReady) yield break;
        float start = openAmount;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            k = 1f - (1f - k) * (1f - k);             // ease out: fast at first, settles gently
            SetAmount(Mathf.Lerp(start, target, k));
            yield return null;
        }
        SetAmount(target);
        running = null;
    }

    /// <summary>Draws the door at a given openness (0 = shut, 1 = open).</summary>
    private void SetAmount(float amount)
    {
        openAmount = Mathf.Clamp01(amount);
        if (!IsReady) return;

        // When shut, hide the copy so the building's own picture shows untouched.
        bool show = openAmount > 0.001f;
        fxRoot.SetActive(show);
        if (!show) return;

        float shade = Mathf.Lerp(1f, openShade, openAmount);
        float squeeze = Mathf.Lerp(1f, openWidth, openAmount);
        foreach (Leaf leaf in leaves)
        {
            // Round to whole pixels so the pixel art stays crisp while it squeezes.
            int visible = Mathf.Max(1, Mathf.RoundToInt(leaf.widthPixels * squeeze));
            Vector3 s = leaf.hinge.localScale;
            s.x = Mathf.Sign(s.x) * visible / (float)leaf.widthPixels;
            leaf.hinge.localScale = s;
            for (int i = 0; i < leaf.pieces.Count; i++)
            {
                Color c = leaf.colors[i];
                leaf.pieces[i].color = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
            }
        }
    }

    /// <summary>Finds a working door near a spot (for example the ExitSpot in front of a building).</summary>
    public static DoorSwing FindNear(Transform spot)
    {
        if (spot == null || spot.parent == null) return null;
        foreach (DoorSwing d in spot.parent.GetComponentsInChildren<DoorSwing>())
            if (d.IsReady) return d;
        return null;
    }

    // ------------------------------------------------------------------
    // Building the cut-out copy
    // ------------------------------------------------------------------

    private void Build()
    {
        Transform building = transform.parent;
        if (building == null) return;

        // 1. Which picture, and where is its door?
        RectInt rect = doorPixels;
        bool twoLeaves = doubleDoor;
        SpriteRenderer main = picture;
        if (main == null || rect.width <= 0)
        {
            foreach (SpriteRenderer sr in building.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null) continue;
                if (main != null && sr != main) continue;
                Known k;
                if (!KnownDoors.TryGetValue(sr.sprite.name, out k)) continue;
                main = sr;
                if (rect.width <= 0) { rect = k.rect; twoLeaves = k.twoLeaves; }
                break;
            }
        }
        if (main == null || main.sprite == null || rect.width <= 0 || rect.height <= 0) return;   // no known door: stay inert

        // 2. The door's corners in the world.
        Sprite ms = main.sprite;
        float ppu = ms.pixelsPerUnit;
        Vector3 bottomLeft = main.transform.TransformPoint(new Vector3((rect.x - ms.pivot.x) / ppu, (rect.y - ms.pivot.y) / ppu, 0f));
        Vector3 topRight = main.transform.TransformPoint(new Vector3((rect.xMax - ms.pivot.x) / ppu, (rect.yMax - ms.pivot.y) / ppu, 0f));
        Vector2 size = topRight - bottomLeft;

        // 3. Every layer of the building that covers the door, bottom layer first.
        var layers = new List<SpriteRenderer>();
        foreach (SpriteRenderer sr in building.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || !sr.enabled) continue;
            if (sr.transform.IsChildOf(transform)) continue;
            if (sr.sortingLayerID != main.sortingLayerID) continue;
            layers.Add(sr);
        }
        layers.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        int topOrder = main.sortingOrder;
        foreach (SpriteRenderer sr in layers) topOrder = Mathf.Max(topOrder, sr.sortingOrder);

        // 4. The holder for everything we draw, sitting on the door's bottom-left corner.
        fxRoot = new GameObject("DoorSwingFx");
        fxRoot.transform.SetParent(building, false);
        fxRoot.transform.position = new Vector3(bottomLeft.x, bottomLeft.y, main.transform.position.z);

        // The dark gap behind the door.
        var gap = new GameObject("Doorway");
        gap.transform.SetParent(fxRoot.transform, false);
        gap.transform.localPosition = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
        gap.transform.localScale = new Vector3(size.x, size.y, 1f);
        doorway = gap.AddComponent<SpriteRenderer>();
        doorway.sprite = WhitePixel();
        doorway.color = insideColor;
        doorway.sharedMaterial = main.sharedMaterial;
        doorway.sortingLayerID = main.sortingLayerID;
        doorway.sortingOrder = topOrder + 1;

        // 5. One leaf (or two, opening from the middle).
        leaves.Clear();
        if (twoLeaves)
        {
            int half = rect.width / 2;
            MakeLeaf(layers, new Rect(bottomLeft.x, bottomLeft.y, size.x * half / rect.width, size.y), half, false, topOrder + 2);
            MakeLeaf(layers, new Rect(bottomLeft.x + size.x * half / rect.width, bottomLeft.y, size.x * (rect.width - half) / rect.width, size.y), rect.width - half, true, topOrder + 2);
        }
        else MakeLeaf(layers, new Rect(bottomLeft.x, bottomLeft.y, size.x, size.y), rect.width, hingeOnRight, topOrder + 2);
    }

    /// <summary>Makes one swinging half: a hinge on one side of 'area' (world units) with a cut-out from each layer.</summary>
    private void MakeLeaf(List<SpriteRenderer> layers, Rect area, int widthPixels, bool hingeRight, int firstOrder)
    {
        var leaf = new Leaf { widthPixels = Mathf.Max(1, widthPixels) };
        var hinge = new GameObject(hingeRight ? "LeafRight" : "LeafLeft");
        hinge.transform.SetParent(fxRoot.transform, false);
        hinge.transform.position = new Vector3(hingeRight ? area.xMax : area.xMin, area.yMin, fxRoot.transform.position.z);
        leaf.hinge = hinge.transform;

        int order = firstOrder;
        foreach (SpriteRenderer layer in layers)
        {
            Sprite cut = CutOut(layer, area, out Vector2 cutBottomLeft);
            if (cut == null) continue;
            var piece = new GameObject("Piece_" + layer.name);
            piece.transform.SetParent(hinge.transform, false);
            piece.transform.position = new Vector3(cutBottomLeft.x, cutBottomLeft.y, hinge.transform.position.z);
            var sr = piece.AddComponent<SpriteRenderer>();
            sr.sprite = cut;
            sr.sharedMaterial = layer.sharedMaterial;
            sr.sortingLayerID = layer.sortingLayerID;
            sr.sortingOrder = order++;
            leaf.pieces.Add(sr);
            leaf.colors.Add(layer.color);
        }
        leaves.Add(leaf);
    }

    /// <summary>
    /// Copies the part of a layer's sprite that lies inside 'area' (world units). Returns null if it doesn't overlap.
    /// 'worldBottomLeft' is where the copy's bottom-left corner belongs.
    /// </summary>
    private static Sprite CutOut(SpriteRenderer layer, Rect area, out Vector2 worldBottomLeft)
    {
        worldBottomLeft = Vector2.zero;
        Sprite s = layer.sprite;
        if (s.packed && s.packingMode == SpritePackingMode.Tight) return null;   // tightly packed atlas sprites can't be cut by rectangle

        // The area's corners in this sprite's pixels (0,0 = bottom-left of the sprite).
        float ppu = s.pixelsPerUnit;
        Vector3 a = layer.transform.InverseTransformPoint(new Vector3(area.xMin, area.yMin, 0f));
        Vector3 b = layer.transform.InverseTransformPoint(new Vector3(area.xMax, area.yMax, 0f));
        int x0 = Mathf.RoundToInt(a.x * ppu + s.pivot.x), y0 = Mathf.RoundToInt(a.y * ppu + s.pivot.y);
        int x1 = Mathf.RoundToInt(b.x * ppu + s.pivot.x), y1 = Mathf.RoundToInt(b.y * ppu + s.pivot.y);
        x0 = Mathf.Max(x0, 0); y0 = Mathf.Max(y0, 0);
        x1 = Mathf.Min(x1, Mathf.RoundToInt(s.rect.width)); y1 = Mathf.Min(y1, Mathf.RoundToInt(s.rect.height));
        if (x1 <= x0 || y1 <= y0) return null;

        Rect tex = s.packed ? s.textureRect : s.rect;
        var cutRect = new Rect(tex.x + x0, tex.y + y0, x1 - x0, y1 - y0);
        Sprite cut = Sprite.Create(s.texture, cutRect, Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
        cut.name = s.name + "_door";
        worldBottomLeft = layer.transform.TransformPoint(new Vector3((x0 - s.pivot.x) / ppu, (y0 - s.pivot.y) / ppu, 0f));
        return cut;
    }

    /// <summary>A 1x1 white sprite (one world unit across), tinted for the doorway.</summary>
    private static Sprite WhitePixel()
    {
        if (whitePixel != null) return whitePixel;
        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whitePixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        whitePixel.name = "DoorwayPixel";
        return whitePixel;
    }
}
