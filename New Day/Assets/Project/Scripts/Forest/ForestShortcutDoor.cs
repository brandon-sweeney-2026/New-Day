using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A fairy door. Two of them work as a pair: one in the first (staging) room of the forest, one in the grove. Press the action button in front of one to
/// step out of the other. The one in the first room stays sealed until the player has made it through the maze (the flag "forest_grove_reached" is set when
/// they first step into the grove), so it can only be used to skip the maze AFTER finishing it.
///
/// Partner       - the door at the other end.
/// Require Flag  - the door only works once this flag is set. Leave empty for a door that always works (the one in the grove).
/// Arrive Offset - where the player appears, relative to this door, when they come through from the partner (default: just in front of it).
/// </summary>
public class ForestShortcutDoor : MonoBehaviour, IInteractable
{
    public ForestShortcutDoor partner;
    public string requireFlag = "forest_grove_reached";
    public Vector2 arriveOffset = new Vector2(0f, -1.3f);
    public Vector2 arriveFacing = Vector2.down;

    [Header("Look")]
    public Sprite lockedSprite;
    public Sprite openSprite;
    public Color glowColor = new Color(0.6f, 1f, 0.8f, 0.3f);

    [Header("Words")]
    public string prompt = "Step through the fairy door";
    [TextArea] public string lockedHint = "The fairy door is sealed. Only someone who has made it all the way through the maze can wake it.";

    private SpriteRenderer sr, glow;
    private bool unlocked;
    private float check, busyUntil;
    private static Image fadeImage;

    public string InteractPrompt { get { return unlocked ? prompt : "Inspect"; } }
    public Vector3 ArrivalPoint { get { return transform.position + (Vector3)arriveOffset; } }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (GetComponent<Collider2D>() == null)
        {
            var b = gameObject.AddComponent<BoxCollider2D>();
            b.size = new Vector2(2.2f, 3.1f); b.offset = new Vector2(0f, 0.35f); b.isTrigger = true;
        }
        var g = new GameObject("Glow");
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        g.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
        glow = g.AddComponent<SpriteRenderer>();
        glow.sprite = RuntimeSprites.Circle;
        glow.sortingOrder = sr != null ? sr.sortingOrder - 1 : 6;
        g.AddComponent<LightingExempt>();
        Refresh(true);
    }

    private void Update()
    {
        check -= Time.deltaTime;
        if (check <= 0f) { check = 0.4f; Refresh(false); }
        if (unlocked)
        {
            Color c = glowColor; c.a *= 0.7f + 0.3f * Mathf.Sin(Time.time * 2.5f);
            glow.color = c;
        }
    }

    private void Refresh(bool force)
    {
        bool now = string.IsNullOrEmpty(requireFlag) || GameState.HasFlag(requireFlag);
        if (!force && now == unlocked) return;
        unlocked = now;
        if (sr != null)
        {
            Sprite s = unlocked ? openSprite : lockedSprite;
            if (s != null) sr.sprite = s;
        }
        glow.color = unlocked ? glowColor : new Color(0f, 0f, 0f, 0f);
        if (unlocked && !force) GameEvents.ShowToast("The fairy door glows softly...");
    }

    public void Interact(PlayerActions player)
    {
        if (Time.unscaledTime < busyUntil) return;
        Refresh(false);
        if (!unlocked) { GameEvents.ShowToast(lockedHint); return; }
        if (partner == null) { GameEvents.ShowToast("This fairy door doesn't lead anywhere yet."); return; }
        busyUntil = Time.unscaledTime + 1f;
        StartCoroutine(Travel(player));
    }

    private IEnumerator Travel(PlayerActions player)
    {
        yield return Fade(0f, 1f, 0.25f);

        Vector3 to = partner.ArrivalPoint;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = to; rb.linearVelocity = Vector2.zero; }
        player.transform.position = to;
        if (player.Controller != null) player.Controller.FaceDirection(partner.arriveFacing);
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.SnapToTarget();

        yield return Fade(1f, 0f, 0.3f);
    }

    // a quick black fade, built on the fly so there is nothing to set up
    private static IEnumerator Fade(float from, float to, float seconds)
    {
        if (fadeImage == null)
        {
            var go = new GameObject("FairyDoorFade");
            var cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 500;
            var img = new GameObject("Black");
            img.transform.SetParent(go.transform, false);
            fadeImage = img.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;
            var rt = fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            Object.DontDestroyOnLoad(go);
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 1f, 0.8f, 0.8f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(2f, 2f, 0f));
        Gizmos.DrawWireSphere(ArrivalPoint, 0.3f);
        if (partner != null) Gizmos.DrawLine(transform.position, partner.transform.position);
    }
}
