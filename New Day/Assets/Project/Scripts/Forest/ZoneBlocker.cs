using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something that blocks the way on until its ForestZone heals: a bramble wall, a broken bridge, a goo pool, a pile of
/// trunks, a thorn arch or a fog bank. When the zone heals and the green wave reaches it, it shakes, changes into its
/// open picture (or fades away, for fog) in a burst of sparkles, and stops blocking. It stays open for good.
///
/// Set up: a SpriteRenderer with the closed picture, Closed Sprite / Open Sprite filled in (blocker_*_sick / _healthy
/// from Assets/Project/Art/ForestHeal), and a collider that blocks the way while closed. For the BRIDGE, the creek's own
/// collider must leave a gap under the bridge: this blocker's collider fills that gap while the bridge is broken.
/// Press the action button in front of it for a hint.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ZoneBlocker : MonoBehaviour, IInteractable
{
    public enum Kind { Bramble, Bridge, GooPool, LogPile, Arch, Fog }

    [Header("What it is")]
    public Kind kind = Kind.Bramble;
    [Tooltip("The picture while the zone is sick. Leave empty to use the one the SpriteRenderer already has.")]
    public Sprite closedSprite;
    [Tooltip("The picture once the way is clear. Leave empty and it simply fades away (the fog).")]
    public Sprite openSprite;

    [Header("Zone")]
    [Tooltip("Leave empty: it uses the zone whose box it stands in.")]
    public ForestZone zone;

    [Header("Blocking")]
    [Tooltip("The collider that stops the player while it is closed. Leave empty to use the first collider on this object (a box is added if there is none).")]
    public Collider2D blockCollider;

    [Header("Clearing")]
    [Tooltip("Seconds it takes to clear once the wave reaches it.")]
    [Min(0.1f)] public float clearSeconds = 1.4f;
    [Tooltip("What the player is told when they inspect it. Leave empty for a sentence that fits its kind.")]
    [TextArea] public string hint = "";

    /// <summary>Every ZoneBlocker in the open scene (ForestZone goes through them when it heals).</summary>
    public static readonly List<ZoneBlocker> All = new List<ZoneBlocker>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { All.Clear(); }

    public bool IsOpen { get; private set; }

    public ForestZone Zone
    {
        get
        {
            if (zone == null) zone = ForestZone.At(transform.position);
            return zone;
        }
    }

    public string InteractPrompt { get { return IsOpen ? "" : "Inspect"; } }

    private SpriteRenderer sr;
    private SpriteRenderer next;          // the open picture, fading in on top while clearing
    private Vector3 basePos;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (closedSprite == null) closedSprite = sr.sprite;
        basePos = transform.localPosition;
        if (blockCollider == null) blockCollider = GetComponent<Collider2D>();
        if (blockCollider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            if (sr.sprite != null) { box.size = sr.sprite.bounds.size; box.offset = sr.sprite.bounds.center; }
            blockCollider = box;
        }
    }

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Start()
    {
        ForestZone z = Zone;
        SetOpen(z != null && z.IsHealed, true);
    }

    private void Update()
    {
        // a fog bank breathes gently while it is closed
        if (kind == Kind.Fog && !IsOpen && next == null)
        {
            Color c = sr.color; c.a = 0.8f + 0.2f * Mathf.Sin(Time.time * 1.3f + basePos.x); sr.color = c;
        }
    }

    /// <summary>Opens (true) or closes (false) the way. 'instant' = no animation.</summary>
    public void SetOpen(bool open, bool instant)
    {
        IsOpen = open;
        if (blockCollider != null) blockCollider.enabled = !open;
        StopAllCoroutines();
        if (next != null) { Destroy(next.gameObject); next = null; }
        transform.localPosition = basePos;
        if (instant || !open || !isActiveAndEnabled) ShowFinal(open);
        else StartCoroutine(Clear());
    }

    private void ShowFinal(bool open)
    {
        Sprite s = open ? openSprite : closedSprite;
        sr.enabled = s != null;
        if (s != null) sr.sprite = s;
        sr.color = Color.white;
    }

    private IEnumerator Clear()
    {
        Color col = ForestHealSettings.Current.sparkleColor;
        Bounds b = sr.bounds;

        // 1. it trembles (fog doesn't: it just starts to thin)
        float shake = kind == Kind.Fog ? 0f : clearSeconds * 0.35f;
        for (float t = 0f; t < shake; t += Time.deltaTime)
        {
            float k = t / shake;
            transform.localPosition = basePos + new Vector3(Mathf.Sin(Time.time * 60f) * 0.04f * k, 0f, 0f);
            yield return null;
        }
        transform.localPosition = basePos;

        // 2. a burst of sparkles over the whole thing
        for (int i = 0; i < 22; i++)
        {
            Vector3 p = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), 0f);
            HealSpark.Spawn(p, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.2f)), Random.Range(0.6f, 1.1f), Random.Range(0.08f, 0.15f), col, sr.sortingOrder + 2);
        }

        // 3. the open picture fades in on top while the closed one fades out (fog drifts up as it goes)
        if (openSprite != null)
        {
            var go = new GameObject("Opening");
            go.transform.SetParent(transform, false);
            next = go.AddComponent<SpriteRenderer>();
            next.sprite = openSprite;
            next.sortingLayerID = sr.sortingLayerID;
            next.sortingOrder = sr.sortingOrder;
            next.sharedMaterial = sr.sharedMaterial;
            next.color = new Color(1f, 1f, 1f, 0f);
        }
        float fade = clearSeconds - shake;
        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / fade);
            sr.color = new Color(1f, 1f, 1f, 1f - k);
            if (next != null) next.color = new Color(1f, 1f, 1f, k);
            if (kind == Kind.Fog) transform.localPosition = basePos + new Vector3(0f, k * 0.8f, 0f);
            yield return null;
        }
        if (next != null) { Destroy(next.gameObject); next = null; }
        transform.localPosition = basePos;
        ShowFinal(true);
    }

    public void Interact(PlayerActions player)
    {
        if (IsOpen) return;
        GameEvents.ShowToast(!string.IsNullOrEmpty(hint) ? hint : DefaultHint());
    }

    private string DefaultHint()
    {
        string where = Zone != null ? Zone.displayName : "this part of the forest";
        string what;
        switch (kind)
        {
            case Kind.Bridge:  what = "The bridge has rotted through."; break;
            case Kind.GooPool: what = "A pool of purple goo bubbles across the path."; break;
            case Kind.LogPile: what = "Rotten trunks are piled across the path."; break;
            case Kind.Arch:    what = "Thorns have woven the old arch shut."; break;
            case Kind.Fog:     what = "A thick purple fog hangs here. You can't see the way."; break;
            default:           what = "A wall of sick, thorny brambles blocks the way."; break;
        }
        return what + " Heal " + where + " at its heart stump and the forest will clear it.";
    }
}
