using UnityEngine;

/// <summary>
/// A tiny glowing speck that drifts and fades: the sparkles of the Healing Forest (the healing wave, a blocker
/// clearing, a shadow animal turning back into a friendly one, the hearts when you pet an animal).
/// Make one with HealSpark.Spawn; it removes itself.
/// </summary>
public class HealSpark : MonoBehaviour
{
    private Vector2 vel;
    private float life, age, size;
    private Color color;
    private SpriteRenderer sr;

    /// <summary>A speck at 'pos' moving at 'velocity' (units per second) that fades out over 'lifetime' seconds.</summary>
    public static void Spawn(Vector3 pos, Vector2 velocity, float lifetime, float size, Color color, int sortingOrder, Sprite picture = null)
    {
        var go = new GameObject("HealSpark");
        go.transform.position = pos;
        var s = go.AddComponent<HealSpark>();
        s.vel = velocity; s.life = Mathf.Max(0.05f, lifetime); s.size = size; s.color = color;
        s.sr = go.AddComponent<SpriteRenderer>();
        s.sr.sprite = picture != null ? picture : RuntimeSprites.Circle;
        s.sr.sortingOrder = sortingOrder;
        s.sr.color = color;
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<LightingExempt>().skipDarkness = true;      // glows in the dark
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float k = age / life;
        transform.position += (Vector3)(vel * Time.deltaTime);
        vel *= 1f - Time.deltaTime * 1.5f;                            // slows down as it drifts
        transform.localScale = Vector3.one * size * (1f - k * 0.5f);
        Color c = color; c.a = color.a * (1f - k);
        sr.color = c;
    }
}
