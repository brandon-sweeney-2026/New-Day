using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives any piece of forest scenery (a tree, bush, rock, mushroom...) a sick picture and a healthy picture.
/// When its ForestZone heals, the green wave reaches it and it changes with a little "pop" and a few sparkles.
/// If the zone is already healed when the scene opens, it shows the healthy picture straight away.
///
/// Set up: put it next to the SpriteRenderer, fill in Sick Sprite and Healthy Sprite (for example tree_broad_sick /
/// tree_broad_healthy from Assets/Project/Art/ForestHeal). The zone is found by itself (the zone whose box it stands in).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HealSwap : MonoBehaviour
{
    [Header("Pictures")]
    [Tooltip("Shown while the zone is sick. Leave empty to use the picture the SpriteRenderer already has.")]
    public Sprite sickSprite;
    [Tooltip("Shown once the zone is healed.")]
    public Sprite healthySprite;

    [Header("Zone")]
    [Tooltip("Leave empty: it uses the zone whose box it stands in.")]
    public ForestZone zone;

    [Header("Once healthy")]
    [Tooltip("Rock gently in the wind once it has leaves again (trees, bushes, grass).")]
    public bool swayWhenHealthy = false;
    [Tooltip("Objects switched ON when healthy (flowers, a bird's nest...).")]
    public GameObject[] showWhenHealthy = new GameObject[0];
    [Tooltip("Objects switched OFF when healthy (goo drips, a purple glow...).")]
    public GameObject[] hideWhenHealthy = new GameObject[0];

    [Header("The change")]
    [Tooltip("Seconds of the little squash-and-pop when it changes.")]
    [Min(0.05f)] public float popSeconds = 0.35f;
    [Tooltip("Sparkles when it turns healthy.")]
    [Min(0)] public int sparkles = 5;

    /// <summary>Every HealSwap in the open scene (ForestZone goes through them when it heals).</summary>
    public static readonly List<HealSwap> All = new List<HealSwap>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { All.Clear(); }

    public bool IsHealthy { get; private set; }

    /// <summary>The zone this belongs to (the one set in the Inspector, or the one whose box it stands in).</summary>
    public ForestZone Zone
    {
        get
        {
            if (zone == null) zone = ForestZone.At(transform.position);
            return zone;
        }
    }

    private SpriteRenderer sr;
    private Vector3 baseScale;
    private WindSway sway;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sickSprite == null) sickSprite = sr.sprite;
        baseScale = transform.localScale;
    }

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Start()
    {
        ForestZone z = Zone;
        SetHealthy(z != null && z.IsHealed, true);
    }

    /// <summary>Shows the healthy (true) or sick (false) picture. 'instant' = no pop and no sparkles.</summary>
    public void SetHealthy(bool healthy, bool instant)
    {
        IsHealthy = healthy;
        foreach (GameObject g in showWhenHealthy) if (g != null) g.SetActive(healthy);
        foreach (GameObject g in hideWhenHealthy) if (g != null) g.SetActive(!healthy);
        StopAllCoroutines();
        if (instant || !isActiveAndEnabled)
        {
            transform.localScale = baseScale;
            ShowPicture(healthy);
            SetSway(healthy);
        }
        else StartCoroutine(Pop(healthy));
    }

    private void ShowPicture(bool healthy)
    {
        Sprite s = healthy ? healthySprite : sickSprite;
        if (s != null) sr.sprite = s;
    }

    private void SetSway(bool healthy)
    {
        if (!swayWhenHealthy) return;
        if (sway == null) sway = GetComponent<WindSway>();
        if (sway == null && healthy)
        {
            sway = gameObject.AddComponent<WindSway>();
            sway.maxAngle = 1.5f;                // a gentle rock, right for trees and bushes (add your own WindSway to choose)
        }
        if (sway != null) sway.enabled = healthy;
    }

    // squashes down a little, swaps the picture in the middle, and springs back up
    private IEnumerator Pop(bool healthy)
    {
        SetSway(false);                         // the sway would fight the squash
        if (healthy)
        {
            Color col = ForestHealSettings.Current.sparkleColor;
            float h = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
            for (int i = 0; i < sparkles; i++)
                HealSpark.Spawn(transform.position + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.1f, h), 0f),
                                new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.3f, 0.8f)), Random.Range(0.5f, 0.9f), Random.Range(0.06f, 0.12f), col, sr.sortingOrder + 1);
        }
        bool swapped = false;
        float t = 0f;
        while (t < popSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popSeconds);
            if (!swapped && k >= 0.5f) { ShowPicture(healthy); swapped = true; }
            float squash = Mathf.Sin(k * Mathf.PI) * (k < 0.5f ? -0.12f : 0.1f);     // down first, then a little overshoot
            transform.localScale = new Vector3(baseScale.x * (1f - squash * 0.5f), baseScale.y * (1f + squash), baseScale.z);
            yield return null;
        }
        if (!swapped) ShowPicture(healthy);
        transform.localScale = baseScale;
        SetSway(healthy);
    }
}
