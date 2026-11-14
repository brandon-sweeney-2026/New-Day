using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The enchanted grove: put it on an empty object in the middle of the grove and size the box (drag in the Scene view) to cover it.
/// - The first time the player steps in, a message appears and the flag "forest_grove_reached" is set.
/// - Fireflies and glowing motes drift around inside the box.
/// - Shadow animals can never be here: any that wander in are banished. Don't paint pollution here either - the grove should stay clean.
/// </summary>
public class ForestGrove : MonoBehaviour
{
    public Vector2 boxSize = new Vector2(24f, 18f);
    public string groveName = "The Grove";
    [TextArea] public string arriveMessage = "The air is clean here. Fireflies drift between the flowers.";

    [Header("Fireflies")]
    [Min(0)] public int fireflies = 28;
    public Color fireflyColor = new Color(1f, 0.95f, 0.55f, 1f);
    [Tooltip("Fireflies glow brighter as it gets dark.")] public bool brighterAtNight = true;

    [Header("Keep it clean")]
    public bool banishShadows = true;

    private Transform player;
    private bool inside;
    private float scan;
    private readonly List<Firefly> flies = new List<Firefly>();

    private class Firefly
    {
        public Transform t; public SpriteRenderer sr; public Vector2 center; public float speed, seed, size;
    }

    private void Start()
    {
        for (int i = 0; i < fireflies; i++)
        {
            var go = new GameObject("Firefly");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.Circle;
            sr.sortingOrder = 60;
            go.AddComponent<LightingExempt>();
            var f = new Firefly
            {
                t = go.transform, sr = sr, speed = Random.Range(0.25f, 0.6f), seed = Random.value * 100f,
                size = Random.Range(0.1f, 0.2f),
                center = new Vector2(Random.Range(-boxSize.x * 0.5f, boxSize.x * 0.5f), Random.Range(-boxSize.y * 0.5f, boxSize.y * 0.5f)),
            };
            go.transform.localScale = Vector3.one * f.size;
            flies.Add(f);
        }
    }

    private void Update()
    {
        float night = 0f;
        if (brighterAtNight && TimeManager.Instance != null)
        {
            float h = TimeManager.Instance.HoursDecimal;
            night = (h >= 19f || h < 5.5f) ? 1f : (h >= 17.5f ? (h - 17.5f) / 1.5f : (h < 7f && h >= 5.5f ? 1f - (h - 5.5f) / 1.5f : 0f));
        }
        foreach (Firefly f in flies)
        {
            float t = Time.time * f.speed + f.seed;
            Vector2 p = f.center + new Vector2(Mathf.Sin(t) * 2f + Mathf.Sin(t * 2.3f) * 0.6f, Mathf.Cos(t * 0.8f) * 1.4f);
            f.t.localPosition = new Vector3(p.x, p.y, 0f);
            float blink = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            Color c = fireflyColor; c.a = Mathf.Lerp(0.25f, 0.95f, blink) * Mathf.Lerp(0.5f, 1f, night);
            f.sr.color = c;
        }

        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            return;
        }

        bool now = Contains(player.position);
        if (now && !inside)
        {
            inside = true;
            if (!GameState.HasFlag("forest_grove_reached"))
            {
                GameState.SetFlag("forest_grove_reached");
                GameEvents.ShowToast(groveName + ": " + arriveMessage);
            }
        }
        else if (!now) inside = false;

        scan -= Time.deltaTime;
        if (scan <= 0f)
        {
            scan = 1f;
            if (banishShadows) BanishShadows();
        }
    }

    public bool Contains(Vector2 p)
    {
        Vector2 c = transform.position;
        return Mathf.Abs(p.x - c.x) <= boxSize.x * 0.5f && Mathf.Abs(p.y - c.y) <= boxSize.y * 0.5f;
    }

    private void BanishShadows()
    {
        foreach (ShadowCreature s in FindObjectsByType<ShadowCreature>())
            if (s != null && Contains(s.transform.position)) Destroy(s.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 1f, 0.7f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0f));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 1f, 0.7f, 0.25f);
        Gizmos.DrawWireCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0f));
    }
}
