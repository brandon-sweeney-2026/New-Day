using UnityEngine;

/// <summary>
/// Makes a picture sway as if a light wind is blowing: tree tops, bushes, flowers, hanging signs...
/// Put it on the object that should move. The picture turns a little around its PIVOT, so give plants a pivot at the
/// bottom (their base stays still and the top moves). For a tree, put it on the leafy top only, not on the trunk.
///
/// The wind comes in slow gusts that roll across the map from left to right, so a row of plants ripples one after the other
/// instead of all moving together. Every number is in the Inspector.
/// </summary>
public class WindSway : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("How far it leans each way, in degrees. Flowers: about 5. Bushes: about 1.5. Tree tops: about 1.5.")]
    public float maxAngle = 3f;
    [Tooltip("How fast it rocks back and forth.")]
    public float speed = 1.4f;
    [Range(0f, 0.1f), Tooltip("How much it stretches and squashes while it moves (0 = not at all). Good for bushes and tree tops.")]
    public float stretch = 0.015f;

    [Header("Gusts")]
    [Range(0f, 1f), Tooltip("0 = a steady breeze. 1 = calm moments and strong gusts.")]
    public float gustiness = 0.6f;

    [Header("Performance")]
    [Tooltip("Stand still while off screen (saves work when there are hundreds of plants).")]
    public bool onlyWhenVisible = true;

    /// <summary>Multiplies every WindSway in the game. 1 = normal, 0 = no wind, 2 = a windy day. Other scripts (weather) can change it.</summary>
    public static float WindStrength = 1f;

    private Quaternion baseRotation;
    private Vector3 baseScale;
    private float phase;          // makes each plant start at a different point of its rocking
    private Renderer rend;

    private void Awake()
    {
        baseRotation = transform.localRotation;
        baseScale = transform.localScale;
        rend = GetComponent<Renderer>();
        // Neighbouring plants get different timing, but the same plant always gets the same one.
        phase = transform.position.x * 0.73f + transform.position.y * 1.31f + Random.value * 2f;
    }

    private void Update()
    {
        if (onlyWhenVisible && rend != null && !rend.isVisible) return;

        float t = Time.time;
        Vector3 p = transform.position;

        // A gust is a slow wave of "how windy is it here right now" (0 to 1) that drifts across the map.
        float gust = Mathf.PerlinNoise(t * 0.22f - p.x * 0.05f, p.y * 0.05f + 7.3f);
        float strength = Mathf.Lerp(1f - gustiness, 1f + gustiness, gust) * WindStrength;

        // Rock back and forth, and lean a bit with the gust.
        float angle = Mathf.Sin(t * speed + phase) * maxAngle * strength
                    - (gust - 0.4f) * maxAngle * gustiness * WindStrength;
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);

        if (stretch > 0f)
        {
            float s = Mathf.Sin(t * speed * 1.7f + phase * 1.3f) * stretch * strength;
            transform.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s * 0.6f), baseScale.z);
        }
    }

    private void OnDisable()
    {
        // Put it back exactly as it was, so a disabled plant is never left leaning.
        transform.localRotation = baseRotation;
        transform.localScale = baseScale;
    }
}
